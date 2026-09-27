namespace PaspanParsers.Cpp;

// Expressions ([expr]): a precedence climbing parser over the binary operators.
internal ref partial struct SyntaxParser
{
    // Binary operator precedences, from the comma operator to the pointer-to-member operators
    private const int CommaPrecedence = 1;
    private const int AssignmentPrecedence = 2;
    private const int LogicalOrPrecedence = 4;

    /// <summary>
    /// expression: assignment expressions separated by the comma operator.
    /// </summary>
    private Expression ParseExpression()
    {
        var left = ParseAssignmentExpression();
        while (left != null && IsPunctuator(","))
        {
            EatToken();
            var right = ParseAssignmentExpression();
            if (right == null)
            {
                return null;
            }

            left = Finish(new BinaryExpression(left, ",", right), left);
        }

        return left;
    }

    /// <summary>
    /// assignment-expression: a conditional expression, or an assignment (right-associative).
    /// </summary>
    private Expression ParseAssignmentExpression()
    {
        EnsureSufficientStack();

        var left = ParseBinaryExpression(LogicalOrPrecedence);
        if (left == null)
        {
            return null;
        }

        if (TryEatPunctuator("?"))
        {
            var whenTrue = ParseExpression();
            if (whenTrue == null || !TryEatPunctuator(":"))
            {
                return null;
            }

            var whenFalse = ParseAssignmentExpression();
            return whenFalse == null ? null : Finish(new ConditionalExpression(left, whenTrue, whenFalse), left);
        }

        var (@operator, precedence, tokens) = PeekBinaryOperator();
        if (precedence == AssignmentPrecedence)
        {
            EatTokens(tokens);
            var right = ParseAssignmentExpression();
            return right == null ? null : Finish(new BinaryExpression(left, @operator, right), left);
        }

        return left;
    }

    /// <summary>
    /// Binary operators with precedence <paramref name="minPrecedence"/> and higher, left-associative.
    /// </summary>
    private Expression ParseBinaryExpression(int minPrecedence)
    {
        var left = ParseUnaryExpression();
        while (left != null)
        {
            var (@operator, precedence, tokens) = PeekBinaryOperator();
            if (precedence < minPrecedence || precedence < LogicalOrPrecedence)
            {
                break;
            }

            EatTokens(tokens);
            var right = ParseBinaryExpression(precedence + 1);
            if (right == null)
            {
                return null;
            }

            left = Finish(new BinaryExpression(left, @operator, right), left);
        }

        return left;
    }

    /// <summary>
    /// The binary operator at the current token, its precedence and the number of tokens it takes;
    /// a precedence of 0 when there is none. '&gt;&gt;', '&gt;=' and '&gt;&gt;=' are composed from
    /// adjacent tokens.
    /// </summary>
    private (string Operator, int Precedence, int Tokens) PeekBinaryOperator()
    {
        var token = Current;
        if (token.Kind != TokenKind.Punctuator)
        {
            return (null, 0, 0);
        }

        if (token.Text == ">")
        {
            var next = TokenAt(token.End);
            if (AreAdjacent(token, next) && next.IsPunctuator("="))
            {
                return (">=", 10, 2);
            }

            if (AreAdjacent(token, next) && next.IsPunctuator(">"))
            {
                var third = TokenAt(next.End);
                return AreAdjacent(next, third) && third.IsPunctuator("=") ? (">>=", AssignmentPrecedence, 3) : (">>", 12, 2);
            }
        }

        var precedence = token.Text switch
        {
            "," => CommaPrecedence,
            "=" or "*=" or "/=" or "%=" or "+=" or "-=" or "<<=" or "&=" or "^=" or "|=" => AssignmentPrecedence,
            "||" => 4,
            "&&" => 5,
            "|" => 6,
            "^" => 7,
            "&" => 8,
            "==" or "!=" => 9,
            "<" or ">" or "<=" => 10,
            "<=>" => 11,
            "<<" => 12,
            "+" or "-" => 13,
            "*" or "/" or "%" => 14,
            ".*" or "->*" => 15,
            _ => 0,
        };

        return (token.Text, precedence, 1);
    }

    private void EatTokens(int count)
    {
        for (var i = 0; i < count; i++)
        {
            EatToken();
        }
    }

    /// <summary>
    /// unary-expression: prefix operators, then a postfix expression.
    /// </summary>
    private Expression ParseUnaryExpression()
    {
        var token = Current;
        if (token.Kind == TokenKind.Punctuator && token.Text is "+" or "-" or "!" or "~" or "*" or "&" or "++" or "--")
        {
            EnsureSufficientStack();
            var start = NodeStart;
            EatToken();
            var operand = ParseUnaryExpression();
            return operand == null ? null : Finish(new UnaryExpression(token.Text, operand), start);
        }

        return ParsePostfixExpression();
    }

    /// <summary>
    /// postfix-expression: a primary expression followed by calls and postfix increments.
    /// </summary>
    private Expression ParsePostfixExpression()
    {
        var expression = ParsePrimaryExpression();
        while (expression != null)
        {
            if (TryEatPunctuator("("))
            {
                var arguments = ParseArguments();
                if (arguments == null)
                {
                    return null;
                }

                expression = Finish(new CallExpression(expression, arguments), expression);
            }
            else if (IsPunctuator("++") || IsPunctuator("--"))
            {
                expression = Finish(new UnaryExpression(EatToken().Text, expression, isPostfix: true), expression);
            }
            else
            {
                break;
            }
        }

        return expression;
    }

    /// <summary>
    /// The arguments after '(' up to and including ')'.
    /// </summary>
    private List<Expression> ParseArguments()
    {
        var arguments = new List<Expression>();
        if (TryEatPunctuator(")"))
        {
            return arguments;
        }

        while (true)
        {
            var argument = ParseAssignmentExpression();
            if (argument == null)
            {
                return null;
            }

            arguments.Add(argument);
            if (TryEatPunctuator(")"))
            {
                return arguments;
            }

            if (!TryEatPunctuator(","))
            {
                return null;
            }
        }
    }

    /// <summary>
    /// primary-expression: literals, names and parenthesized expressions.
    /// </summary>
    private Expression ParsePrimaryExpression()
    {
        var start = NodeStart;
        var token = Current;

        switch (token.Kind)
        {
            case TokenKind.NumericLiteral:
                EatToken();
                Lexer.ScanNumber(_source[token.Start..token.End], out var isFloating);
                return Finish(new LiteralExpression(isFloating ? LiteralKind.Floating : LiteralKind.Integer, token.Text), start);

            case TokenKind.CharacterLiteral:
                EatToken();
                return Finish(new LiteralExpression(LiteralKind.Character, token.Text), start);

            case TokenKind.StringLiteral:
            {
                // Adjacent string literals are one literal
                var text = EatToken().Text;
                while (Current.Kind == TokenKind.StringLiteral)
                {
                    text += " " + EatToken().Text;
                }

                return Finish(new LiteralExpression(LiteralKind.String, text), start);
            }

            case TokenKind.Identifier:
                EatToken();
                return Finish(new NameExpression(token.Text), start);

            case TokenKind.Keyword:
                switch (token.Text)
                {
                    case "true":
                    case "false":
                        EatToken();
                        return Finish(new LiteralExpression(LiteralKind.Boolean, token.Text), start);
                    case "nullptr":
                        EatToken();
                        return Finish(new LiteralExpression(LiteralKind.Nullptr, token.Text), start);
                }

                return null;

            case TokenKind.Punctuator when token.Text == "(":
            {
                EatToken();
                var expression = ParseExpression();
                if (expression == null || !TryEatPunctuator(")"))
                {
                    return null;
                }

                return Finish(new ParenthesizedExpression(expression), start);
            }

            default:
                return null;
        }
    }
}
