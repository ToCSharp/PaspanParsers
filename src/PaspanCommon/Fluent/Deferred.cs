namespace Paspan.Fluent;

public sealed class Deferred<T> : Parser<T>
{

    public Parser<T> Parser { get; set; }

    public Deferred()
    {
    }

    public Deferred(Func<Deferred<T>, Parser<T>> parser)
    {
        Parser = parser(this);
    }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        if (Parser is null)
        {
            throw new InvalidOperationException("Parser has not been initialized");
        }

        // Remember the position where we entered this parser
        var entryPosition = reader.GetCurrentPosition();

        // Mark this parser as active at the current position (unless loop detection is disabled).
        // If it is already active there, this is an infinite recursion: fail gracefully instead of overflowing the stack.
        var trackPosition = !context.DisableLoopDetection;

        if (trackPosition && !context.PushParserAtPosition(this, entryPosition))
        {
            return false;
        }

        try
        {
            context.EnterParser(this);

            var outcome = Parser.Parse(ref reader, context, ref result);

            context.ExitParser(this);

            return outcome;
        }
        finally
        {
            // Unmark the parser even when a ParseException escapes, otherwise a reused
            // ParseContext would report a false cycle at this position
            if (trackPosition)
            {
                context.PopParserAtPosition(this, entryPosition);
            }
        }
    }

    //private bool _initialized = false;
    //private readonly Closure _closure = new();

    //private class Closure
    //{
    //    public object Func;
    //}
}
