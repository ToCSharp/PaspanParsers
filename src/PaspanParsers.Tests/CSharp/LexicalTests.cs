using PaspanParsers.CSharp;

namespace PaspanParsers.Tests.CSharp;

/// <summary>
/// Lexical level of the C# parser: trivia, identifiers, keywords, operators and literals.
/// </summary>
[TestClass]
public class LexicalTests
{
    private static ClassDeclaration ParseClass(string code)
    {
        var unit = CSharpParser.Parse(code);
        Assert.IsNotNull(unit, "failed to parse: " + code);
        return (ClassDeclaration)unit.Members[0];
    }

    /// <summary>
    /// The initializer of 'object x = ...;' in a class.
    /// </summary>
    private static Expression Initializer(string expression)
    {
        var cls = ParseClass($"class C {{ object x = {expression}; }}");
        return ((FieldDeclaration)cls.Members[0]).Variables[0].Initializer;
    }

    private static LiteralExpression Literal(string expression)
    {
        var initializer = Initializer(expression);
        Assert.IsInstanceOfType<LiteralExpression>(initializer, expression);
        return (LiteralExpression)initializer;
    }

    private static void AssertLiteral(string text, object value, LiteralKind kind)
    {
        var literal = Literal(text);
        Assert.AreEqual(kind, literal.Kind, text);
        Assert.AreEqual(value, literal.Value, text);
        Assert.AreEqual(value.GetType(), literal.Value.GetType(), text);
        Assert.AreEqual(text, literal.Text);
    }

    // ========================================
    // Trivia
    // ========================================

    [TestMethod]
    public void Trivia_CommentsAndUnicodeWhiteSpace()
    {
        var code = "/* block */ class\u00A0C // line\u2028{ /** doc */ int\u3000x; \u2029}";

        var cls = ParseClass(code);

        Assert.AreEqual("C", cls.Name);
        Assert.HasCount(1, cls.Members);
    }

    [TestMethod]
    public void Trivia_UnterminatedCommentIsRejected()
    {
        Assert.IsNull(CSharpParser.Parse("class C { } /* unterminated"));
    }

    // ========================================
    // Identifiers and keywords
    // ========================================

    [TestMethod]
    [DataRow("_", "_")]
    [DataRow("_name1", "_name1")]
    [DataRow("имя", "имя")]
    [DataRow("na\u200Bme", "name")]
    [DataRow("@class", "class")]
    [DataRow("@name", "name")]
    [DataRow("\\u0061bc", "abc")]
    [DataRow("a\\u0062c", "abc")]
    [DataRow("\\U00000061", "a")]
    [DataRow("int1", "int1")]
    [DataRow("int_x", "int_x")]
    [DataRow("classic", "classic")]
    public void Identifier_Value(string written, string value)
    {
        var cls = ParseClass($"class C {{ int {written}; }}");

        Assert.AreEqual(value, ((FieldDeclaration)cls.Members[0]).Variables[0].Name);
    }

    [TestMethod]
    [DataRow("var")]
    [DataRow("get")]
    [DataRow("set")]
    [DataRow("value")]
    [DataRow("where")]
    [DataRow("select")]
    [DataRow("from")]
    [DataRow("async")]
    [DataRow("await")]
    [DataRow("record")]
    [DataRow("dynamic")]
    [DataRow("nameof")]
    [DataRow("field")]
    public void Identifier_ContextualKeywordIsIdentifier(string name)
    {
        var cls = ParseClass($"class C {{ int {name}; }}");

        Assert.AreEqual(name, ((FieldDeclaration)cls.Members[0]).Variables[0].Name);
    }

    [TestMethod]
    [DataRow("class")]
    [DataRow("int")]
    [DataRow("return")]
    [DataRow("stackalloc")]
    [DataRow("__arglist")]
    public void Identifier_ReservedKeywordIsNotIdentifier(string name)
    {
        Assert.IsNull(CSharpParser.Parse($"class C {{ int {name}; }}"));
    }

    [TestMethod]
    public void Identifier_DollarIsNotAnIdentifierCharacter()
    {
        Assert.IsNull(CSharpParser.Parse("class C { int $x; }"));
    }

    [TestMethod]
    public void Keyword_DoesNotMatchPrefixOfIdentifier()
    {
        // 'int1' is an identifier naming a type, not the keyword 'int' followed by '1'
        var cls = ParseClass("class C { int1 x; }");

        var type = (NamedTypeReference)((FieldDeclaration)cls.Members[0]).Type;
        Assert.AreEqual("int1", type.Name.Parts[0]);
    }

