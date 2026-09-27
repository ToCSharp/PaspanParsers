namespace PaspanParsers.CSharp;

// Stage 4: patterns.
internal ref partial struct SyntaxParser
{
    /// <summary>
    /// pattern: disjunction of conjunctions of (negated) primary patterns; 'not' binds tighter than 'and',
    /// 'and' tighter than 'or', both combinators are left-associative.
    /// </summary>
    /// <param name="isAfterIs">After <c>is</c> a plain name is a type pattern; elsewhere it is a constant.</param>
    public Pattern ParsePattern(bool isAfterIs = false)
    {
        if (!HasSufficientStack())
        {
            return null;
        }

        var left = ParseConjunctivePattern(isAfterIs);
        while (left != null && IsContextual("or") && CanStartPattern(Peek(1)))
        {
            EatToken();
            var right = ParseConjunctivePattern(isAfterIs);
            left = right == null ? null : new LogicalPattern(LogicalPatternKind.Or, left, right);
        }

        return left;
    }

    private Pattern ParseConjunctivePattern(bool isAfterIs)
    {
        var left = ParseNegatedPattern(isAfterIs);
        while (left != null && IsContextual("and") && CanStartPattern(Peek(1)))
        {
            EatToken();
            var right = ParseNegatedPattern(isAfterIs);
            left = right == null ? null : new LogicalPattern(LogicalPatternKind.And, left, right);
        }

        return left;
    }

    private Pattern ParseNegatedPattern(bool isAfterIs)
    {
        if (IsContextual("not") && CanStartPattern(Peek(1)))
        {
            EatToken();
            var operand = ParseNegatedPattern(isAfterIs);
            return operand == null ? null : new LogicalPattern(LogicalPatternKind.Not, operand);
        }

        return ParsePrimaryPattern(isAfterIs);
    }

    private bool CanStartPattern(SyntaxToken token)
    {
        if (token.Kind == TokenKind.Punctuator)
        {
            return token.Text is "(" or "[" or "{" or "<" or "<=" or ">" or ">=" or "-" or "+" or "!" or "~" or "..";
        }

        if (token.IsContextual("when") || IsQueryKeyword(token))
        {
            return false;
        }

        return CanStartExpression(token);
    }

    private Pattern ParsePrimaryPattern(bool isAfterIs)
    {
        var token = Current;

        if (token.Kind == TokenKind.Punctuator)
        {
            switch (token.Text)
            {
                case "(":
                    if (IsCastAhead())
                    {
                        // A constant with a cast: case (byte)'a':
                        break;
                    }

                    return ParsePositionalOrParenthesizedPattern(null);

                case "[":
                    return ParseListPattern();

                case "{":
                    return ParsePropertyPatternRest(null, null);

                case "..":
                {
                    EatToken();
                    if (!CanStartPattern(Current))
                    {
                        return new SlicePattern();
                    }

                    var sliced = ParsePattern();
                    return sliced == null ? null : new SlicePattern(sliced);
                }

                case "<":
                case "<=":
                case ">":
                case ">=":
                {
                    EatToken();
                    var value = ParseSubExpression(Precedence.Shift);
                    if (value == null)
                    {
                        return null;
                    }

                    var op = token.Text switch
                    {
                        "<" => RelationalOperator.LessThan,
                        "<=" => RelationalOperator.LessThanOrEqual,
                        ">" => RelationalOperator.GreaterThan,
                        _ => RelationalOperator.GreaterThanOrEqual,
                    };

                    return new RelationalPattern(op, value);
                }
            }
        }

        if (token.IsContextual("var"))
        {
            var next = Peek(1);
            if (next.IsPunctuator("(") || (next.IsIdentifier && IsDesignation(next, Peek(2))))
            {
                EatToken();
                VariableDesignation designation = IsPunctuator("(") ? ParseParenthesizedDesignation() : ParseSingleDesignation();
                return designation == null ? null : new VarPattern(designation);
            }
        }

        if (token.IsContextual("_") && !IsNameContinuation(Peek(1)))
        {
            EatToken();
            return new DiscardPattern();
        }

        if (token.IsIdentifier || IsPredefinedTypeKeyword(token))
        {
            var start = _position;
            var type = ParseType(TypeMode.Expression);
            if (type != null)
            {
                if (IsPunctuator("("))
                {
                    return ParsePositionalOrParenthesizedPattern(type);
                }

                if (IsPunctuator("{"))
                {
                    return ParsePropertyPatternRest(type, null);
                }

                if (Current.IsIdentifier && IsDesignation(Current, Peek(1)))
                {
                    return new DeclarationPattern(type, EatToken().Text);
                }

                if (isAfterIs || !IsSimpleName(type))
                {
                    return new TypePattern(type);
                }
            }

            _position = start;
        }

        var expression = ParseSubExpression(Precedence.Shift);
        return expression == null ? null : new ConstantPattern(expression);
    }

    /// <summary>
    /// '(' Type ')' followed by the operand of a cast, rather than a parenthesized pattern
    /// followed by a combinator.
    /// </summary>
    private bool IsCastAhead()
    {
        var start = _position;
        EatToken();
        var type = ParseType(TypeMode.Normal);
        var isCast = type != null && IsPunctuator(")");
        if (isCast)
        {
            var next = Peek(1);
            isCast = IsCastFollow(type, next) && !next.IsContextual("and") && !next.IsContextual("or") && !next.IsContextual("when");
        }

        _position = start;
        return isCast;
    }

    /// <summary>
    /// Tokens after '_' that make it part of a name rather than a discard.
    /// </summary>
    private static bool IsNameContinuation(SyntaxToken token) =>
        (token.IsIdentifier && !token.IsContextual("when") && !token.IsContextual("and") && !token.IsContextual("or"))
        || token.IsPunctuator(".") || token.IsPunctuator("<") || token.IsPunctuator("::")
        || token.IsPunctuator("(") || token.IsPunctuator("{") || token.IsPunctuator("[");

    /// <summary>
    /// Whether the identifier <paramref name="token"/> names a variable after a pattern, rather than
    /// being a pattern combinator ('and', 'or', 'when') or a query keyword.
    /// </summary>
    private bool IsDesignation(SyntaxToken token, SyntaxToken next)
    {
        if (!token.IsIdentifier || IsQueryKeyword(token))
        {
            return false;
        }

        if (token.IsContextual("and") || token.IsContextual("or"))
        {
            return !CanStartPattern(next);
        }

        if (token.IsContextual("when"))
        {
            return !CanStartExpression(next);
        }

        return true;
    }

    private string TryParsePatternDesignation()
    {
        if (Current.IsIdentifier && IsDesignation(Current, Peek(1)))
        {
            return EatToken().Text;
        }

        return null;
    }

    /// <summary>
    /// [Type] '(' [subpattern (',' subpattern)*] ')' [property clause] [designation], or '(' pattern ')'.
    /// </summary>
    private Pattern ParsePositionalOrParenthesizedPattern(TypeReference type)
    {
        EatToken();

        var subpatterns = new List<SubPattern>();
        if (!IsPunctuator(")"))
        {
            while (true)
            {
                string name = null;
                if (Current.IsIdentifier && Peek(1).IsPunctuator(":"))
                {
                    name = EatToken().Text;
                    EatToken();
                }

                var pattern = ParsePattern();
                if (pattern == null)
                {
                    return null;
                }

                subpatterns.Add(new SubPattern(pattern, name));

                if (!TryEatPunctuator(","))
                {
                    break;
                }
            }
        }

        if (!TryEatPunctuator(")"))
        {
            return null;
        }

        var hasDesignation = Current.IsIdentifier && IsDesignation(Current, Peek(1));
        if (type == null && subpatterns is [{ Name: null } single] && !IsPunctuator("{") && !hasDesignation)
        {
            return new ParenthesizedPattern(single.Pattern);
        }

        return ParsePropertyPatternRest(type, subpatterns);
    }

    /// <summary>
    /// [property clause] [designation] after the type and positional part of a recursive pattern.
    /// </summary>
    private Pattern ParsePropertyPatternRest(TypeReference type, List<SubPattern> positional)
    {
        List<PropertySubPattern> properties = null;
        var hasTrailingComma = false;
        if (IsPunctuator("{"))
        {
            properties = ParsePropertyClause(out hasTrailingComma);
            if (properties == null)
            {
                return null;
            }
        }

        var designation = TryParsePatternDesignation();
        return new RecursivePattern(type, positional, properties, designation, hasTrailingComma);
    }

    /// <summary>
    /// '{' [[name ('.' name)* ':'] pattern (',' ...)* [',']] '}'
    /// </summary>
    private List<PropertySubPattern> ParsePropertyClause(out bool hasTrailingComma)
    {
        EatToken();
        hasTrailingComma = false;

        var properties = new List<PropertySubPattern>();
        while (!IsPunctuator("}"))
        {
            string name = null;
            if (Current.IsIdentifier)
            {
                // An extended property pattern: A.B.C:
                var offset = 1;
                while (Peek(offset).IsPunctuator(".") && Peek(offset + 1).IsIdentifier)
                {
                    offset += 2;
                }

                if (Peek(offset).IsPunctuator(":"))
                {
                    var parts = new List<string>();
                    for (var i = 0; i < offset; i++)
                    {
                        var token = EatToken();
                        if (token.IsIdentifier)
                        {
                            parts.Add(token.Text);
                        }
                    }

                    EatToken();
                    name = string.Join(".", parts);
                }
            }

            var pattern = ParsePattern();
            if (pattern == null)
            {
                return null;
            }

            properties.Add(new PropertySubPattern(name, pattern));

            if (!TryEatPunctuator(","))
            {
                break;
            }

            hasTrailingComma = IsPunctuator("}");
        }

        return TryEatPunctuator("}") ? properties : null;
    }

    /// <summary>
    /// '[' [pattern (',' pattern)* [',']] ']' [designation], where '..' [pattern] is a slice.
    /// </summary>
    private Pattern ParseListPattern()
    {
        EatToken();

        var patterns = new List<Pattern>();
        var hasTrailingComma = false;
        while (!IsPunctuator("]"))
        {
            var pattern = ParsePattern();
            if (pattern == null)
            {
                return null;
            }

            patterns.Add(pattern);

            if (!TryEatPunctuator(","))
            {
                break;
            }

            hasTrailingComma = IsPunctuator("]");
        }

        if (!TryEatPunctuator("]"))
        {
            return null;
        }

        return new ListPattern(patterns, TryParsePatternDesignation(), hasTrailingComma);
    }
}
