using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace PaspanParsers.Cpp;

/// <summary>
/// The preprocessing directives of a C++ source ([cpp]) as trivia: before the source is parsed, one pass
/// in source order processes the directives of active code, keeps the macros they define for the
/// conditions of later directives, and records where each directive and each inactive branch ends. The
/// parser never sees directives: <see cref="SyntaxParser"/> skips them with <see cref="TryGetDirective"/>
/// when it scans the trivia before a token, in any order.
/// </summary>
/// <remarks>
/// A directive starts with '#' (or <c>%:</c>) that is the first token of a line: white space and comments
/// may come before it, including a block comment that starts on an earlier line after the start of that
/// line, as in clang. It ends at the end of the logical line; a block comment in it may span lines.
/// Macros are never expanded in the code: the parser sees the names of macros.
/// </remarks>
internal sealed partial class Preprocessor
{
    private sealed class Entry(PreprocessorDirective directive, int next)
    {
        public PreprocessorDirective Directive { get; } = directive;

        /// <summary>
        /// Where the trivia continues: the end of the directive's line, or for a directive that starts an
        /// inactive branch, the next directive that the preprocessor processes.
        /// </summary>
        public int Next { get; set; } = next;
    }

    private readonly CppParseOptions _options;
    private readonly Dictionary<int, Entry> _entries = [];
    // The macros that the directives of the file define, and null for those they undefine
    private Dictionary<string, Macro> _macros;

    // The conditional directives that are open: whether one of their branches was taken
    private readonly Stack<bool> _conditionals = new();

    // Inside an inactive branch: the directive whose Next ends it, and the depth of the conditionals opened in it
    private Entry _skipping;
    private int _skippedDepth;

    private int _counter;

