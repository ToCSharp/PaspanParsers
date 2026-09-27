using Paspan.Common;
using System.Text;

namespace Paspan.Fluent;

public sealed class Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null) : Parser<string>
{
    private readonly Func<char, bool> _extraStart = extraStart;
    private readonly Func<char, bool> _extraPart = extraPart;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<string> result)
    {
        context.EnterParser(this);

        var start = reader.CaptureState();
        var remaining = reader.GetRemaining();

        var length = MatchChar(remaining, isStart: true);

        if (length == 0)
        {
            return false;
        }

        // At this point we have an identifier, read while it's an identifier part.
        var size = length;

        while (size < remaining.Length && (length = MatchChar(remaining[size..], isStart: false)) > 0)
        {
            size += length;
        }

        var end = start + size;

        reader.RollBackState(end);
        reader.SetValue(start, end);
        result.Set(start, end, reader.GetString());
        return true;
    }

    /// <summary>
    /// Returns the number of bytes of the next char if it can start (or continue) an identifier, 0 otherwise.
    /// </summary>
    private int MatchChar(ReadOnlySpan<byte> span, bool isStart)
    {
        if (span.IsEmpty)
        {
            return 0;
        }

        var extra = isStart ? _extraStart : _extraPart;
        var b = span[0];

        if (b < 0x80)
        {
            var isMatch = isStart ? Character.IsIdentifierStart(b) : Character.IsIdentifierPart(b);
            return isMatch || (extra != null && extra((char)b)) ? 1 : 0;
        }

        // Non-ASCII chars can only be accepted by the custom predicates, which expect a decoded char
        // and not a single UTF-8 byte. Chars outside the BMP don't fit in a char and are rejected.
        if (extra != null
            && Rune.DecodeFromUtf8(span, out var rune, out var bytesConsumed) == System.Buffers.OperationStatus.Done
            && rune.IsBmp
            && extra((char)rune.Value))
        {
            return bytesConsumed;
        }

        return 0;
    }
}
