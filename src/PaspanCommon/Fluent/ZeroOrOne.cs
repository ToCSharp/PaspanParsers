namespace Paspan.Fluent;

public sealed class ZeroOrOne<T> : Parser<T>
{
    private readonly Parser<T> _parser;
    private readonly T _defaultValue;

    public ZeroOrOne(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public ZeroOrOne(Parser<T> parser, T defaultValue)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _defaultValue = defaultValue;
    }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        var success = _parser.Parse(ref reader, context, ref parsed);

        result.Set(parsed.Start, parsed.End, success ? parsed.Value : _defaultValue);

        // ZeroOrOne always succeeds
        return true;
    }

    public override string ToString() => $"{_parser}?";
}
