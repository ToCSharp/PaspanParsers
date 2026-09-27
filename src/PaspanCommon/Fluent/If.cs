namespace Paspan.Fluent;
/// <summary>
/// Evaluates a condition once and executes only the selected parser.
/// </summary>
/// <remarks>
/// Without an else parser, a false condition fails without consuming input.
/// With an else parser, only the selected branch is executed: there is no fallback to the other one if it fails.
/// </remarks>
/// <typeparam name="C">The concrete <see cref="ParseContext" /> type to use.</typeparam>
/// <typeparam name="S">The type of the state to pass.</typeparam>
/// <typeparam name="T">The output parser type.</typeparam>
public sealed class If<C, S, T> : Parser<T> where C : ParseContext
{
    private readonly Func<C, S, bool> _predicate;
    private readonly S _state;
    private readonly Parser<T> _thenParser;
    private readonly Parser<T> _elseParser;

    public If(Parser<T> parser, Func<C, S, bool> predicate, S state)
    {
        _thenParser = parser ?? throw new ArgumentNullException(nameof(parser));
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        _state = state;
    }

    public If(Parser<T> thenParser, Parser<T> elseParser, Func<C, S, bool> predicate, S state)
        : this(thenParser, predicate, state)
    {
        _elseParser = elseParser ?? throw new ArgumentNullException(nameof(elseParser));
    }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var parser = _predicate((C)context, _state) ? _thenParser : _elseParser;

        if (parser is null)
        {
            return false;
        }

        var start = reader.CaptureState();

        if (parser.Parse(ref reader, context, ref result))
        {
            return true;
        }

        reader.RollBackState(start);
        return false;
    }

    public override string ToString() => _elseParser is null ? $"{_thenParser} (If)" : $"{_thenParser} (If) {_elseParser} (Else)";
}
