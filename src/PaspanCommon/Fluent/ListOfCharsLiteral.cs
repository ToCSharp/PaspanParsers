namespace Paspan.Fluent;

internal sealed class ListOfChars : Parser<Region>
{
    private readonly List<byte> _map = [];
    private readonly int _minSize;
    private readonly int _maxSize;
    private readonly bool _negate;

    public ListOfChars(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0, bool negate = false)
    {
        foreach (var c in values)
        {
            // The parser matches single bytes, a non-ASCII char would be silently truncated
            if (!char.IsAscii(c))
            {
                throw new ArgumentException($"Only ASCII chars are supported, found '{c}'.", nameof(values));
            }

            _map.Add((byte)c);
        }

        _minSize = minSize;
        _maxSize = maxSize;
        _negate = negate;
    }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<Region> result)
    {
        context.EnterParser(this);

        //var cursor = context.Scanner.Cursor;
        //var span = cursor.Span;
        var start = reader.GetCurrentPosition();

        var size = 0;
        var maxLength = _maxSize > 0 ? Math.Min(reader.Length, _maxSize) : reader.Length;

        var remaining = reader.GetRemaining();

        while (size < maxLength && _map.Contains(remaining[size]) != _negate)
        {
            size++;
        }

        if (size < _minSize)
        {
            return false;
        }

        reader.RollBackState(start + size);

        reader.SetValue(start, start + size);
        result.Set(start, start + size, new Region(start, size));

        return true;
    }

    public override string ToString() => $"AnyOf(ListOfChars)";
}
