namespace Paspan.Fluent
{
    public sealed class NonWhiteSpaceLiteral(bool includeNewLines = true) : Parser<Unit>
    {
        private readonly bool _includeNewLines = includeNewLines;

        public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<Unit> result)
        {
            if (reader.Eof())
            {
                return false;
            }

            var start = reader.CaptureState();

            if (_includeNewLines)
            {
                reader.ReadNonWhiteSpaceOrNewLine();
            }
            else
            {
                reader.ReadNonWhiteSpace();
            }

            var end = reader.GetCurrentPosition();

            if (reader.GetPosition(start).Equals(end))
            {
                return false;
            }

            reader.SetValue(reader.GetPosition(start), end);
            return true;
        }
    }
}
