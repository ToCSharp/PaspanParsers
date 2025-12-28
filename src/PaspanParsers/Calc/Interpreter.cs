using Paspan;
using static Paspan.Common.Constants;

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
    /// This version of the Interpreter evaluates the value while it parses the expression.
    /// </summary>
    public ref struct Interpreter
    {
        private SpanReader _reader;

        public decimal Evaluate(string text)
        {
            _reader = new SpanReader(text);

            return ParseExpression();
        }

        private decimal ParseExpression()
        {
            var value = ParseFactor();

            _reader.SkipWhiteSpace();

            while (true)
            {
                if (_reader.Skip(Plus))
                {
                    _reader.SkipWhiteSpace();

                    value += ParseFactor();
                    
                    _reader.SkipWhiteSpace();
                }
                else if (_reader.Skip(Minus))
                {
                    _reader.SkipWhiteSpace();

                    value -= ParseFactor();
                    
                    _reader.SkipWhiteSpace();
                }
                else
                {
                    break;
                }
            }

            return value;
        }

        private decimal ParseFactor()
        {
            var value = ParseUnaryExpression();

            _reader.SkipWhiteSpace();

            while (true)
            {
                if (_reader.Skip(Asterisk))
                {
                    _reader.SkipWhiteSpace();

                    value *= ParseUnaryExpression();
                    
                    _reader.SkipWhiteSpace();
                }
                else if (_reader.Skip(Slash))
                {
                    _reader.SkipWhiteSpace();

                    value /= ParseUnaryExpression();
                    
                    _reader.SkipWhiteSpace();
                }
                else
                {
                    break;
                }
            }

            return value;
        }

        /*
         unary =    ( "-" ) unary
                    | primary ;
        */

        private decimal ParseUnaryExpression()
        {
            _reader.SkipWhiteSpace();

            if (_reader.Skip(Minus))
            {
                return -1 * ParseUnaryExpression();
            }

            return ParsePrimaryExpression();
        }

        /*
          primary = NUMBER
                    | "(" expression ")" ;
        */

        private decimal ParsePrimaryExpression()
        {
            _reader.SkipWhiteSpace();

            if (_reader.ConsumeDecimalDigits() && _reader.TryGetDecimal(out var number))
            {
                return number;
            }

            if (_reader.Skip((byte)'('))
            {
                var value = ParseExpression();

                if (!_reader.Skip((byte)')'))
                {
                    throw new ParseException("Expected ')'"/*, Position*/);
                }

                return value;
            }

            throw new ParseException("Expected primary expression"/*, Position*/);
        }
    }
}
