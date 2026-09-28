using PaspanParsers.Cpp;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// Checks <see cref="DocumentationComment"/> against the comments clang attaches to declarations. Each
/// comment we find for a declaration, a declarator or an enumerator must be the comment clang attaches to a
/// declaration located inside it; each comment clang attaches must be one we find for a node around its
/// declaration, unless it is in a place we do not look: inside a declaration before the name, or in a
/// condition, a handler or a lambda capture.
/// </summary>
/// <remarks>
/// Clang gives the range of the text of a comment: from after the markers and blank lines at its start to the
/// end of its last paragraph or command, and no range for a comment without text.
/// </remarks>
public static class CppDocumentationChecker
{
    private sealed record Documented(string Node, TextSpan Owner, TextSpan Comment);

    /// <summary>
    /// Returns null when the comments are clang's, otherwise a description of the first difference.
    /// </summary>
    public static string Check(byte[] utf8, TranslationUnit unit, IReadOnlyList<ClangComment> clangComments)
    {
        var ours = new List<Documented>();
        var trivia = new List<TextSpan>();
        var stack = new Stack<ICppNode>([unit]);
        while (stack.Count != 0)
        {
            var node = stack.Pop();
            switch (node)
            {
                case SimpleDeclaration simple:
                    foreach (var declarator in simple.Declarators)
                    {
                        Add(declarator, DocumentationComment.Find(utf8, simple, declarator));
                    }

                    break;
                case Enumerator enumerator:
                    trivia.Add(enumerator.LeadingTrivia);
                    Add(enumerator, DocumentationComment.Find(utf8, enumerator));
                    break;
            }

            if (node is Declaration declaration)
            {
                trivia.Add(declaration.LeadingTrivia);
                Add(declaration, DocumentationComment.Find(utf8, declaration));
            }

            foreach (var child in CppSpanChecker.Children(node))
            {
                stack.Push(child);
            }
        }

        foreach (var documented in ours)
        {
            if (!clangComments.Any(c => Contains(documented.Owner, c.Location) && Matches(utf8, documented.Comment, c.Content)))
            {
                var attached = clangComments.Where(c => Contains(documented.Owner, c.Location)).Take(3).Select(c => $"{c.Kind} at {c.Location}: {Describe(utf8, c.Content)}").ToList();
                return $"{documented.Node} {documented.Owner} has the documentation comment '{Text(utf8, documented.Comment)}', which clang does not attach to a declaration in it"
                    + (attached.Count == 0 ? "" : $" (clang attaches {string.Join(", ", attached)})");
            }
        }

        foreach (var comment in clangComments)
        {
            if (ours.Any(d => Contains(d.Owner, comment.Location) && Matches(utf8, d.Comment, comment.Content)))
            {
                continue;
            }

            // A comment inside a declaration before its name, or in a condition, is not looked for
            if (comment.Content is { } content && !IsTrailing(utf8, content) && !trivia.Any(t => t.Start <= content.Start && content.Start < t.End))
            {
                continue;
            }

            var found = ours.Where(d => Contains(d.Owner, comment.Location)).Take(3).Select(d => $"{d.Node} {d.Owner}: '{Text(utf8, d.Comment)}'").ToList();
            return $"clang attaches the documentation comment {Describe(utf8, comment.Content)} to the {comment.Kind} at {comment.Location}, which we do not find"
                + (found.Count == 0 ? "" : $" (found {string.Join(", ", found)})");
        }

        return null;

        void Add(ICppNode node, TextSpan? comment)
        {
            if (comment is { } span)
            {
                ours.Add(new Documented(node.GetType().Name, node.Span, span));
            }
        }
    }

    /// <summary>
    /// Our comment is clang's: clang's text starts after the markers and blank lines at the start of our
    /// comment, and ends in it. Clang leaves out empty lines at the start of a comment, and its range ends at the
    /// end of its last paragraph or command, which may be before the end of the comment: before a
    /// <c>\code</c> block, after <c>\p</c> in <c>/// Uses \p RHS.</c>. A comment without text has no range in clang.
    /// </summary>
    private static bool Matches(byte[] utf8, TextSpan ours, TextSpan? clang)
    {
        if (clang is not { } content)
        {
            return string.IsNullOrWhiteSpace(DocumentationComment.GetText(utf8, ours));
        }

        return ours.Start < content.Start && content.End <= ours.End && IsMarkersOnly(utf8.AsSpan(ours.Start, content.Start - ours.Start));
    }

    /// <summary>
    /// White space and the characters of comment markers: <c>///</c>, <c>//!</c>, <c>/**</c>, <c>/*!</c>, <c>*/</c>, <c>&lt;</c>.
    /// </summary>
    private static bool IsMarkersOnly(ReadOnlySpan<byte> text) => text.IndexOfAnyExcept(" \t\r\n\v\f/*!<"u8) < 0;

    private static bool IsTrailing(byte[] utf8, TextSpan content) => content.Start >= 4 && utf8[content.Start - 1] == '<'
        && utf8.AsSpan(content.Start - 4, 3) is var marker && (marker.SequenceEqual("///"u8) || marker.SequenceEqual("//!"u8) || marker.SequenceEqual("/**"u8) || marker.SequenceEqual("/*!"u8));

    private static bool Contains(TextSpan span, int offset) => span.Start <= offset && offset < span.End;

    private static string Describe(byte[] utf8, TextSpan? content) => content is { } span ? $"'{Text(utf8, span)}' {span}" : "without text";

    private static string Text(byte[] utf8, TextSpan span)
    {
        var text = span.GetText(utf8).ReplaceLineEndings(" ");
        return text.Length > 60 ? text[..57] + "..." : text;
    }
}
