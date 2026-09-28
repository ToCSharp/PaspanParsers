namespace PaspanParsers.Cpp;

/// <summary>
/// Which declaration specifiers are allowed.
/// </summary>
internal enum SpecifierContext
{
    /// <summary>All declaration specifiers ([dcl.spec]).</summary>
    Declaration,

    /// <summary>A type-specifier-seq: types and cv-qualifiers, as in a type-id.</summary>
    Type,
}

// Declaration specifiers and type-ids ([dcl.spec], [dcl.name]).
internal ref partial struct SyntaxParser
{
    /// <summary>
    /// Keywords that are simple type specifiers ([dcl.type.simple]).
    /// </summary>
    private static readonly HashSet<string> TypeKeywords =
    [
        "void", "bool", "char", "char8_t", "char16_t", "char32_t", "wchar_t", "short", "int", "long",
        "signed", "unsigned", "float", "double", "auto",
    ];

    /// <summary>
    /// Identifiers that clang treats as keywords for types: GNU extensions.
    /// </summary>
    private static readonly HashSet<string> ExtensionTypeNames = ["__int128", "__float128", "_Float16", "__bf16", "_BitInt"];

    /// <summary>
    /// Keywords that are declaration specifiers but no types: cv-qualifiers, storage classes, function
    /// specifiers and <c>typedef</c>.
    /// </summary>
    private static readonly HashSet<string> OtherSpecifierKeywords =
    [
        "const", "volatile",
        "static", "extern", "thread_local", "mutable", "register",
        "inline", "constexpr", "consteval", "constinit", "virtual", "explicit", "typedef", "friend",
    ];

    private static readonly HashSet<string> ClassKeys = ["class", "struct", "union", "enum"];

    /// <summary>
    /// The GNU spellings of <c>restrict</c>, a cv-qualifier of pointers.
    /// </summary>
    private static bool IsRestrict(SyntaxToken token) => token.IsIdentifier && token.Text is "__restrict" or "__restrict__";

    /// <summary>
    /// The current token starts a declaration specifier that is a keyword.
    /// </summary>
    private bool IsDeclSpecifierKeyword => Current is { Kind: TokenKind.Keyword } token
        && (TypeKeywords.Contains(token.Text) || OtherSpecifierKeywords.Contains(token.Text) || ClassKeys.Contains(token.Text)
            || token.Text is "decltype" or "typename");

    /// <summary>
    /// decl-specifier-seq: declaration specifiers in any order. A name is a type specifier only while
    /// there is no other type specifier (<c>unsigned x</c> declares <c>x</c>), and not when it names a
    /// constructor, destructor or conversion function (<c>S::S</c>, <c>S::~S</c>, or <c>S(</c> among the members
    /// of the class <paramref name="className"/>) or a deduction guide (<c>Box(T) -&gt; Box&lt;T&gt;</c>), which
    /// have no specifiers. A class or enumeration may be defined in a declaration. Returns null when there are
    /// no specifiers.
    /// </summary>
    private DeclSpecifierSequence ParseDeclSpecifiers(SpecifierContext context = SpecifierContext.Declaration, string className = null)
    {
        var start = NodeStart;
        var specifiers = new List<DeclSpecifier>();
        var hasType = false;
        while (true)
        {
            var specifierStart = NodeStart;
            var token = Current;
            DeclSpecifier specifier = null;

            if (token.Kind == TokenKind.Keyword)
            {
                if (token.Text == "explicit" && Peek(1).IsPunctuator("(") && context == SpecifierContext.Declaration)
                {
                    specifier = ParseExplicitSpecifier();
                    if (specifier == null)
                    {
                        return null;
                    }
                }
                else if (TypeKeywords.Contains(token.Text) || OtherSpecifierKeywords.Contains(token.Text))
                {
                    if (context == SpecifierContext.Type && !TypeKeywords.Contains(token.Text) && token.Text is not ("const" or "volatile"))
                    {
                        break;
                    }

                    EatToken();
                    specifier = Finish(new KeywordSpecifier(token.Text), specifierStart);
                    hasType |= TypeKeywords.Contains(token.Text);
                }
                else if (ClassKeys.Contains(token.Text) && !hasType)
                {
                    specifier = token.Text == "enum" ? ParseEnumOrElaboratedSpecifier(context) : ParseClassOrElaboratedSpecifier(context);
                    if (specifier == null)
                    {
                        return null;
                    }

                    hasType = true;
                }
                else if (token.Text == "typename" && !hasType)
                {
                    EatToken();
                    var name = ParseName(NameContext.Type);
                    if (name == null)
                    {
                        return null;
                    }

                    specifier = Finish(new NamedTypeSpecifier(name) { IsTypename = true }, specifierStart);
                    hasType = true;
                }
                else if (token.Text == "decltype" && !hasType)
                {
                    specifier = ParseDecltypeOrNamedType();
                    if (specifier == null)
                    {
                        return null;
                    }

                    hasType = true;
                }
            }
            else if (token.IsIdentifier && ExtensionTypeNames.Contains(token.Text))
            {
                specifier = ParseExtensionTypeSpecifier();
                if (specifier == null)
                {
                    return null;
                }

                hasType = true;
            }
            else if (IsRestrict(token) || (token.IsIdentifier && token.Text == "__extension__" && context == SpecifierContext.Declaration))
            {
                // __extension__ before a declaration: no warnings about extensions in it
                EatToken();
                specifier = Finish(new KeywordSpecifier(token.Text), specifierStart);
            }
            else if (IsGnuAttributeStart && context == SpecifierContext.Declaration)
            {
                var attribute = ParseGnuAttributeSpecifier();
                if (attribute == null)
                {
                    return null;
                }

                specifier = Finish(new AttributeDeclSpecifier(attribute), specifierStart);
            }
            else if (!hasType && (token.IsIdentifier || token.IsPunctuator("::")))
            {
                var mark = Save();
                var name = ParseName(NameContext.Type);
                if (name == null || NamesFunctionWithoutType(name) || (IsPunctuator("::") && Peek(1).IsPunctuator("*"))
                    || (name is IdentifierName or QualifiedName && _cache.Symbols.Lookup(name) is SymbolKind.Value or SymbolKind.ValueTemplate or SymbolKind.Namespace)
                    || (context == SpecifierContext.Declaration && IsPunctuator("(") && StartsFunctionWithoutType(name, className)))
                {
                    // A declarator id, the class of a pointer to member, or a variable: int a(b); declares a variable
                    Restore(mark);
                    break;
                }

                specifier = (DeclSpecifier)ParsePlaceholderRest(name, specifierStart) ?? Finish(new NamedTypeSpecifier(name), specifierStart);
                hasType = true;
            }

            if (specifier == null)
            {
                break;
            }

            specifiers.Add(specifier);
        }

        return specifiers.Count == 0 ? null : Finish(new DeclSpecifierSequence(specifiers), start);
    }

    /// <summary>
    /// A name that declares a constructor (<c>S::S</c>), a destructor or a conversion function, which
    /// have no type specifiers, or an operator function, which is no type.
    /// </summary>
    private static bool NamesFunctionWithoutType(Name name)
    {
        var last = name is QualifiedName qualified ? qualified.Name : name;
        if (last is DestructorName or ConversionFunctionName or OperatorFunctionName or LiteralOperatorName)
        {
            return true;
        }

        // S::S and N::S<T>::S
        return name is QualifiedName { Qualifier: { } qualifier } && last is IdentifierName identifier
            && LastIdentifier(qualifier) == identifier.Identifier;
    }

    /// <summary>
    /// <paramref name="name"/>, followed by the current '(', names a function without type specifiers: a
    /// constructor among the members of the class <paramref name="className"/> (<c>S(int)</c>, but not
    /// <c>S (*f)()</c>), or a deduction guide of a known template (<c>Box(T) -&gt; Box&lt;T&gt;</c>).
    /// </summary>
    private bool StartsFunctionWithoutType(Name name, string className)
    {
        if (name is not IdentifierName identifier)
        {
            return false;
        }

        if (identifier.Identifier == className)
        {
            var next = Peek(1);
            return !(next.IsPunctuator("*") || next.IsPunctuator("&") || next.IsPunctuator("&&"));
        }

        return _cache.Symbols.Lookup(identifier.Identifier) == SymbolKind.Template && TokenAfterParentheses().IsPunctuator("->");
    }

    /// <summary>
    /// The token after the parentheses that the current '(' opens.
    /// </summary>
    private SyntaxToken TokenAfterParentheses()
    {
        var depth = 0;
        var token = Current;
        for (; token.Kind != TokenKind.EndOfFile; token = TokenAt(token.End))
        {
            if (token.Kind != TokenKind.Punctuator)
            {
                continue;
            }

            if (token.Text is "(" or "[" or "{")
            {
                depth++;
            }
            else if (token.Text is ")" or "]" or "}" && --depth == 0)
            {
                return TokenAt(token.End);
            }
        }

        return token;
    }

    /// <summary>
    /// <c>explicit(condition)</c>.
    /// </summary>
    private ExplicitSpecifier ParseExplicitSpecifier()
    {
        var start = NodeStart;
        EatToken();
        var condition = ParseParenthesizedExpression();
        return condition == null ? null : Finish(new ExplicitSpecifier(condition), start);
    }

    /// <summary>
    /// The identifier of the last component of <paramref name="name"/>, without template arguments.
    /// </summary>
    private static string LastIdentifier(Name name) => name switch
    {
        IdentifierName identifier => identifier.Identifier,
        TemplateIdName templateId => LastIdentifier(templateId.Template),
        QualifiedName qualified => LastIdentifier(qualified.Name),
        _ => null,
    };

    /// <summary>
    /// <c>auto</c> or <c>decltype(auto)</c> after the name of a concept: <c>std::integral auto</c>.
    /// </summary>
    private PlaceholderTypeSpecifier ParsePlaceholderRest(Name concept, int start)
    {
        if (TryEatKeyword("auto"))
        {
            return Finish(new PlaceholderTypeSpecifier(concept), start);
        }

        if (IsKeyword("decltype") && Peek(1).IsPunctuator("(") && Peek(2).IsKeyword("auto") && Peek(3).IsPunctuator(")"))
        {
            EatTokens(4);
            return Finish(new PlaceholderTypeSpecifier(concept, isDecltypeAuto: true), start);
        }

        return null;
    }

    /// <summary>
    /// The rest of an elaborated type specifier after its key and attributes: a name. An unqualified name is
    /// declared as a type.
    /// </summary>
    private ElaboratedTypeSpecifier ParseElaboratedTypeSpecifierRest(int start, string key, IReadOnlyList<AttributeSpecifier> attributes, Name name)
    {
        if (name == null || name is DestructorName or OperatorFunctionName or ConversionFunctionName or LiteralOperatorName)
        {
            return null;
        }

        if (name is IdentifierName identifier)
        {
            _cache.Symbols.Declare(identifier.Identifier, SymbolKind.Type);
        }

        return Finish(new ElaboratedTypeSpecifier(key, name) { Attributes = attributes }, start);
    }

    /// <summary>
    /// <c>decltype(expression)</c>, <c>decltype(auto)</c>, or a name qualified by decltype: <c>decltype(x)::type</c>.
    /// </summary>
    private DeclSpecifier ParseDecltypeOrNamedType()
    {
        var start = NodeStart;
        if (Peek(1).IsPunctuator("(") && Peek(2).IsKeyword("auto") && Peek(3).IsPunctuator(")"))
        {
            EatTokens(4);
            return Finish(new DecltypeSpecifier(null), start);
        }

        // The operand is parsed once: nested decltype would take exponential time
        var decltype = ParseDecltypeName();
        if (decltype == null)
        {
            return null;
        }

        if (IsPunctuator("::") && IsNameComponentStart(Peek(1)))
        {
            var mark = Save();
            if (ParseName(NameContext.Type, decltype) is QualifiedName name)
            {
                return Finish(new NamedTypeSpecifier(name), start);
            }

            Restore(mark);
        }

        return Finish(new DecltypeSpecifier(decltype.Expression), start);
    }

    // ========================================
    // Type-ids
    // ========================================

    /// <summary>
    /// type-id: a type-specifier-seq and an optional abstract declarator of <paramref name="kind"/>: the type
    /// of a conversion function takes only pointer and reference operators, and the type of a new-expression
    /// only pointers and arrays. Returns null when there is no type.
    /// </summary>
    private TypeId ParseTypeId(DeclaratorKind kind = DeclaratorKind.Abstract)
    {
        var start = NodeStart;
        var specifiers = ParseDeclSpecifiers(SpecifierContext.Type);
        if (specifiers == null || !specifiers.Specifiers.Any(IsTypeSpecifier))
        {
            return null;
        }

        if (!TryParseDeclarator(kind, out var declarator))
        {
            return null;
        }

        return Finish(new TypeId(specifiers, declarator), start);
    }

    /// <summary>
    /// A type that clang has as an extension: <c>__int128</c>, <c>_Float16</c>, <c>_BitInt(N)</c>.
    /// </summary>
    private DeclSpecifier ParseExtensionTypeSpecifier()
    {
        var start = NodeStart;
        var token = EatToken();
        if (token.Text != "_BitInt")
        {
            return Finish(new KeywordSpecifier(token.Text), start);
        }

        if (!TryEatPunctuator("("))
        {
            return null;
        }

        var saved = EnterBrackets();
        var width = ParseConditionalExpression();
        LeaveBrackets(saved);
        return width != null && TryEatPunctuator(")") ? Finish(new BitIntSpecifier(width), start) : null;
    }

    private static bool IsTypeSpecifier(DeclSpecifier specifier) => specifier switch
    {
        KeywordSpecifier keyword => TypeKeywords.Contains(keyword.Keyword) || ExtensionTypeNames.Contains(keyword.Keyword),
        ExplicitSpecifier or AttributeDeclSpecifier => false,
        _ => true,
    };
}
