using Paspan;
using Paspan.Common;
using Paspan.Fluent;
using System.Globalization;
using System.Numerics;
using static Paspan.Fluent.Parsers;
#pragma warning disable IDE0059 // Unnecessary assignment of a value

namespace PaspanParsers.Tests;

[TestClass]
public class FluentTests
{
    [TestMethod]
    public void WhenShouldFailParserWhenFalse()
    {
        var evenIntegers = Literals.Integer().When((c, x) => x % 2 == 0);

        Assert.IsTrue(evenIntegers.TryParse("1234", out var result1));
        Assert.AreEqual(1234, result1);

        Assert.IsFalse(evenIntegers.TryParse("1235", out var result2));
        Assert.AreEqual(default, result2);
    }

    [TestMethod]
    public void IfShouldNotInvokeParserWhenFalse()
    {
        bool invoked = false;

#pragma warning disable CS0618 // Type or member is obsolete
        var evenState = If(predicate: (context, x) => x % 2 == 0, state: 0, parser: Literals.Integer().Then(x => invoked = true));
        var oddState = If(predicate: (context, x) => x % 2 == 0, state: 1, parser: Literals.Integer().Then(x => invoked = true));
#pragma warning restore CS0618 // Type or member is obsolete

        Assert.IsFalse(oddState.TryParse("1234", out var result1));
        Assert.IsFalse(invoked);

        Assert.IsTrue(evenState.TryParse("1234", out var result2));
        Assert.IsTrue(invoked);
    }

    [TestMethod]
    public void WhenShouldResetPositionWhenFalse()
    {
        var evenIntegers = ZeroOrOne(Literals.Integer().When((c, x) => x % 2 == 0)).And(Literals.Integer());

        Assert.IsTrue(evenIntegers.TryParse("1235", out var result1));
        Assert.AreEqual(1235, result1.Item2);
    }

    [TestMethod]
    public void WhenFollowedByShouldSucceedWhenLookaheadMatches()
    {
        var parser = Literals.Integer().WhenFollowedBy(Terms.Text("abc"));

        Assert.IsTrue(parser.TryParse("123abc", out var result1));
        Assert.AreEqual(123, result1);
    }

    [TestMethod]
    public void WhenFollowedByShouldFailWhenLookaheadDoesNotMatch()
    {
        var parser = Literals.Integer().WhenFollowedBy(Terms.Text("abc"));

        Assert.IsFalse(parser.TryParse("123xyz", out var result1));
        Assert.AreEqual(default, result1);
    }

    [TestMethod]
    public void WhenFollowedByShouldNotConsumeInput()
    {
        var parser = Literals.Integer().WhenFollowedBy(Literals.Char('x')).And(Literals.Char('x').AsChar());

        Assert.IsTrue(parser.TryParse("123x", out var result1));
        Assert.AreEqual((123, 'x'), result1);
    }

    [TestMethod]
    public void WhenNotFollowedByShouldSucceedWhenLookaheadDoesNotMatch()
    {
        var parser = Literals.Integer().WhenNotFollowedBy(Terms.Text("abc"));

        Assert.IsTrue(parser.TryParse("123xyz", out var result1));
        Assert.AreEqual(123, result1);
    }

    [TestMethod]
    public void WhenNotFollowedByShouldFailWhenLookaheadMatches()
    {
        var parser = Literals.Integer().WhenNotFollowedBy(Terms.Text("abc"));

        Assert.IsFalse(parser.TryParse("123abc", out var result1));
        Assert.AreEqual(default, result1);
    }

    [TestMethod]
    public void WhenNotFollowedByShouldNotConsumeInput()
    {
        var parser = Literals.Integer().WhenNotFollowedBy(Literals.Char('x')).And(Literals.Char('y').AsChar());

        Assert.IsTrue(parser.TryParse("123y", out var result1));
        Assert.AreEqual((123, 'y'), result1);
    }

    [TestMethod]
    public void ShouldCast()
    {
        var parser = Literals.Integer().Then<decimal>();

        Assert.IsTrue(parser.TryParse("123", out var result1));
        Assert.AreEqual(123, result1);
    }

    [TestMethod]
    public void ShouldReturnElse()
    {
        var parser = Literals.Integer().Then<decimal>().Else(0);

        Assert.IsTrue(parser.TryParse("123", out var result1));
        Assert.AreEqual(123, result1);

        Assert.IsTrue(parser.TryParse(" 123", out var result2));
        Assert.AreEqual(0, result2);
    }

    //[Fact]
    //public void ShouldReturnElseFromFunction()
    //{
    //    var parser = Literals.Integer().Then<decimal>().Else(context => -1);

    //    Assert.IsTrue(parser.TryParse("123", out var result1));
    //    Assert.AreEqual(123, result1);

    //    Assert.IsTrue(parser.TryParse(" 123", out var result2));
    //    Assert.AreEqual(-1, result2);
    //}

    //[Fact]
    //public void ElseFunctionShouldReceiveContext()
    //{
    //    var parser = Literals.Integer().Then<int>().Else(context => context.Scanner.Cursor.Position.Offset);

    //    Assert.IsTrue(parser.TryParse("123", out var result1));
    //    Assert.AreEqual(123, result1);

    //    // When parser fails, it should return the current position (which is 0 before whitespace is skipped)
    //    Assert.IsTrue(parser.TryParse(" 123", out var result2));
    //    Assert.AreEqual(0, result2);
    //}

    //[Fact]
    //public void ElseFunctionWithNullableValue()
    //{
    //    var parser = Literals.Integer().Then<long?>(x => x).Else(context => (long?)null);

    //    Assert.IsTrue(parser.TryParse("123", out var result1));
    //    Assert.AreEqual(123, result1);

    //    Assert.IsTrue(parser.TryParse(" 123", out var result2));
    //    Assert.IsNull(result2);
    //}

    [TestMethod]
    public void ShouldThenElse()
    {
        var parser = Literals.Integer().ThenElse<long?>(x => x, null);

        Assert.IsTrue(parser.TryParse("123", out var result1));
        Assert.AreEqual(123, result1);

        Assert.IsTrue(parser.TryParse(" 123", out var result2));
        Assert.IsNull(result2);
    }

    [TestMethod]
    public void IntegerShouldResetPositionWhenItFails()
    {
        var parser = OneOf(Terms.Integer(NumberOptions.AllowLeadingSign).Then(x => "a"), Literals.Text("+").Then(x => "b"));

        // The + sign will advance the first parser and should reset the position for the second to read it successfully

        Assert.IsTrue(parser.TryParse("+abc", out var result1));
        Assert.AreEqual("b", result1);
    }

    [TestMethod]
    public void DecimalShouldResetPositionWhenItFails()
    {
        var parser = OneOf(Terms.Decimal(NumberOptions.AllowLeadingSign).Then(x => "a"), Literals.Text("+").Then(x => "b"));

        // The + sign will advance the first parser and should reset the position for the second to read it successfully

        Assert.IsTrue(parser.TryParse("+abc", out var result1));
        Assert.AreEqual("b", result1);
    }

    [TestMethod]
    public void ThenShouldConvertParser()
    {
        var evenIntegers = Literals.Integer().Then(x => x % 2);

        Assert.IsTrue(evenIntegers.TryParse("1234", out var result1));
        Assert.AreEqual(0, result1);

        Assert.IsTrue(evenIntegers.TryParse("1235", out var result2));
        Assert.AreEqual(1, result2);
    }

    [TestMethod]
    public void ThenShouldOnlyBeInvokedIfParserSucceeded()
    {
        var invoked = false;
        var evenIntegers = Literals.Integer().Then(x => invoked = true);

        Assert.IsFalse(evenIntegers.TryParse("abc", out var result1));
        Assert.IsFalse(invoked);

        Assert.IsTrue(evenIntegers.TryParse("1235", out var result2));
        Assert.IsTrue(invoked);
    }

    //[Fact]
    //public void ThenShouldProvideStartAndEndOffsets()
    //{
    //    // Use Literals for consistent behavior between compiled and non-compiled modes
    //    var parser = Literals.Identifier().Then((context, start, end, value) =>
    //    {
    //        return $"{value}:{start}-{end}";
    //    });

    //    Assert.IsTrue(parser.TryParse("hello", out var result));
    //    Assert.AreEqual("hello:0-5", result);

    //    // Test with compiled parser - should have the same behavior
    //    var compiled = parser.Compile();
    //    Assert.IsTrue(compiled.TryParse("world", out var result2));
    //    Assert.AreEqual("world:0-5", result2);
    //}

    [TestMethod]
    public void BetweenShouldParseBetweenTwoString()
    {
        var code = Between(Terms.Text("[["), Terms.Integer(), Terms.Text("]]"));

        Assert.IsTrue(code.TryParse("[[123]]", out long result));
        Assert.AreEqual(123, result);

        Assert.IsTrue(code.TryParse(" [[ 123 ]] ", out result));
        Assert.AreEqual(123, result);

        Assert.IsFalse(code.TryParse("abc", out _));
        Assert.IsFalse(code.TryParse("[[abc", out _));
        Assert.IsFalse(code.TryParse("123", out _));
        Assert.IsFalse(code.TryParse("[[123", out _));
        Assert.IsFalse(code.TryParse("[[123]", out _));
    }

