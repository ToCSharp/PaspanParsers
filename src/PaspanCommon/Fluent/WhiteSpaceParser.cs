namespace Paspan.Fluent;

/// <summary>
/// A parser that succeeds when parsing whitespaces as defined in <see cref="ParseContext.WhiteSpaceParser"/>.
/// </summary>
public sealed class WhiteSpaceParser : Parser<Region>
{
    private static readonly WhiteSpaceLiteral _defaultWhiteSpace = new WhiteSpaceLiteral(includeNewLines: true);

    public WhiteSpaceParser()
    {
    }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<Region> result)
    {
        context.EnterParser(this);

        // Если в контексте задан кастомный whitespace parser, используем его
        if (context.WhiteSpaceParser != null)
        {
            return context.WhiteSpaceParser.Parse(ref reader, context, ref result);
        }

        // Иначе используем дефолтный
        return _defaultWhiteSpace.Parse(ref reader, context, ref result);
    }

    public override string ToString() => $"WhiteSpaceParser";
}
