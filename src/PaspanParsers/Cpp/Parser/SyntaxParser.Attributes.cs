using System.Text;

namespace PaspanParsers.Cpp;

// Attributes ([dcl.attr]).
internal ref partial struct SyntaxParser
{
    /// <summary>
    /// The current tokens start an attribute specifier: '[[' is never a subscript or a lambda.
    /// </summary>
    private bool IsAttributeStart => IsPunctuator("[") && Peek(1).IsPunctuator("[");

    /// <summary>
    /// A possibly empty sequence of <c>[[ … ]]</c>; null when one does not parse.
    /// </summary>
    private List<AttributeSpecifier> ParseAttributeSpecifiers()
    {
        var specifiers = new List<AttributeSpecifier>();
        while (IsAttributeStart)
        {
            var specifier = ParseAttributeSpecifier();
            if (specifier == null)
            {
                return null;
            }

            specifiers.Add(specifier);
        }

        return specifiers;
    }

    /// <summary>
    /// <c>[[ using ns: attribute, attribute(arguments)... ]]</c>; empty attributes between commas are allowed.
    /// </summary>
    private AttributeSpecifier ParseAttributeSpecifier()
    {
        var start = NodeStart;
        EatTokens(2);
        string usingNamespace = null;
        if (TryEatKeyword("using"))
        {
            usingNamespace = TryEatIdentifier();
            if (usingNamespace == null || !TryEatPunctuator(":"))
            {
                return null;
            }
        }

        var attributes = new List<CppAttribute>();
        while (true)
        {
            if (IsPunctuator("]") && Peek(1).IsPunctuator("]"))
            {
                EatTokens(2);
                return Finish(new AttributeSpecifier(attributes) { UsingNamespace = usingNamespace }, start);
            }

            if (TryEatPunctuator(","))
            {
                continue;
            }

            var attribute = ParseAttribute();
            if (attribute == null)
            {
                return null;
            }

            attributes.Add(attribute);
            if (!IsPunctuator(",") && !IsPunctuator("]"))
            {
                return null;
            }
        }
    }

    /// <summary>
    /// <c>name</c>, <c>ns::name</c>, <c>name(balanced tokens)</c>, optionally followed by '...'. Keywords are
    /// names here: <c>gnu::const</c>. The arguments are kept as written.
    /// </summary>
    private CppAttribute ParseAttribute()
    {
        var start = NodeStart;
        var name = TryEatAttributeName();
        if (name == null)
        {
            return null;
        }

        string @namespace = null;
        if (TryEatPunctuator("::"))
        {
            @namespace = name;
            name = TryEatAttributeName();
            if (name == null)
            {
                return null;
            }
        }

        string arguments = null;
        if (IsPunctuator("("))
        {
            EatToken();
            var depth = 1;
            var argumentsStart = Current.Start;
            var argumentsEnd = argumentsStart;
            while (true)
            {
                var token = Current;
                if (token.Kind == TokenKind.EndOfFile)
                {
                    return null;
                }

                if (token.Kind == TokenKind.Punctuator)
                {
                    if (token.Text is "(" or "[" or "{")
                    {
                        depth++;
                    }
                    else if (token.Text is ")" or "]" or "}" && --depth == 0)
                    {
                        if (token.Text != ")")
                        {
                            return null;
                        }

                        EatToken();
                        break;
                    }
                }

                argumentsEnd = EatToken().End;
            }

            arguments = argumentsEnd > argumentsStart ? Encoding.UTF8.GetString(_source[argumentsStart..argumentsEnd]) : "";
        }

        var isPackExpansion = TryEatPunctuator("...");
        return Finish(new CppAttribute(@namespace, name, arguments) { IsPackExpansion = isPackExpansion }, start);
    }

    private string TryEatAttributeName()
    {
        var token = Current;
        if (token.Kind is not (TokenKind.Identifier or TokenKind.Keyword))
        {
            return null;
        }

        EatToken();
        return token.Text;
    }
}
