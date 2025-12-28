namespace Paspan.Fluent;

public static class ReadBeforeConfig
{
    public static int MaxLengthDefault = int.MaxValue;
}

public sealed class ReadBefore<T, D, S>(Parser<D> delimiter, bool canBeEmpty = false, bool failOnEof = true, bool consumeDelimiter = false) : Parser<Unit>
{
    private readonly Parser<D> _delimiter = delimiter;
    private readonly Parser<S> _stopOn;
    private readonly int _maxLength = BytesBeforeConfig.MaxLengthDefault;
    private readonly bool _canBeEmpty = canBeEmpty;
    private readonly bool _failOnEof = failOnEof;
    private readonly bool _consumeDelimiter = consumeDelimiter;

    public ReadBefore(Parser<D> delimiter, Parser<S> stopOn, bool canBeEmpty = false, bool failOnEof = true, bool consumeDelimiter = false)
        : this(delimiter, canBeEmpty, failOnEof, consumeDelimiter)
    {
        _stopOn = stopOn;
        _maxLength = BytesBeforeConfig.MaxLengthDefault;
    }

    public ReadBefore(Parser<D> delimiter, int maxLength = -1, bool canBeEmpty = false, bool failOnEof = true, bool consumeDelimiter = false)
        : this(delimiter, canBeEmpty, failOnEof, consumeDelimiter)
    {
        _maxLength = maxLength == -1 ? BytesBeforeConfig.MaxLengthDefault : maxLength;
    }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<Unit> result)
    {
        context.EnterParser(this);

        var start = reader.CaptureState();

        var parsed = new ParseResult<D>();
        var parsed2 = new ParseResult<S>();
        var length = 0;

        while (true)
        {
            var previous = reader.CaptureState();
            var previous2 = reader.GetCurrentPosition();

            if (reader.Eof())
            {
                if (_failOnEof)
                {
                    reader.RollBackState(start);
                    return false;
                }

                if (length == 0 && !_canBeEmpty)
                {
                    reader.RollBackState(start);
                    return false;
                }

                if (length > 0)
                {
                    reader.SetValue(reader.GetPosition(start), previous2);
                    result.Set(Unit.Value);
                }
                return true;
            }

            var delimiterFound = _delimiter.Parse(ref reader, context, ref parsed);

            if (delimiterFound)
            {
                if (length == 0 && !_canBeEmpty)
                {
                    reader.RollBackState(start);
                    return false;
                }

                if (!_consumeDelimiter)
                {
                    reader.RollBackState(previous);
                }

                if (length > 0)
                {
                    reader.SetValue(reader.GetPosition(start), previous2);
                    result.Set(Unit.Value);
                }
                return true;
            }

            if (length >= _maxLength ||
                _stopOn?.Parse(ref reader, context, ref parsed2) == true)
            {
                reader.RollBackState(start);
                return false;
            }

            length++;
            reader.Read(1);
        }
    }

    public override string ToString() => $"TextBefore {_delimiter}";
}
