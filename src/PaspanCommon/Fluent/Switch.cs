namespace Paspan.Fluent;

/// <summary>
/// Routes the parsing based on a custom delegate.
/// </summary>
public sealed class Switch<T, U>(Parser<T> previousParser, Func<ParseContext, T, Parser<U>> action) : Parser<U>
{

    private readonly Parser<T> _previousParser = previousParser ?? throw new ArgumentNullException(nameof(previousParser));
    private readonly Func<ParseContext, T, Parser<U>> _action = action ?? throw new ArgumentNullException(nameof(action));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<U> result)
    {
        var previousResult = new ParseResult<T>();

        if (!_previousParser.Parse(ref reader, context, ref previousResult))
        {
            return false;
        }

        var nextParser = _action(context, previousResult.Value);

        if (nextParser == null)
        {
            return false;
        }

        var parsed = new ParseResult<U>();

        if (nextParser.Parse(ref reader, context, ref parsed))
        {
            result.Set(parsed.Value);
            return true;
        }

        return false;
    }
}
