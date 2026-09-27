namespace PaspanParsers.CSharp;

// Stage 5: statements.
internal ref partial struct SyntaxParser
{
    public BlockStatement ParseBlock()
    {
        if (!TryEatPunctuator("{"))
        {
            return null;
        }

        var noLambdaArrow = _noLambdaArrow;
        var queryDepth = _queryDepth;
        _noLambdaArrow = false;
        _queryDepth = 0;
        try
        {
            var statements = new List<Statement>();
            while (!IsPunctuator("}"))
            {
                var statement = ParseStatement();
                if (statement == null)
                {
                    return null;
                }

                statements.Add(statement);
            }

            EatToken();
            return new BlockStatement(statements.Count != 0 ? statements : null);
        }
        finally
        {
            _noLambdaArrow = noLambdaArrow;
            _queryDepth = queryDepth;
        }
    }

    public Statement ParseStatement()
    {
        if (!HasSufficientStack())
        {
            return null;
        }

        var token = Current;
        switch (token.Kind)
        {
            case TokenKind.Punctuator:
                switch (token.Text)
                {
                    case "{":
                        return ParseBlock();
                    case ";":
                        EatToken();
                        return new EmptyStatement();
                    case "[":
                        return ParseLocalFunctionWithPrefix();
                }

                break;

            case TokenKind.Keyword:
            {
                var statement = ParseKeywordStatement(token, out var handled);
                if (handled)
                {
                    return statement;
                }

                break;
            }

            case TokenKind.Identifier:
            {
                var statement = ParseContextualStatement(token, out var handled);
                if (handled)
                {
                    return statement;
                }

                break;
            }
        }

        return ParseDeclarationOrExpressionStatement();
    }

    private Statement ParseKeywordStatement(SyntaxToken token, out bool handled)
    {
        handled = true;
        switch (token.Text)
        {
            case "if":
                return ParseIfStatement();
            case "while":
                return ParseWhileStatement();
            case "do":
                return ParseDoStatement();
            case "for":
                return ParseForStatement();
            case "foreach":
                return ParseForEachStatement(isAwait: false);
            case "switch":
                return ParseSwitchStatement();
            case "try":
                return ParseTryStatement();
            case "lock":
                return ParseLockStatement();
            case "using":
                return ParseUsingStatement(isAwait: false);
            case "fixed":
                return ParseFixedStatement();
            case "goto":
                return ParseGotoStatement();
            case "const":
                return ParseConstStatement();

            case "break":
                EatToken();
                return TryEatPunctuator(";") ? new BreakStatement() : null;

            case "continue":
                EatToken();
                return TryEatPunctuator(";") ? new ContinueStatement() : null;

            case "return":
            {
                EatToken();
                Expression expression = null;
                if (!IsPunctuator(";"))
                {
                    expression = ParseExpression();
                    if (expression == null)
                    {
                        return null;
                    }
                }

                return TryEatPunctuator(";") ? new ReturnStatement(expression) : null;
            }

            case "throw":
            {
                EatToken();
                Expression expression = null;
                if (!IsPunctuator(";"))
                {
                    expression = ParseExpression();
                    if (expression == null)
                    {
                        return null;
                    }
                }

                return TryEatPunctuator(";") ? new ThrowStatement(expression) : null;
            }

            case "checked":
            case "unchecked":
                if (Peek(1).IsPunctuator("{"))
                {
                    EatToken();
                    var block = ParseBlock();
                    return block == null ? null : new CheckedStatement(token.Text == "checked", block);
                }

                break;

            case "unsafe":
                if (Peek(1).IsPunctuator("{"))
                {
                    EatToken();
                    var block = ParseBlock();
                    return block == null ? null : new UnsafeStatement(block);
                }

                return ParseLocalFunctionWithPrefix();

            case "static":
            case "extern":
                return ParseLocalFunctionWithPrefix();
        }

        handled = false;
        return null;
    }

    private Statement ParseContextualStatement(SyntaxToken token, out bool handled)
    {
        handled = true;
        var next = Peek(1);

        // label: statement
        if (next.IsPunctuator(":"))
        {
            EatToken();
            EatToken();
            var labeled = ParseStatement();
            return labeled == null ? null : new LabeledStatement(token.Text, labeled);
        }

        if (!token.IsVerbatim)
        {
            switch (token.Text)
            {
                case "yield" when next.IsKeyword("return"):
                {
                    EatToken();
                    EatToken();
                    var expression = ParseExpression();
                    return expression != null && TryEatPunctuator(";") ? new YieldReturnStatement(expression) : null;
                }

                case "yield" when next.IsKeyword("break"):
                    EatToken();
                    EatToken();
                    return TryEatPunctuator(";") ? new YieldBreakStatement() : null;

                case "await" when next.IsKeyword("foreach"):
                    EatToken();
                    return ParseForEachStatement(isAwait: true);

                case "await" when next.IsKeyword("using"):
                    EatToken();
                    return ParseUsingStatement(isAwait: true);

                case "await" when IsAwaitExpression():
                    return ParseExpressionStatement();

                case "async":
                {
                    // async local function; otherwise an expression or declaration
                    var start = _position;
                    var function = ParseLocalFunctionWithPrefix();
                    if (function != null)
                    {
                        return function;
                    }

                    _position = start;
                    break;
                }
            }
        }

        handled = false;
        return null;
    }

    private Statement ParseExpressionStatement()
    {
        var expression = ParseExpression();
        return expression != null && TryEatPunctuator(";") ? new ExpressionStatement(expression) : null;
    }

    /// <summary>
    /// A local declaration, a local function or an expression statement.
    /// </summary>
    private Statement ParseDeclarationOrExpressionStatement()
    {
        var start = _position;
        var declaration = TryParseLocalDeclarationOrFunction(null, null);
        if (declaration != null)
        {
            return declaration;
        }

        _position = start;
        return ParseExpressionStatement();
    }

    /// <summary>
    /// Attributes and modifiers, then a local function (or a local declaration when there are none).
    /// </summary>
    private Statement ParseLocalFunctionWithPrefix()
    {
        var attributes = ParseAttributeSections();
        if (attributes == null)
        {
            return null;
        }

        var modifiers = new List<Modifiers>();
        while (true)
        {
            var token = Current;
            Modifiers? modifier = token.Kind switch
            {
                TokenKind.Keyword => token.Text switch
                {
                    "static" => Modifiers.Static,
                    "extern" => Modifiers.Extern,
                    "unsafe" => Modifiers.Unsafe,
                    _ => null,
                },
                TokenKind.Identifier when token.IsContextual("async") && !Peek(1).IsPunctuator("(") && !Peek(1).IsPunctuator("=") => Modifiers.Async,
                _ => null,
            };

            if (!modifier.HasValue)
            {
                break;
            }

            EatToken();
            modifiers.Add(modifier.Value);
        }

        return TryParseLocalDeclarationOrFunction(attributes.Count != 0 ? attributes : null, modifiers.Count != 0 ? modifiers : null);
    }

    /// <summary>
    /// Type identifier ... : a local declaration when '=', ';' or ',' follows the identifier,
    /// a local function when '(' or '&lt;' follows it. Returns null (position unspecified) otherwise.
    /// </summary>
    private Statement TryParseLocalDeclarationOrFunction(List<AttributeSection> attributes, List<Modifiers> modifiers)
    {
        var type = ParseLocalType();
        if (type == null || !Current.IsIdentifier)
        {
            return null;
        }

        var next = Peek(1);
        if (next.IsPunctuator("(") || next.IsPunctuator("<"))
        {
            return type is ScopedTypeReference ? null : ParseLocalFunctionRest(type, attributes, modifiers);
        }

        if (attributes != null || modifiers != null)
        {
            return null;
        }

        if (!next.IsPunctuator("=") && !next.IsPunctuator(";") && !next.IsPunctuator(","))
        {
            return null;
        }

        var variables = ParseVariableDeclarators();
        return variables != null && TryEatPunctuator(";") ? new LocalDeclarationStatement(type, variables) : null;
    }

    /// <summary>
    /// The type of a local: [scoped] [ref [readonly]] Type.
    /// </summary>
    private TypeReference ParseLocalType()
    {
        if (IsContextual("scoped"))
        {
            var start = _position;
            EatToken();
            var inner = ParseReturnType();
            if (inner != null && Current.IsIdentifier)
            {
                return new ScopedTypeReference(inner);
            }

            _position = start;
        }

        return ParseReturnType();
    }

    /// <summary>
    /// identifier ['=' initializer] (',' identifier ['=' initializer])*
    /// </summary>
    public List<VariableDeclarator> ParseVariableDeclarators()
    {
        var variables = new List<VariableDeclarator>();
        while (true)
        {
            var name = TryEatIdentifier();
            if (name == null)
            {
                return null;
            }

            Expression initializer = null;
            if (TryEatPunctuator("="))
            {
                initializer = ParseVariableInitializer();
                if (initializer == null)
                {
                    return null;
                }
            }

            variables.Add(new VariableDeclarator(name, initializer));

            if (!TryEatPunctuator(","))
            {
                return variables;
            }
        }
    }

    /// <summary>
    /// An expression or an array initializer: <c>int[] a = { 1, 2 };</c>
    /// </summary>
    public Expression ParseVariableInitializer() => IsPunctuator("{") ? ParseArrayInitializer() : ParseExpression();

    private Statement ParseLocalFunctionRest(TypeReference returnType, List<AttributeSection> attributes, List<Modifiers> modifiers)
    {
        var name = EatToken().Text;

        List<TypeParameter> typeParameters = null;
        if (IsPunctuator("<"))
        {
            typeParameters = ParseTypeParameterList();
            if (typeParameters == null)
            {
                return null;
            }
        }

        var parameters = ParseParameterList(allowImplicitTypes: false);
        if (parameters == null)
        {
            return null;
        }

        var constraints = ParseConstraintClauses();
        if (constraints == null)
        {
            return null;
        }

        BlockStatement body = null;
        Expression expressionBody = null;
        if (IsPunctuator("{"))
        {
            body = ParseBlock();
            if (body == null)
            {
                return null;
            }
        }
        else if (TryEatPunctuator("=>"))
        {
            expressionBody = ParseExpression();
            if (expressionBody == null || !TryEatPunctuator(";"))
            {
                return null;
            }
        }
        else if (!TryEatPunctuator(";"))
        {
            return null;
        }

        return new LocalFunctionStatement(
            returnType,
            name,
            parameters,
            body,
            expressionBody,
            modifiers,
            typeParameters,
            constraints.Count != 0 ? constraints : null,
            attributes);
    }

    private Statement ParseConstStatement()
    {
        EatToken();
        var type = ParseType(TypeMode.Normal);
        if (type == null)
        {
            return null;
        }

        var variables = ParseVariableDeclarators();
        return variables != null && TryEatPunctuator(";") ? new LocalDeclarationStatement(type, variables, isConst: true) : null;
    }

    // ========================================
    // Selection and iteration
    // ========================================

    /// <summary>
    /// '(' expression ')'
    /// </summary>
    private Expression ParseParenthesizedCondition()
    {
        if (!TryEatPunctuator("("))
        {
            return null;
        }

        var expression = ParseExpressionInNestedContext();
        return expression != null && TryEatPunctuator(")") ? expression : null;
    }

    private Statement ParseIfStatement()
    {
        EatToken();
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

        Statement otherwise = null;
        if (TryEatKeyword("else"))
        {
            otherwise = ParseStatement();
            if (otherwise == null)
            {
                return null;
            }
        }

        return new IfStatement(condition, then, otherwise);
    }

    private Statement ParseWhileStatement()
    {
        EatToken();
        var condition = ParseParenthesizedCondition();
        if (condition == null)
        {
            return null;
        }

        var body = ParseStatement();
        return body == null ? null : new WhileStatement(condition, body);
    }

    private Statement ParseDoStatement()
    {
        EatToken();
        var body = ParseStatement();
        if (body == null || !TryEatKeyword("while"))
        {
            return null;
        }

        var condition = ParseParenthesizedCondition();
        return condition != null && TryEatPunctuator(";") ? new DoStatement(body, condition) : null;
    }

    /// <summary>
    /// for '(' [declaration | expression (',' expression)*] ';' [condition] ';' [expression (',' expression)*] ')' statement
    /// </summary>
    private Statement ParseForStatement()
    {
        EatToken();
        if (!TryEatPunctuator("("))
        {
            return null;
        }

        List<Statement> initializers = null;
        if (!IsPunctuator(";"))
        {
            var start = _position;
            var declaration = TryParseVariableDeclaration();
            if (declaration != null && IsPunctuator(";"))
            {
                initializers = [declaration];
            }
            else
            {
                _position = start;
                var expressions = ParseExpressionList();
                if (expressions == null)
                {
                    return null;
                }

                initializers = expressions.Select(e => (Statement)new ExpressionStatement(e)).ToList();
            }
        }

        if (!TryEatPunctuator(";"))
        {
            return null;
        }

        Expression condition = null;
        if (!IsPunctuator(";"))
        {
            condition = ParseExpressionInNestedContext();
            if (condition == null)
            {
                return null;
            }
        }

        if (!TryEatPunctuator(";"))
        {
            return null;
        }

        List<Expression> iterators = null;
        if (!IsPunctuator(")"))
        {
            iterators = ParseExpressionList();
            if (iterators == null)
            {
                return null;
            }
        }

        if (!TryEatPunctuator(")"))
        {
            return null;
        }

        var body = ParseStatement();
        return body == null ? null : new ForStatement(body, initializers, condition, iterators);
    }

    private List<Expression> ParseExpressionList()
    {
        var expressions = new List<Expression>();
        while (true)
        {
            var expression = ParseExpressionInNestedContext();
            if (expression == null)
            {
                return null;
            }

            expressions.Add(expression);
            if (!TryEatPunctuator(","))
            {
                return expressions;
            }
        }
    }

    /// <summary>
    /// A local variable declaration without the ';': [scoped] [ref] Type identifier ['=' initializer], ...
    /// Returns null (position unspecified) when no declaration is here.
    /// </summary>
    private LocalDeclarationStatement TryParseVariableDeclaration()
    {
        var type = ParseLocalType();
        if (type == null || !Current.IsIdentifier)
        {
            return null;
        }

        var next = Peek(1);
        if (!next.IsPunctuator("=") && !next.IsPunctuator(",") && !next.IsPunctuator(";") && !next.IsPunctuator(")"))
        {
            return null;
        }

        var variables = ParseVariableDeclarators();
        return variables == null ? null : new LocalDeclarationStatement(type, variables);
    }

    /// <summary>
    /// [await] foreach '(' (Type identifier | deconstruction) in expression ')' statement
    /// </summary>
    private Statement ParseForEachStatement(bool isAwait)
    {
        EatToken();
        if (!TryEatPunctuator("("))
        {
            return null;
        }

        TypeReference type = null;
        string identifier = null;
        Expression variable = null;

        var start = _position;
        var candidate = ParseLocalType();
        if (candidate != null && Current.IsIdentifier && Peek(1).IsKeyword("in"))
        {
            type = candidate;
            identifier = EatToken().Text;
        }
        else
        {
            _position = start;
            variable = ParseExpressionInNestedContext();
            if (variable == null)
            {
                return null;
            }
        }

        if (!TryEatKeyword("in"))
        {
            return null;
        }

        var collection = ParseExpressionInNestedContext();
        if (collection == null || !TryEatPunctuator(")"))
        {
            return null;
        }

        var body = ParseStatement();
        return body == null ? null : new ForEachStatement(type, identifier, collection, body, isAwait, variable);
    }

    /// <summary>
    /// switch '(' expression ')' '{' section* '}', or switch (a, b) { ... } on a tuple.
    /// </summary>
    private Statement ParseSwitchStatement()
    {
        EatToken();

        var start = _position;
        Expression governing = null;
        if (TryEatPunctuator("("))
        {
            governing = ParseExpressionInNestedContext();
            if (governing == null || !TryEatPunctuator(")"))
            {
                governing = null;
            }
        }

        if (governing == null)
        {
            // A tuple without extra parentheses: switch (a, b)
            _position = start;
            governing = ParseExpressionInNestedContext();
            if (governing is not TupleExpression)
            {
                return null;
            }
        }

        if (!TryEatPunctuator("{"))
        {
            return null;
        }

        var sections = new List<SwitchSection>();
        while (!IsPunctuator("}"))
        {
            var labels = new List<SwitchLabel>();
            while (true)
            {
                if (TryEatKeyword("case"))
                {
                    var pattern = ParsePattern();
                    if (pattern == null)
                    {
                        return null;
                    }

                    Expression guard = null;
                    if (TryEatContextual("when"))
                    {
                        guard = ParseExpression();
                        if (guard == null)
                        {
                            return null;
                        }
                    }

                    if (!TryEatPunctuator(":"))
                    {
                        return null;
                    }

                    labels.Add(new CaseSwitchLabel(pattern, guard));
                }
                else if (IsKeyword("default") && Peek(1).IsPunctuator(":"))
                {
                    EatToken();
                    EatToken();
                    labels.Add(new DefaultSwitchLabel());
                }
                else
                {
                    break;
                }
            }

            if (labels.Count == 0)
            {
                return null;
            }

            var statements = new List<Statement>();
            while (!IsPunctuator("}") && !IsKeyword("case") && !(IsKeyword("default") && Peek(1).IsPunctuator(":")))
            {
                var statement = ParseStatement();
                if (statement == null)
                {
                    return null;
                }

                statements.Add(statement);
            }

            sections.Add(new SwitchSection(labels, statements));
        }

        EatToken();
        return new SwitchStatement(governing, sections.Count != 0 ? sections : null);
    }

    // ========================================
    // Other statements
    // ========================================

    /// <summary>
    /// try block (catch ['(' Type [identifier] ')'] [when '(' expression ')'] block)* [finally block]
    /// </summary>
    private Statement ParseTryStatement()
    {
        EatToken();
        var block = ParseBlock();
        if (block == null)
        {
            return null;
        }

        var catches = new List<CatchClause>();
        while (TryEatKeyword("catch"))
        {
            TypeReference type = null;
            string identifier = null;
            if (TryEatPunctuator("("))
            {
                type = ParseType(TypeMode.Normal);
                if (type == null)
                {
                    return null;
                }

                identifier = TryEatIdentifier();
                if (!TryEatPunctuator(")"))
                {
                    return null;
                }
            }

            Expression filter = null;
            if (TryEatContextual("when"))
            {
                filter = ParseParenthesizedCondition();
                if (filter == null)
                {
                    return null;
                }
            }

            var catchBlock = ParseBlock();
            if (catchBlock == null)
            {
                return null;
            }

            catches.Add(new CatchClause(catchBlock, type, identifier, filter));
        }

        BlockStatement finallyBlock = null;
        if (TryEatKeyword("finally"))
        {
            finallyBlock = ParseBlock();
            if (finallyBlock == null)
            {
                return null;
            }
        }

        if (catches.Count == 0 && finallyBlock == null)
        {
            return null;
        }

        return new TryStatement(block, catches.Count != 0 ? catches : null, finallyBlock);
    }

    private Statement ParseLockStatement()
    {
        EatToken();
        var expression = ParseParenthesizedCondition();
        if (expression == null)
        {
            return null;
        }

        var body = ParseStatement();
        return body == null ? null : new LockStatement(expression, body);
    }

    /// <summary>
    /// using '(' (declaration | expression) ')' statement, or a using declaration: using Type x = ...;
    /// </summary>
    private Statement ParseUsingStatement(bool isAwait)
    {
        EatToken();

        if (TryEatPunctuator("("))
        {
            Statement resource;
            var start = _position;
            var declaration = TryParseVariableDeclaration();
            if (declaration != null && IsPunctuator(")"))
            {
                resource = declaration;
            }
            else
            {
                _position = start;
                var expression = ParseExpressionInNestedContext();
                if (expression == null)
                {
                    return null;
                }

                resource = new ExpressionStatement(expression);
            }

            if (!TryEatPunctuator(")"))
            {
                return null;
            }

            var body = ParseStatement();
            return body == null ? null : new UsingStatement(resource, body, isAwait);
        }

        var type = ParseLocalType();
        if (type == null)
        {
            return null;
        }

        var variables = ParseVariableDeclarators();
        return variables != null && TryEatPunctuator(";")
            ? new LocalDeclarationStatement(type, variables, isUsing: true, isAwait: isAwait)
            : null;
    }

    /// <summary>
    /// fixed '(' Type declarators ')' statement
    /// </summary>
    private Statement ParseFixedStatement()
    {
        EatToken();
        if (!TryEatPunctuator("("))
        {
            return null;
        }

        var type = ParseType(TypeMode.Normal);
        if (type == null)
        {
            return null;
        }

        var variables = ParseVariableDeclarators();
        if (variables == null || !TryEatPunctuator(")"))
        {
            return null;
        }

        var body = ParseStatement();
        return body == null ? null : new FixedStatement(type, variables, body);
    }

    /// <summary>
    /// goto identifier; goto case expression; goto default;
    /// </summary>
    private Statement ParseGotoStatement()
    {
        EatToken();

        GotoStatement statement;
        if (TryEatKeyword("case"))
        {
            var expression = ParseExpression();
            if (expression == null)
            {
                return null;
            }

            statement = new GotoStatement(null, GotoKind.Case, expression);
        }
        else if (TryEatKeyword("default"))
        {
            statement = new GotoStatement(null, GotoKind.Default);
        }
        else
        {
            var label = TryEatIdentifier();
            if (label == null)
            {
                return null;
            }

            statement = new GotoStatement(label);
        }

        return TryEatPunctuator(";") ? statement : null;
    }
}
