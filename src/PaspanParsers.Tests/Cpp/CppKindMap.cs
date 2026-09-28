using PaspanParsers.Cpp;

namespace PaspanParsers.Tests.Cpp;

/// <summary>How <see cref="CppSpanChecker"/> checks a node.</summary>
public abstract record KindRule;

/// <summary>
/// The node has the span of a clang node of one of the <see cref="Kinds"/>. With
/// <see cref="WithoutSemicolon"/>, the clang node may end before the ';' that ends the node: clang's ranges
/// of expression statements, <c>return</c> and <c>break</c> do not include it, and neither do those of
/// <c>if</c> and loops that end with such a statement.
/// With <see cref="OrAbsent"/>, a node that no clang node has the span of passes when it starts and ends at
/// token boundaries: clang has no <c>InitListExpr</c> for the braces of a constructor call.
/// <see cref="ClangStart"/>, when set, is where clang may start the node instead: at the unqualified concept of
/// a constrained template parameter, <c>integral T</c> in <c>std::integral T</c>. With
/// <see cref="ClangEndBefore"/>, clang may end the node at the last token that ends at or before that offset:
/// before the attributes after the name of a parameter, <c>int a [[maybe_unused]]</c>. Clang's start and end
/// are tried alone and together.
/// </summary>
public sealed record ExactRule(params string[] Kinds) : KindRule
{
    public bool WithoutSemicolon { get; init; }
    public bool OrAbsent { get; init; }
    public int? ClangStart { get; init; }
    public int? ClangEndBefore { get; init; }
}

/// <summary>
/// The node declares a name at the offset <see cref="Name"/> returns (-1 when it declares none): clang has a
/// declaration of one of the <see cref="Kinds"/> located there that ends where the node ends. Clang's
/// declarations start at their declaration specifiers, shared by all declarators of a declaration.
/// <see cref="OtherEnd"/>, when set, is another end clang may give the declaration: a variable initialized
/// with parentheses ends at its last argument, <c>int a(1</c>, unless a constructor is called. With
/// <see cref="AnyEnd"/>, the end is not checked: clang ends the loop variable of a range-based for at the ':'.
/// </summary>
public sealed record DeclarationRule(Func<ICppNode, int> Name, params string[] Kinds) : KindRule
{
    public Func<ICppNode, int> OtherEnd { get; init; }
    public bool AnyEnd { get; init; }
}

/// <summary>
/// A node and its ancestors, from the parent up to the translation unit.
/// </summary>
public sealed record Ancestry(ICppNode Node, Ancestry Parent);

/// <summary>
/// Clang's dump has no ranges for these nodes, such as types, declarators, specifiers and initializers: they
/// start and end at token boundaries. Like every node, they also hold the tokens that
/// <see cref="CppWriter.WriteNode"/> writes for them (<see cref="CppNodeText"/>).
/// </summary>
public sealed record TokensRule : KindRule;

/// <summary>
/// The clang node kinds our nodes correspond to. Every node type needs a rule: a new node type fails
/// the oracle until it is mapped here.
/// </summary>
public static class CppKindMap
{
    private static readonly TokensRule Tokens = new();

    private static readonly string[] FunctionKinds =
        ["FunctionDecl", "CXXMethodDecl", "CXXConstructorDecl", "CXXDestructorDecl", "CXXConversionDecl", "CXXDeductionGuideDecl"];

    private static readonly string[] ClassKinds =
        ["CXXRecordDecl", "ClassTemplateSpecializationDecl", "ClassTemplatePartialSpecializationDecl"];

    // A template declaration is a template, or the specialization or member of a template it defines, which
    // clang starts at the first 'template'
    private static readonly string[] TemplateKinds =
    [
        "ClassTemplateDecl", "FunctionTemplateDecl", "VarTemplateDecl", "TypeAliasTemplateDecl", "ConceptDecl",
        "ClassTemplateSpecializationDecl", "ClassTemplatePartialSpecializationDecl", "VarTemplateSpecializationDecl",
        "VarTemplatePartialSpecializationDecl", "CXXRecordDecl", "VarDecl", "FriendDecl", "EnumDecl", .. FunctionKinds,
    ];

