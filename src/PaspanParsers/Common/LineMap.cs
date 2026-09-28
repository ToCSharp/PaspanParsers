namespace PaspanParsers;

/// <summary>
/// Converts between UTF-8 byte offsets of a parsed input (<see cref="TextSpan"/>) and 1-based line and column
/// numbers. Columns count UTF-16 code units, like editors, Roslyn and the Language Server Protocol, so a
/// character outside the Basic Multilingual Plane is two columns. Lines end at <c>\r\n</c>, <c>\r</c> and
/// <c>\n</c>, and optionally also at U+0085, U+2028 and U+2029, as in C#.
/// </summary>
public sealed class LineMap
{
    private readonly byte[] _source;

    // The offset of the first byte of each line
    private readonly int[] _lineStarts;

    /// <summary>
    /// Builds the map of <paramref name="utf8Source"/>, the input as UTF-8 bytes without the byte order mark
    /// (<see cref="Utf8Source.FromString(string)"/>). The bytes are copied. <paramref name="unicodeLineBreaks"/>
    /// makes U+0085, U+2028 and U+2029 end lines, as in C#; C++ lines end only at <c>\r</c> and <c>\n</c>.
    /// </summary>
    public LineMap(ReadOnlySpan<byte> utf8Source, bool unicodeLineBreaks = true)
    {
        _source = utf8Source.ToArray();

        var starts = new List<int> { 0 };
        for (var i = 0; i < _source.Length; i++)
        {
            var b = _source[i];
            if (b == '\n')
            {
                starts.Add(i + 1);
            }
            else if (b == '\r')
            {
                if (i + 1 < _source.Length && _source[i + 1] == '\n')
                {
                    i++;
                }

                starts.Add(i + 1);
            }
            else if (unicodeLineBreaks && b == 0xC2 && i + 1 < _source.Length && _source[i + 1] == 0x85)
            {
                // U+0085
                i++;
                starts.Add(i + 1);
            }
            else if (unicodeLineBreaks && b == 0xE2 && i + 2 < _source.Length && _source[i + 1] == 0x80 && _source[i + 2] is 0xA8 or 0xA9)
            {
                // U+2028, U+2029
                i += 2;
                starts.Add(i + 1);
            }
        }

        _lineStarts = starts.ToArray();
    }

    /// <summary>The number of lines; an input that ends with a line break has an empty last line.</summary>
    public int LineCount => _lineStarts.Length;

    /// <summary>
    /// The 1-based line and column of <paramref name="offset"/>, a byte offset from 0 to the length of the input.
    /// </summary>
    public (int Line, int Column) GetLineAndColumn(int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, _source.Length);

        var index = Array.BinarySearch(_lineStarts, offset);
        var line = index >= 0 ? index : ~index - 1;
        return (line + 1, Utf16Length(_lineStarts[line], offset) + 1);
    }

    /// <summary>
    /// The byte offset of the 1-based <paramref name="line"/> and <paramref name="column"/>. A column past the
    /// end of the line is the end of the line (before its line break).
    /// </summary>
    public int GetOffset(int line, int column)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(line, LineCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);

        var offset = _lineStarts[line - 1];
        var end = LineEnd(line - 1);
        var units = column - 1;
        while (units > 0 && offset < end)
        {
            var length = SequenceLength(_source[offset]);
            units -= length == 4 ? 2 : 1;
            offset = Math.Min(end, offset + length);
        }

        return offset;
    }

    /// <summary>
    /// The span of the 1-based <paramref name="line"/>, without its line break.
    /// </summary>
    public TextSpan GetLineSpan(int line)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(line, LineCount);
        return new TextSpan(_lineStarts[line - 1], LineEnd(line - 1));
    }

    /// <summary>The end of the line with the 0-based <paramref name="index"/>, before its line break.</summary>
    private int LineEnd(int index)
    {
        if (index + 1 >= _lineStarts.Length)
        {
            return _source.Length;
        }

        var end = _lineStarts[index + 1];
        var b = _source[end - 1];
        if (b == '\n')
        {
            return end - 2 >= _lineStarts[index] && _source[end - 2] == '\r' ? end - 2 : end - 1;
        }

        return b switch
        {
            (byte)'\r' => end - 1,
            0x85 => end - 2,
            _ => end - 3,
        };
    }

    /// <summary>
    /// The number of UTF-16 code units the UTF-8 bytes from <paramref name="start"/> to <paramref name="end"/> encode.
    /// </summary>
    private int Utf16Length(int start, int end)
    {
        var units = 0;
        for (var i = start; i < end; i++)
        {
            var b = _source[i];
            if ((b & 0xC0) != 0x80)
            {
                // A lead byte: four-byte sequences are surrogate pairs in UTF-16
                units += b >= 0xF0 ? 2 : 1;
            }
        }

        return units;
    }

    private static int SequenceLength(byte lead) => lead switch
    {
        < 0x80 => 1,
        >= 0xF0 => 4,
        >= 0xE0 => 3,
        >= 0xC0 => 2,
        _ => 1,
    };
}
