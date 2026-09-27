namespace PaspanParsers.Cpp;

// Statements ([stmt]).
internal ref partial struct SyntaxParser
{
    private Statement ParseStatement()
    {
        EnsureSufficientStack();
        var start = NodeStart;

        if (IsPunctuator("{"))
        {
            return ParseCompoundStatement();
        }

        if (TryEatPunctuator(";"))
        {
            return Finish(new ExpressionStatement(null), start);
        }

        if (TryEatKeyword("return"))
        {
            Expression value = null;
            if (!IsPunctuator(";"))
            {
                value = ParseExpression();
                if (value == null)
                {
                    return null;
                }
            }

            return TryEatPunctuator(";") ? Finish(new ReturnStatement(value), start) : null;
        }

        if (TryEatKeyword("if"))
        {
            return ParseIfStatementRest(start);
        }

        if (TryEatKeyword("while"))
        {
            var condition = ParseParenthesizedCondition();
            if (condition == null)
            {
                return null;
            }

            var body = ParseStatement();
            return body == null ? null : Finish(new WhileStatement(condition, body), start);
        }

        if (IsDeclSpecifierStart)
        {
            var declaration = ParseSimpleDeclaration();
            return declaration == null ? null : Finish(new DeclarationStatement(declaration), start);
        }

        var expression = ParseExpression();
        if (expression == null || !TryEatPunctuator(";"))
        {
            return null;
        }

        return Finish(new ExpressionStatement(expression), start);
    }

    /// <summary>
    /// <c>{ statements }</c>.
    /// </summary>
    private CompoundStatement ParseCompoundStatement()
    {
        var start = NodeStart;
        if (!TryEatPunctuator("{"))
        {
            return null;
        }

        var statements = new List<Statement>();
        while (!IsPunctuator("}"))
        {
            if (Current.Kind == TokenKind.EndOfFile)
            {
                return null;
            }

            var statement = ParseStatement();
            if (statement == null)
            {
                return null;
            }

            statements.Add(statement);
        }

        var closeBraceDirectives = DirectivesBefore(EatToken());
        return Finish(new CompoundStatement(statements) { CloseBraceDirectives = closeBraceDirectives }, start);
    }

    /// <summary>
    /// The rest of an if statement after <c>if</c>.
    /// </summary>
    private IfStatement ParseIfStatementRest(int start)
    {
        var condition = ParseParenthesizedCondition();
        if (condition == null)
        {
            return null;
        }

        var then = ParseStatement();
        if (then == null)
        {
            return null;
        }

        Statement @else = null;
        if (TryEatKeyword("else"))
        {
            @else = ParseStatement();
            if (@else == null)
            {
                return null;
            }
        }

        return Finish(new IfStatement(condition, then, @else), start);
    }

    /// <summary>
    /// <c>( expression )</c> of an if or while statement.
    /// </summary>
    private Expression ParseParenthesizedCondition()
    {
        if (!TryEatPunctuator("("))
        {
            return null;
        }

        var condition = ParseExpression();
        return condition != null && TryEatPunctuator(")") ? condition : null;
    }
}
