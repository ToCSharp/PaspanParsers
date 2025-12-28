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

        // Check for infinite recursion at the same position (unless disabled)
        if (!context.DisableLoopDetection && context.IsParserActiveAtPosition(this, entryPosition))
        {
            // Cycle detected at this position - fail gracefully instead of stack overflow
            return false;
        }

        // Mark this parser as active at the current position (unless loop detection is disabled)
        var trackPosition = !context.DisableLoopDetection && context.PushParserAtPosition(this, entryPosition);

        context.EnterParser(this);

        var outcome = Parser.Parse(ref reader, context, ref result);

        context.ExitParser(this);

        // Mark this parser as inactive at the entry position (only if we tracked it)
        if (trackPosition)
        {
            context.PopParserAtPosition(this, entryPosition);
        }

        return outcome;
    }

    //private bool _initialized = false;
    //private readonly Closure _closure = new();

    //private class Closure
    //{
    //    public object Func;
    //}
}
