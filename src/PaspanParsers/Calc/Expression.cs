namespace PaspanParsers.Calc
{
    public abstract class Expression
    {
        public abstract decimal Evaluate();
    }

    public abstract class BinaryExpression(Expression left, Expression right) : Expression
    {
        public Expression Left { get; } = left;
        public Expression Right { get; } = right;

    }

    public abstract class UnaryExpression(Expression inner) : Expression
    {
        public Expression Inner { get; } = inner;
    }

    public class NegateExpression(Expression inner) : UnaryExpression(inner)
    {
        public override decimal Evaluate()
        {
            return -1 * Inner.Evaluate();
        }
    }


    public class Addition(Expression left, Expression right) : BinaryExpression(left, right)
    {
        public override decimal Evaluate()
        {
            return Left.Evaluate() + Right.Evaluate();
        }
    }

    public class Subtraction(Expression left, Expression right) : BinaryExpression(left, right)
    {
        public override decimal Evaluate()
        {
            return Left.Evaluate() - Right.Evaluate();
        }
    }


    public class Multiplication(Expression left, Expression right) : BinaryExpression(left, right)
    {
        public override decimal Evaluate()
        {
            return Left.Evaluate() * Right.Evaluate();
        }
    }


    public class Division(Expression left, Expression right) : BinaryExpression(left, right)
    {
        public override decimal Evaluate()
        {
            return Left.Evaluate() / Right.Evaluate();
        }
    }

    public class Number(decimal value) : Expression
    {
        public decimal Value { get; } = value;

        public override decimal Evaluate()
        {
            return Value;
        }
    }
}
