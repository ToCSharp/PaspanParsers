namespace Paspan.Fluent;

public sealed class Sequence<T1, T2>(Parser<T1> parser1, Parser<T2> parser2) : Parser<(T1, T2)>
{
    internal readonly Parser<T1> _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
    internal readonly Parser<T2> _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<(T1, T2)> result)
    {
        context.EnterParser(this);

        var parseResult1 = new ParseResult<T1>();

        var start = reader.CaptureState();

        if (_parser1.Parse(ref reader, context, ref parseResult1))
        {
            var parseResult2 = new ParseResult<T2>();

            if (_parser2.Parse(ref reader, context, ref parseResult2))
            {
                result.Set(parseResult1.Start, parseResult2.End, (parseResult1.Value, parseResult2.Value));
                return true;
            }

        }

        reader.RollBackState(start);

        return false;
    }
}

public sealed class Sequence<T1, T2, T3>(Parser<(T1, T2)>
        parser,
    Parser<T3> lastParser
        ) : Parser<(T1, T2, T3)>
{
    private readonly Parser<(T1, T2)> _parser = parser;
    internal readonly Parser<T3> _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<(T1, T2, T3)> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<(T1, T2)>();

        var start = reader.CaptureState();

        if (_parser.Parse(ref reader, context, ref tupleResult))
        {
            var lastResult = new ParseResult<T3>();

            if (_lastParser.Parse(ref reader, context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    lastResult.Value
                    );

                result.Set(tupleResult.Start, lastResult.End, tuple);
                return true;
            }
        }

        reader.RollBackState(start);

        return false;
    }
}

public sealed class Sequence<T1, T2, T3, T4>(Parser<(T1, T2, T3)> parser, Parser<T4> lastParser) : Parser<(T1, T2, T3, T4)>
{
    private readonly Parser<(T1, T2, T3)> _parser = parser;
    internal readonly Parser<T4> _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<(T1, T2, T3, T4)> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<(T1, T2, T3)>();

        var start = reader.CaptureState();

        if (_parser.Parse(ref reader, context, ref tupleResult))
        {
            var lastResult = new ParseResult<T4>();

            if (_lastParser.Parse(ref reader, context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    lastResult.Value
                    );

                result.Set(tupleResult.Start, lastResult.End, tuple);
                return true;
            }
        }

        reader.RollBackState(start);

        return false;
    }
}

public sealed class Sequence<T1, T2, T3, T4, T5>(Parser<(T1, T2, T3, T4)> parser, Parser<T5> lastParser) : Parser<(T1, T2, T3, T4, T5)>
{
    private readonly Parser<(T1, T2, T3, T4)> _parser = parser;
    internal readonly Parser<T5> _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<(T1, T2, T3, T4, T5)> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<(T1, T2, T3, T4)>();

        var start = reader.CaptureState();

        if (_parser.Parse(ref reader, context, ref tupleResult))
        {
            var lastResult = new ParseResult<T5>();

            if (_lastParser.Parse(ref reader, context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4,
                    lastResult.Value
                    );

                result.Set(tupleResult.Start, lastResult.End, tuple);
                return true;
            }
        }

        reader.RollBackState(start);

        return false;
    }
}

public sealed class Sequence<T1, T2, T3, T4, T5, T6>(Parser<(T1, T2, T3, T4, T5)> parser, Parser<T6> lastParser) : Parser<(T1, T2, T3, T4, T5, T6)>
{
    private readonly Parser<(T1, T2, T3, T4, T5)> _parser = parser;
    internal readonly Parser<T6> _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<(T1, T2, T3, T4, T5, T6)> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<(T1, T2, T3, T4, T5)>();

        var start = reader.CaptureState();

        if (_parser.Parse(ref reader, context, ref tupleResult))
        {
            var lastResult = new ParseResult<T6>();

            if (_lastParser.Parse(ref reader, context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4,
                    tupleResult.Value.Item5,
                    lastResult.Value
                    );

                result.Set(tupleResult.Start, lastResult.End, tuple);
                return true;
            }

        }

        reader.RollBackState(start);

        return false;
    }

}

public sealed class Sequence<T1, T2, T3, T4, T5, T6, T7>(Parser<(T1, T2, T3, T4, T5, T6)> parser, Parser<T7> lastParser) : Parser<(T1, T2, T3, T4, T5, T6, T7)>
{
    private readonly Parser<(T1, T2, T3, T4, T5, T6)> _parser = parser;
    internal readonly Parser<T7> _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<(T1, T2, T3, T4, T5, T6, T7)> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<(T1, T2, T3, T4, T5, T6)>();

        var start = reader.CaptureState();

        if (_parser.Parse(ref reader, context, ref tupleResult))
        {
            var lastResult = new ParseResult<T7>();

            if (_lastParser.Parse(ref reader, context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4,
                    tupleResult.Value.Item5,
                    tupleResult.Value.Item6,
                    lastResult.Value
                    );

                result.Set(tupleResult.Start, lastResult.End, tuple);
                return true;
            }

        }

        reader.RollBackState(start);

        return false;
    }
}

public sealed class Sequence<T1, T2, T3, T4, T5, T6, T7, T8>(Parser<(T1, T2, T3, T4, T5, T6, T7)> parser, Parser<T8> lastParser) : Parser<(T1, T2, T3, T4, T5, T6, T7, T8)>
{
    private readonly Parser<(T1, T2, T3, T4, T5, T6, T7)> _parser = parser;
    internal readonly Parser<T8> _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<(T1, T2, T3, T4, T5, T6, T7, T8)> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<(T1, T2, T3, T4, T5, T6, T7)>();

