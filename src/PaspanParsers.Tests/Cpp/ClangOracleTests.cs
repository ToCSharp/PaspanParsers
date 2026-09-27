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
        return run.Ast;
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
        var nodes = run.Ast.Nodes(source, bomLength: 3);

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
        var nodes = Clang.DumpAst(source, []).Ast.Nodes(source, 0);

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
        var ast = Clang.DumpAst(source, []).Ast;
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
        // A GNU statement expression
        var result = CppTestHelper.AssertOracleFails("int f() { return ({ 1; }); }", OracleStatus.ParseFailed);
        StringAssert.StartsWith(result.Detail, "(1,19): Unexpected '{'");
    }

    [TestMethod]
    public void Read_CollectsTheNamesOfTheHeaders()
    {
        Clang.RequireClang();
        var source = Encoding.UTF8.GetBytes("#include <vector>\n#include <concepts>\nstruct mine {};\nint size_t_user(std::size_t n);\n");
        var names = Clang.DumpAst(source, [], collectHeaderNames: true).Ast.HeaderNames;

        Assert.Contains("size_t", names.TypeNames);
        Assert.Contains("vector", names.TemplateNames);
        Assert.Contains("move", names.FunctionTemplateNames);
        Assert.Contains("integral", names.ConceptNames);

        // Only the headers: not the file, the members of classes or the locals of functions
        Assert.DoesNotContain("mine", names.TypeNames);
        Assert.DoesNotContain("vector", names.FunctionTemplateNames);
        Assert.DoesNotContain("value_type", names.FunctionTemplateNames);
    }

    [TestMethod]
    public void Read_LeavesOutTypesThatHeadersDeclareAsFunctionsToo()
    {
        Clang.RequireClang();
        var source = Encoding.UTF8.GetBytes("#include <sys/stat.h>\n#include <string>\nint a;\n");
        var names = Clang.DumpAst(source, [], collectHeaderNames: true).Ast.HeaderNames;

        // struct stat and int stat(const char *, struct stat *)
        Assert.DoesNotContain("stat", names.TypeNames);

        // A member function does not hide a class: std::string::size and std::size_t
        Assert.Contains("string", names.TypeNames);
    }

    [TestMethod]
    public void Preprocess_ExpandsTheMacrosOfTheFileAndKeepsItsLines()
    {
        Clang.RequireClang();
        var source = Encoding.UTF8.GetBytes("#include <cstddef>\n#define TWICE(x) ((x) * 2)\n#if defined(__cplusplus)\nint a = TWICE(NULL != 0);\n#endif\n");
        var preprocessed = ClangPreprocessor.Preprocess(source, ClangOracleOptions.Default);

        var lines = Encoding.UTF8.GetString(preprocessed.Expanded).Split('\n');
        Assert.AreEqual("#include <cstddef>", lines[0]);
        Assert.AreEqual("#define TWICE(x) ((x) * 2)", lines[1]);
        Assert.AreEqual("int a = ((__null != 0) * 2);", lines[3]);

        // The macros of the headers, for conditional directives
        Assert.AreEqual("__null", preprocessed.HeaderMacros["NULL"]);
        Assert.IsFalse(preprocessed.HeaderMacros.ContainsKey("TWICE(x)"));
    }

    [TestMethod]
    public void Normalize_IgnoresTheValuesThePreprocessorMadeFromTheLayout()
    {
        var original = DumpAst("int line = __LINE__; const char *text = __FILE__;\n#define S(x) #x\nconst char *s = S(1+2);\n");
        var reformatted = DumpAst("\n\nint line = __LINE__;\nconst char *text = __FILE__;\n#define S(x) #x\nconst char *s = S(1 + 2);\n");

        Assert.IsNull(ClangAst.FirstDifference(original.Normalize(), reformatted.Normalize()));
    }

    [TestMethod]
    public void Nodes_EndAtTheFirstAngleOfASplitShift()
    {
        Clang.RequireClang();
        var source = Encoding.UTF8.GetBytes("template <class T> struct A {};\ntemplate <class T, class U = A<T>> struct B {};\n");
        var nodes = Clang.DumpAst(source, []).Ast.Nodes(source, 0);

        var parameter = nodes.Single(n => n.Kind == "TemplateTypeParmDecl" && n.NameOffset == 57);
        Assert.AreEqual("class U = A<T>", Encoding.UTF8.GetString(source[parameter.Span.Start..parameter.Span.End]));
        Assert.IsFalse(parameter.FromMacro);
    }

    [TestMethod]
    public void Oracle_ChecksTheModeWithNames()
    {
        Clang.RequireClang();
        var source = Encoding.UTF8.GetBytes("#include <utility>\nint f(std::pair<int, int> p) { return std::get<0>(p); }\nauto g() { return std::pair<int, int>(1, 2); }\n");

        var result = ClangOracle.Check(source, ClangOracleOptions.Default, out var names);
        Assert.AreEqual(OracleStatus.SpanMismatch, result.Status, result.Detail);

        var withNames = ClangOracleOptions.Default with { Headers = new HeaderKnowledge(names, new Dictionary<string, string>()) };
        result = ClangOracle.Check(source, withNames);
        Assert.AreEqual(OracleStatus.Passed, result.Status, result.Detail);
    }

    [TestMethod]
    public void Oracle_ConditionalDirectivesUseClangsPredefinedMacros()
    {
        var macros = new ClangOracleOptions(["ANSWER=42"]).ParseOptions().Macros;
        Assert.AreEqual("202302L", macros["__cplusplus"]);
        Assert.AreEqual("42", macros["ANSWER"]);
    }
}
