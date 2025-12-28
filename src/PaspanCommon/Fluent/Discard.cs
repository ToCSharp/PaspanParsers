namespace Paspan.Fluent;

/// <summary>
/// Doesn't parse anything and return the default value.
/// </summary>
//[Obsolete("Use the Then parser instead.")]
public sealed class Discard<T, U>(Parser<T> parser, U value) : Parser<U>
{
    private readonly Parser<T> _parser = parser;
    private readonly U _value = value;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<U> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        if (_parser.Parse(ref reader, context, ref parsed))
        {
            result.Set(_value);
            return true;
        }

        return false;
    }
}
