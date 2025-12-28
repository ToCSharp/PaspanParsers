using Paspan;

namespace PaspanParsers.Calc
{
    /*
     * Grammar:
     * expression     => factor ( ( "-" | "+" ) factor )* ;
     * factor         => unary ( ( "/" | "*" ) unary )* ;
     * unary          => ( "-" ) unary
     *                 | primary ;
     * primary        => NUMBER
     *                  | "(" expression ")" ;
    */

    /// <summary>
    /// This version of the Parser creates and intermediate AST.
    /// </summary>
    public ref struct Parser
    {
        private SpanReader _reader;

        public Expression Parse(string text)
        {
            _reader = new(text);

            return ParseExpression();
        }

        private Expression ParseExpression()
        {
            var expression = ParseFactor();

            while (true)
            {
                _reader.SkipWhiteSpace();

                if (_reader.Skip((byte)'+'))
                {
                    _reader.SkipWhiteSpace();

                    expression = new Addition(expression, ParseFactor());
                }
                else if (_reader.Skip((byte)'-'))
                {
                    _reader.SkipWhiteSpace();

                    expression = new Subtraction(expression, ParseFactor());
                }
                else
                {
                    break;
                }
            }

            return expression;
        }

        private Expression ParseFactor()
        {
            var expression = ParseUnaryExpression();

            while (true)
            {
                _reader.SkipWhiteSpace();

                if (_reader.Skip((byte)'*'))
                {
                    _reader.SkipWhiteSpace();

                    expression = new Multiplication(expression, ParseUnaryExpression());
                }
                else if (_reader.Skip((byte)'/'))
                {
                    _reader.SkipWhiteSpace();

                    expression = new Division(expression, ParseUnaryExpression());
                }
                else
                {
                    break;
                }
            }

            return expression;
        }

        /*
         unary =    ( "-" ) unary
                    | primary ;
        */

        private Expression ParseUnaryExpression()
        {
            _reader.SkipWhiteSpace();

            if (_reader.Skip((byte)'-'))
            {
                var inner = ParseUnaryExpression();

                return inner == null ? throw new ParseException("Expected expression after '-'"/*, Position*/) : (Expression)new NegateExpression(inner);
            }

            return ParsePrimaryExpression();
        }

        /*
          primary = NUMBER
                    | "(" expression ")" ;
        */

        private Expression ParsePrimaryExpression()
        {
            _reader.SkipWhiteSpace();

            if (_reader.ConsumeDecimalDigits() && _reader.TryGetDecimal(out var number))
            {
                return new Number(number);
            }

            if (_reader.Skip((byte)'('))
            {
                var expression = ParseExpression();

                if (!_reader.Skip((byte)')'))
                {
                    throw new ParseException("Expected ')'"/*, , Position*/);
                }

                return expression;
            }

            throw new ParseException("Expected primary expression"/*, , Position*/);
        }
    }
}