    [TestMethod]
    public void TextShouldResetPosition()
    {
        var code = OneOf(Terms.Text("subtract"), Terms.Text("substitute"));

        Assert.IsFalse(code.TryParse("sublime", out _));
        Assert.IsTrue(code.TryParse("subtract", out _));
        Assert.IsTrue(code.TryParse("substitute", out _));
    }

    [TestMethod]
    public void TextWithWhiteSpaceShouldResetPosition()
    {
        var code = OneOf(Terms.Text("a"), Literals.Text(" b"));

        Assert.IsTrue(code.TryParse(" b", out _));
    }

    [TestMethod]
    public void AndSkipShouldResetPosition()
    {
        var code =
            OneOf(
                Terms.Text("hello").AndSkip(Terms.Text("world")),
                Terms.Text("hello").AndSkip(Terms.Text("universe"))
                );

        Assert.IsFalse(code.TryParse("hello country", out _));
        Assert.IsTrue(code.TryParse("hello universe", out _));
        Assert.IsTrue(code.TryParse("hello world", out _));
    }

    [TestMethod]
    public void SkipAndShouldResetPosition()
    {
        var code =
            OneOf(
                Terms.Text("hello").SkipAnd(Terms.Text("world")),
                Terms.Text("hello").AndSkip(Terms.Text("universe"))
            );

        Assert.IsFalse(code.TryParse("hello country", out _));
        Assert.IsTrue(code.TryParse("hello universe", out _));
        Assert.IsTrue(code.TryParse("hello world", out _));
    }


    [TestMethod]
    public void ShouldSkipSequences()
    {
        var parser = Terms.Char('a').AsChar().And(Terms.Char('b').AsChar()).AndSkip(Terms.Char('c')).And(Terms.Char('d').AsChar());

        Assert.IsTrue(parser.TryParse("abcd", out var result1));
        Assert.AreEqual("abd", result1.Item1.ToString() + result1.Item2 + result1.Item3);
    }

    [TestMethod]
    public void ParseContextShouldUseNewLines()
    {
        Assert.AreEqual("a", Terms.NonWhiteSpace().AsString().Parse("\n\r\v a"));
    }

    [TestMethod]
    public void LiteralsShouldNotSkipWhiteSpaceByDefault()
    {
        Assert.IsFalse(Literals.Char('a').TryParse(" a", out _));
        Assert.IsFalse(Literals.Decimal().TryParse(" 123", out _));
        Assert.IsFalse(Literals.String().TryParse(" 'abc'", out _));
        Assert.IsFalse(Literals.Text("abc").TryParse(" abc", out _));
    }

    [TestMethod]
    public void TermsShouldSkipWhiteSpaceByDefault()
    {
        Assert.IsTrue(Terms.Char('a').TryParse(" a", out _));
        Assert.IsTrue(Terms.Decimal().TryParse(" 123", out _));
        Assert.IsTrue(Terms.String().TryParse(" 'abc'", out _));
        Assert.IsTrue(Terms.Text("abc").TryParse(" abc", out _));
    }

    [TestMethod]
    public void CharLiteralShouldBeCaseSensitive()
    {
        Assert.IsTrue(Literals.Char('a').TryParse("a", out _));
        Assert.IsFalse(Literals.Char('a').TryParse("B", out _));
    }

    [TestMethod]
    [DataRow("a", "a")]
    [DataRow("abc", "abc")]
    [DataRow(" abc", "abc")]
    public void ShouldReadPatterns(string text, string expected)
    {
        Assert.AreEqual(expected, Terms.Pattern(c => Character.IsHexDigit(c)).AsString().Parse(text));
    }

    [TestMethod]
    public void ShouldReadPatternsWithSizes()
    {
        Assert.IsFalse(Terms.Pattern(c => Character.IsHexDigit(c), minSize: 3).TryParse("ab", out _));
        Assert.AreEqual("abc", Terms.Pattern(c => Character.IsHexDigit(c), minSize: 3).AsString().Parse("abc"));
        Assert.AreEqual("abc", Terms.Pattern(c => Character.IsHexDigit(c), maxSize: 3).AsString().Parse("abcd"));
        Assert.AreEqual("abc", Terms.Pattern(c => Character.IsHexDigit(c), minSize: 3, maxSize: 3).AsString().Parse("abcd"));
        Assert.IsFalse(Terms.Pattern(c => Character.IsHexDigit(c), minSize: 3, maxSize: 2).TryParse("ab", out _));
    }

    [TestMethod]
    public void PatternShouldResetPositionWhenFalse()
    {
        Assert.IsFalse(Terms.Pattern(c => c == 'a', minSize: 3)
            .And(Terms.Pattern(c => c == 'Z'))
            .TryParse("aaZZ", out _));

        Assert.IsTrue(Terms.Pattern(c => c == 'a', minSize: 3)
             .And(Terms.Pattern(c => c == 'Z'))
             .TryParse("aaaZZ", out _));
    }

    [TestMethod]
    [DataRow("'a\nb' ", "a\nb")]
    [DataRow("'a\r\nb' ", "a\r\nb")]
    public void ShouldReadStringsWithLineBreaks(string text, string expected)
    {
        Assert.AreEqual(expected, Literals.String(StringLiteralQuotes.Single).Parse(text).ToString());
        Assert.AreEqual(expected, Literals.String(StringLiteralQuotes.SingleOrDouble).Parse(text).ToString());
    }

    [TestMethod]
    public void OrShouldReturnOneOf()
    {
        var a = Literals.Char('a').AsChar();
        var b = Literals.Char('b').AsChar();
        var c = Literals.Char('c').AsChar();

        var o2 = a.Or(b);
        var o3 = a.Or(b).Or(c);

        Assert.IsInstanceOfType<OneOf<char>>(o2);
        Assert.IsTrue(o2.TryParse("a", out _));
        Assert.IsTrue(o2.TryParse("b", out _));
        Assert.IsFalse(o2.TryParse("c", out _));

        Assert.IsInstanceOfType<OneOf<char>>(o3);
        Assert.IsTrue(o3.TryParse("a", out _));
        Assert.IsTrue(o3.TryParse("b", out _));
        Assert.IsTrue(o3.TryParse("c", out _));
        Assert.IsFalse(o3.TryParse("d", out _));
    }

    [TestMethod]
    public void OrShouldReturnOneOfCommonType()
    {
        var a = Literals.Char('a').AsChar();
        var b = Literals.Decimal();

        var o2 = a.Or<char, decimal, object>(b);

        Assert.IsInstanceOfType<OneOf<char, decimal, object>>(o2);
        Assert.IsTrue(o2.TryParse("a", out var c) && (char)c == 'a');
        Assert.IsTrue(o2.TryParse("1", out var d) && (decimal)d == 1);
    }

    [TestMethod]
    public void AndShouldReturnSequences()
    {
        var a = Literals.Char('a').AsChar();

        var s2 = a.And(a);
        var s3 = s2.And(a);
        var s4 = s3.And(a);
        var s5 = s4.And(a);
        var s6 = s5.And(a);
        var s7 = s6.And(a);

        Assert.IsInstanceOfType<Sequence<char, char>>(s2);
        Assert.IsFalse(s2.TryParse("a", out _));
        Assert.IsTrue(s2.TryParse("aab", out _));

        Assert.IsInstanceOfType<Sequence<char, char, char>>(s3);
        Assert.IsFalse(s3.TryParse("aa", out _));
        Assert.IsTrue(s3.TryParse("aaab", out _));

        Assert.IsInstanceOfType<Sequence<char, char, char, char>>(s4);
        Assert.IsFalse(s4.TryParse("aaa", out _));
        Assert.IsTrue(s4.TryParse("aaaab", out _));

        Assert.IsInstanceOfType<Sequence<char, char, char, char, char>>(s5);
        Assert.IsFalse(s5.TryParse("aaaa", out _));
        Assert.IsTrue(s5.TryParse("aaaaab", out _));

        Assert.IsInstanceOfType<Sequence<char, char, char, char, char, char>>(s6);
        Assert.IsFalse(s6.TryParse("aaaaa", out _));
        Assert.IsTrue(s6.TryParse("aaaaaab", out _));

        Assert.IsInstanceOfType<Sequence<char, char, char, char, char, char, char>>(s7);
        Assert.IsFalse(s7.TryParse("aaaaaa", out _));
        Assert.IsTrue(s7.TryParse("aaaaaaab", out _));
    }

    [TestMethod]
    public void SwitchShouldProvidePreviousResult()
    {
        var d = Literals.Text("d:");
        var i = Literals.Text("i:");
        var s = Literals.Text("s:");

        var parser = d.Or(i).Or(s).Switch((context, result) =>
        {
            return result switch
            {
                "d:" => Literals.Decimal().Then<object>(x => x),
                "i:" => Literals.Integer().Then<object>(x => x),
                "s:" => Literals.String().Then<object>(x => x),
                _ => null,
            };
        });

        Assert.IsTrue(parser.TryParse("d:123.456", out var resultD));
        Assert.AreEqual((decimal)123.456, resultD);

        Assert.IsTrue(parser.TryParse("i:123", out var resultI));
        Assert.AreEqual((long)123, resultI);

        Assert.IsTrue(parser.TryParse("s:'123'", out var resultS));
        //Assert.AreEqual("123", ((TextSpan)resultS).ToString());
    }

