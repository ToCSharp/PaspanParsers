using System.Runtime.CompilerServices;
using System.Text;

namespace PaspanParsers.Cpp;

// Macros: definitions and their expansion in the conditions of #if and #elif ([cpp.replace]).
internal sealed partial class Preprocessor
{
    /// <summary>
    /// A macro. <see cref="Parameters"/> is null for an object-like macro; for a variadic macro the last
    /// parameter is the variadic one: <c>__VA_ARGS__</c>, or the name of a named variadic parameter.
    /// </summary>
    private sealed class Macro(string name, string[] parameters, bool isVariadic, List<PpToken> body)
    {
        public string Name { get; } = name;
        public string[] Parameters { get; } = parameters;
        public bool IsVariadic { get; } = isVariadic;
        public List<PpToken> Body { get; } = body;

        public int ParameterIndex(PpToken token)
        {
            return Parameters == null || token.Kind != PpTokenKind.Identifier ? -1 : Array.IndexOf(Parameters, token.Text);
        }
    }

    /// <summary>
    /// Names that <c>defined</c> reports as defined without a <c>#define</c>: the macros clang computes and
    /// the operators of conditions that look like function-like macros.
    /// </summary>
    private static readonly HashSet<string> BuiltinMacros =
    [
        "__LINE__", "__FILE__", "__DATE__", "__TIME__", "__TIMESTAMP__", "__COUNTER__", "__INCLUDE_LEVEL__",
        "__BASE_FILE__", "__FILE_NAME__",
        "__has_include", "__has_include_next", "__has_cpp_attribute", "__has_c_attribute", "__has_attribute",
        "__has_declspec_attribute", "__has_builtin", "__has_feature", "__has_extension", "__has_warning",
        "__is_identifier", "__building_module",
    ];

    // The macros of the options, which every parse shares: the predefined macros of a compiler are many
    private static readonly ConditionalWeakTable<CppParseOptions, Dictionary<string, Macro>> OptionMacros = [];

    private Dictionary<string, Macro> _optionMacros;

    /// <summary>
    /// The macro <paramref name="name"/> defined at the current directive: by a <c>#define</c> of the file
    /// or, unless the file undefines it, by the options.
    /// </summary>
    private bool TryGetMacro(string name, out Macro macro)
    {
        if (_macros != null && _macros.TryGetValue(name, out macro))
        {
            return macro != null;
        }

        _optionMacros ??= OptionMacros.GetValue(_options, DefineOptionMacros);
        return _optionMacros.TryGetValue(name, out macro);
    }

    /// <summary>
    /// Defines <paramref name="macro"/> as <paramref name="name"/>, or undefines the name when it is null.
    /// </summary>
    private void SetMacro(string name, Macro macro)
    {
        (_macros ??= new Dictionary<string, Macro>(StringComparer.Ordinal))[name] = macro;
    }

    private static Dictionary<string, Macro> DefineOptionMacros(CppParseOptions options)
    {
        var macros = new Dictionary<string, Macro>(StringComparer.Ordinal);
        foreach (var (name, replacement) in options.Macros)
        {
            // NAME or F(x): the text of a #define directive
            var text = Encoding.UTF8.GetBytes($"#define {name} {replacement}");
            var tokens = ReadDirectiveTokens(text, 0, out _, out _);
            var definition = ParseDefinition(tokens, 2);
            if (definition.Macro != null)
            {
                macros[definition.Macro.Name] = definition.Macro;
            }
        }

        return macros;
    }

    /// <summary>
    /// <c>#define</c>: defines the macro and returns its definition, or null when the directive is not valid.
    /// </summary>
    private MacroDefinition Define(List<PpToken> tokens, int start)
    {
        var (macro, definition) = ParseDefinition(tokens, start);
        if (macro != null)
        {
            SetMacro(macro.Name, macro);
        }

        return definition;
    }

