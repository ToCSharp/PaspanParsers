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

        if (IsDeclarationStatementStart())
        {
            // What can be a declaration is one ([stmt.ambig]); otherwise it is an expression
            var mark = Save();
            var declaration = ParseSimpleDeclaration();
            if (declaration != null)
            {
                return Finish(new DeclarationStatement(declaration), start);
            }

            Restore(mark);
        }

        var expression = ParseExpression();
        if (expression == null || !TryEatPunctuator(";"))
        {
            return null;
        }

        return Finish(new ExpressionStatement(expression), start);
    }

    /// <summary>
    /// A statement that starts with a declaration specifier keyword, or with a name that is a type: known as
    /// one, or unknown and followed by what can only follow a type in a declaration (<c>X y</c>,
    /// <c>X *y;</c>, <c>X &amp;y =</c>, <c>X&lt;int&gt; y</c>).
    /// </summary>
    private bool IsDeclarationStatementStart()
    {
        if (IsDeclSpecifierKeyword || (Current.IsIdentifier && ExtensionTypeNames.Contains(Current.Text)))
        {
            return true;
        }

        if (!IsNameStart(Current, NameContext.Expression))
        {
            return false;
        }

        var mark = Save();
        try
        {
            var name = ParseName(NameContext.Expression);
            if (name == null)
            {
                return false;
            }

            switch (_cache.Symbols.Lookup(name))
            {
                case SymbolKind.Type or SymbolKind.Template:
                    return true;
                case not null:
                    return false;
            }

            if (IsPunctuator("<"))
            {
                // An unknown template: vector<int> v;
                Restore(mark);
                if (ParseName(NameContext.Type) == null)
                {
                    return false;
                }
            }

            return FollowsTypeInDeclaration();
        }
        finally
        {
            Restore(mark);
        }
    }

    /// <summary>
    /// The tokens after a name can only follow a type: a declarator name, a pointer or reference to one
    /// followed by the end of a declarator, or a pointer to member.
    /// </summary>
    private bool FollowsTypeInDeclaration()
    {
        var next = Current;
        if (next.IsIdentifier || next.IsKeyword("operator"))
        {
            return true;
        }

        if (next.IsPunctuator("::") && Peek(1).IsPunctuator("*"))
        {
            return true;
        }

        if (next.IsPunctuator("*") || next.IsPunctuator("&") || next.IsPunctuator("&&"))
        {
            var offset = 1;
            while (Peek(offset).IsPunctuator("*") || Peek(offset).IsPunctuator("&") || Peek(offset).IsKeyword("const"))
            {
                offset++;
            }

            var after = Peek(offset + 1);
            return Peek(offset).IsIdentifier && (after.IsPunctuator(";") || after.IsPunctuator("=") || after.IsPunctuator(",") || after.IsPunctuator("["));
        }

        return false;
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

        _cache.Symbols.EnterScope();
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

        _cache.Symbols.ExitScope();
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
