using PaspanParsers.CSharp;
using static PaspanParsers.Tests.CSharp.SyntaxTestHelper;

namespace PaspanParsers.Tests.CSharp;

/// <summary>
/// Node positions (stage 8). <see cref="SpanChecker"/>, run by the oracle on every corpus file, compares
/// all spans with Roslyn; these tests pin down the conventions.
/// </summary>
[TestClass]
public class SpanTests
{
    private static string Text(string source, ICSharpNode node) => node.Span.GetText(source);

    [TestMethod]
    public void Span_CoversTheTokensOfTheNodeWithoutTrivia()
    {
        const string source = "class C\n{\n    int M() => /* sum */ a + b * c ;\n}\n";
        var unit = ParserVariants.Parse(source);
        var method = (MethodDeclaration)((ClassDeclaration)unit.Members[0]).Members[0];
        var body = (ExpressionMethodBody)method.Body;
        var add = (BinaryExpression)body.Expression;

        Assert.AreEqual("int M() => /* sum */ a + b * c ;", Text(source, method));
        Assert.AreEqual("a + b * c", Text(source, add));
        Assert.AreEqual("b * c", Text(source, add.Right));
        Assert.AreEqual("a", Text(source, add.Left));
        Assert.AreEqual("int", Text(source, method.ReturnType));
    }

    [TestMethod]
    public void Span_OfAnExpressionBodyEndsBeforeTheSemicolon()
    {
        const string source = "class C { int P => 1; }";
        var property = (PropertyDeclaration)((ClassDeclaration)ParserVariants.Parse(source).Members[0]).Members[0];

        Assert.AreEqual("int P => 1;", Text(source, property));
        Assert.AreEqual("1", Text(source, property.ExpressionBody));
    }

    [TestMethod]
    public void Span_OfADeclarationStartsAtItsAttributes()
    {
        const string source = "namespace N;\n\n[Serializable]\npublic sealed class C : Base\n{\n}\n";
        var unit = ParserVariants.Parse(source);
        var ns = (NamespaceDeclaration)unit.Members[0];
        var type = (ClassDeclaration)ns.Members[0];

        Assert.AreEqual("[Serializable]\npublic sealed class C : Base\n{\n}", Text(source, type));
        Assert.AreEqual("[Serializable]", Text(source, type.Attributes[0]));
        Assert.AreEqual("Base", Text(source, type.BaseTypes[0]));
        Assert.AreEqual(source.TrimEnd(), Text(source, ns));
        Assert.AreEqual("N", Text(source, ns.Name));
    }

    [TestMethod]
    public void Span_OfTheCompilationUnitIsTheWholeInput()
    {
        const string source = "  // comment\nclass C { }\n\n";
        var unit = ParserVariants.Parse(source);

        Assert.AreEqual(new TextSpan(0, source.Length), unit.Span);
        Assert.AreEqual("class C { }", Text(source, unit.Members[0]));
    }

    [TestMethod]
    public void Span_OfPostfixChainsAndStatements()
    {
        var statements = Statements("var x = a.b(1)[2]!;\nif (x) return;");
        var declaration = (LocalDeclarationStatement)statements[0];
        var suppress = (UnaryExpression)declaration.Variables[0].Initializer;
        var element = (ElementAccessExpression)suppress.Operand;
        var invocation = (InvocationExpression)element.Target;
        var source = InMethod("var x = a.b(1)[2]!;\nif (x) return;");

        Assert.AreEqual("var x = a.b(1)[2]!;", Text(source, declaration));
        Assert.AreEqual("x = a.b(1)[2]!", Text(source, declaration.Variables[0]));
        Assert.AreEqual("a.b(1)[2]", Text(source, element));
        Assert.AreEqual("a.b(1)", Text(source, invocation));
        Assert.AreEqual("a.b", Text(source, invocation.Expression));
        Assert.AreEqual("1", Text(source, invocation.Arguments[0]));
        Assert.AreEqual("if (x) return;", Text(source, statements[1]));
    }

