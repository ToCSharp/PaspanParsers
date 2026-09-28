using System.Text;
using System.Text.Json.Nodes;
using PaspanParsers.Cpp;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// Lines and columns of offsets (<see cref="LineMap"/>), the positions of parse errors,
/// <see cref="DocumentationComment"/> and <see cref="CppWriter.WriteNode"/>.
/// </summary>
[TestClass]
public class CppSourceTextTests
{
    // ========================================
    // Lines and columns
    // ========================================

    [TestMethod]
    public void LineMap_CppLinesEndOnlyAtCarriageReturnsAndLineFeeds()
    {
        var utf8 = Encoding.UTF8.GetBytes("int a;\r\nint b;\rint c; // \u2028 \u0085\nint d;");
        var lines = new LineMap(utf8, unicodeLineBreaks: false);

        Assert.AreEqual(4, lines.LineCount);
        Assert.AreEqual((3, 1), lines.GetLineAndColumn(IndexOf(utf8, "int c")));
        Assert.AreEqual((4, 1), lines.GetLineAndColumn(IndexOf(utf8, "int d")));
    }

    [TestMethod]
    public void LineMap_AgreesWithClang_OnTheCorpus()
    {
        Clang.RequireClang();

        var sources = Directory.EnumerateFiles(CppCorpusTests.CorpusDirectory, "*.cpp").Select(File.ReadAllBytes)
            .Append(Encoding.UTF8.GetBytes("int a = 1;\r\nint b = 2;\rint c = 3; // \u2028 x\nconst char *d = \"\U0001F600\", *e = \"\u00e9\";\n"));

        var checkedLocations = 0;
        foreach (var source in sources)
        {
            var run = Clang.DumpAst(source, [], CppCorpusTests.CorpusDirectory);
            Assert.IsTrue(run.Succeeded, run.Errors);
            var bomLength = source.AsSpan().StartsWith("\ufeff"u8) ? 3 : 0;
            var utf8 = source[bomLength..];
            var lines = new LineMap(utf8, unicodeLineBreaks: false);

            foreach (var (offset, line, column) in Locations(run.Ast))
            {
                var position = offset - bomLength;

                // Clang's columns count bytes, ours UTF-16 code units
                Assert.AreEqual(line, lines.GetLineAndColumn(position).Line, $"line of offset {position}");
                Assert.AreEqual(position, lines.GetLineSpan(line).Start + column - 1, $"offset of {line}:{column}");
                Assert.AreEqual(position, lines.GetOffset(line, lines.GetLineAndColumn(position).Column));
                checkedLocations++;
            }
        }

        Assert.IsGreaterThan(1000, checkedLocations);
    }

    /// <summary>
    /// The locations in the main file of clang's dump, with their lines and columns. Clang writes the line of a
    /// location only when it differs from that of the location written before it.
    /// </summary>
    private static List<(int Offset, int Line, int Column)> Locations(ClangAst ast)
    {
        var locations = new List<(int, int, int)>();
        var line = 0;
        void Visit(JsonNode node)
        {
            switch (node)
            {
                case JsonObject obj:
                    if (obj["offset"] is JsonValue offset && obj["col"] is JsonValue column)
                    {
                        line = obj["line"]?.GetValue<int>() ?? line;
                        if (!obj.ContainsKey("includedFrom") && obj["file"]?.GetValue<string>() == "<stdin>")
                        {
                            locations.Add((offset.GetValue<int>(), line, column.GetValue<int>()));
                        }
                    }

                    foreach (var (_, value) in obj)
                    {
                        Visit(value);
                    }

                    break;
                case JsonArray array:
                    foreach (var item in array)
                    {
                        Visit(item);
                    }

                    break;
            }
        }

        foreach (var declaration in ast.Declarations)
        {
            Visit(declaration);
        }

        return locations;
    }

    [TestMethod]
    public void ParseError_HasTheLineAndUtf16ColumnOfLineMap()
    {
        Assert.IsFalse(CppParser.TryParse("int a;\rint b c;", out _, out var error));
        Assert.AreEqual((2, 7), (error.Line, error.Column));

        var source = "const char *s = \"\U0001F600\"; int b c;";
        Assert.IsFalse(CppParser.TryParse(source, out _, out error));
        Assert.AreEqual((1, source.IndexOf("c;", StringComparison.Ordinal) + 1), (error.Line, error.Column));
        Assert.AreEqual(Encoding.UTF8.GetByteCount(source[..source.IndexOf("c;", StringComparison.Ordinal)]), error.Position);
    }

    // ========================================
    // Documentation comments
    // ========================================