    /// <summary>
    /// The name, parameters and replacement list after <c>#define</c>.
    /// </summary>
    private static (Macro Macro, MacroDefinition Definition) ParseDefinition(List<PpToken> tokens, int start)
    {
        if (tokens.Count <= start || tokens[start].Kind != PpTokenKind.Identifier)
        {
            return default;
        }

        var name = tokens[start].Text;
        var i = start + 1;
        List<string> parameters = null;
        var isVariadic = false;

        // A '(' right after the name starts the parameters of a function-like macro
        if (i < tokens.Count && tokens[i].IsPunctuator("(") && !tokens[i].HasSpaceBefore)
        {
            parameters = [];
            i++;
            if (i < tokens.Count && tokens[i].IsPunctuator(")"))
            {
                i++;
            }
            else
            {
                while (true)
                {
                    if (i >= tokens.Count)
                    {
                        return default;
                    }

                    if (tokens[i].IsPunctuator("..."))
                    {
                        isVariadic = true;
                        i++;
                    }
                    else if (tokens[i].Kind == PpTokenKind.Identifier)
                    {
                        parameters.Add(tokens[i].Text);
                        i++;
                        if (i < tokens.Count && tokens[i].IsPunctuator("..."))
                        {
                            // A named variadic parameter (a GNU extension)
                            isVariadic = true;
                            i++;
                        }
                    }
                    else
                    {
                        return default;
                    }

                    if (i < tokens.Count && tokens[i].IsPunctuator(")"))
                    {
                        i++;
                        break;
                    }

                    if (isVariadic || i >= tokens.Count || !tokens[i].IsPunctuator(","))
                    {
                        return default;
                    }

                    i++;
                }
            }
        }

        var body = tokens.GetRange(i, tokens.Count - i);
        if (body.Count > 0)
        {
            body[0] = body[0].With(null, hasSpaceBefore: false);
        }

        var definition = new MacroDefinition(name, parameters?.ToArray(), isVariadic, Spell(body, 0, body.Count));
        string[] expansionParameters = null;
        if (parameters != null)
        {
            var namedVariadic = isVariadic && parameters.Count > 0 && tokens[i - 2].IsPunctuator("...") && tokens[i - 3].Kind == PpTokenKind.Identifier;
            expansionParameters = isVariadic && !namedVariadic ? [.. parameters, "__VA_ARGS__"] : [.. parameters];
        }

        return (new Macro(name, expansionParameters, isVariadic, body), definition);
    }

    private bool IsDefined(string name) => TryGetMacro(name, out _) || BuiltinMacros.Contains(name);

    /// <summary>
    /// The name after <c>#ifdef</c>, <c>#ifndef</c>, <c>#elifdef</c> or <c>#elifndef</c> is a macro.
    /// </summary>
    private bool IsDefined(List<PpToken> tokens, int start)
    {
        return tokens.Count > start && tokens[start].Kind == PpTokenKind.Identifier && IsDefined(tokens[start].Text);
    }

    // ========================================
    // Expansion
    // ========================================

    // Expansions of one condition; recursive macros are stopped by hide sets, this bounds macros that grow
    private const int ExpansionLimit = 100_000;
    private int _expansions;

    /// <summary>
    /// Replaces the macros in <paramref name="input"/>. In a condition, <c>defined</c> and the operators like
    /// <c>__has_include</c> are replaced with numbers, before the macros in their operands would be.
    /// Throws <see cref="FormatException"/> when the input is not valid.
    /// </summary>
    private List<PpToken> Expand(IReadOnlyList<PpToken> input)
    {
        EnsureSufficientStack();
        var output = new List<PpToken>();
        var pending = new Stack<PpToken>();
        for (var i = input.Count - 1; i >= 0; i--)
        {
            pending.Push(input[i]);
        }

        while (pending.Count != 0)
        {
            var token = pending.Pop();
            if (token.Kind != PpTokenKind.Identifier)
            {
                output.Add(token);
                continue;
            }

            if (token.Text == "defined")
            {
                output.Add(Number(ReadDefinedOperand(pending) ? 1 : 0, token.HasSpaceBefore));
                continue;
            }

            if (BuiltinFunctions.Contains(token.Text) && pending.TryPeek(out var open) && open.IsPunctuator("("))
            {
                pending.Pop();
                output.Add(Number(EvaluateBuiltinFunction(token.Text, ReadParenthesized(pending)), token.HasSpaceBefore));
                continue;
            }

            if (token.Text is "__LINE__" or "__COUNTER__" or "__INCLUDE_LEVEL__" && !TryGetMacro(token.Text, out _))
            {
                var value = token.Text switch
                {
                    "__LINE__" => _directiveLine,
                    "__COUNTER__" => _counter++,
                    _ => 0,
                };

                output.Add(Number(value, token.HasSpaceBefore));
                continue;
            }

            if (!TryGetMacro(token.Text, out var macro) || HideSet.Contains(token.HideSet, macro.Name))
            {
                output.Add(token);
                continue;
            }

            if (++_expansions > ExpansionLimit)
            {
                throw new FormatException("Too many macro expansions.");
            }

            List<List<PpToken>> arguments = null;
            if (macro.Parameters != null)
            {
                if (!pending.TryPeek(out var next) || !next.IsPunctuator("("))
                {
                    // The name of a function-like macro without arguments is not replaced
                    output.Add(token);
                    continue;
                }

                pending.Pop();
                arguments = ReadArguments(pending, macro);
            }

            var hideSet = HideSet.Add(token.HideSet, macro.Name);
            var replacement = Substitute(macro, macro.Body, arguments);
            for (var i = replacement.Count - 1; i >= 0; i--)
            {
                var replaced = replacement[i];
                pending.Push(replaced.With(HideSet.Union(replaced.HideSet, hideSet), i == 0 ? token.HasSpaceBefore : replaced.HasSpaceBefore));
            }
        }

        return output;
    }

