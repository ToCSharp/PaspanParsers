namespace PaspanParsers.Cpp;

/// <summary>
/// Scanning primitives for C++ tokens over UTF-8 input, following the lexical grammar of the C++
/// standard ([lex]). Every method inspects a span that starts at the candidate token and returns the
/// token length in bytes (0 when there is no such token).
/// </summary>
internal static class Lexer
{
    /// <summary>
    /// The keywords of C++23 ([lex.key]). The alternative tokens (<c>and</c>, <c>bitor</c>, ...) are
    /// operators, see <see cref="AlternativeTokens"/>.
    /// </summary>
    public static readonly HashSet<string> Keywords =
    [
        "alignas", "alignof", "asm", "auto", "bool", "break", "case", "catch", "char", "char8_t",
        "char16_t", "char32_t", "class", "concept", "const", "consteval", "constexpr", "constinit", "const_cast", "continue",
        "co_await", "co_return", "co_yield", "decltype", "default", "delete", "do", "double", "dynamic_cast", "else",
        "enum", "explicit", "export", "extern", "false", "float", "for", "friend", "goto", "if",
        "inline", "int", "long", "mutable", "namespace", "new", "noexcept", "nullptr", "operator", "private",
        "protected", "public", "register", "reinterpret_cast", "requires", "return", "short", "signed", "sizeof", "static",
        "static_assert", "static_cast", "struct", "switch", "template", "this", "thread_local", "throw", "true", "try",
        "typedef", "typeid", "typename", "union", "unsigned", "using", "virtual", "void", "volatile", "wchar_t",
        "while",
    ];

    /// <summary>
    /// Alternative tokens ([lex.digraph]) and the operators they stand for.
    /// </summary>
    public static readonly Dictionary<string, string> AlternativeTokens = new(StringComparer.Ordinal)
    {
        ["and"] = "&&",
        ["and_eq"] = "&=",
        ["bitand"] = "&",
        ["bitor"] = "|",
        ["compl"] = "~",
        ["not"] = "!",
        ["not_eq"] = "!=",
        ["or"] = "||",
        ["or_eq"] = "|=",
        ["xor"] = "^",
        ["xor_eq"] = "^=",
    };

    // ========================================
    // Characters
    // ========================================

    public static bool IsDecimalDigit(byte b) => b is >= (byte)'0' and <= (byte)'9';

    public static bool IsIdentifierStart(byte b) => b is (>= (byte)'a' and <= (byte)'z') or (>= (byte)'A' and <= (byte)'Z') or (byte)'_' or (byte)'$' or >= 0x80;

    public static bool IsIdentifierPart(byte b) => IsIdentifierStart(b) || IsDecimalDigit(b);

    /// <summary>
    /// The length of a line splice (a backslash and a new line, [lex.phases]) at <paramref name="index"/>, or 0.
    /// </summary>
    public static int SpliceLength(ReadOnlySpan<byte> s, int index)
    {
        if (index >= s.Length || s[index] != '\\')
        {
            return 0;
        }

        var i = index + 1;
        if (i < s.Length && s[i] == '\r')
        {
            return i + 1 < s.Length && s[i + 1] == '\n' ? 3 : 2;
        }

        return i < s.Length && s[i] == '\n' ? 2 : 0;
    }

    // ========================================
    // Trivia
    // ========================================

    /// <summary>
    /// White space, new lines, line splices and comments.
    /// </summary>
    public static int ScanTrivia(ReadOnlySpan<byte> s)
    {
        var i = 0;
        while (i < s.Length)
        {
            var b = s[i];

            if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\v' or (byte)'\f')
            {
                i++;
                continue;
            }

            var splice = SpliceLength(s, i);
            if (splice != 0)
            {
                i += splice;
                continue;
            }

            if (b == '/' && i + 1 < s.Length)
            {
                if (s[i + 1] == '/')
                {
                    // A line splice continues the comment on the next line
                    i += 2;
                    while (i < s.Length && s[i] != '\n' && s[i] != '\r')
                    {
                        i += Math.Max(1, SpliceLength(s, i));
                    }

                    continue;
                }

                if (s[i + 1] == '*')
                {
                    var end = s[(i + 2)..].IndexOf("*/"u8);
                    if (end < 0)
                    {
                        // Unterminated comment: not valid C++, leave it for the parser to reject
                        return i;
                    }

                    i += end + 4;
                    continue;
                }
            }

            break;
        }

        return i;
    }

    // ========================================
    // Identifiers
    // ========================================

    public static int ScanIdentifier(ReadOnlySpan<byte> s)
    {
        if (s.IsEmpty || !IsIdentifierStart(s[0]))
        {
            return 0;
        }

        var i = 1;
        while (i < s.Length && IsIdentifierPart(s[i]))
        {
            i++;
        }

        return i;
    }

    // ========================================
    // Literals
    // ========================================