    [TestMethod]
    public void SwitchShouldReturnCommonType()
    {
        var d = Literals.Text("d:");
        var i = Literals.Text("i:");
        var s = Literals.Text("s:");

        var parser = d.Or(i).Or(s).Switch((context, result) =>
        {
            return result switch
            {
                "d:" => Literals.Decimal().Then(x => x.ToString(CultureInfo.InvariantCulture)),
                "i:" => Literals.Integer().Then(x => x.ToString()),
                "s:" => Literals.String().Then(x => x.ToString()),
                _ => null,
            };
        });

        Assert.IsTrue(parser.TryParse("d:123.456", out var resultD));
        Assert.AreEqual("123.456", resultD);

        Assert.IsTrue(parser.TryParse("i:123", out var resultI));
        Assert.AreEqual("123", resultI);

        Assert.IsTrue(parser.TryParse("s:'123'", out var resultS));
        Assert.AreEqual("123", resultS);
    }

    [TestMethod]
    public void SelectShouldPickParserUsingRuntimeLogic()
    {
        var allowWhiteSpace = true;
        var parser = Select<long>(_ => allowWhiteSpace ? Terms.Integer() : Literals.Integer());

        Assert.IsTrue(parser.TryParse(" 42", out var result1));
        Assert.AreEqual(42, result1);

        allowWhiteSpace = false;

        Assert.IsTrue(parser.TryParse("42", out var result2));
        Assert.AreEqual(42, result2);

        Assert.IsFalse(parser.TryParse(" 42", out _));
    }

    [TestMethod]
    public void SelectShouldFailWhenSelectorReturnsNull()
    {
        var parser = Select<long>(_ => null!);

        Assert.IsFalse(parser.TryParse("123", out _));
    }

    //[Fact]
    //public void SelectShouldHonorConcreteParseContext()
    //{
    //    var parser = Select<CustomParseContext, string>(context => context.PreferYes ? Literals.Text("yes") : Literals.Text("no"));

    //    var yesContext = new CustomParseContext(new Scanner("yes")) { PreferYes = true };
    //    Assert.IsTrue(parser.TryParse(yesContext, out var yes, out _));
    //    Assert.AreEqual("yes", yes);

    //    var noContext = new CustomParseContext(new Scanner("no")) { PreferYes = false };
    //    Assert.IsTrue(parser.TryParse(noContext, out var no, out _));
    //    Assert.AreEqual("no", no);
    //}

    //private sealed class CustomParseContext : ParseContext
    //{
    //    public CustomParseContext(Scanner scanner) : base(scanner)
    //    {
    //    }

    //    public bool PreferYes { get; set; }
    //}

    [TestMethod]
    [DataRow("a", "a")]
    [DataRow("foo", "foo")]
    [DataRow("$_", "$_")]
    [DataRow("a-foo.", "a")]
    [DataRow("abc=3", "abc")]
    [DataRow("abc3", "abc3")]
    [DataRow("abc123", "abc123")]
    [DataRow("abc_3", "abc_3")]
    public void IdentifierShouldParseValidIdentifiers(string text, string identifier)
    {
        Assert.AreEqual(identifier, Literals.Identifier().Parse(text).ToString());
    }

    [TestMethod]
    [DataRow("-foo")]
    [DataRow("-")]
    [DataRow("  ")]
    public void IdentifierShouldNotParseInvalidIdentifiers(string text)
    {
        Assert.IsFalse(Literals.Identifier().TryParse(text, out _));
    }

    [TestMethod]
    [DataRow("-foo")]
    [DataRow("/foo")]
    [DataRow("foo@asd")]
    [DataRow("foo*")]
    public void IdentifierShouldAcceptExtraChars(string text)
    {
        static bool start(char c) => c == '-' || c == '/';
        static bool part(char c) => c == '@' || c == '*';

        Assert.AreEqual(text, Literals.Identifier(start, part).Parse(text).ToString());
    }

    [TestMethod]
    public void IntegersShouldAcceptSignByDefault()
    {
        Assert.IsTrue(Terms.Integer().TryParse("-123", out _));
        Assert.IsTrue(Terms.Integer().TryParse("+123", out _));
    }

    [TestMethod]
    public void DecimalsShouldAcceptSignByDefault()
    {
        Assert.IsTrue(Terms.Decimal().TryParse("-123", out _));
        Assert.IsTrue(Terms.Decimal().TryParse("+123", out _));
    }

    [TestMethod]
    public void NumbersShouldAcceptSignIfAllowed()
    {
        Assert.AreEqual(-123, Terms.Decimal(NumberOptions.AllowLeadingSign).Parse("-123"));
        Assert.AreEqual(-123, Terms.Integer(NumberOptions.AllowLeadingSign).Parse("-123"));
        Assert.AreEqual(123, Terms.Decimal(NumberOptions.AllowLeadingSign).Parse("+123"));
        Assert.AreEqual(123, Terms.Integer(NumberOptions.AllowLeadingSign).Parse("+123"));
    }

    [TestMethod]
    public void NumbersShouldNotAcceptSignIfNotAllowed()
    {
        Assert.IsFalse(Terms.Decimal(NumberOptions.None).TryParse("-123", out _));
        Assert.IsFalse(Terms.Integer(NumberOptions.None).TryParse("-123", out _));
        Assert.IsFalse(Terms.Decimal(NumberOptions.None).TryParse("+123", out _));
        Assert.IsFalse(Terms.Integer(NumberOptions.None).TryParse("+123", out _));
    }

    [TestMethod]
    public void OneOfShouldRestorePosition()
    {
        var choice = OneOf(
            Literals.Char('a').AsChar().And(Literals.Char('b').AsChar()).And(Literals.Char('c').AsChar()).And(Literals.Char('d').AsChar()),
            Literals.Char('a').AsChar().And(Literals.Char('b').AsChar()).And(Literals.Char('e').AsChar()).And(Literals.Char('d').AsChar())
            ).Then(x => x.Item1.ToString() + x.Item2.ToString() + x.Item3.ToString() + x.Item4.ToString());

        Assert.AreEqual("abcd", choice.Parse("abcd"));
        Assert.AreEqual("abed", choice.Parse("abed"));
    }

    [TestMethod]
    public void NonWhiteSpaceShouldStopAtSpaceOrEof()
    {
        Assert.AreEqual("a", Terms.NonWhiteSpace().AsString().Parse(" a"));
        Assert.AreEqual("a", Terms.NonWhiteSpace().AsString().Parse(" a "));
        Assert.AreEqual("a", Terms.NonWhiteSpace().AsString().Parse(" a b"));
        Assert.AreEqual("a", Terms.NonWhiteSpace().AsString().Parse("a b"));
        Assert.AreEqual("abc", Terms.NonWhiteSpace().AsString().Parse("abc b"));
        Assert.AreEqual("abc", Terms.NonWhiteSpace(includeNewLines: true).AsString().Parse("abc\nb"));
        Assert.AreEqual("abc\nb", Terms.NonWhiteSpace(includeNewLines: false).AsString().Parse("abc\nb"));
        Assert.AreEqual("abc", Terms.NonWhiteSpace().AsString().Parse("abc"));

        Assert.IsFalse(Terms.NonWhiteSpace().TryParse("", out _));
        Assert.IsFalse(Terms.NonWhiteSpace().TryParse(" ", out _));
    }

    [TestMethod]
    public void ShouldParseWhiteSpace()
    {
        Assert.AreEqual("\n\r\v\f ", Literals.WhiteSpace(true).AsString().Parse("\n\r\v\f a"));
        Assert.AreEqual("  \f", Literals.WhiteSpace(false).AsString().Parse("  \f\n\r\v a"));
    }

    [TestMethod]
    public void WhiteSpaceShouldFailOnEmpty()
    {
        Assert.IsTrue(Literals.WhiteSpace().TryParse(" ", out _));
        Assert.IsFalse(Literals.WhiteSpace().TryParse("", out _));
    }

    [TestMethod]
    public void ShouldCapture()
    {
        Assert.AreEqual("../foo/bar", Capture(Literals.Text("..").AndSkip(OneOrMany(Literals.Char('/').AndSkip(Terms.Identifier())))).AsString().Parse("../foo/bar"));
    }

    //[Fact]
    //public void ShouldParseEmails()
    //{
    //    Parser<char> Dot = Literals.Char('.').AsChar();
    //    Parser<char> Plus = Literals.Char('+').AsChar();
    //    Parser<char> Minus = Literals.Char('-').AsChar();
    //    Parser<char> At = Literals.Char('@').AsChar();
    //    //Parser<char> WordChar = Literals.Pattern(char.IsLetterOrDigit).Then<char>(x => x.Span[0]);
    //    Parser<IReadOnlyList<char>> WordDotPlusMinus = OneOrMany(OneOf(WordChar, Dot, Plus, Minus));
    //    Parser<IReadOnlyList<char>> WordDotMinus = OneOrMany(OneOf(WordChar, Dot, Minus));
    //    Parser<IReadOnlyList<char>> WordMinus = OneOrMany(OneOf(WordChar, Minus));
    //    Parser<TextSpan> Email = Capture(WordDotPlusMinus.And(At).And(WordMinus).And(Dot).And(WordDotMinus));