    [TestMethod]
    public void LeadingTrivia_IsTheTriviaBeforeADeclarationOrEnumerator()
    {
        var (unit, utf8) = Parse("int a; // after a\n/// The b.\nint b;\nenum E { X, /* y */ Y };");

        Assert.AreEqual(new TextSpan(0, 0), unit.Declarations[0].LeadingTrivia);
        Assert.AreEqual(" // after a\n/// The b.\n", unit.Declarations[1].LeadingTrivia.GetText(utf8));
        var @enum = (EnumSpecifier)((SimpleDeclaration)unit.Declarations[2]).Specifiers.Specifiers[0];
        Assert.AreEqual(" /* y */ ", @enum.Enumerators[1].LeadingTrivia.GetText(utf8));
    }

    [TestMethod]
    public void DocumentationComment_LinesOfAGroup()
    {
        var (unit, utf8) = Parse("// Ordinary.\n/// Adds.\n///\n/// \\param a The first.\nint add(int a, int b);\n");

        var comment = DocumentationComment.Find(utf8, unit.Declarations[0]);
        Assert.AreEqual("/// Adds.\n///\n/// \\param a The first.", comment?.GetText(utf8));
        Assert.AreEqual("Adds.\n\n\\param a The first.", DocumentationComment.GetText(utf8, unit.Declarations[0]));
    }

    [TestMethod]
    public void DocumentationComment_Blocks()
    {
        var (unit, utf8) = Parse("/**\n * Adds.\n *   Indented.\n */\nint add(int a);\n/*! Qt. */ int qt;\n/*** Stars. */ int stars;\n");

        Assert.AreEqual("Adds.\n  Indented.", DocumentationComment.GetText(utf8, unit.Declarations[0]));
        Assert.AreEqual("Qt.", DocumentationComment.GetText(utf8, unit.Declarations[1]));
        Assert.AreEqual("* Stars.", DocumentationComment.GetText(utf8, unit.Declarations[2]));
    }

    [TestMethod]
    public void DocumentationComment_OnlyTheLastGroupBeforeTheDeclaration()
    {
        var (unit, utf8) = Parse("/// Not this.\n\n/// This.\n//! And this.\nint a;\n");

        Assert.AreEqual("This.\nAnd this.", DocumentationComment.GetText(utf8, unit.Declarations[0]));
    }

    [TestMethod]
    public void DocumentationComment_NotAcrossDirectivesOrSemicolons()
    {
        var (unit, utf8) = Parse("/// Directive.\n#define X 1\nint a;\n/// Ordinary comment.\n// a; b\nint b;\n/// Kept.\n// ordinary\nint c;\n");

        Assert.IsNull(DocumentationComment.GetText(utf8, unit.Declarations[0]));
        Assert.IsNull(DocumentationComment.GetText(utf8, unit.Declarations[1]));
        Assert.AreEqual("Kept.", DocumentationComment.GetText(utf8, unit.Declarations[2]));
    }

    [TestMethod]
    public void DocumentationComment_AfterAnInactiveBranchWithQuotes()
    {
        var (unit, utf8) = Parse("#if 0\nit's \"// not a comment\n#endif\n/// After.\nint a;\n");

        Assert.AreEqual("After.", DocumentationComment.GetText(utf8, unit.Declarations[0]));
    }

    [TestMethod]
    public void DocumentationComment_TrailingComments()
    {
        var (unit, utf8) = Parse("/// Before.\nint x; ///< The x.\nint y; /// Not trailing: documents z.\nint z;\nvoid f(); ///< Not of functions.\nint w;\n");

        Assert.AreEqual("The x.", DocumentationComment.GetText(utf8, unit.Declarations[0]));
        Assert.IsNull(DocumentationComment.GetText(utf8, unit.Declarations[1]));
        Assert.AreEqual("Not trailing: documents z.", DocumentationComment.GetText(utf8, unit.Declarations[2]));
        Assert.IsNull(DocumentationComment.GetText(utf8, unit.Declarations[3]));
        Assert.IsNull(DocumentationComment.GetText(utf8, unit.Declarations[4]));
    }

    [TestMethod]
    public void DocumentationComment_OfDeclarators()
    {
        var (unit, utf8) = Parse("/// Both.\nint a, b;\n/// First.\nint c{1}, d;\nint e, ///< The e.\n    f; ///< The f.\n");

        string Text(int declaration, int declarator)
        {
            var simple = (SimpleDeclaration)unit.Declarations[declaration];
            return DocumentationComment.Find(utf8, simple, simple.Declarators[declarator]) is { } span ? DocumentationComment.GetText(utf8, span) : null;
        }

        Assert.AreEqual("Both.", Text(0, 0));
        Assert.AreEqual("Both.", Text(0, 1));
        Assert.AreEqual("First.", Text(1, 0));
        Assert.IsNull(Text(1, 1));
        Assert.AreEqual("The e.", Text(2, 0));
        Assert.AreEqual("The f.", Text(2, 1));
    }

