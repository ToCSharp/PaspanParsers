namespace Paspan.Fluent;

public sealed class OneOrMany<T>(Parser<T> parser) : Parser<List<T>>
{
    private readonly Parser<T> _parser = parser ?? throw new ArgumentNullException(nameof(parser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<List<T>> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();
        var start = reader.CaptureState();

        // An element that consumes nothing doesn't count as the required first match (same as Parlot)
        if (!_parser.Parse(ref reader, context, ref parsed) || reader.GetCurrentPosition() == start)
        {
            reader.RollBackState(start);
            return false;
        }

        var results = new List<T> { parsed.Value };
        var resultStart = parsed.Start;
        var resultEnd = parsed.End;

        while (true)
        {
            var before = reader.CaptureState();

            if (!_parser.Parse(ref reader, context, ref parsed))
            {
                reader.RollBackState(before);
                break;
            }

            // A parser that succeeds without consuming anything would loop forever
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
