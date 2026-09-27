namespace PaspanParsers.CSharp;

/// <summary>
/// C# trivia including preprocessor directives: white space, comments, directive lines and the
/// disabled text of inactive <c>#if</c> branches. The parser never sees directives.
/// </summary>
/// <remarks>
/// The trivia at a position is scanned without state from earlier scans, because tokens are scanned
/// lazily and out of order during lookahead:
/// <list type="bullet">
/// <item><c>#define</c> and <c>#undef</c> may only appear before the first token, so the leading
/// trivia of the file is scanned in order from the symbols of the options, and the symbols it ends with
/// apply to all later trivia.</item>
/// <item><c>#if</c> starts a branch: when its condition is false the scan skips disabled text to the
/// matching <c>#elif</c> (whose condition decides again), <c>#else</c> or <c>#endif</c>.</item>
/// <item><c>#elif</c> or <c>#else</c> reached in active code ends the branch that was taken, so the scan
/// skips to the matching <c>#endif</c>.</item>
/// </list>
/// <c>#nullable</c> directives are returned to the caller, which keeps them in the AST. Other directives
/// (<c>#region</c>, <c>#pragma</c>, <c>#line</c>, <c>#error</c>, <c>#warning</c>, <c>#!</c>, <c>#:</c>)
/// are skipped to the end of the line.
/// </remarks>
internal sealed class Preprocessor
{
    private readonly string[] _initialSymbols;
    private readonly HashSet<string> _symbols;
    private bool _leadingTriviaScanned;

    /// <param name="symbols">
    /// The symbols defined by the options. After the leading trivia is scanned the set holds the
    /// symbols in effect for the rest of the file.
    /// </param>
    public Preprocessor(HashSet<string> symbols)
    {
        _initialSymbols = [.. symbols];
        _symbols = symbols;
    }

    /// <summary>
    /// The length of the trivia at <paramref name="position"/> in <paramref name="source"/>.
    /// </summary>
    public int ScanTrivia(ReadOnlySpan<byte> source, int position) => ScanTrivia(source, position, out _);

    /// <summary>
    /// The length of the trivia at <paramref name="position"/> in <paramref name="source"/>, and the
    /// <c>#nullable</c> directives in it (null when there are none).
    /// </summary>
    public int ScanTrivia(ReadOnlySpan<byte> source, int position, out List<NullableDirective> nullableDirectives)
    {
        nullableDirectives = null;

        if (position == 0)
        {
            var symbols = new HashSet<string>(_initialSymbols, StringComparer.Ordinal);
            var length = Scan(source, 0, symbols, allowDefine: true, ref nullableDirectives);
            _symbols.Clear();
            _symbols.UnionWith(symbols);
            _leadingTriviaScanned = true;
            return length;
        }

        if (!_leadingTriviaScanned)
        {
            ScanTrivia(source, 0);
        }

        return Scan(source, position, _symbols, allowDefine: false, ref nullableDirectives);
    }

    private static int Scan(ReadOnlySpan<byte> s, int position, HashSet<string> symbols, bool allowDefine, ref List<NullableDirective> nullableDirectives)
    {
        var i = position;
        var atLineStart = IsAtLineStart(s, position);
        while (i < s.Length)
        {
            var c = Lexer.DecodeChar(s, i, out var length);

            if (Lexer.IsNewLine(c))
            {
                i += length;
                atLineStart = true;
                continue;
            }

            if (Lexer.IsWhiteSpace(c))
            {
                i += length;
                continue;
            }

            if (c == '/' && i + 1 < s.Length && s[i + 1] is (byte)'/' or (byte)'*')
            {
                var comment = Lexer.ScanTrivia(s[i..]);
                if (comment == 0)
                {
                    break;
                }

                // A block comment may end in the middle of a line; a line comment stops before the new line
                i += comment;
                atLineStart = IsAtLineStart(s, i);
                continue;
            }

            if (c == '#' && atLineStart)
            {
                i = ScanDirective(s, i, symbols, allowDefine, ref nullableDirectives);
                continue;
            }

            break;
        }

        return i - position;
    }

    /// <summary>
    /// Only white space is between the start of the line and <paramref name="position"/>.
    /// </summary>
    private static bool IsAtLineStart(ReadOnlySpan<byte> s, int position)
    {
        var i = position - 1;
        while (i >= 0 && s[i] is (byte)' ' or (byte)'\t' or (byte)'\v' or (byte)'\f')
        {
            i--;
        }

        if (i < 0 || s[i] is (byte)'\n' or (byte)'\r')
        {
            return true;
        }

        // U+0085 (C2 85), U+2028 (E2 80 A8) and U+2029 (E2 80 A9)
        return (s[i] == 0x85 && i >= 1 && s[i - 1] == 0xC2)
            || (s[i] is 0xA8 or 0xA9 && i >= 2 && s[i - 1] == 0x80 && s[i - 2] == 0xE2);
    }

