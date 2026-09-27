using System.Buffers;
using System.Globalization;
using System.Text;

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

    /// <summary>
    /// An identifier ([lex.name]): a start character, then continue characters. Besides ASCII letters, digits,
    /// '_' and '$' (a clang extension), these are Unicode characters of XID_Start and XID_Continue, written
    /// in UTF-8 or as universal character names (<c>\u00e9</c>, <c>\U0001F600</c>, <c>\u{e9}</c>, <c>\N{...}</c>).
    /// </summary>
    public static int ScanIdentifier(ReadOnlySpan<byte> s)
    {
        var i = 0;
        while (i < s.Length)
        {
            var b = s[i];
            if (b < 0x80 && b != '\\')
            {
                if (!(i == 0 ? IsAsciiIdentifierStart(b) : IsAsciiIdentifierStart(b) || IsDecimalDigit(b)))
                {
                    break;
                }

                i++;
                continue;
            }

            int codePoint;
            int length;
            if (b == '\\')
            {
                length = ScanUniversalCharacterName(s, i, out codePoint);
                if (length == 0)
                {
                    break;
                }
            }
            else if (Rune.DecodeFromUtf8(s[i..], out var rune, out length) == OperationStatus.Done)
            {
                codePoint = rune.Value;
            }
            else
            {
                break;
            }

            // A named character (-1) is not looked up
            if (codePoint >= 0 && !(i == 0 ? IsXidStart(codePoint) : IsXidContinue(codePoint)))
            {
                break;
            }

            i += length;
        }

        return i;
    }

    private static bool IsAsciiIdentifierStart(byte b) => b is (>= (byte)'a' and <= (byte)'z') or (>= (byte)'A' and <= (byte)'Z') or (byte)'_' or (byte)'$';

    /// <summary>
    /// XID_Start of Unicode Standard Annex #31, from the general categories and Other_ID_Start.
    /// </summary>
    public static bool IsXidStart(int codePoint)
    {
        if (codePoint is 0x1885 or 0x1886 or 0x2118 or 0x212E or 0x309B or 0x309C)
        {
            return true;
        }

        if (!Rune.IsValid(codePoint))
        {
            return false;
        }

        return Rune.GetUnicodeCategory(new Rune(codePoint)) is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber;
    }

    /// <summary>
    /// XID_Continue of Unicode Standard Annex #31, from the general categories and Other_ID_Continue.
    /// </summary>
    public static bool IsXidContinue(int codePoint)
    {
        if (IsXidStart(codePoint) || codePoint is 0x00B7 or 0x0387 or (>= 0x1369 and <= 0x1371) or 0x19DA)
        {
            return true;
        }

        return Rune.IsValid(codePoint) && Rune.GetUnicodeCategory(new Rune(codePoint)) is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.ConnectorPunctuation;
    }

    /// <summary>
    /// A universal character name at <paramref name="index"/>: <c>\uXXXX</c>, <c>\UXXXXXXXX</c>, <c>\u{X...}</c>
    /// or <c>\N{NAME}</c>. Returns its length and code point, which is -1 for a named character: .NET has no
    /// table of Unicode character names. Returns 0 when there is none.
    /// </summary>
    public static int ScanUniversalCharacterName(ReadOnlySpan<byte> s, int index, out int codePoint)
    {
        codePoint = 0;
        if (index + 1 >= s.Length || s[index] != '\\')
        {
            return 0;
        }

        var kind = s[index + 1];
        var i = index + 2;
        if (kind == 'N')
        {
            codePoint = -1;
            var close = i < s.Length && s[i] == '{' ? s[i..].IndexOf((byte)'}') : -1;
            return close > 1 && s.Slice(i + 1, close - 1).IndexOfAnyExcept(NameCharacters) < 0 ? i + close + 1 - index : 0;
        }

        if (kind == 'u' && i < s.Length && s[i] == '{')
        {
            var close = s[i..].IndexOf((byte)'}');
            if (close < 2 || !TryParseHex(s.Slice(i + 1, close - 1), out var value) || value > 0x10FFFF)
            {
                return 0;
            }

            codePoint = (int)value;
            return i + close + 1 - index;
        }

        var digits = kind switch { (byte)'u' => 4, (byte)'U' => 8, _ => 0 };
        if (digits == 0 || i + digits > s.Length || !TryParseHex(s.Slice(i, digits), out var hex) || hex > 0x10FFFF)
        {
            return 0;
        }

        codePoint = (int)hex;
        return 2 + digits;
    }

    private static readonly SearchValues<byte> NameCharacters = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 -"u8);

    public static bool IsHexDigit(byte b) => b is (>= (byte)'0' and <= (byte)'9') or (>= (byte)'a' and <= (byte)'f') or (>= (byte)'A' and <= (byte)'F');

    public static int HexValue(byte b) => b <= '9' ? b - '0' : (b | 0x20) - 'a' + 10;

    /// <summary>
    /// Parses hexadecimal digits; false when there are none, a character is not a digit or the value exceeds 64 bits.
    /// </summary>
    public static bool TryParseHex(ReadOnlySpan<byte> digits, out ulong value)
    {
        value = 0;
        if (digits.IsEmpty)
        {
            return false;
        }

        foreach (var b in digits)
        {
            if (!IsHexDigit(b) || value > ulong.MaxValue >> 4)
            {
                return false;
            }

            value = (value << 4) | (uint)HexValue(b);
        }

        return true;
    }

    /// <summary>
    /// The value of an identifier: its text with universal character names replaced by their characters
    /// (named characters stay as written).
    /// </summary>
    public static string IdentifierValue(ReadOnlySpan<byte> identifier)
    {
        if (identifier.IndexOf((byte)'\\') < 0)
        {
            return Encoding.UTF8.GetString(identifier);
        }

        var builder = new StringBuilder();
        var i = 0;
        while (i < identifier.Length)
        {
            var length = ScanUniversalCharacterName(identifier, i, out var codePoint);
            if (length > 0)
            {
                builder.Append(codePoint >= 0 ? char.ConvertFromUtf32(codePoint) : Encoding.UTF8.GetString(identifier.Slice(i, length)));
                i += length;
                continue;
            }

            var next = identifier[(i + 1)..].IndexOf((byte)'\\');
            var end = next < 0 ? identifier.Length : i + 1 + next;
            builder.Append(Encoding.UTF8.GetString(identifier[i..end]));
            i = end;
        }

        return builder.ToString();
    }

    // ========================================
    // Line splices
    // ========================================

    /// <summary>
    /// True when <paramref name="s"/> contains a line splice.
    /// </summary>
    public static bool ContainsSplice(ReadOnlySpan<byte> s)
    {
        var i = s.IndexOf((byte)'\\');
        while (i >= 0)
        {
            if (SpliceLength(s, i) > 0)
            {
                return true;
            }

            var next = s[(i + 1)..].IndexOf((byte)'\\');
            i = next < 0 ? -1 : i + 1 + next;
        }

        return false;
    }

    /// <summary>
    /// The logical line that starts at <paramref name="s"/>: its bytes up to the first new line that does not
    /// belong to a line splice, without the splices, and the index in <paramref name="s"/> of each of them.
    /// </summary>
    public static (byte[] Bytes, int[] Positions) RemoveSplices(ReadOnlySpan<byte> s)
    {
        var bytes = new List<byte>();
        var positions = new List<int>();
        var i = 0;
        while (i < s.Length)
        {
            var splice = SpliceLength(s, i);
            if (splice > 0)
            {
                i += splice;
                continue;
            }

            if (s[i] is (byte)'\n' or (byte)'\r')
            {
                break;
            }

            bytes.Add(s[i]);
            positions.Add(i);
            i++;
        }

        return (bytes.ToArray(), positions.ToArray());
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
