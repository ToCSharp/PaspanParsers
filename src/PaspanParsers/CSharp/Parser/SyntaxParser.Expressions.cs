namespace PaspanParsers.CSharp;

/// <summary>
/// Operator precedence of C# 14, from lowest to highest (as in Roslyn).
/// </summary>
internal enum Precedence
{
    Expression,
    Assignment,
    Lambda,
    Conditional,
    Coalescing,
    ConditionalOr,
    ConditionalAnd,
    LogicalOr,
    LogicalXor,
    LogicalAnd,
    Equality,
    Relational,
    Shift,
    Additive,
    Multiplicative,
    Switch,
    Range,
    Unary,
    Cast,
    PointerIndirection,
    AddressOf,
    Primary,
}

// Stage 3: expressions, lambdas and query expressions.
internal ref partial struct SyntaxParser
{
    private enum OperatorKind
    {
        Binary,
        Assignment,
        Coalescing,
        Is,
        As,
        Switch,
        With,
        Range,
    }

    public Expression ParseExpression() => ParseSubExpression(Precedence.Expression);

    private Expression ParseSubExpression(Precedence precedence)
    {
        EnsureSufficientStack();

        var token = Current;

        if (token.IsKeyword("throw"))
        {
            if (precedence > Precedence.Coalescing)
            {
                return null;
            }

            EatToken();
            var thrown = ParseSubExpression(Precedence.Coalescing);
            return thrown == null ? null : Finish(new ThrowExpression(thrown), token.Start);
        }

        if (precedence <= Precedence.Conditional)
        {
            if (IsQueryExpressionStart())
            {
                return ParseQueryExpression();
            }

            if (IsLambdaStart(out var lambdaStart))
            {
                return ParseLambda(lambdaStart);
            }
        }

        var left = ParseUnaryExpression();
        return left == null ? null : ParseBinaryRest(left, precedence);
    }

    private Expression ParseUnaryExpression()
    {
        var token = Current;

        if (token.Kind == TokenKind.Punctuator)
        {
            UnaryOperator? op = token.Text switch
            {
                "+" => UnaryOperator.Plus,
                "-" => UnaryOperator.Minus,
                "!" => UnaryOperator.Not,
                "~" => UnaryOperator.BitwiseNot,
                "++" => UnaryOperator.Increment,
                "--" => UnaryOperator.Decrement,
                "&" => UnaryOperator.AddressOf,
                "*" => UnaryOperator.Dereference,
                "^" => UnaryOperator.Index,
                _ => null,
            };

            if (op.HasValue)
            {
                EatToken();
                var operand = ParseSubExpression(Precedence.Unary);
                return operand == null ? null : Finish(new UnaryExpression(op.Value, operand), token.Start);
            }

            if (token.Text == "..")
            {
                EatToken();
                Expression end = null;
                if (CanStartExpression(Current))
                {
                    end = ParseSubExpression(Precedence.Range);
                    if (end == null)
                    {
                        return null;
                    }
                }

                return Finish(new RangeExpression(null, end), token.Start);
            }
        }
        else if (token.IsKeyword("ref"))
        {
            // Like in Roslyn, 'ref' takes a whole expression: ref c ? ref a : ref b is ref (c ? ref a : ref b)
            EatToken();
            var operand = ParseExpression();
            return operand == null ? null : Finish(new RefExpression(operand), token.Start);
        }
        else if (token.IsContextual("await") && IsAwaitExpression())
        {
            EatToken();
            var operand = ParseSubExpression(Precedence.Unary);
            return operand == null ? null : Finish(new AwaitExpression(operand), token.Start);
        }

        return ParsePostfix(ParsePrimary());
    }

    /// <summary>
    /// 'await' is an operator when an operand follows it; otherwise it is an identifier.
    /// </summary>
    private bool IsAwaitExpression()
    {
        var next = Peek(1);
        switch (next.Kind)
        {
            case TokenKind.Identifier:
                // 'await with { ... }' would be a with expression on a variable named await
                return !(next.IsContextual("with") && Peek(2).IsPunctuator("{"));
            case TokenKind.NumericLiteral:
            case TokenKind.CharacterLiteral:
            case TokenKind.StringLiteral:
            case TokenKind.InterpolatedString:
                return true;
            case TokenKind.Keyword:
                return next.Text is not ("is" or "as" or "switch") && CanStartExpression(next);
            case TokenKind.Punctuator:
                return next.Text is "(" or "!" or "~";
            default:
                return false;
        }
    }

    /// <summary>
    /// True when <paramref name="token"/> can be the first token of an expression.
    /// </summary>
    private static bool CanStartExpression(SyntaxToken token)
    {
        switch (token.Kind)
        {
            case TokenKind.Identifier:
            case TokenKind.NumericLiteral:
            case TokenKind.CharacterLiteral:
            case TokenKind.StringLiteral:
            case TokenKind.InterpolatedString:
                return true;
            case TokenKind.Keyword:
                switch (token.Text)
                {
                    case "this":
                    case "base":
                    case "new":
                    case "typeof":
                    case "sizeof":
                    case "default":
                    case "checked":
                    case "unchecked":
                    case "true":
                    case "false":
                    case "null":
                    case "throw":
                    case "stackalloc":
                    case "delegate":
                    case "ref":
                    case "static":
                    case "__arglist":
                    case "__makeref":
                    case "__reftype":
                    case "__refvalue":
                        return true;
                    default:
                        return IsPredefinedTypeKeyword(token);
                }
            case TokenKind.Punctuator:
                return token.Text is "(" or "[" or "!" or "~" or "+" or "-" or "++" or "--" or "&" or "*" or "^" or "..";
            default:
                return false;
        }
    }

    // ========================================
    // Binary operators
    // ========================================

    private Expression ParseBinaryRest(Expression left, Precedence precedence)
    {
        while (true)
        {
            if (!TryPeekBinaryOperator(out var kind, out var op, out var tokenCount, out var operatorPrecedence))
            {
                if (IsPunctuator("?") && precedence <= Precedence.Conditional)
                {
                    EatToken();
                    var whenTrue = ParseExpressionInNestedContext();
                    if (whenTrue == null || !TryEatPunctuator(":"))
                    {
                        return null;
                    }

                    var whenFalse = ParseExpressionInNestedContext();
                    if (whenFalse == null)
                    {
                        return null;
                    }

                    left = Finish(new ConditionalExpression(left, whenTrue, whenFalse), left);
                    continue;
                }

                return left;
            }

            var isRightAssociative = kind is OperatorKind.Assignment or OperatorKind.Coalescing;
            if (operatorPrecedence < precedence || (operatorPrecedence == precedence && !isRightAssociative))
            {
                return left;
            }

            for (var i = 0; i < tokenCount; i++)
            {
                EatToken();
            }

            switch (kind)
            {
                case OperatorKind.Is:
                {
                    var pattern = ParsePattern(isAfterIs: true);
                    if (pattern == null)
                    {
                        return null;
                    }

                    left = Finish(new IsExpression(left, pattern), left);
                    break;
                }

                case OperatorKind.As:
                {
                    var type = ParseType(TypeMode.Expression);
                    if (type == null)
                    {
                        return null;
                    }

                    left = Finish(new AsExpression(left, type), left);
                    break;
                }

                case OperatorKind.Switch:
                {
                    left = ParseSwitchExpression(left);
                    if (left == null)
                    {
                        return null;
                    }

                    break;
                }

                case OperatorKind.With:
                {
                    var initializer = ParseObjectOrCollectionInitializer();
                    if (initializer == null)
                    {
                        return null;
                    }

                    left = Finish(new WithExpression(left, initializer), left);
                    break;
                }

                case OperatorKind.Range:
                {
                    Expression end = null;
                    if (CanStartExpression(Current))
                    {
                        end = ParseSubExpression(Precedence.Range);
                        if (end == null)
                        {
                            return null;
                        }
                    }

                    left = Finish(new RangeExpression(left, end), left);
                    break;
                }

                default:
                {
                    var right = ParseSubExpression(operatorPrecedence);
                    if (right == null)
                    {
                        return null;
                    }

                    left = Finish(new BinaryExpression(left, op, right), left);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Recognizes the binary operator at the current position, composing '&gt;&gt;', '&gt;&gt;&gt;',
    /// '&gt;&gt;=' and '&gt;&gt;&gt;=' from adjacent '&gt;' tokens.
    /// </summary>
    private bool TryPeekBinaryOperator(out OperatorKind kind, out BinaryOperator op, out int tokenCount, out Precedence precedence)
    {
        kind = OperatorKind.Binary;
        op = default;
        tokenCount = 1;
        precedence = default;

        var token = Current;
        switch (token.Kind)
        {
            case TokenKind.Keyword:
                switch (token.Text)
                {
                    case "is":
                        kind = OperatorKind.Is;
                        precedence = Precedence.Relational;
                        return true;
                    case "as":
                        kind = OperatorKind.As;
                        precedence = Precedence.Relational;
                        return true;
                    case "switch" when Peek(1).IsPunctuator("{"):
                        kind = OperatorKind.Switch;
                        precedence = Precedence.Switch;
                        return true;
                }

                return false;

            case TokenKind.Identifier:
                if (token.IsContextual("with") && Peek(1).IsPunctuator("{"))
                {
                    kind = OperatorKind.With;
                    precedence = Precedence.Switch;
                    return true;
                }

                return false;

            case TokenKind.Punctuator:
                break;

            default:
                return false;
        }

        switch (token.Text)
        {
            case "||": op = BinaryOperator.Or; precedence = Precedence.ConditionalOr; return true;
            case "&&": op = BinaryOperator.And; precedence = Precedence.ConditionalAnd; return true;
            case "|": op = BinaryOperator.BitwiseOr; precedence = Precedence.LogicalOr; return true;
            case "^": op = BinaryOperator.BitwiseXor; precedence = Precedence.LogicalXor; return true;
            case "&": op = BinaryOperator.BitwiseAnd; precedence = Precedence.LogicalAnd; return true;
            case "==": op = BinaryOperator.Equal; precedence = Precedence.Equality; return true;
            case "!=": op = BinaryOperator.NotEqual; precedence = Precedence.Equality; return true;
            case "<": op = BinaryOperator.LessThan; precedence = Precedence.Relational; return true;
            case "<=": op = BinaryOperator.LessThanOrEqual; precedence = Precedence.Relational; return true;
            case ">=": op = BinaryOperator.GreaterThanOrEqual; precedence = Precedence.Relational; return true;
            case "<<": op = BinaryOperator.LeftShift; precedence = Precedence.Shift; return true;
            case "+": op = BinaryOperator.Add; precedence = Precedence.Additive; return true;
            case "-": op = BinaryOperator.Subtract; precedence = Precedence.Additive; return true;
            case "*": op = BinaryOperator.Multiply; precedence = Precedence.Multiplicative; return true;
            case "/": op = BinaryOperator.Divide; precedence = Precedence.Multiplicative; return true;
            case "%": op = BinaryOperator.Modulo; precedence = Precedence.Multiplicative; return true;
            case "..": kind = OperatorKind.Range; precedence = Precedence.Range; return true;
            case "??": kind = OperatorKind.Coalescing; op = BinaryOperator.NullCoalescing; precedence = Precedence.Coalescing; return true;
        }

        BinaryOperator? assignment = token.Text switch
        {
            "=" => BinaryOperator.Assign,
            "+=" => BinaryOperator.AddAssign,
            "-=" => BinaryOperator.SubtractAssign,
            "*=" => BinaryOperator.MultiplyAssign,
            "/=" => BinaryOperator.DivideAssign,
            "%=" => BinaryOperator.ModuloAssign,
            "&=" => BinaryOperator.BitwiseAndAssign,
            "|=" => BinaryOperator.BitwiseOrAssign,
            "^=" => BinaryOperator.BitwiseXorAssign,
            "<<=" => BinaryOperator.LeftShiftAssign,
            "??=" => BinaryOperator.NullCoalescingAssign,
            _ => null,
        };

        if (assignment.HasValue)
        {
            kind = OperatorKind.Assignment;
            op = assignment.Value;
            precedence = Precedence.Assignment;
            return true;
        }

        if (token.Text != ">")
        {
            return false;
        }

        // '>' tokens are composed only when no trivia separates them
        var second = Peek(1);
        if (AreAdjacent(token, second))
        {
            if (second.IsPunctuator(">="))
            {
                kind = OperatorKind.Assignment;
                op = BinaryOperator.RightShiftAssign;
                precedence = Precedence.Assignment;
                tokenCount = 2;
                return true;
            }

            if (second.IsPunctuator(">"))
            {
                var third = Peek(2);
                if (AreAdjacent(second, third))
                {
                    if (third.IsPunctuator(">="))
                    {
                        kind = OperatorKind.Assignment;
                        op = BinaryOperator.UnsignedRightShiftAssign;
                        precedence = Precedence.Assignment;
                        tokenCount = 3;
                        return true;
                    }

                    if (third.IsPunctuator(">"))
                    {
                        op = BinaryOperator.UnsignedRightShift;
                        precedence = Precedence.Shift;
                        tokenCount = 3;
                        return true;
                    }
                }

                op = BinaryOperator.RightShift;
                precedence = Precedence.Shift;
                tokenCount = 2;
                return true;
            }
        }

        op = BinaryOperator.GreaterThan;
        precedence = Precedence.Relational;
        return true;
    }

    /// <summary>
    /// A full expression inside brackets or a conditional branch, where lambdas are always allowed.
    /// </summary>
    private Expression ParseExpressionInNestedContext()
    {
        var noLambdaArrow = _noLambdaArrow;
        _noLambdaArrow = false;
        var expression = ParseExpression();
        _noLambdaArrow = noLambdaArrow;
        return expression;
    }

    // ========================================
    // Primary expressions
    // ========================================

    private Expression ParsePrimary()
    {
        var token = Current;
        switch (token.Kind)
        {
            case TokenKind.NumericLiteral:
            case TokenKind.CharacterLiteral:
            case TokenKind.StringLiteral:
            case TokenKind.InterpolatedString:
                EatToken();
                return Finish(token.Literal, token.Start);

            case TokenKind.Identifier:
                return ParseIdentifierExpression();

            case TokenKind.Keyword:
                return ParseKeywordExpression(token);

            case TokenKind.Punctuator:
                if (token.Text == "(")
                {
                    return ParseParenthesizedExpressionOrCast();
                }

                if (token.Text == "[")
                {
                    return ParseCollectionExpression();
                }

                return null;

            default:
                return null;
        }
    }

    private Expression ParseKeywordExpression(SyntaxToken token) => Finish(ParseKeywordExpressionCore(token), token.Start);

    private Expression ParseKeywordExpressionCore(SyntaxToken token)
    {
        switch (token.Text)
        {
            case "true":
                EatToken();
                return new LiteralExpression(true, LiteralKind.Boolean, "true");
            case "false":
                EatToken();
                return new LiteralExpression(false, LiteralKind.Boolean, "false");
            case "null":
                EatToken();
                return new LiteralExpression(null, LiteralKind.Null, "null");
            case "this":
                EatToken();
                return new ThisExpression();
            case "base":
                EatToken();
                return new BaseExpression();
            case "new":
                return ParseNewExpression();
            case "stackalloc":
                return ParseStackAllocExpression();
            case "delegate":
                return ParseAnonymousMethod([]);

            case "typeof":
            {
                EatToken();
                if (!TryEatPunctuator("("))
                {
                    return null;
                }

                var allowOmitted = _allowOmittedTypeArguments;
                _allowOmittedTypeArguments = true;
                var type = ParseType(TypeMode.Normal);
                _allowOmittedTypeArguments = allowOmitted;

                return type != null && TryEatPunctuator(")") ? new TypeOfExpression(type) : null;
            }

            case "sizeof":
            {
                EatToken();
                if (!TryEatPunctuator("("))
                {
                    return null;
                }

                var type = ParseType(TypeMode.Normal);
                return type != null && TryEatPunctuator(")") ? new SizeOfExpression(type) : null;
            }

            case "default":
            {
                EatToken();
                if (!TryEatPunctuator("("))
                {
                    return new DefaultExpression();
                }

                var type = ParseType(TypeMode.Normal);
                return type != null && TryEatPunctuator(")") ? new DefaultExpression(type) : null;
            }

            case "checked":
            case "unchecked":
            {
                EatToken();
                if (!TryEatPunctuator("("))
                {
                    return null;
                }

                var expression = ParseExpressionInNestedContext();
                return expression != null && TryEatPunctuator(")") ? new CheckedExpression(token.Text == "checked", expression) : null;
            }

            case "__arglist":
            {
                EatToken();
                if (!IsPunctuator("("))
                {
                    return new ArgListExpression();
                }

                var arguments = ParseArgumentList("(", ")");
                return arguments == null ? null : new ArgListExpression(arguments);
            }

            case "__makeref":
            case "__reftype":
            {
                EatToken();
                if (!TryEatPunctuator("("))
                {
                    return null;
                }

                var expression = ParseExpressionInNestedContext();
                if (expression == null || !TryEatPunctuator(")"))
                {
                    return null;
                }

                return token.Text == "__makeref" ? new MakeRefExpression(expression) : new RefTypeExpression(expression);
            }

            case "__refvalue":
            {
                EatToken();
                if (!TryEatPunctuator("("))
                {
                    return null;
                }

                var expression = ParseExpressionInNestedContext();
                if (expression == null || !TryEatPunctuator(","))
                {
                    return null;
                }

                var type = ParseType(TypeMode.Normal);
                return type != null && TryEatPunctuator(")") ? new RefValueExpression(expression, type) : null;
            }
        }

        if (TryGetPredefinedType(token.Text, out var predefined))
        {
            EatToken();
            return new PredefinedTypeExpression(predefined);
        }

        return null;
    }

    private Expression ParseIdentifierExpression()
    {
        var token = Current;

        // alias::Name
        if (Peek(1).IsPunctuator("::"))
        {
            EatToken();
            EatToken();
            if (!Current.IsIdentifier)
            {
                return null;
            }

            return Finish(new AliasQualifiedNameExpression(token.Text, ParseSimpleName()), token.Start);
        }

        // var (a, b) in a deconstruction
        if (token.IsContextual("var") && Peek(1).IsPunctuator("("))
        {
            var declaration = TryParseDeconstructionDeclaration();
            if (declaration != null)
            {
                return declaration;
            }
        }

        return ParseSimpleName();
    }

    /// <summary>
    /// Identifier, followed by type arguments when the token after them allows it: F&lt;int&gt;(x).
    /// </summary>
    private NameExpression ParseSimpleName()
    {
        var token = EatToken();
        var typeArguments = TryParseTypeArgumentsInExpression(out var closeDirectives);
        return Finish(new NameExpression([token.Text], typeArguments) { CloseAngleNullableDirectives = closeDirectives }, token.Start);
    }

    private List<TypeReference> TryParseTypeArgumentsInExpression(out IReadOnlyList<NullableDirective> closeDirectives)
    {
        closeDirectives = null;
        if (!IsPunctuator("<"))
        {
            return null;
        }

        var start = _position;
        var typeArguments = ParseTypeArgumentList(out closeDirectives);
        if (typeArguments != null && IsTypeArgumentFollow(Current))
        {
            return typeArguments;
        }

        _position = start;
        return null;
    }

    /// <summary>
    /// var (a, (b, _)) followed by '=' or 'in'.
    /// </summary>
    private Expression TryParseDeconstructionDeclaration()
    {
        var start = _position;
        var var = EatToken();

        var designation = ParseParenthesizedDesignation();
        if (designation != null && (IsPunctuator("=") || IsKeyword("in")))
        {
            return Finish(new DeclarationExpression(VarType(var), designation), var.Start);
        }

        _position = start;
        return null;
    }

    /// <summary>
    /// The type <c>var</c> of a deconstruction.
    /// </summary>
    private static NamedTypeReference VarType(SyntaxToken var)
    {
        var name = Finish(new NameExpression(["var"]), var.Start, var.End);
        return Finish(new NamedTypeReference(name), var.Start, var.End);
    }

    private ParenthesizedVariableDesignation ParseParenthesizedDesignation()
    {
        EnsureSufficientStack();
        var start = NodeStart;
        if (!TryEatPunctuator("("))
        {
            return null;
        }

        var variables = new List<VariableDesignation>();
        while (true)
        {
            VariableDesignation variable;
            if (IsPunctuator("("))
            {
                variable = ParseParenthesizedDesignation();
            }
            else
            {
                variable = ParseSingleDesignation();
            }

            if (variable == null)
            {
                return null;
            }

            variables.Add(variable);

            if (TryEatPunctuator(","))
            {
                continue;
            }

            return TryEatPunctuator(")") ? Finish(new ParenthesizedVariableDesignation(variables), start) : null;
        }
    }

    private VariableDesignation ParseSingleDesignation()
    {
        var start = NodeStart;
        var name = TryEatIdentifier();
        return name switch
        {
            null => null,
            "_" => Finish(new DiscardDesignation(), start),
            _ => Finish(new SingleVariableDesignation(name), start),
        };
    }

    /// <summary>
    /// A declaration expression: <c>var x</c>, <c>int x</c>, <c>var (a, b)</c>, followed by
    /// ',' or ')'. Restores the position and returns null otherwise.
    /// </summary>
    private DeclarationExpression TryParseDeclarationExpression()
    {
        var start = _position;
        var spanStart = NodeStart;

        if (IsContextual("var") && Peek(1).IsPunctuator("("))
        {
            var var = EatToken();
            var parenthesized = ParseParenthesizedDesignation();
            if (parenthesized != null && IsDeclarationExpressionFollow())
            {
                return Finish(new DeclarationExpression(VarType(var), parenthesized), spanStart);
            }

            _position = start;
            return null;
        }

        // 'a * b' and 'A<T>.B * c' are multiplications, not declarations of pointers
        var type = ParseLocalType();
        if (type != null && Current.IsIdentifier && !IsQueryKeyword(Current) && type is not PointerTypeReference { ElementType: NamedTypeReference { IsNullable: false } })
        {
            var designation = ParseSingleDesignation();
            if (IsDeclarationExpressionFollow())
            {
                return Finish(new DeclarationExpression(type, designation), spanStart);
            }
        }

        _position = start;
        return null;
    }

    /// <summary>
    /// Declaration expressions are arguments and tuple elements: ',' or ')' follows them.
    /// </summary>
    private bool IsDeclarationExpressionFollow()
    {
        var token = Current;
        return token.IsPunctuator(",") || token.IsPunctuator(")");
    }

    // ========================================
    // Postfix operators
    // ========================================

    private Expression ParsePostfix(Expression expression)
    {
        while (expression != null)
        {
            var token = Current;
            if (token.Kind != TokenKind.Punctuator)
            {
                return expression;
            }

            switch (token.Text)
            {
                case ".":
                case "->":
                {
                    if (!Peek(1).IsIdentifier)
                    {
                        return expression;
                    }

                    EatToken();
                    var name = EatToken().Text;
                    var typeArguments = TryParseTypeArgumentsInExpression(out var closeDirectives);
                    var memberAccess = new MemberAccessExpression(name, expression, false, typeArguments, token.Text == "->") { CloseAngleNullableDirectives = closeDirectives };
                    expression = Finish(memberAccess, expression);
                    break;
                }

                case "?":
                {
                    var next = Peek(1);
                    if (next.IsPunctuator(".") && Peek(2).IsIdentifier)
                    {
                        EatToken();
                        EatToken();
                        var name = EatToken().Text;
                        var typeArguments = TryParseTypeArgumentsInExpression(out var closeDirectives);
                        var memberAccess = new MemberAccessExpression(name, expression, true, typeArguments) { CloseAngleNullableDirectives = closeDirectives };
                        expression = Finish(memberAccess, expression);
                        break;
                    }

                    if (next.IsPunctuator("[") && IsConditionalElementAccess(token, next))
                    {
                        EatToken();
                        var arguments = ParseArgumentList("[", "]");
                        if (arguments == null)
                        {
                            return null;
                        }

                        expression = Finish(new ElementAccessExpression(expression, arguments, true), expression);
                        break;
                    }

                    return expression;
                }

                case "(":
                {
                    // nameof accepts unbound generic types: nameof(List<>)
                    var isNameOf = expression is NameExpression { Parts: ["nameof"], TypeArguments: null };
                    var allowOmitted = _allowOmittedTypeArguments;
                    _allowOmittedTypeArguments = isNameOf;
                    var arguments = ParseArgumentList("(", ")");
                    _allowOmittedTypeArguments = allowOmitted;
                    if (arguments == null)
                    {
                        return null;
                    }

                    expression = Finish<Expression>(
                        isNameOf && arguments is [{ Name: null, RefKind: RefKind.None } argument]
                            ? new NameOfExpression(argument.Expression)
                            : new InvocationExpression(expression, arguments),
                        expression);
                    break;
                }

                case "[":
                {
                    var arguments = ParseArgumentList("[", "]");
                    if (arguments == null)
                    {
                        return null;
                    }

                    expression = Finish(new ElementAccessExpression(expression, arguments), expression);
                    break;
                }

                case "++":
                    EatToken();
                    expression = Finish(new UnaryExpression(UnaryOperator.Increment, expression, isPrefix: false), expression);
                    break;

                case "--":
                    EatToken();
                    expression = Finish(new UnaryExpression(UnaryOperator.Decrement, expression, isPrefix: false), expression);
                    break;

                case "!":
                    EatToken();
                    expression = Finish(new UnaryExpression(UnaryOperator.NullForgiving, expression, isPrefix: false), expression);
                    break;

                default:
                    return expression;
            }
        }

        return null;
    }

    /// <summary>
    /// '?[' is a conditional element access unless it reads like 'c ? [collection] : other'.
    /// </summary>
    private bool IsConditionalElementAccess(SyntaxToken question, SyntaxToken bracket)
    {
        if (AreAdjacent(question, bracket))
        {
            return true;
        }

        var end = SkipBalanced(question.End);
        return end < 0 || !TokenAt(end).IsPunctuator(":");
    }

    /// <summary>
    /// open [Argument (',' Argument)*] close
    /// </summary>
    private List<Argument> ParseArgumentList(string open, string close)
    {
        if (!TryEatPunctuator(open))
        {
            return null;
        }

        var arguments = new List<Argument>();
        if (TryEatPunctuator(close))
        {
            return arguments;
        }

        var noLambdaArrow = _noLambdaArrow;
        _noLambdaArrow = false;
        try
        {
            while (true)
            {
                var argument = ParseArgument();
                if (argument == null)
                {
                    return null;
                }

                arguments.Add(argument);

                if (TryEatPunctuator(","))
                {
                    continue;
                }

                return TryEatPunctuator(close) ? arguments : null;
            }
        }
        finally
        {
            _noLambdaArrow = noLambdaArrow;
        }
    }

    private Argument ParseArgument()
    {
        var start = NodeStart;
        string name = null;
        if (Current.IsIdentifier && Peek(1).IsPunctuator(":"))
        {
            name = EatToken().Text;
            EatToken();
        }

        var refKind = RefKind.None;
        if (TryEatKeyword("ref"))
        {
            refKind = RefKind.Ref;
        }
        else if (TryEatKeyword("out"))
        {
            refKind = RefKind.Out;
        }
        else if (TryEatKeyword("in"))
        {
            refKind = RefKind.In;
        }

        Expression expression = null;
        if (refKind != RefKind.None)
        {
            expression = TryParseDeclarationExpression();
        }

        expression ??= ParseExpression();
        return expression == null ? null : Finish(new Argument(expression, name, refKind), start);
    }

    // ========================================
    // Parentheses: casts, parenthesized expressions and tuples
    // ========================================

    private Expression ParseParenthesizedExpressionOrCast()
    {
        var start = _position;
        var spanStart = EatToken().Start;

        var type = ParseType(TypeMode.Normal);
        if (type != null && IsPunctuator(")") && IsCastFollow(type, Peek(1)))
        {
            EatToken();
            var operand = ParseSubExpression(Precedence.Cast);
            if (operand != null)
            {
                return Finish(new CastExpression(type, operand), spanStart);
            }
        }

        _position = start;
        EatToken();

        var noLambdaArrow = _noLambdaArrow;
        _noLambdaArrow = false;
        try
        {
            var firstStart = NodeStart;
            var first = ParseTupleElement(out var firstName, out var isDeclaration);
            if (first == null)
            {
                return null;
            }

            if (TryEatPunctuator(")"))
            {
                return firstName == null && !isDeclaration ? ParsePostfix(Finish(new ParenthesizedExpression(first), spanStart)) : null;
            }

            var elements = new List<TupleExpressionElement> { Finish(new TupleExpressionElement(first, firstName), firstStart) };
            while (TryEatPunctuator(","))
            {
                var elementStart = NodeStart;
                var element = ParseTupleElement(out var name, out _);
                if (element == null)
                {
                    return null;
                }

                elements.Add(Finish(new TupleExpressionElement(element, name), elementStart));
            }

            return TryEatPunctuator(")") ? ParsePostfix(Finish(new TupleExpression(elements), spanStart)) : null;
        }
        finally
        {
            _noLambdaArrow = noLambdaArrow;
        }
    }

    private Expression ParseTupleElement(out string name, out bool isDeclaration)
    {
        name = null;
        if (Current.IsIdentifier && Peek(1).IsPunctuator(":"))
        {
            name = EatToken().Text;
            EatToken();
        }

        var declaration = TryParseDeclarationExpression();
        isDeclaration = declaration != null;
        return declaration ?? ParseExpression();
    }

    /// <summary>
    /// Whether '(' type ')' followed by <paramref name="next"/> is a cast (C# specification, cast expressions):
    /// a type that cannot be an expression makes it a cast before any operand; a name only before
    /// an identifier, a literal, '(', '!', '~' or a keyword that starts an expression.
    /// </summary>
    private bool IsCastFollow(TypeReference type, SyntaxToken next)
    {
        if (IsDefinitelyType(type))
        {
            return CanStartExpression(next) && !(next.Kind == TokenKind.Keyword && next.Text is "is" or "as" or "switch");
        }

        switch (next.Kind)
        {
            case TokenKind.Identifier:
                if (IsQueryKeyword(next))
                {
                    return false;
                }

                if (next.IsContextual("with") && TokenAt(next.End).IsPunctuator("{"))
                {
                    return false;
                }

                return true;
            case TokenKind.NumericLiteral:
            case TokenKind.CharacterLiteral:
            case TokenKind.StringLiteral:
            case TokenKind.InterpolatedString:
                return true;
            case TokenKind.Keyword:
                return next.Text is not ("is" or "as" or "switch") && CanStartExpression(next);
            case TokenKind.Punctuator:
                return next.Text is "(" or "!" or "~";
            default:
                return false;
        }
    }

    // ========================================
    // Object, array and collection creation
    // ========================================

    private Expression ParseNewExpression()
    {
        var start = NodeStart;
        return Finish(ParseNewExpressionCore(), start);
    }

    private Expression ParseNewExpressionCore()
    {
        EatToken();

        // An array of tuples: new (int, string)[n]
        if (IsPunctuator("("))
        {
            var start = _position;
            var tupleType = ParseType(TypeMode.Normal, allowRanks: false);
            if (tupleType is TupleTypeReference or NullableTypeReference && IsPunctuator("["))
            {
                return ParseArrayCreationRest(tupleType);
            }

            _position = start;
        }

        // Target-typed: new(args) { ... }
        if (IsPunctuator("("))
        {
            var arguments = ParseArgumentList("(", ")");
            if (arguments == null)
            {
                return null;
            }

            InitializerExpression initializer = null;
            if (IsPunctuator("{"))
            {
                initializer = ParseObjectOrCollectionInitializer();
                if (initializer == null)
                {
                    return null;
                }
            }

            return new ObjectCreationExpression(null, arguments, initializer);
        }

        // Anonymous object: new { A = 1, b }
        if (IsPunctuator("{"))
        {
            return ParseAnonymousObject();
        }

        // Implicitly typed array: new[] { ... }
        if (IsPunctuator("["))
        {
            var rank = ParseRankSpecifier();
            if (rank == 0 || !IsPunctuator("{"))
            {
                return null;
            }

            var initializer = ParseArrayInitializer();
            return initializer == null ? null : new ImplicitArrayCreationExpression(initializer, rank);
        }

        var type = ParseType(TypeMode.Normal, allowRanks: false);
        if (type == null)
        {
            return null;
        }

        // Arrays of nullable arrays: new byte[]?[4]
        while (IsRankSpecifierAhead())
        {
            var beforeRank = _position;
            var rank = ParseRankSpecifier();
            var rankEnd = _position;
            if (rank == 0 || !TryEatPunctuator("?"))
            {
                _position = beforeRank;
                break;
            }

            var arrayType = Finish(new ArrayTypeReference(type, rank), type.Span.Start, rankEnd);
            type = Finish(new NullableTypeReference(arrayType), type);
        }

        if (IsPunctuator("["))
        {
            return ParseArrayCreationRest(type);
        }

        List<Argument> constructorArguments = null;
        if (IsPunctuator("("))
        {
            constructorArguments = ParseArgumentList("(", ")");
            if (constructorArguments == null)
            {
                return null;
            }
        }

        InitializerExpression objectInitializer = null;
        if (IsPunctuator("{"))
        {
            objectInitializer = ParseObjectOrCollectionInitializer();
            if (objectInitializer == null)
            {
                return null;
            }
        }

        if (constructorArguments == null && objectInitializer == null)
        {
            return null;
        }

        return new ObjectCreationExpression(type, constructorArguments, objectInitializer);
    }

    private Expression ParseArrayCreationRest(TypeReference elementType)
    {
        var sizes = new List<Expression>();
        if (IsRankSpecifierAhead())
        {
            var rank = ParseRankSpecifier();
            if (rank == 0)
            {
                return null;
            }

            for (var i = 0; i < rank; i++)
            {
                sizes.Add(null);
            }
        }
        else
        {
            EatToken();
            while (true)
            {
                var size = ParseExpressionInNestedContext();
                if (size == null)
                {
                    return null;
                }

                sizes.Add(size);
                if (TryEatPunctuator(","))
                {
                    continue;
                }

                if (!TryEatPunctuator("]"))
                {
                    return null;
                }

                break;
            }
        }

        List<int> additionalRanks = null;
        while (IsRankSpecifierAhead())
        {
            var rank = ParseRankSpecifier();
            if (rank == 0)
            {
                return null;
            }

            (additionalRanks ??= []).Add(rank);
        }

        InitializerExpression initializer = null;
        if (IsPunctuator("{"))
        {
            initializer = ParseArrayInitializer();
            if (initializer == null)
            {
                return null;
            }
        }
        else if (sizes[0] == null)
        {
            return null;
        }

        return new ArrayCreationExpression(elementType, sizes, initializer, additionalRanks);
    }

    private Expression ParseStackAllocExpression()
    {
        var start = NodeStart;
        return Finish(ParseStackAllocExpressionCore(), start);
    }

    private Expression ParseStackAllocExpressionCore()
    {
        EatToken();

        if (IsPunctuator("["))
        {
            if (!Peek(1).IsPunctuator("]"))
            {
                return null;
            }

            EatToken();
            EatToken();
            var implicitInitializer = ParseArrayInitializer();
            return implicitInitializer == null ? null : new StackAllocExpression(null, null, implicitInitializer);
        }

        var type = ParseType(TypeMode.Normal, allowRanks: false);
        if (type == null || !TryEatPunctuator("["))
        {
            return null;
        }

        Expression size = null;
        if (!IsPunctuator("]"))
        {
            size = ParseExpressionInNestedContext();
            if (size == null)
            {
                return null;
            }
        }

        if (!TryEatPunctuator("]"))
        {
            return null;
        }

        InitializerExpression initializer = null;
        if (IsPunctuator("{"))
        {
            initializer = ParseArrayInitializer();
            if (initializer == null)
            {
                return null;
            }
        }

        return new StackAllocExpression(type, size, initializer);
    }

    /// <summary>
    /// An initializer after 'new T' or 'with': object members (A = 1, [i] = 2) or collection elements.
    /// </summary>
    private InitializerExpression ParseObjectOrCollectionInitializer()
    {
        if (!IsPunctuator("{"))
        {
            return null;
        }

        var kind = IsObjectInitializerMemberStart(Peek(1), 1) || Peek(1).IsPunctuator("}")
            ? InitializerKind.Object
            : InitializerKind.Collection;

        return ParseInitializer(kind, kind == InitializerKind.Object ? ParseObjectInitializerMember : ParseCollectionElement);
    }

    /// <summary>
    /// True when the token at <paramref name="offset"/> starts 'Name =' or '[arguments] ='.
    /// </summary>
    private bool IsObjectInitializerMemberStart(SyntaxToken token, int offset)
    {
        if (token.IsIdentifier)
        {
            return Peek(offset + 1).IsPunctuator("=");
        }

        if (token.IsPunctuator("["))
        {
            var end = SkipBalanced(token.Start);
            return end >= 0 && TokenAt(end).IsPunctuator("=");
        }

        return false;
    }

    private delegate Expression ElementParser(ref SyntaxParser parser);

    private static Expression ParseObjectInitializerMember(ref SyntaxParser parser)
    {
        var start = parser.NodeStart;
        Expression target;
        if (parser.IsPunctuator("["))
        {
            var arguments = parser.ParseArgumentList("[", "]");
            if (arguments == null)
            {
                return null;
            }

            target = parser.Finish(new ImplicitElementAccessExpression(arguments), start);
        }
        else
        {
            var name = parser.TryEatIdentifier();
            if (name == null)
            {
                return null;
            }

            target = parser.Finish(new NameExpression([name]), start);
        }

        if (!parser.TryEatPunctuator("="))
        {
            return null;
        }

        var value = parser.IsPunctuator("{") ? parser.ParseObjectOrCollectionInitializer() : parser.ParseExpression();
        return value == null ? null : parser.Finish(new BinaryExpression(target, BinaryOperator.Assign, value), start);
    }

    private static Expression ParseCollectionElement(ref SyntaxParser parser)
    {
        if (parser.IsPunctuator("{"))
        {
            return parser.ParseInitializer(InitializerKind.ComplexElement, ParseExpressionElement);
        }

        return parser.ParseExpression();
    }

    private static Expression ParseExpressionElement(ref SyntaxParser parser) => parser.ParseExpression();

    private static Expression ParseArrayElement(ref SyntaxParser parser)
    {
        if (parser.IsPunctuator("{"))
        {
            return parser.ParseArrayInitializer();
        }

        return parser.ParseExpression();
    }

    private InitializerExpression ParseArrayInitializer() => ParseInitializer(InitializerKind.Array, ParseArrayElement);

    /// <summary>
    /// '{' [element (',' element)* [',']] '}'
    /// </summary>
    private InitializerExpression ParseInitializer(InitializerKind kind, ElementParser element)
    {
        // Initializers nest without expressions between them: new C { A = { B = { } } }
        EnsureSufficientStack();
        var start = NodeStart;
        if (!TryEatPunctuator("{"))
        {
            return null;
        }

        var noLambdaArrow = _noLambdaArrow;
        _noLambdaArrow = false;
        try
        {
            var expressions = new List<Expression>();
            var hasTrailingComma = false;
            while (!IsPunctuator("}"))
            {
                var expression = element(ref this);
                if (expression == null)
                {
                    return null;
                }

                expressions.Add(expression);

                if (!TryEatPunctuator(","))
                {
                    break;
                }

                hasTrailingComma = IsPunctuator("}");
            }

            return TryEatPunctuator("}") ? Finish(new InitializerExpression(kind, expressions, hasTrailingComma), start) : null;
        }
        finally
        {
            _noLambdaArrow = noLambdaArrow;
        }
    }

    private Expression ParseAnonymousObject()
    {
        EatToken();
        var members = new List<AnonymousObjectMember>();
        var hasTrailingComma = false;
        while (!IsPunctuator("}"))
        {
            var memberStart = NodeStart;
            string name = null;
            if (Current.IsIdentifier && Peek(1).IsPunctuator("="))
            {
                name = EatToken().Text;
                EatToken();
            }

            var expression = ParseExpressionInNestedContext();
            if (expression == null)
            {
                return null;
            }

            members.Add(Finish(new AnonymousObjectMember(expression, name), memberStart));

            if (!TryEatPunctuator(","))
            {
                break;
            }

            hasTrailingComma = IsPunctuator("}");
        }

        return TryEatPunctuator("}") ? new AnonymousObjectCreationExpression(members, hasTrailingComma) : null;
    }

    /// <summary>
    /// '[' [element (',' element)* [',']] ']' where an element is an expression or '..' expression.
    /// </summary>
    private Expression ParseCollectionExpression()
    {
        var start = EatToken().Start;

        var noLambdaArrow = _noLambdaArrow;
        _noLambdaArrow = false;
        try
        {
            var elements = new List<Expression>();
            var hasTrailingComma = false;
            while (!IsPunctuator("]"))
            {
                Expression element;
                var elementStart = NodeStart;
                if (TryEatPunctuator(".."))
                {
                    var spread = ParseExpression();
                    element = spread == null ? null : Finish(new SpreadElement(spread), elementStart);
                }
                else
                {
                    element = ParseExpression();
                }

                if (element == null)
                {
                    return null;
                }

                elements.Add(element);

                if (!TryEatPunctuator(","))
                {
                    break;
                }

                hasTrailingComma = IsPunctuator("]");
            }

            return TryEatPunctuator("]") ? Finish(new CollectionExpression(elements, hasTrailingComma), start) : null;
        }
        finally
        {
            _noLambdaArrow = noLambdaArrow;
        }
    }

    // ========================================
    // Switch expressions
    // ========================================

    private Expression ParseSwitchExpression(Expression governing)
    {
        if (!TryEatPunctuator("{"))
        {
            return null;
        }

        var noLambdaArrow = _noLambdaArrow;
        try
        {
            var arms = new List<SwitchExpressionArm>();
            var hasTrailingComma = false;
            while (!IsPunctuator("}"))
            {
                _noLambdaArrow = false;
                var armStart = NodeStart;
                var pattern = ParsePattern();
                if (pattern == null)
                {
                    return null;
                }

                Expression guard = null;
                if (TryEatContextual("when"))
                {
                    _noLambdaArrow = true;
                    guard = ParseExpression();
                    _noLambdaArrow = false;
                    if (guard == null)
                    {
                        return null;
                    }
                }

                if (!TryEatPunctuator("=>"))
                {
                    return null;
                }

                var expression = ParseExpression();
                if (expression == null)
                {
                    return null;
                }

                arms.Add(Finish(new SwitchExpressionArm(pattern, expression, guard), armStart));

                if (!TryEatPunctuator(","))
                {
                    break;
                }

                hasTrailingComma = IsPunctuator("}");
            }

            return TryEatPunctuator("}") ? Finish(new SwitchExpression(governing, arms, hasTrailingComma), governing) : null;
        }
        finally
        {
            _noLambdaArrow = noLambdaArrow;
        }
    }

    // ========================================
    // Lambdas and anonymous methods
    // ========================================

    /// <summary>
    /// Looks ahead for a lambda: [attributes] [static | async]* then 'x =&gt;', '(...) =&gt;',
    /// 'ReturnType (...) =&gt;' or an anonymous method. Does not move the position.
    /// </summary>
    private bool IsLambdaStart(out int start)
    {
        start = _position;
        var token = Current;

        // Cheap filter: lambdas start with one of these tokens
        var candidate = token.Kind switch
        {
            TokenKind.Identifier => true,
            TokenKind.Keyword => token.Text is "static" or "delegate" or "ref" || IsPredefinedTypeKeyword(token),
            TokenKind.Punctuator => token.Text is "(" or "[",
            _ => false,
        };

        if (!candidate)
        {
            return false;
        }

        var isLambda = ScanLambda();
        _position = start;
        return isLambda;
    }

    private bool ScanLambda()
    {
        var hasPrefix = false;

        if (IsPunctuator("["))
        {
            var attributes = ParseAttributeSections();
            if (attributes == null || attributes.Count == 0)
            {
                return false;
            }

            hasPrefix = true;
        }

        while (true)
        {
            if (IsKeyword("static") || (IsContextual("async") && !Peek(1).IsPunctuator("=>") && !Peek(1).IsPunctuator(".")))
            {
                EatToken();
                hasPrefix = true;
                continue;
            }

            break;
        }

        if (IsKeyword("delegate") && (Peek(1).IsPunctuator("(") || Peek(1).IsPunctuator("{")))
        {
            return hasPrefix;
        }

        if (Current.IsIdentifier && Peek(1).IsPunctuator("=>"))
        {
            return !_noLambdaArrow || hasPrefix;
        }

        if (IsPunctuator("("))
        {
            var end = SkipBalanced(_position);
            return end >= 0 && TokenAt(end).IsPunctuator("=>") && (!_noLambdaArrow || hasPrefix);
        }

        // Explicit return type: int (string s) => s.Length. A nullable name reads as a conditional:
        // c ? () => a : b
        if (!CanStartReturnTypeOfLambda())
        {
            return false;
        }

        var returnType = ParseReturnType();
        if (returnType != null && IsPunctuator("(") && returnType is not NamedTypeReference { IsNullable: true, TypeArguments: null, Qualifier: null, Alias: null })
        {
            var end = SkipBalanced(_position);
            return end >= 0 && TokenAt(end).IsPunctuator("=>") && (!_noLambdaArrow || hasPrefix);
        }

        return false;
    }

    /// <summary>
    /// Cheap check before parsing a type in <see cref="ScanLambda"/>, which runs for most expressions: a
    /// dotted name or a predefined type is the return type of a lambda only when '(' or a token that
    /// continues a type follows it.
    /// </summary>
    private bool CanStartReturnTypeOfLambda()
    {
        var next = TokenAfterSimpleType(out var isSimpleType);
        return !isSimpleType || next.IsPunctuator("(") || IsTypeContinuation(next);
    }

    /// <summary>
    /// When the input starts with a dotted name or a predefined type, the token after it; lets callers skip
    /// parsing a type that cannot be followed by what they look for. Does not move the position.
    /// </summary>
    private SyntaxToken TokenAfterSimpleType(out bool isSimpleType)
    {
        var token = Current;
        isSimpleType = true;
        if (token.IsIdentifier)
        {
            token = TokenAt(token.End);
            while (token.IsPunctuator(".") && TokenAt(token.End).IsIdentifier)
            {
                token = TokenAt(TokenAt(token.End).End);
            }

            return token;
        }

        if (IsPredefinedTypeKeyword(token))
        {
            return TokenAt(token.End);
        }

        isSimpleType = false;
        return token;
    }

    /// <summary>
    /// Punctuators that continue a type after a name: type arguments, alias qualifier, nullable, array, pointer.
    /// </summary>
    private static bool IsTypeContinuation(SyntaxToken token) => token.Kind == TokenKind.Punctuator && token.Text is "<" or "::" or "?" or "[" or "*";

    private Expression ParseLambda(int start)
    {
        _position = start;
        var spanStart = NodeStart;
        return Finish(ParseLambdaCore(), spanStart);
    }

    private Expression ParseLambdaCore()
    {
        List<AttributeSection> attributes = null;
        if (IsPunctuator("["))
        {
            attributes = ParseAttributeSections();
        }

        var modifiers = new List<Modifiers>();
        while (true)
        {
            if (TryEatKeyword("static"))
            {
                modifiers.Add(Modifiers.Static);
                continue;
            }

            if (IsContextual("async") && !Peek(1).IsPunctuator("=>") && !Peek(1).IsPunctuator("."))
            {
                EatToken();
                modifiers.Add(Modifiers.Async);
                continue;
            }

            break;
        }

        if (IsKeyword("delegate"))
        {
            return ParseAnonymousMethod(modifiers);
        }

        var isAsync = modifiers.Contains(Modifiers.Async);

        // x => ...
        if (Current.IsIdentifier && Peek(1).IsPunctuator("=>"))
        {
            var name = EatToken();
            var parameter = Finish(new Parameter(null, name.Text), name.Start, name.End);
            EatToken();
            var simpleBody = ParseLambdaBody();
            return simpleBody == null
                ? null
                : new LambdaExpression(simpleBody, [parameter], isAsync, modifiers, null, attributes, hasParenthesizedParameters: false);
        }

        TypeReference returnType = null;
        if (!IsPunctuator("("))
        {
            returnType = ParseReturnType();
            if (returnType == null)
            {
                return null;
            }
        }

        var parameters = ParseParameterList(allowImplicitTypes: true);
        if (parameters == null || !TryEatPunctuator("=>"))
        {
            return null;
        }

        var body = ParseLambdaBody();
        return body == null
            ? null
            : new LambdaExpression(body, parameters, isAsync, modifiers, returnType, attributes, hasParenthesizedParameters: true);
    }

    private LambdaBody ParseLambdaBody()
    {
        var start = NodeStart;
        if (IsPunctuator("{"))
        {
            var block = ParseEmbeddedBlock();
            return block == null ? null : Finish(new BlockLambdaBody(block), start);
        }

        var expression = ParseExpression();
        return expression == null ? null : Finish(new ExpressionLambdaBody(expression), start);
    }

    /// <summary>
    /// delegate ['(' parameters ')'] block
    /// </summary>
    private Expression ParseAnonymousMethod(List<Modifiers> modifiers)
    {
        // Lambda modifiers are part of the span set by ParseLambda
        var start = EatToken().Start;

        List<Parameter> parameters = null;
        if (IsPunctuator("("))
        {
            parameters = ParseParameterList(allowImplicitTypes: false);
            if (parameters == null)
            {
                return null;
            }
        }

        if (!IsPunctuator("{"))
        {
            return null;
        }

        var block = ParseEmbeddedBlock();
        return block == null ? null : Finish(new AnonymousMethodExpression(block, parameters, modifiers), start);
    }

    // ========================================
    // Query expressions
    // ========================================

    private static readonly HashSet<string> QueryKeywords =
    [
        "from", "where", "select", "group", "into", "orderby", "join", "let", "on", "equals", "by", "ascending", "descending",
    ];

    /// <summary>
    /// Inside a query, its contextual keywords end expressions instead of being identifiers.
    /// </summary>
    private readonly bool IsQueryKeyword(SyntaxToken token) => _queryDepth > 0 && token.IsIdentifier && !token.IsVerbatim && QueryKeywords.Contains(token.Text);

    /// <summary>
    /// 'from' starts a query when followed by 'identifier in' or 'Type identifier in'.
    /// </summary>
    private bool IsQueryExpressionStart()
    {
        if (!IsContextual("from"))
        {
            return false;
        }

        var next = Peek(1);
        if (next.IsIdentifier && Peek(2).IsKeyword("in"))
        {
            return true;
        }

        if (!next.IsIdentifier && !IsPredefinedTypeKeyword(next) && !next.IsPunctuator("("))
        {
            return false;
        }

        var start = _position;
        EatToken();
        var type = ParseType(TypeMode.Normal);
        var isQuery = type != null && Current.IsIdentifier && Peek(1).IsKeyword("in");
        _position = start;
        return isQuery;
    }

    private Expression ParseQueryExpression()
    {
        _queryDepth++;
        try
        {
            var start = NodeStart;
            var from = ParseFromClause();
            if (from == null)
            {
                return null;
            }

            var body = ParseQueryBody(out var selectOrGroup, out var continuation);
            return body == null ? null : Finish(new QueryExpression(from, body, selectOrGroup, continuation), start);
        }
        finally
        {
            _queryDepth--;
        }
    }

    /// <summary>
    /// from [Type] identifier in expression
    /// </summary>
    private FromClause ParseFromClause()
    {
        var start = EatToken().Start;

        TypeReference type = null;
        if (!(Current.IsIdentifier && Peek(1).IsKeyword("in")))
        {
            type = ParseType(TypeMode.Normal);
            if (type == null)
            {
                return null;
            }
        }

        var identifier = TryEatIdentifier();
        if (identifier == null || !TryEatKeyword("in"))
        {
            return null;
        }

        var expression = ParseExpression();
        return expression == null ? null : Finish(new FromClause(identifier, expression, type), start);
    }

    private List<QueryClause> ParseQueryBody(out SelectOrGroupClause selectOrGroup, out QueryContinuation continuation)
    {
        EnsureSufficientStack();
        selectOrGroup = null;
        continuation = null;

        var clauses = new List<QueryClause>();
        while (true)
        {
            QueryClause clause;
            var clauseStart = NodeStart;
            if (IsContextual("from"))
            {
                clause = ParseFromClause();
            }
            else if (IsContextual("let"))
            {
                EatToken();
                var identifier = TryEatIdentifier();
                if (identifier == null || !TryEatPunctuator("="))
                {
                    return null;
                }

                var expression = ParseExpression();
                clause = expression == null ? null : Finish(new LetClause(identifier, expression), clauseStart);
            }
            else if (IsContextual("where"))
            {
                EatToken();
                var condition = ParseExpression();
                clause = condition == null ? null : Finish(new WhereClause(condition), clauseStart);
            }
            else if (IsContextual("join"))
            {
                clause = ParseJoinClause();
            }
            else if (IsContextual("orderby"))
            {
                clause = ParseOrderByClause();
            }
            else
            {
                break;
            }

            if (clause == null)
            {
                return null;
            }

            clauses.Add(clause);
        }

        var selectStart = NodeStart;
        if (TryEatContextual("select"))
        {
            var expression = ParseExpression();
            if (expression == null)
            {
                return null;
            }

            selectOrGroup = Finish(new SelectClause(expression), selectStart);
        }
        else if (TryEatContextual("group"))
        {
            var grouped = ParseExpression();
            if (grouped == null || !TryEatContextual("by"))
            {
                return null;
            }

            var key = ParseExpression();
            if (key == null)
            {
                return null;
            }

            selectOrGroup = Finish(new GroupClause(grouped, key), selectStart);
        }
        else
        {
            return null;
        }

        var intoStart = NodeStart;
        if (TryEatContextual("into"))
        {
            var identifier = TryEatIdentifier();
            if (identifier == null)
            {
                return null;
            }

            var body = ParseQueryBody(out var continuationSelect, out var nested);
            if (body == null)
            {
                return null;
            }

            continuation = Finish(new QueryContinuation(identifier, body, continuationSelect, nested), intoStart);
        }

        return clauses;
    }

    /// <summary>
    /// join [Type] identifier in expression on expression equals expression [into identifier]
    /// </summary>
    private QueryClause ParseJoinClause()
    {
        var start = EatToken().Start;

        TypeReference type = null;
        if (!(Current.IsIdentifier && Peek(1).IsKeyword("in")))
        {
            type = ParseType(TypeMode.Normal);
            if (type == null)
            {
                return null;
            }
        }

        var identifier = TryEatIdentifier();
        if (identifier == null || !TryEatKeyword("in"))
        {
            return null;
        }

        var inExpression = ParseExpression();
        if (inExpression == null || !TryEatContextual("on"))
        {
            return null;
        }

        var left = ParseExpression();
        if (left == null || !TryEatContextual("equals"))
        {
            return null;
        }

        var right = ParseExpression();
        if (right == null)
        {
            return null;
        }

        string into = null;
        if (TryEatContextual("into"))
        {
            into = TryEatIdentifier();
            if (into == null)
            {
                return null;
            }
        }

        return Finish(new JoinClause(identifier, inExpression, left, right, type, into), start);
    }

    private QueryClause ParseOrderByClause()
    {
        var start = EatToken().Start;

        var orderings = new List<Ordering>();
        while (true)
        {
            var orderingStart = NodeStart;
            var expression = ParseExpression();
            if (expression == null)
            {
                return null;
            }

            var direction = OrderDirection.Ascending;
            var isExplicit = false;
            if (TryEatContextual("ascending"))
            {
                isExplicit = true;
            }
            else if (TryEatContextual("descending"))
            {
                direction = OrderDirection.Descending;
                isExplicit = true;
            }

            orderings.Add(Finish(new Ordering(expression, direction, isExplicit), orderingStart));

            if (!TryEatPunctuator(","))
            {
                return Finish(new OrderByClause(orderings), start);
            }
        }
    }
}
