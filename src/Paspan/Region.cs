using System.Text;

namespace Paspan;

public struct Region(int start, int length)
{
    public int Start = start;
    public int End = start + length;
    public int Length = length;

    public readonly string ToString(ReadOnlySpan<byte> data) => Encoding.UTF8.GetString(data.Slice(Start, Length));

    public override readonly string ToString() => $"Region {Start}..{End}";
}
