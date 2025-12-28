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
            var b = (byte)c;
            _map.Add(b);
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

        for (var i = 0; i < maxLength; i++)
        {
            if (!reader.ReadByte(out var b) || _map.Contains(b) == _negate)
            {
                break;
            }

            size++;
        }

        if (size < _minSize)
        {
            reader.RollBackState(start);
            return false;
        }

        reader.SetValue(start, start + size);
        result.Set(start, start + size, new Region(start, size));

        return true;
    }

    public override string ToString() => $"AnyOf(ListOfChars)";
}
