namespace PaspanParsers.Cpp;

// Lambda expressions ([expr.prim.lambda]) and requires-expressions ([expr.prim.req]).
internal ref partial struct SyntaxParser
{
    // ========================================
    // Lambdas
    // ========================================

    /// <summary>
    /// <c>[captures] &lt;template parameters&gt; requires C attributes (parameters) specifiers noexcept attributes
    /// -&gt; type requires C { body }</c>. The template parameters, parameters and init-captures are declared in
    /// the scope of the lambda.
    /// </summary>
    private LambdaExpression ParseLambdaExpression()
    {
        var start = NodeStart;
        EatToken();
        var symbols = _cache.Symbols;
        var saved = EnterBrackets();
        symbols.EnterScope();
        var lambda = ParseLambdaRest(start);
        symbols.ExitScope();
        LeaveBrackets(saved);
        return lambda;
    }

    private LambdaExpression ParseLambdaRest(int start)
    {
        string captureDefault = null;
        var captures = new List<LambdaCapture>();
        if (!TryEatPunctuator("]"))
        {
            var done = false;
            if ((IsPunctuator("=") || IsPunctuator("&")) && (Peek(1).IsPunctuator(",") || Peek(1).IsPunctuator("]")))
            {
                captureDefault = EatToken().Text;
                done = TryEatPunctuator("]");
                if (!done)
                {
                    EatToken();
                }
            }

            while (!done)
            {
                var capture = ParseLambdaCapture();
                if (capture == null)
                {
                    return null;
                }

                captures.Add(capture);
                done = TryEatPunctuator("]");
                if (!done && !TryEatPunctuator(","))
                {
                    return null;
                }
            }
        }

        List<TemplateParameter> templateParameters = null;
        Expression templateRequiresClause = null;
        if (IsPunctuator("<"))
        {
            templateParameters = ParseTemplateParameterList();
            if (templateParameters == null)
            {
                return null;
            }

            templateRequiresClause = ParseOptionalRequiresClause(out var valid);
            if (!valid)
            {
                return null;
            }
        }

        var attributes = ParseAttributeSpecifiers();
        if (attributes == null)
        {
            return null;
        }

        List<ParameterDeclaration> parameters = null;
        var isVariadic = false;
        var ellipsisWithoutComma = false;
        if (TryEatPunctuator("("))
        {
            parameters = ParseParameterClause(out isVariadic, out ellipsisWithoutComma);
            if (parameters == null)
            {
                return null;
            }

            foreach (var parameter in parameters)
            {
                DeclareName(parameter.Specifiers, parameter.Declarator);
            }
        }

        var specifiers = new List<string>();
        while (IsKeyword("mutable") || IsKeyword("constexpr") || IsKeyword("consteval") || IsKeyword("static"))
        {
            specifiers.Add(EatToken().Text);
        }

        NoexceptSpecifier noexcept = null;
        if (IsKeyword("noexcept"))
        {
            noexcept = ParseNoexceptSpecifier();
            if (noexcept == null)
            {
                return null;
            }
        }

        var typeAttributes = ParseAttributeSpecifiers();
        if (typeAttributes == null)
        {
            return null;
        }

        TypeId trailingReturnType = null;
        if (TryEatPunctuator("->"))
        {
            trailingReturnType = ParseTypeId();
            if (trailingReturnType == null)
            {
                return null;
            }
        }

        var requiresClause = ParseOptionalRequiresClause(out var validClause);
        if (!validClause)
        {
            return null;
        }

        var body = ParseCompoundStatement();
        if (body == null)
        {
            return null;
        }

        return Finish(
            new LambdaExpression(captures, body)
            {
                CaptureDefault = captureDefault,
                TemplateParameters = templateParameters,
                TemplateRequiresClause = templateRequiresClause,
                Attributes = attributes,
                Parameters = parameters,
                IsVariadic = isVariadic,
                EllipsisWithoutComma = ellipsisWithoutComma,
                Specifiers = specifiers,
                Noexcept = noexcept,
                TypeAttributes = typeAttributes,
                TrailingReturnType = trailingReturnType,
                RequiresClause = requiresClause,
            },
            start);
    }

    /// <summary>
    /// A capture: <c>x</c>, <c>&amp;x</c>, <c>xs...</c>, <c>this</c>, <c>*this</c>, or an init-capture
    /// <c>x = e</c>, <c>&amp;x = e</c>, <c>...xs = e</c>, <c>x{ e }</c>, <c>x(e)</c>, whose name is declared.
    /// </summary>
    private LambdaCapture ParseLambdaCapture()
    {
        var start = NodeStart;
        if (TryEatKeyword("this"))
        {
            return Finish(new LambdaCapture(null) { IsThis = true }, start);
        }

        if (IsPunctuator("*") && Peek(1).IsKeyword("this"))
        {
            EatTokens(2);
            return Finish(new LambdaCapture(null) { IsThis = true, IsStarThis = true }, start);
        }

        var isByReference = TryEatPunctuator("&");
        var isInitPack = TryEatPunctuator("...");
        var identifier = TryEatIdentifier();
        if (identifier == null)
        {
            return null;
        }

        Initializer initializer = null;
        var isPack = isInitPack;
        var initializerStart = NodeStart;
        if (TryEatPunctuator("="))
        {
            var value = ParseInitializerClause();
            if (value == null)
            {
                return null;
            }

            initializer = Finish(new EqualsInitializer(value), initializerStart);
        }
        else if (IsPunctuator("(") || IsPunctuator("{"))
        {
            initializer = ParseDirectInitializer();
            if (initializer == null)
            {
                return null;
            }
        }
        else if (isInitPack)
        {
            return null;
        }
        else
        {
            isPack = TryEatPunctuator("...");
        }

        if (initializer != null)
        {
            _cache.Symbols.Declare(identifier, SymbolKind.Value);
        }

        return Finish(new LambdaCapture(identifier, initializer) { IsByReference = isByReference, IsPack = isPack }, start);
    }

    // ========================================
    // Requires-expressions
    // ========================================

    /// <summary>
    /// <c>requires (parameters) { requirements }</c>; the parameters are declared in its scope.
    /// </summary>
    private RequiresExpression ParseRequiresExpression()
    {
        var start = NodeStart;
        EatToken();
        var symbols = _cache.Symbols;
        var saved = EnterBrackets();
        symbols.EnterScope();
        var expression = ParseRequiresExpressionRest(start);
        symbols.ExitScope();
        LeaveBrackets(saved);
        return expression;
    }

    private RequiresExpression ParseRequiresExpressionRest(int start)
    {
        List<ParameterDeclaration> parameters = null;
        if (TryEatPunctuator("("))
        {
            parameters = ParseParameterClause(out var isVariadic, out _);
            if (parameters == null || isVariadic)
            {
                return null;
            }

            foreach (var parameter in parameters)
            {
                DeclareName(parameter.Specifiers, parameter.Declarator);
            }
        }

        if (!TryEatPunctuator("{"))
        {
            return null;
        }

        var requirements = new List<Requirement>();
        while (!TryEatPunctuator("}"))
        {
            var requirement = ParseRequirement();
            if (requirement == null)
            {
                return null;
            }

            requirements.Add(requirement);
        }

        return Finish(new RequiresExpression(parameters, requirements), start);
    }

    /// <summary>
    /// A simple, type, compound or nested requirement, with its ';'.
    /// </summary>
    private Requirement ParseRequirement()
    {
        var start = NodeStart;
        Requirement requirement;
        if (TryEatPunctuator("{"))
        {
            var expression = ParseExpression();
            if (expression == null || !TryEatPunctuator("}"))
            {
                return null;
            }

            var isNoexcept = TryEatKeyword("noexcept");
            Name constraint = null;
            if (TryEatPunctuator("->"))
            {
                constraint = ParseName(NameContext.Type);
                if (constraint == null)
                {
                    return null;
                }
            }

            requirement = new CompoundRequirement(expression) { IsNoexcept = isNoexcept, TypeConstraint = constraint };
        }
        else if (TryEatKeyword("typename"))
        {
            var type = ParseName(NameContext.Type);
            if (type == null)
            {
                return null;
            }

            requirement = new TypeRequirement(type);
        }
        else if (TryEatKeyword("requires"))
        {
            // A requirement that starts with 'requires' is a nested requirement ([expr.prim.req.nested])
            var saved = _inConstraint;
            _inConstraint = true;
            var constraint = ParseBinaryExpression(LogicalOrPrecedence);
            _inConstraint = saved;
            if (constraint == null)
            {
                return null;
            }

            requirement = new NestedRequirement(constraint);
        }
        else
        {
            var expression = ParseExpression();
            if (expression == null)
            {
                return null;
            }

            requirement = new SimpleRequirement(expression);
        }

        return TryEatPunctuator(";") ? Finish(requirement, start) : null;
    }
}
