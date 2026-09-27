namespace PaspanParsers.CSharp;

/// <summary>
/// Where a member declaration appears; it decides which declarations are allowed.
/// </summary>
internal enum MemberContext
{
    /// <summary>Types, namespaces and top-level statements.</summary>
    CompilationUnit,

    /// <summary>Types and namespaces.</summary>
    Namespace,

    /// <summary>Members of a type, including nested types.</summary>
    Type,
}

// Stage 6: type and member declarations.
internal ref partial struct SyntaxParser
{
    /// <summary>
    /// [attributes] modifier* declaration. At the top level of the compilation unit anything that is not
    /// a type or namespace declaration is a statement (<see cref="GlobalStatement"/>): like in Roslyn,
    /// methods and variables there are local functions and local declarations.
    /// </summary>
    private MemberDeclaration ParseMemberDeclaration(MemberContext context)
    {
        EnsureSufficientStack();

        var start = _position;
        var spanStart = NodeStart;
        var nullableDirectives = Current.NullableDirectives;
        var attributes = ParseAttributeSections();
        var modifiers = ParseMemberModifiers();

        var member = ParseMemberDeclarationRest(context, attributes.Count != 0 ? attributes : null, modifiers);
        if (member != null)
        {
            member.NullableDirectives = nullableDirectives;
            return Finish(member, spanStart);
        }

        if (context != MemberContext.CompilationUnit)
        {
            return null;
        }

        // The statement keeps the #nullable directives before it

        _position = start;
        var statement = ParseStatement();
        return statement == null ? null : Finish(new GlobalStatement(statement), statement);
    }

    private MemberDeclaration ParseMemberDeclarationRest(MemberContext context, List<AttributeSection> attributes, List<Modifiers> modifierList)
    {
        var modifiers = Combine(modifierList);
        var list = modifierList.Count != 0 ? modifierList : null;
        var token = Current;

        if (token.Kind == TokenKind.Keyword)
        {
            switch (token.Text)
            {
                case "class":
                case "struct":
                case "interface":
                    return ParseTypeDeclaration(attributes, modifiers, list);
                case "enum":
                    return ParseEnumDeclaration(attributes, modifiers, list);
                // Not a function pointer type (delegate*) or, at the top level, an anonymous method
                case "delegate" when !Peek(1).IsPunctuator("*")
                    && (context != MemberContext.CompilationUnit || (!Peek(1).IsPunctuator("(") && !Peek(1).IsPunctuator("{"))):
                    return ParseDelegateDeclaration(attributes, modifiers, list);
                case "namespace" when context != MemberContext.Type && attributes == null && list == null:
                    return ParseNamespaceDeclaration(context == MemberContext.CompilationUnit);
            }
        }

        if (token.IsContextual("record") && IsRecordDeclarationStart())
        {
            return ParseTypeDeclaration(attributes, modifiers, list);
        }

        if (context != MemberContext.Type)
        {
            return null;
        }

        if (token.IsPunctuator("~"))
        {
            return ParseDestructorDeclaration(attributes, modifiers, list);
        }

        if (token.IsKeyword("event"))
        {
            return ParseEventDeclaration(attributes, modifiers, list);
        }

        if (token.IsKeyword("implicit") || token.IsKeyword("explicit"))
        {
            return ParseConversionOperatorDeclaration(attributes, modifiers, list);
        }

        if (token.IsContextual("extension") && (Peek(1).IsPunctuator("(") || Peek(1).IsPunctuator("<")))
        {
            return ParseExtensionBlockDeclaration(attributes, modifiers, list);
        }

        if (token.IsIdentifier && Peek(1).IsPunctuator("("))
        {
            return ParseConstructorDeclaration(attributes, modifiers, list);
        }

        var type = ParseReturnType();
        if (type == null)
        {
            return null;
        }

        if (IsKeyword("operator"))
        {
            return ParseOperatorDeclarationRest(type, null, attributes, modifiers, list);
        }

        var explicitInterface = ParseExplicitInterfaceSpecifier();

        if (IsKeyword("this"))
        {
            return ParseIndexerDeclarationRest(type, explicitInterface, attributes, modifiers, list);
        }

        if (IsKeyword("operator"))
        {
            return explicitInterface == null ? null : ParseOperatorDeclarationRest(type, explicitInterface, attributes, modifiers, list);
        }

        var nameStart = NodeStart;
        var name = TryEatIdentifier();
        if (name == null)
        {
            return null;
        }

        if (IsPunctuator("(") || IsPunctuator("<"))
        {
            return ParseMethodDeclarationRest(type, explicitInterface, name, attributes, modifiers, list);
        }

        if (IsPunctuator("{") || IsPunctuator("=>"))
        {
            return ParsePropertyDeclarationRest(type, explicitInterface, name, attributes, modifiers, list);
        }

        return explicitInterface == null ? ParseFieldDeclarationRest(type, name, nameStart, attributes, modifiers, list) : null;
    }

    // ========================================
    // Modifiers
    // ========================================

    private static Modifiers Combine(List<Modifiers> modifiers)
    {
        var result = Modifiers.None;
        foreach (var modifier in modifiers)
        {
            result |= modifier;
        }

        return result;
    }

    /// <summary>
    /// Modifiers of a type or member in source order. The contextual modifiers <c>partial</c>, <c>async</c>,
    /// <c>required</c>, <c>file</c> and <c>safe</c> are modifiers only when a declaration follows them.
    /// </summary>
    private List<Modifiers> ParseMemberModifiers()
    {
        var modifiers = new List<Modifiers>();
        while (true)
        {
            var token = Current;
            Modifiers? modifier = null;
            if (token.Kind == TokenKind.Keyword)
            {
                modifier = token.Text switch
                {
                    "public" => Modifiers.Public,
                    "private" => Modifiers.Private,
                    "protected" => Modifiers.Protected,
                    "internal" => Modifiers.Internal,
                    "static" => Modifiers.Static,
                    "readonly" => Modifiers.Readonly,
                    "const" => Modifiers.Const,
                    "virtual" => Modifiers.Virtual,
                    "override" => Modifiers.Override,
                    "abstract" => Modifiers.Abstract,
                    "sealed" => Modifiers.Sealed,
                    "extern" => Modifiers.Extern,
                    "unsafe" => Modifiers.Unsafe,
                    "volatile" => Modifiers.Volatile,
                    "new" => Modifiers.New,
                    "fixed" => Modifiers.Fixed,
                    // 'ref' is a modifier of ref structs; before a type it starts a by-reference return type
                    "ref" when Peek(1).IsKeyword("struct") || Peek(1).IsContextual("partial") => Modifiers.Ref,
                    _ => null,
                };
            }
            else if (token.IsIdentifier && !token.IsVerbatim)
            {
                modifier = token.Text switch
                {
                    "partial" => Modifiers.Partial,
                    "async" => Modifiers.Async,
                    "required" => Modifiers.Required,
                    "file" => Modifiers.File,
                    "safe" => Modifiers.Safe,
                    _ => null,
                };

                if (modifier.HasValue && !IsContextualModifier())
                {
                    modifier = null;
                }
            }

            if (!modifier.HasValue)
            {
                return modifiers;
            }

            EatToken();
            modifiers.Add(modifier.Value);
        }
    }

    /// <summary>
    /// The current contextual keyword is a modifier when a keyword, another contextual modifier or
    /// a type and a member name follow it (<c>async Task Run()</c>), and not when it is itself a type
    /// (<c>async x;</c>) or a name.
    /// </summary>
    private bool IsContextualModifier()
    {
        var next = Peek(1);
        switch (next.Kind)
        {
            case TokenKind.Keyword:
                return true;

            case TokenKind.Identifier:
                break;

            default:
                return false;
        }

        if (!next.IsVerbatim && next.Text is "partial" or "async" or "required" or "file" or "safe" or "record")
        {
            return true;
        }

        // A partial constructor (C# 14): partial C(int x);
        if (IsContextual("partial") && Peek(2).IsPunctuator("("))
        {
            return true;
        }

        var start = _position;
        EatToken();
        var type = ParseReturnType();
        var result = type != null && (Current.IsIdentifier || IsKeyword("this") || IsKeyword("operator"));
        _position = start;
        return result;
    }

    /// <summary>
    /// 'record' starts a record declaration when a name or 'class'/'struct' and a name follow it.
    /// </summary>
    private bool IsRecordDeclarationStart()
    {
        var next = Peek(1);
        if (next.IsIdentifier)
        {
            return true;
        }

        return (next.IsKeyword("class") || next.IsKeyword("struct")) && Peek(2).IsIdentifier;
    }

    // ========================================
    // Namespaces
    // ========================================

    /// <summary>
    /// namespace Name ( '{' body '}' [';'] | ';' body ), where the body of a file-scoped namespace runs to the end of the file.
    /// </summary>
    private MemberDeclaration ParseNamespaceDeclaration(bool allowFileScoped)
    {
        EatToken();

        var nameStart = NodeStart;
        var parts = new List<string>();
        while (true)
        {
            var part = TryEatIdentifier();
            if (part == null)
            {
                return null;
            }

            parts.Add(part);
            if (!TryEatPunctuator("."))
            {
                break;
            }
        }

        var name = Finish(new NameExpression(parts), nameStart);
        var externs = new List<ExternAliasDirective>();
        var usings = new List<UsingDirective>();
        var members = new List<MemberDeclaration>();

        if (allowFileScoped && TryEatPunctuator(";"))
        {
            // The members run to the end of the file, whose #nullable directives belong to the compilation unit
            ParseExternsAndUsings(externs, usings);
            while (Current.Kind != TokenKind.EndOfFile)
            {
                var member = ParseMemberDeclaration(MemberContext.Namespace);
                if (member == null)
                {
                    return null;
                }

                members.Add(member);
            }

            return new NamespaceDeclaration(name, NullIfEmpty(members), NullIfEmpty(usings), NullIfEmpty(externs), isFileScopedNamespace: true);
        }

        if (!TryEatPunctuator("{"))
        {
            return null;
        }

        ParseExternsAndUsings(externs, usings);
        while (!IsPunctuator("}"))
        {
            var member = ParseMemberDeclaration(MemberContext.Namespace);
            if (member == null)
            {
                return null;
            }

            members.Add(member);
        }

        var closeBraceDirectives = EatToken().NullableDirectives;
        return new NamespaceDeclaration(name, NullIfEmpty(members), NullIfEmpty(usings), NullIfEmpty(externs))
        {
            HasTrailingSemicolon = TryEatPunctuator(";"),
            CloseBraceNullableDirectives = closeBraceDirectives,
        };
    }

    private static List<T> NullIfEmpty<T>(List<T> list) => list.Count != 0 ? list : null;

    // ========================================
    // Types
    // ========================================

    /// <summary>
    /// class, struct, interface and record declarations:
    /// keyword Name [type parameters] [parameters] [':' base types] constraints ('{' members '}' [';'] | ';')
    /// </summary>
    private MemberDeclaration ParseTypeDeclaration(List<AttributeSection> attributes, Modifiers modifiers, List<Modifiers> modifierList)
    {
        var keyword = EatToken().Text;
        var isRecordStruct = false;
        var hasClassKeyword = false;
        if (keyword == "record")
        {
            isRecordStruct = TryEatKeyword("struct");
            hasClassKeyword = !isRecordStruct && TryEatKeyword("class");
        }

        var name = TryEatIdentifier();
        if (name == null)
        {
            return null;
        }

        List<TypeParameter> typeParameters = null;
        if (IsPunctuator("<"))
        {
            typeParameters = ParseTypeParameterList();
            if (typeParameters == null)
            {
                return null;
            }
        }

        List<Parameter> parameters = null;
        if (keyword != "interface" && IsPunctuator("("))
        {
            parameters = ParseParameterList(allowImplicitTypes: false);
            if (parameters == null)
            {
                return null;
            }
        }

        List<TypeReference> baseTypes = null;
        List<Argument> baseArguments = null;
        if (TryEatPunctuator(":"))
        {
            baseTypes = [];
            while (true)
            {
                var baseType = ParseType(TypeMode.Normal);
                if (baseType == null)
                {
                    return null;
                }

                baseTypes.Add(baseType);

                // The base class of a primary constructor takes arguments: record B(int X) : A(X);
                if (baseTypes.Count == 1 && IsPunctuator("("))
                {
                    baseArguments = ParseArgumentList("(", ")");
                    if (baseArguments == null)
                    {
                        return null;
                    }
                }

                if (!TryEatPunctuator(","))
                {
                    break;
                }
            }
        }

        var constraints = ParseConstraintClauses();
        if (constraints == null)
        {
            return null;
        }

        List<MemberDeclaration> members = null;
        IReadOnlyList<NullableDirective> openBraceDirectives = null;
        IReadOnlyList<NullableDirective> closeBraceDirectives = null;
        var hasBody = !TryEatPunctuator(";");
        var hasTrailingSemicolon = false;
        if (hasBody)
        {
            members = ParseTypeBody(out openBraceDirectives, out closeBraceDirectives);
            if (members == null)
            {
                return null;
            }

            hasTrailingSemicolon = TryEatPunctuator(";");
        }

        var constraintList = constraints.Count != 0 ? constraints : null;
        members = members != null ? NullIfEmpty(members) : null;

        return keyword switch
        {
            "class" => new ClassDeclaration(name, attributes, modifiers, typeParameters, baseTypes, constraintList, members, parameters)
            {
                ModifierList = modifierList, BaseArguments = baseArguments, HasBody = hasBody, HasTrailingSemicolon = hasTrailingSemicolon,
                OpenBraceNullableDirectives = openBraceDirectives, CloseBraceNullableDirectives = closeBraceDirectives,
            },
            "struct" => new StructDeclaration(name, attributes, modifiers, typeParameters, baseTypes, constraintList, members, parameters)
            {
                ModifierList = modifierList, BaseArguments = baseArguments, HasBody = hasBody, HasTrailingSemicolon = hasTrailingSemicolon,
                OpenBraceNullableDirectives = openBraceDirectives, CloseBraceNullableDirectives = closeBraceDirectives,
            },
            "interface" => new InterfaceDeclaration(name, attributes, modifiers, typeParameters, baseTypes, constraintList, members)
            {
                ModifierList = modifierList, BaseArguments = baseArguments, HasBody = hasBody, HasTrailingSemicolon = hasTrailingSemicolon,
                OpenBraceNullableDirectives = openBraceDirectives, CloseBraceNullableDirectives = closeBraceDirectives,
            },
            _ => new RecordDeclaration(name, isRecordStruct, attributes, modifiers, typeParameters, parameters, baseTypes, constraintList, members)
            {
                ModifierList = modifierList, BaseArguments = baseArguments, HasBody = hasBody, HasTrailingSemicolon = hasTrailingSemicolon,
                OpenBraceNullableDirectives = openBraceDirectives, CloseBraceNullableDirectives = closeBraceDirectives,
                HasClassKeyword = hasClassKeyword,
            },
        };
    }

    /// <summary>
    /// '{' member* '}'
    /// </summary>
    private List<MemberDeclaration> ParseTypeBody(out IReadOnlyList<NullableDirective> openBraceDirectives, out IReadOnlyList<NullableDirective> closeBraceDirectives)
    {
        openBraceDirectives = Current.NullableDirectives;
        closeBraceDirectives = null;
        if (!TryEatPunctuator("{"))
        {
            return null;
        }

        var members = new List<MemberDeclaration>();
        while (!IsPunctuator("}"))
        {
            var member = ParseMemberDeclaration(MemberContext.Type);
            if (member == null)
            {
                return null;
            }

            members.Add(member);
        }

        closeBraceDirectives = EatToken().NullableDirectives;
        return members;
    }

    /// <summary>
    /// enum Name [':' Type] '{' [member (',' member)* [',']] '}' [';']
    /// </summary>
    private MemberDeclaration ParseEnumDeclaration(List<AttributeSection> attributes, Modifiers modifiers, List<Modifiers> modifierList)
    {
        EatToken();
        var name = TryEatIdentifier();
        if (name == null)
        {
            return null;
        }

        TypeReference baseType = null;
        if (TryEatPunctuator(":"))
        {
            baseType = ParseType(TypeMode.Normal);
            if (baseType == null)
            {
                return null;
            }
        }

        if (!TryEatPunctuator("{"))
        {
            return null;
        }

        var members = new List<EnumMember>();
        var hasTrailingComma = false;
        while (!TryEatPunctuator("}"))
        {
            var memberStart = NodeStart;
            var memberAttributes = ParseAttributeSections();
            var memberName = TryEatIdentifier();
            if (memberName == null)
            {
                return null;
            }

            Expression value = null;
            if (TryEatPunctuator("="))
            {
                value = ParseExpression();
                if (value == null)
                {
                    return null;
                }
            }

            members.Add(Finish(new EnumMember(memberName, value, NullIfEmpty(memberAttributes)), memberStart));

            if (TryEatPunctuator(","))
            {
                hasTrailingComma = IsPunctuator("}");
                continue;
            }

            if (!TryEatPunctuator("}"))
            {
                return null;
            }

            break;
        }

        return new EnumDeclaration(name, attributes, modifiers, baseType, NullIfEmpty(members))
        {
            ModifierList = modifierList,
            HasBody = true,
            HasTrailingComma = hasTrailingComma,
            HasTrailingSemicolon = TryEatPunctuator(";"),
        };
    }

    /// <summary>
    /// delegate ReturnType Name [type parameters] '(' parameters ')' constraints ';'
    /// </summary>
    private MemberDeclaration ParseDelegateDeclaration(List<AttributeSection> attributes, Modifiers modifiers, List<Modifiers> modifierList)
    {
        EatToken();
        var returnType = ParseReturnType();
        if (returnType == null)
        {
            return null;
        }

        var name = TryEatIdentifier();
        if (name == null)
        {
            return null;
        }

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
        if (constraints == null || !TryEatPunctuator(";"))
        {
            return null;
        }

        return new DelegateDeclaration(returnType, name, attributes, modifiers, typeParameters, parameters, NullIfEmpty(constraints))
        {
            ModifierList = modifierList,
        };
    }

    /// <summary>
    /// extension [type parameters] '(' receiver ')' constraints '{' members '}' (C# 14)
    /// </summary>
    private MemberDeclaration ParseExtensionBlockDeclaration(List<AttributeSection> attributes, Modifiers modifiers, List<Modifiers> modifierList)
    {
        EatToken();

        List<TypeParameter> typeParameters = null;
        if (IsPunctuator("<"))
        {
            typeParameters = ParseTypeParameterList();
            if (typeParameters == null)
            {
                return null;
            }
        }

        if (!TryEatPunctuator("("))
        {
            return null;
        }

        var receiver = ParseParameter(allowImplicitTypes: false, allowMissingName: true);
        if (receiver == null || !TryEatPunctuator(")"))
        {
            return null;
        }

        var constraints = ParseConstraintClauses();
        if (constraints == null)
        {
            return null;
        }

        var members = ParseTypeBody(out _, out var closeBraceDirectives);
        if (members == null)
        {
            return null;
        }

        return new ExtensionBlockDeclaration(receiver, NullIfEmpty(members), typeParameters, NullIfEmpty(constraints), attributes, modifiers)
        {
            ModifierList = modifierList,
            CloseBraceNullableDirectives = closeBraceDirectives,
        };
    }

    // ========================================
    // Members
    // ========================================

    /// <summary>
    /// The interface of an explicit implementation up to and including the last '.':
    /// <c>IEquatable&lt;T&gt;.</c> in <c>bool IEquatable&lt;T&gt;.Equals(T other)</c>. Returns null
    /// and consumes nothing when the member name is not qualified.
    /// </summary>
    private TypeReference ParseExplicitInterfaceSpecifier()
    {
        var start = _position;
        var spanStart = NodeStart;
        var partsStart = spanStart;

        string alias = null;
        if (Current.IsIdentifier && Peek(1).IsPunctuator("::"))
        {
            alias = EatToken().Text;
            EatToken();
            partsStart = NodeStart;
        }

        TypeReference qualifier = null;
        TypeReference result = null;
        var parts = new List<string>();
        var partsEnd = partsStart;
        while (Current.IsIdentifier)
        {
            var segmentStart = _position;
            var part = EatToken().Text;
            var partEnd = _position;

            List<TypeReference> typeArguments = null;
            IReadOnlyList<NullableDirective> closeDirectives = null;
            if (IsPunctuator("<"))
            {
                typeArguments = ParseTypeArgumentList(out closeDirectives);
                if (typeArguments == null)
                {
                    _position = segmentStart;
                    break;
                }
            }

            var next = Peek(1);
            if (!IsPunctuator(".") || !(next.IsIdentifier || next.IsKeyword("this") || next.IsKeyword("operator")))
            {
                _position = segmentStart;
                break;
            }

            var segmentEnd = _position;
            EatToken();
            parts.Add(part);
            partsEnd = partEnd;
            if (typeArguments != null)
            {
                var qualifierName = Finish(new NameExpression(parts), partsStart, partsEnd);
                var qualifierType = new NamedTypeReference(qualifierName, typeArguments, false, qualifier, alias) { CloseAngleNullableDirectives = closeDirectives };
                qualifier = Finish(qualifierType, spanStart, segmentEnd);
                alias = null;
                parts = [];
                partsStart = NodeStart;
                result = qualifier;
            }
            else
            {
                var name = Finish(new NameExpression(parts.ToList()), partsStart, partsEnd);
                result = Finish(new NamedTypeReference(name, null, false, qualifier, alias), spanStart, segmentEnd);
            }
        }

        if (result == null)
        {
            _position = start;
        }

        return result;
    }

    /// <summary>
    /// A block, '=&gt;' expression ';', or ';' for a member without a body (then <paramref name="body"/> is null).
    /// </summary>
    private bool TryParseMemberBody(out MethodBody body)
    {
        body = null;

        if (IsPunctuator("{"))
        {
            var block = ParseBlock();
            if (block == null)
            {
                return false;
            }

            body = Finish(new BlockMethodBody(block), block);
            return true;
        }

        var arrowStart = NodeStart;
        if (TryEatPunctuator("=>"))
        {
            var expression = ParseExpression();
            if (expression == null || !TryEatPunctuator(";"))
            {
                return false;
            }

            // Like Roslyn's arrow expression clause, the body ends before the ';'
            body = Finish(new ExpressionMethodBody(expression), arrowStart, expression.Span.End);
            return true;
        }

        return TryEatPunctuator(";");
    }

    private MemberDeclaration ParseMethodDeclarationRest(
        TypeReference returnType,
        TypeReference explicitInterface,
        string name,
        List<AttributeSection> attributes,
        Modifiers modifiers,
        List<Modifiers> modifierList)
    {
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
        if (constraints == null || !TryParseMemberBody(out var body))
        {
            return null;
        }

        return new MethodDeclaration(returnType, name, attributes, modifiers, typeParameters, parameters, NullIfEmpty(constraints), body)
        {
            ModifierList = modifierList,
            ExplicitInterface = explicitInterface,
        };
    }

    /// <summary>
    /// Name ('{' accessors '}' ['=' initializer ';'] | '=&gt;' expression ';')
    /// </summary>
    private MemberDeclaration ParsePropertyDeclarationRest(
        TypeReference type,
        TypeReference explicitInterface,
        string name,
        List<AttributeSection> attributes,
        Modifiers modifiers,
        List<Modifiers> modifierList)
    {
        if (TryEatPunctuator("=>"))
        {
            var expression = ParseExpression();
            if (expression == null || !TryEatPunctuator(";"))
            {
                return null;
            }

            return new PropertyDeclaration(type, name, attributes, modifiers, expressionBody: expression)
            {
                ModifierList = modifierList,
                ExplicitInterface = explicitInterface,
            };
        }

        var openBraceDirectives = Current.NullableDirectives;
        var accessors = ParseAccessorList();
        if (accessors == null)
        {
            return null;
        }

        Expression initializer = null;
        if (TryEatPunctuator("="))
        {
            initializer = ParseVariableInitializer();
            if (initializer == null || !TryEatPunctuator(";"))
            {
                return null;
            }
        }

        return new PropertyDeclaration(type, name, attributes, modifiers, accessors, initializer: initializer)
        {
            ModifierList = modifierList,
            ExplicitInterface = explicitInterface,
            OpenBraceNullableDirectives = openBraceDirectives,
        };
    }

    /// <summary>
    /// '{' ([attributes] modifier* (get | set | init) body)* '}'
    /// </summary>
    private List<Accessor> ParseAccessorList()
    {
        if (!TryEatPunctuator("{"))
        {
            return null;
        }

        var accessors = new List<Accessor>();
        while (!TryEatPunctuator("}"))
        {
            var accessorStart = NodeStart;
            var nullableDirectives = Current.NullableDirectives;
            var attributes = ParseAttributeSections();
            var modifierList = ParseMemberModifiers();

            AccessorKind kind;
            if (IsContextual("get"))
            {
                kind = AccessorKind.Get;
            }
            else if (IsContextual("set"))
            {
                kind = AccessorKind.Set;
            }
            else if (IsContextual("init"))
            {
                kind = AccessorKind.Init;
            }
            else
            {
                return null;
            }

            EatToken();
            if (!TryParseMemberBody(out var body))
            {
                return null;
            }

            accessors.Add(Finish(
                new Accessor(kind, NullIfEmpty(attributes), Combine(modifierList), body)
                {
                    ModifierList = NullIfEmpty(modifierList),
                    NullableDirectives = nullableDirectives,
                },
                accessorStart));
        }

        return accessors;
    }

    /// <summary>
    /// this '[' parameters ']' ('{' accessors '}' | '=&gt;' expression ';')
    /// </summary>
    private MemberDeclaration ParseIndexerDeclarationRest(
        TypeReference type,
        TypeReference explicitInterface,
        List<AttributeSection> attributes,
        Modifiers modifiers,
        List<Modifiers> modifierList)
    {
        EatToken();
        if (!TryEatPunctuator("["))
        {
            return null;
        }

        var parameters = ParseParameters("]");
        if (parameters == null || !TryEatPunctuator("]"))
        {
            return null;
        }

        if (TryEatPunctuator("=>"))
        {
            var expression = ParseExpression();
            if (expression == null || !TryEatPunctuator(";"))
            {
                return null;
            }

            return new IndexerDeclaration(type, parameters, null, attributes, modifiers)
            {
                ModifierList = modifierList,
                ExplicitInterface = explicitInterface,
                ExpressionBody = expression,
            };
        }

        var openBraceDirectives = Current.NullableDirectives;
        var accessors = ParseAccessorList();
        if (accessors == null)
        {
            return null;
        }

        return new IndexerDeclaration(type, parameters, accessors, attributes, modifiers)
        {
            ModifierList = modifierList,
            ExplicitInterface = explicitInterface,
            OpenBraceNullableDirectives = openBraceDirectives,
        };
    }

    /// <summary>
    /// identifier ['[' size ']'] ['=' initializer] (',' ...)* ';'
    /// </summary>
    private MemberDeclaration ParseFieldDeclarationRest(
        TypeReference type,
        string firstName,
        int firstNameStart,
        List<AttributeSection> attributes,
        Modifiers modifiers,
        List<Modifiers> modifierList)
    {
        var variables = new List<VariableDeclarator>();
        var name = firstName;
        var nameStart = firstNameStart;
        while (true)
        {
            List<Argument> size = null;
            if (IsPunctuator("["))
            {
                size = ParseArgumentList("[", "]");
                if (size == null)
                {
                    return null;
                }
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

            variables.Add(Finish(new VariableDeclarator(name, initializer) { BracketedArguments = size }, nameStart));

            if (TryEatPunctuator(";"))
            {
                break;
            }

            if (!TryEatPunctuator(","))
            {
                return null;
            }

            nameStart = NodeStart;
            name = TryEatIdentifier();
            if (name == null)
            {
                return null;
            }
        }

        return new FieldDeclaration(type, variables, attributes, modifiers) { ModifierList = modifierList };
    }

    /// <summary>
    /// event Type (declarators ';' | [interface '.'] Name '{' accessors '}')
    /// </summary>
    private MemberDeclaration ParseEventDeclaration(List<AttributeSection> attributes, Modifiers modifiers, List<Modifiers> modifierList)
    {
        EatToken();
        var type = ParseType(TypeMode.Normal);
        if (type == null)
        {
            return null;
        }

        var explicitInterface = ParseExplicitInterfaceSpecifier();
        var start = _position;
        var nameToken = Current;
        var name = TryEatIdentifier();
        if (name == null)
        {
            return null;
        }

        if (IsPunctuator("{"))
        {
            var accessors = ParseEventAccessorList();
            if (accessors == null)
            {
                return null;
            }

            var variable = Finish(new VariableDeclarator(name), nameToken.Start, nameToken.End);
            return new EventDeclaration(type, [variable], attributes, modifiers, accessors)
            {
                ModifierList = modifierList,
                ExplicitInterface = explicitInterface,
            };
        }

        if (explicitInterface != null)
        {
            return null;
        }

        _position = start;
        var variables = ParseVariableDeclarators();
        if (variables == null || !TryEatPunctuator(";"))
        {
            return null;
        }

        return new EventDeclaration(type, variables, attributes, modifiers) { ModifierList = modifierList };
    }

    /// <summary>
    /// '{' ([attributes] (add | remove) (block | '=&gt;' expression ';'))* '}'
    /// </summary>
    private List<EventAccessor> ParseEventAccessorList()
    {
        EatToken();

        var accessors = new List<EventAccessor>();
        while (!TryEatPunctuator("}"))
        {
            var accessorStart = NodeStart;
            var attributes = NullIfEmpty(ParseAttributeSections());

            EventAccessorKind kind;
            if (IsContextual("add"))
            {
                kind = EventAccessorKind.Add;
            }
            else if (IsContextual("remove"))
            {
                kind = EventAccessorKind.Remove;
            }
            else
            {
                return null;
            }

            EatToken();
            if (!TryParseMemberBody(out var body) || body == null)
            {
                return null;
            }

            accessors.Add(Finish(
                body switch
                {
                    BlockMethodBody block => new EventAccessor(kind, block.Block, attributes),
                    ExpressionMethodBody expression => new EventAccessor(kind, null, attributes) { ExpressionBody = expression.Expression },
                    _ => null,
                },
                accessorStart));
        }

        return accessors;
    }

    /// <summary>
    /// Name '(' parameters ')' [':' (base | this) '(' arguments ')'] body
    /// </summary>
    private MemberDeclaration ParseConstructorDeclaration(List<AttributeSection> attributes, Modifiers modifiers, List<Modifiers> modifierList)
    {
        var name = EatToken().Text;
        var parameters = ParseParameterList(allowImplicitTypes: false);
        if (parameters == null)
        {
            return null;
        }

        ConstructorInitializer initializer = null;
        var initializerStart = NodeStart;
        if (TryEatPunctuator(":"))
        {
            var isBase = IsKeyword("base");
            if (!isBase && !IsKeyword("this"))
            {
                return null;
            }

            EatToken();
            var arguments = ParseArgumentList("(", ")");
            if (arguments == null)
            {
                return null;
            }

            initializer = Finish(new ConstructorInitializer(isBase, arguments), initializerStart);
        }

        if (!TryParseMemberBody(out var body))
        {
            return null;
        }

        return new ConstructorDeclaration(name, attributes, modifiers, parameters, initializer, body) { ModifierList = modifierList };
    }

    /// <summary>
    /// '~' Name '(' ')' body
    /// </summary>
    private MemberDeclaration ParseDestructorDeclaration(List<AttributeSection> attributes, Modifiers modifiers, List<Modifiers> modifierList)
    {
        EatToken();
        var name = TryEatIdentifier();
        if (name == null || !TryEatPunctuator("(") || !TryEatPunctuator(")") || !TryParseMemberBody(out var body))
        {
            return null;
        }

        return new DestructorDeclaration(name, attributes, modifiers, body) { ModifierList = modifierList };
    }

    /// <summary>
    /// operator [checked] op '(' parameters ')' body, after the return type and the explicit interface.
    /// </summary>
    private MemberDeclaration ParseOperatorDeclarationRest(
        TypeReference returnType,
        TypeReference explicitInterface,
        List<AttributeSection> attributes,
        Modifiers modifiers,
        List<Modifiers> modifierList)
    {
        EatToken();
        var isChecked = TryEatKeyword("checked");
        var op = ParseOverloadableOperator();
        if (op == null)
        {
            return null;
        }

        var parameters = ParseParameterList(allowImplicitTypes: false);
        if (parameters == null || !TryParseMemberBody(out var body))
        {
            return null;
        }

        return new OperatorDeclaration(returnType, op, parameters, body, attributes, modifiers, isChecked)
        {
            ModifierList = modifierList,
            ExplicitInterface = explicitInterface,
        };
    }

    /// <summary>
    /// The operator of an operator declaration. '&gt;&gt;', '&gt;&gt;&gt;', '&gt;&gt;=' and '&gt;&gt;&gt;='
    /// are composed from adjacent '&gt;' and '&gt;=' tokens, like in expressions.
    /// </summary>
    private string ParseOverloadableOperator()
    {
        var token = Current;
        if (token.IsKeyword("true") || token.IsKeyword("false"))
        {
            EatToken();
            return token.Text;
        }

        if (token.Kind != TokenKind.Punctuator)
        {
            return null;
        }

        if (token.Text == ">")
        {
            var text = ">";
            var last = EatToken();
            while (text.Length < 3)
            {
                var next = Current;
                if (!AreAdjacent(last, next))
                {
                    break;
                }

                if (next.IsPunctuator(">"))
                {
                    text += ">";
                    last = EatToken();
                    continue;
                }

                if (next.IsPunctuator(">="))
                {
                    text += ">=";
                    EatToken();
                }

                break;
            }

            return text;
        }

        switch (token.Text)
        {
            case "+":
            case "-":
            case "!":
            case "~":
            case "++":
            case "--":
            case "*":
            case "/":
            case "%":
            case "&":
            case "|":
            case "^":
            case "<<":
            case "==":
            case "!=":
            case "<":
            case "<=":
            case ">=":
            case "+=":
            case "-=":
            case "*=":
            case "/=":
            case "%=":
            case "&=":
            case "|=":
            case "^=":
            case "<<=":
                EatToken();
                return token.Text;
            default:
                return null;
        }
    }

    /// <summary>
    /// (implicit | explicit) [interface '.'] operator [checked] Type '(' parameters ')' body
    /// </summary>
    private MemberDeclaration ParseConversionOperatorDeclaration(List<AttributeSection> attributes, Modifiers modifiers, List<Modifiers> modifierList)
    {
        var isImplicit = EatToken().Text == "implicit";
        var explicitInterface = ParseExplicitInterfaceSpecifier();
        if (!TryEatKeyword("operator"))
        {
            return null;
        }

        var isChecked = TryEatKeyword("checked");
        var type = ParseType(TypeMode.Normal);
        if (type == null)
        {
            return null;
        }

        var parameters = ParseParameterList(allowImplicitTypes: false);
        if (parameters == null || !TryParseMemberBody(out var body))
        {
            return null;
        }

        return new ConversionOperatorDeclaration(isImplicit, type, parameters, body, attributes, modifiers, isChecked)
        {
            ModifierList = modifierList,
            ExplicitInterface = explicitInterface,
        };
    }
}