    public static KindRule RuleFor(ICppNode node, Ancestry parent) => node switch
    {
        // Clang has no nodes for directives; raw tokens include those of directive lines
        PreprocessorDirective => Tokens,

        // Clang dumps no expressions of types (array bounds, decltype, template arguments, noexcept) and
        // of designators
        Expression when IsInType(parent) => Tokens,

        // Declarations: clang starts a function after its standard attributes, and a declaration statement
        // before them; otherwise a specialization or a member of a class template defined outside it at the
        // 'template'. A defaulted or deleted function ends before its ';'.
        FunctionDefinition function => new ExactRule(FunctionKinds)
        {
            WithoutSemicolon = true,
            ClangStart = function.Attributes.Count > 0
                ? function.Specifiers?.Span.Start ?? function.Declarator.Span.Start
                : OutermostTemplate(parent)?.Span.Start,
            ClangEndBefore = OutermostTemplate(parent) != null || function is { IsDefaulted: true, Declarator: var declarator } && DeclaredName(declarator)?.Name is QualifiedName
                ? DeletedTemplateEnd(function)
                : null,
        },
        SimpleDeclaration => Tokens,
        StaticAssertDeclaration => new ExactRule("StaticAssertDecl") { WithoutSemicolon = true },
        EmptyDeclaration when parent.Node is ClassSpecifier => Tokens,
        EmptyDeclaration => new ExactRule("EmptyDecl"),
        AccessSpecifier => new ExactRule("AccessSpecDecl"),
        NamespaceDefinition => new ExactRule("NamespaceDecl"),
        NamespaceAliasDefinition => new ExactRule("NamespaceAliasDecl") { WithoutSemicolon = true },
        UsingDirective => new ExactRule("UsingDirectiveDecl") { WithoutSemicolon = true },
        UsingEnumDeclaration => new ExactRule("UsingEnumDecl") { WithoutSemicolon = true },
        UsingDeclaration => Tokens,
        UsingDeclarator => new DeclarationRule(n => UnqualifiedName(((UsingDeclarator)n).Name).Span.Start,
            "UsingDecl", "UnresolvedUsingValueDecl", "UnresolvedUsingTypenameDecl", "UsingPackDecl")
        {
            OtherEnd = n => ((UsingDeclarator)n).Name.Span.End,
        },
        AliasDeclaration alias => new ExactRule("TypeAliasDecl") { WithoutSemicolon = true, ClangEndBefore = BitIntEnd(alias) },
        LinkageSpecification => new ExactRule("LinkageSpecDecl") { WithoutSemicolon = true },
        // The inner of template <> template <> has no declaration of its own
        TemplateDeclaration { Parameters.Count: 0 } when parent.Node is TemplateDeclaration => new ExactRule(TemplateKinds) { WithoutSemicolon = true, OrAbsent = true },
        TemplateDeclaration { Declaration: AliasDeclaration alias } => new ExactRule(TemplateKinds) { WithoutSemicolon = true, ClangEndBefore = BitIntEnd(alias) },
        TemplateDeclaration template => new ExactRule(TemplateKinds)
        {
            WithoutSemicolon = true,
            ClangEndBefore = DeletedTemplateEnd(InnermostDeclaration(template))
                ?? (InnermostDeclaration(template) is SimpleDeclaration { Declarators: [var only] } ? DecltypeReturnEnd(only) : null),
        },
        ExplicitInstantiation => new ExactRule("ClassTemplateSpecializationDecl", "VarTemplateSpecializationDecl") { WithoutSemicolon = true, OrAbsent = true },
        ConceptDefinition => Tokens,
        AsmDeclaration when parent.Node is DeclarationStatement => Tokens,
        AsmDeclaration => new ExactRule("FileScopeAsmDecl") { WithoutSemicolon = true },
        ModuleDeclaration or ImportDeclaration => Tokens,
        ExportDeclaration => new ExactRule("ExportDecl") { WithoutSemicolon = true },
        ClassSpecifier => new ExactRule(ClassKinds)
        {
            ClangStart = parent is { Node: DeclSpecifierSequence, Parent: { Node: SimpleDeclaration } declaration } ? OutermostTemplate(declaration.Parent)?.Span.Start : null,
        },
        EnumSpecifier => new ExactRule("EnumDecl"),
        Enumerator { Value: null, Attributes: [var first, ..] } => new ExactRule("EnumConstantDecl") { ClangEndBefore = first.Span.Start },
        Enumerator => new ExactRule("EnumConstantDecl"),
        BaseSpecifier or MemberInitializer => Tokens,
        DeclSpecifierSequence or DeclSpecifier => Tokens,

        // Clang has no declarations for the names of an explicit instantiation
        InitDeclarator when IsInExplicitInstantiation(parent) => Tokens,
        InitDeclarator => new DeclarationRule(
            n => NameLocation(((InitDeclarator)n).Declarator),
            ["VarDecl", "DecompositionDecl", "TypedefDecl", "FieldDecl", "VarTemplateSpecializationDecl", "VarTemplatePartialSpecializationDecl", .. FunctionKinds])
        {
            OtherEnd = n => ((InitDeclarator)n) switch
            {
                { Initializer: ParenthesizedInitializer { Arguments: [.., var last] } } => last.Span.End,
                _ when DecltypeReturnEnd((InitDeclarator)n) is { } nameEnd => nameEnd,

                // Clang ends a declaration before its asm label and GNU attributes, and before the attributes of its name
                { AsmLabel: not null, Initializer: null, Declarator: { } declarator } => declarator.Span.End,
                { Initializer: null, BitFieldWidth: null, Declarator: { } declarator } when TrailingNameAttributes(declarator, n.Span.End) is not null
                    => DeclaredName(declarator).Name.Span.End,
                { Attributes.Count: > 0, Initializer: null, BitFieldWidth: null, Declarator: { } declarator } => declarator.Span.End,
                _ => -1,
            },
        },
        ConditionDeclaration => new DeclarationRule(n => NameLocation(((ConditionDeclaration)n).Declarator), "VarDecl", "DecompositionDecl"),
        ForRangeDeclaration => new DeclarationRule(n => NameLocation(((ForRangeDeclaration)n).Declarator), "VarDecl", "DecompositionDecl")
        {
            AnyEnd = true,
        },
        IdentifierName when parent.Node is StructuredBindingDeclarator => new ExactRule("BindingDecl"),
        Declarator or Name or TypeId or NoexceptSpecifier => Tokens,
        ParameterDeclaration when parent.Node is CatchClause => new ExactRule("VarDecl"),
        ParameterDeclaration parameter => IsFunctionParameter(parameter, parent)
            ? new ExactRule("ParmVarDecl")
            {
                ClangStart = parameter.Attributes.Count > 0 ? parameter.Specifiers.Span.Start : null,
                ClangEndBefore = TrailingNameAttributes(parameter.Declarator, parameter.Span.End),
            }
            : Tokens,
        Initializer => Tokens,

        // Templates and attributes: clang's attribute nodes span the attribute without the brackets, and
        // attributes it does not know have none
        // Clang dumps no parameters of the enclosing class templates of a member defined outside them:
        // template <typename T> T Stack<T>::top() { … }
        TemplateParameter when parent.Node is TemplateDeclaration template && DeclaresQualifiedName(template) => Tokens,
        TypeTemplateParameter { Constraint: QualifiedName { Name: var concept } } => new ExactRule("TemplateTypeParmDecl") { ClangStart = concept.Span.Start },

        // Clang ends an unnamed pack at its key: typename...
        TypeTemplateParameter { IsPack: true, Identifier: null, Default: null } parameter => new ExactRule("TemplateTypeParmDecl")
        {
            ClangEndBefore = parameter.Span.End - 1,
        },
        TypeTemplateParameter => new ExactRule("TemplateTypeParmDecl"),
        NonTypeTemplateParameter => new ExactRule("NonTypeTemplateParmDecl"),
        TemplateTemplateParameter => new ExactRule("TemplateTemplateParmDecl"),
        AttributeSpecifier or CppAttribute => Tokens,

        // Statements. Clang has an asm statement for an asm declaration in a block. Clang's ranges of statements that end with ';' do not include it, except for null
        // statements and declarations. Clang drops attributes it does not know, and with them the
        // AttributedStmt; the attributes of a label belong to it, and its LabelStmt starts after them.
        CompoundStatement => new ExactRule("CompoundStmt"),
        DeclarationStatement { Declaration: AsmDeclaration } => new ExactRule("GCCAsmStmt") { WithoutSemicolon = true },

        // Clang 18 gives the alias declaration of an init-statement no declaration statement, or one that ends after the ';'
        DeclarationStatement { Declaration: AliasDeclaration } => new ExactRule("DeclStmt") { OrAbsent = true },
        DeclarationStatement => new ExactRule("DeclStmt"),
        ExpressionStatement { Expression: null } => new ExactRule("NullStmt"),
        ExpressionStatement => Tokens,
        IfStatement => new ExactRule("IfStmt") { WithoutSemicolon = true },
        SwitchStatement => new ExactRule("SwitchStmt") { WithoutSemicolon = true },
        CaseStatement => new ExactRule("CaseStmt") { WithoutSemicolon = true },
        DefaultStatement => new ExactRule("DefaultStmt") { WithoutSemicolon = true },
        LabeledStatement => new ExactRule("LabelStmt") { WithoutSemicolon = true },
        WhileStatement => new ExactRule("WhileStmt") { WithoutSemicolon = true },
        DoStatement => new ExactRule("DoStmt") { WithoutSemicolon = true },
        ForStatement => new ExactRule("ForStmt") { WithoutSemicolon = true },
        RangeForStatement => new ExactRule("CXXForRangeStmt") { WithoutSemicolon = true },
        BreakStatement => new ExactRule("BreakStmt") { WithoutSemicolon = true },
        ContinueStatement => new ExactRule("ContinueStmt") { WithoutSemicolon = true },
        ReturnStatement => new ExactRule("ReturnStmt") { WithoutSemicolon = true },
        CoReturnStatement => new ExactRule("CoreturnStmt") { WithoutSemicolon = true },
        GotoStatement => new ExactRule("GotoStmt") { WithoutSemicolon = true },
        AttributedStatement => new ExactRule("AttributedStmt") { WithoutSemicolon = true, OrAbsent = true },
        TryStatement => new ExactRule("CXXTryStmt"),
        CatchClause => new ExactRule("CXXCatchStmt"),

        // Expressions
        LiteralExpression when parent.Node is ConcatenatedStringExpression => Tokens,
        ConcatenatedStringExpression => new ExactRule("StringLiteral", "UserDefinedLiteral"),
        LiteralExpression => new ExactRule(
            "IntegerLiteral", "FloatingLiteral", "CharacterLiteral", "StringLiteral", "CXXBoolLiteralExpr",
            "CXXNullPtrLiteralExpr", "UserDefinedLiteral"),
        NameExpression when parent.Node is CallExpression call && call.Callee == node && IsSourceLocationBuiltin(call) => Tokens,
        NameExpression => new ExactRule(
            "DeclRefExpr", "UnresolvedLookupExpr", "DependentScopeDeclRefExpr", "MemberExpr",
            "CXXDependentScopeMemberExpr", "UnresolvedMemberExpr", "PredefinedExpr", "ConceptSpecializationExpr"),
        ParenthesizedExpression => new ExactRule("ParenExpr", "ParenListExpr"),
        UnaryExpression => new ExactRule("UnaryOperator", "CXXOperatorCallExpr", "CoawaitExpr", "DependentCoawaitExpr"),
        BinaryExpression => new ExactRule("BinaryOperator", "CompoundAssignOperator", "CXXOperatorCallExpr", "CXXRewrittenBinaryOperator"),
        ConditionalExpression => new ExactRule("ConditionalOperator", "BinaryConditionalOperator"),
        // Clang has a node of its own for the builtins that give the source location of a call: __builtin_LINE()
        CallExpression sourceLocation when IsSourceLocationBuiltin(sourceLocation) => new ExactRule("SourceLocExpr"),
        CallExpression => new ExactRule(
            "CallExpr", "CXXMemberCallExpr", "CXXOperatorCallExpr", "CXXConstructExpr", "CXXTemporaryObjectExpr",
            "CXXFunctionalCastExpr", "CXXUnresolvedConstructExpr", "UserDefinedLiteral"),
        ThisExpression => new ExactRule("CXXThisExpr"),
        // Clang 18 has no member expression for the call of a function with an explicit object parameter
        MemberAccessExpression when parent.Node is CallExpression call && call.Callee == node => new ExactRule(
            "MemberExpr", "CXXDependentScopeMemberExpr", "UnresolvedMemberExpr", "CXXPseudoDestructorExpr") { OrAbsent = true },
        MemberAccessExpression => new ExactRule("MemberExpr", "CXXDependentScopeMemberExpr", "UnresolvedMemberExpr", "CXXPseudoDestructorExpr"),
        SubscriptExpression => new ExactRule("ArraySubscriptExpr", "CXXOperatorCallExpr"),
        CastExpression => new ExactRule("CStyleCastExpr"),
        NamedCastExpression => new ExactRule("CXXStaticCastExpr", "CXXDynamicCastExpr", "CXXConstCastExpr", "CXXReinterpretCastExpr"),
        FunctionalCastExpression => new ExactRule(
            "CXXFunctionalCastExpr", "CXXTemporaryObjectExpr", "CXXConstructExpr", "CXXScalarValueInitExpr", "CXXUnresolvedConstructExpr"),
        SizeOfExpression => new ExactRule("UnaryExprOrTypeTraitExpr"),
        BuiltinCallExpression => new ExactRule(
            "OffsetOfExpr", "BuiltinBitCastExpr", "VAArgExpr", "ConvertVectorExpr", "TypeTraitExpr", "ArrayTypeTraitExpr", "ExpressionTraitExpr"),
        SizeOfPackExpression => new ExactRule("SizeOfPackExpr"),
        NoexceptExpression => new ExactRule("CXXNoexceptExpr"),
        TypeidExpression => new ExactRule("CXXTypeidExpr"),
        NewExpression => new ExactRule("CXXNewExpr"),
        DeleteExpression => new ExactRule("CXXDeleteExpr"),
        ThrowExpression => new ExactRule("CXXThrowExpr"),
        YieldExpression => new ExactRule("CoyieldExpr"),
        // The braces of a list-initialized class are its constructor call
        InitializerListExpression => new ExactRule("InitListExpr", "CXXConstructExpr", "CXXTemporaryObjectExpr") { OrAbsent = true },
        DesignatedInitializerExpression or Designator => Tokens,
        PackExpansionExpression => new ExactRule("PackExpansionExpr"),
        FoldExpression => new ExactRule("CXXFoldExpr"),
        LambdaExpression => new ExactRule("LambdaExpr"),
        LambdaCapture => Tokens,
        RequiresExpression => new ExactRule("RequiresExpr"),
        Requirement => Tokens,

        _ => null,
    };