    private Preprocessor(CppParseOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Every processed directive, in source order.
    /// </summary>
    public List<PreprocessorDirective> Directives { get; } = [];

    /// <summary>
    /// Processes the directives of <paramref name="source"/>.
    /// </summary>
    public static Preprocessor Run(ReadOnlySpan<byte> source, CppParseOptions options)
    {
        var preprocessor = new Preprocessor(options ?? CppParseOptions.Default);
        if (source.IndexOf((byte)'#') >= 0 || source.IndexOf("%:"u8) >= 0)
        {
            preprocessor.Scan(source);
        }

        return preprocessor;
    }

    /// <summary>
    /// When a directive starts at <paramref name="position"/>, returns it and the position where the trivia
    /// continues after it and after the inactive branch it starts, if any.
    /// </summary>
    public bool TryGetDirective(int position, out int next, out PreprocessorDirective directive)
    {
        if (_entries.TryGetValue(position, out var entry))
        {
            next = entry.Next;
            directive = entry.Directive;
            return true;
        }

        next = position;
        directive = null;
        return false;
    }

    // ========================================
    // Scanning
    // ========================================

    private void Scan(ReadOnlySpan<byte> s)
    {
        var i = 0;
        var atLineStart = true;
        while (i < s.Length)
        {
            var b = s[i];
            if (b is (byte)'\n' or (byte)'\r')
            {
                i++;
                atLineStart = true;
                continue;
            }

            if (b is (byte)' ' or (byte)'\t' or (byte)'\v' or (byte)'\f')
            {
                i++;
                continue;
            }

            var splice = Lexer.SpliceLength(s, i);
            if (splice > 0)
            {
                i += splice;
                continue;
            }

            if (b == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                // A line splice continues the comment on the next line
                i += 2;
                while (i < s.Length && s[i] is not ((byte)'\n' or (byte)'\r'))
                {
                    i += Math.Max(1, Lexer.SpliceLength(s, i));
                }

                continue;
            }

            if (b == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                // A block comment does not change whether the next token starts a line, even when it spans lines
                var close = s[(i + 2)..].IndexOf("*/"u8);
                if (close < 0)
                {
                    break;
                }

                i += close + 4;
                continue;
            }

            if (atLineStart && IsDirectiveStart(s, i))
            {
                i = ProcessDirective(s, i);
                atLineStart = false;
                continue;
            }

            i = SkipCode(s, i);
            atLineStart = false;
        }

        if (_skipping != null)
        {
            // An unterminated conditional
            _skipping.Next = s.Length;
        }
    }

    private static bool IsDirectiveStart(ReadOnlySpan<byte> s, int i)
    {
        // %:%: is ## and does not start a directive
        return s[i] == '#' || (s[i..].StartsWith("%:"u8) && !s[(i + 2)..].StartsWith("%:"u8));
    }

    // The bytes that end a stretch of code for SkipCode: new lines, comments, line splices and literals
    private static readonly SearchValues<byte> CodeStops = SearchValues.Create("\n\r/\\\"'"u8);

    /// <summary>
    /// Skips code, which is not at the start of a line, up to the next new line, comment or line splice.
    /// Character and string literals, which may contain those, are skipped whole: a raw string may span lines.
    /// </summary>
    private static int SkipCode(ReadOnlySpan<byte> s, int i)
    {
        // Where the bytes that were not looked at start
        var unscanned = i;
        while (true)
        {
            var next = s[i..].IndexOfAny(CodeStops);
            if (next < 0)
            {
                return s.Length;
            }

            var j = i + next;
            switch (s[j])
            {
                case (byte)'/' when j + 1 < s.Length && s[j + 1] is (byte)'/' or (byte)'*':
                case (byte)'\\' when Lexer.SpliceLength(s, j) > 0:
                case (byte)'\n' or (byte)'\r':
                    return j;
                case (byte)'"' or (byte)'\'':
                {
                    // The quote may belong to the identifier or number before it: an encoding prefix (u8"…",
                    // R"(…)"), a digit separator (1'000). Scan the tokens from the start of that word past it.
                    var start = j;
                    while (start > unscanned && IsWordByte(s[start - 1]))
                    {
                        start--;
                    }

                    while (start <= j)
                    {
                        start += TokenLength(s[start..]);
                    }

                    i = unscanned = start;
                    break;
                }

                default:
                    i = j + 1;
                    break;
            }

            if (i >= s.Length)
            {
                return s.Length;
            }
        }
    }

    private static bool IsWordByte(byte b) => Lexer.IsIdentifierPart(b) || b == '.';

    /// <summary>
    /// The length of the token at the start of <paramref name="s"/>, as far as it matters for finding
    /// directives and comments: literals, numbers and identifiers are whole tokens, anything else is one byte.
    /// </summary>
    private static int TokenLength(ReadOnlySpan<byte> s)
    {
        var length = Lexer.ScanQuotedLiteral(s, out _);
        if (length > 0)
        {
            return length;
        }

        length = Lexer.ScanNumber(s, out _);
        if (length > 0)
        {
            return length;
        }

        length = Lexer.ScanIdentifier(s);
        return length > 0 ? length : 1;
    }

    // ========================================
    // Directives
    // ========================================

    private static readonly Dictionary<string, PreprocessorDirectiveKind> DirectiveKinds = new(StringComparer.Ordinal)
    {
        ["if"] = PreprocessorDirectiveKind.If,
        ["ifdef"] = PreprocessorDirectiveKind.Ifdef,
        ["ifndef"] = PreprocessorDirectiveKind.Ifndef,
        ["elif"] = PreprocessorDirectiveKind.Elif,
        ["elifdef"] = PreprocessorDirectiveKind.Elifdef,
        ["elifndef"] = PreprocessorDirectiveKind.Elifndef,
        ["else"] = PreprocessorDirectiveKind.Else,
        ["endif"] = PreprocessorDirectiveKind.Endif,
        ["define"] = PreprocessorDirectiveKind.Define,
        ["undef"] = PreprocessorDirectiveKind.Undef,
        ["include"] = PreprocessorDirectiveKind.Include,
        ["include_next"] = PreprocessorDirectiveKind.IncludeNext,
        ["import"] = PreprocessorDirectiveKind.Import,
        ["embed"] = PreprocessorDirectiveKind.Embed,
        ["line"] = PreprocessorDirectiveKind.Line,
        ["pragma"] = PreprocessorDirectiveKind.Pragma,
        ["error"] = PreprocessorDirectiveKind.Error,
        ["warning"] = PreprocessorDirectiveKind.Warning,
        ["ident"] = PreprocessorDirectiveKind.Ident,
    };

    /// <summary>
    /// Processes the directive at <paramref name="hash"/> and returns the end of its line.
    /// </summary>
    private int ProcessDirective(ReadOnlySpan<byte> s, int hash)
    {
        var tokens = ReadDirectiveTokens(s, hash, out var lastTokenEnd, out var lineEnd);

        // tokens[0] is '#'
        var nameToken = tokens.Count > 1 ? tokens[1] : null;
        var kind = nameToken switch
        {
            null => PreprocessorDirectiveKind.Null,
            { Kind: PpTokenKind.Number } => PreprocessorDirectiveKind.Line,
            { Kind: PpTokenKind.Identifier } => DirectiveKinds.GetValueOrDefault(nameToken.Text, PreprocessorDirectiveKind.Other),
            _ => PreprocessorDirectiveKind.Other,
        };

        var name = nameToken?.Kind == PpTokenKind.Identifier ? nameToken.Text : "";
        var argumentStart = name.Length == 0 ? 1 : 2;

        if (_skipping != null)
        {
            ProcessSkippedDirective(s, kind, name, tokens, argumentStart, hash, lastTokenEnd, lineEnd);
            return lineEnd;
        }

        var taken = false;
        MacroDefinition macro = null;
        switch (kind)
        {
            case PreprocessorDirectiveKind.If:
                taken = EvaluateCondition(s, hash, tokens, argumentStart);
                _conditionals.Push(taken);
                break;
            case PreprocessorDirectiveKind.Ifdef:
            case PreprocessorDirectiveKind.Ifndef:
                taken = IsDefined(tokens, argumentStart) == (kind == PreprocessorDirectiveKind.Ifdef);
                _conditionals.Push(taken);
                break;
            case PreprocessorDirectiveKind.Endif:
                _conditionals.TryPop(out _);
                break;
            case PreprocessorDirectiveKind.Define:
                macro = Define(tokens, argumentStart);
                break;
            case PreprocessorDirectiveKind.Undef:
                if (tokens.Count > argumentStart && tokens[argumentStart].Kind == PpTokenKind.Identifier)
                {
                    SetMacro(tokens[argumentStart].Text, null);
                }

                break;
        }

        // In active code, #elif and #else end the branch that was taken
        var entry = Record(s, kind, name, tokens, argumentStart, hash, lastTokenEnd, lineEnd, taken, macro);
        if (kind is PreprocessorDirectiveKind.If or PreprocessorDirectiveKind.Ifdef or PreprocessorDirectiveKind.Ifndef && !taken
            || kind is PreprocessorDirectiveKind.Elif or PreprocessorDirectiveKind.Elifdef or PreprocessorDirectiveKind.Elifndef or PreprocessorDirectiveKind.Else && _conditionals.Count > 0)
        {
            _skipping = entry;
            _skippedDepth = 0;
        }

        return lineEnd;
    }

    /// <summary>
    /// A directive in an inactive branch: only conditional directives of the same level are processed.
    /// </summary>
    private void ProcessSkippedDirective(
        ReadOnlySpan<byte> s, PreprocessorDirectiveKind kind, string name, List<PpToken> tokens, int argumentStart, int hash, int lastTokenEnd, int lineEnd)
    {
        switch (kind)
        {
            case PreprocessorDirectiveKind.If or PreprocessorDirectiveKind.Ifdef or PreprocessorDirectiveKind.Ifndef:
                _skippedDepth++;
                return;
            case PreprocessorDirectiveKind.Endif when _skippedDepth > 0:
                _skippedDepth--;
                return;
            case PreprocessorDirectiveKind.Elif or PreprocessorDirectiveKind.Elifdef or PreprocessorDirectiveKind.Elifndef
                or PreprocessorDirectiveKind.Else or PreprocessorDirectiveKind.Endif when _skippedDepth == 0:
                break;
            default:
                return;
        }

        var taken = false;
        if (kind != PreprocessorDirectiveKind.Endif && _conditionals.TryPeek(out var branchTaken) && !branchTaken)
        {
            taken = kind switch
            {
                PreprocessorDirectiveKind.Elif => EvaluateCondition(s, hash, tokens, argumentStart),
                PreprocessorDirectiveKind.Elifdef => IsDefined(tokens, argumentStart),
                PreprocessorDirectiveKind.Elifndef => !IsDefined(tokens, argumentStart),
                _ => true,
            };
        }

        var entry = Record(s, kind, name, tokens, argumentStart, hash, lastTokenEnd, lineEnd, taken, null);
        _skipping.Next = hash;
        _skipping = entry;

        if (kind == PreprocessorDirectiveKind.Endif)
        {
            _conditionals.TryPop(out _);
            _skipping = null;
        }
        else if (taken)
        {
            _conditionals.Pop();
            _conditionals.Push(true);
            _skipping = null;
        }
    }

    private Entry Record(
        ReadOnlySpan<byte> s, PreprocessorDirectiveKind kind, string name, List<PpToken> tokens, int argumentStart,
        int hash, int lastTokenEnd, int lineEnd, bool taken, MacroDefinition macro)
    {
        var directive = new PreprocessorDirective(kind, name, Spell(tokens, argumentStart, tokens.Count), Encoding.UTF8.GetString(s[hash..lastTokenEnd]))
        {
            Span = new TextSpan(hash, lastTokenEnd),
            IsBranchTaken = taken,
            Macro = macro,
        };

        Directives.Add(directive);
        var entry = new Entry(directive, lineEnd);
        _entries[hash] = entry;
        return entry;
    }

    /// <summary>
    /// The tokens of the directive at <paramref name="hash"/>, starting with '#'; the end of its last token;
    /// and the end of its line: the new line after it or the end of the input.
    /// </summary>
    private static List<PpToken> ReadDirectiveTokens(ReadOnlySpan<byte> s, int hash, out int lastTokenEnd, out int lineEnd)
    {
        var tokens = new List<PpToken>();
        lastTokenEnd = hash;
        var (line, positions, end) = Lexer.LogicalLine(s, hash);
        var j = 0;
        var space = false;
        while (j < line.Length)
        {
            var b = line[j];
            if (b is (byte)' ' or (byte)'\t' or (byte)'\v' or (byte)'\f')
            {
                j++;
                space = true;
                continue;
            }

            if (b == '/' && j + 1 < line.Length && line[j + 1] == '/')
            {
                break;
            }

            if (b == '/' && j + 1 < line.Length && line[j + 1] == '*')
            {
                space = true;
                var close = line.AsSpan(j + 2).IndexOf("*/"u8);
                if (close >= 0)
                {
                    j += close + 4;
                    continue;
                }

                // The comment continues on the next lines, and so does the directive
                var from = positions[j + 1] + 1;
                var rawClose = s[from..].IndexOf("*/"u8);
                if (rawClose < 0)
                {
                    end = s.Length;
                    break;
                }

                (line, positions, end) = Lexer.LogicalLine(s, from + rawClose + 2);
                j = 0;
                continue;
            }

            var kind = ScanToken(line.AsSpan(j), out var length, out var text);
            tokens.Add(new PpToken(kind, text, space));
            space = false;
            lastTokenEnd = positions[j + length - 1] + 1;
            j += length;
        }

        lineEnd = end;
        return tokens;
    }

    /// <summary>
    /// The preprocessing token at the start of <paramref name="s"/>.
    /// </summary>
    private static PpTokenKind ScanToken(ReadOnlySpan<byte> s, out int length, out string text)
    {
        length = Lexer.ScanQuotedLiteral(s, out var isString);
        if (length > 0)
        {
            text = Encoding.UTF8.GetString(s[..length]);
            return isString ? PpTokenKind.String : PpTokenKind.Character;
        }

        length = Lexer.ScanNumber(s, out _);
        if (length > 0)
        {
            text = Encoding.UTF8.GetString(s[..length]);
            return PpTokenKind.Number;
        }

        length = Lexer.ScanIdentifier(s);
        if (length > 0)
        {
            text = Lexer.IdentifierValue(s[..length]);
            if (Lexer.AlternativeTokens.TryGetValue(text, out var @operator))
            {
                text = @operator;
                return PpTokenKind.Punctuator;
            }

            return PpTokenKind.Identifier;
        }

        // The parser composes '>>', '>=' and '>>=' from single '>' tokens; here they are whole
        if (s[0] == '>')
        {
            length = s.StartsWith(">>="u8) ? 3 : s.StartsWith(">>"u8) || s.StartsWith(">="u8) ? 2 : 1;
            text = Encoding.ASCII.GetString(s[..length]);
            return PpTokenKind.Punctuator;
        }

        length = Lexer.ScanPunctuator(s, out text);
        if (length > 0)
        {
            return PpTokenKind.Punctuator;
        }

        length = 1;
        text = Encoding.Latin1.GetString(s[..1]);
        return PpTokenKind.Other;
    }

    /// <summary>
    /// The text of <paramref name="tokens"/> from <paramref name="start"/> to <paramref name="end"/>, with a
    /// single space where the source has white space between them.
    /// </summary>
    private static string Spell(IReadOnlyList<PpToken> tokens, int start, int end)
    {
        var builder = new StringBuilder();
        for (var i = start; i < end; i++)
        {
            if (i > start && tokens[i].HasSpaceBefore)
            {
                builder.Append(' ');
            }

            builder.Append(tokens[i].Text);
        }

        return builder.ToString();
    }

    private static void EnsureSufficientStack() => RuntimeHelpers.EnsureSufficientExecutionStack();
}

internal enum PpTokenKind : byte
{
    Identifier,
    Number,
    Character,
    String,
    Punctuator,
    Other,