    [TestMethod]
    public void DocumentationComment_OfEnumeratorsAndMembers()
    {
        var (unit, utf8) = Parse("enum E { A, ///< The A.\n  /// The B.\n  B,\n  C /**< The C. */ };\nstruct S {\n  /// The x.\npublic:\n  int x;\n};\n");

        var @enum = (EnumSpecifier)((SimpleDeclaration)unit.Declarations[0]).Specifiers.Specifiers[0];
        CollectionAssert.AreEqual(new[] { "The A.", "The B.", "The C." }, @enum.Enumerators.Select(e => DocumentationComment.GetText(utf8, e)).ToList());

        var members = ((ClassSpecifier)((SimpleDeclaration)unit.Declarations[1]).Specifiers.Specifiers[0]).Members;
        Assert.AreEqual("The x.", DocumentationComment.GetText(utf8, members[0]));
        Assert.AreEqual("The x.", DocumentationComment.GetText(utf8, members[1]));
    }

    [TestMethod]
    public void DocumentationComment_OfNodesBuiltInCode_IsNull()
    {
        Assert.IsNull(DocumentationComment.Find("/// A.\n"u8, new EmptyDeclaration()));
    }

    [TestMethod]
    public void DocumentationComment_AgreesWithClang()
    {
        CppTestHelper.AssertOracle("/// A.\nint a; ///< Trailing.\nenum E { X, ///< X.\n  Y };\n/** B.\n */\ntemplate <class T> struct B { /// M.\n  int m; };\n");
    }

    [TestMethod]
    public void DocumentationChecker_FindsDifferencesWithClang()
    {
        var (unit, utf8) = Parse("/// A.\nint a;\nint b;\n");
        var a = IndexOf(utf8, "a;");
        var b = IndexOf(utf8, "b;");

        // Clang's range of the text of a comment starts after its marker
        var text = new TextSpan(3, 6);
        Assert.IsNull(CppDocumentationChecker.Check(utf8, unit, [new ClangComment("VarDecl", a, text)]));
        StringAssert.Contains(CppDocumentationChecker.Check(utf8, unit, []), "which clang does not attach");
        StringAssert.Contains(
            CppDocumentationChecker.Check(utf8, unit, [new ClangComment("VarDecl", a, text), new ClangComment("VarDecl", b, text)]), "which we do not find");
    }

    private static (TranslationUnit Unit, byte[] Utf8) Parse(string source) => (CppTestHelper.Parse(source), Encoding.UTF8.GetBytes(source));

    private static int IndexOf(byte[] utf8, string text) => utf8.AsSpan().IndexOf(Encoding.UTF8.GetBytes(text));

    // ========================================
    // Writing single nodes
    // ========================================

    [TestMethod]
    public void WriteNode_WritesAnyNodeAsInItsTree()
    {
        var unit = CppTestHelper.Parse(
            "struct D : public virtual B<int> { D() : x(1) {} int x; };\nenum E { A = 1 };\nvoid f(int count...) { int (*p)[3] = nullptr; }\nint renamed __asm__(\"symbol\");\n");

        string Write(CppNode node)
        {
            var writer = new CppWriter();
            writer.WriteNode(node);
            return writer.GetResult().Trim();
        }

        var @class = (ClassSpecifier)((SimpleDeclaration)unit.Declarations[0]).Specifiers.Specifiers[0];
        Assert.AreEqual("public virtual B<int>", Write(@class.Bases[0]));
        Assert.AreEqual("x(1)", Write(((FunctionDefinition)@class.Members[0]).Initializers[0]));

        var @enum = (EnumSpecifier)((SimpleDeclaration)unit.Declarations[1]).Specifiers.Specifiers[0];
        Assert.AreEqual("A = 1", Write(@enum.Enumerators[0]));

        var function = (FunctionDefinition)unit.Declarations[2];
        Assert.AreEqual("f(int count...)", Write(function.Declarator));
        var local = (SimpleDeclaration)((DeclarationStatement)function.Body.Statements[0]).Declaration;
        Assert.AreEqual("(*p)[3] = nullptr", Write(local.Declarators[0]));
        Assert.AreEqual("= nullptr", Write(local.Declarators[0].Initializer));

        Assert.AreEqual("int renamed __asm__(\"symbol\");", Write(unit.Declarations[3]));
    }

    [TestMethod]
    public void SizeOfPack_NameSpansTheName()
    {
        var source = "template <class... Ts> int n = sizeof...( Ts );";
        var unit = CppTestHelper.Parse(source);
        var size = (SizeOfPackExpression)((EqualsInitializer)((SimpleDeclaration)((TemplateDeclaration)unit.Declarations[0]).Declaration).Declarators[0].Initializer).Value;
        var name = source.LastIndexOf("Ts", StringComparison.Ordinal);
        Assert.AreEqual(new TextSpan(name, name + 2), size.Pack.Span);
    }
}
