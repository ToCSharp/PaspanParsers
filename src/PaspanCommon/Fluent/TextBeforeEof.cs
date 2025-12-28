namespace Paspan.Fluent;

public sealed class TextBeforeEof<T>(bool canBeEmpty = false, bool failOnEof = false, bool consumeDelimiter = false) : Parser<string>
{
    private readonly bool _canBeEmpty = canBeEmpty;
    private readonly bool _failOnEof = failOnEof;
    private readonly bool _consumeDelimiter = consumeDelimiter;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<string> result)
    {
        context.EnterParser(this);

        var start = reader.CaptureState();
        var res = reader.MoveToEof();
        var end = reader.GetCurrentPosition();
        if (reader.TryGetString(reader.GetPosition(start), end - start, out string value))
            result.Set(value);
        else
            result.Set("");

        return res;
    }
}