    /// <summary>
    /// Processes the directive at <paramref name="hash"/> and returns the position after it: the start of
    /// the next line, or the start of the line after the disabled text it skips.
    /// </summary>
    private static int ScanDirective(ReadOnlySpan<byte> s, int hash, HashSet<string> symbols, bool allowDefine, ref List<NullableDirective> nullableDirectives)
    {
        var name = ReadDirectiveName(s, hash, out var argumentStart, out var lineEnd, out var nextLine);
        switch (name)
        {
            case "if":
                return Evaluate(s[argumentStart..lineEnd], symbols) ? nextLine : SkipDisabledText(s, nextLine, symbols);

            case "elif":
            case "else":
                return SkipToEndIf(s, nextLine);

            case "define":
            case "undef":
                if (allowDefine)
                {
                    var i = argumentStart;
                    var symbol = ReadIdentifier(s, ref i, lineEnd);
                    if (symbol != null)
                    {
                        if (name == "define")
                        {
                            symbols.Add(symbol);
                        }
                        else
                        {
                            symbols.Remove(symbol);
                        }
                    }
                }

                return nextLine;

            case "nullable":
            {
                var i = argumentStart;
                var directive = ReadNullableDirective(s, ref i, lineEnd);
                if (directive != null)
                {
                    (nullableDirectives ??= []).Add(directive);
                }

                return nextLine;
            }

            default:
                return nextLine;
        }
    }

    /// <summary>
    /// enable | disable | restore [warnings | annotations]
    /// </summary>
    private static NullableDirective ReadNullableDirective(ReadOnlySpan<byte> s, ref int i, int end)
    {
        NullableSetting? setting = ReadIdentifier(s, ref i, end) switch
        {
            "enable" => NullableSetting.Enable,
            "disable" => NullableSetting.Disable,
            "restore" => NullableSetting.Restore,
            _ => null,
        };

        if (!setting.HasValue)
        {
            return null;
        }

        NullableTarget? target = ReadIdentifier(s, ref i, end) switch
        {
            "warnings" => NullableTarget.Warnings,
            "annotations" => NullableTarget.Annotations,
            _ => null,
        };

        return new NullableDirective(setting.Value, target);
    }

    /// <summary>
    /// Reads the name of the directive at <paramref name="hash"/> ("if", "region", "!" for <c>#!</c>, ...).
    /// </summary>
    private static string ReadDirectiveName(ReadOnlySpan<byte> s, int hash, out int argumentStart, out int lineEnd, out int nextLine)
    {
        lineEnd = Lexer.FindLineEnd(s, hash);
        nextLine = lineEnd < s.Length ? lineEnd + Lexer.NewLineLength(s, lineEnd) : lineEnd;

        var i = hash + 1;
        if (i < lineEnd && s[i] is (byte)'!' or (byte)':')
        {
            argumentStart = i + 1;
            return s[i] == '!' ? "!" : ":";
        }

        i = Lexer.SkipWhiteSpace(s, i, lineEnd);
        var start = i;
        while (i < lineEnd && s[i] is >= (byte)'a' and <= (byte)'z')
        {
            i++;
        }

        argumentStart = i;
        return System.Text.Encoding.ASCII.GetString(s[start..i]);
    }

    /// <summary>
    /// Skips the lines of a false branch and returns the start of the line after the directive that
    /// ends it: a true <c>#elif</c>, <c>#else</c> or the matching <c>#endif</c>.
    /// </summary>
    private static int SkipDisabledText(ReadOnlySpan<byte> s, int index, HashSet<string> symbols)
    {
        var depth = 0;
        while (index < s.Length)
        {
            var hash = Lexer.SkipWhiteSpace(s, index, s.Length);
            if (hash >= s.Length || s[hash] != '#')
            {
                var lineEnd = Lexer.FindLineEnd(s, index);
                index = lineEnd < s.Length ? lineEnd + Lexer.NewLineLength(s, lineEnd) : lineEnd;
                continue;
            }

            var name = ReadDirectiveName(s, hash, out var argumentStart, out var end, out var nextLine);
            switch (name)
            {
                case "if":
                    depth++;
                    break;
                case "endif":
                    if (depth == 0)
                    {
                        return nextLine;
                    }

                    depth--;
                    break;
                case "else" when depth == 0:
                    return nextLine;
                case "elif" when depth == 0:
                    if (Evaluate(s[argumentStart..end], symbols))
                    {
                        return nextLine;
                    }

                    break;
            }

            index = nextLine;
        }

        return index;
    }

