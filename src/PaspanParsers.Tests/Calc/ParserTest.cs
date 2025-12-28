using PaspanParsers.Calc;

namespace PaspanParsers.Tests.Calc
{
    [TestClass]
    public class ParserTests : CalcTests
    {
        protected override decimal Evaluate(string text)
        {
            return new Parser().Parse(text).Evaluate();
        }
    }
}