        var start = reader.CaptureState();

        if (_parser.Parse(ref reader, context, ref tupleResult))
        {
            var lastResult = new ParseResult<T8>();

            if (_lastParser.Parse(ref reader, context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4,
                    tupleResult.Value.Item5,
                    tupleResult.Value.Item6,
                        tupleResult.Value.Item7,
                        lastResult.Value
                    );

                result.Set(tupleResult.Start, lastResult.End, tuple);
                return true;
            }

        }

        reader.RollBackState(start);

        return false;
    }
}

public sealed class Sequence<T1, T2, T3, T4, T5, T6, T7, T8, T9>(Parser<(T1, T2, T3, T4, T5, T6, T7, T8)> parser, Parser<T9> lastParser)
    : Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9)>
{
    private readonly Parser<(T1, T2, T3, T4, T5, T6, T7, T8)> _parser = parser;
    internal readonly Parser<T9> _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<(T1, T2, T3, T4, T5, T6, T7, T8, T9)> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<(T1, T2, T3, T4, T5, T6, T7, T8)>();

        var start = reader.CaptureState();

        if (_parser.Parse(ref reader, context, ref tupleResult))
        {
            var lastResult = new ParseResult<T9>();

            if (_lastParser.Parse(ref reader, context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4,
                    tupleResult.Value.Item5,
                    tupleResult.Value.Item6,
                        tupleResult.Value.Item7,
                        tupleResult.Value.Item8,
                        lastResult.Value
                    );

                result.Set(tupleResult.Start, lastResult.End, tuple);
                return true;
            }

        }

        reader.RollBackState(start);

        return false;
    }
}

public sealed class Sequence<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9)> parser, Parser<T10> lastParser)
    : Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)>
{
    private readonly Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9)> _parser = parser;
    internal readonly Parser<T10> _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<(T1, T2, T3, T4, T5, T6, T7, T8, T9)>();

        var start = reader.CaptureState();

        if (_parser.Parse(ref reader, context, ref tupleResult))
        {
            var lastResult = new ParseResult<T10>();

            if (_lastParser.Parse(ref reader, context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4,
                    tupleResult.Value.Item5,
                    tupleResult.Value.Item6,
                        tupleResult.Value.Item7,
                        tupleResult.Value.Item8,
                        tupleResult.Value.Item9,
                        lastResult.Value
                    );

                result.Set(tupleResult.Start, lastResult.End, tuple);
                return true;
            }

        }

        reader.RollBackState(start);

        return false;
    }
}

public sealed class Sequence<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)> parser, Parser<T11> lastParser)
    : Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11)>
{
    private readonly Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)> _parser = parser;
    internal readonly Parser<T11> _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11)> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)>();

        var start = reader.CaptureState();

        if (_parser.Parse(ref reader, context, ref tupleResult))
        {
            var lastResult = new ParseResult<T11>();

            if (_lastParser.Parse(ref reader, context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4,
                    tupleResult.Value.Item5,
                    tupleResult.Value.Item6,
                        tupleResult.Value.Item7,
                        tupleResult.Value.Item8,
                        tupleResult.Value.Item9,
                        tupleResult.Value.Item10,
                        lastResult.Value
                    );

                result.Set(tupleResult.Start, lastResult.End, tuple);
                return true;
            }

        }

        reader.RollBackState(start);

        return false;
    }
}

public sealed class Sequence<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11)> parser, Parser<T12> lastParser)
    : Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12)>
{
    private readonly Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11)> _parser = parser;
    internal readonly Parser<T12> _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12)> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11)>();

        var start = reader.CaptureState();

        if (_parser.Parse(ref reader, context, ref tupleResult))
        {
            var lastResult = new ParseResult<T12>();

            if (_lastParser.Parse(ref reader, context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4,
                    tupleResult.Value.Item5,
                    tupleResult.Value.Item6,
                        tupleResult.Value.Item7,
                        tupleResult.Value.Item8,
                        tupleResult.Value.Item9,
                        tupleResult.Value.Item10,
                        tupleResult.Value.Item11,
                        lastResult.Value
                    );

                result.Set(tupleResult.Start, lastResult.End, tuple);
                return true;
            }

        }

        reader.RollBackState(start);

        return false;
    }
}

public sealed class Sequence<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12)> parser, Parser<T13> lastParser)
    : Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13)>
{
    private readonly Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12)> _parser = parser;
    internal readonly Parser<T13> _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13)> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12)>();

        var start = reader.CaptureState();

        if (_parser.Parse(ref reader, context, ref tupleResult))
        {
            var lastResult = new ParseResult<T13>();

            if (_lastParser.Parse(ref reader, context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4,
                    tupleResult.Value.Item5,
                    tupleResult.Value.Item6,
                        tupleResult.Value.Item7,
                        tupleResult.Value.Item8,
                        tupleResult.Value.Item9,
                        tupleResult.Value.Item10,
                        tupleResult.Value.Item11,
                        tupleResult.Value.Item12,
                        lastResult.Value
                    );

                result.Set(tupleResult.Start, lastResult.End, tuple);
                return true;
            }

        }

        reader.RollBackState(start);

        return false;
    }
}
