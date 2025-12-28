using System.Numerics;

namespace Paspan.Fluent;

public static class NumberLiterals
{
    public const byte DefaultDecimalSeparator = (byte)'.';
    public const byte DefaultGroupSeparator = (byte)',';

    public static Parser<T> CreateNumberLiteralParser<T>(NumberOptions numberOptions = NumberOptions.Number, byte decimalSeparator = DefaultDecimalSeparator, byte groupSeparator = DefaultGroupSeparator)
    where T : INumber<T>
    {
        return new NumberLiteral<T>(numberOptions, decimalSeparator, groupSeparator);
    }
}
