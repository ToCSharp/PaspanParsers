using Paspan.Common;

namespace Paspan.Fluent;

public sealed class Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null) : Parser<string>
{
    private readonly Func<char, bool> _extraStart = extraStart;
    private readonly Func<char, bool> _extraPart = extraPart;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<string> result)
    {
        context.EnterParser(this);

        // TODO: reader.PeekChar(1)
        if (reader.Peek(1))
        {
            var b = reader.GetValue()[0];
            if (Character.IsIdentifierStart(b) || _extraStart != null && _extraStart((char)b))
            {
                var start = reader.CaptureState();
                // At this point we have an identifier, read while it's an identifier part.
                reader.Read(1);

                while (reader.Peek(1))
                {
                    b = reader.GetValue()[0];
                    if (Character.IsIdentifierPart(b) || (_extraPart != null && _extraPart((char)b)))
                    {
                        reader.Read(1);
                    }
                    else
                    {
                        break;
                    }
                }

                var end = reader.GetCurrentPosition();

                reader.SetValue(reader.GetPosition(start), end);
                result.Set(reader.GetString());
                return true;
            }

        }
        return false;
    }
}
