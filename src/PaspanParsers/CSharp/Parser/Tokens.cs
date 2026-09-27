using System.Text;
using Paspan;
using Paspan.Fluent;

namespace PaspanParsers.CSharp;

// Token parsers. They read one C# token at the current position and do not skip
// trivia themselves: CSharpParser wraps them with Parsers.SkipWhiteSpace.

/// <summary>
/// White space, new lines and comments between tokens.
/// </summary>
internal sealed class TriviaParser : Parser<Region>
{
    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<Region> result)
    {
        var start = reader.GetCurrentPosition();
        var length = Lexer.ScanTrivia(reader.GetRemaining());
        reader.Read(length);
        result.Set(start, start + length, new Region(start, length));
        return true;
    }
}

/// <summary>
/// An identifier: any identifier-or-keyword token except a reserved keyword without '@'.
/// The value has the '@' removed and escapes decoded.
/// </summary>
internal sealed class IdentifierToken : Parser<string>
{
    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<string> result)
    {
        var s = reader.GetRemaining();
        var length = Lexer.ScanIdentifierOrKeyword(s, out var isVerbatim, out var hasEscape);
        if (length == 0)
        {
            return false;
        }

        var value = Lexer.IdentifierValue(s[..length], isVerbatim, hasEscape);
        if (!isVerbatim && !hasEscape && Lexer.ReservedKeywords.Contains(value))
        {
            return false;
        }

        var start = reader.GetCurrentPosition();
        reader.Read(length);
        result.Set(start, start + length, value);
        return true;
    }
}

/// <summary>
/// A keyword, reserved or contextual: the exact word, not followed by an identifier character.
/// </summary>
internal sealed class KeywordToken(string keyword) : Parser<string>
{
    private readonly byte[] _bytes = Encoding.UTF8.GetBytes(keyword);

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<string> result)
    {
        var s = reader.GetRemaining();
        if (!s.StartsWith(_bytes) || Lexer.IsIdentifierPartAt(s, _bytes.Length))
        {
            return false;
        }

        var start = reader.GetCurrentPosition();
        reader.Read(_bytes.Length);
        result.Set(start, start + _bytes.Length, keyword);
        return true;
    }

    public override string ToString() => $"Keyword '{keyword}'";
}

/// <summary>
/// An operator or punctuator. With maximal munch the token must not be the prefix of a longer
/// one at this position ('&lt;' does not match "&lt;="). Closing '&gt;' of type argument lists is
/// matched without maximal munch, so "List&lt;List&lt;int&gt;&gt;" works like in Roslyn.
/// </summary>
internal sealed class PunctuatorToken : Parser<string>
{
    private static readonly string[] Punctuators =
    [
        "{", "}", "[", "]", "(", ")", ".", ",", ":", ";", "+", "-", "*", "/", "%", "&", "|", "^", "!", "~",
        "=", "<", ">", "?", "??", "::", "++", "--", "&&", "||", "->", "==", "!=", "<=", ">=", "+=", "-=",
        "*=", "/=", "%=", "&=", "|=", "^=", "<<", "<<=", ">>", ">>=", ">>>", ">>>=", "=>", "??=", "..",
    ];

    private readonly string _text;
    private readonly byte[] _bytes;
    private readonly byte[][] _longer;

    public PunctuatorToken(string text, bool maximalMunch = true)
    {
        _text = text;
        _bytes = Encoding.UTF8.GetBytes(text);
        _longer = maximalMunch
            ? Punctuators.Where(p => p.Length > text.Length && p.StartsWith(text, StringComparison.Ordinal)).Select(Encoding.UTF8.GetBytes).ToArray()
            : [];
    }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<string> result)
    {
        var s = reader.GetRemaining();
        if (!s.StartsWith(_bytes))
        {
            return false;
        }

        foreach (var longer in _longer)
        {
            if (s.StartsWith(longer))
            {
                return false;
            }
        }

        var start = reader.GetCurrentPosition();
        reader.Read(_bytes.Length);
        result.Set(start, start + _bytes.Length, _text);
        return true;
    }

    public override string ToString() => $"'{_text}'";
}

