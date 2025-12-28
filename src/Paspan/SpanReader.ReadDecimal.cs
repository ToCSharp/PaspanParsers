using Paspan.Common;
using Paspan.Fluent;
using System.Runtime.CompilerServices;

namespace Paspan;

public ref partial struct SpanReader
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadDecimal() => ReadDecimal(out _);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadDecimal(out ReadOnlySpan<byte> number) => ReadDecimal(true, true, false, true, out number);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadDecimal(NumberOptions numberOptions, out ReadOnlySpan<byte> number, byte decimalSeparator = (byte)'.', byte groupSeparator = (byte)',')
    {
        return ReadDecimal(
            (numberOptions & NumberOptions.AllowLeadingSign) != 0,
            (numberOptions & NumberOptions.AllowDecimalSeparator) != 0,
            (numberOptions & NumberOptions.AllowGroupSeparators) != 0,
            (numberOptions & NumberOptions.AllowExponent) != 0,
            out number,
            decimalSeparator,
            groupSeparator);
    }

    public bool ReadDecimal(bool allowLeadingSign, bool allowDecimalSeparator, bool allowGroupSeparator, bool allowExponent, out ReadOnlySpan<byte> number, byte decimalSeparator = (byte)'.', byte groupSeparator = (byte)',')
    {
        // The buffer is read while the value is a valid decimal number. For instance `123,a` will return `123`.

        var start = CaptureState();

        if (allowLeadingSign)
        {
            if (Current is (byte)'-' or (byte)'+')
            {
                AdvanceNoNewLines(1);
            }
        }

        if (!ReadInteger(out number))
        {
            // If there is no number, check if the decimal separator is allowed and present, otherwise fail
            if (!allowDecimalSeparator ||Current != decimalSeparator)
            {
                RollBackState(start);
                return false;
            }
        }

        // Number can be empty if we have a decimal separator directly, in this case don't expect group separators
        if (!number.IsEmpty && allowGroupSeparator && Current == groupSeparator)
        {
            var beforeGroupPosition = CaptureState();

            // Group separators can be repeated as many times
            while (true)
            {
                if (Current == groupSeparator)
                {
                    AdvanceNoNewLines(1);
                }
                else if (!ReadInteger())
                {
                    // it was not a group separator so go back where the symbol was and stop
                    RollBackState(beforeGroupPosition);
                    break;
                }
                else
                {
                    beforeGroupPosition = GetCurrentPosition();
                }
            }
        }

        var beforeDecimalSeparator = GetCurrentPosition();

        if (allowDecimalSeparator && Current == decimalSeparator)
        {
            AdvanceNoNewLines(1);

            var numberIsEmpty = number.IsEmpty;

            if (!ReadInteger(out number))
            {
                // A decimal separator must be followed by a number if there is no integral part, e.g. `[NaN].[NaN]`
                if (numberIsEmpty)
                {
                    RollBackState(beforeDecimalSeparator);

                    return false;
                }

                number = _buffer[start.._consumed];
                return true;
            }
        }

        var beforeExponent = GetCurrentPosition();

        if (allowExponent && (Current is (byte)'e' or (byte)'E'))
        {
            AdvanceNoNewLines(1);

            if (Current is (byte)'-' or (byte)'+')
            {
                AdvanceNoNewLines(1);
            }

            // The exponent must be followed by a number, without a group separator, otherwise backtrack to before the exponent
            if (!ReadInteger(out _))
            {
                RollBackState(beforeExponent);
                number = _buffer[start.._consumed];
                return true;
            }
        }

        number = _buffer[start.._consumed];
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadInteger() => ReadInteger(out _);

    public bool ReadInteger(out ReadOnlySpan<byte> result)
    {
        var span = _buffer[_consumed..];

        var noDigitIndex = span.IndexOfAnyExcept(Character._decimalDigits);

        // If first char is not a digit, fail
        if (noDigitIndex == 0 || span.IsEmpty)
        {
            result = [];
            return false;
        }

        // If all chars are digits
        if (noDigitIndex == -1)
        {
            result = span;
        }
        else
        {
            result = span[..noDigitIndex];
        }

        AdvanceNoNewLines(result.Length);

        return true;
    }

}
