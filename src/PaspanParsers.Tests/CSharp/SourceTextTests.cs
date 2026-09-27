using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PaspanParsers.CSharp;

namespace PaspanParsers.Tests.CSharp;

/// <summary>
/// Parsing UTF-8 bytes, <see cref="LineMap"/> and <see cref="DocumentationComment"/>.
/// </summary>
[TestClass]
public class SourceTextTests
{
    public TestContext TestContext { get; set; }

    // ========================================
    // UTF-8 input
    // ========================================

    [TestMethod]
    public void TryParse_Utf8Bytes_GivesTheSameSpansAsTheString()
    {
        const string source = "// ünïcödé\nclass C { void M() { var s = \"😀\"; } }\n";
        Assert.IsTrue(CSharpParser.TryParse(source, null, out var fromString, out _));

        var withBom = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(source)).ToArray();
        Assert.IsTrue(CSharpParser.TryParse(withBom, null, out var fromBytes, out _));

        var utf8 = CSharpParser.GetUtf8Source(withBom);
        Assert.AreEqual(withBom.Length - 3, utf8.Length);
        CollectionAssert.AreEqual(CSharpParser.GetUtf8Source(source), utf8.ToArray());

        var method = (MethodDeclaration)((ClassDeclaration)fromBytes.Members[0]).Members[0];
        var expected = (MethodDeclaration)((ClassDeclaration)fromString.Members[0]).Members[0];
        Assert.AreEqual(expected.Span, method.Span);
        Assert.AreEqual("void M() { var s = \"😀\"; }", method.Span.GetText(utf8.Span));
    }

    [TestMethod]
    public void TryParse_Utf8Bytes_WithRecovery()
    {
        var utf8 = Encoding.UTF8.GetBytes("class C { int x = ; void M() { } }");
        Assert.IsTrue(CSharpParser.TryParse(utf8, new PaspanParsers.CSharp.CSharpParseOptions(errorRecovery: true), out var unit, out _));
        Assert.HasCount(1, unit.Errors);
    }

    // ========================================
    // LineMap
    // ========================================

    [TestMethod]
    public void LineMap_LinesAndColumns()
    {
        var utf8 = CSharpParser.GetUtf8Source("ab\ncd\r\nef\rgh\u2028ij\u0085kl");
        var map = new LineMap(utf8);

        Assert.AreEqual(6, map.LineCount);
        Assert.AreEqual((1, 1), map.GetLineAndColumn(0));
        Assert.AreEqual((1, 3), map.GetLineAndColumn(2));
        Assert.AreEqual((2, 1), map.GetLineAndColumn(3));
        Assert.AreEqual((3, 1), map.GetLineAndColumn(7));
        Assert.AreEqual((4, 1), map.GetLineAndColumn(10));
        Assert.AreEqual((5, 1), map.GetLineAndColumn(15));
        Assert.AreEqual((6, 1), map.GetLineAndColumn(19));
        Assert.AreEqual((6, 3), map.GetLineAndColumn(utf8.Length));

        Assert.AreEqual("cd", map.GetLineSpan(2).GetText(utf8));
        Assert.AreEqual("gh", map.GetLineSpan(4).GetText(utf8));
        Assert.AreEqual("ij", map.GetLineSpan(5).GetText(utf8));
    }

    [TestMethod]
    public void LineMap_ColumnsCountUtf16CodeUnits()
    {
        const string line = "var é = \"😀x\";";
        var utf8 = CSharpParser.GetUtf8Source(line);
        var map = new LineMap(utf8);

        // Every UTF-16 position of the line maps to a byte offset and back
        for (var column = 1; column <= line.Length + 1; column++)
        {
            if (column > 1 && char.IsHighSurrogate(line[column - 2]))
            {
                continue;
            }

            var offset = map.GetOffset(1, column);
            Assert.AreEqual(Encoding.UTF8.GetByteCount(line.AsSpan(0, column - 1)), offset, $"column {column}");
            Assert.AreEqual((1, column), map.GetLineAndColumn(offset));
        }

        Assert.AreEqual(utf8.Length, map.GetOffset(1, 100));
    }

    [TestMethod]
    public void LineMap_AgreesWithRoslyn_OnTheCorpus()
    {
        foreach (var path in CorpusFiles().Take(40))
        {
            var source = File.ReadAllText(path);
            var utf8 = CSharpParser.GetUtf8Source(source);
            var map = new LineMap(utf8);
            var text = Microsoft.CodeAnalysis.Text.SourceText.From(source.TrimStart('﻿'));

            var offsets = ByteOffsets(source.TrimStart('﻿'));
            for (var i = 0; i < text.Length; i += 37)
            {
                var roslyn = text.Lines.GetLinePosition(i);
                Assert.AreEqual((roslyn.Line + 1, roslyn.Character + 1), map.GetLineAndColumn(offsets[i]), $"{path} at {i}");
            }
        }
    }

    // ========================================
    // Documentation comments
    // ========================================

    [TestMethod]
    public void DocumentationComment_SingleLine()
    {
        const string source = """
            class C
            {
                /// <summary>
                /// Does it.
                /// </summary>
                [Obsolete]
                public void M() { }

                // Not documentation
                int f;
            }
            """;
        var (utf8, members) = Members(source);

        Assert.AreEqual("<summary>\nDoes it.\n</summary>", DocumentationComment.GetXml(utf8, members[0]));
        Assert.IsNull(DocumentationComment.GetXml(utf8, members[1]));
    }

    [TestMethod]
    public void DocumentationComment_MultiLine_AndEnumMembers()
    {
        const string source = """
            /**
             * <summary>Colors.</summary>
             */
            enum E
            {
                /// <summary>Red.</summary>
                Red,

                Green,
            }
            """;
        Assert.IsTrue(CSharpParser.TryParse(source, null, out var unit, out _));
        var utf8 = CSharpParser.GetUtf8Source(source);
        var enumDeclaration = (EnumDeclaration)unit.Members[0];

        Assert.AreEqual("<summary>Colors.</summary>", DocumentationComment.GetXml(utf8, enumDeclaration));
        Assert.AreEqual("<summary>Red.</summary>", DocumentationComment.GetXml(utf8, enumDeclaration.Members[0]));
        Assert.IsNull(DocumentationComment.GetXml(utf8, enumDeclaration.Members[1]));
    }

    [TestMethod]
    public void DocumentationComment_FourSlashes_IsNotDocumentation()
    {
        var (utf8, members) = Members("class C\n{\n    //// <summary>x</summary>\n    void M() { }\n}\n");
        Assert.IsNull(DocumentationComment.GetXml(utf8, members[0]));
    }

    /// <summary>
    /// For every member of the corpus with single-line documentation comments, the comment text read from
    /// our <see cref="MemberDeclaration.LeadingTrivia"/> is the text of Roslyn's documentation comment trivia.
    /// </summary>
    [TestMethod]
    public void DocumentationComment_AgreesWithRoslyn_OnTheCorpus()
    {
        var compared = 0;
        var differences = new List<string>();
        foreach (var path in CorpusFiles())
        {
            var source = File.ReadAllText(path).TrimStart('﻿');
            if (!CSharpParser.TryParse(source, null, out var unit, out _))
            {
                continue;
            }

            var utf8 = CSharpParser.GetUtf8Source(source);
            var offsets = ByteOffsets(source);
            var ours = new Dictionary<int, string>();
            foreach (var node in Descendants(unit))
            {
                switch (node)
                {
                    case GlobalStatement or IncompleteMemberDeclaration:
                        break;
                    case MemberDeclaration member:
                        ours[member.Span.Start] = DocumentationComment.GetXml(utf8, member);
                        break;
                    case EnumMember member:
                        ours[member.Span.Start] = DocumentationComment.GetXml(utf8, member);
                        break;
                }
            }

            var root = CSharpSyntaxTree.ParseText(source).GetRoot();
            foreach (var member in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                if (member is GlobalStatementSyntax or IncompleteMemberSyntax)
                {
                    continue;
                }

                var trivia = member.GetLeadingTrivia();
                if (trivia.Any(t => t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia) || t.IsKind(SyntaxKind.DisabledTextTrivia)))
                {
                    continue;
                }

                var expected = RoslynDocumentation(trivia);
                if (!ours.TryGetValue(offsets[member.SpanStart], out var actual))
                {
                    continue;
                }

                compared++;
                if (expected != actual)
                {
                    differences.Add($"{path}:{member.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: expected [{expected}] but was [{actual}]");
                }
            }
        }

        TestContext.WriteLine($"{compared} members compared");
        Assert.IsGreaterThan(1000, compared);
        Assert.IsEmpty(differences, string.Join("\n", differences.Take(20)));
    }

    private static string RoslynDocumentation(SyntaxTriviaList trivia)
    {
        var lines = trivia
            .Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
            .SelectMany(t => t.ToFullString().ReplaceLineEndings("\n").Split('\n'))
            .Select(line => line.TrimStart())
            .Where(line => line.StartsWith("///", StringComparison.Ordinal))
            .Select(line => line[3..])
            .Select(line => (line.StartsWith(' ') ? line[1..] : line).TrimEnd())
            .ToList();

        return lines.Count == 0 ? null : string.Join("\n", lines);
    }

    private static (byte[] Utf8, IReadOnlyList<MemberDeclaration> Members) Members(string source)
    {
        Assert.IsTrue(CSharpParser.TryParse(source, null, out var unit, out var error), error?.Message);
        return (CSharpParser.GetUtf8Source(source), ((ClassDeclaration)unit.Members[0]).Members);
    }

    private static IEnumerable<ICSharpNode> Descendants(ICSharpNode node)
    {
        var stack = new Stack<ICSharpNode>();
        stack.Push(node);
        while (stack.Count != 0)
        {
            var current = stack.Pop();
            yield return current;
            foreach (var child in SpanChecker.Children(current))
            {
                stack.Push(child);
            }
        }
    }

    /// <summary>The UTF-8 offset of every UTF-16 position of <paramref name="source"/>, and of its end.</summary>
    private static int[] ByteOffsets(string source)
    {
        var offsets = new int[source.Length + 1];
        var offset = 0;
        for (var i = 0; i < source.Length; i++)
        {
            offsets[i] = offset;
            if (char.IsHighSurrogate(source[i]) && i + 1 < source.Length && char.IsLowSurrogate(source[i + 1]))
            {
                offsets[i + 1] = offset;
                offset += 4;
                i++;
            }
            else
            {
                offset += Encoding.UTF8.GetByteCount(source.AsSpan(i, 1));
            }
        }

        offsets[source.Length] = offset;
        return offsets;
    }

    private static IEnumerable<string> CorpusFiles()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PaspanParsers.slnx")))
        {
            directory = directory.Parent;
        }

        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(Path.Combine(directory!.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{separator}bin{separator}") && !path.Contains($"{separator}obj{separator}"))
            .Order(StringComparer.Ordinal);
    }
}
