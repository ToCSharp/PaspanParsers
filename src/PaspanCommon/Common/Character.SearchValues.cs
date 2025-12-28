using System.Buffers;
using System.Runtime.CompilerServices;

namespace Paspan.Common;

public static partial class Character
{
    public const string DecimalDigits = "0123456789";
    public const string HexDigits = "0123456789abcdefABCDEF";
    public const string OctalDigits = "01234567";
    public const string BinaryDigits = "01";

    public const string AZLower = "abcdefghijklmnopqrstuvwxyz";
    public const string AZUpper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string AZ = AZLower + AZUpper;
    public const string AlphaNumeric = AZ + DecimalDigits;
    public const string DefaultIdentifierStart = "$_" + AZ;
    public const string DefaultIdentifierPart = "$_" + AZ + DecimalDigits;
    public const string NewLines = "\n\r\v";

    //internal static readonly SearchValues<char> _decimalDigits = SearchValues.Create(DecimalDigits);
    internal static readonly SearchValues<byte> _decimalDigits = SearchValues.Create("0123456789"u8);
    internal static readonly SearchValues<byte> _hexDigits = SearchValues.Create("0123456789abcdefABCDEF"u8);
    internal static readonly SearchValues<char> _identifierStart = SearchValues.Create(DefaultIdentifierStart);
    internal static readonly SearchValues<char> _identifierPart = SearchValues.Create(DefaultIdentifierPart);
    internal static readonly SearchValues<char> _newLines = SearchValues.Create(NewLines);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDecimalDigit(char ch) => _decimalDigits.Contains((byte)ch);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsIdentifierStart(char ch) => _identifierStart.Contains(ch);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsIdentifierPart(char ch) => _identifierPart.Contains(ch);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsHexDigit(char ch) => _hexDigits.Contains((byte)ch);
}
