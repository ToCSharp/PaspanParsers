namespace PaspanParsers.Cpp;

// Statements ([stmt]).
internal ref partial struct SyntaxParser
{
    /// <summary>
    /// statement: dispatched on its first token. A statement that starts with a keyword of a statement,
    /// '{', ';' or a label is that statement; any other statement is a declaration if it can be one
    /// ([stmt.ambig]), and an expression otherwise.
    /// </summary>
    private Statement ParseStatement()
    {
        EnsureSufficientStack();
        if (IsAttributeStart)
        {
            return ParseAttributedStatement();
        }

        var start = NodeStart;
        var token = Current;
        if (token.Kind == TokenKind.Keyword)
        {
            switch (token.Text)
            {
                case "if":
                    return ParseIfStatement();
                case "switch":
                    return ParseSwitchStatement();
                case "while":
                    return ParseWhileStatement();
                case "do":
                    return ParseDoStatement();
                case "for":
                    return ParseForStatement();
                case "break":
                    EatToken();
                    return TryEatPunctuator(";") ? Finish(new BreakStatement(), start) : null;
                case "continue":
                    EatToken();
                    return TryEatPunctuator(";") ? Finish(new ContinueStatement(), start) : null;
                case "return" or "co_return":
                    return ParseReturnStatement();
                case "goto":
                    EatToken();
                    var label = TryEatIdentifier();
                    return label != null && TryEatPunctuator(";") ? Finish(new GotoStatement(label), start) : null;
                case "case":
                    return ParseCaseStatement();
                case "default":
                    EatToken();
                    return TryEatPunctuator(":") && TryParseLabeledSubstatement(out var statement)
                        ? Finish(new DefaultStatement(statement), start)
                        : null;
                case "try":
                    return ParseTryStatement();
            }
        }
        else if (token.Kind == TokenKind.Punctuator)
        {
            if (token.Text == "{")
            {
                return ParseCompoundStatement();
            }

            if (token.Text == ";")
            {
                EatToken();
                return Finish(new ExpressionStatement(null), start);
            }
        }
        else if (token.IsIdentifier && Peek(1).IsPunctuator(":"))
        {
            // A label; '::' is a single token
            EatTokens(2);
            return TryParseLabeledSubstatement(out var statement) ? Finish(new LabeledStatement(token.Text, statement), start) : null;
        }

        return ParseDeclarationOrExpressionStatement();
    }

    /// <summary>
    /// A statement after attributes. The attributes of a declaration belong to the declaration
    /// (<c>[[maybe_unused]] int a;</c>); those of other statements, including labels, to the statement.
    /// </summary>
    private Statement ParseAttributedStatement()
    {
        var start = NodeStart;
        if (IsDeclarationStatementStart())
        {
            var mark = Save();
            var declaration = ParseBlockDeclaration();
            if (declaration != null)
            {
                return Finish(new DeclarationStatement(declaration), start);
            }

            Restore(mark);
        }

        var attributes = ParseAttributeSpecifiers();
        if (attributes == null)
        {
            return null;
        }

        var statement = ParseStatement();
        return statement == null ? null : Finish(new AttributedStatement(attributes, statement), start);
    }

    /// <summary>
    /// A declaration statement or an expression statement: what can be a declaration is one ([stmt.ambig]).
    /// </summary>
    private Statement ParseDeclarationOrExpressionStatement()
    {
        var start = NodeStart;
        if (IsKeyword("static_assert") || IsDeclarationStatementStart())
        {
            var mark = Save();
            var declaration = ParseBlockDeclaration();
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
    /// The statement after a label, or null (and true) for a label at the end of a block: <c>end: }</c> (C++23).
    /// </summary>
    private bool TryParseLabeledSubstatement(out Statement statement)
    {
        statement = null;
        if (IsPunctuator("}"))
        {
            return true;
        }

        statement = ParseStatement();
        return statement != null;
    }

    // ========================================
    // Declarations or expressions
    // ========================================

    /// <summary>
    /// A statement that starts with attributes and a declaration, with a declaration specifier keyword, or
    /// with a name that is a type: known as one, or unknown and followed by what can only follow a type in
    /// a declaration (<c>X y</c>, <c>X const</c>, <c>X *y;</c>, <c>X &amp;y =</c>, <c>X&lt;int&gt; y</c>).
    /// </summary>
    private bool IsDeclarationStatementStart()
    {
        var mark = Save();
        try
        {
            if (IsAttributeStart && ParseAttributeSpecifiers() == null)
            {
                return false;
            }

            if (IsDeclSpecifierKeyword || (Current.IsIdentifier && ExtensionTypeNames.Contains(Current.Text)))
            {
                return true;
            }

            if (!IsNameStart(Current, NameContext.Expression))
            {
                return false;
            }

            var nameStart = Save();
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
                Restore(nameStart);
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
    /// The tokens after a name can only follow a type: a declarator name, a cv-qualifier, a pointer to one
    /// followed by the end of a declarator, a reference to one followed by its initializer (<c>X &amp;&amp; y;</c>
    /// is an expression: a reference needs an initializer), or a pointer to member.
    /// </summary>
    private bool FollowsTypeInDeclaration()
    {
        var next = Current;
        if (next.IsIdentifier || next.IsKeyword("operator") || next.IsKeyword("const") || next.IsKeyword("volatile"))
        {
            return true;
        }

        if (next.IsPunctuator("::") && Peek(1).IsPunctuator("*"))
        {
            return true;
        }

        if (next.IsPunctuator("*") || next.IsPunctuator("&") || next.IsPunctuator("&&"))
        {
            var offset = 0;
            var isReference = false;
            while (Peek(offset).IsPunctuator("*") || Peek(offset).IsPunctuator("&") || Peek(offset).IsPunctuator("&&") || Peek(offset).IsKeyword("const"))
            {
                isReference |= !Peek(offset).IsPunctuator("*") && !Peek(offset).IsKeyword("const");
                offset++;
            }

            var after = Peek(offset + 1);
            if (!Peek(offset).IsIdentifier)
            {
                return false;
            }

            return isReference
                ? after.IsPunctuator("=") || after.IsPunctuator("{")
                : after.IsPunctuator(";") || after.IsPunctuator("=") || after.IsPunctuator(",") || after.IsPunctuator("[");
        }

        return false;
    }

    // ========================================
    // Blocks
    // ========================================

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
    /// The statement of an if, switch or loop: it has a block scope of its own, even without braces.
    /// </summary>
    private Statement ParseSubstatement()
    {
        _cache.Symbols.EnterScope();
        var statement = ParseStatement();
        _cache.Symbols.ExitScope();
        return statement;
    }

    // ========================================
    // Selection statements
    // ========================================

    /// <summary>
    /// <c>if (init; condition) statement else statement</c>, <c>if constexpr (…)</c>, <c>if consteval { … }</c>
    /// and <c>if !consteval { … }</c>. The names declared in the init-statement and the condition are in
    /// scope in both branches.
    /// </summary>
    private IfStatement ParseIfStatement()
    {
        var start = NodeStart;
        EatToken();
        if (IsKeyword("consteval") || (IsPunctuator("!") && Peek(1).IsKeyword("consteval")))
        {
            var isNegated = TryEatPunctuator("!");
            EatToken();
            var block = ParseCompoundStatement();
            if (block == null || !TryParseElse(out var otherwise))
            {
                return null;
            }

            return Finish(new IfStatement(null, block, otherwise) { IsConsteval = true, IsNegated = isNegated }, start);
        }

        var isConstexpr = TryEatKeyword("constexpr");
        _cache.Symbols.EnterScope();
        if (!TryParseConditionClause(allowInitStatement: true, out var initStatement, out var condition))
        {
            return null;
        }

        var then = ParseSubstatement();
        if (then == null || !TryParseElse(out var @else))
        {
            return null;
        }

        _cache.Symbols.ExitScope();
        return Finish(new IfStatement(condition, then, @else) { IsConstexpr = isConstexpr, InitStatement = initStatement }, start);
    }

    /// <summary>
    /// <c>else statement</c>, if any.
    /// </summary>
    private bool TryParseElse(out Statement @else)
    {
        @else = null;
        if (!TryEatKeyword("else"))
        {
            return true;
        }

        @else = ParseSubstatement();
        return @else != null;
    }

    /// <summary>
    /// <c>switch (init; condition) statement</c>.
    /// </summary>
    private SwitchStatement ParseSwitchStatement()
    {
        var start = NodeStart;
        EatToken();
        _cache.Symbols.EnterScope();
        if (!TryParseConditionClause(allowInitStatement: true, out var initStatement, out var condition))
        {
            return null;
        }

        var body = ParseSubstatement();
        if (body == null)
        {
            return null;
        }

        _cache.Symbols.ExitScope();
        return Finish(new SwitchStatement(condition, body) { InitStatement = initStatement }, start);
    }

    /// <summary>
    /// <c>case value: statement</c> and the GNU range <c>case 1 ... 3: statement</c>.
    /// </summary>
    private CaseStatement ParseCaseStatement()
    {
        var start = NodeStart;
        EatToken();
        var value = ParseAssignmentExpression();
        if (value == null)
        {
            return null;
        }

        Expression rangeEnd = null;
        if (TryEatPunctuator("..."))
        {
            rangeEnd = ParseAssignmentExpression();
            if (rangeEnd == null)
            {
                return null;
            }
        }

        if (!TryEatPunctuator(":") || !TryParseLabeledSubstatement(out var statement))
        {
            return null;
        }

        return Finish(new CaseStatement(value, statement) { RangeEnd = rangeEnd }, start);
    }

    /// <summary>
    /// <c>( init-statement condition )</c> of an if or switch statement, or <c>( condition )</c> of a while
    /// statement. There is an init-statement when a ';' is inside the parentheses outside brackets.
    /// </summary>
    private bool TryParseConditionClause(bool allowInitStatement, out Statement initStatement, out CppNode condition)
    {
        initStatement = null;
        condition = null;
        if (!TryEatPunctuator("("))
        {
            return false;
        }

        var saved = EnterBrackets();
        if (allowInitStatement && CountSemicolonsBeforeClose() > 0)
        {
            initStatement = ParseInitStatement();
            if (initStatement == null)
            {
                return false;
            }
        }

        condition = ParseCondition(")");
        LeaveBrackets(saved);
        return condition != null && TryEatPunctuator(")");
    }

    /// <summary>
    /// The number of ';' outside brackets before the ')' that closes the current parentheses: the
    /// init-statement and the parts of a for statement end with them.
    /// </summary>
    private int CountSemicolonsBeforeClose()
    {
        var depth = 0;
        var count = 0;
        for (var token = Current; token.Kind != TokenKind.EndOfFile; token = TokenAt(token.End))
        {
            if (token.Kind != TokenKind.Punctuator)
            {
                continue;
            }

            switch (token.Text)
            {
                case "(" or "[" or "{":
                    depth++;
                    break;
                case ")" or "]" or "}":
                    if (depth-- == 0)
                    {
                        return count;
                    }

                    break;
                case ";" when depth == 0:
                    count++;
                    break;
            }
        }

        return count;
    }

    /// <summary>
    /// init-statement: a simple declaration or an expression statement, including its ';'.
    /// </summary>
    private Statement ParseInitStatement()
    {
        var start = NodeStart;
        return TryEatPunctuator(";") ? Finish(new ExpressionStatement(null), start) : ParseDeclarationOrExpressionStatement();
    }

    /// <summary>
    /// condition: a declaration with an initializer (<c>int k = next()</c>, <c>T x{ 1 }</c>) followed by
    /// <paramref name="close"/>, or an expression.
    /// </summary>
    private CppNode ParseCondition(string close)
    {
        if (IsDeclarationStatementStart())
        {
            var mark = Save();
            var declaration = ParseConditionDeclaration();
            if (declaration != null && IsPunctuator(close))
            {
                return declaration;
            }

            Restore(mark);
        }

        return ParseExpression();
    }

    private ConditionDeclaration ParseConditionDeclaration()
    {
        var start = NodeStart;
        var attributes = ParseAttributeSpecifiers();
        if (attributes == null)
        {
            return null;
        }

        var specifiers = ParseDeclSpecifiers();
        if (specifiers == null || !TryParseDeclarator(DeclaratorKind.Named, out var declarator))
        {
            return null;
        }

        DeclareName(specifiers, declarator);
        var initializerStart = NodeStart;
        Initializer initializer;
        if (TryEatPunctuator("="))
        {
            var value = ParseInitializerClause();
            initializer = value == null ? null : Finish(new EqualsInitializer(value), initializerStart);
        }
        else
        {
            initializer = IsPunctuator("{") ? ParseBracedInitializer() : null;
        }

        return initializer == null ? null : Finish(new ConditionDeclaration(specifiers, declarator, initializer) { Attributes = attributes }, start);
    }

    // ========================================
    // Iteration statements
    // ========================================

    /// <summary>
    /// <c>while (condition) statement</c>.
    /// </summary>
    private WhileStatement ParseWhileStatement()
    {
        var start = NodeStart;
        EatToken();
        _cache.Symbols.EnterScope();
        if (!TryParseConditionClause(allowInitStatement: false, out _, out var condition))
        {
            return null;
        }

        var body = ParseSubstatement();
        if (body == null)
        {
            return null;
        }

        _cache.Symbols.ExitScope();
        return Finish(new WhileStatement(condition, body), start);
    }

    /// <summary>
    /// <c>do statement while (expression);</c>.
    /// </summary>
    private DoStatement ParseDoStatement()
    {
        var start = NodeStart;
        EatToken();
        var body = ParseSubstatement();
        if (body == null || !TryEatKeyword("while") || !TryEatPunctuator("("))
        {
            return null;
        }

        var saved = EnterBrackets();
        var condition = ParseExpression();
        LeaveBrackets(saved);
        if (condition == null || !TryEatPunctuator(")") || !TryEatPunctuator(";"))
        {
            return null;
        }

        return Finish(new DoStatement(body, condition), start);
    }

    /// <summary>
    /// <c>for (init-statement condition; increment) statement</c> or the range-based
    /// <c>for (init-statement declaration : range) statement</c>: two ';' outside brackets make the first.
    /// </summary>
    private Statement ParseForStatement()
    {
        var start = NodeStart;
        EatToken();
        if (!TryEatPunctuator("("))
        {
            return null;
        }

        _cache.Symbols.EnterScope();
        var saved = EnterBrackets();
        var semicolons = CountSemicolonsBeforeClose();
        Statement initStatement = null;
        if (semicolons >= 2 && IsPunctuator(";"))
        {
            // for (;;) has no init-statement, like in clang
            EatToken();
        }
        else if (semicolons > 0)
        {
            initStatement = ParseInitStatement();
            if (initStatement == null)
            {
                return null;
            }
        }

        Statement result;
        if (semicolons >= 2)
        {
            CppNode condition = null;
            if (!IsPunctuator(";"))
            {
                condition = ParseCondition(";");
                if (condition == null)
                {
                    return null;
                }
            }

            if (!TryEatPunctuator(";"))
            {
                return null;
            }

            Expression increment = null;
            if (!IsPunctuator(")"))
            {
                increment = ParseExpression();
                if (increment == null)
                {
                    return null;
                }
            }

            LeaveBrackets(saved);
            if (!TryEatPunctuator(")"))
            {
                return null;
            }

            var body = ParseSubstatement();
            result = body == null ? null : Finish(new ForStatement(initStatement, condition, increment, body), start);
        }
        else
        {
            var declaration = ParseForRangeDeclaration();
            if (declaration == null || !TryEatPunctuator(":"))
            {
                return null;
            }

            var range = IsPunctuator("{") ? ParseBracedInitList() : ParseExpression();
            LeaveBrackets(saved);
            if (range == null || !TryEatPunctuator(")"))
            {
                return null;
            }

            // The loop variable is not in scope in the range
            DeclareName(declaration.Specifiers, declaration.Declarator);
            var body = ParseSubstatement();
            result = body == null ? null : Finish(new RangeForStatement(declaration, range, body) { InitStatement = initStatement }, start);
        }

        _cache.Symbols.ExitScope();
        return result;
    }

    /// <summary>
    /// for-range-declaration: attributes, specifiers and a declarator, which may be a structured binding.
    /// </summary>
    private ForRangeDeclaration ParseForRangeDeclaration()
    {
        var start = NodeStart;
        var attributes = ParseAttributeSpecifiers();
        if (attributes == null)
        {
            return null;
        }

        var specifiers = ParseDeclSpecifiers();
        if (specifiers == null || !TryParseDeclarator(DeclaratorKind.Named, out var declarator))
        {
            return null;
        }

        return Finish(new ForRangeDeclaration(specifiers, declarator) { Attributes = attributes }, start);
    }

    // ========================================
    // Jump statements
    // ========================================

    /// <summary>
    /// <c>return expression;</c> or <c>co_return expression;</c>; the expression may be a braced-init-list or absent.
    /// </summary>
    private Statement ParseReturnStatement()
    {
        var start = NodeStart;
        var isCoroutine = EatToken().Text == "co_return";
        Expression value = null;
        if (!IsPunctuator(";"))
        {
            value = IsPunctuator("{") ? ParseBracedInitList() : ParseExpression();
            if (value == null)
            {
                return null;
            }
        }

        if (!TryEatPunctuator(";"))
        {
            return null;
        }

        return isCoroutine ? Finish(new CoReturnStatement(value), start) : Finish(new ReturnStatement(value), start);
    }

    // ========================================
    // Exceptions
    // ========================================

    /// <summary>
    /// <c>try { … } catch (declaration) { … } catch (...) { … }</c>.
    /// </summary>
    private TryStatement ParseTryStatement()
    {
        var start = NodeStart;
        EatToken();
        var block = ParseCompoundStatement();
        if (block == null)
        {
            return null;
        }

        var handlers = new List<CatchClause>();
        while (IsKeyword("catch"))
        {
            var handler = ParseCatchClause();
            if (handler == null)
            {
                return null;
            }

            handlers.Add(handler);
        }

        return handlers.Count == 0 ? null : Finish(new TryStatement(block, handlers), start);
    }

    /// <summary>
    /// <c>catch (declaration) { … }</c> or <c>catch (...) { … }</c>; the declared name is in scope in the block.
    /// </summary>
    private CatchClause ParseCatchClause()
    {
        var start = NodeStart;
        EatToken();
        if (!TryEatPunctuator("("))
        {
            return null;
        }

        _cache.Symbols.EnterScope();
        ParameterDeclaration declaration = null;
        if (!TryEatPunctuator("..."))
        {
            var saved = EnterBrackets();
            declaration = ParseParameter();
            LeaveBrackets(saved);
            if (declaration == null || declaration.DefaultValue != null)
            {
                return null;
            }

            DeclareName(declaration.Specifiers, declaration.Declarator);
        }

        if (!TryEatPunctuator(")"))
        {
            return null;
        }

        var body = ParseCompoundStatement();
        if (body == null)
        {
            return null;
        }

        _cache.Symbols.ExitScope();
        return Finish(new CatchClause(declaration, body), start);
    }
}
