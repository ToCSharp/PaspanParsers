using PaspanParsers.CSharp;

namespace PaspanParsers.Tests.CSharp;

/// <summary>
/// Parses snippets of statements, expressions, types and patterns inside a method, and checks
/// them against Roslyn.
/// </summary>
internal static class SyntaxTestHelper
{
    public static string InMethod(string statements) => $"class C\n{{\n    void M()\n    {{\n{statements}\n    }}\n}}\n";

    public static IReadOnlyList<Statement> Statements(string statements)
    {
        var unit = ParserVariants.Parse(InMethod(statements));
        Assert.IsNotNull(unit, "failed to parse: " + statements);

        var method = (MethodDeclaration)((ClassDeclaration)unit.Members[0]).Members[0];
        return ((BlockMethodBody)method.Body).Block.Statements ?? [];
    }

    public static Statement Statement(string statement)
    {
        var statements = Statements(statement);
        Assert.HasCount(1, statements, statement);
        return statements[0];
    }

    /// <summary>
    /// The initializer of <c>var x = expression;</c>.
    /// </summary>
    public static Expression Expression(string expression)
    {
        var declaration = (LocalDeclarationStatement)Statement($"var x = {expression};");
        return declaration.Variables[0].Initializer;
    }

    public static T Expression<T>(string expression) where T : Expression
    {
        var result = Expression(expression);
        Assert.IsInstanceOfType<T>(result, expression);
        return (T)result;
    }

    /// <summary>
    /// The type of <c>Type x;</c>.
    /// </summary>
    public static TypeReference Type(string type)
    {
        var declaration = (LocalDeclarationStatement)Statement($"{type} x;");
        return declaration.Type;
    }

    /// <summary>
    /// The pattern of <c>o is pattern</c>.
    /// </summary>
    public static Pattern Pattern(string pattern) => Expression<IsExpression>($"o is {pattern}").Pattern;

    public static void AssertParseFails(string statements)
    {
        Assert.IsNull(ParserVariants.Parse(InMethod(statements)), "should not parse: " + statements);
    }

    /// <summary>
    /// The statements must be valid C#, parse, and be written back equivalent to Roslyn's tree.
    /// </summary>
    public static void AssertOracle(string statements)
    {
        var result = RoslynOracle.Check(InMethod(statements));
        Assert.AreEqual(OracleStatus.Passed, result.Status, $"{statements}\n{result.Detail}");
    }
}