/// <summary>
/// Integer and real literals: decimal, hexadecimal and binary, '_' separators, type suffixes.
/// </summary>
internal sealed class NumericLiteralToken : Parser<Expression>
{
    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<Expression> result)
    {
        var s = reader.GetRemaining();
        var length = Lexer.ScanNumericLiteral(s, out var value, out var kind);
        if (length == 0)
        {
            return false;
        }

        var start = reader.GetCurrentPosition();
        var text = Encoding.UTF8.GetString(s[..length]);
        reader.Read(length);
        result.Set(start, start + length, new LiteralExpression(value, kind, text));
        return true;
    }
}

/// <summary>
/// Character literals with all escape sequences.
/// </summary>
internal sealed class CharacterLiteralToken : Parser<Expression>
{
    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<Expression> result)
    {
        var s = reader.GetRemaining();
        var length = Lexer.ScanCharacterLiteral(s, out var value);
        if (length == 0)
        {
            return false;
        }

        var start = reader.GetCurrentPosition();
        var text = Encoding.UTF8.GetString(s[..length]);
        reader.Read(length);
        result.Set(start, start + length, new LiteralExpression(value, LiteralKind.Character, text));
        return true;
    }
}

/// <summary>
/// Regular, verbatim and raw string literals, optionally with the u8 suffix.
/// </summary>
internal sealed class StringLiteralToken : Parser<Expression>
{
    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<Expression> result)
    {
        var s = reader.GetRemaining();
        var length = Lexer.ScanStringLiteral(s, out var value, out var isUtf8);
        if (length == 0)
        {
            return false;
        }

        var start = reader.GetCurrentPosition();
        var text = Encoding.UTF8.GetString(s[..length]);
        reader.Read(length);

        var literal = isUtf8
            ? new LiteralExpression(Encoding.UTF8.GetBytes(value), LiteralKind.Utf8String, text)
            : new LiteralExpression(value, LiteralKind.String, text);

        result.Set(start, start + length, literal);
        return true;
    }
}

