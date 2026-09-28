using System.Text;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// The tokens of C++ text, spelled as the parser spells them and concatenated without white space: to
/// compare the text of a node's span with what <see cref="PaspanParsers.Cpp.CppWriter"/> writes for the node.
/// Comments, white space and line splices are dropped, digraphs and alternative tokens become the operators
/// they stand for (<c>&lt;%</c> is <c>{</c>, <c>and</c> is <c>&amp;&amp;</c>) and universal character names in
/// identifiers become their characters. Literals are kept as written.
/// </summary>
public static class CppNodeText
{
    private static readonly Dictionary<string, string> AlternativeTokens = new(StringComparer.Ordinal)
    {
        ["and"] = "&&", ["or"] = "||", ["not"] = "!", ["bitand"] = "&", ["bitor"] = "|", ["xor"] = "^", ["compl"] = "~",
        ["and_eq"] = "&=", ["or_eq"] = "|=", ["xor_eq"] = "^=", ["not_eq"] = "!=",
    };

    /// <summary>
    /// The normalized tokens of <paramref name="utf8"/> from <paramref name="span"/>, without the parts in
    /// <paramref name="skipped"/> (sorted spans of directives and inactive branches); null when the text has
    /// an identifier with a named character (<c>\N{...}</c>), which the parser does not decode.
    /// </summary>
    public static string OfSource(byte[] utf8, TextSpan span, IReadOnlyList<TextSpan> skipped)
    {
        var text = new StringBuilder();
        var start = span.Start;
        foreach (var skip in skipped)
        {
            if (skip.End <= start)
            {
                continue;
            }

            if (skip.Start >= span.End)
            {
                break;
            }

            if (skip.Start > start)
            {
                text.Append(Encoding.UTF8.GetString(utf8, start, skip.Start - start)).Append('\n');
            }

            start = Math.Max(start, skip.End);
        }

        if (start < span.End)
        {
            text.Append(Encoding.UTF8.GetString(utf8, start, span.End - start));
        }

        return Normalize(text.ToString());
    }

    /// <summary>
    /// The normalized tokens of text that <see cref="PaspanParsers.Cpp.CppWriter"/> wrote, without the
    /// <paramref name="directives"/> it wrote on their own lines.
    /// </summary>
    public static string OfWritten(string written, IEnumerable<string> directives)
    {
        written = "\n" + written.ReplaceLineEndings("\n");
        foreach (var directive in directives.Select(d => d.ReplaceLineEndings("\n")).Distinct().OrderByDescending(d => d.Length))
        {
            // The same directive may be written on consecutive lines, whose line breaks the replacements share
            string previous;
            do
            {
                previous = written;
                written = written.Replace("\n" + directive + "\n", "\n\n");
            }
            while (written != previous);
        }

        return Normalize(written);
    }

