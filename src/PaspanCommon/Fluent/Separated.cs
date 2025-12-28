namespace Paspan.Fluent;

public sealed class Separated<U, T>(Parser<U> separator, Parser<T> parser) : Parser<List<T>>
{
    private readonly Parser<U> _separator = separator ?? throw new ArgumentNullException(nameof(separator));
    private readonly Parser<T> _parser = parser ?? throw new ArgumentNullException(nameof(parser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<List<T>> result)
    {
        context.EnterParser(this);

        var results = new List<T>();

        var start = 0;
        var end = 0;

        var first = true;
        var parsed = new ParseResult<T>();
        var separatorResult = new ParseResult<U>();

        while (true)
        {
            if (!first)
            {
                // Save position before parsing separator, in case we need to rollback
                var positionBeforeSeparator = reader.CaptureState();
                
                if (!_separator.Parse(ref reader, context, ref separatorResult))
                {
                    break;
                }
                
                // Try to parse the next value after the separator
                if (!_parser.Parse(ref reader, context, ref parsed))
                {
                    // A separator was found, but not followed by another value.
                    // It's still successful if there was one value parsed, but we reset the cursor to before the separator
                    reader.RollBackState(positionBeforeSeparator);
                    break;
                }
            }
            else
            {
                // First element
                if (!_parser.Parse(ref reader, context, ref parsed))
                {
                    // If first element fails to parse, the entire parser fails
                    return false;
                }
            }

            if (first)
            {
                start = parsed.Start;
                first = false;
            }

            end = parsed.End;
            results.Add(parsed.Value);
        }

        result = new ParseResult<List<T>>(start, end, results);
        return true;
    }
}