/// <summary>
/// Interpolated strings: $"...", $@"...", @$"..." and raw $"""...""" with any number of '$'.
/// Interpolation holes are parsed with the expression parser.
/// </summary>
internal sealed class InterpolatedStringToken(Parser<Expression> expression) : Parser<Expression>
{
    private enum Mode { Regular, Verbatim, Raw }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<Expression> result)
    {
        var start = reader.GetCurrentPosition();
        if (TryParse(ref reader, context, out var value))
        {
            result.Set(start, reader.GetCurrentPosition(), value);
            return true;
        }

        reader.RollBackState(start);
        return false;
    }

    private bool TryParse(ref SpanReader reader, ParseContext context, out Expression value)
    {
        value = null;
        var start = reader.GetCurrentPosition();
        var s = reader.GetRemaining();

        // Prefix: '$'s and an optional '@' on either side, then the opening quotes
        var i = 0;
        var isVerbatim = false;
        if (i < s.Length && s[i] == '@')
        {
            isVerbatim = true;
            i++;
        }

        var dollars = 0;
        while (i < s.Length && s[i] == '$')
        {
            dollars++;
            i++;
        }

        if (dollars == 0)
        {
            return false;
        }

        if (!isVerbatim && i < s.Length && s[i] == '@')
        {
            isVerbatim = true;
            i++;
        }

        if (i >= s.Length || s[i] != '"')
        {
            return false;
        }

        var quoteRun = 0;
        while (i + quoteRun < s.Length && s[i + quoteRun] == '"')
        {
            quoteRun++;
        }

        var mode = isVerbatim ? Mode.Verbatim : quoteRun >= 3 ? Mode.Raw : Mode.Regular;
        var quotes = mode == Mode.Raw ? quoteRun : 1;
        if (mode != Mode.Raw && dollars > 1)
        {
            return false;
        }

        i += quotes;

        var isMultiLine = false;
        if (mode == Mode.Raw && Lexer.IsRestOfLineWhiteSpace(s, i, out var contentStart))
        {
            isMultiLine = true;
            i = contentStart;
        }

        var startToken = Encoding.UTF8.GetString(s[..i]);
        var braces = mode == Mode.Raw ? dollars : 1;

        var contents = new List<InterpolatedStringContent>();
        var textStart = i;
        var endStart = 0;

        void AddText(ReadOnlySpan<byte> source, int end)
        {
            if (end > textStart)
            {
                contents.Add(new InterpolatedStringText(Encoding.UTF8.GetString(source[textStart..end]), null));
            }
        }

        while (true)
        {
            if (i >= s.Length)
            {
                return false;
            }

            var b = s[i];

            // End of the string
            if (b == '"')
            {
                if (mode == Mode.Verbatim && i + 1 < s.Length && s[i + 1] == '"')
                {
                    i += 2;
                    continue;
                }

                if (mode != Mode.Raw)
                {
                    AddText(s, i);
                    endStart = i;
                    i++;
                    break;
                }

                var run = CountRun(s, i, (byte)'"');
                if (!isMultiLine && run == quotes)
                {
                    AddText(s, i);
                    endStart = i;
                    i += run;
                    break;
                }

                if (run >= quotes)
                {
                    return false;
                }

                i += run;
                continue;
            }

            if (b == '{')
            {
                var run = CountRun(s, i, (byte)'{');
                if (mode != Mode.Raw && run >= 2)
                {
                    i += 2;
                    continue;
                }

                if (mode == Mode.Raw && run < braces)
                {
                    i += run;
                    continue;
                }

                // Extra braces before a raw interpolation are content
                var holeStart = i + run - braces;
                AddText(s, holeStart);

                reader.RollBackState(start + holeStart + braces);
                if (!ParseInterpolation(ref reader, context, braces, out var interpolation))
                {
                    return false;
                }

                contents.Add(interpolation);
                i = reader.GetCurrentPosition() - start;
                textStart = i;
                continue;
            }

            if (b == '}')
            {
                var run = CountRun(s, i, (byte)'}');
                if (mode != Mode.Raw)
                {
                    if (run < 2)
                    {
                        return false;
                    }

                    i += 2;
                    continue;
                }

                if (run >= braces)
                {
                    return false;
                }

                i += run;
                continue;
            }

            if (b == '\\' && mode == Mode.Regular)
            {
                var length = Lexer.EscapeSequenceLength(s, i);
                if (length == 0)
                {
                    return false;
                }

                i += length;
                continue;
            }

            var c = Lexer.DecodeChar(s, i, out var charLength);
            if (Lexer.IsNewLine(c))
            {
                if (mode == Mode.Regular || (mode == Mode.Raw && !isMultiLine))
                {
                    return false;
                }

                if (isMultiLine)
                {
                    // The closing line holds only white space and the closing quotes
                    var nextLine = i + Lexer.NewLineLength(s, i);
                    var lineEnd = Lexer.FindLineEnd(s, nextLine);
                    var indentEnd = Lexer.SkipWhiteSpace(s, nextLine, lineEnd);
                    if (indentEnd < lineEnd && CountRun(s, indentEnd, (byte)'"') >= quotes)
                    {
                        AddText(s, i);
                        endStart = i;
                        var indentation = Encoding.UTF8.GetString(s[nextLine..indentEnd]);
                        SetRawValues(contents, indentation);
                        i = indentEnd + quotes;
                        break;
                    }
                }
            }

            i += charLength;
        }

        if (mode != Mode.Raw || !isMultiLine)
        {
            SetValues(contents, mode);
        }

        reader.RollBackState(start + i);
        var endToken = Encoding.UTF8.GetString(s[endStart..i]);
        value = new InterpolatedStringExpression(startToken, contents, endToken, braces);
        return true;
    }

    private bool ParseInterpolation(ref SpanReader reader, ParseContext context, int braces, out Interpolation interpolation)
    {
        interpolation = null;

        var expressionResult = new ParseResult<Expression>();
        if (!expression.Parse(ref reader, context, ref expressionResult))
        {
            return false;
        }

        Expression alignment = null;
        string format = null;

        context.SkipWhiteSpace(ref reader);
        if (reader.Current == ',')
        {
            reader.Read(1);
            var alignmentResult = new ParseResult<Expression>();
            if (!expression.Parse(ref reader, context, ref alignmentResult))
            {
                return false;
            }

            alignment = alignmentResult.Value;
            context.SkipWhiteSpace(ref reader);
        }

        if (reader.Current == ':')
        {
            reader.Read(1);
            var s = reader.GetRemaining();
            var end = s.IndexOf((byte)'}');
            if (end < 0)
            {
                return false;
            }

            format = Encoding.UTF8.GetString(s[..end]);
            reader.Read(end);
        }

        var remaining = reader.GetRemaining();
        if (CountRun(remaining, 0, (byte)'}') < braces)
        {
            return false;
        }

        reader.Read(braces);
        interpolation = new Interpolation(expressionResult.Value, alignment, format);
        return true;
    }

    private static int CountRun(ReadOnlySpan<byte> s, int index, byte b)
    {
        var count = 0;
        while (index + count < s.Length && s[index + count] == b)
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Decodes the text parts of regular, verbatim and single-line raw strings.
    /// </summary>
    private static void SetValues(List<InterpolatedStringContent> contents, Mode mode)
    {
        for (var n = 0; n < contents.Count; n++)
        {
            if (contents[n] is InterpolatedStringText text)
            {
                contents[n] = new InterpolatedStringText(text.Text, DecodeText(text.Text, mode));
            }
        }
    }

    private static string DecodeText(string raw, Mode mode)
    {
        if (mode == Mode.Raw)
        {
            return raw;
        }

        var builder = new StringBuilder(raw.Length);
        var bytes = Encoding.UTF8.GetBytes(raw);
        var i = 0;
        while (i < bytes.Length)
        {
            var b = bytes[i];
            if ((b == '{' || b == '}') && i + 1 < bytes.Length && bytes[i + 1] == b)
            {
                builder.Append((char)b);
                i += 2;
            }
            else if (mode == Mode.Verbatim && b == '"' && i + 1 < bytes.Length && bytes[i + 1] == '"')
            {
                builder.Append('"');
                i += 2;
            }
            else if (mode == Mode.Regular && b == '\\' && Lexer.EscapeSequenceLength(bytes, i) is var length and > 0)
            {
                builder.Append(Lexer.UnescapeRegular(bytes.AsSpan(i, length)));
                i += length;
            }
            else
            {
                Lexer.DecodeChar(bytes, i, out var charLength);
                builder.Append(Encoding.UTF8.GetString(bytes, i, charLength));
                i += charLength;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Removes the closing line's indentation from each content line of a multi-line raw string.
    /// </summary>
    private static void SetRawValues(List<InterpolatedStringContent> contents, string indentation)
    {
        var lastText = contents.FindLastIndex(c => c is InterpolatedStringText);
        var atLineStart = true;

        for (var n = 0; n < contents.Count; n++)
        {
            if (contents[n] is not InterpolatedStringText text)
            {
                atLineStart = false;
                continue;
            }

            var lines = text.Text.ReplaceLineEndings("\n").Split('\n');
            var builder = new StringBuilder();
            for (var l = 0; l < lines.Length; l++)
            {
                var line = lines[l];
                if (l > 0)
                {
                    builder.Append('\n');
                }

                var startsLine = l > 0 || atLineStart;
                var endsLine = l < lines.Length - 1 || n == lastText && n == contents.Count - 1;

                if (startsLine && endsLine && string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                builder.Append(startsLine && line.StartsWith(indentation, StringComparison.Ordinal) ? line[indentation.Length..] : line);
            }

            contents[n] = new InterpolatedStringText(text.Text, builder.ToString());
            atLineStart = false;
        }
    }
}
