namespace PaspanParsers.Cpp;

// Translation unit, declarations, declaration specifiers and declarators.
internal ref partial struct SyntaxParser
{
    /// <summary>
    /// Keywords that are declaration specifiers ([dcl.spec]): fundamental types, cv-qualifiers, storage
    /// classes and function specifiers.
    /// </summary>
    private static readonly HashSet<string> SpecifierKeywords =
    [
        "void", "bool", "char", "char8_t", "char16_t", "char32_t", "wchar_t", "short", "int", "long",
        "signed", "unsigned", "float", "double", "auto",
        "const", "volatile",
        "static", "extern", "thread_local", "mutable", "register",
        "inline", "constexpr", "consteval", "constinit", "virtual", "explicit",
    ];

    private bool IsDeclSpecifierStart => Current is { Kind: TokenKind.Keyword } token && SpecifierKeywords.Contains(token.Text);

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
    private Declaration ParseDeclaration()
    {
        EnsureSufficientStack();
        var start = NodeStart;

        var specifiers = ParseDeclSpecifiers();
        if (specifiers == null)
        {
            return null;
        }

        var declarator = ParseDeclarator();
        if (declarator == null)
        {
            return null;
        }

        if (declarator is FunctionDeclarator && IsPunctuator("{"))
        {
            var body = ParseCompoundStatement();
            return body == null ? null : Finish(new FunctionDefinition(specifiers, declarator, body), start);
        }

        return ParseSimpleDeclarationRest(start, specifiers, declarator);
    }

    /// <summary>
    /// A simple declaration in a block: <c>int a = 1, b;</c>.
    /// </summary>
    private SimpleDeclaration ParseSimpleDeclaration()
    {
        var start = NodeStart;
        var specifiers = ParseDeclSpecifiers();
        if (specifiers == null)
        {
            return null;
        }

        var declarator = ParseDeclarator();
        return declarator == null ? null : ParseSimpleDeclarationRest(start, specifiers, declarator);
    }

    /// <summary>
    /// The initializer of the first declarator, the other init-declarators and the ';'.
    /// </summary>
    private SimpleDeclaration ParseSimpleDeclarationRest(int start, DeclSpecifierSequence specifiers, Declarator first)
    {
        var declarators = new List<InitDeclarator>();
        var declarator = first;
        while (true)
        {
            var initDeclarator = ParseInitDeclaratorRest(declarator);
            if (initDeclarator == null)
            {
                return null;
            }

            declarators.Add(initDeclarator);
            if (!TryEatPunctuator(","))
            {
                break;
            }

            declarator = ParseDeclarator();
            if (declarator == null)
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
    /// The optional initializer after <paramref name="declarator"/>.
    /// </summary>
    private InitDeclarator ParseInitDeclaratorRest(Declarator declarator)
    {
        Initializer initializer = null;
        var start = NodeStart;
        if (TryEatPunctuator("="))
        {
            var value = ParseAssignmentExpression();
            if (value == null)
            {
                return null;
            }

            initializer = Finish(new EqualsInitializer(value), start);
        }

        return Finish(new InitDeclarator(declarator, initializer), declarator);
    }

    // ========================================
    // Declaration Specifiers
    // ========================================

    /// <summary>
    /// decl-specifier-seq: one or more declaration specifiers.
    /// </summary>
    private DeclSpecifierSequence ParseDeclSpecifiers()
    {
        var start = NodeStart;
        var specifiers = new List<DeclSpecifier>();
        while (IsDeclSpecifierStart)
        {
            var specifierStart = NodeStart;
            specifiers.Add(Finish(new KeywordSpecifier(EatToken().Text), specifierStart));
        }

        return specifiers.Count == 0 ? null : Finish(new DeclSpecifierSequence(specifiers), start);
    }

    // ========================================
    // Declarators
    // ========================================

    /// <summary>
    /// A declarator: a name, optionally followed by a parameter list.
    /// </summary>
    private Declarator ParseDeclarator()
    {
        var start = NodeStart;
        var name = TryEatIdentifier();
        if (name == null)
        {
            return null;
        }

        Declarator declarator = Finish(new NameDeclarator(name), start);
        if (TryEatPunctuator("("))
        {
            var parameters = ParseParameters();
            if (parameters == null)
            {
                return null;
            }

            declarator = Finish(new FunctionDeclarator(declarator, parameters), start);
        }

        return declarator;
    }

    /// <summary>
    /// The parameters after '(' up to and including ')'.
    /// </summary>
    private List<ParameterDeclaration> ParseParameters()
    {
        var parameters = new List<ParameterDeclaration>();
        if (TryEatPunctuator(")"))
        {
            return parameters;
        }

        while (true)
        {
            var parameter = ParseParameter();
            if (parameter == null)
            {
                return null;
            }

            parameters.Add(parameter);
            if (TryEatPunctuator(")"))
            {
                return parameters;
            }

            if (!TryEatPunctuator(","))
            {
                return null;
            }
        }
    }

    private ParameterDeclaration ParseParameter()
    {
        var start = NodeStart;
        var specifiers = ParseDeclSpecifiers();
        if (specifiers == null)
        {
            return null;
        }

        Declarator declarator = null;
        if (Current.IsIdentifier)
        {
            declarator = ParseDeclarator();
            if (declarator == null)
            {
                return null;
            }
        }

        Expression defaultValue = null;
        if (TryEatPunctuator("="))
        {
            defaultValue = ParseAssignmentExpression();
            if (defaultValue == null)
            {
                return null;
            }
        }

        return Finish(new ParameterDeclaration(specifiers, declarator, defaultValue), start);
    }
}