    //    string _email = "sebastien.ros@gmail.com";

    //    Assert.IsTrue(Email.TryParse(_email, out var result));
    //    Assert.AreEqual(_email, result.ToString());
    //}

    //[Fact]
    //public void ShouldParseEmailsWithAnyOf()
    //{
    //    var letterOrDigitChars = "01234567890abcdefghijklmnopqrstuvwxyz";

    //    var Dot = Literals.AnyOf(".");
    //    var LetterOrDigit = Literals.AnyOf(letterOrDigitChars);
    //    var LetterOrDigitDotPlusMinus = Literals.AnyOf(letterOrDigitChars + ".+-");
    //    var LetterOrDigitDotMinus = Literals.AnyOf(letterOrDigitChars + ".-");
    //    var LetterOrDigitMinus = Literals.AnyOf(letterOrDigitChars + "-");

    //    Parser<char> At = Literals.Char('@');
    //    Parser<TextSpan> Email = Capture(LetterOrDigitDotPlusMinus.And(At).And(LetterOrDigitMinus).And(Dot).And(LetterOrDigitDotMinus));

    //    string _email = "sebastien.ros@gmail.com";

    //    Assert.IsTrue(Email.TryParse(_email, out var result));
    //    Assert.AreEqual(_email, result.ToString());
    //}

    [TestMethod]
    public void ShouldParseEof()
    {
        Assert.IsTrue(Always<object>().Eof().TryParse("", out _));
        Assert.IsFalse(Always<object>().Eof().TryParse(" ", out _));
        Assert.IsTrue(Terms.Decimal().Eof().TryParse("123", out var result) && result == 123);
        Assert.IsFalse(Terms.Decimal().Eof().TryParse("123 ", out _));
    }

    [TestMethod]
    public void EmptyShouldAlwaysSucceed()
    {
        Assert.IsTrue(Always<object>().TryParse("123", out var result) && result == null);
        Assert.IsTrue(Always(1).TryParse("123", out var r2) && r2 == 1);
    }


    [TestMethod]
    public void FailShouldFail()
    {
        Assert.IsFalse(Fail<object>().TryParse("123", out var result));
    }

    [TestMethod]
    public void NotShouldNegateParser()
    {
        Assert.IsFalse(Not(Terms.Decimal()).TryParse("123", out _));
        Assert.IsTrue(Not(Terms.Decimal()).TryParse("Text", out _));
    }

    [TestMethod]
    public void DiscardShouldReplaceValue()
    {
        Assert.IsTrue(Terms.Decimal().Discard<bool>(false).TryParse("123", out var r1) && r1 == false);
        Assert.IsTrue(Terms.Decimal().Discard<bool>(true).TryParse("123", out var r2) && r2 == true);
        Assert.IsFalse(Terms.Decimal().Discard<bool>(true).TryParse("abc", out _));

        Assert.IsTrue(Terms.Decimal().Then<int>().TryParse("123", out var t1) && t1 == 123);
        Assert.IsTrue(Terms.Decimal().Then(true).TryParse("123", out var t2) && t2 == true);
        Assert.IsFalse(Terms.Decimal().Then(true).TryParse("abc", out _));
    }

    [TestMethod]
    public void ErrorShouldThrowIfParserSucceeds()
    {
        Assert.IsFalse(Literals.Char('a').Error("'a' was not expected").TryParse("a", out _, out var error));
        Assert.AreEqual("'a' was not expected", error.Message);

        Assert.IsFalse(Literals.Char('a').Error<int>("'a' was not expected").TryParse("a", out _, out error));
        Assert.AreEqual("'a' was not expected", error.Message);
    }

    [TestMethod]
    public void ErrorShouldReturnFalseThrowIfParserFails()
    {
        Assert.IsFalse(Literals.Char('a').Error("'a' was not expected").TryParse("b", out _, out var error));
        Assert.IsNull(error);

        Assert.IsFalse(Literals.Char('a').Error<int>("'a' was not expected").TryParse("b", out _, out error));
        Assert.IsNull(error);
    }

    [TestMethod]
    public void ErrorShouldThrow()
    {
        Assert.IsFalse(Literals.Char('a').Error("'a' was not expected").TryParse("a", out _, out var error));
        Assert.AreEqual("'a' was not expected", error.Message);
    }

    [TestMethod]
    public void ErrorShouldResetPosition()
    {
        Assert.IsFalse(Literals.Char('a').Error("'a' was not expected").TryParse("a", out _, out var error));
        Assert.AreEqual("'a' was not expected", error.Message);
    }

    [TestMethod]
    public void ElseErrorShouldThrowIfParserFails()
    {
        Assert.IsFalse(Literals.Char('a').ElseError("'a' was expected").TryParse("b", out _, out var error));
        Assert.AreEqual("'a' was expected", error.Message);
    }

    [TestMethod]
    public void ElseErrorShouldFlowResultIfParserSucceeds()
    {
        Assert.IsTrue(Literals.Char('a').AsChar().ElseError("'a' was expected").TryParse("a", out var result));
        Assert.AreEqual('a', result);
    }

    [TestMethod]
    public void TextBeforeShouldReturnAllCharBeforeDelimiter()
    {
        Assert.IsFalse(AnyCharBefore(Literals.Char('a')).TryParse("", out _));
        Assert.IsTrue(AnyCharBefore(Literals.Char('a'), canBeEmpty: true).TryParse("", out var result1));

        Assert.IsTrue(AnyCharBefore(Literals.Char('a')).TryParse("hello", out var result2));
        Assert.AreEqual("hello", result2);
        Assert.IsTrue(AnyCharBefore(Literals.Char('a'), canBeEmpty: false).TryParse("hello", out _));
        Assert.IsFalse(AnyCharBefore(Literals.Char('a'), failOnEof: true).TryParse("hello", out _));
    }

    [TestMethod]
    public void TextBeforeShouldStopAtDelimiter()
    {
        Assert.IsTrue(AnyCharBefore(Literals.Char('a')).TryParse("hellao", out var result1));
        Assert.AreEqual("hell", result1);
    }

    [TestMethod]
    public void TextBeforeShouldNotConsumeDelimiter()
    {
        Assert.IsTrue(AnyCharBefore(Literals.Char('a')).And(Literals.Char('a')).TryParse("hellao", out _));
        Assert.IsFalse(AnyCharBefore(Literals.Char('a'), consumeDelimiter: true).And(Literals.Char('a')).TryParse("hellao", out _));
    }

    [TestMethod]
    public void TextBeforeShouldBeValidAtEof()
    {
        Assert.IsTrue(AnyCharBefore(Literals.Char('a')).TryParse("hella", out var result1));
        Assert.AreEqual("hell", result1);
    }

    [TestMethod]
    public void BetweenShouldResetPosition()
    {
        Assert.IsTrue(Between(Terms.Char('['), Terms.Text("abcd"), Terms.Char(']')).Then(x => x.ToString()).Or(Literals.Text(" [abc")).TryParse(" [abc]", out var result1));
        Assert.AreEqual(" [abc", result1);
    }

    [TestMethod]
    public void SeparatedShouldSplit()
    {
        var parser = Separated(Terms.Char(','), Terms.Decimal());

        Assert.HasCount(1, parser.Parse("1"));
        Assert.HasCount(2, parser.Parse("1,2"));
        Assert.IsNull(parser.Parse(",1,"));
        Assert.IsNull(parser.Parse(""));

        var result = parser.Parse("1, 2,3");

        Assert.AreEqual(1, result[0]);
        Assert.AreEqual(2, result[1]);
        Assert.AreEqual(3, result[2]);
    }

    [TestMethod]
    public void SeparatedShouldNotBeConsumedIfNotFollowedByValue()
    {
        // This test ensures that the separator is not consumed if there is no valid net value.

        var parser = Separated(Terms.Char(','), Terms.Decimal()).AndSkip(Terms.Char(',')).And(Terms.Identifier()).Then(x => true);

        Assert.IsFalse(parser.Parse("1"));
        Assert.IsFalse(parser.Parse("1,"));
        Assert.IsTrue(parser.Parse("1,x"));
    }

    [TestMethod]
    public void ShouldSkipWhiteSpace()
    {
        var parser = SkipWhiteSpace(Literals.Text("abc"));

        Assert.IsNull(parser.Parse(""));
        Assert.IsTrue(parser.TryParse("abc", out var result1));
        Assert.AreEqual("abc", result1);

        Assert.IsTrue(parser.TryParse("  abc", out var result2));
        Assert.AreEqual("abc", result2);
    }

    [TestMethod]
    public void SkipWhiteSpaceShouldResetPosition()
    {
        var parser = SkipWhiteSpace(Literals.Text("abc")).Or(Literals.Text(" ab"));

        Assert.IsTrue(parser.TryParse(" ab", out var result1));
        Assert.AreEqual(" ab", result1);
    }

