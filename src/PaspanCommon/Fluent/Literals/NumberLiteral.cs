#if NET8_0_OR_GREATER
using System.Globalization;
using System.Numerics;

namespace Paspan.Fluent;

public sealed class NumberLiteral<T> : Parser<T>
    where T : INumber<T>
{
    private const byte DefaultDecimalSeparator = (byte)'.';
    private const byte DefaultGroupSeparator = (byte)',';

    private readonly byte _decimalSeparator;
    private readonly byte _groupSeparator;
    private readonly NumberStyles _numberStyles;
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;
    private readonly bool _allowLeadingSign;
    private readonly bool _allowDecimalSeparator;
    private readonly bool _allowGroupSeparator;
    private readonly bool _allowExponent;

    public NumberLiteral(NumberOptions numberOptions = NumberOptions.Number, byte decimalSeparator = DefaultDecimalSeparator, byte groupSeparator = DefaultGroupSeparator)
    {
        _decimalSeparator = decimalSeparator;
        _groupSeparator = groupSeparator;
        _numberStyles = numberOptions.ToNumberStyles();

        if (decimalSeparator != NumberLiterals.DefaultDecimalSeparator ||
            groupSeparator != NumberLiterals.DefaultGroupSeparator)
        {
            _culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            // Преобразуем byte в char, затем в string для правильного отображения символа
            _culture.NumberFormat.NumberDecimalSeparator = ((char)decimalSeparator).ToString();
            _culture.NumberFormat.NumberGroupSeparator = ((char)groupSeparator).ToString();
        }

        _allowLeadingSign = (numberOptions & NumberOptions.AllowLeadingSign) != 0;
        _allowDecimalSeparator = (numberOptions & NumberOptions.AllowDecimalSeparator) != 0;
        _allowGroupSeparator = (numberOptions & NumberOptions.AllowGroupSeparators) != 0;
        _allowExponent = (numberOptions & NumberOptions.AllowExponent) != 0;

        Name = "NumberLiteral";
    }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var reset = reader.CaptureState();
        var start = reset;

        if (reader.ReadDecimal(_allowLeadingSign, _allowDecimalSeparator, _allowGroupSeparator, _allowExponent, out var number, _decimalSeparator, _groupSeparator))
        {
            var end = reader.GetCurrentPosition();

            if (T.TryParse(number, _numberStyles, _culture, out var value))
            {
                result.Set(start, end, value);

                return true;
            }
        }

        reader.RollBackState(reset);

        return false;
    }

}
#endif
