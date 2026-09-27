using PaspanParsers.Cpp;

namespace PaspanParsers.Tests.Cpp;

/// <summary>How <see cref="CppSpanChecker"/> checks a node.</summary>
public abstract record KindRule;

/// <summary>
/// The node has the span of a clang node of one of the <see cref="Kinds"/>. With
/// <see cref="WithoutSemicolon"/>, the clang node may end before the ';' that ends the node: clang's ranges
/// of expression statements, <c>return</c> and <c>break</c> do not include it, and neither do those of
/// <c>if</c> and loops that end with such a statement.
/// </summary>
public sealed record ExactRule(params string[] Kinds) : KindRule
{
    public bool WithoutSemicolon { get; init; }
}

/// <summary>
/// The node declares a name at the offset <see cref="Name"/> returns (-1 when it declares none): clang has a
/// declaration of one of the <see cref="Kinds"/> located there that ends where the node ends. Clang's
/// declarations start at their declaration specifiers, shared by all declarators of a declaration.
/// </summary>
public sealed record DeclarationRule(Func<ICppNode, int> Name, params string[] Kinds) : KindRule;

/// <summary>
/// A node and its ancestors, from the parent up to the translation unit.
/// </summary>
public sealed record Ancestry(ICppNode Node, Ancestry Parent);

/// <summary>Clang's dump has no ranges for these nodes: they start and end at token boundaries.</summary>
public sealed record TokensRule : KindRule;

/// <summary>
/// The clang node kinds our nodes correspond to. Every node type needs a rule: a new node type fails
/// the oracle until it is mapped here.
/// </summary>
public static class CppKindMap
{
    private static readonly TokensRule Tokens = new();

    private static readonly string[] FunctionKinds =
        ["FunctionDecl", "CXXMethodDecl", "CXXConstructorDecl", "CXXDestructorDecl", "CXXConversionDecl"];

    public static KindRule RuleFor(ICppNode node, Ancestry parent) => node switch
    {
        // Clang has no nodes for directives; raw tokens include those of directive lines
        PreprocessorDirective => Tokens,

        // Clang dumps no expressions of types: array bounds, decltype, template arguments, noexcept
        Expression when IsInType(parent) => Tokens,

        // Declarations
        FunctionDefinition => new ExactRule(FunctionKinds),
        SimpleDeclaration => Tokens,
        DeclSpecifierSequence or DeclSpecifier => Tokens,
        InitDeclarator => new DeclarationRule(n => NameLocation(((InitDeclarator)n).Declarator), ["VarDecl", "TypedefDecl", .. FunctionKinds]),
        Declarator or Name or TypeId or NoexceptSpecifier => Tokens,
        ParameterDeclaration parameter => IsFunctionParameter(parameter, parent) ? new ExactRule("ParmVarDecl") : Tokens,
        Initializer => Tokens,

        // Statements
        CompoundStatement => new ExactRule("CompoundStmt"),
        DeclarationStatement => new ExactRule("DeclStmt"),
        ExpressionStatement { Expression: null } => new ExactRule("NullStmt"),
        ExpressionStatement => Tokens,
        IfStatement => new ExactRule("IfStmt") { WithoutSemicolon = true },
        WhileStatement => new ExactRule("WhileStmt") { WithoutSemicolon = true },
        ReturnStatement => new ExactRule("ReturnStmt") { WithoutSemicolon = true },

        // Expressions
        LiteralExpression when parent.Node is ConcatenatedStringExpression => Tokens,
        ConcatenatedStringExpression => new ExactRule("StringLiteral", "UserDefinedLiteral"),
        LiteralExpression => new ExactRule(
            "IntegerLiteral", "FloatingLiteral", "CharacterLiteral", "StringLiteral", "CXXBoolLiteralExpr",
            "CXXNullPtrLiteralExpr", "UserDefinedLiteral"),
        NameExpression => new ExactRule(
            "DeclRefExpr", "UnresolvedLookupExpr", "DependentScopeDeclRefExpr", "MemberExpr",
            "CXXDependentScopeMemberExpr", "UnresolvedMemberExpr"),
        ParenthesizedExpression => new ExactRule("ParenExpr", "ParenListExpr"),
        UnaryExpression => new ExactRule("UnaryOperator", "CXXOperatorCallExpr"),
        BinaryExpression => new ExactRule("BinaryOperator", "CompoundAssignOperator", "CXXOperatorCallExpr", "CXXRewrittenBinaryOperator"),
        ConditionalExpression => new ExactRule("ConditionalOperator"),
        CallExpression => new ExactRule(
            "CallExpr", "CXXMemberCallExpr", "CXXOperatorCallExpr", "CXXConstructExpr", "CXXTemporaryObjectExpr",
            "CXXFunctionalCastExpr", "CXXUnresolvedConstructExpr"),

        _ => null,
    };