    [TestMethod]
    public void OneOfShouldNotFailWithLookupConflicts()
    {
        var parser = Literals.Text("abc").Or(Literals.Text("ab")).Or(Literals.Text("a"));

        Assert.IsTrue(parser.TryParse("a", out _));
        Assert.IsTrue(parser.TryParse("ab", out _));
        Assert.IsTrue(parser.TryParse("abc", out _));
    }

    [TestMethod]
    public void OneOfShouldHandleSkipWhiteSpaceMix()
    {
        var parser = Literals.Text("a").Or(Terms.Text("b"));

        Assert.IsTrue(parser.TryParse("a", out _));
        Assert.IsTrue(parser.TryParse("b", out _));
        Assert.IsFalse(parser.TryParse(" a", out _));
        Assert.IsTrue(parser.TryParse(" b", out _));
    }

    [TestMethod]
    public void OneOfShouldHandleParsedWhiteSpace()
    {
        var parser = Literals.Text("a").Or(AnyCharBefore(Literals.Text("c"), null, false, true).Then(x => x.ToString()));

        Assert.IsTrue(parser.TryParse("a", out _));
        Assert.IsFalse(parser.TryParse("b", out _));
        Assert.IsFalse(parser.TryParse(" a", out _));
        Assert.IsTrue(parser.TryParse("\rcde", out _));
    }

    //[Fact]
    //public void OneOfShouldHandleContextualWhiteSpace()
    //{
    //    var parser = Terms.Text("a").Or(Terms.Text("b"));

    //    Assert.IsTrue(parser.TryParse(new ParseContext(new Scanner("\rb")), out _, out _));
    //    Assert.IsTrue(parser.TryParse(new ParseContext(new Scanner(" b")), out _, out _));
    //    Assert.IsFalse(parser.TryParse(new ParseContext(new Scanner("\rb"), useNewLines: true), out _, out _));
    //    Assert.IsTrue(parser.TryParse(new ParseContext(new Scanner(" b"), useNewLines: true), out _, out _));
    //}

    //[Fact]
    //public void SkipWhiteSpaceShouldResponseParseContextUseNewLines()
    //{
    //    // Default behavior, newlines are skipped like any other space. The grammar is not "New Line Aware"

    //    Assert.IsTrue(
    //        SkipWhiteSpace(Literals.Text("ab"))
    //        .TryParse(new ParseContext(new Scanner(" \nab"), useNewLines: false),
    //        out var _, out var _));

    //    // Here newlines are not skipped

    //    Assert.IsFalse(
    //        SkipWhiteSpace(Literals.Text("ab"))
    //        .TryParse(new ParseContext(new Scanner(" \nab"), useNewLines: true),
    //        out var _, out var _));

    //    // Here newlines are not skipped, and the grammar reads them explicitly

    //    Assert.IsTrue(
    //        SkipWhiteSpace(Literals.WhiteSpace(includeNewLines: true).SkipAnd(Literals.Text("ab")))
    //        .TryParse(new ParseContext(new Scanner(" \nab"), useNewLines: true),
    //        out var _, out var _));
    //}

    [TestMethod]
    public void ZeroOrManyShouldHandleAllSizes()
    {
        var parser = ZeroOrMany(Terms.Text("+").Or(Terms.Text("-")).And(Terms.Integer()));

        var result1 = parser.Parse("");
        Assert.IsNotNull(result1);
        Assert.IsEmpty(result1);

        var result2 = parser.Parse("+1");
        Assert.IsNotNull(result2);
        Assert.HasCount(1, result2);
        Assert.AreEqual(("+", 1L), result2[0]);

        var result3 = parser.Parse("+1-2");
        Assert.IsNotNull(result3);
        Assert.HasCount(2, result3);
        Assert.AreEqual(("+", 1L), result3[0]);
        Assert.AreEqual(("-", 2L), result3[1]);
    }

    [TestMethod]
    public void ShouldParseSequence()
    {
        var a = Literals.Char('a').AsChar();
        var b = Literals.Char('b').AsChar();
        var c = Literals.Char('c').AsChar();
        var d = Literals.Char('d').AsChar();
        var e = Literals.Char('e').AsChar();
        var f = Literals.Char('f').AsChar();
        var g = Literals.Char('g').AsChar();
        var h = Literals.Char('h').AsChar();

        Assert.IsTrue(a.And(b).TryParse("ab", out var r));
        Assert.AreEqual(('a', 'b'), r);

        Assert.IsTrue(a.And(b).And(c).TryParse("abc", out var r1));
        Assert.AreEqual(('a', 'b', 'c'), r1);

        Assert.IsTrue(a.And(b).AndSkip(c).TryParse("abc", out var r2));
        Assert.AreEqual(('a', 'b'), r2);

        Assert.IsTrue(a.And(b).SkipAnd(c).TryParse("abc", out var r3));
        Assert.AreEqual(('a', 'c'), r3);
    }

    [TestMethod]
    public void ShouldParseSequenceAndSkip()
    {
        var a = Literals.Char('a').AsChar();
        var b = Literals.Char('b').AsChar();
        var c = Literals.Char('c').AsChar();
        var d = Literals.Char('d').AsChar();
        var e = Literals.Char('e').AsChar();
        var f = Literals.Char('f').AsChar();
        var g = Literals.Char('g').AsChar();
        var h = Literals.Char('h').AsChar();

        Assert.IsTrue(a.AndSkip(b).TryParse("ab", out var r));
        Assert.AreEqual(('a'), r);

        Assert.IsTrue(a.AndSkip(b).And(c).TryParse("abc", out var r1));
        Assert.AreEqual(('a', 'c'), r1);

        Assert.IsTrue(a.AndSkip(b).AndSkip(c).TryParse("abc", out var r2));
        Assert.AreEqual(('a'), r2);

        Assert.IsTrue(a.AndSkip(b).SkipAnd(c).TryParse("abc", out var r3));
        Assert.AreEqual(('c'), r3);
    }

    [TestMethod]
    public void ShouldParseSequenceSkipAnd()
    {
        var a = Literals.Char('a').AsChar();
        var b = Literals.Char('b').AsChar();
        var c = Literals.Char('c').AsChar();
        var d = Literals.Char('d').AsChar();
        var e = Literals.Char('e').AsChar();
        var f = Literals.Char('f').AsChar();
        var g = Literals.Char('g').AsChar();
        var h = Literals.Char('h').AsChar();

        Assert.IsTrue(a.SkipAnd(b).TryParse("ab", out var r));
        Assert.AreEqual(('b'), r);

        Assert.IsTrue(a.SkipAnd(b).And(c).TryParse("abc", out var r1));
        Assert.AreEqual(('b', 'c'), r1);

        Assert.IsTrue(a.SkipAnd(b).AndSkip(c).TryParse("abc", out var r2));
        Assert.AreEqual(('b'), r2);

        Assert.IsTrue(a.SkipAnd(b).SkipAnd(c).TryParse("abc", out var r3));
        Assert.AreEqual(('c'), r3);
    }

    [TestMethod]
    public void ShouldReturnConstantResult()
    {
        var a = Literals.Char('a').Then(123);
        var b = Literals.Char('b').Then("1");

        Assert.AreEqual(123, a.Parse("a"));
        Assert.AreEqual("1", b.Parse("b"));
    }

    [TestMethod]
    public void ShouldParseWithCaseSensitivity()
    {
        var parser1 = Literals.Text("not", caseInsensitive: true);

        Assert.AreEqual("not", parser1.Parse("not"));
        Assert.AreEqual("nOt", parser1.Parse("nOt"));
        Assert.AreEqual("NOT", parser1.Parse("NOT"));

        var parser2 = Terms.Text("not", caseInsensitive: true);

        Assert.AreEqual("not", parser2.Parse("not"));
        Assert.AreEqual("nOt", parser2.Parse("nOt"));
        Assert.AreEqual("NOT", parser2.Parse("NOT"));
    }

    [TestMethod]
    public void ShouldBuildCaseInsensitiveLookupTable()
    {
        var parser = OneOf(
            Literals.Text("not", caseInsensitive: true),
            Literals.Text("abc", caseInsensitive: false),
            Literals.Text("aBC", caseInsensitive: false)
            );

        Assert.AreEqual("not", parser.Parse("not"));
        Assert.AreEqual("nOt", parser.Parse("nOt"));
        Assert.AreEqual("abc", parser.Parse("abc"));
        Assert.AreEqual("aBC", parser.Parse("aBC"));
        Assert.IsNull(parser.Parse("ABC"));
    }

    [TestMethod]
    [DataRow("2", 2)]
    [DataRow("2 ^ 3", 8)]
    [DataRow("2 ^ 2 ^ 3", 256)]
    public void ShouldParseRightAssociativity(string expression, double result)
    {
        var primary = Terms.Number<double>(NumberOptions.Float);
        var exponent = Terms.Char('^');

        var exponentiation = primary.RightAssociative(
            (exponent, static (a, b) => System.Math.Pow(a, b))
            );

        Assert.AreEqual(result, exponentiation.Parse(expression));
    }


