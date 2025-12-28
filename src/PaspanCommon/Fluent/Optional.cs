namespace Paspan.Fluent;
/// <summary>
/// Returns a list containing zero or one element.
/// </summary>
/// <remarks>
/// This parser will always succeed. If the previous parser fails, it will return an empty list.
/// </remarks>
public sealed class Optional<T>(Parser<T> parser) : Parser<Option<T>>
{
    private readonly Parser<T> _parser = parser ?? throw new ArgumentNullException(nameof(parser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<Option<T>> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        var success = _parser.Parse(ref reader, context, ref parsed);

        result.Set(parsed.Start, parsed.End, success ? new Option<T>(parsed.Value) : new Option<T>());

        // Optional always succeeds
        return true;
    }

    public override string ToString() => $"{_parser}?";
}
