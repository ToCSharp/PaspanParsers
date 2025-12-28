namespace Paspan.Fluent;

/// <summary>
/// Doesn't parse anything and return the default value.
/// </summary>
public sealed class Empty<T>(T value) : Parser<T>
{
    private readonly T _value = value;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        result.Set(_value);

        return true;
    }
}
