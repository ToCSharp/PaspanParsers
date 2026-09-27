using PaspanParsers.CSharp;
using static PaspanParsers.Tests.CSharp.SyntaxTestHelper;

namespace PaspanParsers.Tests.CSharp;

/// <summary>
/// Patterns (stage 4): precedence of the combinators and every pattern form.
/// </summary>
[TestClass]
public class PatternTests
{
    [TestMethod]
    public void Combinators_NotBindsTighterThanAndTighterThanOr()
    {
        var or = (LogicalPattern)Pattern("not A and B or C");

        Assert.AreEqual(LogicalPatternKind.Or, or.Kind);
        var and = (LogicalPattern)or.Left;
        Assert.AreEqual(LogicalPatternKind.And, and.Kind);
        Assert.AreEqual(LogicalPatternKind.Not, ((LogicalPattern)and.Left).Kind);
    }

    [TestMethod]
    public void Combinators_AreLeftAssociative()
    {
        var or = (LogicalPattern)Pattern("1 or 2 or 3");

        Assert.IsInstanceOfType<LogicalPattern>(or.Left);
        Assert.IsInstanceOfType<ConstantPattern>(or.Right);
    }

    [TestMethod]
    public void Relational_ValueIsShiftExpression()
    {
        var and = (LogicalPattern)Pattern("> 0 and <= 10 + 1");

        var right = (RelationalPattern)and.Right;
        Assert.AreEqual(RelationalOperator.LessThanOrEqual, right.Operator);
        Assert.IsInstanceOfType<BinaryExpression>(right.Expression);
    }

    [TestMethod]
    public void Type_DeclarationVarAndDiscard()
    {
        Assert.IsInstanceOfType<TypePattern>(Pattern("string"));
        Assert.AreEqual("s", ((DeclarationPattern)Pattern("string s")).Identifier);
        Assert.AreEqual("x", ((VarPattern)Pattern("var x")).Identifier);
        Assert.IsInstanceOfType<ParenthesizedVariableDesignation>(((VarPattern)Pattern("var (a, b)")).Designation);
        Assert.IsInstanceOfType<DiscardPattern>(Pattern("_"));
    }

    [TestMethod]
    public void Type_NameIsTypeAfterIsAndConstantInCase()
    {
        Assert.IsInstanceOfType<TypePattern>(Pattern("A.B"));

        var switchStatement = (SwitchStatement)Statement("switch (o) { case A.B: break; }");
        var label = (CaseSwitchLabel)switchStatement.Sections[0].Labels[0];
        Assert.IsInstanceOfType<ConstantPattern>(label.Pattern);
    }

    [TestMethod]
    public void Recursive_PositionalPropertyAndDesignation()
    {
        var pattern = (RecursivePattern)Pattern("Point(X: var x, _) { Y: > 0 } named");

        Assert.AreEqual("Point", ((NamedTypeReference)pattern.Type).Name.Parts[0]);
        Assert.AreEqual("X", pattern.PositionalPatterns[0].Name);
        Assert.IsInstanceOfType<DiscardPattern>(pattern.PositionalPatterns[1].Pattern);
        Assert.AreEqual("Y", pattern.PropertyPatterns[0].PropertyName);
        Assert.AreEqual("named", pattern.Designation);
    }

    [TestMethod]
    public void Recursive_EmptyPropertyPatternIsKept()
    {
        var pattern = (RecursivePattern)Pattern("{ }");

        Assert.IsNotNull(pattern.PropertyPatterns);
        Assert.HasCount(0, pattern.PropertyPatterns);
    }

    [TestMethod]
    public void Recursive_ExtendedPropertyPattern()
    {
        var pattern = (RecursivePattern)Pattern("{ InnerException.Message: \"inner\", }");

        Assert.AreEqual("InnerException.Message", pattern.PropertyPatterns[0].PropertyName);
        Assert.IsTrue(pattern.PropertyPatternsHaveTrailingComma);
    }

