namespace PaspanParsers.Tests.Calc
{
    [TestClass]
    public abstract class CalcTests
    {
        protected abstract decimal Evaluate(string text);

        [TestMethod]
        [DataRow("123", 123)]
        [DataRow("0", 0)]
        public void TestNumber(string text, int value)
        {
            var result = Evaluate(text);

            Assert.AreEqual(value, result);
        }

        [TestMethod]
        [DataRow("123.0", 123.0)]
        [DataRow("123.1", 123.1)]
        [DataRow("123.456789", 123.456789)]
        public void TestDecimalNumber(string text, double value)
        {
            var result = Evaluate(text);

            Assert.AreEqual((decimal)value, result);
        }

        [TestMethod]
        [DataRow("123 + 123", 246)]
        [DataRow("123 + 123 + 123", 369)]
        public void TestAddition(string text, int value)
        {
            var result = Evaluate(text);

            Assert.AreEqual(value, result);
        }

        [TestMethod]
        [DataRow("123 - 123", 0)]
        public void TestSubtraction(string text, int value)
        {
            var result = Evaluate(text);

            Assert.AreEqual(value, result);
        }

        [TestMethod]
        [DataRow("123 * 2", 246)]
        public void TestMultiplication(string text, int value)
        {
            var result = Evaluate(text);

            Assert.AreEqual(value, result);
        }

        [TestMethod]
        [DataRow("123 / 123", 1)]
        public void TestDivision(string text, int value)
        {
            var result = Evaluate(text);

            Assert.AreEqual(value, result);
        }

        [TestMethod]
        [DataRow("3 + (1 - 2)", 2)]
        [DataRow("(3 + 1) * 2", 8)]
        [DataRow("( (3 + 1) * 2 ) + 1", 9)]
        public void TestGroup(string text, int value)
        {
            var result = Evaluate(text);

            Assert.AreEqual(value, result);
        }

        [TestMethod]
        [DataRow("3 + 1 * 2", 5)]
        public void TestPrecedence(string text, int value)
        {
            var result = Evaluate(text);

            Assert.AreEqual(value, result);
        }

        [TestMethod]
        [DataRow("-2", -2)]
        [DataRow("-(1+2)", -3)]
        [DataRow("--(1+2)", 3)]
        public void TestUnary(string text, int value)
        {
            var result = Evaluate(text);

            Assert.AreEqual(value, result);
        }
    }
}
