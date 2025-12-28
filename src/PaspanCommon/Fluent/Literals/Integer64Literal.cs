using static Paspan.Common.Constants;

namespace Paspan.Fluent;

public sealed class Integer64Literal(NumberOptions numberOptions = NumberOptions.AllowLeadingSign) : Parser<long>
{
    private readonly NumberOptions _numberOptions = numberOptions;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<long> result)
    {
        context.EnterParser(this);

        var start = reader.CaptureState();

        var sign = 1;
        if ((_numberOptions & NumberOptions.AllowLeadingSign) == NumberOptions.AllowLeadingSign)
        {
            if (reader.Skip(Minus))
            {
                sign = -1;
            }
            else
            {
                reader.Skip(Plus);
            }
        }

        if (reader.ConsumeIntegerDigits() && reader.TryGetInt64(out var value))
        {
            result.Set(sign * value);
            return true;
        }

        reader.RollBackState(start);

        return false;
    }
}