    private static PpToken Number(long value, bool hasSpaceBefore) => new(PpTokenKind.Number, value.ToString(System.Globalization.CultureInfo.InvariantCulture), hasSpaceBefore);

    /// <summary>
    /// <c>defined name</c> or <c>defined ( name )</c> after <c>defined</c>.
    /// </summary>
    private bool ReadDefinedOperand(Stack<PpToken> pending)
    {
        if (!pending.TryPop(out var token))
        {
            throw new FormatException("Expected a macro name after 'defined'.");
        }

        var parenthesized = token.IsPunctuator("(");
        if (parenthesized && !pending.TryPop(out token))
        {
            throw new FormatException("Expected a macro name after 'defined'.");
        }

        if (token.Kind != PpTokenKind.Identifier || (parenthesized && !(pending.TryPop(out var close) && close.IsPunctuator(")"))))
        {
            throw new FormatException("Expected a macro name after 'defined'.");
        }

        return IsDefined(token.Text);
    }

    /// <summary>
    /// The tokens after '(' up to the matching ')', which is consumed.
    /// </summary>
    private static List<PpToken> ReadParenthesized(Stack<PpToken> pending)
    {
        var tokens = new List<PpToken>();
        var depth = 0;
        while (pending.TryPop(out var token))
        {
            if (token.IsPunctuator("("))
            {
                depth++;
            }
            else if (token.IsPunctuator(")") && depth-- == 0)
            {
                return tokens;
            }

            tokens.Add(token);
        }

        throw new FormatException("Unterminated parentheses.");
    }

    /// <summary>
    /// The arguments of a function-like macro after '(' up to the matching ')', which is consumed.
    /// </summary>
    private static List<List<PpToken>> ReadArguments(Stack<PpToken> pending, Macro macro)
    {
        var arguments = new List<List<PpToken>> { new() };
        var depth = 0;
        while (true)
        {
            if (!pending.TryPop(out var token))
            {
                throw new FormatException($"Unterminated arguments of the macro '{macro.Name}'.");
            }

            if (token.IsPunctuator("("))
            {
                depth++;
            }
            else if (token.IsPunctuator(")") && depth-- == 0)
            {
                break;
            }
            else if (token.IsPunctuator(",") && depth == 0 && !(macro.IsVariadic && arguments.Count == macro.Parameters.Length))
            {
                // The commas of the variadic argument are part of it
                arguments.Add([]);
                continue;
            }

            arguments[^1].Add(token);
        }

        if (macro.Parameters.Length == 0 && arguments is [[]])
        {
            arguments.Clear();
        }
        else if (macro.IsVariadic && arguments.Count == macro.Parameters.Length - 1)
        {
            // The variadic argument may be left out
            arguments.Add([]);
        }

        if (arguments.Count != macro.Parameters.Length)
        {
            throw new FormatException($"The macro '{macro.Name}' takes {macro.Parameters.Length} arguments, not {arguments.Count}.");
        }

        return arguments;
    }

