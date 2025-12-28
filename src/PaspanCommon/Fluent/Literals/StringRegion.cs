namespace Paspan.Fluent;

public sealed class StringRegion(StringLiteralQuotes quotes) : Parser<Region>
{
    private readonly StringLiteralQuotes _quotes = quotes;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<Region> result)
    {
        context.EnterParser(this);

        var start = reader.CaptureState();

        var success = _quotes switch
        {
            StringLiteralQuotes.Single => reader.ReadSingleQuotedString(),
            StringLiteralQuotes.Double => reader.ReadDoubleQuotedString(),
            StringLiteralQuotes.SingleOrDouble => reader.ReadQuotedString(),
            _ => false
        };

        if (success)
        {
            var regionStart = reader.GetPosition(start, 1);
            var regionEnd = reader.GetCurrentPosition(-1);
            result.Set(new Region(regionStart, regionEnd - regionStart));
            return true;
        }

        reader.RollBackState(start);

        return false;
    }
}
