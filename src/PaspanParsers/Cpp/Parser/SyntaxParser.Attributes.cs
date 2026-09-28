using System.Text;

namespace PaspanParsers.Cpp;

// Attributes ([dcl.attr]), alignment specifiers ([dcl.align]) and GNU attributes.
internal ref partial struct SyntaxParser
{
    /// <summary>
    /// The current tokens start a standard attribute specifier: '[[' is never a subscript or a lambda.
    /// </summary>
    private bool IsAttributeStart => IsPunctuator("[") && Peek(1).IsPunctuator("[");

    /// <summary>
    /// The current tokens start an attribute specifier: <c>[[</c>, <c>alignas(</c> or a GNU <c>__attribute__((</c>.
    /// </summary>
    private bool IsAttributeSpecifierStart => IsAttributeStart || IsKeyword("alignas") || IsGnuAttributeStart;

    private bool IsGnuAttributeStart => Current.IsIdentifier && Current.Text is "__attribute__" or "__attribute" && Peek(1).IsPunctuator("(");

    /// <summary>
    /// The token is <c>asm</c>, <c>__asm</c> or <c>__asm__</c>.
    /// </summary>
    private static bool IsAsmKeyword(SyntaxToken token) => token.IsKeyword("asm") || (token.IsIdentifier && token.Text is "__asm__" or "__asm");

    /// <summary>
    /// A possibly empty sequence of <c>[[ … ]]</c>, <c>alignas( … )</c> and GNU <c>__attribute__(( … ))</c>;
    /// null when one does not parse.
    /// </summary>
    private IReadOnlyList<AttributeSpecifier> ParseAttributeSpecifiers()
    {
        List<AttributeSpecifier> specifiers = null;
        while (true)
        {
            AttributeSpecifier specifier;
            if (IsAttributeStart)
            {
                specifier = ParseAttributeSpecifier();
            }
            else if (IsKeyword("alignas"))
            {
                specifier = ParseAlignasSpecifier();
            }
            else if (IsGnuAttributeStart)
            {
                specifier = ParseGnuAttributeSpecifier();
            }
            else
            {
                return specifiers ?? (IReadOnlyList<AttributeSpecifier>)[];
            }

            if (specifier == null)
            {
                return null;
            }

            (specifiers ??= []).Add(specifier);
        }
    }

    /// <summary>
    /// A possibly empty sequence of <c>[[ … ]]</c>: the attributes after a declarator id, which
    /// <c>alignas</c> cannot follow. Null when one does not parse.
    /// </summary>
    private IReadOnlyList<AttributeSpecifier> ParseStandardAttributeSpecifiers()
    {
        List<AttributeSpecifier> specifiers = null;
        while (IsAttributeStart)
        {
            var specifier = ParseAttributeSpecifier();
            if (specifier == null)
            {
                return null;
            }

            (specifiers ??= []).Add(specifier);
        }

        return specifiers ?? (IReadOnlyList<AttributeSpecifier>)[];
    }

    /// <summary>
    /// A possibly empty sequence of GNU attributes, which may follow a declarator. Null when one does not parse.
    /// </summary>
    private IReadOnlyList<AttributeSpecifier> ParseGnuAttributeSpecifiers()
    {
        List<AttributeSpecifier> specifiers = null;
        while (IsGnuAttributeStart)
        {
            var specifier = ParseGnuAttributeSpecifier();
            if (specifier == null)
            {
                return null;
            }

            (specifiers ??= []).Add(specifier);
        }

        return specifiers ?? (IReadOnlyList<AttributeSpecifier>)[];
    }

    /// <summary>
    /// <c>alignas(type-id)</c>, <c>alignas(expression)</c> or <c>alignas(pack...)</c>.
    /// </summary>
    private AlignasSpecifier ParseAlignasSpecifier()
    {
        var start = NodeStart;
        EatToken();
        if (!TryEatPunctuator("("))
        {
            return null;
        }

        var saved = EnterBrackets();
        var mark = Save();
        CppNode operand = ParseTypeId();
        if (operand is not TypeId type || !(IsPunctuator(")") || IsPunctuator("...")) || IsValueName(type))
        {
            Restore(mark);
            operand = ParseAssignmentExpression();
        }

        LeaveBrackets(saved);
        var isPackExpansion = TryEatPunctuator("...");
        if (operand == null || !TryEatPunctuator(")"))
        {
            return null;
        }

        return Finish(new AlignasSpecifier(operand) { IsPackExpansion = isPackExpansion }, start);
    }

    /// <summary>
    /// <c>__attribute__((attributes))</c>: the attributes are kept as written.
    /// </summary>
    private GnuAttributeSpecifier ParseGnuAttributeSpecifier()
    {
        var start = NodeStart;
        var keyword = EatToken().Text;
        if (!TryEatPunctuator("(") || !IsPunctuator("("))
        {
            return null;
        }

        var arguments = ParseParenthesizedText();
        return arguments != null && TryEatPunctuator(")") ? Finish(new GnuAttributeSpecifier(keyword, arguments), start) : null;
    }

    /// <summary>
    /// The source between the current '(' and its matching ')', which are consumed; null when the
    /// brackets inside do not match.
    /// </summary>
    private string ParseParenthesizedText()
    {
        if (!TryEatPunctuator("("))
        {
            return null;
        }

        var depth = 1;
        var textStart = Current.Start;
        var textEnd = textStart;
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

            textEnd = EatToken().End;
        }

        return textEnd > textStart ? Encoding.UTF8.GetString(_source[textStart..textEnd]) : "";
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
            arguments = ParseParenthesizedText();
            if (arguments == null)
            {
                return null;
            }
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
