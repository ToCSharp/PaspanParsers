namespace Paspan.Fluent;

/// <summary>
/// Successful when the cursor is at the end of the string.
/// </summary>
public sealed class Eof<T>(Parser<T> parser) : Parser<T>
{
    private readonly Parser<T> _parser = parser;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var start = reader.CaptureState();

        if (_parser.Parse(ref reader, context, ref result) && reader.Eof())
        {
            return true;
        }

        reader.RollBackState(start);
        return false;
    }
}
