using PaspanParsers.CSharp;

namespace PaspanParsers.Tests.CSharp;

[TestClass]
public class CSharpParserEntryPointTests
{
    [TestMethod]
    public void Parse_SkipsByteOrderMark()
    {
        var result = ParserVariants.Parse("﻿class A { }");

        Assert.IsNotNull(result);
        Assert.HasCount(1, result.Members);
    }

    [TestMethod]
    public void Parse_AllowsLeadingAndTrailingTrivia()
    {
        var code = "\n  // leading comment\n  class A { }\n  /* trailing */ // comment\n\n";

        var result = ParserVariants.Parse(code);

        Assert.IsNotNull(result);
        Assert.HasCount(1, result.Members);
    }

    [TestMethod]
    public void Parse_EmptyInput_ReturnsEmptyCompilationUnit()
    {
        var result = ParserVariants.Parse("");

        Assert.IsNotNull(result);
        Assert.IsNull(result.Members);
    }

    [TestMethod]
    [DataRow("class A\n{\n    void M() { x = ; }\n}", 3, 20, "Unexpected ';'")]
    [DataRow("class A { } }", 1, 13, "Unexpected '}'")]
    [DataRow("class A {", 1, 10, "Unexpected end of file")]
    [DataRow("// привет\nclass { }", 2, 7, "Unexpected '{'")]
    public void TryParse_ReportsWhereTheInputStopsParsing(string code, int line, int column, string message)
    {
        Assert.IsFalse(CSharpParser.TryParse(code, out var result, out var error));

        Assert.IsNull(result);
        Assert.AreEqual(message, error.Message);
        Assert.AreEqual((line, column), (error.Line, error.Column));
    }

    [TestMethod]
    public void Parse_RejectsTrailingGarbage()
    {
        Assert.IsNull(ParserVariants.Parse("class A { } class"));
    }

    [TestMethod]
    public void TryParse_WithOptions()
    {
        var options = new CSharpParseOptions(CSharpLanguageVersion.CSharp12, ["DEBUG"]);

        var success = CSharpParser.TryParse("class A { }", options, out var result, out var error);

        Assert.IsTrue(success);
        Assert.IsNull(error);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members[0]);
    }

    [TestMethod]
    public void ParseContext_TracksOptionsAndSymbols()
    {
        var context = new CSharpParseContext(new CSharpParseOptions(preprocessorSymbols: ["DEBUG", "TRACE"]));

        Assert.AreEqual(CSharpLanguageVersion.Latest, context.Options.LanguageVersion);
        Assert.Contains("DEBUG", context.DefinedSymbols);
        Assert.Contains("TRACE", context.DefinedSymbols);
    }

    [TestMethod]
    public void RoslynOracle_PassesSupportedCode()
    {
        var result = RoslynOracle.Check("namespace N { public class A { private int x; public int Get() => x; } }");

        Assert.AreEqual(OracleStatus.Passed, result.Status, result.Detail);
    }

    [TestMethod]
    public void RoslynOracle_SkipsInvalidCode()
    {
        Assert.AreEqual(OracleStatus.Invalid, RoslynOracle.Check("class { ").Status);
    }

    [TestMethod]
    [DataRow("1_000", "1000")]
    [DataRow("0xFF", "255")]
    [DataRow("\"a\\tb\"", "@\"a\tb\"")]
    public void RoslynEquivalence_ComparesTokenText(string written, string other)
    {
        // The oracle relies on Roslyn treating differently written literals as different
        // (identifiers, in contrast, compare by value: @x and x are the same name)
        static Microsoft.CodeAnalysis.SyntaxNode Parse(string value) =>
            Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText($"class C {{ object x = {value}; }}").GetRoot();

        Assert.IsTrue(Microsoft.CodeAnalysis.CSharp.SyntaxFactory.AreEquivalent(Parse(written), Parse(written), topLevel: false));
        Assert.IsFalse(Microsoft.CodeAnalysis.CSharp.SyntaxFactory.AreEquivalent(Parse(written), Parse(other), topLevel: false));
    }
}
