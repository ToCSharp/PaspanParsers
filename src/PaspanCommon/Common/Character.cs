using System.Runtime.CompilerServices;
using static Paspan.Common.Constants;

namespace Paspan.Common;

public static partial class Character
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInRange(char ch, char min, char max) => ch - (uint)min <= max - (uint)min;
    public static bool IsDecimalDigit(byte ch)
       => ch >= '0' && ch <= '9';

    public static bool IsHexDigit(byte b) => HexConverter.IsHexChar(b);

    public static bool IsWhiteSpace(byte ch) => (ch == Space) || (ch == Tab) || (ch == FormFeed)
        || (ch == 0xA0) //  non-breaking space
                        // TODO: 0x1680 && (0x1680 || 0x180E || (ch >= 0x2000 && ch <= 0x200A) || 0x202F || 0x205F || 0x3000 || 0xFEFF) Parlot
        ;
    public static bool IsNewLine(byte ch) => (ch == LineFeed) || (ch == CarriageReturn) || (ch == 11/*'\v'*/);

    public static bool IsWhiteSpaceOrNewLine(byte ch)
    => IsNewLine(ch) || IsWhiteSpace(ch);

    public static bool IsIdentifierStart(byte ch)
    => (ch == '$') || (ch == '_') ||
       (ch >= 'A' && ch <= 'Z') ||
       (ch >= 'a' && ch <= 'z');

    public static bool IsIdentifierPart(byte ch)
    => IsIdentifierStart(ch) || IsDecimalDigit(ch);

}
