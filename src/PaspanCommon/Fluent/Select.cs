namespace Paspan.Fluent;

/// <summary>
/// Selects a parser instance at runtime and delegates parsing to it.
/// </summary>
/// <typeparam name="C">The concrete <see cref="ParseContext" /> type to use.</typeparam>
/// <typeparam name="T">The output parser type.</typeparam>
public sealed class Select<C, T>(Func<C, Parser<T>> selector) : Parser<T> where C : ParseContext
{
    private readonly Func<C, Parser<T>> _selector = selector ?? throw new ArgumentNullException(nameof(selector));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var nextParser = _selector((C)context);

        if (nextParser == null)
        {
            return false;
        }

        var parsed = new ParseResult<T>();

        if (nextParser.Parse(ref reader, context, ref parsed))
        {
            result.Set(parsed.Start, parsed.End, parsed.Value);

            return true;
        }

        return false;
    }

    public override string ToString() => "(Select)";
}
