using System.Text;

namespace PaspanParsers.CSharp;

/// <summary>
/// Reads documentation comments (<c>///</c> lines and <c>/** */</c> blocks) from the leading trivia of
/// declarations, which the parser records in <see cref="MemberDeclaration.LeadingTrivia"/> and
/// <see cref="EnumMember.LeadingTrivia"/>.
/// </summary>
/// <remarks>
/// Like Roslyn, every documentation comment in the trivia belongs to the declaration, also when blank lines,
/// other comments or directives separate it from the declaration. Text in an inactive <c>#if</c> branch is
/// trivia too and is not told apart: a <c>///</c> line in it counts.
/// </remarks>
public static class DocumentationComment
{
    /// <summary>
    /// The XML of the documentation comments of <paramref name="member"/>, without the comment markers,
    /// or null when it has none. <paramref name="utf8Source"/> is the parsed input (see <see cref="TextSpan"/>).
    /// </summary>
    public static string GetXml(ReadOnlySpan<byte> utf8Source, MemberDeclaration member) => GetXml(utf8Source, member.LeadingTrivia);

    /// <summary>
    /// The XML of the documentation comments of <paramref name="member"/>, or null when it has none.
    /// </summary>
    public static string GetXml(ReadOnlySpan<byte> utf8Source, EnumMember member) => GetXml(utf8Source, member.LeadingTrivia);

    /// <summary>
    /// The XML of the documentation comments in <paramref name="trivia"/>, a span of trivia of
    /// <paramref name="utf8Source"/>, without the comment markers; null when there are none.
    /// </summary>
    public static string GetXml(ReadOnlySpan<byte> utf8Source, TextSpan trivia)
    {
        if (trivia.IsEmpty || trivia.End > utf8Source.Length)
        {
            return null;
        }

        var bytes = utf8Source[trivia.Start..trivia.End];

        // Most declarations have no documentation comment: skip decoding the trivia
        if (bytes.IndexOf("///"u8) < 0 && bytes.IndexOf("/**"u8) < 0)
        {
            return null;
        }

        var text = Encoding.UTF8.GetString(bytes);
        StringBuilder xml = null;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (Starts(text, i, "///") && !Starts(text, i, "////"))
            {
                var end = LineEnd(text, i);
                var line = text[(i + 3)..end];
                AppendLine(ref xml, line.StartsWith(' ') ? line[1..] : line);
                i = end;
            }
            else if (Starts(text, i, "/**") && !Starts(text, i, "/**/"))
            {
                var close = text.IndexOf("*/", i + 3, StringComparison.Ordinal);
                var end = close < 0 ? text.Length : close;
                AppendBlock(ref xml, text[(i + 3)..end]);
                i = close < 0 ? text.Length : close + 2;
            }
            else if (Starts(text, i, "/*"))
            {
                var close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = close < 0 ? text.Length : close + 2;
            }
            else
            {
                // A '//' comment, a directive or inactive text
                i = LineEnd(text, i);
            }
        }

        return xml?.ToString();
    }

    private static bool Starts(string text, int index, string value) => string.CompareOrdinal(text, index, value, 0, value.Length) == 0;

    private static int LineEnd(string text, int index)
    {
        var end = text.IndexOfAny(['\r', '\n', '\u0085', '\u2028', '\u2029'], index);
        return end < 0 ? text.Length : end;
    }

    private static void AppendLine(ref StringBuilder xml, string line)
    {
        if (xml == null)
        {
            xml = new StringBuilder();
        }
        else
        {
            xml.Append('\n');
        }

        xml.Append(line.TrimEnd());
    }

    /// <summary>
    /// The lines of a <c>/** */</c> comment, without the '*' that starts continuation lines.
    /// </summary>
    private static void AppendBlock(ref StringBuilder xml, string block)
    {
        var lines = block.ReplaceLineEndings("\n").Split('\n');
        for (var n = 0; n < lines.Length; n++)
        {
            var line = lines[n].TrimStart();
            if (line.StartsWith('*'))
            {
                line = line[1..];
            }

            if (line.StartsWith(' '))
            {
                line = line[1..];
            }

            // The text after '/**' and before '*/' is often empty
            if (line.Trim().Length == 0 && (n == 0 || n == lines.Length - 1))
            {
                continue;
            }

            AppendLine(ref xml, line);
        }
    }
}
