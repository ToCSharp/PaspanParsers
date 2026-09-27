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
/// The node declares the name <see cref="Name"/> returns (null when it declares none): clang has a
/// declaration of one of the <see cref="Kinds"/> at that name that ends where the node ends. Clang's
/// declarations start at their declaration specifiers, shared by all declarators of a declaration.
/// </summary>
public sealed record DeclarationRule(Func<ICppNode, NameDeclarator> Name, params string[] Kinds) : KindRule;

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

    public static KindRule RuleFor(ICppNode node) => node switch
    {
        // Declarations
        FunctionDefinition => new ExactRule(FunctionKinds),
        SimpleDeclaration => Tokens,
        DeclSpecifierSequence or DeclSpecifier => Tokens,
        InitDeclarator => new DeclarationRule(n => DeclaredName(((InitDeclarator)n).Declarator), ["VarDecl", .. FunctionKinds]),
        Declarator => Tokens,
        ParameterDeclaration => new ExactRule("ParmVarDecl"),
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
    /// The name a declarator declares, or null for an abstract declarator.
    /// </summary>
    public static NameDeclarator DeclaredName(Declarator declarator) => declarator switch
    {
        NameDeclarator name => name,
        FunctionDeclarator function => DeclaredName(function.Inner),
        _ => null,
    };
}