    /// <summary>
    /// The outermost of the template declarations that <paramref name="parent"/> and its ancestors directly
    /// are, or null when the parent is none.
    /// </summary>
    private static TemplateDeclaration OutermostTemplate(Ancestry parent)
    {
        TemplateDeclaration outermost = null;
        for (var ancestor = parent; ancestor?.Node is TemplateDeclaration template; ancestor = ancestor.Parent)
        {
            outermost = template;
        }

        return outermost;
    }

    /// <summary>
    /// The template declaration declares a member of a class outside it: <c>template &lt;class T&gt; int S&lt;T&gt;::x;</c>,
    /// or holds another template declaration.
    /// </summary>
    private static bool DeclaresQualifiedName(TemplateDeclaration template)
    {
        switch (template.Declaration)
        {
            case TemplateDeclaration:
                return true;
            case FunctionDefinition function:
                return DeclaredName(function.Declarator)?.Name is QualifiedName;
            case SimpleDeclaration simple:
                return simple.Declarators.Any(d => DeclaredName(d.Declarator)?.Name is QualifiedName)
                    || simple.Specifiers?.Specifiers.Any(s => s is ClassSpecifier { Name: QualifiedName } or EnumSpecifier { Name: QualifiedName }) == true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Clang ends a deleted or defaulted function template, and a function defaulted outside its class
    /// (<c>S::~S() = default;</c>), which gets a body, at its declarator, before <c>= delete</c>: the end of the
    /// declarator when <paramref name="declaration"/> is such a function; otherwise null.
    /// </summary>
    private static int? DeletedTemplateEnd(Declaration declaration)
    {
        return declaration is FunctionDefinition { IsDeleted: true } or FunctionDefinition { IsDefaulted: true }
            ? ((FunctionDefinition)declaration).Declarator.Span.End
            : null;
    }

    /// <summary>
    /// Clang 18 ends the declaration of a function whose return type is a trailing <c>decltype(e)</c>, without a
    /// body or initializer, at its name: <c>auto f() -&gt; decltype(x);</c>. The end of the name for such a
    /// declarator; otherwise null.
    /// </summary>
    private static int? DecltypeReturnEnd(InitDeclarator declarator)
    {
        return declarator is { Initializer: null, IsPure: false, Declarator: FunctionDeclarator { TrailingReturnType: { Declarator: null, Specifiers.Specifiers: [DecltypeSpecifier] } } function }
            && DeclaredName(function) is { } name
                ? name.Name.Span.End
                : null;
    }

    private static Declaration InnermostDeclaration(TemplateDeclaration template)
    {
        var declaration = template.Declaration;
        while (declaration is TemplateDeclaration inner)
        {
            declaration = inner.Declaration;
        }

        return declaration;
    }

    /// <summary>
    /// Clang 18 ends the type <c>_BitInt(N)</c> at the keyword, and with it an alias of it: <c>using I = _BitInt(N);</c>.
    /// The end of the keyword when the alias declaration ends with the type; otherwise null.
    /// </summary>
    private static int? BitIntEnd(AliasDeclaration alias)
    {
        return alias.Type is { Declarator: null, Specifiers.Specifiers: [.., BitIntSpecifier bitInt] } ? bitInt.Span.Start + "_BitInt".Length : null;
    }

    private static bool IsSourceLocationBuiltin(CallExpression call)
    {
        return call.Callee is NameExpression { Name: IdentifierName { Identifier: "__builtin_FILE" or "__builtin_FILE_NAME" or "__builtin_LINE"
            or "__builtin_COLUMN" or "__builtin_FUNCTION" or "__builtin_FUNCSIG" or "__builtin_source_location" } };
    }

    private static bool IsInExplicitInstantiation(Ancestry parent)
    {
        for (var ancestor = parent; ancestor != null; ancestor = ancestor.Parent)
        {
            switch (ancestor.Node)
            {
                case ExplicitInstantiation:
                    return true;
                case ClassSpecifier or CompoundStatement:
                    return false;
            }
        }

        return false;
    }

    private static Name UnqualifiedName(Name name) => name is QualifiedName qualified ? qualified.Name : name;

    /// <summary>
    /// The start of the attributes after the declared name, when they end the declaration at
    /// <paramref name="end"/>: <c>int a [[maybe_unused]]</c>; otherwise null.
    /// </summary>
    private static int? TrailingNameAttributes(Declarator declarator, int end)
    {
        return DeclaredName(declarator) is { Attributes: [var first, ..] } name && name.Span.End == end ? first.Span.Start : null;
    }

    /// <summary>
    /// An expression is part of a type, a designator or an attribute: the nearest ancestor that is not an
    /// expression is a declarator, a specifier, a type-id, a name, a designator or an attribute specifier, or
    /// the expression is in the member of <c>__builtin_offsetof</c>.
    /// </summary>
    private static bool IsInType(Ancestry parent)
    {
        for (var ancestor = parent; ancestor != null; ancestor = ancestor.Parent)
        {
            switch (ancestor.Node)
            {
                // The member of __builtin_offsetof(S, a.b) is no expression for clang
                case Declarator or DeclSpecifier or DeclSpecifierSequence or TypeId or Name or NoexceptSpecifier or Designator or AttributeSpecifier
                    or BuiltinCallExpression { Name: "__builtin_offsetof" }:
                    return true;
                case Expression or Initializer:
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
        // The parameters of lambdas and requires-expressions
        switch (parent.Node)
        {
            case LambdaExpression lambda:
                return !IsVoid(lambda.Parameters);
            case RequiresExpression requires:
                return !IsVoid(requires.Parameters);
        }

        if (parent.Node is not FunctionDeclarator function || function.Inner is null || StripParentheses(function.Inner) is not NameDeclarator)
        {
            return false;
        }

        if (IsVoid(function.Parameters))
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
                    return (ancestor.Parent?.Node is not SimpleDeclaration declaration
                        || declaration.Specifiers?.Specifiers.Any(s => s is KeywordSpecifier { Keyword: "typedef" }) != true)
                        && !IsInExplicitInstantiation(ancestor.Parent);
                default:
                    return false;
            }
        }

        return false;
    }

    /// <summary>
    /// <c>(void)</c>: no parameters.
    /// </summary>
    private static bool IsVoid(IReadOnlyList<ParameterDeclaration> parameters)
    {
        return parameters is [{ Declarator: null, DefaultValue: null, Specifiers.Specifiers: [KeywordSpecifier { Keyword: "void" }] }];
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
    /// <c>~</c>, at the '[' of a structured binding. -1 for an abstract declarator.
    /// </summary>
    public static int NameLocation(Declarator declarator)
    {
        // A structured binding is located at its '['
        for (var binding = declarator; binding is ReferenceDeclarator or StructuredBindingDeclarator;)
        {
            if (binding is StructuredBindingDeclarator)
            {
                return binding.Span.Start;
            }

            binding = ((ReferenceDeclarator)binding).Inner;
        }

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
