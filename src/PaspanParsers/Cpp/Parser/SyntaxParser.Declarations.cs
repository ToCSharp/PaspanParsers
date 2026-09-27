namespace PaspanParsers.Cpp;

// Translation unit and declarations.
internal ref partial struct SyntaxParser
{
    // ========================================
    // Translation Unit
    // ========================================

    /// <summary>
    /// translation-unit: declaration-seq? ([basic.link]). The span is the whole input.
    /// </summary>
    private TranslationUnit ParseTranslationUnit()
    {
        var declarations = new List<Declaration>();
        while (Current.Kind != TokenKind.EndOfFile)
        {
            var declaration = ParseDeclaration();
            if (declaration == null)
            {
                return null;
            }

            declarations.Add(declaration);
        }

        var directives = _cache.Preprocessor(_source).Directives;
        var unit = new TranslationUnit(declarations) { Directives = directives, EndDirectives = DirectivesBefore(Current) };
        return Finish(unit, 0, _source.Length);
    }

    // ========================================
    // Declarations
    // ========================================

    /// <summary>
    /// A function definition or a simple declaration.
    /// </summary>
    private Declaration ParseDeclaration() => ParseDeclaration(allowFunctionDefinition: true);

    /// <summary>
    /// A simple declaration in a block: <c>int a = 1, *b;</c>.
    /// </summary>
    private SimpleDeclaration ParseSimpleDeclaration() => ParseDeclaration(allowFunctionDefinition: false) as SimpleDeclaration;

    private Declaration ParseDeclaration(bool allowFunctionDefinition)
    {
        EnsureSufficientStack();
        var start = NodeStart;

        var specifiers = ParseDeclSpecifiers();
        if (specifiers == null && !IsNameStart(Current, NameContext.Declarator))
        {
            return null;
        }

        // A declaration without declarators declares a class or enumeration: struct Point;
        if (IsPunctuator(";"))
        {
            if (specifiers?.Specifiers.Any(s => s is ElaboratedTypeSpecifier) != true)
            {
                return null;
            }

            EatToken();
            return Finish(new SimpleDeclaration(specifiers, []), start);
        }

        if (!TryParseDeclarator(DeclaratorKind.Named, out var declarator))
        {
            return null;
        }

        // Only constructors, destructors and conversion functions have no specifiers
        if (specifiers == null && !NamesFunctionWithoutType(DeclaredName(declarator).Name))
        {
            return null;
        }

        var requiresClause = ParseOptionalRequiresClause(out var valid);
        if (!valid)
        {
            return null;
        }

        if (allowFunctionDefinition && IsPunctuator("{") && DeclaresFunction(declarator))
        {
            return ParseFunctionBodyRest(start, specifiers, declarator, requiresClause);
        }

        return ParseSimpleDeclarationRest(start, specifiers, declarator, requiresClause);
    }

    /// <summary>
    /// The body of a function definition: the parameters are declared in the scope of the body.
    /// </summary>
    private FunctionDefinition ParseFunctionBodyRest(int start, DeclSpecifierSequence specifiers, Declarator declarator, Expression requiresClause)
    {
        DeclareName(specifiers, declarator);
        var symbols = _cache.Symbols;
        symbols.EnterScope();
        if (InnermostOperator(declarator) is FunctionDeclarator function)
        {
            foreach (var parameter in function.Parameters)
            {
                DeclareName(parameter.Specifiers, parameter.Declarator);
            }
        }

        var body = ParseCompoundStatement();
        symbols.ExitScope();
        return body == null ? null : Finish(new FunctionDefinition(specifiers, declarator, body) { RequiresClause = requiresClause }, start);
    }

    /// <summary>
    /// <c>requires constraint</c> after a declarator or template parameters, or null when there is none;
    /// <paramref name="valid"/> is false when the clause does not parse.
    /// </summary>
    private Expression ParseOptionalRequiresClause(out bool valid)
    {
        valid = true;
        if (!TryEatKeyword("requires"))
        {
            return null;
        }

        var saved = _inConstraint;
        _inConstraint = true;
        var constraint = ParseConstraintLogicalOrExpression();
        _inConstraint = saved;
        valid = constraint != null;
        return constraint;
    }

    /// <summary>
    /// constraint-logical-or-expression: primary expressions joined by <c>&amp;&amp;</c> and <c>||</c>. Postfix
    /// operators are not part of it: in <c>[]&lt;class T&gt; requires (sizeof(T) &gt; 1) (T x) { }</c> the
    /// parameters follow the constraint.
    /// </summary>
    private Expression ParseConstraintLogicalOrExpression()
    {
        var left = ParseConstraintLogicalAndExpression();
        while (left != null && IsPunctuator("||"))
        {
            EatToken();
            var right = ParseConstraintLogicalAndExpression();
            if (right == null)
            {
                return null;
            }

            left = Finish(new BinaryExpression(left, "||", right), left);
        }

        return left;
    }

    private Expression ParseConstraintLogicalAndExpression()
    {
        var left = ParsePrimaryExpression();
        while (left != null && IsPunctuator("&&"))
        {
            EatToken();
            var right = ParsePrimaryExpression();
            if (right == null)
            {
                return null;
            }

            left = Finish(new BinaryExpression(left, "&&", right), left);
        }

        return left;
    }

    /// <summary>
    /// The initializer of the first declarator, the other init-declarators and the ';'.
    /// </summary>
    private SimpleDeclaration ParseSimpleDeclarationRest(int start, DeclSpecifierSequence specifiers, Declarator first, Expression requiresClause)
    {
        var declarators = new List<InitDeclarator>();
        var declarator = first;
        while (true)
        {
            var initDeclarator = ParseInitDeclaratorRest(specifiers, declarator, requiresClause);
            if (initDeclarator == null)
            {
                return null;
            }

            declarators.Add(initDeclarator);
            if (!TryEatPunctuator(","))
            {
                break;
            }

            if (!TryParseDeclarator(DeclaratorKind.Named, out declarator))
            {
                return null;
            }

            requiresClause = ParseOptionalRequiresClause(out var valid);
            if (!valid)
            {
                return null;
            }
        }

        if (!TryEatPunctuator(";"))
        {
            return null;
        }

        return Finish(new SimpleDeclaration(specifiers, declarators), start);
    }

    /// <summary>
    /// The optional initializer after <paramref name="declarator"/>: <c>= value</c>, <c>= { list }</c>,
    /// <c>(arguments)</c> or <c>{ list }</c>. The name is declared before the initializer, which can refer to it.
    /// </summary>
    private InitDeclarator ParseInitDeclaratorRest(DeclSpecifierSequence specifiers, Declarator declarator, Expression requiresClause)
    {
        DeclareName(specifiers, declarator);

        Initializer initializer = null;
        var start = NodeStart;
        if (TryEatPunctuator("="))
        {
            var value = ParseInitializerClause();
            if (value == null)
            {
                return null;
            }

            initializer = Finish(new EqualsInitializer(value), start);
        }
        else if (IsPunctuator("(") || IsPunctuator("{"))
        {
            // A '(' that starts parameters was taken by the declarator
            initializer = ParseDirectInitializer();
            if (initializer == null)
            {
                return null;
            }
        }

        return Finish(new InitDeclarator(declarator, initializer) { RequiresClause = requiresClause }, declarator);
    }

    /// <summary>
    /// Declares the name of <paramref name="declarator"/> in the current scope: a type after <c>typedef</c>,
    /// otherwise a value. Qualified names declare nothing new.
    /// </summary>
    private readonly void DeclareName(DeclSpecifierSequence specifiers, Declarator declarator)
    {
        if (DeclaredName(declarator)?.Name is IdentifierName identifier)
        {
            var isTypedef = specifiers?.Specifiers.Any(s => s is KeywordSpecifier { Keyword: "typedef" }) == true;
            _cache.Symbols.Declare(identifier.Identifier, isTypedef ? SymbolKind.Type : SymbolKind.Value);
        }
    }
}
