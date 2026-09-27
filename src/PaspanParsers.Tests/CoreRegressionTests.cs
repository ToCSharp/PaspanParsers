using Paspan;
using Paspan.Common;
using Paspan.Fluent;
using PaspanParsers.Json;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.Tests;

/// <summary>
/// Regression tests for bugs fixed in the core library (SpanReader and combinators).
/// </summary>
[TestClass]
public class CoreRegressionTests
{
    private static int PositionAfter<T>(Parser<T> parser, string input, out bool success)
    {
        var reader = new SpanReader(input);
        var result = new ParseResult<T>();
        success = parser.Parse(ref reader, new ParseContext(), ref result);
        return reader.GetCurrentPosition();
    }

    // SpanReader

    [TestMethod]
    public void ReadByteShouldReturnFalseAtEof()
    {
        var reader = new SpanReader("a");

        Assert.IsTrue(reader.ReadByte(out var b));
        Assert.AreEqual((byte)'a', b);
        Assert.IsFalse(reader.ReadByte(out _));
    }

    [TestMethod]
    public void QuotedStringShouldDetectEscapeRightAfterOpeningQuote()
    {
        var reader = new SpanReader("\"\\\"x\"");

        Assert.IsTrue(reader.ReadQuotedString());
        Assert.AreEqual("\\\"x", reader.GetString());
        Assert.IsTrue(reader.Eof());
    }

    [TestMethod]
    [DataRow("\"a\\x4\"", "a\\x4")]
    [DataRow("\"a\\x41\"", "a\\x41")]
    [DataRow("\"a\\x41a\"", "a\\x41a")]
    [DataRow("\"a\\x0041\"", "a\\x0041")]
    [DataRow("\"a\\x00410\"", "a\\x00410")]
    [DataRow("\"a\\u0041\"", "a\\u0041")]
    public void QuotedStringShouldNotSkipCharAfterHexEscape(string text, string expected)
    {
        var reader = new SpanReader(text);

        Assert.IsTrue(reader.ReadQuotedString());
        Assert.AreEqual(expected, reader.GetString());
        Assert.IsTrue(reader.Eof());
    }

    [TestMethod]
    [DataRow("\"\\u004\"")]
    [DataRow("\"\\xg\"")]
    public void QuotedStringShouldRejectInvalidHexEscape(string text)
    {
        var reader = new SpanReader(text);

        Assert.IsFalse(reader.ReadQuotedString());
        Assert.AreEqual(0, reader.GetCurrentPosition());
    }

    [TestMethod]
    public void ReadDecimalShouldReadExponentAfterTrailingSeparator()
    {
        var reader = new SpanReader("1.e5");

        Assert.IsTrue(reader.ReadDecimal(out var number));
        Assert.AreEqual("1.e5", reader.GetString(number));
    }

    // Literals

    [TestMethod]
    [DataRow("\"\\u0041\"", "A")]
    [DataRow("\"\\x41\"", "A")]
    [DataRow("\"\\x041z\"", "Az")]
    [DataRow("\"a\\u00e9b\"", "aéb")]
    public void StringLiteralShouldDecodeHexEscapes(string text, string expected)
    {
        Assert.AreEqual(expected, Literals.String().Parse(text));
    }

    [TestMethod]
    public void IntegerLiteralsShouldParseMinValue()
    {
        Assert.AreEqual(int.MinValue, new IntegerLiteral().Parse(int.MinValue.ToString()));
        Assert.AreEqual(long.MinValue, new Integer64Literal().Parse(long.MinValue.ToString()));
        Assert.AreEqual(42, new IntegerLiteral().Parse("+42"));
        Assert.IsFalse(new IntegerLiteral().TryParse("2147483648", out _));
    }

    [TestMethod]
    public void KeywordShouldNotMatchWhenFollowedByNonAsciiLetter()
    {
        var parser = Terms.Keyword("return");

        Assert.IsFalse(parser.TryParse("returnы", out _));
        Assert.IsFalse(parser.TryParse("returné", out _));
        Assert.IsTrue(parser.TryParse("return«", out _));
    }

    [TestMethod]
    public void WhiteSpaceShouldNotSplitMultiByteChars()
    {
        // 'Р' is D0 A0 in UTF-8, 0xA0 must not be treated as whitespace
        Assert.AreEqual("аР", Terms.NonWhiteSpace().AsString().Parse(" аР b"));
        Assert.IsFalse(Character.IsWhiteSpace(0xA0));
    }

    [TestMethod]
    public void CharDigitHelpersShouldRejectNonAsciiChars()
    {
        // (byte)'İ' == 0x30 == '0'
        Assert.IsFalse(Character.IsDecimalDigit('İ'));
        Assert.IsFalse(Character.IsHexDigit('İ'));
        Assert.IsTrue(Character.IsDecimalDigit('7'));
        Assert.IsTrue(Character.IsHexDigit('F'));
    }

    // Combinators

    [TestMethod]
    public void AnyOfShouldNotConsumeTheFirstNonMatchingByte()
    {
        var parser = Literals.AnyOf("ab").And(Literals.Char('c'));

        Assert.IsTrue(parser.TryParse("abc", out _));
        Assert.AreEqual(2, PositionAfter(Literals.AnyOf("ab"), "abc", out var success));
        Assert.IsTrue(success);
        Assert.AreEqual(2, PositionAfter(Literals.NoneOf("c"), "abc", out success));
        Assert.IsTrue(success);
    }