    /// <summary>
    /// The replacement list <paramref name="body"/> of <paramref name="macro"/> with the parameters replaced by
    /// the <paramref name="arguments"/> (null for an object-like macro): stringized after '#', as written
    /// next to '##', otherwise with their macros expanded. Then '##' pastes tokens together.
    /// </summary>
    private List<PpToken> Substitute(Macro macro, IReadOnlyList<PpToken> body, List<List<PpToken>> arguments)
    {
        EnsureSufficientStack();
        var result = new List<PpToken>();
        for (var i = 0; i < body.Count; i++)
        {
            var token = body[i];
            if (arguments != null && token.IsPunctuator("#") && i + 1 < body.Count && macro.ParameterIndex(body[i + 1]) is var stringized and >= 0)
            {
                result.Add(new PpToken(PpTokenKind.String, Stringize(arguments[stringized]), token.HasSpaceBefore, token.HideSet));
                i++;
                continue;
            }

            if (arguments != null && macro.IsVariadic && token is { Kind: PpTokenKind.Identifier, Text: "__VA_OPT__" }
                && i + 1 < body.Count && body[i + 1].IsPunctuator("("))
            {
                // __VA_OPT__(content): the content when the variadic argument has tokens
                var close = MatchingParenthesis(body, i + 1);
                var content = new List<PpToken>();
                for (var k = i + 2; k < close; k++)
                {
                    content.Add(body[k]);
                }

                if (Expand(arguments[^1]).Count > 0)
                {
                    result.AddRange(Substitute(macro, content, arguments));
                }
                else
                {
                    result.Add(new PpToken(PpTokenKind.Placemarker, "", token.HasSpaceBefore));
                }

                i = close;
                continue;
            }

            var parameter = arguments != null ? macro.ParameterIndex(token) : -1;
            if (parameter < 0)
            {
                result.Add(token);
                continue;
            }

            var pasted = (i + 1 < body.Count && body[i + 1].IsPunctuator("##")) || (i > 0 && body[i - 1].IsPunctuator("##"));
            var argument = pasted ? arguments[parameter] : Expand(arguments[parameter]);
            if (argument.Count == 0)
            {
                result.Add(new PpToken(PpTokenKind.Placemarker, "", token.HasSpaceBefore));
                continue;
            }

            for (var k = 0; k < argument.Count; k++)
            {
                result.Add(k == 0 ? argument[k].With(argument[k].HideSet, token.HasSpaceBefore) : argument[k]);
            }
        }

        return Paste(result);
    }

    private static int MatchingParenthesis(IReadOnlyList<PpToken> tokens, int open)
    {
        var depth = 0;
        for (var i = open; i < tokens.Count; i++)
        {
            if (tokens[i].IsPunctuator("("))
            {
                depth++;
            }
            else if (tokens[i].IsPunctuator(")") && --depth == 0)
            {
                return i;
            }
        }

        throw new FormatException("Unterminated __VA_OPT__.");
    }

    /// <summary>
    /// Pastes the tokens around each '##' into one token and removes placemarkers.
    /// </summary>
    private static List<PpToken> Paste(List<PpToken> tokens)
    {
        var result = new List<PpToken>();
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.IsPunctuator("##") && result.Count > 0 && i + 1 < tokens.Count)
            {
                var left = result[^1];
                var right = tokens[++i];
                result[^1] = left.Kind == PpTokenKind.Placemarker ? right
                    : right.Kind == PpTokenKind.Placemarker ? left
                    : Join(left, right);
                continue;
            }

            result.Add(token);
        }

        result.RemoveAll(t => t.Kind == PpTokenKind.Placemarker);
        return result;
    }

    private static PpToken Join(PpToken left, PpToken right)
    {
        var text = left.Text + right.Text;
        var bytes = Encoding.UTF8.GetBytes(text);
        var kind = ScanToken(bytes, out var length, out var scanned);

        // Pasting that does not form one token is not valid; keep the text as one token anyway
        return length == bytes.Length
            ? new PpToken(kind, scanned, left.HasSpaceBefore, left.HideSet)
            : new PpToken(PpTokenKind.Other, text, left.HasSpaceBefore, left.HideSet);
    }

    /// <summary>
    /// The string literal of a stringized argument ([cpp.stringize]).
    /// </summary>
    private static string Stringize(List<PpToken> argument)
    {
        var builder = new StringBuilder("\"");
        for (var i = 0; i < argument.Count; i++)
        {
            var token = argument[i];
            if (i > 0 && token.HasSpaceBefore)
            {
                builder.Append(' ');
            }

            if (token.Kind is PpTokenKind.String or PpTokenKind.Character)
            {
                builder.Append(token.Text.Replace("\\", "\\\\").Replace("\"", "\\\""));
            }
            else
            {
                builder.Append(token.Text);
            }
        }

        return builder.Append('"').ToString();
    }
}