    /// <summary>
    /// An expression is part of a type: the nearest ancestor that is not an expression is a declarator, a
    /// specifier, a type-id or a name.
    /// </summary>
    private static bool IsInType(Ancestry parent)
    {
        for (var ancestor = parent; ancestor != null; ancestor = ancestor.Parent)
        {
            switch (ancestor.Node)
            {
                case Declarator or DeclSpecifier or DeclSpecifierSequence or TypeId or Name or NoexceptSpecifier:
                    return true;
                case Expression:
                    continue;
                default:
                    return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Clang has a <c>ParmVarDecl</c> for a parameter of a declared function: the function declarator is the
    /// first thing applied to the declared name, and the declaration is no typedef, parameter or type-id.
    /// <c>(void)</c> has none, and neither have the parameters of function types (<c>int (*f)(int)</c>).
    /// </summary>
    private static bool IsFunctionParameter(ParameterDeclaration parameter, Ancestry parent)
    {
        if (parent.Node is not FunctionDeclarator function || function.Inner is null || StripParentheses(function.Inner) is not NameDeclarator)
        {
            return false;
        }

        if (function.Parameters is [{ Declarator: null, DefaultValue: null, Specifiers.Specifiers: [KeywordSpecifier { Keyword: "void" }] }])
        {
            return false;
        }

        for (var ancestor = parent.Parent; ancestor != null; ancestor = ancestor.Parent)
        {
            switch (ancestor.Node)
            {
                case Declarator:
                    continue;
                case FunctionDefinition:
                    return true;
                case InitDeclarator:
                    return ancestor.Parent?.Node is not SimpleDeclaration declaration
                        || declaration.Specifiers?.Specifiers.Any(s => s is KeywordSpecifier { Keyword: "typedef" }) != true;
                default:
                    return false;
            }
        }

        return false;
    }

    private static Declarator StripParentheses(Declarator declarator)
    {
        while (declarator is ParenthesizedDeclarator parenthesized)
        {
            declarator = parenthesized.Inner;
        }

        return declarator;
    }

    /// <summary>
    /// Where clang locates the declaration of a declarator: at the unqualified name, at <c>operator</c> or
    /// <c>~</c>. -1 for an abstract declarator.
    /// </summary>
    public static int NameLocation(Declarator declarator)
    {
        var name = DeclaredName(declarator)?.Name;
        while (true)
        {
            switch (name)
            {
                case null:
                    return -1;
                case QualifiedName qualified:
                    name = qualified.Name;
                    continue;
                case TemplateIdName templateId:
                    name = templateId.Template;
                    continue;
                default:
                    return name.Span.Start;
            }
        }
    }

    /// <summary>
    /// The name a declarator declares, or null for an abstract declarator.
    /// </summary>
    public static NameDeclarator DeclaredName(Declarator declarator) => declarator switch
    {
        NameDeclarator name => name,
        PackDeclarator pack => DeclaredName(pack.Inner),
        PointerDeclarator pointer => DeclaredName(pointer.Inner),
        ReferenceDeclarator reference => DeclaredName(reference.Inner),
        MemberPointerDeclarator member => DeclaredName(member.Inner),
        ArrayDeclarator array => DeclaredName(array.Inner),
        FunctionDeclarator function => DeclaredName(function.Inner),
        ParenthesizedDeclarator parenthesized => DeclaredName(parenthesized.Inner),
        _ => null,
    };
}
