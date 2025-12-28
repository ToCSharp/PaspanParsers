namespace Paspan.Fluent;

public sealed class SkipWhiteSpace<T>(Parser<T> parser) : Parser<T>
{
    private readonly Parser<T> _parser = parser;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var start = reader.CaptureState();

        // Use the ParseContext's logic to ignore whitespaces since it knows about multi-line grammars
        context.SkipWhiteSpace(ref reader);

        if (_parser.Parse(ref reader, context, ref result))
        {
            return true;
        }

        reader.RollBackState(start);
        return false;
    }
}
