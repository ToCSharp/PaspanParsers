using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace PaspanParsers.Cpp;

/// <summary>
/// Builds literal nodes from the text of literal tokens and computes their values ([lex.literal]).
/// </summary>
internal static class Literals
{
    // ========================================
    // Numbers
    // ========================================

    /// <summary>
    /// An integer or floating literal: its value, its suffix and its user-defined suffix.
    /// </summary>
    public static LiteralExpression Number(string text)
    {
        var s = text.Replace("'", "");
        var radix = 10;
        var i = 0;
        if (s.Length > 1 && s[0] == '0' && s[1] is 'x' or 'X')
        {
            radix = 16;
            i = 2;
        }
        else if (s.Length > 1 && s[0] == '0' && s[1] is 'b' or 'B')
        {
            radix = 2;
            i = 2;
        }

        var integerStart = i;
        while (i < s.Length && IsDigit(s[i], radix))
        {
            i++;
        }

        var integerEnd = i;
        var fractionStart = i;
        var isFloating = false;
        if (i < s.Length && s[i] == '.' && radix != 2)
        {
            isFloating = true;
            i++;
            fractionStart = i;
            while (i < s.Length && IsDigit(s[i], radix))
            {
                i++;
            }
        }

        var fractionEnd = i;
        var exponent = 0;
        if (i < s.Length && (radix == 16 ? s[i] is 'p' or 'P' : radix == 10 && s[i] is 'e' or 'E'))
        {
            var j = i + 1;
            var negative = false;
            if (j < s.Length && s[j] is '+' or '-')
            {
                negative = s[j] == '-';
                j++;
            }

            if (j < s.Length && char.IsAsciiDigit(s[j]))
            {
                isFloating = true;
                var digitsStart = j;
                while (j < s.Length && char.IsAsciiDigit(s[j]))
                {
                    j++;
                }

                exponent = int.TryParse(s.AsSpan(digitsStart, j - digitsStart), CultureInfo.InvariantCulture, out var e) ? e : int.MaxValue;
                exponent = negative ? -exponent : exponent;
                i = j;
            }
        }

        var suffix = s[i..];
        object value;
        if (isFloating)
        {
            value = radix == 16
                ? HexFloat(s[integerStart..integerEnd], s[fractionStart..fractionEnd], exponent)
                : double.Parse(s.AsSpan(0, i), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
        else
        {
            // A decimal number that starts with 0 is octal
            var digits = s[integerStart..integerEnd];
            if (radix == 10 && digits.Length > 1 && digits[0] == '0')
            {
                radix = 8;
            }

            value = ParseInteger(digits, radix);
        }

        return new LiteralExpression(isFloating ? LiteralKind.Floating : LiteralKind.Integer, text, value)
        {
            Suffix = suffix.Length == 0 || suffix[0] == '_' ? null : suffix,
            UserDefinedSuffix = suffix.Length != 0 && suffix[0] == '_' ? suffix : null,
        };
    }

    private static bool IsDigit(char c, int radix) => radix == 16 ? char.IsAsciiHexDigit(c) : char.IsAsciiDigit(c);

    private static object ParseInteger(string digits, int radix)
    {
        ulong value = 0;
        foreach (var c in digits)
        {
            var digit = (ulong)(char.IsAsciiDigit(c) ? c - '0' : (c | 0x20) - 'a' + 10);
            if (digit >= (ulong)radix || value > (ulong.MaxValue - digit) / (ulong)radix)
            {
                return null;
            }

            value = value * (ulong)radix + digit;
        }

        return value;
    }

    private static double HexFloat(string integer, string fraction, int exponent)
    {
        var mantissa = BigInteger.Parse("0" + integer + fraction, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        return Math.ScaleB((double)mantissa, exponent - 4 * fraction.Length);
    }

    // ========================================
    // Characters and strings
    // ========================================

    /// <summary>
    /// A character literal: its encoding, value and user-defined suffix.
    /// </summary>
    public static LiteralExpression Character(string text)
    {
        var (encoding, _, body, suffix) = Split(text);
        var units = new List<uint>();
        object value = null;
        if (TryDecode(body, isRaw: false, encoding, units))
        {
            if (units.Count == 1)
            {
                value = (long)units[0];
            }
            else if (units.Count > 1 && encoding == CharacterEncoding.Ordinary)
            {
                // A multicharacter literal: an int, as GCC and clang compute it
                var multicharacter = 0;
                foreach (var unit in units)
                {
                    multicharacter = (multicharacter << 8) | (byte)unit;
                }

                value = (long)multicharacter;
            }
        }

        return new LiteralExpression(LiteralKind.Character, text, value) { Encoding = encoding, UserDefinedSuffix = suffix };
    }

    /// <summary>
    /// A string literal on its own: its encoding, value and user-defined suffix.
    /// </summary>
    public static LiteralExpression String(string text)
    {
        var (encoding, isRaw, body, suffix) = Split(text);
        return new LiteralExpression(LiteralKind.String, text, StringValue([(body, isRaw)], encoding))
        {
            Encoding = encoding,
            IsRaw = isRaw,
            UserDefinedSuffix = suffix,
        };
    }

    /// <summary>
    /// Adjacent string literals. The concatenation has the encoding of the parts with a prefix, and its
    /// parts are decoded in that encoding ([lex.string]).
    /// </summary>
    public static ConcatenatedStringExpression Concatenate(IReadOnlyList<LiteralExpression> parts)
    {
        var encoding = parts.Select(p => p.Encoding).FirstOrDefault(e => e != CharacterEncoding.Ordinary);
        var bodies = parts.Select(p => (Split(p.Text).Body, p.IsRaw)).ToList();
        return new ConcatenatedStringExpression(parts, StringValue(bodies, encoding))
        {
            Encoding = encoding,
            UserDefinedSuffix = parts.Select(p => p.UserDefinedSuffix).FirstOrDefault(s => s != null),
        };
    }

    /// <summary>
    /// The parts of a character or string literal: the encoding of its prefix, whether it is raw, the
    /// text between the quotes (the delimiters and parentheses of a raw string) and the user-defined suffix.
    /// </summary>
    private static (CharacterEncoding Encoding, bool IsRaw, byte[] Body, string Suffix) Split(string text)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var prefixLength = Lexer.ScanLiteralPrefix(bytes, out var isRaw);
        var prefix = System.Text.Encoding.UTF8.GetString(bytes, 0, prefixLength - (isRaw ? 1 : 0));
        var encoding = prefix switch
        {
            "u8" => CharacterEncoding.Utf8,
            "u" => CharacterEncoding.Utf16,
            "U" => CharacterEncoding.Utf32,
            "L" => CharacterEncoding.Wide,
            _ => CharacterEncoding.Ordinary,
        };

        var quote = bytes[prefixLength];
        var close = Array.LastIndexOf(bytes, quote);
        var body = bytes[(prefixLength + 1)..close];
        if (isRaw)
        {
            // delimiter( ... )delimiter
            var open = Array.IndexOf(body, (byte)'(');
            body = body[(open + 1)..(body.Length - open - 1)];
        }

        var suffix = close + 1 < bytes.Length ? System.Text.Encoding.UTF8.GetString(bytes, close + 1, bytes.Length - close - 1) : null;
        return (encoding, isRaw, body, suffix);
    }

    private static object StringValue(IEnumerable<(byte[] Body, bool IsRaw)> parts, CharacterEncoding encoding)
    {
        var units = new List<uint>();
        foreach (var (body, isRaw) in parts)
        {
            if (!TryDecode(body, isRaw, encoding, units))
            {
                return null;
            }
        }

        switch (encoding)
        {
            case CharacterEncoding.Ordinary:
            case CharacterEncoding.Utf8:
                return units.Select(u => (byte)u).ToArray();

            case CharacterEncoding.Utf16:
                return new string(units.Select(u => (char)u).ToArray());

            default:
            {
                var builder = new StringBuilder();
                foreach (var unit in units)
                {
                    if (!Rune.IsValid(unit))
                    {
                        return null;
                    }

                    builder.Append(new Rune(unit).ToString());
                }

                return builder.ToString();
            }
        }
    }

    /// <summary>
    /// Appends the code units of the characters and escape sequences of <paramref name="body"/> in
    /// <paramref name="encoding"/>; false when a character cannot be decoded.
    /// </summary>
    private static bool TryDecode(ReadOnlySpan<byte> body, bool isRaw, CharacterEncoding encoding, List<uint> units)
    {
        var mask = encoding switch
        {
            CharacterEncoding.Ordinary or CharacterEncoding.Utf8 => 0xFFu,
            CharacterEncoding.Utf16 => 0xFFFFu,
            _ => 0xFFFFFFFFu,
        };

        var i = 0;
        while (i < body.Length)
        {
            if (!isRaw && body[i] == '\\')
            {
                if (!TryDecodeEscape(body, ref i, out var value, out var isCodePoint))
                {
                    return false;
                }

                if (isCodePoint)
                {
                    AppendCodePoint(value, encoding, units);
                }
                else
                {
                    units.Add(value & mask);
                }

                continue;
            }

            if (Rune.DecodeFromUtf8(body[i..], out var rune, out var length) == OperationStatus.Done)
            {
                AppendCodePoint((uint)rune.Value, encoding, units);
            }
            else if (mask == 0xFF)
            {
                // Bytes that are not UTF-8 are copied to narrow strings
                units.Add(body[i]);
                length = 1;
            }
            else
            {
                return false;
            }

            i += length;
        }

        return true;
    }

    private static void AppendCodePoint(uint codePoint, CharacterEncoding encoding, List<uint> units)
    {
        switch (encoding)
        {
            case CharacterEncoding.Ordinary:
            case CharacterEncoding.Utf8:
            {
                Span<byte> bytes = stackalloc byte[4];
                var length = Rune.IsValid(codePoint) ? new Rune(codePoint).EncodeToUtf8(bytes) : 0;
                foreach (var b in bytes[..length])
                {
                    units.Add(b);
                }

                break;
            }

            case CharacterEncoding.Utf16:
                if (codePoint > 0xFFFF && Rune.IsValid(codePoint))
                {
                    var text = new Rune(codePoint).ToString();
                    units.Add(text[0]);
                    units.Add(text[1]);
                }
                else
                {
                    units.Add(codePoint);
                }

                break;

            default:
                units.Add(codePoint);
                break;
        }
    }

    /// <summary>
    /// The escape sequence at <paramref name="i"/> ([lex.ccon]), which is moved past it. A numeric escape
    /// gives a code unit, a universal character name a code point (<paramref name="isCodePoint"/>).
    /// </summary>
    private static bool TryDecodeEscape(ReadOnlySpan<byte> s, ref int i, out uint value, out bool isCodePoint)
    {
        value = 0;
        isCodePoint = false;
        if (i + 1 >= s.Length)
        {
            return false;
        }

        var c = s[i + 1];
        uint? simple = c switch
        {
            (byte)'\'' or (byte)'"' or (byte)'?' or (byte)'\\' => c,
            (byte)'a' => 7,
            (byte)'b' => 8,
            (byte)'f' => 12,
            (byte)'n' => 10,
            (byte)'r' => 13,
            (byte)'t' => 9,
            (byte)'v' => 11,
            (byte)'e' or (byte)'E' => 27,
            _ => null,
        };

        if (simple != null)
        {
            value = simple.Value;
            i += 2;
            return true;
        }

        if (c is >= (byte)'0' and <= (byte)'7')
        {
            // Up to three octal digits
            var end = i + 1;
            while (end < s.Length && end < i + 4 && s[end] is >= (byte)'0' and <= (byte)'7')
            {
                value = (value << 3) | (uint)(s[end] - '0');
                end++;
            }

            i = end;
            return true;
        }

        if (c is (byte)'o' or (byte)'x')
        {
            var radix = c == 'o' ? 8 : 16;
            var start = i + 2;
            var delimited = start < s.Length && s[start] == '{';
            var end = delimited ? start + 1 : start;
            var digitsStart = end;
            ulong number = 0;
            while (end < s.Length && (radix == 8 ? s[end] is >= (byte)'0' and <= (byte)'7' : Lexer.IsHexDigit(s[end])))
            {
                number = number * (ulong)radix + (ulong)Lexer.HexValue(s[end]);
                if (number > uint.MaxValue)
                {
                    return false;
                }

                end++;
            }

            if (end == digitsStart || (delimited && (end >= s.Length || s[end] != '}')) || (!delimited && radix == 8))
            {
                return false;
            }

            value = (uint)number;
            i = delimited ? end + 1 : end;
            return true;
        }

        var length = Lexer.ScanUniversalCharacterName(s, i, out var codePoint);
        if (length == 0 || codePoint < 0)
        {
            // Named characters are not decoded
            return false;
        }

        value = (uint)codePoint;
        isCodePoint = true;
        i += length;
        return true;
    }
}
