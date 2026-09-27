namespace PaspanParsers.Cpp;

/// <summary>
/// Where a name is parsed, which decides whether a '&lt;' after an identifier starts template arguments.
/// </summary>
internal enum NameContext
{
    /// <summary>A type name: '&lt;' after an identifier always starts template arguments.</summary>
    Type,

    /// <summary>An expression: only a known template takes template arguments.</summary>
    Expression,

    /// <summary>A declarator id: like an expression, and a destructor name may start it.</summary>
    Declarator,

    /// <summary>
    /// A member after '.' or '-&gt;': like a declarator id, but its first component is looked up in the class of
    /// the object, which is not known, so only a template the file or the options know is one.
    /// </summary>
    Member,
}

// Names ([basic.lookup], [temp.names]): qualified names, template-ids, operator and conversion function
// names, literal operators and destructors.
internal ref partial struct SyntaxParser
{
    /// <summary>
    /// A position to go back to after a speculative parse, with the symbols declared up to it.
    /// </summary>
    private readonly record struct Mark(int Position, int Symbols, bool InTemplateArguments, bool InConstraint, int DeferredBodies);

    private readonly Mark Save() => new(_position, _cache.Symbols.Checkpoint(), _inTemplateArguments, _inConstraint, _deferredBodies?.Bodies.Count ?? 0);

    private void Restore(Mark mark)
    {
        _position = mark.Position;
        _cache.Symbols.Rollback(mark.Symbols);
        _inTemplateArguments = mark.InTemplateArguments;
        _inConstraint = mark.InConstraint;

        // The member functions of a declaration that is parsed again
        if (_deferredBodies != null && mark.DeferredBodies < _deferredBodies.Bodies.Count)
        {
            _deferredBodies.Bodies.RemoveRange(mark.DeferredBodies, _deferredBodies.Bodies.Count - mark.DeferredBodies);
        }
    }

    /// <summary>
    /// The token can start a name.
    /// </summary>
    private bool IsNameStart(SyntaxToken token, NameContext context)
    {
        return token.IsIdentifier || token.IsKeyword("operator")
            || (token.IsPunctuator("::") && IsNameComponentStart(TokenAt(token.End)))
            || (token.IsKeyword("decltype") && context == NameContext.Type)
            || (token.IsPunctuator("~") && context is NameContext.Declarator or NameContext.Member);
    }

    /// <summary>
    /// The token can start a component of a name after '::'.
    /// </summary>
    private static bool IsNameComponentStart(SyntaxToken token)
    {
        return token.IsIdentifier || token.IsKeyword("operator") || token.IsKeyword("template") || token.IsPunctuator("~");
    }

    /// <summary>
    /// A name: <c>a</c>, <c>::a::b&lt;int&gt;::c</c>, <c>operator+</c>, <c>S::~S</c>. The '::' of a
    /// pointer to member (<c>S::*</c>) is left for the caller. Returns null when there is no name.
    /// </summary>
    private Name ParseName(NameContext context)
    {
        EnsureSufficientStack();
        var start = NodeStart;
        var global = false;
        if (IsPunctuator("::") && IsNameComponentStart(Peek(1)))
        {
            EatToken();
            global = true;
        }

        Name qualifier = null;
        while (true)
        {
            var qualified = global || qualifier != null;
            var isTemplate = qualified && TryEatKeyword("template");
            var component = ParseNameComponent(context, qualified, isTemplate, qualifier, global);
            if (component == null)
            {
                return null;
            }

            var canQualify = component is IdentifierName or TemplateIdName or DecltypeName;
            if (canQualify && IsPunctuator("::") && IsNameComponentStart(Peek(1)))
            {
                qualifier = qualified ? Finish(new QualifiedName(qualifier, component) { IsTemplate = isTemplate }, start) : component;
                EatToken();
                continue;
            }

            if (!qualified)
            {
                // decltype(x) alone is no name: it is a type specifier
                return component is DecltypeName ? null : component;
            }

            return Finish(new QualifiedName(qualifier, component) { IsTemplate = isTemplate }, start);
        }
    }

    /// <summary>
    /// A component of a name: an identifier or template-id, an operator, conversion or literal operator
    /// function, a destructor, or as the first component, <c>decltype(expression)</c>.
    /// </summary>
    private Name ParseNameComponent(NameContext context, bool qualified, bool isTemplate, Name qualifier = null, bool global = false)
    {
        var start = NodeStart;
        var token = Current;

        if (token.IsIdentifier)
        {
            EatToken();
            var identifier = Finish(new IdentifierName(token.Text), start);
            if (!IsPunctuator("<"))
            {
                return identifier;
            }

            // The component is looked up in the scope of its qualifier: ns::Tmpl<int>. A variable of the file does
            // not hide a member template: path.leaf<Leaf>()
            var kind = _cache.Symbols.LookupComponent(qualifier, global, identifier.Identifier);
            if (context == NameContext.Member && !qualified && kind is not (SymbolKind.Template or SymbolKind.ValueTemplate))
            {
                kind = null;
            }

            if (isTemplate || context == NameContext.Type || kind is SymbolKind.Template or SymbolKind.ValueTemplate or SymbolKind.Concept)
            {
                return ParseTemplateId(identifier, start, commit: isTemplate || context == NameContext.Type);
            }

            // An unknown template: template arguments followed by '::' (N::S<int>::S), in a constraint (C<T>),
            // or in an expression followed by a token that cannot follow a comparison a < b > c
            if (kind != null)
            {
                return identifier;
            }

            var mark = Save();
            var templateId = ParseTemplateId(identifier, start, commit: false);
            // In template arguments, a '>' closes the enclosing ones: bool = is_integral_v<T>>
            if (templateId is TemplateIdName
                && (_inConstraint || IsPunctuator("::") || (context != NameContext.Type && (FollowsTemplateId(Current) || (_inTemplateArguments && IsPunctuator(">"))))))
            {
                return templateId;
            }

            Restore(mark);
            return identifier;
        }

        if (token.IsKeyword("operator"))
        {
            return ParseOperatorName();
        }

        if (token.IsPunctuator("~") && (qualified || context is NameContext.Declarator or NameContext.Member))
        {
            EatToken();
            var typeStart = NodeStart;
            Name type;
            if (IsKeyword("decltype"))
            {
                type = ParseDecltypeName();
            }
            else
            {
                var name = TryEatIdentifier();
                if (name == null)
                {
                    return null;
                }

                type = Finish(new IdentifierName(name), typeStart);
                if (IsPunctuator("<"))
                {
                    type = ParseTemplateId(type, typeStart, commit: false);
                }
            }

            return type == null ? null : Finish(new DestructorName(type), start);
        }

        if (token.IsKeyword("decltype") && !qualified)
        {
            return ParseDecltypeName();
        }

        return null;
    }

    /// <summary>
    /// After <c>a &lt; b &gt;</c> with an unknown <c>a</c>, the token cannot start the right operand of a
    /// comparison, so the '&lt;' and '&gt;' enclose template arguments: <c>get&lt;0&gt;(t)</c>,
    /// <c>std::array&lt;int, 3&gt;{}</c>, <c>f(is_same_v&lt;T, U&gt;)</c>.
    /// </summary>
    private static bool FollowsTemplateId(SyntaxToken token)
    {
        return token.Kind == TokenKind.EndOfFile
            || (token.Kind == TokenKind.Punctuator && token.Text is "(" or ")" or "[" or "]" or "{" or "}" or ";" or "," or ":"
                or "?" or "==" or "!=" or "&&" or "||" or "|" or "^" or "...");
    }

    private DecltypeName ParseDecltypeName()
    {
        var start = NodeStart;
        EatToken();
        var expression = ParseParenthesizedDecltypeOperand();
        return expression == null ? null : Finish(new DecltypeName(expression), start);
    }

    /// <summary>
    /// <c>( expression )</c> after <c>decltype</c>.
    /// </summary>
    private Expression ParseParenthesizedDecltypeOperand()
    {
        if (!TryEatPunctuator("("))
        {
            return null;
        }

        var saved = EnterBrackets();
        var expression = ParseExpression();
        LeaveBrackets(saved);
        return expression != null && TryEatPunctuator(")") ? expression : null;
    }

    // ========================================
    // Template arguments
    // ========================================

    /// <summary>
    /// The template arguments after <paramref name="template"/>. When <paramref name="commit"/> is false and
    /// they do not parse, returns <paramref name="template"/> and leaves the '&lt;' unconsumed: it is then a
    /// less-than operator.
    /// </summary>
    private Name ParseTemplateId(Name template, int start, bool commit)
    {
        var mark = Save();
        var arguments = ParseTemplateArgumentList();
        if (arguments != null)
        {
            return Finish(new TemplateIdName(template, arguments), start);
        }

        if (commit)
        {
            return null;
        }

        Restore(mark);
        return template;
    }

    /// <summary>
    /// <c>&lt; arguments &gt;</c>.
    /// </summary>
    private List<CppNode> ParseTemplateArgumentList()
    {
        // A '<' that starts no template arguments is tried again from other alternatives: remember it, or
        // chains like a < b < c < d take exponential time
        var key = (_position, _cache.Symbols.Checkpoint(), _inTemplateArguments, _inConstraint);
        if (_cache.FailedTemplateArguments.Contains(key))
        {
            return null;
        }

        var arguments = ParseTemplateArgumentListCore();
        if (arguments == null)
        {
            _cache.FailedTemplateArguments.Add(key);
        }

        return arguments;
    }

    private List<CppNode> ParseTemplateArgumentListCore()
    {
        if (!TryEatPunctuator("<"))
        {
            return null;
        }

        var arguments = new List<CppNode>();
        if (TryEatTemplateClose())
        {
            return arguments;
        }

        while (true)
        {
            var argument = ParseTemplateArgument();
            if (argument == null)
            {
                return null;
            }

            arguments.Add(argument);
            if (TryEatTemplateClose())
            {
                return arguments;
            }

            if (!TryEatPunctuator(","))
            {
                return null;
            }
        }
    }

    /// <summary>
    /// The '&gt;' that closes template arguments; in <c>A&lt;B&lt;int&gt;&gt;</c> each '&gt;' closes a list.
    /// </summary>
    private bool TryEatTemplateClose()
    {
        return TryEatPunctuator(">");
    }

    /// <summary>
    /// A template argument: a type-id when it is one ([temp.arg]), otherwise an expression.
    /// </summary>
    private CppNode ParseTemplateArgument()
    {
        var mark = Save();
        var type = ParseTypeId();
        if (type != null && (IsPunctuator(",") || IsPunctuator(">") || IsPunctuator("...")) && !IsValueName(type))
        {
            if (TryEatPunctuator("..."))
            {
                type = Finish(new TypeId(type.Specifiers, type.Declarator) { IsPackExpansion = true }, type);
            }

            return type;
        }

        Restore(mark);
        var saved = _inTemplateArguments;
        _inTemplateArguments = true;
        var expression = ParseAssignmentExpression();
        _inTemplateArguments = saved;
        return expression == null ? null : TryParsePackExpansion(expression);
    }

    /// <summary>
    /// A type-id that is only a name known as a value, which makes it an expression.
    /// </summary>
    private readonly bool IsValueName(TypeId type)
    {
        return type.Declarator == null && type.Specifiers.Specifiers is [NamedTypeSpecifier { IsTypename: false } named]
            && _cache.Symbols.Lookup(named.Name) is SymbolKind.Value or SymbolKind.ValueTemplate;
    }

    // ========================================
    // Operator names
    // ========================================

    /// <summary>
    /// <c>operator</c> followed by an operator, a type (a conversion function) or <c>""</c> and a suffix.
    /// </summary>
    private Name ParseOperatorName()
    {
        var start = NodeStart;
        EatToken();
        var token = Current;

        // Literal operators: operator "" _km, operator ""_km
        if (token.Kind == TokenKind.StringLiteral && token.Text.StartsWith("\"\"", StringComparison.Ordinal))
        {
            EatToken();
            var suffix = token.Text.Length > 2 ? token.Text[2..] : TryEatIdentifier();
            return suffix == null ? null : Finish(new LiteralOperatorName(suffix), start);
        }

        var @operator = ParseOverloadableOperator();
        if (@operator != null)
        {
            Name name = Finish(new OperatorFunctionName(@operator), start);
            if (!IsPunctuator("<"))
            {
                return name;
            }

            // Operator functions are not declared in the symbols: template arguments are recognized as those of an
            // unknown template, operator()<bool>(value), friend bool operator< <>(A, A)
            var mark = Save();
            var templateId = ParseTemplateId(name, start, commit: false);
            if (templateId is TemplateIdName && (FollowsTemplateId(Current) || IsPunctuator("::")))
            {
                return templateId;
            }

            Restore(mark);
            return name;
        }

        // A conversion function: its type has no parentheses or arrays, so it ends before them
        var type = ParseTypeId(DeclaratorKind.Conversion);
        return type == null ? null : Finish(new ConversionFunctionName(type), start);
    }

    /// <summary>
    /// The operator after <c>operator</c>, or null.
    /// </summary>
    private string ParseOverloadableOperator()
    {
        var token = Current;
        if (token.Kind == TokenKind.Keyword)
        {
            switch (token.Text)
            {
                case "new":
                case "delete":
                    EatToken();
                    if (IsPunctuator("[") && Peek(1).IsPunctuator("]"))
                    {
                        EatToken();
                        EatToken();
                        return token.Text + "[]";
                    }

                    return token.Text;
                case "co_await":
                    EatToken();
                    return token.Text;
                default:
                    return null;
            }
        }

        if (token.Kind != TokenKind.Punctuator)
        {
            return null;
        }

        switch (token.Text)
        {
            case "(" when Peek(1).IsPunctuator(")"):
                EatToken();
                EatToken();
                return "()";
            case "[" when Peek(1).IsPunctuator("]"):
                EatToken();
                EatToken();
                return "[]";
            case ">":
            {
                // >, >=, >> and >>= are composed from adjacent '>' and '=' tokens
                var (composed, _, count) = PeekBinaryOperator();
                EatTokens(count);
                return composed;
            }

            case "+" or "-" or "*" or "/" or "%" or "^" or "&" or "|" or "~" or "!" or "=" or "<"
                or "+=" or "-=" or "*=" or "/=" or "%=" or "^=" or "&=" or "|=" or "<<" or "<<=" or "==" or "!="
                or "<=" or "<=>" or "&&" or "||" or "++" or "--" or "," or "->*" or "->":
                EatToken();
                return token.Text;
            default:
                return null;
        }
    }
}
