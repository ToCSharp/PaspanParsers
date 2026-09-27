namespace Paspan.Fluent;

public sealed class OneOrMany<T>(Parser<T> parser) : Parser<List<T>>
{
    private readonly Parser<T> _parser = parser ?? throw new ArgumentNullException(nameof(parser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<List<T>> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();
        var start = reader.CaptureState();

        if (!_parser.Parse(ref reader, context, ref parsed))
        {
            reader.RollBackState(start);
            return false;
        }

        var results = new List<T> { parsed.Value };
        var resultStart = parsed.Start;
        var resultEnd = parsed.End;

        // A parser that succeeds without consuming anything would loop forever
        var before = start;

        while (reader.GetCurrentPosition() != before)
        {
            before = reader.CaptureState();

            if (!_parser.Parse(ref reader, context, ref parsed))
            {
                reader.RollBackState(before);
                break;
            }

            if (reader.GetCurrentPosition() == before)
            {
                break;
            }

            resultEnd = parsed.End;
            results.Add(parsed.Value);
        }

        result.Set(resultStart, resultEnd, results);
        return true;
    }

}
