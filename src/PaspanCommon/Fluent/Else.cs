namespace Paspan.Fluent;

/// <summary>
/// Returns a default value if the previous parser failed.
/// </summary>
public sealed class Else<T> : Parser<T>
{
    private readonly Parser<T> _parser;
    private readonly T _value;
    private readonly Func<T> _func;

    public Else(Parser<T> parser, T value)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _value = value;
    }

    public Else(Parser<T> parser, Func<T> func)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _func = func ?? throw new ArgumentNullException(nameof(func));
    }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        if (!_parser.Parse(ref reader, context, ref result))
        {
            if (_func != null)
            {
                result.Set(result.Start, result.End, _func());
            }
            else
            {
                // _value can't be null if _func is null
                result.Set(result.Start, result.End, _value!);
            }
        }

        return true;
    }

    public override string ToString() => $"{_parser} (Else)";
}
