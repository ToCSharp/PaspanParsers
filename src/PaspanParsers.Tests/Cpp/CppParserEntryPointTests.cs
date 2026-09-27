using System.Text;
using PaspanParsers.Cpp;

namespace PaspanParsers.Tests.Cpp;

[TestClass]
public class CppParserEntryPointTests
{
    [TestMethod]
    public void EmptyInput_IsAnEmptyTranslationUnit()
    {
        foreach (var source in new[] { "", "  \n\t", "// comment\n/* block\n comment */\n", "int a;\\\n" })
        {
            var unit = CppTestHelper.Parse(source);
            Assert.AreEqual(new TextSpan(0, Encoding.UTF8.GetByteCount(source)), unit.Span, source);
        }
    }

    [TestMethod]
    public void Main_ParsesToAFunctionDefinition()
    {
        var unit = CppTestHelper.Parse("int main() { return 0; }");

        var main = (FunctionDefinition)unit.Declarations.Single();
        Assert.AreEqual("int", ((KeywordSpecifier)main.Specifiers.Specifiers.Single()).Keyword);
        var declarator = (FunctionDeclarator)main.Declarator;
        Assert.AreEqual("main", ((NameDeclarator)declarator.Inner).Name);
        Assert.IsEmpty(declarator.Parameters);

        var @return = (ReturnStatement)main.Body.Statements.Single();
        Assert.AreEqual("0", ((LiteralExpression)@return.Expression).Text);
        Assert.AreEqual(new TextSpan(13, 22), @return.Span);
        Assert.AreEqual(new TextSpan(0, 24), main.Span);
    }

    [TestMethod]
    public void TryParse_ReportsWhereTheInputStopsParsing()
    {
        Assert.IsFalse(CppParser.TryParse("int main()\n{\n    return 0\n}\n", out var unit, out var error));
        Assert.IsNull(unit);
        Assert.AreEqual("Unexpected '}'", error.Message);
        Assert.AreEqual(4, error.Line);
        Assert.AreEqual(1, error.Column);
    }

    [TestMethod]
    public void Utf8Input_SkipsTheByteOrderMark()
    {
        var source = "﻿int a = 1;"u8.ToArray();
        Assert.IsTrue(CppParser.TryParse(source, null, out var unit, out _));
        Assert.AreEqual(new TextSpan(0, source.Length - 3), unit.Span);
        Assert.AreEqual(new TextSpan(0, 10), unit.Declarations[0].Span);
    }

    [TestMethod]
    public void DeeplyNestedInput_ParsesOnALargeStack()
    {
        var depth = 20_000;
        var source = $"int a = {new string('(', depth)}1{new string(')', depth)};";

        var unit = CppTestHelper.Parse(source);
        var writer = new CppWriter();
        writer.WriteTranslationUnit(unit);
        Assert.AreEqual(source, writer.GetResult().TrimEnd());
    }

    [TestMethod]
    public void Oracle_Basics()
    {
        CppTestHelper.AssertOracle("int square(int x) { return x * x; }\nint main() { int a = square(2), b = -a; return a + b; }\n");
    }
}
