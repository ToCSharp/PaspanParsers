using PaspanParsers.Cpp;

namespace PaspanParsers.Tests.Cpp;

[TestClass]
public class CppLexicalTests
{
    [TestMethod]
    public void GreaterThanOperators_AreComposedFromAdjacentTokens()
    {
        Assert.AreEqual(">>", CppTestHelper.Expression<BinaryExpression>("a >> b").Operator);
        Assert.AreEqual(">=", CppTestHelper.Expression<BinaryExpression>("a >= b").Operator);
        Assert.AreEqual(">>=", CppTestHelper.Expression<BinaryExpression>("a >>= b").Operator);
        Assert.AreEqual(">", CppTestHelper.Expression<BinaryExpression>("a > b").Operator);
        CppTestHelper.AssertParseFails(CppTestHelper.InFunction("a > > b;"));
    }

    [TestMethod]
    public void AlternativeTokensAndDigraphs_AreTheirOperators()
    {
        var expression = CppTestHelper.Expression<BinaryExpression>("a and not b");
        Assert.AreEqual("&&", expression.Operator);
        Assert.AreEqual("!", ((UnaryExpression)expression.Right).Operator);

        var unit = CppTestHelper.Parse("int f() <% return 1 bitor 2; %>");
        Assert.AreEqual("|", ((BinaryExpression)((ReturnStatement)((FunctionDefinition)unit.Declarations[0]).Body.Statements[0]).Expression).Operator);
    }

    [TestMethod]
    public void Operators_AreScannedLongestFirst()
    {
        var shift = CppTestHelper.Expression<BinaryExpression>("a <<= b <=> c");
        Assert.AreEqual("<<=", shift.Operator);
        Assert.AreEqual("<=>", ((BinaryExpression)shift.Right).Operator);

        var decrement = CppTestHelper.Expression<BinaryExpression>("a---b");
        Assert.AreEqual("-", decrement.Operator);
        Assert.IsTrue(((UnaryExpression)decrement.Left).IsPostfix);
    }

    [TestMethod]
    public void Literals_KeepTheirSourceText()
    {
        foreach (var (text, kind) in new[]
        {
            ("0x1'FFu", LiteralKind.Integer),
            ("1'000'000ULL", LiteralKind.Integer),
            ("1.5e-3f", LiteralKind.Floating),
            (".5", LiteralKind.Floating),
            ("0x1.8p+1", LiteralKind.Floating),
            ("u8'a'", LiteralKind.Character),
            ("L'\\''", LiteralKind.Character),
            ("u\"a\\\"b\"", LiteralKind.String),
            ("R\"delim(a)\" b)delim\"", LiteralKind.String),
            ("LR\"(x)\"", LiteralKind.String),
        })
        {
            var literal = CppTestHelper.Expression<LiteralExpression>(text);
            Assert.AreEqual(kind, literal.Kind, text);
            Assert.AreEqual(text, literal.Text);
        }
    }

    [TestMethod]
    public void Trivia_IncludesCommentsAndLineSplices()
    {
        var unit = CppTestHelper.Parse("// a comment \\\n continued\nint /* c */ a\\\n = 1;");
        var declaration = (SimpleDeclaration)unit.Declarations.Single();
        Assert.AreEqual("1", ((LiteralExpression)((EqualsInitializer)declaration.Declarators[0].Initializer).Value).Text);
    }

    [TestMethod]
    public void Writer_SeparatesTokensThatWouldJoin()
    {
        foreach (var source in new[] { "a - -b", "- -a", "+ ++a", "a-- - b", "a & &b", "a < -b" })
        {
            var unit = CppTestHelper.Parse(CppTestHelper.InFunction(source + ";"));
            var writer = new CppWriter();
            writer.WriteTranslationUnit(unit);
            StringAssert.Contains(writer.GetResult(), source + ";", source);
        }
    }
}