    [TestMethod]
    public void AnyOfShouldRejectNonAsciiChars()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Literals.AnyOf("aы"));
    }

    [TestMethod]
    public void TermsAnyOfAsStringShouldNotIncludeLeadingWhiteSpace()
    {
        Assert.AreEqual("ab", Terms.AnyOf("ab").AsString().Parse("  ab"));
    }

    [TestMethod]
    public void IfShouldFailWhenInnerParserFails()
    {
        var parser = If(_ => true, Literals.Char('a'));

        Assert.IsTrue(parser.TryParse("a", out _));
        Assert.IsFalse(parser.TryParse("b", out _));
    }

    [TestMethod]
    public void ReadExtensionShouldApplyParser()
    {
        var parser = Literals.Text("ab").Read(1);

        Assert.IsTrue(parser.TryParse("abc", out var result));
        Assert.AreEqual("ab", result);
        Assert.IsFalse(parser.TryParse("xyz", out _));
    }

    [TestMethod]
    public void ZeroOrManyShouldTerminateOnParserThatConsumesNothing()
    {
        var zeroOrMany = ZeroOrMany(ZeroOrOne(Literals.Char('a')));

        Assert.IsTrue(zeroOrMany.TryParse("aab", out var many));
        Assert.HasCount(2, many);
        Assert.IsTrue(zeroOrMany.TryParse("b", out many));
        Assert.IsEmpty(many);
    }

    [TestMethod]
    public void OneOrManyShouldTerminateOnParserThatConsumesNothing()
    {
        var oneOrMany = OneOrMany(ZeroOrOne(Literals.Char('a')));

        Assert.IsTrue(oneOrMany.TryParse("aab", out var many));
        Assert.HasCount(2, many);
        Assert.IsTrue(oneOrMany.TryParse("b", out many));
        Assert.HasCount(1, many);
    }

    [TestMethod]
    public void SeparatedShouldTerminateWhenNothingIsConsumed()
    {
        var parser = Separated(ZeroOrOne(Literals.Char(',')), ZeroOrOne(Literals.Char('a')));

        Assert.IsTrue(parser.TryParse("a,a", out var items));
        Assert.HasCount(2, items);
    }

    [TestMethod]
    public void FailingParsersShouldNotMoveTheCursor()
    {
        var ab = Literals.Char('a').And(Literals.Char('b'));

        Assert.AreEqual(0, PositionAfter(ab.Eof(), "abc", out var success));
        Assert.IsFalse(success);

        Assert.AreEqual(0, PositionAfter(Literals.Char('a').Switch((c, _) => Literals.Char('b')), "ac", out success));
        Assert.IsFalse(success);

        Assert.AreEqual(0, PositionAfter(Not(Literals.Char('x')), "ab", out success));
        Assert.IsTrue(success);
    }

    [TestMethod]
    public void AnyCharBeforeShouldRollBackWhenEmptyIsNotAllowed()
    {
        var parser = AnyCharBefore(Literals.Char(';'), consumeDelimiter: true);

        Assert.AreEqual(0, PositionAfter(parser, ";abc", out var success));
        Assert.IsFalse(success);
    }

    [TestMethod]
    public void TextBeforeShouldAddTheWholeDelimiterToTheResult()
    {
        var parser = new TextBefore<string>(Literals.Text("*/"), addDelimiterToResult: true);

        Assert.AreEqual("abc*/", parser.Parse("abc*/def"));
    }

    [TestMethod]
    public void TextBeforeShouldNotReadPastBufferAtEof()
    {
        var parser = new TextBefore<string>(Literals.Text("*/"), failOnEof: false, addDelimiterToResult: true);

        Assert.AreEqual("abc", parser.Parse("abc"));
    }

    [TestMethod]
    [DataRow("// comment")]
    [DataRow("//")]
    [DataRow("//\n")]
    [DataRow("// comment\n")]
    public void SingleLineCommentsShouldAcceptEofAndEmptyComments(string text)
    {
        Assert.IsTrue(Literals.Comments("//").TryParse(text, out _));
    }

    [TestMethod]
    public void PooledStringValueShouldReuseStringsWithTheSameContent()
    {
        var first = PooledStringValue.GetString("pooled-value"u8);
        var second = PooledStringValue.GetString("pooled-value"u8.ToArray());

        Assert.AreSame(first, second);
    }

    // Parsers relying on the combinators

    [TestMethod]
    public void JsonShouldParseEmptyArraysAndObjects()
    {
        var array = JsonParser.Parse("[]") as JsonArray;
        Assert.IsNotNull(array);
        Assert.IsEmpty(array.Elements);

        var obj = JsonParser.Parse("{ }") as JsonObject;
        Assert.IsNotNull(obj);
        Assert.IsEmpty(obj.Members);

        var nested = JsonParser.Parse("{\"a\": [], \"b\": {}}") as JsonObject;
        Assert.IsNotNull(nested);
        Assert.HasCount(2, nested.Members);
    }
}
