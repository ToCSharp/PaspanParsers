using static Paspan.Common.Constants;

namespace Paspan.Fluent;

public sealed class IntegerLiteral(NumberOptions numberOptions = NumberOptions.AllowLeadingSign) : Parser<int>
{
    private readonly NumberOptions _numberOptions = numberOptions;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<int> result)
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

        if (reader.ConsumeIntegerDigits() && reader.TryGetInt32(out var value))
        {
            result.Set(sign * value);
            return true;
        }

        reader.RollBackState(start);

        return false;
    }
}
