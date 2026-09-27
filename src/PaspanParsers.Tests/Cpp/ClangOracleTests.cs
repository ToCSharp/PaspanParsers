using System.Text;
using System.Text.Json.Nodes;
using PaspanParsers.Cpp;
using PaspanParsers.Tests.CSharp;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// Tests of the clang oracle itself: reading clang's AST, comparing trees and checking kinds.
/// </summary>
[TestClass]
public class ClangOracleTests
{
    private static ClangAst DumpAst(string source)
    {
        Clang.RequireClang();
        var run = Clang.DumpAst(Encoding.UTF8.GetBytes(source), []);
        Assert.IsTrue(run.Succeeded, run.Errors);
        return ClangAst.Read(run.Output);
    }

    [TestMethod]
    public void Read_KeepsOnlyTheDeclarationsOfTheMainFile()
    {
        var ast = DumpAst("#include <cstddef>\nint a;\nstd::size_t b;\n");

        var names = ast.Declarations.Select(d => d["name"]?.GetValue<string>()).ToList();
        CollectionAssert.AreEqual(new[] { "a", "b" }, names);
    }

    [TestMethod]
    public void Normalize_IgnoresFormattingCommentsAndPositions()
    {
        var original = DumpAst("/// The answer.\nint answer = 42;\nauto f = [](int x) { return x; };\n");
        var reformatted = DumpAst("\n\nint   answer=42;   auto f=[](int x){return x;};");

        Assert.IsNull(ClangAst.FirstDifference(original.Normalize(), reformatted.Normalize()));
    }

    [TestMethod]
    public void FirstDifference_NamesTheDifferingNode()
    {
        var expected = DumpAst("int f(int a, int b) { return a - b * 2; }");
        var actual = DumpAst("int f(int a, int b) { return (a - b) * 2; }");

        var difference = ClangAst.FirstDifference(expected.Normalize(), actual.Normalize());
        StringAssert.StartsWith(difference, "/FunctionDecl f/CompoundStmt/ReturnStmt/BinaryOperator.opcode");
    }

    [TestMethod]
    public void Nodes_HaveByteOffsetsWithoutTheByteOrderMark()
    {
        var source = "﻿int é = 1;"u8.ToArray();
        var run = Clang.DumpAst(source, []);
        var nodes = ClangAst.Read(run.Output).Nodes(source, bomLength: 3);

        var variable = nodes.Single(n => n.Kind == "VarDecl");
        // 'é' is two bytes
        Assert.AreEqual(new TextSpan(0, 10), variable.Span);
        Assert.AreEqual(4, variable.NameOffset);
        Assert.AreEqual(new TextSpan(9, 10), nodes.Single(n => n.Kind == "IntegerLiteral").Span);
    }

    [TestMethod]
    public void RawTokens_AreTheTokensOfTheSource()
    {
        Clang.RequireClang();
        var source = Encoding.UTF8.GetBytes("int a = R\"x(\n)\n)x\"[0]; // comment\n");

        var tokens = ClangAst.RawTokens(source, 0).Select(t => Encoding.UTF8.GetString(source[t.Start..t.End])).ToList();
        CollectionAssert.AreEqual(new[] { "int", "a", "=", "R\"x(\n)\n)x\"", "[", "0", "]", ";" }, tokens);
    }

    [TestMethod]
    public void RawTokens_StartAfterLineSplicesAndSpanThem()
    {
        Clang.RequireClang();
        var source = Encoding.UTF8.GetBytes("int ma\\\nin = 1 +\\\n2;\n");

        var tokens = ClangAst.RawTokens(source, 0).Select(t => Encoding.UTF8.GetString(source[t.Start..t.End])).ToList();
        CollectionAssert.AreEqual(new[] { "int", "ma\\\nin", "=", "1", "+", "2", ";" }, tokens);
    }

    [TestMethod]
    public void LiteralChecker_RejectsAWrongValue()
    {
        Clang.RequireClang();
        var source = Encoding.UTF8.GetBytes("auto a = 42; auto b = \"x\\n\"; auto c = 'c'; auto d = 1.5;\n");
        var unit = CppParser.Parse(Encoding.UTF8.GetString(source));
        var nodes = ClangAst.Read(Clang.DumpAst(source, []).Output).Nodes(source, 0);

        Assert.IsNull(CppLiteralChecker.Check(unit, nodes));

        foreach (var (kind, wrong) in new (string, JsonNode)[] { ("IntegerLiteral", "43"), ("StringLiteral", "\"x\""), ("CharacterLiteral", 100L), ("FloatingLiteral", "2.5") })
        {
            var changed = nodes.Select(n => n.Kind == kind ? n with { Value = wrong } : n).ToList();
            StringAssert.StartsWith(CppLiteralChecker.Check(unit, changed), "LiteralExpression", kind);
        }
    }

    [TestMethod]
    public void LiteralChecker_WritesStringsLikeClang()
    {
        Clang.RequireClang();
        var source = "auto a = \"tab\\there\\0end\\x7f\"; auto b = u8\"\\xE9é\"; auto c = u\"a\\U0001F600\"; auto d = L\"w\\u00e9\\x100\"; auto e = U\"\\x{41}\" \"z\";\n";
        CppTestHelper.AssertOracle(source);
    }

    [TestMethod]
    public void SpanChecker_RejectsANodeOfTheWrongKind()
    {
        Clang.RequireClang();
        var source = Encoding.UTF8.GetBytes("int a = 1;\nint main() { return a * 2; }\n");
        var unit = CppParser.Parse(Encoding.UTF8.GetString(source));
        var ast = ClangAst.Read(Clang.DumpAst(source, []).Output);
        var tokens = ClangAst.RawTokens(source, 0);

        Assert.IsNull(CppSpanChecker.Check(source, unit, ast.Nodes(source, 0), tokens));

        // As if clang had read 'a * 2' as a declaration
        var wrong = ast.Nodes(source, 0).Select(n => n.Kind == "BinaryOperator" ? n with { Kind = "VarDecl" } : n).ToList();
        var problem = CppSpanChecker.Check(source, unit, wrong, tokens);
        StringAssert.StartsWith(problem, "BinaryExpression");
        StringAssert.Contains(problem, "(found VarDecl)");
    }

    [TestMethod]
    public void Oracle_ReportsInvalidSource()
    {
        var result = CppTestHelper.AssertOracleFails("int main() { return 0 }", OracleStatus.Invalid);
        StringAssert.Contains(result.Detail, "expected ';'");
    }

    [TestMethod]
    public void Oracle_ReportsValidSourceTheParserRejects()
    {
        var result = CppTestHelper.AssertOracleFails("struct S { int a; };", OracleStatus.ParseFailed);
        StringAssert.StartsWith(result.Detail, "(1,1): Unexpected 'struct'");
    }

    [TestMethod]
    public void Oracle_ConditionalDirectivesUseClangsPredefinedMacros()
    {
        var macros = new ClangOracleOptions(["ANSWER=42"]).ParseOptions().Macros;
        Assert.AreEqual("202302L", macros["__cplusplus"]);
        Assert.AreEqual("42", macros["ANSWER"]);
    }
}
