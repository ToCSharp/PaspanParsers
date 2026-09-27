namespace PaspanParsers.Cpp;

// Template declarations ([temp.pre]), template parameters ([temp.param]), explicit instantiations and
// specializations, and concepts ([temp.concept]).
internal ref partial struct SyntaxParser
{
    /// <summary>
    /// <c>template &lt;parameters&gt; requires constraint declaration</c>, an explicit specialization
    /// <c>template &lt;&gt; declaration</c>, or an explicit instantiation <c>template declaration</c>. The
    /// parameters are declared in a scope of their own; the declared entity is a template.
    /// </summary>
    private Declaration ParseTemplateDeclaration(DeclarationContext context)
    {
        var start = NodeStart;
        if (!Peek(1).IsPunctuator("<"))
        {
            return ParseExplicitInstantiation(context);
        }

        EatToken();
        var symbols = _cache.Symbols;
        symbols.EnterScope(ScopeKind.TemplateParameters);
        var parameters = ParseTemplateParameterList();
        if (parameters == null)
        {
            return null;
        }

        var requiresClause = ParseOptionalRequiresClause(out var valid);
        if (!valid)
        {
            return null;
        }

        var declaration = IsKeyword("concept") ? ParseConceptDefinition() : ParseDeclaration(context);
        if (declaration == null)
        {
            return null;
        }

        symbols.ExitScope();
        return Finish(new TemplateDeclaration(parameters, declaration) { RequiresClause = requiresClause }, start);
    }

    /// <summary>
    /// <c>template declaration</c> or <c>extern template declaration</c>.
    /// </summary>
    private ExplicitInstantiation ParseExplicitInstantiation(DeclarationContext context)
    {
        var start = NodeStart;
        var isExtern = TryEatKeyword("extern");
        EatToken();
        var declaration = ParseDeclaration(context);
        return declaration == null ? null : Finish(new ExplicitInstantiation(declaration) { IsExtern = isExtern }, start);
    }

    /// <summary>
    /// <c>concept Name = constraint;</c> after the template parameters. A name followed by '&lt;' in the
    /// constraint is a template-id, as in a requires-clause.
    /// </summary>
    private ConceptDefinition ParseConceptDefinition()
    {
        var start = NodeStart;
        EatToken();
        var name = TryEatIdentifier();
        if (name == null || !TryEatPunctuator("="))
        {
            return null;
        }

        // The concept is known in its own definition: concept C = requires { requires C<int>; } is invalid anyway
        _cache.Symbols.Declare(name, SymbolKind.Concept);
        var saved = _inConstraint;
        _inConstraint = true;
        var constraint = ParseConditionalExpression();
        _inConstraint = saved;
        return constraint != null && TryEatPunctuator(";") ? Finish(new ConceptDefinition(name, constraint), start) : null;
    }

    /// <summary>
    /// <c>&lt; template parameters &gt;</c>. The names of the parameters are declared in the current scope:
    /// type parameters as types, template template parameters as templates, others as values; packs as packs.
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

        if (DeclaredName(parameter.Declarator)?.Name is IdentifierName name)
        {
            _cache.Symbols.DeclareTemplateParameter(name.Identifier, SymbolKind.Value, isPack: IsPackDeclarator(parameter.Declarator));
        }

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
            _cache.Symbols.DeclareTemplateParameter(identifier, SymbolKind.Type, isPack);
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
            symbols.DeclareTemplateParameter(identifier, SymbolKind.Template, isPack);
        }

        return Finish(new TemplateTemplateParameter(parameters, key, identifier) { IsPack = isPack, Default = @default }, start);
    }
}