    /// <summary>An empty macro argument next to <c>##</c>.</summary>
    Placemarker,
}

/// <summary>
/// A preprocessing token of a directive or a macro expansion.
/// </summary>
internal sealed class PpToken(PpTokenKind kind, string text, bool hasSpaceBefore, HideSet hideSet = null)
{
    public PpTokenKind Kind { get; } = kind;
    public string Text { get; } = text;
    public bool HasSpaceBefore { get; } = hasSpaceBefore;

    /// <summary>The macros whose expansion produced the token, which do not expand it again.</summary>
    public HideSet HideSet { get; } = hideSet;

    public bool IsPunctuator(string text) => Kind == PpTokenKind.Punctuator && Text == text;

    public PpToken With(HideSet hideSet, bool hasSpaceBefore) => new(Kind, Text, hasSpaceBefore, hideSet);

    public override string ToString() => Text;
}

/// <summary>
/// An immutable set of macro names, as a list.
/// </summary>
internal sealed class HideSet(string name, HideSet next)
{
    public string Name { get; } = name;
    public HideSet Next { get; } = next;

    public static bool Contains(HideSet set, string name)
    {
        for (var current = set; current != null; current = current.Next)
        {
            if (current.Name == name)
            {
                return true;
            }
        }

        return false;
    }

    public static HideSet Add(HideSet set, string name) => Contains(set, name) ? set : new HideSet(name, set);

    public static HideSet Union(HideSet first, HideSet second)
    {
        // Sets are immutable and shared: the tokens of a replacement list have no hide set of their own
        if (first == null || first == second)
        {
            return second;
        }

        var result = first;
        for (var current = second; current != null; current = current.Next)
        {
            result = Add(result, current.Name);
        }

        return result;
    }
}
