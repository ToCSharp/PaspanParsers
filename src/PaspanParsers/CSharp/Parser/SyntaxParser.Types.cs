namespace PaspanParsers.CSharp;

/// <summary>
/// How a '?' after a type is read.
/// </summary>
internal enum TypeMode
{
    /// <summary>'?' makes the type nullable: declarations, type arguments, casts.</summary>
    Normal,

    /// <summary>
    /// After <c>is</c> and <c>as</c> and in patterns: '?' is nullable only when no expression can follow it,
    /// so <c>x is int ? a : b</c> is a conditional expression. Pointer types are not allowed.
    /// </summary>
    Expression,
}

// Stage 2: names and types.
internal ref partial struct SyntaxParser
{
    public static bool TryGetPredefinedType(string keyword, out PredefinedType type)
    {
        switch (keyword)
        {
            case "bool": type = PredefinedType.Bool; return true;
            case "byte": type = PredefinedType.Byte; return true;
            case "sbyte": type = PredefinedType.SByte; return true;
            case "short": type = PredefinedType.Short; return true;
            case "ushort": type = PredefinedType.UShort; return true;
            case "int": type = PredefinedType.Int; return true;
            case "uint": type = PredefinedType.UInt; return true;
            case "long": type = PredefinedType.Long; return true;
            case "ulong": type = PredefinedType.ULong; return true;
            case "char": type = PredefinedType.Char; return true;
            case "float": type = PredefinedType.Float; return true;
            case "double": type = PredefinedType.Double; return true;
            case "decimal": type = PredefinedType.Decimal; return true;
            case "string": type = PredefinedType.String; return true;
            case "object": type = PredefinedType.Object; return true;
            case "void": type = PredefinedType.Void; return true;
            default: type = default; return false;
        }
    }

    private static bool IsPredefinedTypeKeyword(SyntaxToken token) => token.Kind == TokenKind.Keyword && TryGetPredefinedType(token.Text, out _);

    /// <summary>
    /// Parses a type; on failure restores the position and returns null.
    /// </summary>
    private TypeReference ParseType(TypeMode mode, bool allowRanks = true)
    {
        var start = _position;
        var type = ParseUnderlyingType();
        if (type == null)
        {
            _position = start;
            return null;
        }

        type = ParseTypeSuffixes(type, mode, allowRanks);
        if (type == null)
        {
            _position = start;
        }

        return type;
    }

    /// <summary>
    /// A return type or the type of a local: <c>ref</c> and <c>ref readonly</c> are allowed.
    /// </summary>
    private TypeReference ParseReturnType()
    {
        var start = _position;
        if (TryEatKeyword("ref"))
        {
            var isReadOnly = TryEatKeyword("readonly");
            var type = ParseType(TypeMode.Normal);
            if (type == null)
            {
                _position = start;
                return null;
            }

            return new RefTypeReference(type, isReadOnly);
        }

        return ParseType(TypeMode.Normal);
    }

    private TypeReference ParseUnderlyingType()
    {
        var token = Current;
        switch (token.Kind)
        {
            case TokenKind.Keyword:
                if (TryGetPredefinedType(token.Text, out var predefined))
                {
                    EatToken();
                    return new PredefinedTypeReference(predefined);
                }

                if (token.Text == "delegate" && Peek(1).IsPunctuator("*"))
                {
                    return ParseFunctionPointerType();
                }

                return null;

            case TokenKind.Identifier:
                return ParseNamedType();

            case TokenKind.Punctuator when token.Text == "(":
                return ParseTupleType();

            default:
                return null;
        }
    }

    private TypeReference ParseTypeSuffixes(TypeReference type, TypeMode mode, bool allowRanks)
    {
        while (true)
        {
            var token = Current;
            if (token.Kind != TokenKind.Punctuator)
            {
                return type;
            }

            if (token.Text == "?" && !IsNullableType(type) && (mode == TypeMode.Normal || IsNullableQuestionInExpression()))
            {
                EatToken();
                type = MakeNullable(type);
                continue;
            }

            if (token.Text == "*" && mode == TypeMode.Normal)
            {
                EatToken();
                type = new PointerTypeReference(type);
                continue;
            }

            if (token.Text == "[" && allowRanks && IsRankSpecifierAhead())
            {
                var rank = ParseRankSpecifier();
                if (rank == 0)
                {
                    return null;
                }

                type = new ArrayTypeReference(type, rank);
                continue;
            }

            return type;
        }
    }

    /// <summary>
    /// After 'is' or 'as', '?' is nullable when no expression follows it (<c>x as int? ?? 0</c>)
    /// or when an array rank specifier follows it (<c>x is string?[] array</c>).
    /// </summary>
    private bool IsNullableQuestionInExpression()
    {
        var next = Peek(1);
        if (next.IsPunctuator("["))
        {
            var afterBracket = Peek(2);
            return afterBracket.IsPunctuator("]") || afterBracket.IsPunctuator(",");
        }

        return !CanStartExpression(next);
    }

    private static bool IsNullableType(TypeReference type) => type switch
    {
        NamedTypeReference named => named.IsNullable,
        PredefinedTypeReference predefined => predefined.IsNullable,
        NullableTypeReference => true,
        _ => false,
    };

    private static TypeReference MakeNullable(TypeReference type) => type switch
    {
        NamedTypeReference named => new NamedTypeReference(named.Name, named.TypeArguments, true, named.Qualifier, named.Alias),
        PredefinedTypeReference predefined => new PredefinedTypeReference(predefined.Type, true),
        _ => new NullableTypeReference(type),
    };

    /// <summary>
    /// '[' followed by ']' or ',': an array rank specifier without sizes.
    /// </summary>
    private bool IsRankSpecifierAhead()
    {
        if (!IsPunctuator("["))
        {
            return false;
        }

        var next = Peek(1);
        return next.IsPunctuator("]") || next.IsPunctuator(",");
    }

    /// <summary>
    /// Parses '[' ','* ']' and returns the rank.
    /// </summary>
    private int ParseRankSpecifier()
    {
        EatToken();
        var rank = 1;
        while (TryEatPunctuator(","))
        {
            rank++;
        }

        return TryEatPunctuator("]") ? rank : 0;
    }

    /// <summary>
    /// Identifier ('::' Identifier)? TypeArguments? ('.' Identifier TypeArguments?)*
    /// </summary>
    private TypeReference ParseNamedType()
    {
        string alias = null;
        if (Peek(1).IsPunctuator("::"))
        {
            alias = EatToken().Text;
            EatToken();
            if (!Current.IsIdentifier)
            {
                return null;
            }
        }

        TypeReference qualifier = null;
        var parts = new List<string>();
        while (true)
        {
            parts.Add(EatToken().Text);

            List<TypeReference> typeArguments = null;
            if (IsPunctuator("<"))
            {
                var beforeArguments = _position;
                typeArguments = ParseTypeArgumentList();
                if (typeArguments == null)
                {
                    _position = beforeArguments;
                }
            }

            if (IsPunctuator(".") && Peek(1).IsIdentifier)
            {
                if (typeArguments != null)
                {
                    qualifier = new NamedTypeReference(new NameExpression(parts), typeArguments, false, qualifier, alias);
                    alias = null;
                    parts = [];
                }

                EatToken();
                continue;
            }

            return new NamedTypeReference(new NameExpression(parts), typeArguments, false, qualifier, alias);
        }
    }

    /// <summary>
    /// '&lt;' Type (',' Type)* '&gt;', or '&lt;' ','* '&gt;' for unbound generic types inside typeof.
    /// Returns null without restoring the position when the input is not a type argument list.
    /// </summary>
    private List<TypeReference> ParseTypeArgumentList()
    {
        if (!TryEatPunctuator("<"))
        {
            return null;
        }

        var arguments = new List<TypeReference>();

        if (_allowOmittedTypeArguments && (IsPunctuator(">") || IsPunctuator(",")))
        {
            arguments.Add(new OmittedTypeReference());
            while (TryEatPunctuator(","))
            {
                arguments.Add(new OmittedTypeReference());
            }

            return TryEatPunctuator(">") ? arguments : null;
        }

        while (true)
        {
            var type = ParseType(TypeMode.Normal);
            if (type == null)
            {
                return null;
            }

            arguments.Add(type);

            if (TryEatPunctuator(","))
            {
                continue;
            }

            return TryEatPunctuator(">") ? arguments : null;
        }
    }

    /// <summary>
    /// '(' Type Identifier? (',' Type Identifier?)+ ')'
    /// </summary>
    private TypeReference ParseTupleType()
    {
        EatToken();
        var elements = new List<TupleElement>();
        while (true)
        {
            var type = ParseType(TypeMode.Normal);
            if (type == null)
            {
                return null;
            }

            string name = null;
            if (Current.IsIdentifier)
            {
                name = EatToken().Text;
            }

            elements.Add(new TupleElement(type, name));

            if (TryEatPunctuator(","))
            {
                continue;
            }

            if (TryEatPunctuator(")") && elements.Count >= 2)
            {
                return new TupleTypeReference(elements);
            }

            return null;
        }
    }

    /// <summary>
    /// delegate* [managed | unmanaged ['[' Identifier (',' Identifier)* ']']] '&lt;' Parameter (',' Parameter)* '&gt;'
    /// </summary>
    private TypeReference ParseFunctionPointerType()
    {
        EatToken();
        EatToken();

        string callingConvention = null;
        List<string> unmanagedConventions = null;

        if (IsContextual("managed") || IsContextual("unmanaged"))
        {
            callingConvention = EatToken().Text;

            if (callingConvention == "unmanaged" && TryEatPunctuator("["))
            {
                unmanagedConventions = [];
                while (true)
                {
                    var name = TryEatIdentifier();
                    if (name == null)
                    {
                        return null;
                    }

                    unmanagedConventions.Add(name);
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
        }

        if (!TryEatPunctuator("<"))
        {
            return null;
        }

        var parameters = new List<FunctionPointerParameter>();
        while (true)
        {
            var modifiers = new List<ParameterModifier>();
            while (true)
            {
                if (TryEatKeyword("ref"))
                {
                    modifiers.Add(ParameterModifier.Ref);
                }
                else if (TryEatKeyword("in"))
                {
                    modifiers.Add(ParameterModifier.In);
                }
                else if (TryEatKeyword("out"))
                {
                    modifiers.Add(ParameterModifier.Out);
                }
                else if (TryEatKeyword("readonly"))
                {
                    modifiers.Add(ParameterModifier.Readonly);
                }
                else
                {
                    break;
                }
            }

            var type = ParseType(TypeMode.Normal);
            if (type == null)
            {
                return null;
            }

            parameters.Add(new FunctionPointerParameter(type, modifiers.Count != 0 ? modifiers : null));

            if (TryEatPunctuator(","))
            {
                continue;
            }

            if (!TryEatPunctuator(">"))
            {
                return null;
            }

            return new FunctionPointerTypeReference(parameters, callingConvention, unmanagedConventions);
        }
    }

    /// <summary>
    /// A type that cannot be an expression: a cast with it is a cast whatever follows.
    /// </summary>
    private static bool IsDefinitelyType(TypeReference type) => type switch
    {
        NamedTypeReference named => named.TypeArguments != null || named.IsNullable || named.Alias != null || named.Qualifier != null,
        _ => true,
    };

    /// <summary>
    /// True for identifiers and dotted names without type arguments: these read the same as expressions.
    /// </summary>
    private static bool IsSimpleName(TypeReference type) => type is NamedTypeReference { TypeArguments: null, IsNullable: false, Alias: null, Qualifier: null };

    /// <summary>
    /// The token after a type argument list in an expression decides whether '&lt;' opened type
    /// arguments or was a less-than operator (C# specification, grammar ambiguities).
    /// </summary>
    private static bool IsTypeArgumentFollow(SyntaxToken token)
    {
        switch (token.Kind)
        {
            case TokenKind.EndOfFile:
                return true;
            case TokenKind.Punctuator:
                switch (token.Text)
                {
                    case "(":
                    case ")":
                    case "]":
                    case "}":
                    case ":":
                    case ";":
                    case ",":
                    case ".":
                    case "?":
                    case "==":
                    case "!=":
                    case "|":
                    case "^":
                    case "&&":
                    case "||":
                    case "&":
                    case "[":
                    case "=>":
                        return true;
                }

                return false;
            default:
                return false;
        }
    }
}