    /// <summary>
    /// Skips the remaining branches of a conditional whose branch was taken, up to and including the matching <c>#endif</c>.
    /// </summary>
    private static int SkipToEndIf(ReadOnlySpan<byte> s, int index)
    {
        var depth = 0;
        while (index < s.Length)
        {
            var hash = Lexer.SkipWhiteSpace(s, index, s.Length);
            if (hash >= s.Length || s[hash] != '#')
            {
                var lineEnd = Lexer.FindLineEnd(s, index);
                index = lineEnd < s.Length ? lineEnd + Lexer.NewLineLength(s, lineEnd) : lineEnd;
                continue;
            }

            var name = ReadDirectiveName(s, hash, out _, out _, out var nextLine);
            if (name == "if")
            {
                depth++;
            }
            else if (name == "endif")
            {
                if (depth == 0)
                {
                    return nextLine;
                }

                depth--;
            }

            index = nextLine;
        }

        return index;
    }

    // ========================================
    // Conditions
    // ========================================

    /// <summary>
    /// Evaluates the condition of <c>#if</c> or <c>#elif</c>: symbols, <c>true</c>, <c>false</c>,
    /// <c>!</c>, <c>==</c>, <c>!=</c>, <c>&amp;&amp;</c>, <c>||</c> and parentheses, optionally followed by a comment.
    /// </summary>
    public static bool Evaluate(ReadOnlySpan<byte> condition, HashSet<string> symbols)
    {
        var i = 0;
        return EvaluateOr(condition, ref i, symbols);
    }

    private static bool EvaluateOr(ReadOnlySpan<byte> s, ref int i, HashSet<string> symbols)
    {
        var value = EvaluateAnd(s, ref i, symbols);
        while (TryEat(s, ref i, "||"u8))
        {
            value |= EvaluateAnd(s, ref i, symbols);
        }

        return value;
    }

    private static bool EvaluateAnd(ReadOnlySpan<byte> s, ref int i, HashSet<string> symbols)
    {
        var value = EvaluateEquality(s, ref i, symbols);
        while (TryEat(s, ref i, "&&"u8))
        {
            value &= EvaluateEquality(s, ref i, symbols);
        }

        return value;
    }

    private static bool EvaluateEquality(ReadOnlySpan<byte> s, ref int i, HashSet<string> symbols)
    {
        var value = EvaluateUnary(s, ref i, symbols);
        while (true)
        {
            if (TryEat(s, ref i, "=="u8))
            {
                value = value == EvaluateUnary(s, ref i, symbols);
            }
            else if (TryEat(s, ref i, "!="u8))
            {
                value = value != EvaluateUnary(s, ref i, symbols);
            }
            else
            {
                return value;
            }
        }
    }

    private static bool EvaluateUnary(ReadOnlySpan<byte> s, ref int i, HashSet<string> symbols)
    {
        if (TryEat(s, ref i, "!"u8))
        {
            return !EvaluateUnary(s, ref i, symbols);
        }

        if (TryEat(s, ref i, "("u8))
        {
            var value = EvaluateOr(s, ref i, symbols);
            TryEat(s, ref i, ")"u8);
            return value;
        }

        SkipSpaces(s, ref i);
        var symbol = ReadIdentifier(s, ref i, s.Length);
        return symbol switch
        {
            null => false,
            "true" => true,
            "false" => false,
            _ => symbols.Contains(symbol),
        };
    }

    private static bool TryEat(ReadOnlySpan<byte> s, ref int i, ReadOnlySpan<byte> text)
    {
        SkipSpaces(s, ref i);
        if (!s[i..].StartsWith(text))
        {
            return false;
        }

        // '!' is not the start of '!='
        if (text.Length == 1 && text[0] == '!' && i + 1 < s.Length && s[i + 1] == '=')
        {
            return false;
        }

        i += text.Length;
        return true;
    }

    private static void SkipSpaces(ReadOnlySpan<byte> s, ref int i)
    {
        i = Lexer.SkipWhiteSpace(s, i, s.Length);
    }

    private static string ReadIdentifier(ReadOnlySpan<byte> s, ref int i, int end)
    {
        i = Lexer.SkipWhiteSpace(s, i, end);
        var length = Lexer.ScanIdentifierOrKeyword(s[i..end], out var isVerbatim, out var hasEscape);
        if (length == 0)
        {
            return null;
        }

        var value = Lexer.IdentifierValue(s.Slice(i, length), isVerbatim, hasEscape);
        i += length;
        return value;
    }
}
