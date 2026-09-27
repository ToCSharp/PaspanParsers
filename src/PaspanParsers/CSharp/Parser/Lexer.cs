using System.Buffers;
using System.Globalization;
using System.Text;

namespace PaspanParsers.CSharp;

/// <summary>
/// Scanning primitives for C# tokens over UTF-8 input, following the lexical grammar of the
/// C# specification. Every method inspects a span that starts at the candidate token and
/// returns the token length in bytes (0 when there is no such token).
/// </summary>
internal static class Lexer
{
    /// <summary>
    /// Reserved keywords: never identifiers unless written with the '@' prefix.
    /// </summary>
    public static readonly HashSet<string> ReservedKeywords =
    [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum",
        "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto",
        "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
        "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
        "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
        "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
        "__arglist", "__makeref", "__reftype", "__refvalue",
    ];

    // ========================================
    // Characters
    // ========================================

    /// <summary>
    /// Decodes the UTF-8 character at <paramref name="index"/>; returns -1 for malformed input.
    /// </summary>
    public static int DecodeChar(ReadOnlySpan<byte> s, int index, out int length)
    {
        var b = s[index];
        if (b < 0x80)
        {
            length = 1;
            return b;
        }

        if (Rune.DecodeFromUtf8(s[index..], out var rune, out length) == OperationStatus.Done)
        {
            return rune.Value;
        }

        length = 1;
        return -1;
    }

    public static bool IsNewLine(int c) => c is '\r' or '\n' or '\u0085' or 0x2028 or 0x2029;

    public static bool IsWhiteSpace(int c)
    {
        if (c < 0x80)
        {
            return c is ' ' or '\t' or '\v' or '\f';
        }

        return c == 0xFEFF || (c <= 0xFFFF && CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.SpaceSeparator);
    }

    public static bool IsIdentifierStart(int c)
    {
        if (c < 0x80)
        {
            return c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_';
        }

        return CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter
            or UnicodeCategory.LetterNumber;
    }

    public static bool IsIdentifierPart(int c)
    {
        if (c < 0x80)
        {
            return c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_';
        }

        return IsIdentifierStart(c) || CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.DecimalDigitNumber
            or UnicodeCategory.ConnectorPunctuation or UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.Format;
    }

    private static bool IsFormatCharacter(int c) => c >= 0x80 && CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format;

    public static bool IsDecimalDigit(byte b) => b is >= (byte)'0' and <= (byte)'9';

    public static bool IsHexDigit(byte b) => b is (>= (byte)'0' and <= (byte)'9') or (>= (byte)'a' and <= (byte)'f') or (>= (byte)'A' and <= (byte)'F');

    private static int HexValue(byte b) => b <= '9' ? b - '0' : (b | 0x20) - 'a' + 10;

    private static bool TryParseHex(ReadOnlySpan<byte> digits, out int value)
    {
        value = 0;
        foreach (var d in digits)
        {
            if (!IsHexDigit(d))
            {
                return false;
            }

            value = (value << 4) | HexValue(d);
        }

        return true;
    }

    // ========================================
    // Trivia
    // ========================================

    /// <summary>
    /// Scans white space, new lines and comments.
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

            if (b == '/' && i + 1 < s.Length)
            {
                if (s[i + 1] == '/')
                {
                    i += 2;
                    while (i < s.Length)
                    {
                        var c = DecodeChar(s, i, out var length);
                        if (IsNewLine(c))
                        {
                            break;
                        }

                        i += length;
                    }

                    continue;
                }

                if (s[i + 1] == '*')
                {
                    var end = s[(i + 2)..].IndexOf("*/"u8);
                    if (end < 0)
                    {
                        // Unterminated comment: not valid C#, leave it for the parser to reject
                        return i;
                    }

                    i += end + 4;
                    continue;
                }
            }

            if (b >= 0x80)
            {
                var c = DecodeChar(s, i, out var length);
                if (IsWhiteSpace(c) || IsNewLine(c))
                {
                    i += length;
                    continue;
                }
            }

