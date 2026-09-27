using System.Buffers;

namespace Paspan.Fluent;

public enum StringLiteralQuotes
{
    Single,
    Double,
    SingleOrDouble
}

public sealed class StringLiteral(StringLiteralQuotes quotes) : Parser<string>
{
    private readonly StringLiteralQuotes _quotes = quotes;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<string> result)
    {
        context.EnterParser(this);

        var start = reader.CaptureState();

        var success = _quotes switch
        {
            StringLiteralQuotes.Single => reader.ReadSingleQuotedString(),
            StringLiteralQuotes.Double => reader.ReadDoubleQuotedString(),
            StringLiteralQuotes.SingleOrDouble => reader.ReadQuotedString(),
            _ => false
        };

        if (success && reader.TryGetString(out var value))
        {
            // Decode escape sequences
            value = DecodeString(value);
            result.Set(start, reader.GetCurrentPosition(), value);
            return true;
        }

        reader.RollBackState(start);

        return false;
    }

    private static string DecodeString(string s)
    {
        if (string.IsNullOrEmpty(s) || s.IndexOf('\\') == -1)
        {
            return s;
        }

        return DecodeStringInternal(s.AsSpan());
    }

    private static string DecodeStringInternal(ReadOnlySpan<char> span)
    {
        char[] rentedBuffer = null;
        Span<char> buffer = span.Length <= 128
            ? stackalloc char[span.Length]
            : (rentedBuffer = ArrayPool<char>.Shared.Rent(span.Length));

        try
        {
            var dataIndex = 0;

            for (var i = 0; i < span.Length; i++)
            {
                var c = span[i];

                if (c == '\\')
                {
                    i++;
                    if (i >= span.Length) break;
                    c = span[i];

                    switch (c)
                    {
                        case '\'': c = '\''; break;
                        case '"': c = '\"'; break;
                        case '\\': c = '\\'; break;
                        case '0': c = '\0'; break;
                        case 'a': c = '\a'; break;
                        case 'b': c = '\b'; break;
                        case 'f': c = '\f'; break;
                        case 'n': c = '\n'; break;
                        case 'r': c = '\r'; break;
                        case 't': c = '\t'; break;
                        case 'v': c = '\v'; break;
                        case '/': c = '/'; break; // JSON escape
                        case 'u': c = DecodeHex(span, ref i, 4, 4); break;
                        case 'x': c = DecodeHex(span, ref i, 1, 4); break;
                    }
                }

                buffer[dataIndex++] = c;
            }

            return buffer[..dataIndex].ToString();
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }
        }
    }

    /// <summary>
    /// Decodes the hex digits following an escape char at <paramref name="i"/>, leaving <paramref name="i"/> on the last digit.
    /// The digits have already been validated by <see cref="SpanReader"/>.
    /// </summary>
    private static char DecodeHex(ReadOnlySpan<char> span, ref int i, int minDigits, int maxDigits)
    {
        var value = 0;
        var digits = 0;

        while (digits < maxDigits && i + 1 < span.Length && char.IsAsciiHexDigit(span[i + 1]))
        {
            i++;
            digits++;
            value = (value << 4) | HexValue(span[i]);
        }

        return digits >= minDigits ? (char)value : span[i];
    }

    private static int HexValue(char c) => c <= '9' ? c - '0' : (c | 0x20) - 'a' + 10;
}