    [TestMethod]
    [DataRow("2", 2)]
    [DataRow("2 / 4", 0.5)]
    [DataRow("2 / 2 * 3", 3)]
    public void ShouldParseLeftAssociativity(string expression, double result)
    {
        var primary = Terms.Number<double>(NumberOptions.Float);

        var multiplicative = primary.LeftAssociative(
            (Terms.Char('*'), static (a, b) => a * b),
            (Terms.Char('/'), static (a, b) => a / b)
            );

        Assert.AreEqual(result, multiplicative.Parse(expression));
    }

    [TestMethod]
    [DataRow("2", 2)]
    [DataRow("-2", -2)]
    [DataRow("--2", 2)]
    public void ShouldParsePrefix(string expression, double result)
    {
        var primary = Terms.Number<double>(NumberOptions.Float);

        var unary = primary.Unary(
            (Terms.Char('-'), static (a) => 0 - a)
            );

        Assert.AreEqual(result, unary.Parse(expression));
    }

    [TestMethod]
    public void ShouldZeroOrOne()
    {
        var parser = ZeroOrOne(Terms.Text("hello"));

        Assert.AreEqual("hello", parser.Parse(" hello world hello"));
        Assert.IsNull(parser.Parse(" foo"));
    }

    [TestMethod]
    public void ShouldOneOrMany()
    {
        var parser = OneOrMany(Terms.Text("hello"));

        var result1 = parser.Parse(" hello hello world");
        Assert.HasCount(2, result1);
        Assert.AreEqual("hello", result1[0]);
        Assert.AreEqual("hello", result1[1]);

        Assert.IsFalse(parser.TryParse(" foo", out _));
    }

    [TestMethod]
    public void OptionalShouldSucceed()
    {
        var parser = Terms.Text("hello").Optional();

        Assert.AreEqual("hello", parser.Parse(" hello world hello").Value);
        Assert.IsNull(parser.Parse(" foo").Value);
    }

    [TestMethod]
    public void ZeroOrOneShouldNotBeSeekable()
    {
        var a = Literals.Char('a');
        var b = Literals.Char('b');
        var c = Literals.Char('c');

        var oneOf = OneOf(ZeroOrOne(a), b);

        // This should succeed, the ZeroOrOne(a) should always return true 
        Assert.IsTrue(oneOf.TryParse("c", out _));
    }

    [TestMethod]
    public void ZeroOrManyShouldNotBeSeekable()
    {
        var a = Literals.Char('a').AsChar();
        var b = Literals.Char('b').AsChar();
        var c = Literals.Char('c').AsChar();

        var oneOf = OneOf(ZeroOrMany(a).Then('a'), b);

        // This should succeed, the ZeroOrMany(a) should always return true 
        Assert.IsTrue(oneOf.TryParse("c", out _));
    }

    [TestMethod]
    public void ShouldZeroOrOneWithDefault()
    {
        var parser = ZeroOrOne(Terms.Text("hello"), "world");

        Assert.AreEqual("world", parser.Parse(" this is an apple"));
        Assert.AreEqual("hello", parser.Parse(" hello world"));
    }

    [TestMethod]
    public void NumberReturnsAnyType()
    {
        Assert.AreEqual((byte)123, Literals.Number<byte>().Parse("123"));
        Assert.AreEqual((sbyte)123, Literals.Number<sbyte>().Parse("123"));
        Assert.AreEqual((int)123, Literals.Number<int>().Parse("123"));
        Assert.AreEqual((uint)123, Literals.Number<uint>().Parse("123"));
        Assert.AreEqual((long)123, Literals.Number<long>().Parse("123"));
        Assert.AreEqual((ulong)123, Literals.Number<ulong>().Parse("123"));
        Assert.AreEqual((short)123, Literals.Number<short>().Parse("123"));
        Assert.AreEqual((ushort)123, Literals.Number<ushort>().Parse("123"));
        Assert.AreEqual((decimal)123, Literals.Number<decimal>().Parse("123"));
        Assert.AreEqual((double)123, Literals.Number<double>().Parse("123"));
        Assert.AreEqual((float)123, Literals.Number<float>().Parse("123"));
#if NET6_0_OR_GREATER
        Assert.AreEqual((Half)123, Literals.Number<Half>().Parse("123"));
#endif
        Assert.AreEqual((BigInteger)123, Literals.Number<BigInteger>().Parse("123"));
#if NET8_0_OR_GREATER
        Assert.AreEqual((nint)123, Literals.Number<nint>().Parse("123"));
        Assert.AreEqual((nuint)123, Literals.Number<nuint>().Parse("123"));
        Assert.AreEqual((Int128)123, Literals.Number<Int128>().Parse("123"));
        Assert.AreEqual((UInt128)123, Literals.Number<UInt128>().Parse("123"));
#endif
    }

    [TestMethod]
    public void NumberCanReadExponent()
    {
        var e = NumberOptions.AllowExponent;

        Assert.AreEqual((byte)120, Literals.Number<byte>(e).Parse("12e1"));
        Assert.AreEqual((sbyte)120, Literals.Number<sbyte>(e).Parse("12e1"));
        Assert.AreEqual((int)120, Literals.Number<int>(e).Parse("12e1"));
        Assert.AreEqual((uint)120, Literals.Number<uint>(e).Parse("12e1"));
        Assert.AreEqual((long)120, Literals.Number<long>(e).Parse("12e1"));
        Assert.AreEqual((ulong)120, Literals.Number<ulong>(e).Parse("12e1"));
        Assert.AreEqual((short)120, Literals.Number<short>(e).Parse("12e1"));
        Assert.AreEqual((ushort)120, Literals.Number<ushort>(e).Parse("12e1"));
        Assert.AreEqual((decimal)120, Literals.Number<decimal>(e).Parse("12e1"));
        Assert.AreEqual((double)120, Literals.Number<double>(e).Parse("12e1"));
        Assert.AreEqual((float)120, Literals.Number<float>(e).Parse("12e1"));
#if NET6_0_OR_GREATER
        Assert.AreEqual((Half)120, Literals.Number<Half>(e).Parse("12e1"));
#endif
        Assert.AreEqual((BigInteger)120, Literals.Number<BigInteger>(e).Parse("12e1"));
#if NET8_0_OR_GREATER
        Assert.AreEqual((nint)120, Literals.Number<nint>(e).Parse("12e1"));
        Assert.AreEqual((nuint)120, Literals.Number<nuint>(e).Parse("12e1"));
        Assert.AreEqual((Int128)120, Literals.Number<Int128>(e).Parse("12e1"));
        Assert.AreEqual((UInt128)120, Literals.Number<UInt128>(e).Parse("12e1"));
#endif
    }

    [TestMethod]
    [DataRow(1, "1")]
    [DataRow(1, "+1")]
    [DataRow(-1, "-1")]
    [DataRow(1, "1.0")]
    [DataRow(1, "1.00")]
    [DataRow(0.1, ".1")]
    [DataRow(1.1, "1.1")]
    [DataRow(1.123, "1.123")]
    [DataRow(1.123, "+1.123")]
    [DataRow(-1.123, "-1.123")]
    [DataRow(1123, "1,123")]
    [DataRow(1123, "1,1,,2,3")]
    [DataRow(1123, "+1,123")]
    [DataRow(-1123, "-1,1,,2,3")]
    [DataRow(1123.123, "1,123.123")]
    [DataRow(1123.123, "1,1,,2,3.123")]
    [DataRow(10, "1e1")]
    [DataRow(11, "1.1e1")]
    [DataRow(1, ".1e1")]
    [DataRow(10, "1e+1")]
    [DataRow(11, "1.1e+1")]
    [DataRow(1, ".1e+1")]
    [DataRow(0.1, "1e-1")]
    [DataRow(0.11, "1.1e-1")]
    [DataRow(0.01, ".1e-1")]
    public void NumberParsesAllNumbers(double expected, string source)
    {
        Assert.AreEqual((decimal)expected, Literals.Number<decimal>(NumberOptions.Any).Parse(source));
    }

    [TestMethod]
    public void NumberParsesCustomDecimalSeparator()
    {
        Assert.AreEqual((decimal)123.456, Literals.Number<decimal>(NumberOptions.Any, decimalSeparator: '|').Parse("123|456"));
    }

    [TestMethod]
    public void NumberParsesCustomGroupSeparator()
    {
        Assert.AreEqual((decimal)123456, Literals.Number<decimal>(NumberOptions.Any, '.', groupSeparator: '|').Parse("123|456"));
    }

    [TestMethod]
    [DataRow("-1")]
    [DataRow("256")]
    public void NumberShouldNotParseOverflow(string source)
    {
        Assert.IsFalse(Literals.Number<byte>().TryParse(source, out var _));
    }

    [TestMethod]
    [DataRow("a", "a", "a")]
    [DataRow("a", "aa", "aa")]
    [DataRow("a", "aaaa", "aaaa")]
    [DataRow("ab", "ab", "ab")]
    [DataRow("ba", "ab", "ab")]
    [DataRow("abc", "aaabbbccc", "aaabbbccc")]
    [DataRow("a", "aaab", "aaa")]
    [DataRow("aa", "aaaaab", "aaaaa")]
    public void AnyOfShouldMatch(string chars, string source, string expected)
    {
        Assert.AreEqual(expected, Literals.AnyOf(chars).AsString().Parse(source));
    }

