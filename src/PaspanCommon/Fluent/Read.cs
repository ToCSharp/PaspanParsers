namespace Paspan.Fluent;


public sealed class Read(int count) : Parser<Unit>
{
    private readonly int _count = count;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<Unit> result)
    {
        context.EnterParser(this);

        var start = reader.CaptureState();

        if (reader.Read(_count))
        {
            result.Set(Unit.Value);
            return true;
        }

        return false;
    }

    public override string ToString() => $"Read({_count})" ;
}

//public sealed class Read<T> : Parser<Unit>
//{
//    private readonly Func<T, int> _action1;
//    private readonly Parser<T> _parser;

//    public Read(Parser<T> parser, Func<T, int> action)
//    {
//        _action1 = action ?? throw new ArgumentNullException(nameof(action));
//        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
//    }

//    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<Unit> result)
//    {
//        context.EnterParser(this);

//        var start = reader.CaptureState();

//        var parsed = new ParseResult<T>();

//        if (_parser.Parse(ref reader, context, ref parsed))
//        {
//            var count = _action1.Invoke(parsed.Value);
//            if (reader.Read(count))
//            {
//                result.Set(start, reader.GetCurrentPosition(), Unit.Value);
//                return true;
//            }

//        }
//        reader.RollBackState(start);
//        return false;
//    }

//    public override string ToString() => $"Read(count from action)" ;
//}

public sealed class Read<T>(Parser<T> parser, Func<T, int> action) : Parser<T>
{
    private readonly Func<T, int> _action1 = action ?? throw new ArgumentNullException(nameof(action));
    private readonly Parser<T> _parser = parser ?? throw new ArgumentNullException(nameof(parser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var start = reader.CaptureState();

        if (_parser.Parse(ref reader, context, ref result))
        {
            var count = _action1.Invoke(result.Value);
            if (reader.Read(count))
            {
                return true;
            }

        }
        reader.RollBackState(start);
        return false;
    }

    public override string ToString() => $"Read(count from action)";
}
