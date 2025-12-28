using System.Text;

namespace Paspan;

public static partial class SpanReaderHelpers
{
    // A hex digit is valid if it is in the range: [0..9] | [A..F] | [a..f]
    // Otherwise, return false.
    public static bool IsHexDigit(byte nextByte) => HexConverter.IsHexChar(nextByte);

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="value"/> is in the range [0..9].
    /// Otherwise, returns <see langword="false"/>.
    /// </summary>
    public static bool IsDigit(byte value) => value >= 48 && value <= 57;
    //public static bool IsDigit(byte value) => (uint)(value - '0') <= '9' - '0';

    public static string Utf8GetString(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes);

}
