using PaspanParsers.CSharp;

namespace PaspanParsers.Tests.CSharp;

[TestClass]
public class CSharpParserEntryPointTests
{
    [TestMethod]
    public void Parse_SkipsByteOrderMark()
    {
        var result = CSharpParser.Parse("﻿class A { }");

        Assert.IsNotNull(result);
        Assert.HasCount(1, result.Members);
    }

    [TestMethod]
    public void Parse_AllowsLeadingAndTrailingTrivia()
    {
        var code = "\n  // leading comment\n  class A { }\n  /* trailing */ // comment\n\n";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.HasCount(1, result.Members);
    }

    [TestMethod]
    public void Parse_EmptyInput_ReturnsEmptyCompilationUnit()
    {
        var result = CSharpParser.Parse("");

        Assert.IsNotNull(result);
        Assert.IsNull(result.Members);
    }

    [TestMethod]
    public void Parse_RejectsTrailingGarbage()
    {
        Assert.IsNull(CSharpParser.Parse("class A { } class"));
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
}
