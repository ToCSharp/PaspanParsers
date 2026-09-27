namespace PaspanParsers.Cpp;

/// <summary>
/// Where a declaration is, which decides the declarations allowed.
/// </summary>
internal enum DeclarationContext
{
    /// <summary>A namespace, a linkage specification or an export declaration.</summary>
    Namespace,

    /// <summary>The members of a class: access specifiers, bit-fields, constructors, pure and virt-specifiers.</summary>
    Class,

    /// <summary>A block: no function definitions, templates, linkage specifications or namespace definitions.</summary>
    Block,
}

// Translation unit and declarations ([dcl], [class.mem]).
internal ref partial struct SyntaxParser
{
    // ========================================
    // Translation Unit
    // ========================================

    /// <summary>
    /// translation-unit: declaration-seq? ([basic.link]), with module and import declarations. The span is
    /// the whole input.
    /// </summary>
    private TranslationUnit ParseTranslationUnit()
    {
        var declarations = new List<Declaration>();
        while (Current.Kind != TokenKind.EndOfFile)
        {
            var declaration = IsModuleDeclarationStart(0) || IsImportDeclarationStart(0)
                ? ParseModuleOrImportDeclaration(NodeStart, isExport: false)
                : ParseDeclaration(DeclarationContext.Namespace);
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
    /// A declaration in a block: <c>int a = 1, *b;</c>, <c>static_assert(sizeof(int) == 4);</c>,
    /// <c>using namespace std;</c>.
    /// </summary>
    private Declaration ParseBlockDeclaration() => ParseDeclaration(DeclarationContext.Block);

    /// <summary>
    /// A declaration in <paramref name="context"/>: one that starts with a keyword (namespaces, using,
    /// templates, linkage specifications, <c>asm</c>, access specifiers), an empty declaration, a function
    /// definition or a simple declaration.
    /// </summary>
    private Declaration ParseDeclaration(DeclarationContext context)
    {
        EnsureSufficientStack();
        var start = NodeStart;
        var token = Current;
        switch (token.Kind)
        {
            case TokenKind.Keyword:
                switch (token.Text)
                {
                    case "static_assert":
                        return ParseStaticAssertDeclaration();
                    case "namespace":
                        return ParseNamespaceDeclaration(context);
                    case "inline" when Peek(1).IsKeyword("namespace") && context == DeclarationContext.Namespace:
                        return ParseNamespaceDefinition();
                    case "using":
                        return ParseUsingDeclaration();
                    case "template" when context != DeclarationContext.Block:
                        return ParseTemplateDeclaration(context);
                    case "extern" when Peek(1).IsKeyword("template") && context != DeclarationContext.Block:
                        return ParseExplicitInstantiation(context);
                    case "extern" when Peek(1).Kind == TokenKind.StringLiteral && context == DeclarationContext.Namespace:
                        return ParseLinkageSpecification();
                    case "export" when context == DeclarationContext.Namespace:
                        return ParseExportDeclaration();
                    case "asm":
                        return ParseAsmDeclaration();
                    case "public" or "protected" or "private" when context == DeclarationContext.Class && Peek(1).IsPunctuator(":"):
                        EatTokens(2);
                        return Finish(new AccessSpecifier(token.Text), start);
                }

                break;

            case TokenKind.Punctuator when token.Text == ";" && context != DeclarationContext.Block:
                EatToken();
                return Finish(new EmptyDeclaration(), start);

            case TokenKind.Identifier when token.Text is "__asm__" or "__asm":
                return ParseAsmDeclaration();
        }

        var attributes = ParseAttributeSpecifiers();
        if (attributes == null)
        {
            return null;
        }

        var className = context == DeclarationContext.Class ? _cache.Symbols.CurrentClassName : null;
        var specifiers = ParseDeclSpecifiers(SpecifierContext.Declaration, className);
        if (specifiers == null && !IsNameStart(Current, NameContext.Declarator))
        {
            return null;
        }

        // A declaration without declarators declares a class or enumeration: struct Point; struct Point { };
        if (IsPunctuator(";"))
        {
            if (specifiers?.Specifiers.Any(s => s is ElaboratedTypeSpecifier or ClassSpecifier or EnumSpecifier) != true)
            {
                return null;
            }

            EatToken();
            return Finish(new SimpleDeclaration(specifiers, []) { Attributes = attributes }, start);
        }

        // An unnamed bit-field has no declarator: int : 0;
        Declarator declarator = null;
        if (!IsUnnamedBitField(context, specifiers))
        {
            if (!TryParseDeclarator(DeclaratorKind.Named, out declarator))
            {
                return null;
            }

            if (specifiers == null && !DeclaresWithoutType(declarator, className))
            {
                return null;
            }
        }

        var virtSpecifiers = ParseVirtSpecifiers(declarator);
        var requiresClause = ParseOptionalRequiresClause(out var valid);
        if (!valid)
        {
            return null;
        }

        if (context != DeclarationContext.Block && declarator != null && DeclaresFunction(declarator) && StartsFunctionBody())
        {
            return ParseFunctionDefinitionRest(start, attributes, specifiers, declarator, virtSpecifiers, requiresClause);
        }

        return ParseSimpleDeclarationRest(context, start, attributes, specifiers, declarator, virtSpecifiers, requiresClause);
    }

    /// <summary>
    /// <c>static_assert(condition, message);</c> or <c>static_assert(condition);</c>.
    /// </summary>
    private StaticAssertDeclaration ParseStaticAssertDeclaration()
    {
        var start = NodeStart;
        EatToken();
        if (!TryEatPunctuator("("))
        {
            return null;
        }

        var saved = EnterBrackets();
        var condition = ParseAssignmentExpression();
        Expression message = null;
        if (condition != null && TryEatPunctuator(","))
        {
            message = ParseAssignmentExpression();
            condition = message == null ? null : condition;
        }

        LeaveBrackets(saved);
        if (condition == null || !TryEatPunctuator(")") || !TryEatPunctuator(";"))
        {
            return null;
        }

        return Finish(new StaticAssertDeclaration(condition, message), start);
    }

    /// <summary>
    /// <c>{ declarations }</c> of a namespace, a linkage specification or an export declaration, or the
    /// members of a class; null when a declaration does not parse.
    /// </summary>
    private List<Declaration> ParseDeclarationBlock(DeclarationContext context, out IReadOnlyList<PreprocessorDirective> closeBraceDirectives)
    {
        closeBraceDirectives = null;
        if (!TryEatPunctuator("{"))
        {
            return null;
        }

        var declarations = new List<Declaration>();
        while (!IsPunctuator("}"))
        {
            if (Current.Kind == TokenKind.EndOfFile)
            {
                return null;
            }

            var declaration = ParseDeclaration(context);
            if (declaration == null)
            {
                return null;
            }

            declarations.Add(declaration);
        }

        closeBraceDirectives = DirectivesBefore(EatToken());
        return declarations;
    }

    /// <summary>
    /// The declarator declares a function without type specifiers: a constructor (<c>S::S</c>, or <c>S</c>
    /// in the class <paramref name="className"/>), a destructor, a conversion function, or a deduction guide
    /// <c>Box(T) -&gt; Box&lt;T&gt;</c>.
    /// </summary>
    private static bool DeclaresWithoutType(Declarator declarator, string className)
    {
        var name = DeclaredName(declarator)?.Name;
        if (name == null)
        {
            return false;
        }

        if (NamesFunctionWithoutType(name))
        {
            return true;
        }

        return name is IdentifierName identifier && InnermostOperator(declarator) is FunctionDeclarator function
            && (identifier.Identifier == className || function.TrailingReturnType != null);
    }

    /// <summary>
    /// A member declaration continues with the ':' of a bit-field without a name: <c>int : 0;</c>.
    /// </summary>
    private bool IsUnnamedBitField(DeclarationContext context, DeclSpecifierSequence specifiers)
    {
        return context == DeclarationContext.Class && specifiers != null && IsPunctuator(":");
    }

    /// <summary>
    /// <c>override</c> and <c>final</c> after the declarator of a function.
    /// </summary>
    private List<string> ParseVirtSpecifiers(Declarator declarator)
    {
        var specifiers = new List<string>();
        if (declarator == null || !DeclaresFunction(declarator))
        {
            return specifiers;
        }

        while (Current.IsIdentifier && Current.Text is "override" or "final")
        {
            specifiers.Add(EatToken().Text);
        }

        return specifiers;
    }

    // ========================================
    // Function definitions
    // ========================================

    /// <summary>
    /// The current token starts the body of a function: '{', a ctor-initializer, <c>try</c>, <c>= default</c>
    /// or <c>= delete</c>.
    /// </summary>
    private bool StartsFunctionBody()
    {
        return IsPunctuator("{") || IsPunctuator(":") || IsKeyword("try")
            || (IsPunctuator("=") && (Peek(1).IsKeyword("default") || Peek(1).IsKeyword("delete")));
    }

    /// <summary>
    /// The body of a function definition: <c>= default;</c>, <c>= delete;</c>, or a block with a
    /// ctor-initializer before it and handlers after it for a function-try-block. The parameters are declared
    /// in the scope of the body; the body of a function defined outside its class or namespace
    /// (<c>int S::f() { … }</c>) sees the names declared there.
    /// </summary>
    private FunctionDefinition ParseFunctionDefinitionRest(
        int start, IReadOnlyList<AttributeSpecifier> attributes, DeclSpecifierSequence specifiers, Declarator declarator,
        IReadOnlyList<string> virtSpecifiers, Expression requiresClause)
    {
        DeclareName(specifiers, declarator);
        if (TryEatPunctuator("="))
        {
            var keyword = EatToken().Text;
            return TryEatPunctuator(";")
                ? Finish(new FunctionDefinition(specifiers, declarator, null)
                {
                    Attributes = attributes,
                    VirtSpecifiers = virtSpecifiers,
                    RequiresClause = requiresClause,
                    IsDefaulted = keyword == "default",
                    IsDeleted = keyword == "delete",
                }, start)
                : null;
        }

        var symbols = _cache.Symbols;
        var qualifiedScopes = DeclaredName(declarator).Name is QualifiedName qualified
            ? symbols.EnterQualifiedScope(qualified.Qualifier, qualified.Qualifier == null)
            : 0;
        symbols.EnterScope();
        if (InnermostOperator(declarator) is FunctionDeclarator function)
        {
            foreach (var parameter in function.Parameters)
            {
                DeclareName(parameter.Specifiers, parameter.Declarator);
            }
        }

        var isTryBlock = TryEatKeyword("try");
        List<MemberInitializer> initializers = null;
        if (IsPunctuator(":"))
        {
            initializers = ParseCtorInitializer();
            if (initializers == null)
            {
                return null;
            }
        }

        var body = ParseCompoundStatement();
        if (body == null)
        {
            return null;
        }

        List<CatchClause> handlers = null;
        if (isTryBlock)
        {
            handlers = ParseHandlers();
            if (handlers == null)
            {
                return null;
            }
        }

        symbols.ExitScope();
        symbols.ExitScopes(qualifiedScopes);
        return Finish(new FunctionDefinition(specifiers, declarator, body)
        {
            Attributes = attributes,
            VirtSpecifiers = virtSpecifiers,
            RequiresClause = requiresClause,
            Initializers = initializers,
            Handlers = handlers,
        }, start);
    }

    /// <summary>
    /// ctor-initializer: <c>: member(arguments), Base{ list }, Bases(args)...</c>.
    /// </summary>
    private List<MemberInitializer> ParseCtorInitializer()
    {
        EatToken();
        var initializers = new List<MemberInitializer>();
        do
        {
            var start = NodeStart;
            Name member = IsKeyword("decltype") ? ParseDecltypeName() : ParseName(NameContext.Type);
            if (member == null)
            {
                return null;
            }

            var initializer = ParseDirectInitializer();
            if (initializer == null)
            {
                return null;
            }

            var isPackExpansion = TryEatPunctuator("...");
            initializers.Add(Finish(new MemberInitializer(member, initializer) { IsPackExpansion = isPackExpansion }, start));
        }
        while (TryEatPunctuator(","));

        return initializers;
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

    // ========================================
    // Simple declarations
    // ========================================

    /// <summary>
    /// The rest of the first declarator, the other declarators and the ';'.
    /// </summary>
    private SimpleDeclaration ParseSimpleDeclarationRest(
        DeclarationContext context, int start, IReadOnlyList<AttributeSpecifier> attributes, DeclSpecifierSequence specifiers,
        Declarator first, IReadOnlyList<string> virtSpecifiers, Expression requiresClause)
    {
        var declarators = new List<InitDeclarator>();
        var declarator = first;
        var declaratorStart = first?.Span.Start ?? NodeStart;
        IReadOnlyList<AttributeSpecifier> leadingAttributes = [];
        while (true)
        {
            var initDeclarator = ParseInitDeclaratorRest(context, specifiers, declarator, virtSpecifiers, requiresClause);
            if (initDeclarator == null)
            {
                return null;
            }

            declarators.Add(leadingAttributes.Count == 0
                ? initDeclarator
                : Finish(new InitDeclarator(initDeclarator.Declarator, initDeclarator.Initializer)
                {
                    LeadingAttributes = leadingAttributes,
                    AsmLabel = initDeclarator.AsmLabel,
                    Attributes = initDeclarator.Attributes,
                    VirtSpecifiers = initDeclarator.VirtSpecifiers,
                    RequiresClause = initDeclarator.RequiresClause,
                    BitFieldWidth = initDeclarator.BitFieldWidth,
                    IsPure = initDeclarator.IsPure,
                }, declaratorStart));
            if (!TryEatPunctuator(","))
            {
                break;
            }

            declaratorStart = NodeStart;
            leadingAttributes = ParseGnuAttributeSpecifiers();
            if (leadingAttributes == null)
            {
                return null;
            }

            declarator = null;
            if (!IsUnnamedBitField(context, specifiers) && !TryParseDeclarator(DeclaratorKind.Named, out declarator))
            {
                return null;
            }

            virtSpecifiers = ParseVirtSpecifiers(declarator);
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

        return Finish(new SimpleDeclaration(specifiers, declarators) { Attributes = attributes }, start);
    }

    /// <summary>
    /// What follows <paramref name="declarator"/> in an init-declarator or member-declarator: an asm label and
    /// GNU attributes, the width of a bit-field, and an initializer (<c>= value</c>, <c>= { list }</c>,
    /// <c>(arguments)</c>, <c>{ list }</c>) or the pure specifier <c>= 0</c>. The name is declared before the
    /// initializer, which can refer to it. The declarator is null for an unnamed bit-field.
    /// </summary>
    private InitDeclarator ParseInitDeclaratorRest(
        DeclarationContext context, DeclSpecifierSequence specifiers, Declarator declarator, IReadOnlyList<string> virtSpecifiers, Expression requiresClause)
    {
        var start = declarator?.Span.Start ?? NodeStart;
        if (declarator != null)
        {
            DeclareName(specifiers, declarator);
        }

        string asmLabel = null;
        if (IsAsmKeyword(Current) && Peek(1).IsPunctuator("("))
        {
            EatToken();
            asmLabel = ParseParenthesizedText();
            if (asmLabel == null)
            {
                return null;
            }
        }

        var attributes = ParseGnuAttributeSpecifiers();
        if (attributes == null)
        {
            return null;
        }

        Expression width = null;
        if (context == DeclarationContext.Class && TryEatPunctuator(":"))
        {
            width = ParseConditionalExpression();
            if (width == null)
            {
                return null;
            }
        }

        var isPure = false;
        Initializer initializer = null;
        var initializerStart = NodeStart;
        if (context == DeclarationContext.Class && declarator != null && DeclaresFunction(declarator)
            && IsPunctuator("=") && Peek(1) is { Kind: TokenKind.NumericLiteral, Text: "0" })
        {
            EatTokens(2);
            isPure = true;
        }
        else if (TryEatPunctuator("="))
        {
            var value = ParseInitializerClause();
            if (value == null)
            {
                return null;
            }

            initializer = Finish(new EqualsInitializer(value), initializerStart);
        }
        else if (IsPunctuator("{") || (IsPunctuator("(") && width == null))
        {
            // A '(' that starts parameters was taken by the declarator
            initializer = ParseDirectInitializer();
            if (initializer == null)
            {
                return null;
            }
        }

        return Finish(new InitDeclarator(declarator, initializer)
        {
            AsmLabel = asmLabel,
            Attributes = attributes,
            VirtSpecifiers = virtSpecifiers,
            RequiresClause = requiresClause,
            BitFieldWidth = width,
            IsPure = isPure,
        }, start);
    }

    /// <summary>
    /// Declares the name of <paramref name="declarator"/> in the current scope: a type after <c>typedef</c>,
    /// otherwise a value; the names of a structured binding are values. Qualified names declare nothing new,
    /// and neither do the names of constructors and deduction guides, which have no type specifiers.
    /// </summary>
    private readonly void DeclareName(DeclSpecifierSequence specifiers, Declarator declarator)
    {
        if (specifiers?.Specifiers.Any(IsTypeSpecifier) != true)
        {
            return;
        }

        if (StructuredBinding(declarator) is { } binding)
        {
            foreach (var name in binding.Names)
            {
                _cache.Symbols.Declare(name.Identifier, SymbolKind.Value);
            }

            return;
        }

        if (DeclaredName(declarator)?.Name is IdentifierName identifier)
        {
            var isTypedef = specifiers?.Specifiers.Any(s => s is KeywordSpecifier { Keyword: "typedef" }) == true;
            _cache.Symbols.Declare(identifier.Identifier, isTypedef ? SymbolKind.Type : SymbolKind.Value);
        }
    }
}