    // ========================================
    // Operators
    // ========================================

    [TestMethod]
    [DataRow("a << b", BinaryOperator.LeftShift)]
    [DataRow("a >> b", BinaryOperator.RightShift)]
    [DataRow("a >>> b", BinaryOperator.UnsignedRightShift)]
    [DataRow("a <= b", BinaryOperator.LessThanOrEqual)]
    [DataRow("a >= b", BinaryOperator.GreaterThanOrEqual)]
    [DataRow("a < b", BinaryOperator.LessThan)]
    [DataRow("a > b", BinaryOperator.GreaterThan)]
    [DataRow("a && b", BinaryOperator.And)]
    [DataRow("a & b", BinaryOperator.BitwiseAnd)]
    [DataRow("a || b", BinaryOperator.Or)]
    [DataRow("a | b", BinaryOperator.BitwiseOr)]
    [DataRow("a == b", BinaryOperator.Equal)]
    [DataRow("a != b", BinaryOperator.NotEqual)]
    public void Operator_MaximalMunch(string expression, BinaryOperator op)
    {
        var binary = (BinaryExpression)Initializer(expression);

        Assert.AreEqual(op, binary.Operator);
        Assert.IsInstanceOfType<NameExpression>(binary.Right);
    }

    [TestMethod]
    public void Operator_ShiftIsNotSplitByTrivia()
    {
        // '> >' is two tokens, not a shift
        Assert.IsNull(CSharpParser.Parse("class C { object x = a > > b; }"));
    }

    [TestMethod]
    public void Operator_NestedGenericClosingAngles()
    {
        var cls = ParseClass("class C { List<List<int>> x; }");

        var type = (NamedTypeReference)((FieldDeclaration)cls.Members[0]).Type;
        var inner = (NamedTypeReference)type.TypeArguments[0];
        Assert.IsInstanceOfType<PredefinedTypeReference>(inner.TypeArguments[0]);
    }

    // ========================================
    // Numeric literals
    // ========================================

    [TestMethod]
    public void Integer_TypeFollowsValueAndSuffix()
    {
        AssertLiteral("0", 0, LiteralKind.Integer);
        AssertLiteral("42", 42, LiteralKind.Integer);
        AssertLiteral("2147483647", int.MaxValue, LiteralKind.Integer);
        AssertLiteral("2147483648", 2147483648u, LiteralKind.Integer);
        AssertLiteral("4294967296", 4294967296L, LiteralKind.Integer);
        AssertLiteral("18446744073709551615", ulong.MaxValue, LiteralKind.Integer);
        AssertLiteral("42u", 42u, LiteralKind.Integer);
        AssertLiteral("42U", 42u, LiteralKind.Integer);
        AssertLiteral("42L", 42L, LiteralKind.Integer);
        AssertLiteral("42l", 42L, LiteralKind.Integer);
        AssertLiteral("42UL", 42UL, LiteralKind.Integer);
        AssertLiteral("42lu", 42UL, LiteralKind.Integer);
        AssertLiteral("4294967296u", 4294967296UL, LiteralKind.Integer);
    }

    [TestMethod]
    public void Integer_HexBinaryAndSeparators()
    {
        AssertLiteral("0xFF", 255, LiteralKind.Integer);
        AssertLiteral("0Xff", 255, LiteralKind.Integer);
        AssertLiteral("0xFFFF_FFFF", 0xFFFF_FFFFu, LiteralKind.Integer);
        AssertLiteral("0x_1", 1, LiteralKind.Integer);
        AssertLiteral("0b1010", 10, LiteralKind.Integer);
        AssertLiteral("0B_1111_0000", 240, LiteralKind.Integer);
        AssertLiteral("1_000_000", 1_000_000, LiteralKind.Integer);
        AssertLiteral("1__0", 10, LiteralKind.Integer);
        AssertLiteral("0x8000_0000_0000_0000", 0x8000_0000_0000_0000UL, LiteralKind.Integer);
    }