    /// <summary>
    /// A preprocessing number ([lex.ppnumber]): a digit or '.' and a digit, then digits, identifier characters,
    /// digit separators, '.' and signs after an exponent letter. <paramref name="isFloating"/> tells a
    /// floating literal from an integer one.
    /// </summary>
    public static int ScanNumber(ReadOnlySpan<byte> s, out bool isFloating)
    {
        isFloating = false;
        if (s.IsEmpty || !(IsDecimalDigit(s[0]) || (s[0] == '.' && s.Length > 1 && IsDecimalDigit(s[1]))))
        {
            return 0;
        }

        var isHex = s.Length > 1 && s[0] == '0' && s[1] is (byte)'x' or (byte)'X';
        var i = 0;
        while (i < s.Length)
        {
            var b = s[i];
            if (b is (byte)'+' or (byte)'-')
            {
                var previous = s[i - 1];
                var isExponent = isHex ? previous is (byte)'p' or (byte)'P' : previous is (byte)'e' or (byte)'E';
                if (!isExponent)
                {
                    break;
                }

                isFloating = true;
                i++;
                continue;
            }

            if (b == '\'' && i + 1 < s.Length && IsIdentifierPart(s[i + 1]))
            {
                // A digit separator
                i += 2;
                continue;
            }

            if (b == '.')
            {
                isFloating = true;
                i++;
                continue;
            }

            if (!IsIdentifierPart(b))
            {
                break;
            }

            if (isHex ? b is (byte)'p' or (byte)'P' : b is (byte)'e' or (byte)'E' && !IsInSuffix(s, i))
            {
                isFloating = true;
            }

            i++;
        }

        return i;
    }

    /// <summary>
    /// True when the 'e' at <paramref name="index"/> of a decimal number follows a suffix or a user-defined
    /// suffix starts before it (<c>1_e</c>), so it is no exponent.
    /// </summary>
    private static bool IsInSuffix(ReadOnlySpan<byte> s, int index)
    {
        for (var i = 0; i < index; i++)
        {
            if (s[i] is not ((>= (byte)'0' and <= (byte)'9') or (byte)'\'' or (byte)'.'))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The length of an encoding prefix (<c>u8</c>, <c>u</c>, <c>U</c>, <c>L</c>) followed by an optional
    /// <c>R</c> of a raw string, before a quote; 0 when the text at <paramref name="s"/> is no such prefix.
    /// </summary>
    public static int ScanLiteralPrefix(ReadOnlySpan<byte> s, out bool isRaw)
    {
        isRaw = false;
        var i = 0;
        if (s.StartsWith("u8"u8))
        {
            i = 2;
        }
        else if (!s.IsEmpty && s[0] is (byte)'u' or (byte)'U' or (byte)'L')
        {
            i = 1;
        }

        if (i < s.Length && s[i] == 'R' && i + 1 < s.Length && s[i + 1] == '"')
        {
            isRaw = true;
            return i + 1;
        }

        return i < s.Length && s[i] is (byte)'"' or (byte)'\'' ? i : 0;
    }

    /// <summary>
    /// A character or string literal with an optional encoding prefix and user-defined suffix, or a raw
    /// string literal. <paramref name="isString"/> tells strings from characters.
    /// </summary>
    public static int ScanQuotedLiteral(ReadOnlySpan<byte> s, out bool isString)
    {
        isString = false;
        var prefix = ScanLiteralPrefix(s, out var isRaw);
        if (prefix == 0 && (s.IsEmpty || s[0] is not ((byte)'"' or (byte)'\'')))
        {
            return 0;
        }

        var i = prefix;
        var quote = s[i];
        isString = quote == '"';

        if (isRaw)
        {
            // R"delimiter( ... )delimiter"
            var open = s[(i + 1)..].IndexOf((byte)'(');
            if (open < 0 || open > 16)
            {
                return 0;
            }

            var delimiter = s.Slice(i + 1, open);
            var body = i + 1 + open + 1;
            while (true)
            {
                var close = s[body..].IndexOf((byte)')');
                if (close < 0)
                {
                    return 0;
                }

                var end = body + close + 1;
                if (s[end..].StartsWith(delimiter) && end + delimiter.Length < s.Length && s[end + delimiter.Length] == '"')
                {
                    i = end + delimiter.Length + 1;
                    break;
                }

                body = end;
            }
        }
        else
        {
            i++;
            while (true)
            {
                if (i >= s.Length || s[i] is (byte)'\n' or (byte)'\r')
                {
                    return 0;
                }

                if (s[i] == '\\')
                {
                    i += SpliceLength(s, i) is var splice and > 0 ? splice : 2;
                    continue;
                }

                if (s[i] == quote)
                {
                    i++;
                    break;
                }

                i++;
            }
        }

        // A user-defined literal suffix
        return i + ScanIdentifier(s[i..]);
    }
}
