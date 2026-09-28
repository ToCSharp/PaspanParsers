using System.Text;

namespace PaspanParsers.Cpp;

/// <summary>
/// Finds the documentation comments of declarations and reads their text, following the rules clang uses
/// to attach comments to declarations (<c>ASTContext::getRawCommentForDeclNoCache</c>). Documentation
/// comments are Doxygen's <c>///</c> and <c>//!</c> lines and <c>/** */</c> and <c>/*! */</c> blocks; like clang,
/// <c>////</c> and <c>/**/</c> count too.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Documentation comments separated only by white space with at most one line break form one comment,
/// also when their kinds differ (<c>/// a</c> and <c>/** b */</c> on the next line).</item>
/// <item>The comment of a declaration is the last documentation comment before it, unless the text between
/// them contains <c>;</c>, <c>{</c>, <c>}</c>, <c>#</c> or <c>@</c> (another declaration, a directive).
/// Ordinary comments between them do not matter, unless they contain those characters, and neither do
/// access specifiers: <c>/// The x.\npublic:\nint x;</c> documents <c>public:</c> and <c>x</c>.</item>
/// <item>A trailing comment (<c>///&lt;</c>, <c>//!&lt;</c>, <c>/**&lt;</c>, <c>/*!&lt;</c>) documents the variable,
/// field or enumerator on whose line it starts, after its name: <c>int x; ///&lt; The x.</c>. It comes
/// before a comment in front of the declaration, and never documents the declaration after it.</item>
/// </list>
/// Clang checks the text between the comment and the declared name; <see cref="Find(ReadOnlySpan{byte}, Declaration)"/>
/// looks at the trivia before the declaration, which also covers what the declaration declares first:
/// <c>/** A point. */ struct Point { int x; } origin;</c> documents <c>Point</c> for clang and the
/// declaration here, but not <c>origin</c>, which <see cref="Find(ReadOnlySpan{byte}, SimpleDeclaration, InitDeclarator)"/>
/// tells. Comments inside a declaration before its name (<c>int /** x */ x;</c>) are found only for
/// declarators, and those in conditions, <c>for</c> ranges, handlers and lambda captures not at all.
/// </remarks>
public static class DocumentationComment
{
    /// <summary>
    /// The documentation comment of <paramref name="declaration"/>, or null. <paramref name="utf8Source"/> is
    /// the parsed input (see <see cref="TextSpan"/>). The comment spans from the start of its first part to
    /// the end of its last; <see cref="GetText(ReadOnlySpan{byte}, TextSpan)"/> reads it. A declaration of a
    /// single variable or field may have a trailing comment.
    /// </summary>
    public static TextSpan? Find(ReadOnlySpan<byte> utf8Source, Declaration declaration)
    {
        if (declaration is SimpleDeclaration { Declarators: [var only] } simple && CanHaveTrailingComment(simple, only)
            && Trailing(utf8Source, NameLocation(only)) is { } trailing)
        {
            return trailing;
        }

        return Leading(utf8Source, declaration.DocumentationStart ?? declaration.LeadingTrivia.Start, declaration.LeadingTrivia.End);
    }

    /// <summary>
    /// The documentation comment of the entity that <paramref name="declarator"/> of <paramref name="declaration"/>
    /// declares, or null: its trailing comment, or the last documentation comment before its name, in or
    /// after the leading trivia of the declaration. <c>/// Coordinates.\nint x, y;</c> documents both.
    /// </summary>
    public static TextSpan? Find(ReadOnlySpan<byte> utf8Source, SimpleDeclaration declaration, InitDeclarator declarator)
    {
        var name = NameLocation(declarator);
        if (CanHaveTrailingComment(declaration, declarator) && Trailing(utf8Source, name) is { } trailing)
        {
            return trailing;
        }

        return Leading(utf8Source, declaration.DocumentationStart ?? declaration.LeadingTrivia.Start, name);
    }

    /// <summary>
    /// The documentation comment of <paramref name="enumerator"/>, or null: its trailing comment
    /// (<c>Red, ///&lt; The color red.</c>), or the one before it.
    /// </summary>
    public static TextSpan? Find(ReadOnlySpan<byte> utf8Source, Enumerator enumerator)
    {
        return Trailing(utf8Source, enumerator.Span.Start) ?? Leading(utf8Source, enumerator.LeadingTrivia.Start, enumerator.Span.Start);
    }

