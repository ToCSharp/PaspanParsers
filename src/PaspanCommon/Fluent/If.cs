namespace Paspan.Fluent;
/// <summary>
/// Ensure the given parser is valid based on a condition, and backtracks if not.
/// </summary>
/// <typeparam name="C">The concrete <see cref="ParseContext" /> type to use.</typeparam>
/// <typeparam name="S">The type of the state to pass.</typeparam>
/// <typeparam name="T">The output parser type.</typeparam>
public sealed class If<C, S, T>(Parser<T> parser, Func<C, S, bool> predicate, S state) : Parser<T> where C : ParseContext
{
    private readonly Func<C, S, bool> _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    private readonly S _state = state;
    private readonly Parser<T> _parser = parser ?? throw new ArgumentNullException(nameof(parser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var valid = _predicate((C)context, _state);

        if (valid)
        {
            var start = reader.CaptureState();

            if (!_parser.Parse(ref reader, context, ref result))
            {
                reader.RollBackState(start);
            }
        }

        return valid;
    }

    public override string ToString() => $"{_parser} (If)";
}