    [TestMethod]
    public void Real_TypeFollowsSuffix()
    {
        AssertLiteral("1.5", 1.5, LiteralKind.Real);
        AssertLiteral(".5", 0.5, LiteralKind.Real);
        AssertLiteral("1e3", 1000.0, LiteralKind.Real);
        AssertLiteral("1.5E-3", 0.0015, LiteralKind.Real);
        AssertLiteral("1e+2", 100.0, LiteralKind.Real);
        AssertLiteral("1.5f", 1.5f, LiteralKind.Real);
        AssertLiteral("2F", 2f, LiteralKind.Real);
        AssertLiteral("1d", 1.0, LiteralKind.Real);
        AssertLiteral("19.99m", 19.99m, LiteralKind.Real);
        AssertLiteral("1_000.000_1", 1000.0001, LiteralKind.Real);
    }

    [TestMethod]
    public void Numeric_NoSignNoTrailingDot()
    {
        // A sign is a unary operator, not part of the literal
        var negative = (UnaryExpression)Initializer("-1");
        Assert.AreEqual(UnaryOperator.Minus, negative.Operator);
        Assert.AreEqual(1, ((LiteralExpression)negative.Operand).Value);

        // '1.' is not a real literal
        Assert.IsNull(CSharpParser.Parse("class C { object x = 1.; }"));
    }

    [TestMethod]
    public void Numeric_ArgumentsAreNotGrouped()
    {
        var invocation = (InvocationExpression)Initializer("f(1,2)");

        Assert.HasCount(2, invocation.Arguments);
    }

    // ========================================
    // Character literals
    // ========================================

    [TestMethod]
    public void Character_Escapes()
    {
        AssertLiteral("'a'", 'a', LiteralKind.Character);
        AssertLiteral("'я'", 'я', LiteralKind.Character);
        AssertLiteral("'\\''", '\'', LiteralKind.Character);
        AssertLiteral("'\"'", '"', LiteralKind.Character);
        AssertLiteral("'\\\\'", '\\', LiteralKind.Character);
        AssertLiteral("'\\0'", '\0', LiteralKind.Character);
        AssertLiteral("'\\a'", '\a', LiteralKind.Character);
        AssertLiteral("'\\e'", '\u001B', LiteralKind.Character);
        AssertLiteral("'\\n'", '\n', LiteralKind.Character);
        AssertLiteral("'\\x41'", 'A', LiteralKind.Character);
        AssertLiteral("'\\x0041'", 'A', LiteralKind.Character);
        AssertLiteral("'\\u0041'", 'A', LiteralKind.Character);
        AssertLiteral("'\\U00000041'", 'A', LiteralKind.Character);
    }

    // ========================================
    // String literals
    // ========================================

    [TestMethod]
    public void String_Regular()
    {
        AssertLiteral("\"\"", "", LiteralKind.String);
        AssertLiteral("\"a\\tb\\\"c\\\\\"", "a\tb\"c\\", LiteralKind.String);
        AssertLiteral("\"\\a\\e\\x41\\u0042\\U0001F600\"", "\a\u001BAB\U0001F600", LiteralKind.String);
        AssertLiteral("\"привет\"", "привет", LiteralKind.String);
    }

    [TestMethod]
    public void String_RegularCannotSpanLines()
    {
        Assert.IsNull(CSharpParser.Parse("class C { object x = \"a\nb\"; }"));
    }

    [TestMethod]
    public void String_Verbatim()
    {
        AssertLiteral("@\"C:\\path\"", "C:\\path", LiteralKind.String);
        AssertLiteral("@\"say \"\"hi\"\"\"", "say \"hi\"", LiteralKind.String);
        AssertLiteral("@\"line1\nline2\"", "line1\nline2", LiteralKind.String);
    }

    [TestMethod]
    public void String_RawSingleLine()
    {
        AssertLiteral("\"\"\"He said \"hi\".\"\"\"", "He said \"hi\".", LiteralKind.String);
        AssertLiteral("\"\"\"\"a \"\"\" b\"\"\"\"", "a \"\"\" b", LiteralKind.String);
    }

    [TestMethod]
    public void String_RawMultiLineRemovesIndentation()
    {
        var text = "\"\"\"\n        {\n          \"a\": 1\n\n        }\n        \"\"\"";

        AssertLiteral(text, "{\n  \"a\": 1\n\n}", LiteralKind.String);
    }

    [TestMethod]
    public void String_Utf8()
    {
        var literal = Literal("\"abc\"u8");

        Assert.AreEqual(LiteralKind.Utf8String, literal.Kind);
        CollectionAssert.AreEqual("abc"u8.ToArray(), (byte[])literal.Value);
        Assert.AreEqual("\"abc\"u8", literal.Text);
        Assert.AreEqual(LiteralKind.Utf8String, Literal("@\"a\"U8").Kind);
        Assert.AreEqual(LiteralKind.Utf8String, Literal("\"\"\"a\"\"\"u8").Kind);
    }