    /// <summary>
    /// The text of the documentation comment of <paramref name="declaration"/> (see <see cref="GetText(ReadOnlySpan{byte}, TextSpan)"/>), or null.
    /// </summary>
    public static string GetText(ReadOnlySpan<byte> utf8Source, Declaration declaration)
    {
        return Find(utf8Source, declaration) is { } comment ? GetText(utf8Source, comment) : null;
    }

    /// <summary>
    /// The text of the documentation comment of <paramref name="enumerator"/>, or null.
    /// </summary>
    public static string GetText(ReadOnlySpan<byte> utf8Source, Enumerator enumerator)
    {
        return Find(utf8Source, enumerator) is { } comment ? GetText(utf8Source, comment) : null;
    }

    /// <summary>
    /// The text of the documentation comments in <paramref name="comment"/>, lines separated by <c>\n</c>:
    /// without the markers <c>///</c>, <c>//!</c>, <c>/**</c>, <c>/*!</c>, <c>*/</c> and the <c>&lt;</c> of a
    /// trailing comment, one space after them, the <c>*</c> that starts a line of a block, the white space
    /// at the ends of lines, and the empty first and last lines of a block. Doxygen commands are kept as
    /// written: <c>\brief Adds.</c>
    /// </summary>
    public static string GetText(ReadOnlySpan<byte> utf8Source, TextSpan comment)
    {
        var text = new StringBuilder();
        var first = true;
        foreach (var part in Comments(utf8Source, comment.Start, Math.Min(comment.End, utf8Source.Length)))
        {
            if (!part.IsDocumentation)
            {
                continue;
            }

            var markers = part.IsTrailing ? 4 : 3;
            var block = utf8Source[part.Start..part.End];
            var isBlock = block[1] == '*';
            var content = Encoding.UTF8.GetString(block[markers..(isBlock ? Math.Max(markers, block.Length - 2) : block.Length)]);
            if (!isBlock)
            {
                // A line splice continues a '//' comment on the next line
                AppendLine(text, ref first, WithoutOneSpace(RemoveSplices(content)));
                continue;
            }

            var lines = content.ReplaceLineEndings("\n").Split('\n');
            for (var n = 0; n < lines.Length; n++)
            {
                var line = lines[n];
                if (n > 0)
                {
                    line = line.TrimStart();
                    if (line.StartsWith('*'))
                    {
                        line = line[1..];
                    }
                }

                // The text after '/**' and before '*/' is often empty
                if (line.Trim().Length == 0 && lines.Length > 1 && (n == 0 || n == lines.Length - 1))
                {
                    continue;
                }

                AppendLine(text, ref first, WithoutOneSpace(line));
            }
        }

        return text.ToString();
    }

    // ========================================
    // Clang's rules
    // ========================================

    /// <summary>
    /// Clang attaches trailing comments only to variables, fields and enumerators: not to functions and typedefs.
    /// </summary>
    private static bool CanHaveTrailingComment(SimpleDeclaration declaration, InitDeclarator declarator)
    {
        return (declarator.Declarator == null || !SyntaxParser.DeclaresFunction(declarator.Declarator))
            && declaration.Specifiers?.Specifiers.Any(s => s is KeywordSpecifier { Keyword: "typedef" }) != true;
    }

    /// <summary>
    /// The last group of documentation comments in <paramref name="utf8Source"/> from <paramref name="start"/>
    /// to <paramref name="end"/> (the location of a declaration), unless it is trailing or the text after it
    /// contains one of <c>;{}#@</c>.
    /// </summary>
    private static TextSpan? Leading(ReadOnlySpan<byte> utf8Source, int start, int end)
    {
        if (start < 0 || end > utf8Source.Length || start >= end || !MayContainDocumentation(utf8Source[start..end]))
        {
            return null;
        }

        var comments = Comments(utf8Source, start, end).Where(c => c.IsDocumentation).ToList();
        if (comments.Count == 0)
        {
            return null;
        }

        var last = comments[^1];
        if (last.IsTrailing || utf8Source[last.End..end].IndexOfAny(";{}#@"u8) >= 0)
        {
            return null;
        }

        var first = comments.Count - 1;
        while (first > 0 && CanMerge(utf8Source, comments[first - 1], comments[first]))
        {
            first--;
        }

        return new TextSpan(comments[first].Start, last.End);
    }

