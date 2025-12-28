namespace Paspan.Fluent;
/// <summary>
/// Doesn't parse anything and fails parsing.
/// </summary>
public sealed class Fail<T> : Parser<T>
{
    public Fail()
    {
        Name = "Fail";
    }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        return false;
    }
}