            break;
        }

        return i;
    }

    // ========================================
    // Identifiers and keywords
    // ========================================

    /// <summary>
    /// Reads one identifier character, which may be written as a \uXXXX or \UXXXXXXXX escape.
    /// </summary>
    private static int ReadIdentifierChar(ReadOnlySpan<byte> s, int index, out int length, out bool escaped)
    {
        escaped = false;

        if (s[index] == '\\')
        {
            if (index + 1 < s.Length && s[index + 1] is (byte)'u' or (byte)'U')
            {
                var digits = s[index + 1] == 'u' ? 4 : 8;
                if (index + 2 + digits <= s.Length && TryParseHex(s.Slice(index + 2, digits), out var c))
                {
                    length = 2 + digits;
                    escaped = true;
                    return c;
                }
            }

            length = 0;
            return -1;
        }

        return DecodeChar(s, index, out length);
    }

    /// <summary>
    /// Scans an identifier or keyword, including the '@' prefix of verbatim identifiers.
    /// </summary>
    public static int ScanIdentifierOrKeyword(ReadOnlySpan<byte> s, out bool isVerbatim, out bool hasEscape)
    {
        isVerbatim = false;
        hasEscape = false;

        var i = 0;
        if (s.Length > 0 && s[0] == '@')
        {
            isVerbatim = true;
            i = 1;
        }

        if (i >= s.Length)
        {
            return 0;
        }

        var c = ReadIdentifierChar(s, i, out var length, out var escaped);
        if (c < 0 || !IsIdentifierStart(c))
        {
            return 0;
        }

        hasEscape |= escaped;
        i += length;

        while (i < s.Length)
        {
            c = ReadIdentifierChar(s, i, out length, out escaped);
            if (c < 0 || !IsIdentifierPart(c))
            {
                break;
            }

            hasEscape |= escaped;
            i += length;
        }

        return i;
    }

    /// <summary>
    /// True when the character at <paramref name="index"/> would continue an identifier.
    /// </summary>
    public static bool IsIdentifierPartAt(ReadOnlySpan<byte> s, int index)
    {
        if (index >= s.Length)
        {
            return false;
        }

        var c = ReadIdentifierChar(s, index, out _, out _);
        return c >= 0 && IsIdentifierPart(c);
    }

    /// <summary>
    /// The value of an identifier token: without '@', with escapes decoded and formatting characters removed.
    /// </summary>
    public static string IdentifierValue(ReadOnlySpan<byte> token, bool isVerbatim, bool hasEscape)
    {
        if (isVerbatim)
        {
            token = token[1..];
        }

        if (!hasEscape && token.IndexOfAnyExceptInRange((byte)0, (byte)0x7F) < 0)
        {
            return Encoding.UTF8.GetString(token);
        }

        var builder = new StringBuilder(token.Length);
        var i = 0;
        while (i < token.Length)
        {
            var c = ReadIdentifierChar(token, i, out var length, out _);
            if (!IsFormatCharacter(c))
            {
                builder.Append(char.ConvertFromUtf32(c));
            }

            i += length;
        }

        return builder.ToString();
    }

    // ========================================
    // Numeric literals
    // ========================================

    /// <summary>
    /// Scans an integer or real literal and computes its value with the type C# gives it.
    /// </summary>
    public static int ScanNumericLiteral(ReadOnlySpan<byte> s, out object value, out LiteralKind kind)
    {
        value = null;
        kind = LiteralKind.Integer;

        if (s.Length == 0)
        {
            return 0;
        }

        // Hexadecimal and binary integers
        if (s.Length > 2 && s[0] == '0' && (s[1] | 0x20) is (byte)'x' or (byte)'b')
        {
            var isHex = (s[1] | 0x20) == 'x';
            var i = 2;
            var digitsStart = i;
            while (i < s.Length && (s[i] == '_' || (isHex ? IsHexDigit(s[i]) : s[i] is (byte)'0' or (byte)'1')))
            {
                i++;
            }

            var digits = s[digitsStart..i];
            if (digits.IsEmpty || digits[^1] == '_' || digits.IndexOfAnyExcept((byte)'_') < 0)
            {
                return 0;
            }

            ulong number = 0;
            foreach (var d in digits)
            {
                if (d != '_')
                {
                    number = isHex ? (number << 4) | (uint)HexValue(d) : (number << 1) | (uint)(d - '0');
                }
            }

            i += ScanIntegerSuffix(s[i..], out var isUnsigned, out var isLong);
            if (IsIdentifierPartAt(s, i))
            {
                return 0;
            }

            value = IntegerValue(number, isUnsigned, isLong);
            return i;
        }

        var position = 0;
        var isReal = false;

        // Integer part: digits with '_' separators between them
        var integerLength = ScanDecimalDigits(s);
        position += integerLength;

        // Fraction: '.' must be followed by a digit, so '1.ToString()' and '1..2' keep the integer
        if (position + 1 < s.Length && s[position] == '.' && IsDecimalDigit(s[position + 1]))
        {
            isReal = true;
            position++;
            position += ScanDecimalDigits(s[position..]);
        }
        else if (integerLength == 0)
        {
            return 0;
        }

        // Exponent
        if (position < s.Length && (s[position] | 0x20) == 'e')
        {
            var exponent = position + 1;
            if (exponent < s.Length && s[exponent] is (byte)'+' or (byte)'-')
            {
                exponent++;
            }

            var exponentDigits = ScanDecimalDigits(s[exponent..]);
            if (exponentDigits > 0)
            {
                isReal = true;
                position = exponent + exponentDigits;
            }
        }

        var text = RemoveSeparators(s[..position]);

        // Real suffix
        if (position < s.Length && (s[position] | 0x20) is (byte)'f' or (byte)'d' or (byte)'m')
        {
            var suffix = (char)(s[position] | 0x20);
            position++;
            if (IsIdentifierPartAt(s, position))
            {
                return 0;
            }

            kind = LiteralKind.Real;
            value = suffix switch
            {
                'f' => float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture),
                'd' => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture),
                _ => decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture),
            };
            return position;
        }

        if (isReal)
        {
            if (IsIdentifierPartAt(s, position))
            {
                return 0;
            }

            kind = LiteralKind.Real;
            value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            return position;
        }

        position += ScanIntegerSuffix(s[position..], out var unsigned, out var @long);
        if (IsIdentifierPartAt(s, position) || !ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var integer))
        {
            return 0;
        }

        value = IntegerValue(integer, unsigned, @long);
        return position;
    }

    private static int ScanDecimalDigits(ReadOnlySpan<byte> s)
    {
        if (s.IsEmpty || !IsDecimalDigit(s[0]))
        {
            return 0;
        }

        var i = 1;
        var lastDigit = 1;
        while (i < s.Length && (IsDecimalDigit(s[i]) || s[i] == '_'))
        {
            i++;
            if (IsDecimalDigit(s[i - 1]))
            {
                lastDigit = i;
            }
        }

        // A trailing '_' is not part of the literal
        return lastDigit;
    }

    private static string RemoveSeparators(ReadOnlySpan<byte> s)
    {
        var text = Encoding.ASCII.GetString(s);
        return text.Contains('_') ? text.Replace("_", "") : text;
    }

    private static int ScanIntegerSuffix(ReadOnlySpan<byte> s, out bool isUnsigned, out bool isLong)
    {
        isUnsigned = false;
        isLong = false;

        var i = 0;
        while (i < s.Length && i < 2)
        {
            var c = s[i] | 0x20;
            if (c == 'u' && !isUnsigned)
            {
                isUnsigned = true;
            }
            else if (c == 'l' && !isLong)
            {
                isLong = true;
            }
            else
            {
                break;
            }

            i++;
        }

        return i;
    }

    /// <summary>
    /// The first of int, uint, long, ulong (restricted by the suffix) that can represent the value.
    /// </summary>
    private static object IntegerValue(ulong value, bool isUnsigned, bool isLong)
    {
        if (!isUnsigned && !isLong && value <= int.MaxValue)
        {
            return (int)value;
        }

        if (!isLong && value <= uint.MaxValue)
        {
            return (uint)value;
        }

        if (!isUnsigned && value <= long.MaxValue)
        {
            return (long)value;
        }

        return value;
    }

    // ========================================
    // Character and string literals
    // ========================================

    /// <summary>
    /// Reads a simple escape sequence starting at the backslash and appends the decoded text.
    /// </summary>
    private static int ScanEscapeSequence(ReadOnlySpan<byte> s, int index, StringBuilder value)
    {
        if (index + 1 >= s.Length)
        {
            return 0;
        }

        char? simple = s[index + 1] switch
        {
            (byte)'\'' => '\'',
            (byte)'"' => '"',
            (byte)'\\' => '\\',
            (byte)'0' => '\0',
            (byte)'a' => '\a',
            (byte)'b' => '\b',
            (byte)'e' => '\u001B',
            (byte)'f' => '\f',
            (byte)'n' => '\n',
            (byte)'r' => '\r',
            (byte)'t' => '\t',
            (byte)'v' => '\v',
            _ => null,
        };

        if (simple.HasValue)
        {
            value.Append(simple.Value);
            return 2;
        }

        switch (s[index + 1])
        {
            case (byte)'x':
            {
                var digits = 0;
                var code = 0;
                while (digits < 4 && index + 2 + digits < s.Length && IsHexDigit(s[index + 2 + digits]))
                {
                    code = (code << 4) | HexValue(s[index + 2 + digits]);
                    digits++;
                }

                if (digits == 0)
                {
                    return 0;
                }

                value.Append((char)code);
                return 2 + digits;
            }
            case (byte)'u':
            case (byte)'U':
            {
                var digits = s[index + 1] == 'u' ? 4 : 8;
                if (index + 2 + digits > s.Length || !TryParseHex(s.Slice(index + 2, digits), out var code) || code > 0x10FFFF)
                {
                    return 0;
                }

                // A \u escape is a UTF-16 code unit and may be a lone surrogate: '\uD800'
                if (code <= 0xFFFF)
                {
                    value.Append((char)code);
                }
                else
                {
                    value.Append(char.ConvertFromUtf32(code));
                }

                return 2 + digits;
            }
            default:
                return 0;
        }
    }

    /// <summary>
    /// Scans a character literal: 'a', '\n', 'A'.
    /// </summary>
    public static int ScanCharacterLiteral(ReadOnlySpan<byte> s, out char value)
    {
        value = default;
        if (s.Length < 3 || s[0] != '\'')
        {
            return 0;
        }

        int length;
        if (s[1] == '\\')
        {
            var builder = new StringBuilder(2);
            length = ScanEscapeSequence(s, 1, builder);
            if (length == 0 || builder.Length != 1)
            {
                return 0;
            }

            value = builder[0];
        }
        else
        {
            var c = DecodeChar(s, 1, out length);
            if (c < 0 || c > 0xFFFF || c == '\'' || IsNewLine(c))
            {
                return 0;
            }

            value = (char)c;
        }

        var end = 1 + length;
        return end < s.Length && s[end] == '\'' ? end + 1 : 0;
    }

    /// <summary>
    /// Scans a regular, verbatim or raw string literal and an optional u8 suffix.
    /// </summary>
    public static int ScanStringLiteral(ReadOnlySpan<byte> s, out string value, out bool isUtf8)
    {
        isUtf8 = false;

        int length;
        if (s.Length >= 3 && s[0] == '"' && s[1] == '"' && s[2] == '"')
        {
            length = ScanRawStringLiteral(s, out value);
        }
        else if (s.Length >= 2 && s[0] == '@' && s[1] == '"')
        {
            length = ScanVerbatimStringLiteral(s, out value);
        }
        else if (s.Length >= 2 && s[0] == '"')
        {
            length = ScanRegularStringLiteral(s, out value);
        }
        else
        {
            value = null;
            return 0;
        }

        if (length > 0 && length + 1 < s.Length && (s[length] | 0x20) == 'u' && s[length + 1] == '8')
        {
            isUtf8 = true;
            length += 2;
        }

        return length;
    }

    private static int ScanRegularStringLiteral(ReadOnlySpan<byte> s, out string value)
    {
        value = null;
        var builder = new StringBuilder();
        var i = 1;
        while (i < s.Length)
        {
            var b = s[i];
            if (b == '"')
            {
                value = builder.ToString();
                return i + 1;
            }

            if (b == '\\')
            {
                var length = ScanEscapeSequence(s, i, builder);
                if (length == 0)
                {
                    return 0;
                }

                i += length;
                continue;
            }

            var c = DecodeChar(s, i, out var charLength);
            if (c < 0 || IsNewLine(c))
            {
                return 0;
            }

            builder.Append(char.ConvertFromUtf32(c));
            i += charLength;
        }

        return 0;
    }

    private static int ScanVerbatimStringLiteral(ReadOnlySpan<byte> s, out string value)
    {
        value = null;
        var builder = new StringBuilder();
        var i = 2;
        while (i < s.Length)
        {
            if (s[i] == '"')
            {
                if (i + 1 < s.Length && s[i + 1] == '"')
                {
                    builder.Append('"');
                    i += 2;
                    continue;
                }

                value = builder.ToString();
                return i + 1;
            }

            var start = i;
            i++;
            while (i < s.Length && s[i] != '"')
            {
                i++;
            }

            builder.Append(Encoding.UTF8.GetString(s[start..i]));
        }

        return 0;
    }

    private static int CountRepeated(ReadOnlySpan<byte> s, int index, byte b)
    {
        var count = 0;
        while (index + count < s.Length && s[index + count] == b)
        {
            count++;
        }

        return count;
    }

    private static int ScanRawStringLiteral(ReadOnlySpan<byte> s, out string value)
    {
        value = null;
        var quotes = CountRepeated(s, 0, (byte)'"');
        var i = quotes;

        if (!IsRestOfLineWhiteSpace(s, i, out var contentStart))
        {
            // Single-line raw string: content up to the closing quotes on the same line
            var content = i;
            while (i < s.Length)
            {
                if (s[i] == '"')
                {
                    var run = CountRepeated(s, i, (byte)'"');
                    if (run == quotes)
                    {
                        value = Encoding.UTF8.GetString(s[content..i]);
                        return i + run;
                    }

                    if (run > quotes)
                    {
                        return 0;
                    }

                    i += run;
                    continue;
                }

                if (IsNewLine(DecodeChar(s, i, out var length)))
                {
                    return 0;
                }

                i += length;
            }

            return 0;
        }

        // Multi-line raw string: find the line holding only white space and the closing quotes
        var lines = new List<(int Start, int End)>();
        var lineStart = contentStart;
        while (lineStart <= s.Length)
        {
            var lineEnd = FindLineEnd(s, lineStart);
            var indentEnd = SkipWhiteSpace(s, lineStart, lineEnd);

            if (indentEnd < lineEnd && CountRepeated(s, indentEnd, (byte)'"') >= quotes)
            {
                var indentation = s[lineStart..indentEnd];
                value = RawStringValue(s, lines, indentation);
                return indentEnd + quotes;
            }

            lines.Add((lineStart, lineEnd));
            if (lineEnd >= s.Length)
            {
                return 0;
            }

            lineStart = lineEnd + NewLineLength(s, lineEnd);
        }

        return 0;
    }

    private static string RawStringValue(ReadOnlySpan<byte> s, List<(int Start, int End)> lines, ReadOnlySpan<byte> indentation)
    {
        var builder = new StringBuilder();
        for (var n = 0; n < lines.Count; n++)
        {
            var (start, end) = lines[n];
            if (n > 0)
            {
                // Keep the source new line between content lines
                var previousEnd = lines[n - 1].End;
                builder.Append(Encoding.UTF8.GetString(s[previousEnd..(previousEnd + NewLineLength(s, previousEnd))]));
            }

            var line = s[start..end];
            if (SkipWhiteSpace(line, 0, line.Length) == line.Length)
            {
                // White space only lines become empty
                continue;
            }

            builder.Append(Encoding.UTF8.GetString(line.StartsWith(indentation) ? line[indentation.Length..] : line));
        }

        return builder.ToString();
    }

    /// <summary>
    /// True when only white space follows until the end of the line; <paramref name="nextLine"/> is the next line start.
    /// </summary>
    public static bool IsRestOfLineWhiteSpace(ReadOnlySpan<byte> s, int index, out int nextLine)
    {
        var lineEnd = FindLineEnd(s, index);
        nextLine = lineEnd + (lineEnd < s.Length ? NewLineLength(s, lineEnd) : 0);
        return lineEnd < s.Length && SkipWhiteSpace(s, index, lineEnd) == lineEnd;
    }

    public static int FindLineEnd(ReadOnlySpan<byte> s, int index)
    {
        while (index < s.Length)
        {
            var c = DecodeChar(s, index, out var length);
            if (IsNewLine(c))
            {
                return index;
            }

            index += length;
        }

        return index;
    }

    public static int NewLineLength(ReadOnlySpan<byte> s, int index)
    {
        if (s[index] == '\r')
        {
            return index + 1 < s.Length && s[index + 1] == '\n' ? 2 : 1;
        }

        DecodeChar(s, index, out var length);
        return length;
    }

    public static int SkipWhiteSpace(ReadOnlySpan<byte> s, int index, int end)
    {
        while (index < end)
        {
            var c = DecodeChar(s, index, out var length);
            if (!IsWhiteSpace(c))
            {
                break;
            }

            index += length;
        }

        return index;
    }

    /// <summary>
    /// Decodes the escape sequences of a regular string fragment.
    /// </summary>
    public static string UnescapeRegular(ReadOnlySpan<byte> s)
    {
        if (s.IndexOf((byte)'\\') < 0)
        {
            return Encoding.UTF8.GetString(s);
        }

        var builder = new StringBuilder();
        var i = 0;
        while (i < s.Length)
        {
            if (s[i] == '\\')
            {
                var length = ScanEscapeSequence(s, i, builder);
                if (length > 0)
                {
                    i += length;
                    continue;
                }
            }

            var start = i;
            i++;
            while (i < s.Length && s[i] != '\\')
            {
                i++;
            }

            builder.Append(Encoding.UTF8.GetString(s[start..i]));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Length of a valid escape sequence at <paramref name="index"/>, or 0.
    /// </summary>
    public static int EscapeSequenceLength(ReadOnlySpan<byte> s, int index) => ScanEscapeSequence(s, index, new StringBuilder(2));
}