    /// <summary>
    /// The trailing comment that starts on the line of <paramref name="name"/>, after it, with the trailing
    /// comments merged with it; null when the first documentation comment after the name on its line is
    /// not trailing, or there is none.
    /// </summary>
    private static TextSpan? Trailing(ReadOnlySpan<byte> utf8Source, int name)
    {
        if (name < 0 || name >= utf8Source.Length)
        {
            return null;
        }

        var i = name;
        while (i < utf8Source.Length)
        {
            var b = utf8Source[i];
            if (b is (byte)'\n' or (byte)'\r' || Lexer.SpliceLength(utf8Source, i) != 0)
            {
                return null;
            }

            if (b is (byte)' ' or (byte)'\t' or (byte)'\v' or (byte)'\f')
            {
                i++;
                continue;
            }

            if (TryScanComment(utf8Source, i, out var comment))
            {
                if (comment.IsDocumentation)
                {
                    return comment.IsTrailing ? new TextSpan(comment.Start, MergedEnd(utf8Source, comment)) : null;
                }

                // Past an ordinary comment that ends the line or spans lines, later comments start on other lines
                if (utf8Source[comment.Start..comment.End].IndexOfAny((byte)'\n', (byte)'\r') >= 0 || utf8Source[comment.Start + 1] == '/')
                {
                    return null;
                }

                i = comment.End;
                continue;
            }

            var length = TokenLength(utf8Source, i);
            if (utf8Source.Slice(i, length).IndexOfAny((byte)'\n', (byte)'\r') >= 0)
            {
                // A raw string literal that spans lines
                return null;
            }

            i += length;
        }

        return null;
    }

    /// <summary>
    /// The end of <paramref name="comment"/> merged with the comments after it that clang merges with it.
    /// </summary>
    private static int MergedEnd(ReadOnlySpan<byte> utf8Source, Comment comment)
    {
        while (true)
        {
            var next = comment.End;
            while (next < utf8Source.Length && utf8Source[next] is (byte)' ' or (byte)'\t' or (byte)'\v' or (byte)'\f' or (byte)'\r' or (byte)'\n')
            {
                next++;
            }

            if (!TryScanComment(utf8Source, next, out var following) || !following.IsDocumentation || !CanMerge(utf8Source, comment, following))
            {
                return comment.End;
            }

            comment = following;
        }
    }

