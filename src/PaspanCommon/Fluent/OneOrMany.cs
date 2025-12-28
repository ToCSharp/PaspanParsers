namespace Paspan.Fluent;

public sealed class OneOrMany<T>(Parser<T> parser) : Parser<List<T>>
{
    private readonly Parser<T> _parser = parser ?? throw new ArgumentNullException(nameof(parser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<List<T>> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        if (!_parser.Parse(ref reader, context, ref parsed))
        {
            return false;
        }

        //var start = parsed.Start;
        var results = new List<T>();

        do
        {
            //end = parsed.End;
            results.Add(parsed.Value);

        } while (_parser.Parse(ref reader, context, ref parsed));

        result = new ParseResult<List<T>>(results);
        return true;
    }

}