    [TestMethod]
    [DataRow("a", "b")]
    [DataRow("a", "bbb")]
    [DataRow("abc", "dabc")]
    public void AnyOfShouldNotMatch(string chars, string source)
    {
        Assert.IsFalse(Literals.AnyOf(chars).TryParse(source, out var _));
    }

    [TestMethod]
    public void AnyOfShouldRespectSizeConstraints()
    {
        Assert.IsTrue(Literals.AnyOf("a", minSize: 0).AsString().TryParse("aaa", out var r) && r == "aaa");
        Assert.IsTrue(Literals.AnyOf("a", minSize: 0).AsString().TryParse("bbb", out _));
        Assert.IsFalse(Literals.AnyOf("a", minSize: 4).AsString().TryParse("aaa", out _));
        Assert.IsFalse(Literals.AnyOf("a", minSize: 2).AsString().TryParse("ab", out _));
        Assert.IsFalse(Literals.AnyOf("a", minSize: 3).AsString().TryParse("ab", out _));
        Assert.AreEqual("aa", Literals.AnyOf("a", minSize: 2, maxSize: 2).AsString().Parse("aa"));
        Assert.AreEqual("aa", Literals.AnyOf("a", minSize: 2, maxSize: 3).AsString().Parse("aa"));
        Assert.AreEqual("a", Literals.AnyOf("a", maxSize: 1).AsString().Parse("aa"));
        Assert.AreEqual("aaaa", Literals.AnyOf("a", minSize: 2, maxSize: 4).AsString().Parse("aaaaaa"));
        Assert.IsFalse(Literals.AnyOf("a", minSize: 2, maxSize: 2).TryParse("a", out _));
    }

    [TestMethod]
    public void AnyOfShouldResetPositionWhenFalse()
    {
        Assert.IsFalse(Literals.AnyOf("a", minSize: 3)
            .And(Literals.AnyOf("Z"))
            .TryParse("aaZZ", out _));

        Assert.IsTrue(Literals.AnyOf("a", minSize: 3)
             .And(Literals.AnyOf("Z"))
             .TryParse("aaaZZ", out _));
    }

    [TestMethod]
    [DataRow("a", "b", "b")]
    [DataRow("a", "bb", "bb")]
    [DataRow("a", "bbbb", "bbbb")]
    [DataRow("ab", "cd", "cd")]
    [DataRow("ba", "cd", "cd")]
    [DataRow("abc", "dddeeefff", "dddeeefff")]
    [DataRow("a", "bbba", "bbb")]
    [DataRow("aa", "bbbbba", "bbbbb")]
    public void NoneOfShouldMatch(string chars, string source, string expected)
    {
        Assert.AreEqual(expected, Literals.NoneOf(chars).AsString().Parse(source));
    }

    [TestMethod]
    [DataRow("a", "a")]
    [DataRow("a", "aaa")]
    [DataRow("abc", "beee")]
    public void NoneOfShouldNotMatch(string chars, string source)
    {
        Assert.IsFalse(Literals.NoneOf(chars).TryParse(source, out var _));
    }

    [TestMethod]
    public void NoneOfShouldRespectSizeConstraints()
    {
        Assert.IsTrue(Literals.NoneOf("a", minSize: 0).AsString().TryParse("bbb", out var r) && r == "bbb");
        Assert.IsTrue(Literals.NoneOf("a", minSize: 0).TryParse("aaa", out _));
        Assert.IsFalse(Literals.NoneOf("a", minSize: 4).TryParse("bbb", out _));
        Assert.IsFalse(Literals.NoneOf("a", minSize: 2).TryParse("ba", out _));
        Assert.IsFalse(Literals.NoneOf("a", minSize: 3).TryParse("ba", out _));
        Assert.AreEqual("bb", Literals.NoneOf("a", minSize: 2, maxSize: 2).AsString().Parse("bb"));
        Assert.AreEqual("bb", Literals.NoneOf("a", minSize: 2, maxSize: 3).AsString().Parse("bb"));
        Assert.AreEqual("b", Literals.NoneOf("a", maxSize: 1).AsString().Parse("bb"));
        Assert.AreEqual("bbbb", Literals.NoneOf("a", minSize: 2, maxSize: 4).AsString().Parse("bbbbb"));
        Assert.IsFalse(Literals.NoneOf("a", minSize: 2, maxSize: 2).TryParse("b", out _));
    }

    [TestMethod]
    public void NoneOfShouldResetPositionWhenFalse()
    {
        Assert.IsFalse(Literals.NoneOf("Z", minSize: 3)
            .And(Literals.NoneOf("a"))
            .TryParse("aaZZ", out _));

        Assert.IsTrue(Literals.NoneOf("Z", minSize: 3)
             .And(Literals.NoneOf("a"))
             .TryParse("aaaZZ", out _));
    }


    [TestMethod]
    public void ElseErrorShouldNotBeSeekable()
    {
        Parser<char> a = Terms.Char('a').AsChar();
        Parser<char> b = Terms.Char('b').AsChar();
        Parser<object> c = a.Then<object>();

        // Use two parsers to ensure OneOf tries to build a lookup table
        var parser = OneOf(a.ElseError("Error"), b);

        Assert.IsTrue(parser.TryParse("a", out _));
        Assert.Throws<ParseException>(() => parser.Parse("b"));
    }