    /// <summary>
    /// Clang merges two documentation comments that are both trailing or both not, when only white space
    /// with at most one line break separates them.
    /// </summary>
    private static bool CanMerge(ReadOnlySpan<byte> utf8Source, Comment first, Comment second)
    {
        if (first.IsTrailing != second.IsTrailing)
        {
            return false;
        }

        var lineBreaks = 0;
        for (var i = first.End; i < second.Start; i++)
        {
            switch (utf8Source[i])
            {
                case (byte)' ' or (byte)'\t' or (byte)'\v' or (byte)'\f':
                    break;
                case (byte)'\r' or (byte)'\n':
                    if (++lineBreaks > 1)
                    {
                        return false;
                    }

                    // \r\n and \n\r are one line break
                    if (i + 1 < second.Start && utf8Source[i + 1] is (byte)'\r' or (byte)'\n' && utf8Source[i + 1] != utf8Source[i])
                    {
                        i++;
                    }

                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Where clang locates the declaration of <paramref name="declarator"/>: at the unqualified name, at
    /// <c>operator</c> or <c>~</c>, at the '[' of a structured binding; at the start of an unnamed bit-field.
    /// </summary>
    private static int NameLocation(InitDeclarator declarator)
    {
        if (declarator.Declarator == null)
        {
            return declarator.Span.Start;
        }

        if (SyntaxParser.StructuredBinding(declarator.Declarator) is { } binding)
        {
            return binding.Span.Start;
        }

        var name = SyntaxParser.DeclaredName(declarator.Declarator)?.Name;
        while (true)
        {
            switch (name)
            {
                case null:
                    return declarator.Span.Start;
                case QualifiedName qualified:
                    name = qualified.Name;
                    continue;
                case TemplateIdName templateId:
                    name = templateId.Template;
                    continue;
                default:
                    return name.Span.Start;
            }
        }
    }

    // ========================================
    // Scanning
    // ========================================

    /// <summary>
    /// A comment: <see cref="End"/> is before the line break that ends a <c>//</c> comment.
    /// </summary>
    private readonly record struct Comment(int Start, int End, bool IsDocumentation, bool IsTrailing);

    private static bool MayContainDocumentation(ReadOnlySpan<byte> text)
    {
        return text.IndexOf("//"u8) >= 0 || text.IndexOf("/*"u8) >= 0;
    }

    /// <summary>
    /// The comments that start in <paramref name="utf8Source"/> from <paramref name="start"/> to
    /// <paramref name="end"/>, which starts a token or trivia; the tokens and directives between them are skipped.
    /// </summary>
    private static List<Comment> Comments(ReadOnlySpan<byte> utf8Source, int start, int end)
    {
        var comments = new List<Comment>();
        var i = start;
        while (i < end)
        {
            if (TryScanComment(utf8Source, i, out var comment))
            {
                comments.Add(comment);
                i = comment.End;
            }
            else if (utf8Source[i] is (byte)' ' or (byte)'\t' or (byte)'\v' or (byte)'\f' or (byte)'\r' or (byte)'\n')
            {
                i++;
            }
            else
            {
                i += TokenLength(utf8Source, i);
            }
        }

        return comments;
    }

    private static bool TryScanComment(ReadOnlySpan<byte> utf8Source, int start, out Comment comment)
    {
        comment = default;
        if (start + 1 >= utf8Source.Length || utf8Source[start] != '/')
        {
            return false;
        }

        int end;
        bool isDocumentation;
        if (utf8Source[start + 1] == '/')
        {
            // A line splice continues the comment on the next line
            end = start + 2;
            while (end < utf8Source.Length && utf8Source[end] is not ((byte)'\n' or (byte)'\r'))
            {
                end += Math.Max(1, Lexer.SpliceLength(utf8Source, end));
            }

            isDocumentation = end - start >= 3 && utf8Source[start + 2] is (byte)'/' or (byte)'!';
        }
        else if (utf8Source[start + 1] == '*')
        {
            var close = utf8Source[(start + 2)..].IndexOf("*/"u8);
            if (close < 0)
            {
                return false;
            }

            end = start + 2 + close + 2;
            isDocumentation = utf8Source[start + 2] is (byte)'*' or (byte)'!';
        }
        else
        {
            return false;
        }

        var isTrailing = isDocumentation && end - start > 3 && utf8Source[start + 3] == '<';
        comment = new Comment(start, end, isDocumentation, isTrailing);
        return true;
    }

    /// <summary>
    /// The length of the token, or the character of a directive or of an inactive branch, at <paramref name="index"/>:
    /// literals are skipped whole, so that the comment markers in them are not comments.
    /// </summary>
    private static int TokenLength(ReadOnlySpan<byte> utf8Source, int index)
    {
        var rest = utf8Source[index..];
        var b = rest[0];
        if (Lexer.IsIdentifierStart(b) || b is (byte)'"' or (byte)'\'')
        {
            // An unterminated quote, possible in an inactive branch, is one character
            var literal = Lexer.ScanQuotedLiteral(rest, out _);
            if (literal > 0)
            {
                return literal;
            }

            return b is (byte)'"' or (byte)'\'' ? 1 : Math.Max(1, Lexer.ScanIdentifier(rest));
        }

        if (Lexer.IsDecimalDigit(b) || (b == '.' && rest.Length > 1 && Lexer.IsDecimalDigit(rest[1])))
        {
            // A pp-number, whose digit separators are no quotes: 1'000
            return Math.Max(1, Lexer.ScanNumber(rest, out _));
        }

        return 1;
    }

    // ========================================
    // Text
    // ========================================

    private static void AppendLine(StringBuilder text, ref bool first, string line)
    {
        if (!first)
        {
            text.Append('\n');
        }

        first = false;
        text.Append(line.TrimEnd());
    }

    private static string WithoutOneSpace(string line) => line.StartsWith(' ') ? line[1..] : line;

    private static string RemoveSplices(string line)
    {
        return line.Contains('\\') ? line.Replace("\\\r\n", "").Replace("\\\n", "").Replace("\\\r", "") : line;
    }
}
