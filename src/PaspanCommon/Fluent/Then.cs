namespace Paspan.Fluent;

/// <summary>
/// Returns a new <see cref="Parser{U}" /> converting the input value of 
/// type T to the output value of type U using a custom function.
/// </summary>
/// <typeparam name="T">The input parser type.</typeparam>
/// <typeparam name="U">The output parser type.</typeparam>
public sealed class Then<T, U>(Parser<T> parser) : Parser<U>
{
    private readonly Func<T, U> _action1;
    private readonly Func<ParseContext, T, U> _action2;
    private readonly Func<ParseContext, int, int, T, U> _action3;
    private readonly U _value;
    private readonly Parser<T> _parser = parser ?? throw new ArgumentNullException(nameof(parser));

    public Then(Parser<T> parser, Func<T, U> action) : this(parser)
    {
        _action1 = action ?? throw new ArgumentNullException(nameof(action));
    }

    public Then(Parser<T> parser, Func<ParseContext, T, U> action) : this(parser)
    {
        _action2 = action ?? throw new ArgumentNullException(nameof(action));
    }

    public Then(Parser<T> parser, Func<ParseContext, int, int, T, U> action) : this(parser)
    {
        _action3 = action ?? throw new ArgumentNullException(nameof(action));
    }

    public Then(Parser<T> parser, U value) : this(parser)
    {
        _value = value;
    }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<U> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        if (_parser.Parse(ref reader, context, ref parsed))
        {
            if (_action1 != null)
            {
                result.Set(_action1.Invoke(parsed.Value));
            }
            else if (_action2 != null)
            {
                result.Set(_action2.Invoke(context, parsed.Value));
            }
            else if (_action3 != null)
            {
                result.Set(_action3.Invoke(context, parsed.Start, parsed.End, parsed.Value));
            }
            else
            {
                // _value can't be null if action1, action2, and action3 are null
                result.Set(parsed.Start, parsed.End, _value!);
            }

            return true;
        }

        return false;
    }

    override public string ToString() => $"{_parser} (Then)";
}
