namespace Paspan.Fluent;

/// <summary>
/// A parser that temporarily sets a custom whitespace parser for its inner parser.
/// </summary>
public sealed class WithWhiteSpaceParser<T>(Parser<T> parser, Parser<Region> whiteSpaceParser) : Parser<T>
{
    private readonly Parser<T> _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    private readonly Parser<Region> _whiteSpaceParser = whiteSpaceParser ?? throw new ArgumentNullException(nameof(whiteSpaceParser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        // Save the current whitespace parser
        var previousWhiteSpaceParser = context.WhiteSpaceParser;

        try
        {
            // Set the custom whitespace parser
            context.WhiteSpaceParser = _whiteSpaceParser;

            // Parse with the custom whitespace parser
            var success = _parser.Parse(ref reader, context, ref result);

            return success;
        }
        finally
        {
            // Restore the previous whitespace parser
            context.WhiteSpaceParser = previousWhiteSpaceParser;
        }
    }

    public override string ToString() => $"{_parser} (With Custom WS)";
}
