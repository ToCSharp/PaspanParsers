namespace PaspanParsers.Cpp;

/// <summary>
/// Which declarators are allowed.
/// </summary>
internal enum DeclaratorKind
{
    /// <summary>A declarator with a name, as in a declaration.</summary>
    Named,

    /// <summary>An abstract declarator, as in a type-id: <c>(*)(int)</c>.</summary>
    Abstract,

    /// <summary>A parameter: named or abstract.</summary>
    Parameter,

    /// <summary>The abstract declarator of a conversion function: pointer and reference operators only.</summary>
    Conversion,

    /// <summary>The declarator of a new-expression: pointers and arrays, <c>new int *[n][3]</c>.</summary>
    New,
}

// Declarators ([dcl.decl]): pointers, references, pointers to members, arrays, functions and parentheses.
internal ref partial struct SyntaxParser
{
    /// <summary>
    /// A declarator of <paramref name="kind"/>. Returns false when the input is not valid; an abstract
    /// declarator may be empty, and <paramref name="declarator"/> is then null.
    /// </summary>
    private bool TryParseDeclarator(DeclaratorKind kind, out Declarator declarator)
    {
        EnsureSufficientStack();
        declarator = null;
        var start = NodeStart;

        // ptr-operator: * cv, & , &&, nested-name-specifier * cv; a new-expression is followed by '&&' in new int && b
        if (IsPunctuator("*") || (kind != DeclaratorKind.New && (IsPunctuator("&") || IsPunctuator("&&"))))
        {
            var @operator = EatToken().Text;
            var qualifiers = @operator == "*" ? ParseCvQualifiers() : null;
            if (!TryParseDeclarator(kind, out var inner) || (inner == null && kind == DeclaratorKind.Named))
            {
                return false;
            }

            declarator = @operator == "*"
                ? Finish(new PointerDeclarator(inner, qualifiers), start)
                : Finish(new ReferenceDeclarator(inner, isRvalue: @operator == "&&"), start);
            return true;
        }

        if (IsNameStart(Current, NameContext.Type) && TryParseMemberPointerClass(out var @class))
        {
            var qualifiers = ParseCvQualifiers();
            if (!TryParseDeclarator(kind, out var inner) || (inner == null && kind == DeclaratorKind.Named))
            {
                return false;
            }

            declarator = Finish(new MemberPointerDeclarator(@class, inner, qualifiers), start);
            return true;
        }

        if (kind == DeclaratorKind.Conversion)
        {
            return true;
        }

        return TryParseNoPointerDeclarator(kind, out declarator);
    }

    /// <summary>
    /// The class of a pointer to member: a name followed by <c>::*</c>, which are consumed.
    /// </summary>
    private bool TryParseMemberPointerClass(out Name @class)
    {
        var mark = Save();
        @class = ParseName(NameContext.Type);
        if (@class != null && IsPunctuator("::") && Peek(1).IsPunctuator("*"))
        {
            EatTokens(2);
            return true;
        }

        Restore(mark);
        @class = null;
        return false;
    }

    /// <summary>
    /// <c>const</c>, <c>volatile</c> and the GNU <c>__restrict</c>, in source order.
    /// </summary>
    private List<string> ParseCvQualifiers()
    {
        var qualifiers = new List<string>();
        while (IsKeyword("const") || IsKeyword("volatile") || IsRestrict(Current))
        {
            qualifiers.Add(EatToken().Text);
        }

        return qualifiers;
    }

    /// <summary>
    /// noptr-declarator: a declarator id, a pack or a parenthesized declarator, followed by parameter lists
    /// and array bounds.
    /// </summary>
    private bool TryParseNoPointerDeclarator(DeclaratorKind kind, out Declarator declarator)
    {
        declarator = null;
        var start = NodeStart;

        if (kind == DeclaratorKind.New)
        {
            return TryParseNewArrayDeclarator(out declarator);
        }

        if (IsPunctuator("(") && IsNestedDeclarator(kind))
        {
            EatToken();
            if (!TryParseDeclarator(kind, out var inner) || inner == null || !TryEatPunctuator(")"))
            {
                return false;
            }

            declarator = Finish(new ParenthesizedDeclarator(inner), start);
        }
        else if (IsPunctuator("...") && kind == DeclaratorKind.Parameter && (IsNameStart(Peek(1), NameContext.Declarator) || _parameterTypeIsPack))
        {
            // A parameter pack: ...args, or without a name when the type names a pack: Ts..., Ts &&...
            // Otherwise a '...' before ')' is the ellipsis of a variadic function
            EatToken();
            Declarator inner = null;
            if (IsNameStart(Current, NameContext.Declarator))
            {
                inner = ParseNameDeclarator();
                if (inner == null)
                {
                    return false;
                }
            }

            declarator = Finish(new PackDeclarator(inner), start);
        }
        else if (kind == DeclaratorKind.Named && IsPunctuator("[") && !Peek(1).IsPunctuator("["))
        {
            // A structured binding takes no parameters or array bounds
            return TryParseStructuredBindingDeclarator(out declarator);
        }
        else if (kind != DeclaratorKind.Abstract && IsNameStart(Current, NameContext.Declarator))
        {
            declarator = ParseNameDeclarator();
            if (declarator == null)
            {
                return false;
            }
        }
        else if (kind == DeclaratorKind.Named)
        {
            return false;
        }

        // Parameter lists and array bounds
        while (true)
        {
            if (IsPunctuator("("))
            {
                // In a declaration, a '(' after the name that starts no parameters starts an initializer
                var mark = Save();
                var function = ParseFunctionDeclaratorRest(declarator, start);
                if (function == null)
                {
                    Restore(mark);
                    if (kind == DeclaratorKind.Named && declarator != null)
                    {
                        break;
                    }

                    return false;
                }

                declarator = function;
            }
            else if (IsPunctuator("[") && !Peek(1).IsPunctuator("["))
            {
                EatToken();
                Expression size = null;
                if (!IsPunctuator("]"))
                {
                    var saved = EnterBrackets();
                    size = ParseAssignmentExpression();
                    LeaveBrackets(saved);
                    if (size == null)
                    {
                        return false;
                    }
                }

                if (!TryEatPunctuator("]"))
                {
                    return false;
                }

                declarator = Finish(new ArrayDeclarator(declarator, size), start);
            }
            else
            {
                break;
            }
        }

        return true;
    }

    /// <summary>
    /// A declarator id and the attributes after it: <c>a [[maybe_unused]]</c>.
    /// </summary>
    private NameDeclarator ParseNameDeclarator()
    {
        var start = NodeStart;
        var name = ParseName(NameContext.Declarator);
        if (name == null)
        {
            return null;
        }

        var attributes = ParseStandardAttributeSpecifiers();
        return attributes == null ? null : Finish(new NameDeclarator(name) { Attributes = attributes }, start);
    }

    /// <summary>
    /// <c>[a, b]</c> of a structured binding declaration.
    /// </summary>
    private bool TryParseStructuredBindingDeclarator(out Declarator declarator)
    {
        declarator = null;
        var start = NodeStart;
        EatToken();
        var names = new List<IdentifierName>();
        do
        {
            var nameStart = NodeStart;
            var identifier = TryEatIdentifier();
            if (identifier == null)
            {
                return false;
            }

            names.Add(Finish(new IdentifierName(identifier), nameStart));
        }
        while (TryEatPunctuator(","));

        if (!TryEatPunctuator("]"))
        {
            return false;
        }

        declarator = Finish(new StructuredBindingDeclarator(names), start);
        return true;
    }

    /// <summary>
    /// The array bounds of the type of a new-expression, if any: the first may be any expression or empty.
    /// </summary>
    private bool TryParseNewArrayDeclarator(out Declarator declarator)
    {
        declarator = null;
        var start = NodeStart;
        while (IsPunctuator("[") && !Peek(1).IsPunctuator("["))
        {
            EatToken();
            Expression size = null;
            if (!IsPunctuator("]"))
            {
                var saved = EnterBrackets();
                size = ParseExpression();
                LeaveBrackets(saved);
                if (size == null)
                {
                    return false;
                }
            }

            if (!TryEatPunctuator("]"))
            {
                return false;
            }

            declarator = Finish(new ArrayDeclarator(declarator, size), start);
        }

        return true;
    }

    /// <summary>
    /// A '(' at the start of a declarator starts a nested declarator, not a parameter list: always in a
    /// named declarator; in an abstract one when a pointer operator follows; in a parameter also when a
    /// name follows that is not a type ([dcl.ambig.res]: <c>int (x)</c> declares <c>x</c>, <c>int (T)</c>
    /// is a function taking a <c>T</c>).
    /// </summary>
    private bool IsNestedDeclarator(DeclaratorKind kind)
    {
        if (kind == DeclaratorKind.Named)
        {
            return true;
        }

        var next = Peek(1);
        if (next.IsPunctuator("*") || next.IsPunctuator("&") || next.IsPunctuator("&&") || next.IsPunctuator("("))
        {
            return true;
        }

        if (!IsNameStart(next, NameContext.Type))
        {
            return false;
        }

        // A pointer to member: (S::*)
        var mark = Save();
        EatToken();
        var isMemberPointer = TryParseMemberPointerClass(out _);
        Restore(mark);
        if (isMemberPointer)
        {
            return true;
        }

        if (kind != DeclaratorKind.Parameter || !next.IsIdentifier)
        {
            return false;
        }

        return _cache.Symbols.Lookup(next.Text) is not (SymbolKind.Type or SymbolKind.Template) && !ExtensionTypeNames.Contains(next.Text);
    }

    /// <summary>
    /// The parameters after <paramref name="inner"/> and what follows them: cv- and ref-qualifiers,
    /// <c>noexcept</c> and a trailing return type. Null when there is no parameter list.
    /// </summary>
    private FunctionDeclarator ParseFunctionDeclaratorRest(Declarator inner, int start)
    {
        if (!TryEatPunctuator("("))
        {
            return null;
        }

        var saved = EnterBrackets();
        var parameters = ParseParameterClause(out var isVariadic);
        LeaveBrackets(saved);
        if (parameters == null)
        {
            return null;
        }

        var qualifiers = ParseCvQualifiers();
        string refQualifier = null;
        if (IsPunctuator("&") || IsPunctuator("&&"))
        {
            refQualifier = EatToken().Text;
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
        else if (IsKeyword("throw") && Peek(1).IsPunctuator("(") && Peek(2).IsPunctuator(")"))
        {
            var throwStart = NodeStart;
            EatTokens(3);
            noexcept = Finish(new NoexceptSpecifier { IsThrow = true }, throwStart);
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

        return Finish(
            new FunctionDeclarator(inner, parameters)
            {
                IsVariadic = isVariadic,
                Qualifiers = qualifiers,
                RefQualifier = refQualifier,
                Noexcept = noexcept,
                TrailingReturnType = trailingReturnType,
            },
            start);
    }

    /// <summary>
    /// <c>noexcept</c> or <c>noexcept(condition)</c>.
    /// </summary>
    private NoexceptSpecifier ParseNoexceptSpecifier()
    {
        var start = NodeStart;
        EatToken();
        Expression condition = null;
        if (TryEatPunctuator("("))
        {
            var saved = EnterBrackets();
            condition = ParseAssignmentExpression();
            LeaveBrackets(saved);
            if (condition == null || !TryEatPunctuator(")"))
            {
                return null;
            }
        }

        return Finish(new NoexceptSpecifier(condition), start);
    }

    /// <summary>
    /// The parameters after '(' up to and including ')', and whether they end with the ellipsis of a
    /// variadic function: <c>(int, ...)</c>, <c>(int...)</c> or <c>(...)</c>.
    /// </summary>
    private List<ParameterDeclaration> ParseParameterClause(out bool isVariadic)
    {
        isVariadic = false;
        var parameters = new List<ParameterDeclaration>();
        if (TryEatPunctuator(")"))
        {
            return parameters;
        }

        while (true)
        {
            if (TryEatPunctuator("..."))
            {
                isVariadic = true;
                return TryEatPunctuator(")") ? parameters : null;
            }

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

            if (IsPunctuator("...") && Peek(1).IsPunctuator(")"))
            {
                EatTokens(2);
                isVariadic = true;
                return parameters;
            }

            if (!TryEatPunctuator(","))
            {
                return null;
            }
        }
    }

    /// <summary>
    /// A parameter: attributes, <c>this</c> for an explicit object parameter, specifiers, a named or abstract
    /// declarator and a default argument.
    /// </summary>
    private ParameterDeclaration ParseParameter()
    {
        var start = NodeStart;
        var attributes = ParseAttributeSpecifiers();
        if (attributes == null)
        {
            return null;
        }

        var isExplicitObject = TryEatKeyword("this");
        var specifiers = ParseDeclSpecifiers();
        if (specifiers == null)
        {
            return null;
        }

        var saved = _parameterTypeIsPack;
        _parameterTypeIsPack = NamesPack(specifiers, _cache.Symbols);
        var parsed = TryParseDeclarator(DeclaratorKind.Parameter, out var declarator);
        _parameterTypeIsPack = saved;
        if (!parsed)
        {
            return null;
        }

        Expression defaultValue = null;
        if (TryEatPunctuator("="))
        {
            defaultValue = ParseInitializerClause();
            if (defaultValue == null)
            {
                return null;
            }
        }

        return Finish(new ParameterDeclaration(specifiers, declarator, defaultValue) { Attributes = attributes, IsExplicitObject = isExplicitObject }, start);
    }

    /// <summary>
    /// The type names a template parameter pack that is not expanded: <c>Ts</c>, <c>const Box&lt;Ts&gt;</c>.
    /// </summary>
    private static bool NamesPack(DeclSpecifierSequence specifiers, Symbols symbols)
    {
        return specifiers.Specifiers.Any(s => s is NamedTypeSpecifier named && NamesPack(named.Name, symbols));
    }

    private static bool NamesPack(Name name, Symbols symbols) => name switch
    {
        IdentifierName identifier => symbols.IsPack(identifier.Identifier),
        TemplateIdName templateId => NamesPack(templateId.Template, symbols)
            || templateId.Arguments.Any(a => a is TypeId { IsPackExpansion: false } type && NamesPack(type.Specifiers, symbols)),
        QualifiedName qualified => (qualified.Qualifier != null && NamesPack(qualified.Qualifier, symbols)) || NamesPack(qualified.Name, symbols),
        _ => false,
    };

    // ========================================
    // Declarator queries
    // ========================================

    /// <summary>
    /// The name a declarator declares, or null for an abstract declarator.
    /// </summary>
    public static NameDeclarator DeclaredName(Declarator declarator) => declarator switch
    {
        NameDeclarator name => name,
        PackDeclarator pack => DeclaredName(pack.Inner),
        PointerDeclarator pointer => DeclaredName(pointer.Inner),
        ReferenceDeclarator reference => DeclaredName(reference.Inner),
        MemberPointerDeclarator member => DeclaredName(member.Inner),
        ArrayDeclarator array => DeclaredName(array.Inner),
        FunctionDeclarator function => DeclaredName(function.Inner),
        ParenthesizedDeclarator parenthesized => DeclaredName(parenthesized.Inner),
        _ => null,
    };

    /// <summary>
    /// The declarator declares a pack: <c>...args</c>, <c>&amp;&amp;...args</c>.
    /// </summary>
    public static bool IsPackDeclarator(Declarator declarator) => declarator switch
    {
        PackDeclarator => true,
        PointerDeclarator pointer => IsPackDeclarator(pointer.Inner),
        ReferenceDeclarator reference => IsPackDeclarator(reference.Inner),
        MemberPointerDeclarator member => IsPackDeclarator(member.Inner),
        _ => false,
    };

    /// <summary>
    /// The structured binding a declarator declares, possibly by reference: <c>&amp;[a, b]</c>; null for other declarators.
    /// </summary>
    public static StructuredBindingDeclarator StructuredBinding(Declarator declarator) => declarator switch
    {
        StructuredBindingDeclarator binding => binding,
        ReferenceDeclarator reference => StructuredBinding(reference.Inner),
        _ => null,
    };

    /// <summary>
    /// The declarator applied first to the declared name, without parentheses: the function declarator in
    /// <c>(*f(int))[3]</c> (a function returning a pointer to an array). Null when there is no name or
    /// nothing is applied to it.
    /// </summary>
    public static Declarator InnermostOperator(Declarator declarator)
    {
        Declarator found = null;
        var current = declarator;
        while (current != null)
        {
            var inner = current switch
            {
                PackDeclarator pack => pack.Inner,
                PointerDeclarator pointer => pointer.Inner,
                ReferenceDeclarator reference => reference.Inner,
                MemberPointerDeclarator member => member.Inner,
                ArrayDeclarator array => array.Inner,
                FunctionDeclarator function => function.Inner,
                ParenthesizedDeclarator parenthesized => parenthesized.Inner,
                _ => null,
            };

            if (current is not (ParenthesizedDeclarator or PackDeclarator or NameDeclarator))
            {
                found = current;
            }

            current = inner;
        }

        return found;
    }

    /// <summary>
    /// The declarator declares a function: the first thing applied to the name is a parameter list.
    /// </summary>
    public static bool DeclaresFunction(Declarator declarator) => DeclaredName(declarator) != null && InnermostOperator(declarator) is FunctionDeclarator;
}
