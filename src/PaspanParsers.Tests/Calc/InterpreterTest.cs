using PaspanParsers.Calc;

namespace PaspanParsers.Tests.Calc
{
    [TestClass]
    public class InterpreterTests : CalcTests
    {
        protected override decimal Evaluate(string text)
        {
            return new Interpreter().Evaluate(text);
        }
    }
}
