namespace Paspan.Fluent;

/// <summary>
/// Ensure the given parser matches at the current position without consuming input (positive lookahead).
/// </summary>
/// <typeparam name="T">The output parser type.</typeparam>
public sealed class WhenFollowedBy<T>(Parser<T> parser, Parser<object> lookahead) : Parser<T>
{
    private readonly Parser<T> _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    private readonly Parser<object> _lookahead = lookahead ?? throw new ArgumentNullException(nameof(lookahead));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var start = reader.CaptureState();

        // First, parse with the main parser
        var mainSuccess = _parser.Parse(ref reader, context, ref result);

        if (!mainSuccess)
        {
            return false;
        }

        // Save position before lookahead check
        var beforeLookahead = reader.CaptureState();

        // Now check if the lookahead parser matches at the current position
        var lookaheadResult = new ParseResult<object>();
        var lookaheadSuccess = _lookahead.Parse(ref reader, context, ref lookaheadResult);

        // Reset position to before the lookahead (it shouldn't consume input)
        reader.RollBackState(beforeLookahead);

        // If lookahead failed, fail this parser and reset to start
        if (!lookaheadSuccess)
        {
            reader.RollBackState(start);
            return false;
        }

        return true;
    }

    public override string ToString() => $"{_parser} (WhenFollowedBy {_lookahead})";
}
