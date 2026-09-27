namespace Paspan.Fluent;

/// <summary>
/// Selects a parser at runtime and delegates parsing to it.
/// </summary>
/// <remarks>
/// The parser is either returned by a selector, or picked by its index in a fixed set of parsers.
/// A <c>null</c> parser or an out-of-range index fails without consuming input.
/// </remarks>
/// <typeparam name="C">The concrete <see cref="ParseContext" /> type to use.</typeparam>
/// <typeparam name="T">The output parser type.</typeparam>
public sealed class Select<C, T> : Parser<T> where C : ParseContext
{
    private readonly Func<C, Parser<T>> _selector;
    private readonly Func<C, int> _contextIndexSelector;
    private readonly Func<int> _indexSelector;
    private readonly Parser<T>[] _parsers;

    /// <summary>
    /// Creates a parser that executes the parser returned by <paramref name="selector"/>.
    /// </summary>
    public Select(Func<C, Parser<T>> selector)
    {
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    /// <summary>
    /// Creates a parser that executes the parser at the index returned by <paramref name="selector"/> using the current context.
    /// </summary>
    public Select(Func<C, int> selector, params Parser<T>[] parsers)
        : this(parsers)
    {
        _contextIndexSelector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    /// <summary>
    /// Creates a parser that executes the parser at the index returned by a context-free <paramref name="selector"/>.
    /// </summary>
    public Select(Func<int> selector, params Parser<T>[] parsers)
        : this(parsers)
    {
        _indexSelector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    private Select(Parser<T>[] parsers)
    {
        ArgumentNullException.ThrowIfNull(parsers);

        if (Array.IndexOf(parsers, null) >= 0)
        {
            throw new ArgumentException("Parsers array must not contain null elements.", nameof(parsers));
        }

        // Copy so that later changes to the caller's array don't alter the grammar
        _parsers = [.. parsers];
    }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var nextParser = SelectParser((C)context);

        if (nextParser == null)
        {
            return false;
        }

        var start = reader.CaptureState();
        var parsed = new ParseResult<T>();

        if (nextParser.Parse(ref reader, context, ref parsed))
        {
            result.Set(parsed.Start, parsed.End, parsed.Value);

            return true;
        }

        reader.RollBackState(start);
        return false;
    }

    private Parser<T> SelectParser(C context)
    {
        if (_selector != null)
        {
            return _selector(context);
        }

        var index = _indexSelector != null ? _indexSelector() : _contextIndexSelector(context);

        return (uint)index < (uint)_parsers.Length ? _parsers[index] : null;
    }

    public override string ToString() => "(Select)";
}
