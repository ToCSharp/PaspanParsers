namespace Paspan.Fluent;

public sealed class OneOf<A, B, T>(Parser<A> parserA, Parser<B> parserB) : Parser<T>
    where A : T
    where B : T
{
    private readonly Parser<A> _parserA = parserA ?? throw new ArgumentNullException(nameof(parserA));
    private readonly Parser<B> _parserB = parserB ?? throw new ArgumentNullException(nameof(parserB));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var start = reader.CaptureState();
        var resultA = new ParseResult<A>();

        if (_parserA.Parse(ref reader, context, ref resultA))
        {
            result.Set(resultA.Start, resultA.End, resultA.Value);

            return true;
        }

        // Restore position before trying the second parser
        reader.RollBackState(start);

        var resultB = new ParseResult<B>();

        if (_parserB.Parse(ref reader, context, ref resultB))
        {
            result.Set(resultB.Start, resultB.End, resultB.Value);

            return true;
        }

        return false;
    }
}