    [TestMethod]
    public void WithWhiteSpaceParserShouldUseCustomWhiteSpace()
    {
        // Example from the issue: using dots as whitespace
        var hello = Terms.Text("hello");
        var world = Terms.Text("world");
        var parser = hello.And(world).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('.'))));

        // Should succeed with dots as whitespace
        Assert.IsTrue(parser.TryParse("..hello.world", out var result));
        Assert.AreEqual("hello", result.Item1.ToString());
        Assert.AreEqual("world", result.Item2.ToString());

        // Should succeed with multiple dots
        Assert.IsTrue(parser.TryParse("...hello...world", out var result2));
        Assert.AreEqual("hello", result2.Item1.ToString());
        Assert.AreEqual("world", result2.Item2.ToString());
    }

    [TestMethod]
    public void WithWhiteSpaceParserShouldNotSkipRegularWhiteSpace()
    {
        // When using custom whitespace parser, regular spaces should NOT be skipped
        var hello = Terms.Text("hello");
        var world = Terms.Text("world");
        var parser = hello.And(world).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('.'))));

        // Should fail with regular whitespace
        Assert.IsFalse(parser.TryParse("hello world", out _));
    }

    [TestMethod]
    public void WithWhiteSpaceParserShouldRestoreOriginalParser()
    {
        // After the WithWhiteSpaceParser parser completes, the original whitespace parser should be restored
        var hello = Terms.Text("hello");
        var world = Terms.Text("world");
        var inner = hello.And(world).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('.'))));
        var outer = Terms.Text("outer");
        var parser = inner.And(outer);

        // Inside WithWhiteSpaceParser, dots are whitespace
        // Outside, regular whitespace should work
        Assert.IsTrue(parser.TryParse("..hello.world outer", out var result));
        Assert.AreEqual("hello", result.Item1);
        Assert.AreEqual("world", result.Item2);
        Assert.AreEqual("outer", result.Item3);
    }

    [TestMethod]
    public void WithWhiteSpaceParserShouldWorkWithNestedParsers()
    {
        // Test nested WithWhiteSpaceParser calls
        var a = Terms.Text("a");
        var b = Terms.Text("b");
        var c = Terms.Text("c");

        var innerParser = a.And(b).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('.'))));
        var outerParser = innerParser.And(c).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('-'))));

        // First test a simpler case
        Assert.IsTrue(innerParser.TryParse("a.b", out var inner1));
        Assert.AreEqual("a", inner1.Item1.ToString());
        Assert.AreEqual("b", inner1.Item2.ToString());

        // Test outer without nesting first
        var simpleOuter = Terms.Text("ab").And(c).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('-'))));
        Assert.IsTrue(simpleOuter.TryParse("-ab-c", out var simple1));
        Assert.AreEqual("ab", simple1.Item1.ToString());
        Assert.AreEqual("c", simple1.Item2.ToString());

        // For the nested case:
        // - innerParser uses "." as whitespace for parsing "a.b"
        // - outerParser uses "-" as whitespace for parsing innerParser and c
        // - But innerParser itself doesn't skip whitespace (it's not a Terms parser)
        // - So the input should be "a.b-c" (no leading "-")
        //   The innerParser will parse "a.b", then c will skip "-" and parse "c"
        Assert.IsTrue(outerParser.TryParse("a.b-c", out var result));
        Assert.AreEqual("a", result.Item1);
        Assert.AreEqual("b", result.Item2);
        Assert.AreEqual("c", result.Item3);
    }

    [TestMethod]
    public void WithWhiteSpaceParserShouldWorkWithZeroOrMany()
    {
        // Test that custom whitespace works with ZeroOrMany combinator
        var word = Terms.Identifier();
        var parser = ZeroOrMany(word).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char(','))));

        Assert.IsTrue(parser.TryParse(",hello,world,foo", out var result));
        Assert.HasCount(3, result);
        Assert.AreEqual("hello", result[0].ToString());
        Assert.AreEqual("world", result[1].ToString());
        Assert.AreEqual("foo", result[2].ToString());
    }

    [TestMethod]
    public void WithWhiteSpaceParserShouldAllowEmptyWhiteSpace()
    {
        // Test that we can parse without any whitespace when custom parser doesn't match
        var hello = Terms.Text("hello");
        var world = Terms.Text("world");
        var parser = hello.And(world).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('.'))));

        // Should succeed with no whitespace between tokens
        Assert.IsTrue(parser.TryParse("helloworld", out var result));
        Assert.AreEqual("hello", result.Item1.ToString());
        Assert.AreEqual("world", result.Item2.ToString());
    }

    [TestMethod]
    public void WithWhiteSpaceParserShouldWorkWithMultipleCharWhiteSpace()
    {
        // Test using a multi-character whitespace parser
        var hello = Terms.Text("hello");
        var world = Terms.Text("world");
        var parser = hello.And(world).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('.'))));

        // Should succeed with multiple dots
        Assert.IsTrue(parser.TryParse("...hello....world", out var result));
        Assert.AreEqual("hello", result.Item1.ToString());
        Assert.AreEqual("world", result.Item2.ToString());
    }

    [TestMethod]
    public void DeferredShouldDetectInfiniteRecursion()
    {
        // Test case 1: Direct self-reference
        var loop = Deferred<string>();
        loop.Parser = loop;

        // Should fail gracefully instead of causing stack overflow
        Assert.IsFalse(loop.TryParse("hello parlot", out var result1));
        Assert.IsNull(result1);
    }

    [TestMethod]
    public void RecursiveShouldDetectInfiniteRecursion()
    {
        // Test case 2: Recursive self-reference
        var loop = Recursive<string>(c => c);

        // Should fail gracefully instead of causing stack overflow
        Assert.IsFalse(loop.TryParse("hello parlot", out var result2));
        Assert.IsNull(result2);
    }

    [TestMethod]
    public void DeferredShouldAllowValidRecursion()
    {
        // Valid recursive parser - should still work
        // This represents a simple recursive grammar like: list ::= '[' (item (',' item)*)? ']'
        var list = Deferred<string>();
        var item = Literals.Text("item");
        var comma = Literals.Char(',');
        var openBracket = Literals.Char('[');
        var closeBracket = Literals.Char(']');

        // A list can contain items or nested lists
        var element = item.Or(list);
        var elements = ZeroOrMany(element.And(ZeroOrOne(comma)));
        list.Parser = Between(openBracket, elements, closeBracket).Then(x => "list");

        // This should work fine - it's recursive but makes progress
        Assert.IsTrue(list.TryParse("[]", out var result));
        Assert.AreEqual("list", result);
    }

    //[Fact]
    //public void DisableLoopDetectionShouldAllowInfiniteRecursion()
    //{
    //    // When DisableLoopDetection is true, the parser should not detect infinite loops
    //    var loop = Deferred<string>();
    //    loop.Parser = loop;

    //    // Test with loop detection enabled (default)
    //    var contextWithDetection = new ParseContext(new Scanner("test"), disableLoopDetection: false);
    //    Assert.IsFalse(loop.TryParse(contextWithDetection, out var _, out var _));

    //    // We can't safely test the DisableLoopDetection = true case to completion without stack overflow,
    //    // but the implementation is verified by the fact that the flag is properly checked in the code
    //}

    [TestMethod]
    public void WhiteSpaceParserShouldUseParseContextParser()
    {
        // Test that the whitespace parser respects the ParseContext settings
        var hello = Literals.Text("hello");
        var world = Literals.Text("world");
        var parser = Terms.WhiteSpace().AsString().And(hello).And(Terms.WhiteSpace().AsString()).And(world).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('.'))));

        // Should use the custom whitespace parser from the context
        Assert.IsTrue(parser.TryParse("..hello....world", out var result, out var _));
        Assert.AreEqual("..", result.Item1);
        Assert.AreEqual("hello", result.Item2);
        Assert.AreEqual("....", result.Item3);
        Assert.AreEqual("world", result.Item4);
    }

    [TestMethod]
    public void WithWhiteSpaceParserDoesntRepeat()
    {
        // Test that the whitespace parser can fail and propagate the failure
        var hello = Literals.Text("hello");
        var world = Terms.Text("world");
        var ws = Capture(Literals.Text("!!!")); // This whitespace parser only matches "!!!"
        var parser = hello.And(world).WithWhiteSpaceParser(ws);

        Assert.IsTrue(parser.TryParse("hello!!!world", out var _, out var _));
        Assert.IsFalse(parser.TryParse("hello world", out var _, out var _));
        Assert.IsFalse(parser.TryParse("hello!!! world", out var _, out var _));
        Assert.IsFalse(parser.TryParse("hello!!!!!!world", out var _, out var _));
    }

    [TestMethod]
    public void WithWhiteSpaceParserRepeatingPattern()
    {
        // Test that the whitespace parser can fail and propagate the failure
        var hello = Literals.Text("hello");
        var world = Terms.Text("world");
        var ws = Capture(Literals.Text("!!!").OneOrMany());
        var parser = hello.And(world).WithWhiteSpaceParser(ws);

        Assert.IsTrue(parser.TryParse("hello!!!world", out var _, out var _));
        Assert.IsFalse(parser.TryParse("hello world", out var _, out var _));
        Assert.IsFalse(parser.TryParse("hello!!! world", out var _, out var _));
        Assert.IsTrue(parser.TryParse("hello!!!!!!world", out var _, out var _));
        Assert.IsFalse(parser.TryParse("hello!!!!!world", out var _, out var _));
    }

    [TestMethod]
    public void LiteralsKeywordShouldNotMatchIfFollowedByLetter()
    {
        var parser = Literals.Keyword("if");

        Assert.IsTrue(parser.TryParse("if", out var result));
        Assert.AreEqual("if", result);

        Assert.IsTrue(parser.TryParse("if ", out result));
        Assert.AreEqual("if", result);

        Assert.IsTrue(parser.TryParse("if(", out result));
        Assert.AreEqual("if", result);

        Assert.IsFalse(parser.TryParse("ifoo", out result));
        Assert.IsFalse(parser.TryParse("ifA", out result));
        Assert.IsFalse(parser.TryParse("ifZ", out result));
    }

    [TestMethod]
    public void TermsKeywordShouldNotMatchIfFollowedByLetter()
    {
        var parser = Terms.Keyword("if");

        Assert.IsTrue(parser.TryParse("if", out var result));
        Assert.AreEqual("if", result);

        Assert.IsTrue(parser.TryParse(" if", out result));
        Assert.AreEqual("if", result);

        Assert.IsTrue(parser.TryParse(" if ", out result));
        Assert.AreEqual("if", result);

        Assert.IsTrue(parser.TryParse(" if(", out result));
        Assert.AreEqual("if", result);

        Assert.IsFalse(parser.TryParse("ifoo", out result));
        Assert.IsFalse(parser.TryParse(" ifA", out result));
        Assert.IsFalse(parser.TryParse(" ifZ", out result));
    }

    [TestMethod]
    public void KeywordShouldWorkInIfElseSequence()
    {
        var parser = Terms.Keyword("if")
            .SkipAnd(Terms.Char('('))
            .And(Terms.Identifier())
            .AndSkip(Terms.Char(')'));

        Assert.IsTrue(parser.TryParse("if(foo)", out var result));
        Assert.AreEqual("foo", result.Item2.ToString());

        Assert.IsFalse(parser.TryParse("ifoo(bar)", out result));
        Assert.IsFalse(parser.TryParse("iff(bar)", out result));
    }

    [TestMethod]
    public void KeywordShouldBeCaseInsensitiveWhenRequested()
    {
        var parser = Literals.Keyword("if", caseInsensitive: true);

        Assert.IsTrue(parser.TryParse("if", out var result));
        Assert.AreEqual("if", result);

        Assert.IsTrue(parser.TryParse("IF", out result));
        Assert.AreEqual("IF", result);

        Assert.IsTrue(parser.TryParse("If", out result));
        Assert.AreEqual("If", result);

        Assert.IsFalse(parser.TryParse("ifoo", out result));
        Assert.IsFalse(parser.TryParse("IFoo", out result));
    }

    [TestMethod]
    public void KeywordShouldMatchNonLetterCharactersAfter()
    {
        var parser = Terms.Keyword("return");

        Assert.IsTrue(parser.TryParse("return;", out var result));
        Assert.AreEqual("return", result);

        Assert.IsTrue(parser.TryParse("return\n", out result));
        Assert.AreEqual("return", result);

        Assert.IsTrue(parser.TryParse("return123", out result));
        Assert.AreEqual("return", result);

        Assert.IsTrue(parser.TryParse("return_", out result));
        Assert.AreEqual("return", result);

        Assert.IsFalse(parser.TryParse("returns", out result));
    }
}
#pragma warning restore IDE0059 // Unnecessary assignment of a value
