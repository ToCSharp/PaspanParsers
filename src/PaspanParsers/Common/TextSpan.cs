namespace PaspanParsers;

/// <summary>
/// A range of the parsed input in UTF-8 bytes: <see cref="Start"/> is the first byte, <see cref="End"/>
/// the byte after the last one. Offsets count from the start of the input without its byte order mark.
/// </summary>
public readonly record struct TextSpan(int Start, int End)
{
    public int Length => End - Start;

    public bool IsEmpty => Start == End;

    /// <summary>True when <paramref name="span"/> lies inside this span.</summary>
    public bool Contains(TextSpan span) => Start <= span.Start && span.End <= End;

    /// <summary>
    /// The text of the span in <paramref name="utf8Source"/>, the input as UTF-8 bytes without the byte order mark.
    /// </summary>
    public string GetText(ReadOnlySpan<byte> utf8Source) => System.Text.Encoding.UTF8.GetString(utf8Source[Start..End]);

    /// <summary>
    /// The text of the span in <paramref name="source"/>, the string that was parsed. The string is encoded
    /// to UTF-8 on each call; use <see cref="GetText(ReadOnlySpan{byte})"/> for many spans of one input.
    /// </summary>
    public string GetText(string source) => GetText(Utf8Source.FromString(source));

    public override string ToString() => $"[{Start}..{End})";
}
