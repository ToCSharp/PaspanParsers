namespace Paspan.Fluent;

public sealed class PatternLiteral : Parser<Unit>
{
    private readonly Func<byte, bool> _predicate;
    private readonly int _minSize;
    private readonly int _maxSize;

    public PatternLiteral(Func<byte, bool> predicate, int minSize = 1, int maxSize = 0)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        _minSize = minSize;
        _maxSize = maxSize;
        if(_maxSize == 0)
            _maxSize = int.MaxValue;
    }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<Unit> result)
    {
        context.EnterParser(this);
        var start = reader.CaptureState();

        if (reader.Eof() || !reader.SkipWhile(_predicate, _maxSize))
        {
            return false;
        }
        if (reader.ValueLength < _minSize)
        {
            reader.RollBackState(start);
            return false;
        }

        //result.Set(reader.GetValueAsSequence());
        result.Set(Unit.Value);
        return true;
    }
}
