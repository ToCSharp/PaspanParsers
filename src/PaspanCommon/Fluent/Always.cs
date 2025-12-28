namespace Paspan.Fluent;

/// <summary>
/// Doesn't parse anything and return the default value.
/// </summary>
public sealed class Always<T> : Parser<T>
{
    private readonly T _value;

    public Always(T value)
    {
        Name = "Always";
        _value = value;
    }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        result.Set(reader.GetCurrentPosition(), reader.GetCurrentPosition(), _value);

        return true;
    }
}
