namespace PaspanParsers.Cpp;

// Expressions ([expr]): precedence climbing over the binary operators, unary expressions and casts,
// postfix expressions and primary expressions.
internal ref partial struct SyntaxParser
{
    // Binary operator precedences, from the comma operator to the pointer-to-member operators
    private const int CommaPrecedence = 1;
    private const int AssignmentPrecedence = 2;
    private const int LogicalOrPrecedence = 4;

    /// <summary>
    /// expression: assignment expressions separated by the comma operator. A comma followed by '...' ends
    /// the operand of a fold expression: <c>(xs , ...)</c>.
    /// </summary>
    private Expression ParseExpression()
    {
        var left = ParseAssignmentExpression();
        while (left != null && IsPunctuator(",") && !Peek(1).IsPunctuator("..."))
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
    /// assignment-expression: a conditional expression, an assignment (right-associative, the right operand
    /// may be a braced-init-list), <c>throw</c> or <c>co_yield</c>.
    /// </summary>
    private Expression ParseAssignmentExpression()
    {
        EnsureSufficientStack();
        var start = NodeStart;

        if (TryEatKeyword("throw"))
        {
            Expression operand = null;
            if (!EndsThrowOperand(Current))
            {
                operand = ParseAssignmentExpression();
                if (operand == null)
                {
                    return null;
                }
            }

            return Finish(new ThrowExpression(operand), start);
        }

        if (TryEatKeyword("co_yield"))
        {
            var operand = ParseInitializerClause();
            return operand == null ? null : Finish(new YieldExpression(operand), start);
        }

        var left = ParseBinaryExpression(LogicalOrPrecedence);
        if (left == null)
        {
            return null;
        }

        if (IsPunctuator("?"))
        {
            return ParseConditionalRest(left);
        }

        var (@operator, precedence, tokens) = PeekBinaryOperator();
        if (precedence == AssignmentPrecedence && !ClosesTemplateArguments(@operator) && !Peek(tokens).IsPunctuator("..."))
        {
            EatTokens(tokens);
            var right = ParseInitializerClause();
            return right == null ? null : Finish(new BinaryExpression(left, @operator, right), left);
        }

        return left;
    }

    /// <summary>
    /// conditional-expression: a logical-or expression, or <c>condition ? a : b</c>. The width of a bit-field
    /// is one: in <c>int x : 3 = 1;</c> the <c>= 1</c> is the initializer.
    /// </summary>
    private Expression ParseConditionalExpression()
    {
        var left = ParseBinaryExpression(LogicalOrPrecedence);
        return left != null && IsPunctuator("?") ? ParseConditionalRest(left) : left;
    }

    /// <summary>
    /// <c>? a : b</c> after the condition <paramref name="condition"/>.
    /// </summary>
    private Expression ParseConditionalRest(Expression condition)
    {
        EatToken();

        // The GNU conditional a ?: b has no middle operand
        Expression whenTrue = null;
        if (!IsPunctuator(":"))
        {
            whenTrue = ParseExpression();
            if (whenTrue == null)
            {
                return null;
            }
        }

        if (!TryEatPunctuator(":"))
        {
            return null;
        }

        var whenFalse = ParseAssignmentExpression();
        return whenFalse == null ? null : Finish(new ConditionalExpression(condition, whenTrue, whenFalse), condition);
    }

    /// <summary>
    /// The token ends a <c>throw</c> without an operand.
    /// </summary>
    private static bool EndsThrowOperand(SyntaxToken token)
    {
        return token.Kind == TokenKind.EndOfFile
            || (token.Kind == TokenKind.Punctuator && token.Text is ")" or ";" or "," or ":" or "]" or "}" or ">");
    }

    /// <summary>
    /// Binary operators with precedence <paramref name="minPrecedence"/> and higher, left-associative. An
    /// operator followed by '...' ends the operand of a fold expression: <c>(xs + ... + 0)</c>.
    /// </summary>
    private Expression ParseBinaryExpression(int minPrecedence)
    {
        var left = ParseCastExpression();
        while (left != null)
        {
            var (@operator, precedence, tokens) = PeekBinaryOperator();
            if (precedence < minPrecedence || precedence < LogicalOrPrecedence || ClosesTemplateArguments(@operator)
                || Peek(tokens).IsPunctuator("..."))
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

    /// <summary>
    /// In template arguments, the first '&gt;' of <paramref name="operator"/> closes them: <c>&gt;</c>,
    /// <c>&gt;&gt;</c> and <c>&gt;&gt;=</c> are not operators there, <c>&gt;=</c> is.
    /// </summary>
    private readonly bool ClosesTemplateArguments(string @operator)
    {
        return _inTemplateArguments && @operator is ">" or ">>" or ">>=";
    }

    private void EatTokens(int count)
    {
        for (var i = 0; i < count; i++)
        {
            EatToken();
        }
    }

    // ========================================
    // Brackets
    // ========================================

    /// <summary>
    /// Enters parentheses, brackets or braces: a '&gt;' inside them compares, and a name followed by '{'
    /// may start a functional cast. Returns the state to restore with <see cref="LeaveBrackets"/>.
    /// </summary>
    private (bool InTemplateArguments, bool InConstraint) EnterBrackets()
    {
        var saved = (_inTemplateArguments, _inConstraint);
        _inTemplateArguments = false;
        _inConstraint = false;
        return saved;
    }

    private void LeaveBrackets((bool InTemplateArguments, bool InConstraint) saved)
    {
        (_inTemplateArguments, _inConstraint) = saved;
    }

    /// <summary>
    /// initializer-clause: an assignment expression or a braced-init-list.
    /// </summary>
    private Expression ParseInitializerClause()
    {
        return IsPunctuator("{") ? ParseBracedInitList() : ParseAssignmentExpression();
    }

    /// <summary>
    /// The initializer clauses after an opening '(' or '[' up to and including <paramref name="close"/>;
    /// each may be followed by '...'.
    /// </summary>
    private List<Expression> ParseExpressionList(string close)
    {
        var saved = EnterBrackets();
        var list = ParseExpressionListCore(close);
        LeaveBrackets(saved);
        return list;
    }

    private List<Expression> ParseExpressionListCore(string close)
    {
        var list = new List<Expression>();
        if (TryEatPunctuator(close))
        {
            return list;
        }

        while (true)
        {
            var clause = ParseInitializerClause();
            if (clause == null)
            {
                return null;
            }

            list.Add(TryParsePackExpansion(clause));
            if (TryEatPunctuator(close))
            {
                return list;
            }

            if (!TryEatPunctuator(","))
            {
                return null;
            }
        }
    }

    /// <summary>
    /// <paramref name="pattern"/> followed by '...' is a pack expansion.
    /// </summary>
    private Expression TryParsePackExpansion(Expression pattern)
    {
        return TryEatPunctuator("...") ? Finish(new PackExpansionExpression(pattern), pattern) : pattern;
    }

    /// <summary>
    /// braced-init-list: <c>{ 1, { 2 }, .x = 3, xs... , }</c>.
    /// </summary>
    private InitializerListExpression ParseBracedInitList()
    {
        EnsureSufficientStack();
        var start = NodeStart;
        if (!TryEatPunctuator("{"))
        {
            return null;
        }

        var saved = EnterBrackets();
        var list = ParseBracedInitListRest(start);
        LeaveBrackets(saved);
        return list;
    }

    private InitializerListExpression ParseBracedInitListRest(int start)
    {
        var elements = new List<Expression>();
        if (TryEatPunctuator("}"))
        {
            return Finish(new InitializerListExpression(elements), start);
        }

        var hasTrailingComma = false;
        while (true)
        {
            var element = IsDesignatorStart() ? ParseDesignatedInitializer() : ParseInitializerClause();
            if (element == null)
            {
                return null;
            }

            elements.Add(element is DesignatedInitializerExpression ? element : TryParsePackExpansion(element));
            if (TryEatPunctuator("}"))
            {
                break;
            }

            if (!TryEatPunctuator(","))
            {
                return null;
            }

            if (TryEatPunctuator("}"))
            {
                hasTrailingComma = true;
                break;
            }
        }

        return Finish(new InitializerListExpression(elements) { HasTrailingComma = hasTrailingComma }, start);
    }

    /// <summary>
    /// A designator starts the element: <c>.x</c>, or <c>[index]</c> followed by '=', '.' or '[' (otherwise
    /// '[' starts a lambda).
    /// </summary>
    private bool IsDesignatorStart()
    {
        if (IsPunctuator(".") && Peek(1).IsIdentifier)
        {
            return true;
        }

        if (!IsPunctuator("[") || Peek(1).IsPunctuator("["))
        {
            return false;
        }

        var mark = Save();
        EatToken();
        var saved = EnterBrackets();
        var index = ParseAssignmentExpression();
        LeaveBrackets(saved);
        var result = index != null && TryEatPunctuator("]") && (IsPunctuator("=") || IsPunctuator(".") || IsPunctuator("["));
        Restore(mark);
        return result;
    }

    /// <summary>
    /// <c>.x = value</c>, <c>.x{ list }</c>, <c>.a.b[1] = value</c>.
    /// </summary>
    private DesignatedInitializerExpression ParseDesignatedInitializer()
    {
        var start = NodeStart;
        var designators = new List<Designator>();
        while (true)
        {
            var designatorStart = NodeStart;
            if (TryEatPunctuator("."))
            {
                var member = TryEatIdentifier();
                if (member == null)
                {
                    return null;
                }

                designators.Add(Finish(new Designator(member), designatorStart));
            }
            else if (IsPunctuator("[") && !Peek(1).IsPunctuator("["))
            {
                EatToken();
                var saved = EnterBrackets();
                var index = ParseAssignmentExpression();
                LeaveBrackets(saved);
                if (index == null || !TryEatPunctuator("]"))
                {
                    return null;
                }

                designators.Add(Finish(new Designator(null, index), designatorStart));
            }
            else
            {
                break;
            }
        }

        if (TryEatPunctuator("="))
        {
            var value = ParseInitializerClause();
            return value == null ? null : Finish(new DesignatedInitializerExpression(designators, value) { HasEquals = true }, start);
        }

        var list = ParseBracedInitList();
        return list == null ? null : Finish(new DesignatedInitializerExpression(designators, list), start);
    }

    /// <summary>
    /// <c>( expressions )</c> of a direct initialization.
    /// </summary>
    private ParenthesizedInitializer ParseParenthesizedInitializer()
    {
        var start = NodeStart;
        if (!TryEatPunctuator("("))
        {
            return null;
        }

        var arguments = ParseExpressionList(")");
        return arguments == null ? null : Finish(new ParenthesizedInitializer(arguments), start);
    }

    /// <summary>
    /// <c>{ list }</c> of a list initialization.
    /// </summary>
    private BracedInitializer ParseBracedInitializer()
    {
        var list = ParseBracedInitList();
        return list == null ? null : Finish(new BracedInitializer(list), list);
    }

    /// <summary>
    /// The parenthesized or braced initializer of a functional cast or a new-expression, or null.
    /// </summary>
    private Initializer ParseDirectInitializer()
    {
        if (IsPunctuator("("))
        {
            return ParseParenthesizedInitializer();
        }

        return IsPunctuator("{") ? ParseBracedInitializer() : null;
    }

    // ========================================
    // Unary expressions and casts
    // ========================================

    /// <summary>
    /// cast-expression and unary-expression: C-style casts, prefix operators, <c>sizeof</c>, <c>alignof</c>,
    /// <c>noexcept</c>, <c>new</c>, <c>delete</c> and <c>co_await</c>, then a postfix expression.
    /// </summary>
    private Expression ParseCastExpression()
    {
        EnsureSufficientStack();
        var start = NodeStart;
        var token = Current;

        switch (token.Kind)
        {
            case TokenKind.Punctuator:
                switch (token.Text)
                {
                    case "(":
                        if (TryParseCastExpression(out var cast))
                        {
                            return cast;
                        }

                        break;

                    case "+" or "-" or "!" or "~" or "*" or "&" or "++" or "--":
                    {
                        EatToken();
                        var operand = ParseCastExpression();
                        return operand == null ? null : Finish(new UnaryExpression(token.Text, operand), start);
                    }

                    case "::" when Peek(1).IsKeyword("new"):
                        EatToken();
                        return ParseNewExpression(start, isGlobal: true);

                    case "::" when Peek(1).IsKeyword("delete"):
                        EatToken();
                        return ParseDeleteExpression(start, isGlobal: true);
                }

                break;

            case TokenKind.Keyword:
                switch (token.Text)
                {
                    case "sizeof":
                    case "alignof":
                        return ParseSizeOfExpression();

                    case "noexcept":
                    {
                        EatToken();
                        var operand = ParseParenthesizedExpression();
                        return operand == null ? null : Finish(new NoexceptExpression(operand), start);
                    }

                    case "new":
                        return ParseNewExpression(start, isGlobal: false);

                    case "delete":
                        return ParseDeleteExpression(start, isGlobal: false);

                    case "co_await":
                    {
                        EatToken();
                        var operand = ParseCastExpression();
                        return operand == null ? null : Finish(new UnaryExpression(token.Text, operand), start);
                    }
                }

                break;

            case TokenKind.Identifier when token.Text is "_Alignof" or "__alignof" or "__alignof__" && Peek(1).IsPunctuator("("):
                return ParseSizeOfExpression();

            case TokenKind.Identifier when token.Text == "__extension__":
            {
                // GNU: no warnings about extensions in the operand
                EatToken();
                var operand = ParseCastExpression();
                return operand == null ? null : Finish(new UnaryExpression(token.Text, operand), start);
            }
        }

        return ParsePostfixExpression();
    }

    /// <summary>
    /// <c>( expression )</c>, the parentheses not part of the result: the operand of <c>noexcept</c> and
    /// <c>typeid</c>.
    /// </summary>
    private Expression ParseParenthesizedExpression()
    {
        if (!TryEatPunctuator("("))
        {
            return null;
        }

        var saved = EnterBrackets();
        var expression = ParseExpression();
        LeaveBrackets(saved);
        return expression != null && TryEatPunctuator(")") ? expression : null;
    }

    /// <summary>
    /// A C-style cast <c>(type)operand</c> at the current '('. Returns false, with the position unchanged,
    /// when the parentheses do not hold a cast.
    /// </summary>
    /// <remarks>
    /// The parentheses hold a cast when they hold a type-id that is certainly a type (it has a keyword, a
    /// pointer or reference, or a name known as a type) and an operand follows. A single name that is not
    /// declared in the file (from a header) is a type when an operand follows that cannot continue an
    /// expression in parentheses: an identifier, a literal or a keyword like <c>sizeof</c>.
    /// <c>(size_t)n</c> is a cast, <c>(x)(y)</c> a call and <c>(x) - y</c> a subtraction.
    /// </remarks>
    private bool TryParseCastExpression(out Expression cast)
    {
        cast = null;
        var start = NodeStart;
        var mark = Save();
        EatToken();
        var saved = EnterBrackets();
        var type = ParseTypeId();
        LeaveBrackets(saved);
        if (type == null || !TryEatPunctuator(")"))
        {
            Restore(mark);
            return false;
        }

        var certainty = TypeCertainty(type);
        if (certainty == Certainty.No || (certainty == Certainty.Maybe && !StartsCastOperand(Current)))
        {
            Restore(mark);
            return false;
        }

        var operand = ParseCastExpression();
        if (operand == null)
        {
            // Like T(), a type-id in parentheses may be an expression: (T())
            Restore(mark);
            return false;
        }

        cast = Finish(new CastExpression(type, operand), start);
        return true;
    }

    private enum Certainty
    {
        No,
        Maybe,
        Yes,
    }

    /// <summary>
    /// Whether a type-id is certainly a type: it has a keyword specifier, a pointer or reference declarator,
    /// or names a type known as one. A name that is not declared in the file, alone or with only array and
    /// function declarators (<c>(a[1])</c>, <c>(f())</c>), may be an expression.
    /// </summary>
    private readonly Certainty TypeCertainty(TypeId type)
    {
        if (type.Specifiers.Specifiers is not [NamedTypeSpecifier { IsTypename: false } named])
        {
            return Certainty.Yes;
        }

        for (var declarator = type.Declarator; declarator != null;)
        {
            switch (declarator)
            {
                case ArrayDeclarator array:
                    declarator = array.Inner;
                    break;
                case FunctionDeclarator function:
                    declarator = function.Inner;
                    break;
                default:
                    return Certainty.Yes;
            }
        }

        return _cache.Symbols.Lookup(named.Name) switch
        {
            SymbolKind.Type or SymbolKind.Template => Certainty.Yes,
            null => Certainty.Maybe,
            _ => Certainty.No,
        };
    }

    /// <summary>
    /// After <c>(x)</c> with an unknown name <c>x</c>, the token starts the operand of a cast and cannot
    /// continue an expression.
    /// </summary>
    private static bool StartsCastOperand(SyntaxToken token)
    {
        switch (token.Kind)
        {
            case TokenKind.Identifier:
            case TokenKind.NumericLiteral:
            case TokenKind.CharacterLiteral:
            case TokenKind.StringLiteral:
                return true;
            case TokenKind.Keyword:
                return token.Text is "this" or "true" or "false" or "nullptr" or "sizeof" or "alignof" or "noexcept" or "new"
                    or "delete" or "typeid" or "static_cast" or "dynamic_cast" or "const_cast" or "reinterpret_cast"
                    or "co_await" or "operator" or "requires" || TypeKeywords.Contains(token.Text);
            case TokenKind.Punctuator:
                return token.Text is "!" or "~" or "::";
            default:
                return false;
        }
    }

    /// <summary>
    /// <c>sizeof</c> or <c>alignof</c> of a type in parentheses or of an expression, or <c>sizeof...(pack)</c>.
    /// </summary>
    private Expression ParseSizeOfExpression()
    {
        var start = NodeStart;
        var keyword = EatToken().Text;
        if (keyword == "sizeof" && TryEatPunctuator("..."))
        {
            if (!TryEatPunctuator("("))
            {
                return null;
            }

            var nameStart = NodeStart;
            var identifier = TryEatIdentifier();
            if (identifier == null || !TryEatPunctuator(")"))
            {
                return null;
            }

            return Finish(new SizeOfPackExpression(Finish(new IdentifierName(identifier), nameStart)), start);
        }

        var type = TryParseParenthesizedTypeId();
        if (type != null)
        {
            return Finish(new SizeOfExpression(keyword, type), start);
        }

        var operand = ParseCastExpression();
        return operand == null ? null : Finish(new SizeOfExpression(keyword, operand), start);
    }

    /// <summary>
    /// <c>( type-id )</c> at the current token, when the parentheses hold a type-id; otherwise null, with
    /// the position unchanged. A name that is not declared in the file is taken as a type.
    /// </summary>
    private TypeId TryParseParenthesizedTypeId()
    {
        if (!IsPunctuator("("))
        {
            return null;
        }

        var mark = Save();
        EatToken();
        var saved = EnterBrackets();
        var type = ParseTypeId();
        LeaveBrackets(saved);
        if (type != null && TryEatPunctuator(")"))
        {
            return type;
        }

        Restore(mark);
        return null;
    }

    /// <summary>
    /// A new-expression after <c>::</c>, if any: <c>new (placement) type initializer</c>.
    /// </summary>
    private NewExpression ParseNewExpression(int start, bool isGlobal)
    {
        EatToken();
        List<Expression> placement = null;
        TypeId type = null;
        var isParenthesizedType = false;

        if (IsPunctuator("("))
        {
            // new (T) or new (placement) T: the parentheses hold the type unless a type follows them
            var mark = Save();
            type = TryParseParenthesizedTypeId();
            if (type != null && !StartsNewTypeId(Current))
            {
                isParenthesizedType = true;
            }
            else
            {
                Restore(mark);
                type = null;
                EatToken();
                placement = ParseExpressionList(")");
                if (placement == null)
                {
                    return null;
                }
            }
        }

        if (type == null)
        {
            if (IsPunctuator("("))
            {
                type = TryParseParenthesizedTypeId();
                isParenthesizedType = true;
            }
            else
            {
                type = ParseTypeId(DeclaratorKind.New);
            }

            if (type == null)
            {
                return null;
            }
        }

        Initializer initializer = null;
        if (IsPunctuator("(") || IsPunctuator("{"))
        {
            initializer = ParseDirectInitializer();
            if (initializer == null)
            {
                return null;
            }
        }

        return Finish(new NewExpression(placement, type, initializer) { IsGlobal = isGlobal, IsParenthesizedType = isParenthesizedType }, start);
    }

    /// <summary>
    /// The token can start the type of a new-expression after placement arguments.
    /// </summary>
    private static bool StartsNewTypeId(SyntaxToken token)
    {
        return token.IsIdentifier || token.IsPunctuator("::")
            || (token.Kind == TokenKind.Keyword && (TypeKeywords.Contains(token.Text) || token.Text is "const" or "volatile"
                or "typename" or "decltype" or "class" or "struct" or "union" or "enum"));
    }

    /// <summary>
    /// <c>delete operand</c> or <c>delete[] operand</c> after <c>::</c>, if any.
    /// </summary>
    private DeleteExpression ParseDeleteExpression(int start, bool isGlobal)
    {
        EatToken();
        var isArray = false;
        if (IsPunctuator("[") && Peek(1).IsPunctuator("]"))
        {
            EatTokens(2);
            isArray = true;
        }

        var operand = ParseCastExpression();
        return operand == null ? null : Finish(new DeleteExpression(operand) { IsGlobal = isGlobal, IsArray = isArray }, start);
    }

    // ========================================
    // Postfix expressions
    // ========================================

    /// <summary>
    /// postfix-expression: a primary expression followed by calls, subscripts, member accesses and postfix
    /// increments. '[[' starts an attribute, never a subscript.
    /// </summary>
    private Expression ParsePostfixExpression()
    {
        var expression = ParsePrimaryExpression();
        while (expression != null)
        {
            var token = Current;
            if (token.Kind != TokenKind.Punctuator)
            {
                break;
            }

            switch (token.Text)
            {
                case "(":
                {
                    EatToken();
                    var arguments = ParseExpressionList(")");
                    if (arguments == null)
                    {
                        return null;
                    }

                    expression = Finish(new CallExpression(expression, arguments), expression);
                    break;
                }

                case "[" when !Peek(1).IsPunctuator("["):
                {
                    EatToken();
                    var arguments = ParseExpressionList("]");
                    if (arguments == null)
                    {
                        return null;
                    }

                    expression = Finish(new SubscriptExpression(expression, arguments), expression);
                    break;
                }

                case "." or "->":
                {
                    EatToken();
                    var isTemplate = TryEatKeyword("template");
                    var member = isTemplate
                        ? ParseNameComponent(NameContext.Member, qualified: false, isTemplate: true)
                        : ParseName(NameContext.Member);
                    if (member == null)
                    {
                        return null;
                    }

                    expression = Finish(new MemberAccessExpression(expression, token.Text, member) { IsTemplate = isTemplate }, expression);
                    break;
                }

                case "++" or "--":
                    EatToken();
                    expression = Finish(new UnaryExpression(token.Text, expression, isPostfix: true), expression);
                    break;

                default:
                    return expression;
            }
        }

        return expression;
    }

    // ========================================
    // Primary expressions
    // ========================================

    /// <summary>
    /// primary-expression: literals, <c>this</c>, names, parenthesized and fold expressions, lambdas and
    /// requires-expressions; and the postfix expressions that start with a keyword or a type: named casts,
    /// <c>typeid</c> and functional casts.
    /// </summary>
    private Expression ParsePrimaryExpression()
    {
        var start = NodeStart;
        var token = Current;

        switch (token.Kind)
        {
            case TokenKind.NumericLiteral:
                EatToken();
                return Finish(Literals.Number(token.Text), start);

            case TokenKind.CharacterLiteral:
                EatToken();
                return Finish(Literals.Character(token.Text), start);

            case TokenKind.StringLiteral:
            {
                // Adjacent string literals are one literal
                var first = Finish(Literals.String(EatToken().Text), start);
                if (Current.Kind != TokenKind.StringLiteral)
                {
                    return first;
                }

                var parts = new List<LiteralExpression> { first };
                while (Current.Kind == TokenKind.StringLiteral)
                {
                    var partStart = NodeStart;
                    parts.Add(Finish(Literals.String(EatToken().Text), partStart));
                }

                return Finish(Literals.Concatenate(parts), start);
            }

            case TokenKind.Identifier when Peek(1).IsPunctuator("(") && BuiltinArguments(token.Text) != null:
                return ParseBuiltinCallExpression();

            case TokenKind.Identifier when ExtensionTypeNames.Contains(token.Text):
            {
                var type = ParseExtensionTypeSpecifier();
                return type == null ? null : ParseFunctionalCastRest(type, start);
            }

            case TokenKind.Identifier:
            case TokenKind.Keyword when token.Text == "operator":
            case TokenKind.Punctuator when token.Text == "::" && IsNameStart(token, NameContext.Expression):
                return ParseNameOrFunctionalCast();

            case TokenKind.Keyword:
                return ParseKeywordPrimaryExpression();

            case TokenKind.Punctuator when token.Text == "(":
                return ParseParenthesizedOrFoldExpression();

            case TokenKind.Punctuator when token.Text == "[" && !Peek(1).IsPunctuator("["):
                return ParseLambdaExpression();

            default:
                return null;
        }
    }

    /// <summary>
    /// A primary expression that starts with a keyword.
    /// </summary>
    private Expression ParseKeywordPrimaryExpression()
    {
        var start = NodeStart;
        var token = Current;
        switch (token.Text)
        {
            case "true":
            case "false":
                EatToken();
                return Finish(new LiteralExpression(LiteralKind.Boolean, token.Text, token.Text == "true"), start);

            case "nullptr":
                EatToken();
                return Finish(new LiteralExpression(LiteralKind.Nullptr, token.Text), start);

            case "this":
                EatToken();
                return Finish(new ThisExpression(), start);

            case "static_cast" or "dynamic_cast" or "const_cast" or "reinterpret_cast":
                return ParseNamedCastExpression();

            case "typeid":
            {
                EatToken();
                if (!IsPunctuator("("))
                {
                    return null;
                }

                var type = TryParseParenthesizedTypeId();
                if (type != null)
                {
                    return Finish(new TypeidExpression(type), start);
                }

                var operand = ParseParenthesizedExpression();
                return operand == null ? null : Finish(new TypeidExpression(operand), start);
            }

            case "requires":
                return ParseRequiresExpression();

            case "typename":
            {
                // typename T::type(x)
                EatToken();
                var name = ParseName(NameContext.Type);
                return name == null
                    ? null
                    : ParseFunctionalCastRest(Finish(new NamedTypeSpecifier(name) { IsTypename = true }, start), start);
            }

            case "decltype":
            {
                // decltype(x)::member is a name, decltype(x)(y) a functional cast
                var specifier = ParseDecltypeOrNamedType();
                return specifier switch
                {
                    NamedTypeSpecifier named => IsPunctuator("{")
                        ? ParseFunctionalCastRest(named, start)
                        : Finish(new NameExpression(named.Name), start),
                    DecltypeSpecifier { Expression: not null } decltype => ParseFunctionalCastRest(decltype, start),
                    _ => null,
                };
            }

            default:
                if (TypeKeywords.Contains(token.Text))
                {
                    // int(x), auto{x}
                    EatToken();
                    return ParseFunctionalCastRest(Finish(new KeywordSpecifier(token.Text), start), start);
                }

                return null;
        }
    }

    /// <summary>
    /// A name, or a functional cast when the name is a type followed by '(' or '{'. A name that is not known
    /// is a type before '{' (<c>std::string{}</c>), since nothing else can be followed by a brace in an
    /// expression, but a function before '(': names from headers are not known.
    /// </summary>
    private Expression ParseNameOrFunctionalCast()
    {
        var start = NodeStart;
        var name = ParseName(NameContext.Expression);
        if (name == null)
        {
            return null;
        }

        // In a requires-clause, a brace after the constraint starts the function body: requires C<T> { }
        if (!_inConstraint && (IsPunctuator("(") || IsPunctuator("{")) && IsTypeInExpression(name, IsPunctuator("{")))
        {
            return ParseFunctionalCastRest(Finish(new NamedTypeSpecifier(name), start), start);
        }

        return Finish(new NameExpression(name), start);
    }

    /// <summary>
    /// A name in an expression is a type: it is known as one, or before a brace (<paramref name="beforeBrace"/>)
    /// it is not known or is a template-id. A template-id before '(' is a type when it names a class or alias
    /// template, and a call when it names a function template or an unknown one. The name of a class template
    /// alone is a type too: its injected-class-name, or a type whose arguments are deduced (<c>Box(1)</c>).
    /// </summary>
    private readonly bool IsTypeInExpression(Name name, bool beforeBrace)
    {
        var last = name is QualifiedName qualified ? qualified.Name : name;
        if (last is not (IdentifierName or TemplateIdName))
        {
            return false;
        }

        var kind = _cache.Symbols.Lookup(name);
        if (beforeBrace)
        {
            return kind is null or SymbolKind.Type or SymbolKind.Template;
        }

        return kind == SymbolKind.Type || (kind == SymbolKind.Template && last is IdentifierName);
    }

    /// <summary>
    /// Clang's type traits ([meta] as builtins): their arguments are types.
    /// </summary>
    private static readonly HashSet<string> TypeTraits =
    [
        "__has_nothrow_assign", "__has_nothrow_move_assign", "__has_nothrow_copy", "__has_nothrow_constructor",
        "__has_trivial_assign", "__has_trivial_move_assign", "__has_trivial_copy", "__has_trivial_constructor",
        "__has_trivial_move_constructor", "__has_trivial_destructor", "__has_virtual_destructor",
        "__has_unique_object_representations", "__is_abstract", "__is_aggregate", "__is_arithmetic", "__is_array",
        "__is_assignable", "__is_base_of", "__is_bounded_array", "__is_class", "__is_complete_type", "__is_compound",
        "__is_const", "__is_constructible", "__is_convertible", "__is_convertible_to", "__is_destructible",
        "__is_empty", "__is_enum", "__is_final", "__is_floating_point", "__is_function", "__is_fundamental",
        "__is_integral", "__is_interface_class", "__is_layout_compatible", "__is_literal", "__is_literal_type",
        "__is_lvalue_reference", "__is_member_function_pointer", "__is_member_object_pointer", "__is_member_pointer",
        "__is_nothrow_assignable", "__is_nothrow_constructible", "__is_nothrow_destructible", "__is_nullptr",
        "__is_object", "__is_pod", "__is_pointer", "__is_pointer_interconvertible_base_of", "__is_polymorphic",
        "__is_reference", "__is_referenceable", "__is_rvalue_reference", "__is_same", "__is_scalar", "__is_scoped_enum",
        "__is_sealed", "__is_signed", "__is_standard_layout", "__is_trivial", "__is_trivially_assignable",
        "__is_trivially_constructible", "__is_trivially_copyable", "__is_trivially_destructible",
        "__is_trivially_equality_comparable", "__is_trivially_relocatable", "__is_unbounded_array", "__is_union",
        "__is_unsigned", "__is_void", "__is_volatile", "__reference_binds_to_temporary",
        "__reference_constructs_from_temporary", "__reference_converts_from_temporary", "__can_pass_in_regs",
        "__array_rank",
    ];

    /// <summary>
    /// What the arguments of a clang builtin that takes types are: 'T' a type-id, 'E' an expression, and the
    /// last one repeats; null for any other name.
    /// </summary>
    private static string BuiltinArguments(string name) => name switch
    {
        "__builtin_offsetof" => "TE",
        "__builtin_bit_cast" => "TE",
        "__builtin_va_arg" or "__builtin_convertvector" => "ET",
        "__array_extent" => "TE",
        "__is_lvalue_expr" or "__is_rvalue_expr" => "E",
        _ => TypeTraits.Contains(name) ? "T" : null,
    };

    /// <summary>
    /// A call of a clang builtin that takes types: <c>__builtin_offsetof(S, member)</c>, <c>__is_same(T, U)</c>.
    /// </summary>
    private BuiltinCallExpression ParseBuiltinCallExpression()
    {
        var start = NodeStart;
        var name = EatToken().Text;
        var kinds = BuiltinArguments(name);
        EatToken();
        var saved = EnterBrackets();
        var arguments = new List<CppNode>();
        while (!IsPunctuator(")"))
        {
            if (arguments.Count > 0 && !TryEatPunctuator(","))
            {
                LeaveBrackets(saved);
                return null;
            }

            CppNode argument;
            if (kinds[Math.Min(arguments.Count, kinds.Length - 1)] == 'T')
            {
                var type = ParseTypeId();
                if (type != null && TryEatPunctuator("..."))
                {
                    type = Finish(new TypeId(type.Specifiers, type.Declarator) { IsPackExpansion = true }, type);
                }

                argument = type;
            }
            else
            {
                var expression = ParseAssignmentExpression();
                argument = expression == null ? null : TryParsePackExpansion(expression);
            }

            if (argument == null)
            {
                LeaveBrackets(saved);
                return null;
            }

            arguments.Add(argument);
        }

        LeaveBrackets(saved);
        EatToken();
        return Finish(new BuiltinCallExpression(name, arguments), start);
    }

    /// <summary>
    /// The initializer of a functional cast of <paramref name="type"/>: <c>(arguments)</c> or <c>{ list }</c>.
    /// </summary>
    private FunctionalCastExpression ParseFunctionalCastRest(DeclSpecifier type, int start)
    {
        var initializer = ParseDirectInitializer();
        return initializer == null ? null : Finish(new FunctionalCastExpression(type, initializer), start);
    }

    /// <summary>
    /// <c>static_cast&lt;type&gt;(expression)</c> and the other named casts.
    /// </summary>
    private NamedCastExpression ParseNamedCastExpression()
    {
        var start = NodeStart;
        var keyword = EatToken().Text;
        if (!TryEatPunctuator("<"))
        {
            return null;
        }

        var saved = _inTemplateArguments;
        _inTemplateArguments = true;
        var type = ParseTypeId();
        _inTemplateArguments = saved;
        if (type == null || !TryEatTemplateClose())
        {
            return null;
        }

        var operand = ParseParenthesizedExpression();
        return operand == null ? null : Finish(new NamedCastExpression(keyword, type, operand), start);
    }

    /// <summary>
    /// <c>(expression)</c>, or a fold expression: <c>(xs + ...)</c>, <c>(... + xs)</c>, <c>(xs + ... + 0)</c>.
    /// </summary>
    private Expression ParseParenthesizedOrFoldExpression()
    {
        var start = NodeStart;
        EatToken();
        var saved = EnterBrackets();
        var expression = ParseParenthesizedOrFoldRest(start);
        LeaveBrackets(saved);
        return expression;
    }

    private Expression ParseParenthesizedOrFoldRest(int start)
    {
        if (TryEatPunctuator("..."))
        {
            var (leftOperator, leftPrecedence, leftTokens) = PeekBinaryOperator();
            if (leftPrecedence == 0)
            {
                return null;
            }

            EatTokens(leftTokens);
            var pack = ParseCastExpression();
            return pack != null && TryEatPunctuator(")") ? Finish(new FoldExpression(null, leftOperator, pack), start) : null;
        }

        var expression = ParseExpression();
        if (expression == null)
        {
            return null;
        }

        if (TryEatPunctuator(")"))
        {
            return Finish(new ParenthesizedExpression(expression), start);
        }

        var (@operator, precedence, tokens) = PeekBinaryOperator();
        if (precedence == 0 || !Peek(tokens).IsPunctuator("..."))
        {
            return null;
        }

        EatTokens(tokens + 1);
        Expression right = null;
        if (!IsPunctuator(")"))
        {
            var (rightOperator, _, rightTokens) = PeekBinaryOperator();
            if (rightOperator != @operator)
            {
                return null;
            }

            EatTokens(rightTokens);
            right = ParseCastExpression();
            if (right == null)
            {
                return null;
            }
        }

        return TryEatPunctuator(")") ? Finish(new FoldExpression(expression, @operator, right), start) : null;
    }
}
