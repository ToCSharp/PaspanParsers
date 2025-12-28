namespace Paspan.Fluent;

/// <summary>
/// OneOf the inner choices when all parsers return the same type.
/// We then return the actual result of each parser.
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class OneOf<T>(Parser<T>[] parsers) : Parser<T>
{
    private readonly Parser<T>[] _parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));
    /// <summary>
    /// Gets the parsers before they get SkipWhitespace removed.
    /// </summary>
    public IReadOnlyList<Parser<T>> OriginalParsers { get; } = parsers;

    public Parser<T>[] Parsers => _parsers;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var start = reader.CaptureState();

        foreach (var parser in _parsers)
        {
            if (parser.Parse(ref reader, context, ref result))
            {
                return true;
            }

            // Restore position before trying the next parser
            reader.RollBackState(start);
        }

        return false;
    }
}
