using PaspanParsers.Cpp;
using PaspanParsers.Tests.CSharp;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// Parses snippets of C++ statements and expressions inside a function, and checks sources against clang.
/// </summary>
internal static class CppTestHelper
{
    public static string InFunction(string statements) => $"void f()\n{{\n{statements}\n}}\n";

    public static TranslationUnit Parse(string source)
    {
        var success = CppParser.TryParse(source, out var unit, out var error);
        Assert.IsTrue(success, $"failed to parse: {source}\n({error?.Line},{error?.Column}): {error?.Message}");
        return unit;
    }

    public static IReadOnlyList<Statement> Statements(string statements)
    {
        var function = (FunctionDefinition)Parse(InFunction(statements)).Declarations[0];
        return function.Body.Statements;
    }

    public static Statement Statement(string statement)
    {
        var statements = Statements(statement);
        Assert.HasCount(1, statements, statement);
        return statements[0];
    }

    /// <summary>
    /// The expression of the statement <c>expression;</c>.
    /// </summary>
    public static Expression Expression(string expression)
    {
        return ((ExpressionStatement)Statement(expression + ";")).Expression;
    }

    public static T Expression<T>(string expression) where T : Expression
    {
        var result = Expression(expression);
        Assert.IsInstanceOfType<T>(result, expression);
        return (T)result;
    }

    public static void AssertParseFails(string source)
    {
        Assert.IsNull(CppParser.Parse(source), "should not parse: " + source);
    }

    /// <summary>
    /// The source must be valid C++, parse, be written back to the same clang AST and have the spans
    /// and kinds of clang's nodes.
    /// </summary>
    public static void AssertOracle(string source)
    {
        Clang.RequireClang();
        var result = ClangOracle.Check(source);
        Assert.AreEqual(OracleStatus.Passed, result.Status, $"{source}\n{result.Detail}");
    }

    /// <summary>
    /// The oracle must reject the source with <paramref name="status"/>.
    /// </summary>
    public static OracleResult AssertOracleFails(string source, OracleStatus status)
    {
        Clang.RequireClang();
        var result = ClangOracle.Check(source);
        Assert.AreEqual(status, result.Status, $"{source}\n{result.Detail}");
        return result;
    }
}
