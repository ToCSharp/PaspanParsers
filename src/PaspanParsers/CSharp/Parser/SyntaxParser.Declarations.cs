namespace PaspanParsers.CSharp;

// Parts shared by declarations, local functions and lambdas: attributes, parameters,
// type parameters and constraints.
internal ref partial struct SyntaxParser
{
    // ========================================
    // Attributes
    // ========================================

    /// <summary>
    /// Zero or more attribute sections. A '[' that does not start a valid section is left unconsumed.
    /// </summary>
    public List<AttributeSection> ParseAttributeSections()
    {
        var sections = new List<AttributeSection>();
        while (IsPunctuator("["))
        {
            var start = _position;
            var section = ParseAttributeSection();
            if (section == null)
            {
                _position = start;
                break;
            }

            sections.Add(section);
        }

        return sections;
    }

    /// <summary>
    /// '[' [target ':'] attribute (',' attribute)* [','] ']'
    /// </summary>
    private AttributeSection ParseAttributeSection()
    {
        EatToken();

        AttributeTarget? target = null;
        var token = Current;
        if ((token.IsIdentifier || token.Kind == TokenKind.Keyword) && Peek(1).IsPunctuator(":"))
        {
            target = token.Text switch
            {
                "assembly" => AttributeTarget.Assembly,
                "module" => AttributeTarget.Module,
                "field" => AttributeTarget.Field,
                "event" => AttributeTarget.Event,
                "method" => AttributeTarget.Method,
                "param" => AttributeTarget.Param,
                "property" => AttributeTarget.Property,
                "return" => AttributeTarget.Return,
                "type" => AttributeTarget.Type,
                "typevar" => AttributeTarget.TypeVar,
                _ => null,
            };

            if (target == null)
            {
                return null;
            }

            EatToken();
            EatToken();
        }

        var attributes = ParseAttributeList();
        if (attributes == null || attributes.Count == 0 || !TryEatPunctuator("]"))
        {
            return null;
        }

        return new AttributeSection(attributes, target);
    }

    /// <summary>
    /// attribute (',' attribute)* [','], stopping before ']'.
    /// </summary>
    public List<AttributeNode> ParseAttributeList()
    {
        var attributes = new List<AttributeNode>();
        while (Current.IsIdentifier)
        {
            var attribute = ParseAttribute();
            if (attribute == null)
            {
                return null;
            }

            attributes.Add(attribute);

            if (!TryEatPunctuator(","))
            {
                break;
            }
        }

        return attributes;
    }

    /// <summary>
    /// Name ['(' arguments ')'] where arguments may be positional, 'name: value' or 'Name = value'.
    /// </summary>
    private AttributeNode ParseAttribute()
    {
        string alias = null;
        if (Peek(1).IsPunctuator("::"))
        {
            alias = EatToken().Text;
            EatToken();
        }

        var parts = new List<string>();
        List<TypeReference> typeArguments = null;
        while (true)
        {
            var part = TryEatIdentifier();
            if (part == null)
            {
                return null;
            }

            parts.Add(part);

            if (IsPunctuator("<"))
            {
                typeArguments = ParseTypeArgumentList();
                if (typeArguments == null)
                {
                    return null;
                }

                break;
            }

            if (!TryEatPunctuator("."))
            {
                break;
            }
        }

        var name = new NameExpression(parts, typeArguments, alias);

        if (!IsPunctuator("("))
        {
            return new AttributeNode(name);
        }

        EatToken();
        var arguments = new List<Argument>();
        if (!TryEatPunctuator(")"))
        {
            while (true)
            {
                Argument argument;
                if (Current.IsIdentifier && Peek(1).IsPunctuator("="))
                {
                    var argumentName = EatToken().Text;
                    EatToken();
                    var value = ParseExpressionInNestedContext();
                    argument = value == null ? null : new Argument(value, argumentName, isNameEquals: true);
                }
                else
                {
                    argument = ParseArgument();
                }

                if (argument == null)
                {
                    return null;
                }

                arguments.Add(argument);

                if (TryEatPunctuator(","))
                {
                    continue;
                }

                if (!TryEatPunctuator(")"))
                {
                    return null;
                }

                break;
            }
        }

        return new AttributeNode(name, arguments);
    }

    // ========================================
    // Parameters
    // ========================================

    /// <summary>
    /// '(' [parameter (',' parameter)*] ')'
    /// </summary>
    private List<Parameter> ParseParameterList(bool allowImplicitTypes)
    {
        if (!TryEatPunctuator("("))
        {
            return null;
        }

        var parameters = ParseParameters(")", allowImplicitTypes);
        return parameters != null && TryEatPunctuator(")") ? parameters : null;
    }

    /// <summary>
    /// Parameters separated by commas, stopping before <paramref name="close"/>.
    /// </summary>
    public List<Parameter> ParseParameters(string close, bool allowImplicitTypes = false)
    {
        var parameters = new List<Parameter>();
        if (IsPunctuator(close))
        {
            return parameters;
        }

        while (true)
        {
            var parameter = ParseParameter(allowImplicitTypes);
            if (parameter == null)
            {
                return null;
            }

            parameters.Add(parameter);

            if (!TryEatPunctuator(","))
            {
                return parameters;
            }
        }
    }

    /// <summary>
    /// [attributes] modifier* Type identifier ['=' default], or modifier* identifier for implicitly typed lambda parameters.
    /// </summary>
    private Parameter ParseParameter(bool allowImplicitTypes)
    {
        var attributes = ParseAttributeSections();

        var modifiers = new List<ParameterModifier>();
        while (true)
        {
            var token = Current;
            ParameterModifier? modifier = token.Kind switch
            {
                TokenKind.Keyword => token.Text switch
                {
                    "this" => ParameterModifier.This,
                    "ref" => ParameterModifier.Ref,
                    "out" => ParameterModifier.Out,
                    "in" => ParameterModifier.In,
                    "params" => ParameterModifier.Params,
                    "readonly" => ParameterModifier.Readonly,
                    _ => null,
                },
                TokenKind.Identifier when token.IsContextual("scoped") && IsScopedModifier() => ParameterModifier.Scoped,
                _ => null,
            };

            if (!modifier.HasValue)
            {
                break;
            }

            EatToken();
            modifiers.Add(modifier.Value);
        }

        var primary = modifiers.FirstOrDefault(m => m is not (ParameterModifier.Scoped or ParameterModifier.Readonly));
        var attributeList = attributes.Count != 0 ? attributes : null;

        if (IsKeyword("__arglist"))
        {
            EatToken();
            return new Parameter(null, "__arglist", primary, null, attributeList, modifiers);
        }

        if (allowImplicitTypes && Current.IsIdentifier && (Peek(1).IsPunctuator(",") || Peek(1).IsPunctuator(")")))
        {
            return new Parameter(null, EatToken().Text, primary, null, attributeList, modifiers);
        }

        var type = ParseType(TypeMode.Normal);
        if (type == null)
        {
            return null;
        }

        var name = TryEatIdentifier();
        if (name == null)
        {
            return null;
        }

        Expression defaultValue = null;
        if (TryEatPunctuator("="))
        {
            defaultValue = ParseExpressionInNestedContext();
            if (defaultValue == null)
            {
                return null;
            }
        }

        return new Parameter(type, name, primary, defaultValue, attributeList, modifiers);
    }

    /// <summary>
    /// 'scoped' is a parameter modifier when a modifier or a type and a parameter name follow it.
    /// </summary>
    private bool IsScopedModifier()
    {
        var next = Peek(1);
        if (next.IsKeyword("ref") || next.IsKeyword("in") || next.IsKeyword("out"))
        {
            return true;
        }

        var start = _position;
        EatToken();
        var type = ParseType(TypeMode.Normal);
        var isModifier = type != null && Current.IsIdentifier;
        _position = start;
        return isModifier;
    }

    // ========================================
    // Type parameters and constraints
    // ========================================

    /// <summary>
    /// '&lt;' [attributes] [in | out] identifier (',' ...)* '&gt;'
    /// </summary>
    public List<TypeParameter> ParseTypeParameterList()
    {
        if (!TryEatPunctuator("<"))
        {
            return null;
        }

        var parameters = new List<TypeParameter>();
        while (true)
        {
            var attributes = ParseAttributeSections();

            VarianceKind? variance = null;
            if (TryEatKeyword("in"))
            {
                variance = VarianceKind.In;
            }
            else if (TryEatKeyword("out"))
            {
                variance = VarianceKind.Out;
            }

            var name = TryEatIdentifier();
            if (name == null)
            {
                return null;
            }

            parameters.Add(new TypeParameter(name, variance, attributes.Count != 0 ? attributes : null));

            if (TryEatPunctuator(","))
            {
                continue;
            }

            return TryEatPunctuator(">") ? parameters : null;
        }
    }

    /// <summary>
    /// (where identifier ':' constraint (',' constraint)*)*
    /// </summary>
    public List<TypeParameterConstraint> ParseConstraintClauses()
    {
        var clauses = new List<TypeParameterConstraint>();
        while (IsContextual("where") && Peek(1).IsIdentifier && Peek(2).IsPunctuator(":"))
        {
            EatToken();
            var name = EatToken().Text;
            EatToken();

            var constraints = new List<TypeConstraint>();
            while (true)
            {
                var constraint = ParseTypeConstraint();
                if (constraint == null)
                {
                    return null;
                }

                constraints.Add(constraint);

                if (!TryEatPunctuator(","))
                {
                    break;
                }
            }

            clauses.Add(new TypeParameterConstraint(name, constraints));
        }

        return clauses;
    }

    private TypeConstraint ParseTypeConstraint()
    {
        if (TryEatKeyword("class"))
        {
            return new ClassConstraint(TryEatPunctuator("?"));
        }

        if (TryEatKeyword("struct"))
        {
            return new StructConstraint();
        }

        if (IsKeyword("new") && Peek(1).IsPunctuator("(") && Peek(2).IsPunctuator(")"))
        {
            EatToken();
            EatToken();
            EatToken();
            return new ConstructorConstraint();
        }

        if (TryEatKeyword("default"))
        {
            return new DefaultConstraint();
        }

        if (IsContextual("allows") && Peek(1).IsKeyword("ref") && Peek(2).IsKeyword("struct"))
        {
            EatToken();
            EatToken();
            EatToken();
            return new AllowsRefStructConstraint();
        }

        var next = Peek(1);
        var endsConstraint = !next.IsPunctuator(".") && !next.IsPunctuator("<") && !next.IsPunctuator("::") && !next.IsPunctuator("?");
        if (IsContextual("unmanaged") && endsConstraint)
        {
            EatToken();
            return new UnmanagedConstraint();
        }

        if (IsContextual("notnull") && endsConstraint)
        {
            EatToken();
            return new NotNullConstraint();
        }

        var type = ParseType(TypeMode.Normal);
        return type == null ? null : new TypeReferenceConstraint(type);
    }
}
