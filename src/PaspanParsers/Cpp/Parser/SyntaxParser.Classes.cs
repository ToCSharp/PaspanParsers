namespace PaspanParsers.Cpp;

// Classes ([class]) and enumerations ([dcl.enum]).
internal ref partial struct SyntaxParser
{
    // ========================================
    // Classes
    // ========================================

    /// <summary>
    /// A class definition (<c>struct Point : Base { … }</c>) in a declaration, where the head is followed by
    /// '{' or the ':' of a base clause, or an elaborated type specifier (<c>struct Point</c>).
    /// </summary>
    private DeclSpecifier ParseClassOrElaboratedSpecifier(SpecifierContext context)
    {
        var start = NodeStart;
        var key = EatToken().Text;
        var attributes = ParseAttributeSpecifiers();
        if (attributes == null)
        {
            return null;
        }

        Name name = null;
        if (IsNameStart(Current, NameContext.Type) && !IsKeyword("decltype"))
        {
            name = ParseName(NameContext.Type);
            if (name == null)
            {
                return null;
            }
        }

        // final is a class-virt-specifier only after a name and before the body or the bases
        var isFinal = false;
        if (name != null && Current is { Kind: TokenKind.Identifier, Text: "final" } && (Peek(1).IsPunctuator("{") || Peek(1).IsPunctuator(":")))
        {
            EatToken();
            isFinal = true;
        }

        if (context == SpecifierContext.Declaration && (IsPunctuator("{") || IsPunctuator(":")))
        {
            return ParseClassSpecifierRest(start, key, attributes, name, isFinal);
        }

        return isFinal ? null : ParseElaboratedTypeSpecifierRest(start, key, attributes, name);
    }

    /// <summary>
    /// The base clause and the members of a class after its head. The class is declared before its bases,
    /// which can name it: <c>struct Node : Base&lt;Node&gt;</c>. A class defined with a qualified name
    /// (<c>struct Outer::Inner { … }</c>) sees the names of the enclosing class.
    /// </summary>
    private ClassSpecifier ParseClassSpecifierRest(int start, string key, IReadOnlyList<AttributeSpecifier> attributes, Name name, bool isFinal)
    {
        var symbols = _cache.Symbols;
        var qualifiedScopes = name is QualifiedName qualified ? symbols.EnterQualifiedScope(qualified.Qualifier, qualified.Qualifier == null) : 0;
        var unqualified = name is QualifiedName { Name: var last } ? last : name;
        symbols.EnterClass(ScopeKind.Class, LastIdentifier(unqualified), declare: name is IdentifierName);

        List<BaseSpecifier> bases = [];
        if (IsPunctuator(":"))
        {
            bases = ParseBaseClause();
            if (bases == null)
            {
                return null;
            }
        }

        var members = ParseDeclarationBlock(DeclarationContext.Class, out var closeBraceDirectives);
        if (members == null)
        {
            return null;
        }

        symbols.ExitScope();
        symbols.ExitScopes(qualifiedScopes);
        return Finish(new ClassSpecifier(key, name, bases, members)
        {
            Attributes = attributes,
            IsFinal = isFinal,
            CloseBraceDirectives = closeBraceDirectives,
        }, start);
    }

    /// <summary>
    /// <c>: public Base, virtual protected Other, Bases...</c>. The members of known bases become visible
    /// in the class.
    /// </summary>
    private List<BaseSpecifier> ParseBaseClause()
    {
        EatToken();
        var bases = new List<BaseSpecifier>();
        do
        {
            var start = NodeStart;
            var attributes = ParseAttributeSpecifiers();
            if (attributes == null)
            {
                return null;
            }

            var isVirtualFirst = TryEatKeyword("virtual");
            string access = null;
            if (IsKeyword("public") || IsKeyword("protected") || IsKeyword("private"))
            {
                access = EatToken().Text;
            }

            var isVirtual = isVirtualFirst || TryEatKeyword("virtual");
            Name name = IsKeyword("decltype") ? ParseDecltypeName() : ParseName(NameContext.Type);
            if (name == null)
            {
                return null;
            }

            var isPackExpansion = TryEatPunctuator("...");
            bases.Add(Finish(new BaseSpecifier(name)
            {
                Attributes = attributes,
                IsVirtualFirst = isVirtualFirst,
                IsVirtual = isVirtual,
                Access = access,
                IsPackExpansion = isPackExpansion,
            }, start));
            _cache.Symbols.AddBase(name);
        }
        while (TryEatPunctuator(","));

        return bases;
    }

    // ========================================
    // Enumerations
    // ========================================

    /// <summary>
    /// An enumeration in a declaration, where the head is followed by '{' or the ':' of an underlying type, or
    /// is scoped and followed by ';' (<c>enum class E;</c>); otherwise an elaborated type specifier
    /// (<c>enum Color</c>).
    /// </summary>
    private DeclSpecifier ParseEnumOrElaboratedSpecifier(SpecifierContext context)
    {
        var start = NodeStart;
        EatToken();
        string scopedKey = null;
        if (IsKeyword("class") || IsKeyword("struct"))
        {
            scopedKey = EatToken().Text;
        }

        var attributes = ParseAttributeSpecifiers();
        if (attributes == null)
        {
            return null;
        }

        Name name = null;
        if (IsNameStart(Current, NameContext.Type) && !IsKeyword("decltype"))
        {
            name = ParseName(NameContext.Type);
            if (name == null)
            {
                return null;
            }
        }

        if (context != SpecifierContext.Declaration || !(IsPunctuator("{") || IsPunctuator(":") || (scopedKey != null && IsPunctuator(";"))))
        {
            return scopedKey == null ? ParseElaboratedTypeSpecifierRest(start, "enum", attributes, name) : null;
        }

        TypeId underlyingType = null;
        if (TryEatPunctuator(":"))
        {
            underlyingType = ParseTypeId();
            if (underlyingType == null)
            {
                return null;
            }
        }

        var symbols = _cache.Symbols;
        var identifier = name is IdentifierName simple ? simple.Identifier : null;
        if (!IsPunctuator("{"))
        {
            // An opaque declaration: enum class E : int;
            if (identifier != null)
            {
                symbols.Declare(identifier, SymbolKind.Type);
            }

            return Finish(new EnumSpecifier(scopedKey, name, null) { Attributes = attributes, UnderlyingType = underlyingType }, start);
        }

        var qualifiedScopes = name is QualifiedName qualified ? symbols.EnterQualifiedScope(qualified.Qualifier, qualified.Qualifier == null) : 0;
        symbols.EnterClass(ScopeKind.Enum, LastIdentifier(name), declare: identifier != null, isScopedEnum: scopedKey != null);
        var enumerators = ParseEnumeratorList(out var hasTrailingComma, out var closeBraceDirectives);
        if (enumerators == null)
        {
            return null;
        }

        symbols.ExitScope();
        symbols.ExitScopes(qualifiedScopes);
        return Finish(new EnumSpecifier(scopedKey, name, enumerators)
        {
            Attributes = attributes,
            UnderlyingType = underlyingType,
            HasTrailingComma = hasTrailingComma,
            CloseBraceDirectives = closeBraceDirectives,
        }, start);
    }

    /// <summary>
    /// <c>{ First, Second [[deprecated]] = 2, }</c>. The enumerators are declared as values.
    /// </summary>
    private List<Enumerator> ParseEnumeratorList(out bool hasTrailingComma, out IReadOnlyList<PreprocessorDirective> closeBraceDirectives)
    {
        hasTrailingComma = false;
        closeBraceDirectives = null;
        EatToken();
        var enumerators = new List<Enumerator>();
        while (!IsPunctuator("}"))
        {
            var start = NodeStart;
            var identifier = TryEatIdentifier();
            if (identifier == null)
            {
                return null;
            }

            var attributes = ParseAttributeSpecifiers();
            if (attributes == null)
            {
                return null;
            }

            Expression value = null;
            if (TryEatPunctuator("="))
            {
                value = ParseConditionalExpression();
                if (value == null)
                {
                    return null;
                }
            }

            _cache.Symbols.Declare(identifier, SymbolKind.Value);
            enumerators.Add(Finish(new Enumerator(identifier, value) { Attributes = attributes }, start));
            if (!TryEatPunctuator(","))
            {
                break;
            }

            hasTrailingComma = IsPunctuator("}");
        }

        if (!IsPunctuator("}"))
        {
            return null;
        }

        closeBraceDirectives = DirectivesBefore(EatToken());
        return enumerators;
    }
}
