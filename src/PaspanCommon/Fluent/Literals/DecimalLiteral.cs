using static Paspan.Common.Constants;

namespace Paspan.Fluent;

public sealed class DecimalLiteral(NumberOptions numberOptions = NumberOptions.AllowLeadingSign) : Parser<decimal>
{
    private readonly NumberOptions _numberOptions = numberOptions;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<decimal> result)
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

        if (reader.ConsumeDecimalDigits() && reader.TryGetDecimal(out decimal value))
        {
            result.Set(sign * value);
            return true;
        }

        reader.RollBackState(start);

        return false;
    }
}
