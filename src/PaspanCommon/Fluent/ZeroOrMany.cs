namespace Paspan.Fluent;

public sealed class ZeroOrMany<T>(Parser<T> parser) : Parser<List<T>>
{
    private readonly Parser<T> _parser = parser ?? throw new ArgumentNullException(nameof(parser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<List<T>> result)
    {
        context.EnterParser(this);

        var results = new List<T>();

        var start = 0;
        var end = 0;

        var first = true;
        var parsed = new ParseResult<T>();

        // TODO: it's not restoring an intermediate failed text position
        // is the inner parser supposed to be clean?

        while (_parser.Parse(ref reader, context, ref parsed))
        {
            if (first)
            {
                first = false;
                start = parsed.Start;
            }

            end = parsed.End;
            
            results.Add(parsed.Value);
        }

        result.Set(start, end, results);

        return true;
    }
    public override string ToString() => $"{_parser}*";
}