    [TestMethod]
    public void Parenthesized_AndPositional()
    {
        Assert.IsInstanceOfType<ParenthesizedPattern>(Pattern("(> 0 and < 5)"));
        Assert.HasCount(2, ((RecursivePattern)Pattern("(0, 0)")).PositionalPatterns);
    }

    [TestMethod]
    public void List_WithSlices()
    {
        var list = (ListPattern)Pattern("[1, .., var last] items");

        Assert.IsInstanceOfType<SlicePattern>(list.Patterns[1]);
        Assert.IsNull(((SlicePattern)list.Patterns[1]).Pattern);
        Assert.AreEqual("items", list.Designation);

        var slice = (SlicePattern)((ListPattern)Pattern("[.. var rest]")).Patterns[0];
        Assert.IsInstanceOfType<VarPattern>(slice.Pattern);
    }

    [TestMethod]
    public void Constant_WithCast()
    {
        var or = (LogicalPattern)Pattern("(byte)'a' or (byte)'b'");

        Assert.IsInstanceOfType<CastExpression>(((ConstantPattern)or.Left).Expression);
    }

    [TestMethod]
    public void SwitchArm_GuardEndsBeforeArrow()
    {
        var switchExpression = Expression<SwitchExpression>("n switch { var v when v > 0 => 1, A.B when F() => 2, _ => 3 }");

        Assert.HasCount(3, switchExpression.Arms);
        Assert.IsInstanceOfType<BinaryExpression>(switchExpression.Arms[0].Guard);
        Assert.IsInstanceOfType<InvocationExpression>(switchExpression.Arms[1].Guard);
    }

    [TestMethod]
    [DataRow("if (o is null) { } if (o is not null && o is string s) { } if (o is int i and > 0 and < 10) { }")]
    [DataRow("if (o is var anything) { } if (p is { X: 0, Y: > 0 }) { } if (p is (0, 0)) { } if (p is Point(var x, var y) { X: 1 } named) { }")]
    [DataRow("if (o is Point { X: 1 or 2 } or null) { } if (arr is [1, 2, .. var rest, 5]) { } if (arr is [_, _] or []) { } if (arr is [.., > 0]) { }")]
    [DataRow("if (o is Exception { InnerException.Message: \"inner\" }) { } if (n is (> 0 and < 5) or 10) { } if (o is int or long) { } if (o is not (string or int)) { }")]
    [DataRow("if (o is { } nonNull) { } if (o is { Length: 0, } empty) { } if (arr is [1, 2,]) { } if (o is Point()) { } if (o is Point { }) { }")]
    [DataRow("if (x is (byte)'-' or (byte)'+') { } if (x is (int)1) { } if (x is -1 or +2) { } if (x is not > 0) { }")]
    [DataRow("if (o is string?[] strings) { } if (o is List<int> list) { } if (o is int[] ints) { } if (o is global::System.String) { }")]
    [DataRow("if (o is A.B.C) { } if (o is A.B.C abc) { } if (o is var (a, b)) { } if (o is var _) { } if (o is (var a2, _) pair) { }")]
    [DataRow("var r = n switch { < 0 => \"negative\", 0 => \"zero\", > 0 and <= 10 when n % 2 == 0 => \"small even\", int v when v > 1000 => \"large\", _ => \"other\", };")]
    [DataRow("var d = o switch { Point(var x, _) => x, { } => 1, null => 0 };")]
    [DataRow("var e = (a, b) switch { (0, 0) => 0, (> 0, _) => 1, (_, var y) when y > 0 => 2, _ => 3 };")]
    [DataRow("switch (o) { case 0: case 1 when b > 0: break; case int n and > 100: goto default; case > 50: goto case 0; case string { Length: > 0 } s: break; case null: break; default: break; }")]
    [DataRow("switch (a, b) { case (0, 0): break; case var (x, y) when x > y: break; }")]
    [DataRow("var q = from x in xs where x is int i select i; var k = o is int ? 1 : 2; var m = o is int? ? 1 : 2;")]
    public void Oracle_Patterns(string statements) => AssertOracle(statements);
}