    [TestMethod]
    public void Span_IsInUtf8BytesOfTheInputWithoutTheByteOrderMark()
    {
        const string source = "\uFEFFclass Привет { string s = \"мир\"; string e = \"🤩\"; }";
        var unit = ParserVariants.Parse(source);
        var type = (ClassDeclaration)unit.Members[0];
        var field = (FieldDeclaration)type.Members[0];
        var literal = field.Variables[0].Initializer;

        // "class " is 6 bytes, "Привет" 12
        Assert.AreEqual(0, type.Span.Start);
        Assert.AreEqual("\"мир\"", Text(source, literal));
        Assert.AreEqual(8, literal.Span.Length);

        var utf8 = CSharpParser.GetUtf8Source(source);
        Assert.AreEqual(utf8.Length, type.Span.End);
        Assert.AreEqual("string s = \"мир\";", field.Span.GetText(utf8));

        // A character outside the BMP is 4 bytes
        var emoji = ((FieldDeclaration)type.Members[1]).Variables[0].Initializer;
        Assert.AreEqual(6, emoji.Span.Length);
        Assert.AreEqual("\"🤩\"", Text(source, emoji));
        AssertOracle("var e = \"🤩\"; var s = \"мир\";");
    }

    [TestMethod]
    public void Span_OfInterpolatedStringParts()
    {
        const string source = "class C { string s = $\"a{x,5:N}b\"; }";
        var field = (FieldDeclaration)((ClassDeclaration)ParserVariants.Parse(source).Members[0]).Members[0];
        var interpolated = (InterpolatedStringExpression)field.Variables[0].Initializer;

        Assert.AreEqual("$\"a{x,5:N}b\"", Text(source, interpolated));
        Assert.AreEqual("a", Text(source, interpolated.Contents[0]));
        Assert.AreEqual("{x,5:N}", Text(source, interpolated.Contents[1]));
        Assert.AreEqual("x", Text(source, ((Interpolation)interpolated.Contents[1]).Expression));
        Assert.AreEqual("b", Text(source, interpolated.Contents[2]));
    }

    [TestMethod]
    public void Span_OfANullableDirectiveIsItsLine()
    {
        const string source = "#nullable enable\nclass C { }";
        var type = ParserVariants.Parse(source).Members[0];

        Assert.AreEqual("#nullable enable", Text(source, type.NullableDirectives[0]));
        Assert.AreEqual("class C { }", Text(source, type));
    }

    [TestMethod]
    public void Span_OfNodesBuiltInCodeIsEmpty()
    {
        var name = new NameExpression(["x"]);

        Assert.AreEqual(default, name.Span);
        Assert.IsTrue(name.Span.IsEmpty);
    }

    [TestMethod]
    public void Ref_TakesAWholeExpression()
    {
        var declaration = (LocalDeclarationStatement)Statement("ref int r = ref c ? ref a : ref b;");
        var reference = (RefExpression)declaration.Variables[0].Initializer;
        var conditional = (ConditionalExpression)reference.Expression;

        Assert.IsInstanceOfType<RefExpression>(conditional.TrueExpression);
        Assert.IsInstanceOfType<RefExpression>(conditional.FalseExpression);
    }

    [TestMethod]
    [DataRow("ref int r = ref c ? ref a : ref b; ref var s = ref x is null ? ref y : ref z;")]
    [DataRow("var q = from a in xs join b in ys on a equals b into g let n = g.Count() where n > 0 orderby n descending select n into m select m;")]
    [DataRow("var t = (a: 1, b: (x, y)); var (p, (q, _)) = t; foreach (var (k, v) in d) { } int[][,] arr = new int[2][,]; var n = new byte[]?[4];")]
    [DataRow("var s = o switch { int i when i > 0 => 1, { Length: > 2 } or [_, .., _] => 2, _ => 3 }; var c = a?.b.c?[0]?.d();")]
    [DataRow("var x = typeof(Dictionary<,>); var y = global::System.Collections.Generic.List<int>.Enumerator.Current; A<B>.C<D>.E f = default;")]
    [DataRow("var r = $$\"\"\"\n    a {{x}} {b}\n    \"\"\"; var u = \"\\u0041\"u8; var l = static async (int x) => await F(x);")]
    public void Spans_MatchRoslyn(string statements) => AssertOracle(statements);
}
