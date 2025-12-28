namespace Paspan.Fluent
{
    public sealed class WhiteSpaceLiteral(bool includeNewLines = true) : Parser<Region>
    {
        private readonly bool _includeNewLines = includeNewLines;

        public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<Region> result)
    {
        if (reader.Eof())
        {
            return false;
        }

        var start = reader.CaptureState();

        if (_includeNewLines)
        {
            reader.SkipWhiteSpaceOrNewLine();
        }
        else
        {
            reader.SkipWhiteSpace();
        }

        var end = reader.GetCurrentPosition();

        if (reader.GetPosition(start).Equals(end))
        {
            return false;
        }

        // Устанавливаем ValueSpan для использования в других парсерах
        reader.SetValue(start, end);
        
        // Создаем Region и устанавливаем его в результат
        var region = new Region(start, end - start);
        result.Set(start, end, region);
        return true;
    }
    }
}
