using static Paspan.Common.Constants;

namespace Paspan.Fluent;

public sealed class IntegerLiteral(NumberOptions numberOptions = NumberOptions.AllowLeadingSign) : Parser<int>
{
    private readonly NumberOptions _numberOptions = numberOptions;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<int> result)
    {
        context.EnterParser(this);

        var start = reader.CaptureState();

        if ((_numberOptions & NumberOptions.AllowLeadingSign) == NumberOptions.AllowLeadingSign)
        {
            if (!reader.Skip(Minus))
            {
                reader.Skip(Plus);
            }
        }

        // Parse the sign together with the digits so that MinValue doesn't overflow
        if (reader.ConsumeIntegerDigits())
        {
            reader.SetValue(start, reader.GetCurrentPosition());
            if (reader.TryGetInt32(out var value))
            {
                result.Set(start, reader.GetCurrentPosition(), value);
                return true;
            }
        }

        reader.RollBackState(start);

        return false;
    }
}
