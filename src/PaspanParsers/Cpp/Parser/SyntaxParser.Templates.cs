namespace PaspanParsers.Cpp;

// Template parameters ([temp.param]).
internal ref partial struct SyntaxParser
{
    /// <summary>
    /// <c>&lt; template parameters &gt;</c>. The names of the parameters are declared in the current scope:
    /// type parameters as types, template template parameters as templates, others as values.
    /// </summary>
    private List<TemplateParameter> ParseTemplateParameterList()
    {
        if (!TryEatPunctuator("<"))
        {
            return null;
        }

        // A '>' in a default argument closes the list, as in template arguments
        var saved = (_inTemplateArguments, _inConstraint);
        _inTemplateArguments = true;
        _inConstraint = false;
        var parameters = ParseTemplateParameterListRest();
        (_inTemplateArguments, _inConstraint) = saved;
        return parameters;
    }

    private List<TemplateParameter> ParseTemplateParameterListRest()
    {
        var parameters = new List<TemplateParameter>();
        if (TryEatTemplateClose())
        {
            return parameters;
        }

        while (true)
        {
            var parameter = ParseTemplateParameter();
            if (parameter == null)
            {
                return null;
            }

            parameters.Add(parameter);
            if (TryEatTemplateClose())
            {
                return parameters;
            }

            if (!TryEatPunctuator(","))
            {
                return null;
            }
        }
    }

    /// <summary>
    /// A type parameter, a template template parameter, a type parameter constrained by a concept, or a
    /// non-type parameter.
    /// </summary>
    private TemplateParameter ParseTemplateParameter()
    {
        EnsureSufficientStack();
        var start = NodeStart;
        if (IsKeyword("template"))
        {
            return ParseTemplateTemplateParameter();
        }

        if ((IsKeyword("typename") || IsKeyword("class")) && IsTypeParameterRest(1))
        {
            var key = EatToken().Text;
            return ParseTypeParameterRest(start, key, null);
        }

        // C T, C<int> T, ns::C... Ts: the name of a concept. A name that is not declared in the file (from a
        // header) is taken as a concept unless it ends with _t, like std::size_t in std::size_t N
        if (IsNameStart(Current, NameContext.Type) && !Current.IsKeyword("decltype"))
        {
            var mark = Save();
            var constraint = ParseName(NameContext.Type);
            if (constraint != null && IsConceptName(constraint) && IsTypeParameterRest(0))
            {
                return ParseTypeParameterRest(start, null, constraint);
            }

            Restore(mark);
        }

        var parameter = ParseParameter();
        if (parameter == null)
        {
            return null;
        }

        DeclareName(parameter.Specifiers, parameter.Declarator);
        return Finish(new NonTypeTemplateParameter(parameter), start);
    }

    /// <summary>
    /// <paramref name="name"/> names a concept: known as one, or not declared in the file and not ending
    /// with <c>_t</c>.
    /// </summary>
    private readonly bool IsConceptName(Name name)
    {
        var last = name is QualifiedName qualified ? qualified.Name : name;
        var identifier = LastIdentifier(last);
        if (identifier == null || last is not (IdentifierName or TemplateIdName))
        {
            return false;
        }

        return _cache.Symbols.Lookup(identifier) switch
        {
            SymbolKind.Concept => true,
            null => !identifier.EndsWith("_t", StringComparison.Ordinal),
            _ => false,
        };
    }

    /// <summary>
    /// The token at <paramref name="offset"/> continues a type parameter: <c>...</c>, a name followed by
    /// ',', '&gt;' or '=', or ',', '&gt;' or '=' for an unnamed parameter. After <c>typename</c>, a qualified
    /// name is the type of a non-type parameter: <c>typename T::type N</c>.
    /// </summary>
    private bool IsTypeParameterRest(int offset)
    {
        var token = Peek(offset);
        if (token.IsIdentifier)
        {
            token = Peek(offset + 1);
        }

        return token.IsPunctuator("...") || token.IsPunctuator(",") || token.IsPunctuator(">") || token.IsPunctuator("=");
    }

    /// <summary>
    /// The rest of a type parameter after its key or constraint: <c>... T = default</c>.
    /// </summary>
    private TypeTemplateParameter ParseTypeParameterRest(int start, string key, Name constraint)
    {
        var isPack = TryEatPunctuator("...");
        var identifier = TryEatIdentifier();
        TypeId @default = null;
        if (TryEatPunctuator("="))
        {
            @default = ParseTypeId();
            if (@default == null)
            {
                return null;
            }
        }

        if (identifier != null)
        {
            _cache.Symbols.Declare(identifier, SymbolKind.Type);
        }

        return Finish(new TypeTemplateParameter(key, identifier) { Constraint = constraint, IsPack = isPack, Default = @default }, start);
    }

    /// <summary>
    /// <c>template &lt;parameters&gt; class TT = default</c>. The parameters are declared in their own scope.
    /// </summary>
    private TemplateTemplateParameter ParseTemplateTemplateParameter()
    {
        var start = NodeStart;
        EatToken();
        var symbols = _cache.Symbols;
        symbols.EnterScope();
        var parameters = ParseTemplateParameterList();
        symbols.ExitScope();
        if (parameters == null || !(IsKeyword("class") || IsKeyword("typename")))
        {
            return null;
        }

        var key = EatToken().Text;
        var isPack = TryEatPunctuator("...");
        var identifier = TryEatIdentifier();
        Name @default = null;
        if (TryEatPunctuator("="))
        {
            @default = ParseName(NameContext.Type);
            if (@default == null)
            {
                return null;
            }
        }

        if (identifier != null)
        {
            symbols.Declare(identifier, SymbolKind.Template);
        }

        return Finish(new TemplateTemplateParameter(parameters, key, identifier) { IsPack = isPack, Default = @default }, start);
    }
}
