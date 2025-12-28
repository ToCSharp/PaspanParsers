using Paspan;

namespace PaspanParsers.Tests
{
    [TestClass]
    public class SpanReaderTests
    {
        [TestMethod]
        [DataRow("Lorem ipsum")]
        [DataRow("'Lorem ipsum")]
        [DataRow("Lorem ipsum'")]
        [DataRow("\"Lorem ipsum")]
        [DataRow("Lorem ipsum\"")]
        [DataRow("'Lorem ipsum\"")]
        [DataRow("\"Lorem ipsum'")]
        public void ShouldNotReadEscapedStringWithoutMatchingQuotes(string text)
        {
            SpanReader s = new(text);
            Assert.IsFalse(s.ReadQuotedString());
        }

        [TestMethod]
        [DataRow("'Lorem ipsum'", "Lorem ipsum")]
        [DataRow("\"Lorem ipsum\"", "Lorem ipsum")]
        public void ShouldReadEscapedStringWithMatchingQuotes(string text, string expected)
        {
            SpanReader s = new(text);
            var success = s.ReadQuotedString();
            Assert.IsTrue(success);
            Assert.AreEqual(expected, s.GetString());
        }

        [TestMethod]
        [DataRow("'Lorem \\n ipsum'", "Lorem \\n ipsum")]
        [DataRow("\"Lorem \\n ipsum\"", "Lorem \\n ipsum")]
        [DataRow("\"Lo\\trem \\n ipsum\"", "Lo\\trem \\n ipsum")]
        [DataRow("'Lorem \\u1234 ipsum'", "Lorem \\u1234 ipsum")]
        [DataRow("'Lorem \\xabcd ipsum'", "Lorem \\xabcd ipsum")]
        public void ShouldReadStringWithEscapes(string text, string expected)
        {
            SpanReader s = new(text);
            var success = s.ReadQuotedString();
            Assert.IsTrue(success);
            Assert.AreEqual(expected, s.GetString());
        }

        [TestMethod]
        [DataRow("'Lorem \\w ipsum'")]
        [DataRow("'Lorem \\u12 ipsum'")]
        [DataRow("'Lorem \\xg ipsum'")]
        public void ShouldNotReadStringWithInvalidEscapes(string text)
        {
            SpanReader s = new(text);
            Assert.IsFalse(s.ReadQuotedString());
        }

        [TestMethod]
        public void ReadSingleQuotedStringShouldReadSingleQuotedStrings()
        {
            SpanReader s = new("'abcd'");
            s.ReadSingleQuotedString();
            Assert.AreEqual("abcd", s.GetString());

            s = new("'a\\nb'");
            s.ReadSingleQuotedString();
            Assert.AreEqual("a\\nb", s.GetString());

            Assert.IsFalse(new SpanReader("'abcd").ReadSingleQuotedString());
            Assert.IsFalse(new SpanReader("abcd'").ReadSingleQuotedString());
            Assert.IsFalse(new SpanReader("ab\\'cd").ReadSingleQuotedString());
        }

        [TestMethod]
        public void ReadDoubleQuotedStringShouldReadDoubleQuotedStrings()
        {
            SpanReader s = new("\"abcd\"");
            s.ReadDoubleQuotedString();
            Assert.AreEqual("abcd", s.GetString());

            s = new("\"a\\nb\"");
            s.ReadDoubleQuotedString();
            Assert.AreEqual("a\\nb", s.GetString());

            Assert.IsFalse(new SpanReader("\"abcd").ReadDoubleQuotedString());
            Assert.IsFalse(new SpanReader("abcd\"").ReadDoubleQuotedString());
            Assert.IsFalse(new SpanReader("\"ab\\\"cd").ReadDoubleQuotedString());
        }

        [TestMethod]
        [DataRow("1", "1")]
        [DataRow("123", "123")]
        [DataRow("123a", "123")]
        [DataRow("123.0", "123.0")]
        [DataRow("123.0a", "123.0")]
        [DataRow("123.01", "123.01")]
        public void ShouldReadValidDecimal(string text, string expected)
        {
            SpanReader s = new(text);
            Assert.IsTrue(s.ConsumeDecimalDigits());
            Assert.AreEqual(expected, s.GetString());
        }

        //[Theory]
        //[InlineData(" 1")]
        //[InlineData("123.")]
        //public void ShouldNotReadInvalidDecimal(string text)
        //{
        //    //Assert.IsFalse(new Scanner(text).ReadDecimal());
        //}

        [TestMethod]
        [DataRow("'a\nb' ", "a\nb")]
        [DataRow("'a\r\nb' ", "a\r\nb")]
        public void ShouldReadStringsWithLineBreaks(string text, string expected)
        {
            SpanReader s = new(text);
            Assert.IsTrue(s.ReadSingleQuotedString());
            Assert.AreEqual(expected, s.GetString());
        }

        [TestMethod]
        [DataRow("'a\\bc'", "a\\bc")]
        [DataRow("'\\xa0'", "\\xa0")]
        [DataRow("'\\xfh'", "\\xfh")]
        [DataRow("'\\u1234'", "\\u1234")]
        [DataRow("' a\\bc ' ", " a\\bc ")]
        [DataRow("' \\xa0 ' ", " \\xa0 ")]
        [DataRow("' \\xfh ' ", " \\xfh ")]
        [DataRow("' \\u1234 ' ", " \\u1234 ")]

        public void ShouldReadUnicodeSequence(string text, string expected)
        {
            SpanReader s = new(text);
            s.ReadQuotedString();
            Assert.AreEqual(expected, s.GetString());
        }
    }
}
