namespace Paspan.Fluent;

public sealed class ElseError<T>(Parser<T> parser, string message) : Parser<T>
{
    private readonly Parser<T> _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    private readonly string _message = message;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        if (!_parser.Parse(ref reader, context, ref result))
        {
            throw new ParseException(_message);
        }

        return true;
    }
}

public sealed class Error<T>(Parser<T> parser, string message) : Parser<T>
{
    private readonly Parser<T> _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    private readonly string _message = message;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        if (_parser.Parse(ref reader, context, ref result))
        {
            throw new ParseException(_message);
        }

        return false;
    }
}

public sealed class Error<T, U>(Parser<T> parser, string message) : Parser<U>
{
    private readonly Parser<T> _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    private readonly string _message = message;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<U> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        if (_parser.Parse(ref reader, context, ref parsed))
        {
            throw new ParseException(_message);
        }

        return false;
    }
}
