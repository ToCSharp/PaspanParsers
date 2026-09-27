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

    [TestMethod]
    public void IntegerLiterals_HaveTheirValues()
    {
        foreach (var (text, value, suffix) in new (string, ulong?, string)[]
        {
            ("0", 0UL, null),
            ("42", 42UL, null),
            ("017", 15UL, null),
            ("0x1F'FFull", 0x1FFFUL, "ull"),
            ("0b1010", 10UL, null),
            ("18446744073709551615ULL", ulong.MaxValue, "ULL"),
            ("18446744073709551616", null, null),
            ("42uz", 42UL, "uz"),
        })
        {
            var literal = CppTestHelper.Expression<LiteralExpression>(text);
            Assert.AreEqual(LiteralKind.Integer, literal.Kind, text);
            Assert.AreEqual(value, (ulong?)literal.Value, text);
            Assert.AreEqual(suffix, literal.Suffix, text);
            Assert.IsNull(literal.UserDefinedSuffix, text);
        }
    }

    [TestMethod]
    public void FloatingLiterals_HaveTheirValues()
    {
        foreach (var (text, value) in new[]
        {
            ("1.5", 1.5), (".5", 0.5), ("1.", 1.0), ("1e3", 1000.0), ("2.5E-1f", 0.25), ("1'000.5", 1000.5),
            ("0x1.8p1", 3.0), ("0x.Cp-2", 0.1875), ("0xAp0", 10.0), ("1e400", double.PositiveInfinity),
        })
        {
            var literal = CppTestHelper.Expression<LiteralExpression>(text);
            Assert.AreEqual(LiteralKind.Floating, literal.Kind, text);
            Assert.AreEqual(value, (double)literal.Value, text);
        }
    }

    [TestMethod]
    public void UserDefinedSuffixes_AreSeparated()
    {
        var number = CppTestHelper.Expression<LiteralExpression>("1.5_km");
        Assert.AreEqual("_km", number.UserDefinedSuffix);
        Assert.AreEqual(1.5, number.Value);

        var integer = CppTestHelper.Expression<LiteralExpression>("12_e");
        Assert.AreEqual(LiteralKind.Integer, integer.Kind);
        Assert.AreEqual("_e", integer.UserDefinedSuffix);

        var text = CppTestHelper.Expression<LiteralExpression>("u\"ab\"_s");
        Assert.AreEqual("_s", text.UserDefinedSuffix);
        Assert.AreEqual("ab", text.Value);

        var character = CppTestHelper.Expression<LiteralExpression>("'x'_c");
        Assert.AreEqual("_c", character.UserDefinedSuffix);
        Assert.AreEqual((long)'x', character.Value);
    }

    [TestMethod]
    public void CharacterLiterals_HaveTheirValues()
    {
        foreach (var (text, value, encoding) in new (string, long, CharacterEncoding)[]
        {
            ("'a'", 'a', CharacterEncoding.Ordinary),
            ("'\\n'", 10, CharacterEncoding.Ordinary),
            ("'\\''", '\'', CharacterEncoding.Ordinary),
            ("'\\0'", 0, CharacterEncoding.Ordinary),
            ("'\\xFF'", 255, CharacterEncoding.Ordinary),
            ("'\\377'", 255, CharacterEncoding.Ordinary),
            ("'\\x{41}'", 0x41, CharacterEncoding.Ordinary),
            ("'\\o{101}'", 0x41, CharacterEncoding.Ordinary),
            ("'\\e'", 27, CharacterEncoding.Ordinary),
            ("'ab'", ('a' << 8) | 'b', CharacterEncoding.Ordinary),
            ("u8'x'", 'x', CharacterEncoding.Utf8),
            ("u'\\u00e9'", 0xE9, CharacterEncoding.Utf16),
            ("U'\\U0001F600'", 0x1F600, CharacterEncoding.Utf32),
            ("U'\\u{1F600}'", 0x1F600, CharacterEncoding.Utf32),
            ("U'😀'", 0x1F600, CharacterEncoding.Utf32),
            ("L'\\xFFFFFFFF'", 0xFFFFFFFF, CharacterEncoding.Wide),
        })
        {
            var literal = CppTestHelper.Expression<LiteralExpression>(text);
            Assert.AreEqual(LiteralKind.Character, literal.Kind, text);
            Assert.AreEqual(value, (long)literal.Value, text);
            Assert.AreEqual(encoding, literal.Encoding, text);
        }
    }

    [TestMethod]
    public void StringLiterals_HaveTheirValues()
    {
        var plain = CppTestHelper.Expression<LiteralExpression>("\"a\\tb\\x41\\101\\u00e9é\"");
        CollectionAssert.AreEqual("a\tbAA\u00e9\u00e9"u8.ToArray(), (byte[])plain.Value);

        var narrowHex = CppTestHelper.Expression<LiteralExpression>("\"\\xE9\"");
        CollectionAssert.AreEqual(new byte[] { 0xE9 }, (byte[])narrowHex.Value);

        var utf16 = CppTestHelper.Expression<LiteralExpression>("u\"\\U0001F600\"");
        Assert.AreEqual(CharacterEncoding.Utf16, utf16.Encoding);
        Assert.AreEqual("\U0001F600", utf16.Value);

        var raw = CppTestHelper.Expression<LiteralExpression>("LR\"x(a\\n)\")x\"");
        Assert.IsTrue(raw.IsRaw);
        Assert.AreEqual(CharacterEncoding.Wide, raw.Encoding);
        Assert.AreEqual("a\\n)\"", raw.Value);

        var named = CppTestHelper.Expression<LiteralExpression>("\"\\N{LATIN SMALL LETTER E WITH ACUTE}\"");
        Assert.IsNull(named.Value);
    }

    [TestMethod]
    public void AdjacentStrings_AreConcatenatedInTheirEncoding()
    {
        var concatenation = CppTestHelper.Expression<ConcatenatedStringExpression>("\"a\\x41\" u\"b\" R\"(c)\"");
        Assert.HasCount(3, concatenation.Parts);
        Assert.AreEqual(CharacterEncoding.Utf16, concatenation.Encoding);
        Assert.AreEqual("aAbc", concatenation.Value);
        Assert.AreEqual(concatenation.Span.Start, concatenation.Parts[0].Span.Start);
        Assert.AreEqual(concatenation.Span.End, concatenation.Parts[2].Span.End);

        var narrow = CppTestHelper.Expression<ConcatenatedStringExpression>("\"a\"\n\"b\"");
        CollectionAssert.AreEqual("ab"u8.ToArray(), (byte[])narrow.Value);

        Assert.IsInstanceOfType<LiteralExpression>(CppTestHelper.Expression("\"single\""));
    }

    [TestMethod]
    public void LineSplices_InsideTokens_AreRemovedFromTheirText()
    {
        var source = "int ma\\\nin = 1 +\\\r\n2;\nauto s = \"a\\\nb\";\nauto r = R\"(x\\\ny)\";\nbool k = tr\\\nue;";
        var unit = CppTestHelper.Parse(source);

        var declaration = (SimpleDeclaration)unit.Declarations[0];
        var name = (NameDeclarator)declaration.Declarators[0].Declarator;
        Assert.AreEqual("main", name.Name.ToString());
        Assert.AreEqual("ma\\\nin", name.Span.GetText(source));

        // An operator split by a splice is one token
        var lessOrEqual = CppTestHelper.Expression<BinaryExpression>("a <\\\n= b");
        Assert.AreEqual("<=", lessOrEqual.Operator);

        var s = (LiteralExpression)((EqualsInitializer)((SimpleDeclaration)unit.Declarations[1]).Declarators[0].Initializer).Value;
        Assert.AreEqual("\"ab\"", s.Text);
        CollectionAssert.AreEqual("ab"u8.ToArray(), (byte[])s.Value);

        // Splices in raw strings are kept
        var r = (LiteralExpression)((EqualsInitializer)((SimpleDeclaration)unit.Declarations[2]).Declarators[0].Initializer).Value;
        CollectionAssert.AreEqual("x\\\ny"u8.ToArray(), (byte[])r.Value);

        var k = (LiteralExpression)((EqualsInitializer)((SimpleDeclaration)unit.Declarations[3]).Declarators[0].Initializer).Value;
        Assert.AreEqual(true, k.Value);
    }

    [TestMethod]
    public void Identifiers_AreUnicodeAndUniversalCharacterNames()
    {
        foreach (var (text, value) in new[]
        {
            ("café", "café"),
            ("caf\\u00e9", "café"),
            ("\\U000065e5本", "日本"),
            ("x\\u{2C7C}", "x\u2C7C"),
            ("αβγ", "αβγ"),
            ("a·b", "a·b"),
            ("_$x1", "_$x1"),
            ("\\N{GREEK SMALL LETTER ALPHA}", "\\N{GREEK SMALL LETTER ALPHA}"),
        })
        {
            Assert.AreEqual(value, CppTestHelper.Expression<NameExpression>(text).Name.ToString(), text);
        }

        // Not identifier characters: an emoji, a digit first, a no-break space
        CppTestHelper.AssertParseFails(CppTestHelper.InFunction("😀;"));
        CppTestHelper.AssertParseFails(CppTestHelper.InFunction("int \\u0031a;"));
        CppTestHelper.AssertParseFails(CppTestHelper.InFunction("int a\u00A0b;"));
    }
}