    private static string Normalize(string text)
    {
        text = text.Replace("\\\r\n", "").Replace("\\\n", "").Replace("\\\r", "");
        var result = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
            }
            else if (c == '/' && At(text, i + 1, "/"))
            {
                while (i < text.Length && text[i] is not ('\n' or '\r'))
                {
                    i++;
                }
            }
            else if (c == '/' && At(text, i + 1, "*"))
            {
                var close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = close < 0 ? text.Length : close + 2;
            }
            else if (c is '"' or '\'')
            {
                i = Literal(text, i, result);
            }
            else if (IsWordStart(c) || c == '\\')
            {
                var start = i;
                var word = Word(text, ref i);
                if (word == null)
                {
                    return null;
                }

                if (i < text.Length && text[i] is '"' or '\'' && word is "L" or "u" or "U" or "u8" or "R" or "LR" or "uR" or "UR" or "u8R")
                {
                    // An encoding prefix, or a raw string
                    i = word.EndsWith('R') && text[i] == '"' ? RawString(text, start, i, result) : Literal(text, start, result, i);
                    continue;
                }

                result.Append(AlternativeTokens.GetValueOrDefault(word, word));
            }
            else if (char.IsAsciiDigit(c) || (c == '.' && i + 1 < text.Length && char.IsAsciiDigit(text[i + 1])))
            {
                i = Number(text, i, result);
            }
            else
            {
                i = Punctuator(text, i, result);
            }
        }

        return result.ToString();
    }

    private static bool At(string text, int index, string value) => index <= text.Length - value.Length && string.CompareOrdinal(text, index, value, 0, value.Length) == 0;

    private static bool IsWordStart(char c) => char.IsAsciiLetter(c) || c is '_' or '$' || c >= 0x80;

    /// <summary>
    /// An identifier or keyword with its universal character names decoded; null for a named character.
    /// </summary>
    private static string Word(string text, ref int i)
    {
        var word = new StringBuilder();
        while (i < text.Length)
        {
            var c = text[i];
            if (IsWordStart(c) || char.IsAsciiDigit(c))
            {
                word.Append(c);
                i++;
            }
            else if (c == '\\' && At(text, i + 1, "N{"))
            {
                return null;
            }
            else if (c == '\\' && i + 1 < text.Length && text[i + 1] is 'u' or 'U')
            {
                var digits = text[i + 1] == 'u' ? 4 : 8;
                string hex;
                if (At(text, i + 2, "{"))
                {
                    var close = text.IndexOf('}', i + 3);
                    hex = text[(i + 3)..close];
                    i = close + 1;
                }
                else
                {
                    hex = text.Substring(i + 2, digits);
                    i += 2 + digits;
                }

                word.Append(char.ConvertFromUtf32(Convert.ToInt32(hex, 16)));
            }
            else
            {
                break;
            }
        }

        if (word.Length == 0)
        {
            // A backslash that starts no universal character name
            word.Append(text[i++]);
        }

        return word.ToString();
    }

    /// <summary>
    /// A character or string literal from <paramref name="start"/> (its prefix), whose quote is at
    /// <paramref name="quote"/>, with its user-defined suffix.
    /// </summary>
    private static int Literal(string text, int start, StringBuilder result, int quote = -1)
    {
        quote = quote < 0 ? start : quote;
        var delimiter = text[quote];
        var i = quote + 1;
        while (i < text.Length && text[i] != delimiter && text[i] is not ('\n' or '\r'))
        {
            i += text[i] == '\\' ? 2 : 1;
        }

        i = Math.Min(text.Length, i + 1);
        i = Suffix(text, i);
        result.Append(text, start, i - start);
        return i;
    }

    private static int RawString(string text, int start, int quote, StringBuilder result)
    {
        var open = text.IndexOf('(', quote);
        var delimiter = ")" + text[(quote + 1)..open] + "\"";
        var close = text.IndexOf(delimiter, open, StringComparison.Ordinal);
        var i = close < 0 ? text.Length : Suffix(text, close + delimiter.Length);
        result.Append(text, start, i - start);
        return i;
    }

    private static int Suffix(string text, int i)
    {
        while (i < text.Length && (IsWordStart(text[i]) || char.IsAsciiDigit(text[i])))
        {
            i++;
        }

        return i;
    }

    /// <summary>
    /// A preprocessing number: digits, identifier characters, digit separators, '.' and exponent signs.
    /// </summary>
    private static int Number(string text, int i, StringBuilder result)
    {
        var start = i;
        while (i < text.Length)
        {
            var c = text[i];
            if (c is '+' or '-' && text[i - 1] is 'e' or 'E' or 'p' or 'P')
            {
                i++;
            }
            else if (c == '\'' && i + 1 < text.Length && (IsWordStart(text[i + 1]) || char.IsAsciiDigit(text[i + 1])))
            {
                i += 2;
            }
            else if (IsWordStart(c) || char.IsAsciiDigit(c) || c == '.')
            {
                i++;
            }
            else
            {
                break;
            }
        }

        result.Append(text, start, i - start);
        return i;
    }

    /// <summary>
    /// A punctuator character, or a digraph as the operator it stands for. <c>&lt;::</c> is <c>&lt;</c> and
    /// <c>::</c> unless <c>:</c> or <c>&gt;</c> follows.
    /// </summary>
    private static int Punctuator(string text, int i, StringBuilder result)
    {
        if (At(text, i, "<:") && !(At(text, i, "<::") && !At(text, i, "<:::") && !At(text, i, "<::>")))
        {
            result.Append('[');
            return i + 2;
        }

        if (At(text, i, ":>"))
        {
            result.Append(']');
            return i + 2;
        }

        if (At(text, i, "<%"))
        {
            result.Append('{');
            return i + 2;
        }

        if (At(text, i, "%>"))
        {
            result.Append('}');
            return i + 2;
        }

        if (At(text, i, "%:%:"))
        {
            result.Append("##");
            return i + 4;
        }

        if (At(text, i, "%:"))
        {
            result.Append('#');
            return i + 2;
        }

        result.Append(text[i]);
        return i + 1;
    }
}
