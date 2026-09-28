namespace PaspanParsers;

/// <summary>
/// The bytes the parsers read: the input as UTF-8 without its byte order mark. Node spans
/// (<see cref="TextSpan"/>) are offsets into them.
/// </summary>
public static class Utf8Source
{
    /// <summary>
    /// The UTF-8 encoding of <paramref name="input"/> without the byte order mark.
    /// </summary>
    public static byte[] FromString(string input)
    {
        input ??= string.Empty;

        // A byte order mark is not part of the source text
        var start = input.Length > 0 && input[0] == '﻿' ? 1 : 0;
        return System.Text.Encoding.UTF8.GetBytes(input, start, input.Length - start);
    }

    /// <summary>
    /// <paramref name="utf8Source"/> without its byte order mark.
    /// </summary>
    public static ReadOnlyMemory<byte> WithoutByteOrderMark(ReadOnlyMemory<byte> utf8Source)
    {
        return utf8Source.Span.StartsWith("﻿"u8) ? utf8Source[3..] : utf8Source;
    }
}
