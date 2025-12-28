using PaspanParsers.Calc;

namespace PaspanParsers.Tests.Calc
{
    [TestClass]
    public class FluentParserTests : CalcTests
    {
        protected override decimal Evaluate(string text)
        {
            _ = FluentParser.Expression.TryParse(text, out var expression);
            return expression.Evaluate();
        }
    }
}