    // ========================================
    // Interpolated strings
    // ========================================

    private static InterpolatedStringExpression Interpolated(string text)
    {
        var initializer = Initializer(text);
        Assert.IsInstanceOfType<InterpolatedStringExpression>(initializer, text);
        return (InterpolatedStringExpression)initializer;
    }

    private static string TextValue(InterpolatedStringContent content) => ((InterpolatedStringText)content).Value;

    [TestMethod]
    public void Interpolated_TextAndHoles()
    {
        var s = Interpolated("$\"a = {a}, b = {b,5:F2}!\"");

        Assert.AreEqual("$\"", s.StartToken);
        Assert.AreEqual("\"", s.EndToken);
        Assert.HasCount(5, s.Contents);
        Assert.AreEqual("a = ", TextValue(s.Contents[0]));
        Assert.IsInstanceOfType<NameExpression>(((Interpolation)s.Contents[1]).Expression);
        var formatted = (Interpolation)s.Contents[3];
        Assert.AreEqual(5, ((LiteralExpression)formatted.Alignment).Value);
        Assert.AreEqual("F2", formatted.Format);
        Assert.AreEqual("!", TextValue(s.Contents[4]));
    }

    [TestMethod]
    public void Interpolated_EscapedBracesAndEscapes()
    {
        var s = Interpolated("$\"{{x}} \\t {y}\"");

        Assert.AreEqual("{x} \t ", TextValue(s.Contents[0]));
        Assert.AreEqual("{{x}} \\t ", ((InterpolatedStringText)s.Contents[0]).Text);
    }

    [TestMethod]
    public void Interpolated_Verbatim()
    {
        var s = Interpolated("$@\"{name}\\path \"\"q\"\"\"");
        Assert.AreEqual("$@\"", s.StartToken);
        Assert.AreEqual("\\path \"q\"", TextValue(s.Contents[1]));

        Assert.AreEqual("@$\"", Interpolated("@$\"{name}\"").StartToken);
    }

    [TestMethod]
    public void Interpolated_NestedStrings()
    {
        var s = Interpolated("$\"outer {$\"inner {x}\"} {\"lit\"}\"");

        Assert.IsInstanceOfType<InterpolatedStringExpression>(((Interpolation)s.Contents[1]).Expression);
        Assert.AreEqual("lit", ((LiteralExpression)((Interpolation)s.Contents[3]).Expression).Value);
    }

    [TestMethod]
    public void Interpolated_ParenthesizedConditional()
    {
        var s = Interpolated("$\"{(a > 0 ? b : c)}\"");

        var hole = (Interpolation)s.Contents[0];
        Assert.IsInstanceOfType<ParenthesizedExpression>(hole.Expression);
        Assert.IsNull(hole.Format);
    }

    [TestMethod]
    public void Interpolated_RawWithMultipleDollars()
    {
        var s = Interpolated("$$\"\"\"{\"value\": {{x}}}\"\"\"");

        Assert.AreEqual(2, s.BraceCount);
        Assert.AreEqual("$$\"\"\"", s.StartToken);
        Assert.AreEqual("{\"value\": ", TextValue(s.Contents[0]));
        Assert.IsInstanceOfType<Interpolation>(s.Contents[1]);
        Assert.AreEqual("}", TextValue(s.Contents[2]));
    }

    [TestMethod]
    public void Interpolated_RawMultiLine()
    {
        var s = Interpolated("$\"\"\"\n    Name: {name}\n      Next\n    \"\"\"");

        Assert.AreEqual("Name: ", TextValue(s.Contents[0]));
        Assert.AreEqual("\n  Next", TextValue(s.Contents[2]));
        Assert.AreEqual("\n    \"\"\"", s.EndToken);
    }

    [TestMethod]
    public void Interpolated_WriterReproducesSource()
    {
        foreach (var text in new[]
        {
            "$\"a {b,-3:X} {{c}}\"",
            "$@\"{a}\\path\"",
            "$$\"\"\"{x}: {{y}}\"\"\"",
            "$\"\"\"\n    line {a}\n    \"\"\"",
        })
        {
            var code = $"class C {{ object x = {text}; }}";
            var writer = new CSharpWriter();
            writer.WriteCompilationUnit(CSharpParser.Parse(code));

            Assert.Contains(text, writer.GetResult());
        }
    }
}
