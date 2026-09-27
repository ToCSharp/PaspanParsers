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

    // Only ASCII whitespace: in UTF-8 any byte >= 0x80 is part of a multi-byte sequence,
    // e.g. 0xA0 is the second byte of NBSP (C2 A0) but also of Cyrillic 'Р' (D0 A0).
    public static bool IsWhiteSpace(byte ch) => (ch == Space) || (ch == Tab) || (ch == FormFeed);
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
