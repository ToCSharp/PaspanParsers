namespace PaspanParsers.CSharp;

// Stage 6: the compilation unit, extern aliases and using directives.
internal ref partial struct SyntaxParser
{
    /// <summary>
    /// extern-alias* using* global-attribute* member*, up to the end of the input.
    /// </summary>
    public CompilationUnit ParseCompilationUnit()
    {
        var externs = new List<ExternAliasDirective>();
        var usings = new List<UsingDirective>();
        ParseExternsAndUsings(externs, usings);

        var attributes = new List<AttributeSection>();
        while (IsGlobalAttributeSectionStart())
        {
            var nullableDirectives = Current.NullableDirectives;
            var section = ParseAttributeSection();
            if (section == null)
            {
                return null;
            }

            section.NullableDirectives = nullableDirectives;
            attributes.Add(section);
        }

        var members = new List<MemberDeclaration>();
        while (Current.Kind != TokenKind.EndOfFile)
        {
            var member = ParseMemberDeclaration(MemberContext.CompilationUnit);
            if (member == null)
            {
                return null;
            }

            members.Add(member);
        }

        // The compilation unit spans the whole input, with the trivia around the tokens
        return Finish(
            new CompilationUnit(NullIfEmpty(externs), NullIfEmpty(usings), NullIfEmpty(attributes), NullIfEmpty(members))
            {
                EndNullableDirectives = Current.NullableDirectives,
            },
            0,
            _source.Length);
    }

    /// <summary>
    /// '[' (assembly | module) ':'
    /// </summary>
    private bool IsGlobalAttributeSectionStart()
    {
        var target = Peek(1);
        return IsPunctuator("[") && (target.IsContextual("assembly") || target.IsContextual("module")) && Peek(2).IsPunctuator(":");
    }

    /// <summary>
    /// Extern alias and using directives at the start of a compilation unit or namespace. A 'using'
    /// that is not a directive (a using statement at the top level) ends them.
    /// </summary>
    private void ParseExternsAndUsings(List<ExternAliasDirective> externs, List<UsingDirective> usings)
    {
        while (true)
        {
            var nullableDirectives = Current.NullableDirectives;

            if (IsKeyword("extern") && Peek(1).IsContextual("alias") && Peek(2).IsIdentifier && Peek(3).IsPunctuator(";"))
            {
                var start = EatToken().Start;
                EatToken();
                var identifier = EatToken().Text;
                EatToken();
                externs.Add(Finish(new ExternAliasDirective(identifier) { NullableDirectives = nullableDirectives }, start));
                continue;
            }

            if (IsKeyword("using") || (IsContextual("global") && Peek(1).IsKeyword("using")))
            {
                var start = _position;
                var spanStart = NodeStart;
                var directive = ParseUsingDirective();
                if (directive != null)
                {
                    directive.NullableDirectives = nullableDirectives;
                    usings.Add(Finish(directive, spanStart));
                    continue;
                }

                _position = start;
            }

            return;
        }
    }

    /// <summary>
    /// [global] using [static] [unsafe] (Name | Alias '=' Type | Type) ';'
    /// </summary>
    private UsingDirective ParseUsingDirective()
    {
        var isGlobal = TryEatContextual("global");
        EatToken();
        var isStatic = TryEatKeyword("static");
        var isUnsafe = TryEatKeyword("unsafe");

        if (!isStatic && Current.IsIdentifier && Peek(1).IsPunctuator("="))
        {
            var alias = EatToken().Text;
            EatToken();
            var target = ParseType(TypeMode.Normal);
            if (target == null || !TryEatPunctuator(";"))
            {
                return null;
            }

            var targetName = AsName(target);
            return new UsingAliasDirective(alias, targetName, targetName == null ? target : null) { IsGlobal = isGlobal, IsUnsafe = isUnsafe };
        }

        var type = ParseType(TypeMode.Normal);
        if (type == null || !TryEatPunctuator(";"))
        {
            return null;
        }

        var name = AsName(type);
        if (isStatic)
        {
            return new UsingStaticDirective(name, name == null ? type : null) { IsGlobal = isGlobal, IsUnsafe = isUnsafe };
        }

        return name != null && name.TypeArguments == null
            ? new UsingNamespaceDirective(name) { IsGlobal = isGlobal, IsUnsafe = isUnsafe }
            : null;
    }

    /// <summary>
    /// The type as a <see cref="NameExpression"/> when it is a (possibly alias-qualified) name with
    /// type arguments only on its last part; otherwise null.
    /// </summary>
    private static NameExpression AsName(TypeReference type)
    {
        return type is NamedTypeReference { Qualifier: null, IsNullable: false } named
            ? Finish(new NameExpression(named.Name.Parts, named.TypeArguments, named.Alias) { CloseAngleNullableDirectives = named.CloseAngleNullableDirectives }, named.Span.Start, named.Span.End)
            : null;
    }
}
