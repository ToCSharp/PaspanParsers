using Paspan.Fluent;
using static Paspan.Fluent.Parsers;
using static PaspanParsers.CSharp.SyntaxRules;
using static PaspanParsers.CSharp.TokenParsers;

namespace PaspanParsers.CSharp;

// Declarations and the compilation unit: the combinator counterpart of SyntaxParser.Members.cs,
// SyntaxParser.Declarations.cs and SyntaxParser.CompilationUnit.cs.
internal sealed partial class HybridGrammar
{
    /// <summary>
    /// extern-alias* using* global-attribute* member*, up to the end of the input.
    /// </summary>
    public Parser<CompilationUnit> CompilationUnit { get; }

    /// <summary>
    /// The attributes and modifiers before the rest of a member declaration. The rest builds the member node and reads
    /// them with <see cref="SyntaxRules.Prefix{TPrefix}(ParseContext)"/>; a member that is not valid with them is null.
    /// </summary>
    private readonly record struct MemberPrefix(List<AttributeSection> Attributes, Modifiers Modifiers, List<Modifiers> ModifierList);

    /// <summary>
    /// The return type and the explicit interface before the name of a method, property, indexer, field or operator.
    /// </summary>
    private readonly record struct TypedPrefix(TypeReference Type, TypeReference ExplicitInterface);

    /// <summary>
    /// The body of a class, struct, interface or record: '{' member* '}' [';'], or ';'.
    /// </summary>
    private readonly record struct TypeBody(
        bool HasBody,
        List<MemberDeclaration> Members,
        IReadOnlyList<NullableDirective> OpenBraceDirectives,
        IReadOnlyList<NullableDirective> CloseBraceDirectives,
        bool HasTrailingSemicolon);

    /// <summary>
    /// The keyword of a type declaration; a record may be a record struct or say 'class'.
    /// </summary>
    private readonly record struct TypeKeyword(string Keyword, bool IsRecordStruct, bool HasClassKeyword);

    /// <summary>
    /// The extern alias and using directives at the start of a compilation unit or namespace.
    /// </summary>
    private readonly record struct ExternsAndUsings(List<ExternAliasDirective> Externs, List<UsingDirective> Usings);

    private readonly record struct NamespaceBody(
        bool IsFileScoped,
        List<ExternAliasDirective> Externs,
        List<UsingDirective> Usings,
        List<MemberDeclaration> Members,
        IReadOnlyList<NullableDirective> CloseBraceDirectives,
        bool HasTrailingSemicolon);

    /// <summary>
    /// Parts of the grammar shared by declarations and statements: attributes, modifiers, parameters, type
    /// parameters and constraints (the counterpart of <c>SyntaxParser.Declarations.cs</c>).
    /// </summary>
    private sealed class DeclarationParts
    {
        public Parser<List<AttributeSection>> AttributeSections { get; }

        /// <summary>
        /// An attribute section that keeps the <c>#nullable</c> directives before it, as the global attributes do.
        /// </summary>
        public Parser<AttributeSection> GlobalAttributeSection { get; }

        public Parser<List<Modifiers>> MemberModifiers { get; }

        public Parser<List<TypeParameter>> TypeParameterList { get; }

        public Parser<List<Parameter>> ParameterList { get; }

        /// <summary>
        /// Parameters separated by commas, possibly none: the parameters of an indexer before ']'.
        /// </summary>
        public Parser<List<Parameter>> Parameters { get; }

        /// <summary>
        /// The receiver of an extension block, whose name may be missing.
        /// </summary>
        public Parser<Parameter> Receiver { get; }

        public Parser<List<TypeParameterConstraint>> ConstraintClauses { get; }

        public DeclarationParts(Parser<Expression> nestedExpression, Parser<TypeReference> type)
        {
            var identifier = TokenParsers.Identifier;
            var comma = Punctuator(",");
            var argument = Rule(SyntaxParser.ParseArgumentRule);
            var typeArgumentList = Rule(SyntaxParser.ParseTypeArgumentListRule);

            // Attributes: '[' [target ':'] attribute (',' attribute)* [','] ']'
            var attributeTarget = Lookahead(static (ref SyntaxParser p) => (p.Current.IsIdentifier || p.Current.Kind == TokenKind.Keyword) && p.Peek(1).IsPunctuator(":"))
                .SkipAnd(Token(static t => t.IsIdentifier || t.Kind == TokenKind.Keyword))
                .Then(static t => ParseAttributeTarget(t.Text))
                .When(static t => t.HasValue)
                .AndSkip(Punctuator(":"));

            // [alias '::'] identifier ('.' identifier)* [type arguments]
            var attributeName = Node(
                ZeroOrOne(identifier.AndSkip(Punctuator("::")).Then(static t => t.Text))
                    .And(Separated(Punctuator("."), identifier))
                    .And(ZeroOrOne(typeArgumentList))
                    .Then(static x => new NameExpression(x.Item2.ConvertAll(static t => t.Text), x.Item3?.Types, x.Item1)
                    {
                        CloseAngleNullableDirectives = x.Item3?.CloseDirectives,
                    }));

            // Name = value, or an argument; the name decides once '=' follows it
            var attributeArgument = new TokenSwitch<Argument>()
                .OnKind(
                    TokenKind.Identifier,
                    Node(identifier.AndSkip(Punctuator("=")).And(nestedExpression).Then(static x => new Argument(x.Item2, x.Item1.Text, isNameEquals: true))),
                    when: static (ref SyntaxParser p) => p.Peek(1).IsPunctuator("="),
                    commit: true)
                .Otherwise(argument);

            var attributeArguments = Punctuator("(").SkipAnd(ZeroOrOne(Separated(comma, attributeArgument))).AndSkip(Punctuator(")"))
                .Then(static arguments => arguments ?? []);

            var attribute = Node(
                attributeName.And(ZeroOrOne(attributeArguments))
                    .Then(static x => x.Item2 == null ? new AttributeNode(x.Item1) : new AttributeNode(x.Item1, x.Item2)));

            var attributeList = attribute.And(ZeroOrMany(comma.SkipAnd(attribute))).AndSkip(ZeroOrOne(comma))
                .Then(static x =>
                {
                    x.Item2.Insert(0, x.Item1);
                    return x.Item2;
                });

            var attributeSection = Punctuator("[").And(ZeroOrOne(attributeTarget, null)).And(attributeList).AndSkip(Punctuator("]"));

            var section = Node(attributeSection.Then(static x => new AttributeSection(x.Item3, x.Item2)));
            AttributeSections = ZeroOrMany(section);
            GlobalAttributeSection = Node(
                attributeSection.Then(static x => new AttributeSection(x.Item3, x.Item2) { NullableDirectives = x.Item1.NullableDirectives }));

            // Modifiers in source order; the contextual ones only when a declaration follows them
            var memberModifier = new TokenSwitch<Modifiers>();
            foreach (var (keyword, modifier) in KeywordModifiers)
            {
                memberModifier.OnKeyword(keyword, Keyword(keyword).Then(modifier));
            }

            // 'ref' is a modifier of ref structs; before a type it starts a by-reference return type
            memberModifier.OnKeyword(
                "ref",
                Keyword("ref").Then(Modifiers.Ref),
                when: static (ref SyntaxParser p) => p.Peek(1).IsKeyword("struct") || p.Peek(1).IsContextual("partial"));

            foreach (var (keyword, modifier) in ContextualModifiers)
            {
                memberModifier.OnContextual(keyword, Contextual(keyword).Then(modifier), when: static (ref SyntaxParser p) => p.IsContextualModifier());
            }

            MemberModifiers = ZeroOrMany(memberModifier);

            // Parameters: [attributes] modifier* (Type identifier ['=' default] | __arglist)
            var parameterModifier = new TokenSwitch<ParameterModifier>()
                .OnKeyword("this", Keyword("this").Then(ParameterModifier.This))
                .OnKeyword("ref", Keyword("ref").Then(ParameterModifier.Ref))
                .OnKeyword("out", Keyword("out").Then(ParameterModifier.Out))
                .OnKeyword("in", Keyword("in").Then(ParameterModifier.In))
                .OnKeyword("params", Keyword("params").Then(ParameterModifier.Params))
                .OnKeyword("readonly", Keyword("readonly").Then(ParameterModifier.Readonly))
                .OnContextual("scoped", Contextual("scoped").Then(ParameterModifier.Scoped), when: static (ref SyntaxParser p) => p.IsScopedModifier());

            var defaultValue = ZeroOrOne(Punctuator("=").SkipAnd(nestedExpression));

            Parser<Parameter> Parameter(bool allowMissingName)
            {
                // The receiver of an extension block may have no name: extension(string) { }
                var name = allowMissingName
                    ? identifier.Then(static t => t.Text).Or(Lookahead(static (ref SyntaxParser p) => p.Current.IsPunctuator(")")).Then(static _ => (string)null))
                    : identifier.Then(static t => t.Text);

                var tail = new TokenSwitch<(TypeReference Type, string Name, Expression DefaultValue)>()
                    .OnKeyword("__arglist", Keyword("__arglist").Then(static _ => ((TypeReference)null, "__arglist", (Expression)null)), commit: true)
                    .Otherwise(type.And(name).And(defaultValue).Then(static x => (x.Item1, x.Item2, x.Item3)));

                return Node(
                    AttributeSections.And(ZeroOrMany(parameterModifier)).And(tail)
                        .Then(static x =>
                        {
                            var primary = x.Item2.FirstOrDefault(static m => m is not (ParameterModifier.Scoped or ParameterModifier.Readonly));
                            var attributes = x.Item1.Count != 0 ? x.Item1 : null;
                            return new Parameter(x.Item3.Type, x.Item3.Name, primary, x.Item3.DefaultValue, attributes, x.Item2);
                        }),
                    static (p, directives) => p.NullableDirectives = directives);
            }

            var parameter = Parameter(allowMissingName: false);
            Parameters = ZeroOrOne(Separated(comma, parameter)).Then(static parameters => parameters ?? []);
            ParameterList = Punctuator("(").SkipAnd(Parameters).AndSkip(Punctuator(")"));
            Receiver = Parameter(allowMissingName: true);

            // Type parameters: '<' [attributes] [in | out] identifier (',' ...)* '>'
            var variance = ZeroOrOne(
                Keyword("in").Then(static _ => (VarianceKind?)VarianceKind.In).Or(Keyword("out").Then(static _ => (VarianceKind?)VarianceKind.Out)),
                null);

            var typeParameter = Node(
                AttributeSections.And(variance).And(identifier)
                    .Then(static x => new TypeParameter(x.Item3.Text, x.Item2, x.Item1.Count != 0 ? x.Item1 : null)));

            TypeParameterList = Punctuator("<").SkipAnd(Separated(comma, typeParameter)).AndSkip(Punctuator(">"));

            // Constraints: (where identifier ':' constraint (',' constraint)*)*
            TokenCondition endsConstraint = static (ref SyntaxParser p) =>
                p.Peek(1) is not { Kind: TokenKind.Punctuator, Text: "." or "<" or "::" or "?" };

            var constraint = Node(
                new TokenSwitch<TypeConstraint>()
                    .OnKeyword("class", Keyword("class").SkipAnd(ZeroOrOne(Punctuator("?").Then(true), false)).Then(TypeConstraint (n) => new ClassConstraint(n)), commit: true)
                    .OnKeyword("struct", Keyword("struct").Then(TypeConstraint (_) => new StructConstraint()), commit: true)
                    .OnKeyword("new", Keyword("new").And(Punctuator("(")).And(Punctuator(")")).Then(TypeConstraint (_) => new ConstructorConstraint()))
                    .OnKeyword("default", Keyword("default").Then(TypeConstraint (_) => new DefaultConstraint()), commit: true)
                    .OnContextual("allows", Contextual("allows").And(Keyword("ref")).And(Keyword("struct")).Then(TypeConstraint (_) => new AllowsRefStructConstraint()))
                    .OnContextual("unmanaged", Contextual("unmanaged").Then(TypeConstraint (_) => new UnmanagedConstraint()), when: endsConstraint)
                    .OnContextual("notnull", Contextual("notnull").Then(TypeConstraint (_) => new NotNullConstraint()), when: endsConstraint)
                    .Otherwise(type.Then(TypeConstraint (t) => new TypeReferenceConstraint(t))));

            var constraintClause = Node(
                Contextual("where").And(identifier).AndSkip(Punctuator(":")).And(Separated(comma, constraint))
                    .Then(static x => new TypeParameterConstraint(x.Item2.Text, x.Item3) { NullableDirectives = x.Item1.NullableDirectives }));

            // Once 'where name :' is seen the clause must parse, like in the hand-written parser
            ConstraintClauses = ZeroOrMany(constraintClause)
                .AndSkip(Lookahead(static (ref SyntaxParser p) => !(p.Current.IsContextual("where") && p.Peek(1).IsIdentifier && p.Peek(2).IsPunctuator(":"))));
        }

        private static AttributeTarget? ParseAttributeTarget(string text) => text switch
        {
            "assembly" => AttributeTarget.Assembly,
            "module" => AttributeTarget.Module,
            "field" => AttributeTarget.Field,
            "event" => AttributeTarget.Event,
            "method" => AttributeTarget.Method,
            "param" => AttributeTarget.Param,
            "property" => AttributeTarget.Property,
            "return" => AttributeTarget.Return,
            "type" => AttributeTarget.Type,
            "typevar" => AttributeTarget.TypeVar,
            _ => null,
        };

        private static readonly (string, Modifiers)[] KeywordModifiers =
        [
            ("public", Modifiers.Public),
            ("private", Modifiers.Private),
            ("protected", Modifiers.Protected),
            ("internal", Modifiers.Internal),
            ("static", Modifiers.Static),
            ("readonly", Modifiers.Readonly),
            ("const", Modifiers.Const),
            ("virtual", Modifiers.Virtual),
            ("override", Modifiers.Override),
            ("abstract", Modifiers.Abstract),
            ("sealed", Modifiers.Sealed),
            ("extern", Modifiers.Extern),
            ("unsafe", Modifiers.Unsafe),
            ("volatile", Modifiers.Volatile),
            ("new", Modifiers.New),
            ("fixed", Modifiers.Fixed),
        ];

        private static readonly (string, Modifiers)[] ContextualModifiers =
        [
            ("partial", Modifiers.Partial),
            ("async", Modifiers.Async),
            ("required", Modifiers.Required),
            ("file", Modifiers.File),
            ("safe", Modifiers.Safe),
        ];
    }

    /// <summary>
    /// Operators that are a single token in an operator declaration; '&gt;&gt;', '&gt;&gt;&gt;' and their
    /// assignments are composed from adjacent tokens.
    /// </summary>
    private static readonly HashSet<string> OverloadableOperators =
    [
        "+", "-", "!", "~", "++", "--", "*", "/", "%", "&", "|", "^", "<<", "==", "!=", "<", "<=", ">=",
        "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=",
    ];

    private static Parser<CompilationUnit> CreateCompilationUnit(
        DeclarationParts parts,
        Parser<BlockStatement> block,
        Parser<Statement> statement,
        Parser<List<VariableDeclarator>> declarators)
    {
        var expression = Rule(SyntaxParser.ParseExpressionRule);
        var nestedExpression = Rule(SyntaxParser.ParseNestedExpressionRule);
        var type = Rule(SyntaxParser.ParseTypeRule);
        var returnType = Rule(SyntaxParser.ParseReturnTypeRule);
        var explicitInterface = ZeroOrOne(Rule(SyntaxParser.ParseExplicitInterfaceSpecifierRule));
        var variableInitializer = Rule(SyntaxParser.ParseVariableInitializerRule);
        var argumentList = Rule(SyntaxParser.ParseArgumentListRule);
        var bracketedArgumentList = Rule(SyntaxParser.ParseBracketedArgumentListRule);

        var attributeSections = parts.AttributeSections;
        var typeParameterList = ZeroOrOne(parts.TypeParameterList);
        var parameterList = parts.ParameterList;
        var constraintClauses = parts.ConstraintClauses;

        var identifier = TokenParsers.Identifier;
        var semicolon = Punctuator(";");
        var comma = Punctuator(",");
        var optionalSemicolon = ZeroOrOne(semicolon.Then(true), false);
        var endOfFile = Token(static t => t.Kind == TokenKind.EndOfFile);

        var unitMember = new RecursiveRule<MemberDeclaration>();
        var namespaceMember = new RecursiveRule<MemberDeclaration>();
        var typeMember = new RecursiveRule<MemberDeclaration>();

        // ========================================
        // Extern aliases and using directives
        // ========================================

        var externAlias = Node(
            Keyword("extern").SkipAnd(Contextual("alias")).SkipAnd(identifier).AndSkip(semicolon)
                .Then(CSharpNode (t) => new ExternAliasDirective(t.Text)),
            static (d, directives) => ((ExternAliasDirective)d).NullableDirectives = directives);

        // [global] using [static] [unsafe] (Name | Alias '=' Type | Type) ';'
        var usingPrefix = ZeroOrOne(
                Lookahead(static (ref SyntaxParser p) => p.Current.IsContextual("global") && p.Peek(1).IsKeyword("using")).SkipAnd(Contextual("global")).Then(true),
                false)
            .AndSkip(Keyword("using"))
            .And(ZeroOrOne(Keyword("static").Then(true), false))
            .And(ZeroOrOne(Keyword("unsafe").Then(true), false));

        var usingTarget = new TokenSwitch<(string Alias, TypeReference Type)>()
            .OnKind(
                TokenKind.Identifier,
                identifier.AndSkip(Punctuator("=")).And(type).Then(static x => (x.Item1.Text, x.Item2)),
                when: static (ref SyntaxParser p) => p.Peek(1).IsPunctuator("="),
                commit: true)
            .Otherwise(type.Then(static t => ((string)null, t)));

        var usingDirective = Node(
            usingPrefix.And(usingTarget).AndSkip(semicolon)
                .Then(CSharpNode (x) => BuildUsingDirective(x.Item1, x.Item2, x.Item3, x.Item4.Alias, x.Item4.Type))
                .When(static d => d != null),
            static (d, directives) => ((UsingDirective)d).NullableDirectives = directives);

        // Extern aliases and usings in any order; a 'using' that is not a directive ends them
        var externsAndUsings = ZeroOrMany(externAlias.Or(usingDirective))
            .Then(static nodes => new ExternsAndUsings(nodes.OfType<ExternAliasDirective>().ToList(), nodes.OfType<UsingDirective>().ToList()));

        // ========================================
        // Member bodies and accessors
        // ========================================

        // A block, '=>' expression ';', or ';' for a member without a body (null)
        var memberBody = new TokenSwitch<MethodBody>()
            .OnPunctuator("{", block.Then(MethodBody (b) => new BlockMethodBody(b) { Span = b.Span }), commit: true)
            .OnPunctuator(
                "=>",
                // Like Roslyn's arrow expression clause, the body ends before the ';'
                Punctuator("=>").And(expression).AndSkip(semicolon)
                    .Then(MethodBody (x) => new ExpressionMethodBody(x.Item2) { Span = new TextSpan(x.Item1.Start, x.Item2.Span.End) }),
                commit: true)
            .OnPunctuator(";", semicolon.Then(static _ => (MethodBody)null), commit: true);

        // [attributes] modifier* (get | set | init) body
        var accessorKind = Contextual("get").Then(AccessorKind.Get)
            .Or(Contextual("set").Then(AccessorKind.Set))
            .Or(Contextual("init").Then(AccessorKind.Init));

        var accessor = Node(
            attributeSections.And(parts.MemberModifiers).And(accessorKind).And(memberBody)
                .Then(static x => new Accessor(x.Item3, NullIfEmpty(x.Item1), SyntaxParser.Combine(x.Item2), x.Item4) { ModifierList = NullIfEmpty(x.Item2) }),
            static (a, directives) => a.NullableDirectives = directives);

        // '{' accessor* '}', with the '{' for its #nullable directives
        var accessorList = Punctuator("{").And(ZeroOrMany(accessor)).AndSkip(Punctuator("}"));

        // [attributes] (add | remove) (block | '=>' expression ';')
        var eventAccessorKind = Contextual("add").Then(EventAccessorKind.Add).Or(Contextual("remove").Then(EventAccessorKind.Remove));

        var eventAccessor = Node(
            attributeSections.And(eventAccessorKind).And(memberBody)
                .When(static x => x.Item3 != null)
                .Then(static x => x.Item3 is BlockMethodBody body
                    ? new EventAccessor(x.Item2, body.Block, NullIfEmpty(x.Item1))
                    : new EventAccessor(x.Item2, null, NullIfEmpty(x.Item1)) { ExpressionBody = ((ExpressionMethodBody)x.Item3).Expression }));

        var eventAccessorList = Punctuator("{").SkipAnd(ZeroOrMany(eventAccessor)).AndSkip(Punctuator("}"));

        // ========================================
        // Types
        // ========================================

        var typeBody = Punctuator("{").And(ZeroOrMany(typeMember)).And(Punctuator("}"));

        var typeBodyOrSemicolon = semicolon.Then(static _ => new TypeBody(false, null, null, null, false))
            .Or(typeBody.And(optionalSemicolon).Then(static x => new TypeBody(true, x.Item2, x.Item1.NullableDirectives, x.Item3.NullableDirectives, x.Item4)));

        // ':' Type [arguments] (',' Type)*: the base class of a primary constructor takes arguments
        var baseList = Punctuator(":").SkipAnd(type).And(ZeroOrOne(argumentList)).And(ZeroOrMany(comma.SkipAnd(type)))
            .Then(static x =>
            {
                x.Item3.Insert(0, x.Item1);
                return (Types: x.Item3, Arguments: x.Item2);
            });

        // keyword Name [type parameters] [parameters] [':' base types] constraints body
        Parser<MemberDeclaration> TypeDeclaration(Parser<TypeKeyword> keyword, bool allowParameters) =>
            keyword.And(identifier).And(typeParameterList)
                .And(allowParameters ? ZeroOrOne(parameterList) : Always<List<Parameter>>(null))
                .And(ZeroOrOne(baseList, (null, null)))
                .And(constraintClauses)
                .And(typeBodyOrSemicolon)
                .Then(static (c, x) => BuildTypeDeclaration(Prefix<MemberPrefix>(c), x.Item1, x.Item2.Text, x.Item3, x.Item4, x.Item5.Types, x.Item5.Arguments, x.Item6, x.Item7));

        Parser<TypeKeyword> PlainTypeKeyword(string keyword) => Keyword(keyword).Then(_ => new TypeKeyword(keyword, false, false));

        var recordKeyword = Contextual("record").SkipAnd(ZeroOrOne(Keyword("struct").Or(Keyword("class"))))
            .Then(static t => new TypeKeyword("record", t.IsKeyword("struct"), t.IsKeyword("class")));

        // enum Name [':' Type] '{' [member (',' member)* [',']] '}' [';']
        var enumMember = Node(
            attributeSections.And(identifier).And(ZeroOrOne(Punctuator("=").SkipAnd(expression)))
                .Then(static x => new EnumMember(x.Item2.Text, x.Item3, NullIfEmpty(x.Item1))));

        var enumMembers = enumMember.And(ZeroOrMany(comma.SkipAnd(enumMember))).And(ZeroOrOne(comma.Then(true), false))
            .Then(static x =>
            {
                x.Item2.Insert(0, x.Item1);
                return (Members: x.Item2, HasTrailingComma: x.Item3);
            });

        var enumDeclaration = Keyword("enum").SkipAnd(identifier).And(ZeroOrOne(Punctuator(":").SkipAnd(type)))
            .AndSkip(Punctuator("{")).And(ZeroOrOne(enumMembers, (null, false))).AndSkip(Punctuator("}"))
            .And(optionalSemicolon)
            .Then(static (c, x) =>
            {
                var p = Prefix<MemberPrefix>(c);
                return (MemberDeclaration)new EnumDeclaration(x.Item1.Text, p.Attributes, p.Modifiers, x.Item2, x.Item3.Members)
                {
                    ModifierList = p.ModifierList,
                    HasBody = true,
                    HasTrailingComma = x.Item3.HasTrailingComma,
                    HasTrailingSemicolon = x.Item4,
                };
            });

        // delegate ReturnType Name [type parameters] '(' parameters ')' constraints ';'
        var delegateDeclaration = Keyword("delegate").SkipAnd(returnType).And(identifier).And(typeParameterList).And(parameterList).And(constraintClauses).AndSkip(semicolon)
            .Then(static (c, x) =>
            {
                var p = Prefix<MemberPrefix>(c);
                return (MemberDeclaration)new DelegateDeclaration(x.Item1, x.Item2.Text, p.Attributes, p.Modifiers, x.Item3, x.Item4, NullIfEmpty(x.Item5))
                {
                    ModifierList = p.ModifierList,
                };
            });

        // extension [type parameters] '(' receiver ')' constraints '{' members '}' (C# 14)
        var extensionBlock = Contextual("extension").SkipAnd(typeParameterList)
            .AndSkip(Punctuator("(")).And(parts.Receiver).AndSkip(Punctuator(")"))
            .And(constraintClauses)
            .And(typeBody)
            .Then(static (c, x) =>
            {
                var p = Prefix<MemberPrefix>(c);
                return (MemberDeclaration)new ExtensionBlockDeclaration(x.Item2, NullIfEmpty(x.Item4.Item2), x.Item1, NullIfEmpty(x.Item3), p.Attributes, p.Modifiers)
                {
                    ModifierList = p.ModifierList,
                    CloseBraceNullableDirectives = x.Item4.Item3.NullableDirectives,
                };
            });

        // ========================================
        // Members
        // ========================================

        // Name [type parameters] '(' parameters ')' constraints body
        var method = identifier.And(typeParameterList).And(parameterList).And(constraintClauses).And(memberBody)
            .Then(static (c, x) =>
            {
                var (p, t) = (Prefix<MemberPrefix>(c), Prefix<TypedPrefix>(c));
                return (MemberDeclaration)new MethodDeclaration(t.Type, x.Item1.Text, p.Attributes, p.Modifiers, x.Item2, x.Item3, NullIfEmpty(x.Item4), x.Item5)
                {
                    ModifierList = p.ModifierList,
                    ExplicitInterface = t.ExplicitInterface,
                };
            });

        // Name '=>' expression ';'
        var expressionProperty = identifier.AndSkip(Punctuator("=>")).And(expression).AndSkip(semicolon)
            .Then(static (c, x) =>
            {
                var (p, t) = (Prefix<MemberPrefix>(c), Prefix<TypedPrefix>(c));
                return (MemberDeclaration)new PropertyDeclaration(t.Type, x.Item1.Text, p.Attributes, p.Modifiers, expressionBody: x.Item2)
                {
                    ModifierList = p.ModifierList,
                    ExplicitInterface = t.ExplicitInterface,
                };
            });

        // Name '{' accessors '}' ['=' initializer ';']
        var property = identifier.And(accessorList).And(ZeroOrOne(Punctuator("=").SkipAnd(variableInitializer).AndSkip(semicolon)))
            .Then(static (c, x) =>
            {
                var (p, t) = (Prefix<MemberPrefix>(c), Prefix<TypedPrefix>(c));
                return (MemberDeclaration)new PropertyDeclaration(t.Type, x.Item1.Text, p.Attributes, p.Modifiers, x.Item2.Item2, initializer: x.Item3)
                {
                    ModifierList = p.ModifierList,
                    ExplicitInterface = t.ExplicitInterface,
                    OpenBraceNullableDirectives = x.Item2.Item1.NullableDirectives,
                };
            });

        // identifier ['[' size ']'] ['=' initializer] (',' ...)* ';'
        var fieldDeclarator = Node(
            identifier.And(ZeroOrOne(bracketedArgumentList)).And(ZeroOrOne(Punctuator("=").SkipAnd(variableInitializer)))
                .Then(static x => new VariableDeclarator(x.Item1.Text, x.Item3) { BracketedArguments = x.Item2 }));

        var field = Separated(comma, fieldDeclarator).AndSkip(semicolon)
            .Then(static (c, variables) =>
            {
                var (p, t) = (Prefix<MemberPrefix>(c), Prefix<TypedPrefix>(c));
                return t.ExplicitInterface != null
                    ? null
                    : (MemberDeclaration)new FieldDeclaration(t.Type, variables, p.Attributes, p.Modifiers) { ModifierList = p.ModifierList };
            });

        // this '[' parameters ']' ('=>' expression ';' | '{' accessors '}')
        var indexerBody = Punctuator("=>").SkipAnd(expression).AndSkip(semicolon).Then(static e => (Expression: e, OpenBrace: default(SyntaxToken), Accessors: (List<Accessor>)null))
            .Or(accessorList.Then(static x => (Expression: (Expression)null, OpenBrace: x.Item1, Accessors: x.Item2)));

        var indexer = Keyword("this").SkipAnd(Punctuator("[")).SkipAnd(parts.Parameters).AndSkip(Punctuator("]")).And(indexerBody)
            .Then(static (c, x) =>
            {
                var (p, t) = (Prefix<MemberPrefix>(c), Prefix<TypedPrefix>(c));
                return (MemberDeclaration)(x.Item2.Accessors == null
                    ? new IndexerDeclaration(t.Type, x.Item1, null, p.Attributes, p.Modifiers)
                    {
                        ModifierList = p.ModifierList,
                        ExplicitInterface = t.ExplicitInterface,
                        ExpressionBody = x.Item2.Expression,
                    }
                    : new IndexerDeclaration(t.Type, x.Item1, x.Item2.Accessors, p.Attributes, p.Modifiers)
                    {
                        ModifierList = p.ModifierList,
                        ExplicitInterface = t.ExplicitInterface,
                        OpenBraceNullableDirectives = x.Item2.OpenBrace.NullableDirectives,
                    });
            });

        // operator [checked] op '(' parameters ')' body
        var overloadableOperator = Keyword("true").Or(Keyword("false"))
            .Or(Punctuator(">>>=")).Or(Punctuator(">>>")).Or(Punctuator(">>=")).Or(Punctuator(">>")).Or(Punctuator(">"))
            .Or(Token(static t => t.Kind == TokenKind.Punctuator && OverloadableOperators.Contains(t.Text)))
            .Then(static t => t.Text);

        var isChecked = ZeroOrOne(Keyword("checked").Then(true), false);

        var operatorDeclaration = Keyword("operator").SkipAnd(isChecked).And(overloadableOperator).And(parameterList).And(memberBody)
            .Then(static (c, x) =>
            {
                var (p, t) = (Prefix<MemberPrefix>(c), Prefix<TypedPrefix>(c));
                return (MemberDeclaration)new OperatorDeclaration(t.Type, x.Item2, x.Item3, x.Item4, p.Attributes, p.Modifiers, x.Item1)
                {
                    ModifierList = p.ModifierList,
                    ExplicitInterface = t.ExplicitInterface,
                };
            });

        TokenCondition nextIs(string first, string second) => (ref SyntaxParser p) => p.Peek(1).IsPunctuator(first) || p.Peek(1).IsPunctuator(second);

        // After the return type and the explicit interface, the next tokens decide the member
        var afterType = new TokenSwitch<MemberDeclaration>()
            .OnKeyword("this", indexer, commit: true)
            .OnKeyword("operator", operatorDeclaration, commit: true)
            .OnKind(TokenKind.Identifier, method, when: nextIs("(", "<"), commit: true)
            .OnKind(TokenKind.Identifier, expressionProperty, when: static (ref SyntaxParser p) => p.Peek(1).IsPunctuator("=>"), commit: true)
            .OnKind(TokenKind.Identifier, property, when: static (ref SyntaxParser p) => p.Peek(1).IsPunctuator("{"), commit: true)
            .OnKind(TokenKind.Identifier, field, commit: true);

        var typedMember = WithPrefix(returnType.And(explicitInterface).Then(static x => new TypedPrefix(x.Item1, x.Item2)), afterType);

        // Name '(' parameters ')' [':' (base | this) '(' arguments ')'] body
        var constructorInitializer = Node(
            Punctuator(":").SkipAnd(Keyword("base").Or(Keyword("this"))).And(argumentList)
                .Then(static x => new ConstructorInitializer(x.Item1.Text == "base", x.Item2)));

        var constructor = identifier.And(parameterList).And(ZeroOrOne(constructorInitializer)).And(memberBody)
            .Then(static (c, x) =>
            {
                var p = Prefix<MemberPrefix>(c);
                return (MemberDeclaration)new ConstructorDeclaration(x.Item1.Text, p.Attributes, p.Modifiers, x.Item2, x.Item3, x.Item4) { ModifierList = p.ModifierList };
            });

        // '~' Name '(' ')' body
        var destructor = Punctuator("~").SkipAnd(identifier).AndSkip(Punctuator("(")).AndSkip(Punctuator(")")).And(memberBody)
            .Then(static (c, x) =>
            {
                var p = Prefix<MemberPrefix>(c);
                return (MemberDeclaration)new DestructorDeclaration(x.Item1.Text, p.Attributes, p.Modifiers, x.Item2) { ModifierList = p.ModifierList };
            });

        // event Type ([interface '.'] Name '{' accessors '}' | declarators ';')
        var eventWithAccessors = identifier.And(eventAccessorList)
            .Then(static x => (Name: x.Item1, Accessors: x.Item2, Variables: (List<VariableDeclarator>)null));

        var eventTail = new TokenSwitch<(SyntaxToken Name, List<EventAccessor> Accessors, List<VariableDeclarator> Variables)>()
            .OnKind(TokenKind.Identifier, eventWithAccessors, when: static (ref SyntaxParser p) => p.Peek(1).IsPunctuator("{"), commit: true)
            .Otherwise(declarators.AndSkip(semicolon).Then(static v => (Name: default(SyntaxToken), Accessors: (List<EventAccessor>)null, Variables: v)));

        var eventDeclaration = Keyword("event").SkipAnd(type).And(explicitInterface).And(eventTail)
            .Then(static (c, x) => BuildEventDeclaration(Prefix<MemberPrefix>(c), x.Item1, x.Item2, x.Item3.Name, x.Item3.Accessors, x.Item3.Variables));

        // (implicit | explicit) [interface '.'] operator [checked] Type '(' parameters ')' body
        var conversionOperator = Keyword("implicit").Or(Keyword("explicit")).And(explicitInterface).AndSkip(Keyword("operator"))
            .And(isChecked).And(type).And(parameterList).And(memberBody)
            .Then(static (c, x) =>
            {
                var p = Prefix<MemberPrefix>(c);
                return (MemberDeclaration)new ConversionOperatorDeclaration(x.Item1.Text == "implicit", x.Item4, x.Item5, x.Item6, p.Attributes, p.Modifiers, x.Item3)
                {
                    ModifierList = p.ModifierList,
                    ExplicitInterface = x.Item2,
                };
            });

        // ========================================
        // Namespaces
        // ========================================

        var namespaceName = Node(Separated(Punctuator("."), identifier).Then(static parts => new NameExpression(parts.ConvertAll(static t => t.Text))));

        // '{' body '}' [';']
        var namespaceBlock = Punctuator("{").SkipAnd(externsAndUsings).And(ZeroOrMany(namespaceMember)).And(Punctuator("}")).And(optionalSemicolon)
            .Then(static x => new NamespaceBody(false, x.Item1.Externs, x.Item1.Usings, x.Item2, x.Item3.NullableDirectives, x.Item4));

        // ';' body, up to the end of the file, whose #nullable directives belong to the compilation unit
        var fileScopedBody = semicolon.SkipAnd(externsAndUsings).And(ZeroOrMany(namespaceMember))
            .AndSkip(Lookahead(static (ref SyntaxParser p) => p.Current.Kind == TokenKind.EndOfFile))
            .Then(static x => new NamespaceBody(true, x.Item1.Externs, x.Item1.Usings, x.Item2, null, false));

        // namespace Name ('{' body '}' [';'] | ';' body), file-scoped only at the top level
        Parser<MemberDeclaration> Namespace(bool allowFileScoped) =>
            Keyword("namespace").SkipAnd(namespaceName).And(allowFileScoped ? fileScopedBody.Or(namespaceBlock) : namespaceBlock)
                .Then(MemberDeclaration (x) => x.Item2.IsFileScoped
                    ? new NamespaceDeclaration(x.Item1, NullIfEmpty(x.Item2.Members), NullIfEmpty(x.Item2.Usings), NullIfEmpty(x.Item2.Externs), isFileScopedNamespace: true)
                    : new NamespaceDeclaration(x.Item1, NullIfEmpty(x.Item2.Members), NullIfEmpty(x.Item2.Usings), NullIfEmpty(x.Item2.Externs))
                    {
                        HasTrailingSemicolon = x.Item2.HasTrailingSemicolon,
                        CloseBraceNullableDirectives = x.Item2.CloseBraceDirectives,
                    });

        // ========================================
        // Member declarations
        // ========================================

        // After the attributes and modifiers the first tokens decide the declaration, like
        // SyntaxParser.ParseMemberDeclarationRest; outside types only type declarations are members
        Parser<MemberDeclaration> Rest(MemberContext context)
        {
            TokenCondition isDelegateDeclaration = context == MemberContext.CompilationUnit
                ? static (ref SyntaxParser p) => p.Peek(1) is not { Kind: TokenKind.Punctuator, Text: "*" or "(" or "{" }
                : static (ref SyntaxParser p) => !p.Peek(1).IsPunctuator("*");

            var rest = new TokenSwitch<MemberDeclaration>()
                .OnKeyword("class", TypeDeclaration(PlainTypeKeyword("class"), allowParameters: true), commit: true)
                .OnKeyword("struct", TypeDeclaration(PlainTypeKeyword("struct"), allowParameters: true), commit: true)
                .OnKeyword("interface", TypeDeclaration(PlainTypeKeyword("interface"), allowParameters: false), commit: true)
                .OnKeyword("enum", enumDeclaration, commit: true)
                .OnKeyword("delegate", delegateDeclaration, when: isDelegateDeclaration, commit: true)
                .OnContextual("record", TypeDeclaration(recordKeyword, allowParameters: true), when: static (ref SyntaxParser p) => p.IsRecordDeclarationStart(), commit: true);

            if (context != MemberContext.Type)
            {
                return rest;
            }

            return rest
                .OnPunctuator("~", destructor, commit: true)
                .OnKeyword("event", eventDeclaration, commit: true)
                .OnKeyword("implicit", conversionOperator, commit: true)
                .OnKeyword("explicit", conversionOperator, commit: true)
                .OnContextual("extension", extensionBlock, when: nextIs("(", "<"), commit: true)
                .OnKind(TokenKind.Identifier, constructor, when: static (ref SyntaxParser p) => p.Peek(1).IsPunctuator("("), commit: true)
                .Otherwise(typedMember);
        }

        Parser<MemberDeclaration> Member(MemberContext context)
        {
            var prefix = attributeSections.And(parts.MemberModifiers)
                .Then(static x => new MemberPrefix(NullIfEmpty(x.Item1), SyntaxParser.Combine(x.Item2), NullIfEmpty(x.Item2)));

            var member = WithPrefix(prefix, Rest(context)).When(static m => m != null);

            // A namespace has no attributes or modifiers
            if (context != MemberContext.Type)
            {
                member = new TokenSwitch<MemberDeclaration>()
                    .OnKeyword("namespace", Namespace(allowFileScoped: context == MemberContext.CompilationUnit), commit: true)
                    .Otherwise(member);
            }

            member = Node(member, static (m, directives) => m.NullableDirectives = directives);

            // At the top level anything that is not a type or namespace declaration is a statement; the
            // statement keeps the #nullable directives before it
            if (context == MemberContext.CompilationUnit)
            {
                member = member.Or(Node(statement.Then(MemberDeclaration (s) => new GlobalStatement(s))));
            }

            return member;
        }

        unitMember.Parser = Member(MemberContext.CompilationUnit);
        namespaceMember.Parser = Member(MemberContext.Namespace);
        typeMember.Parser = Member(MemberContext.Type);

        // ========================================
        // Compilation unit
        // ========================================

        // Global attributes: once '[assembly:' or '[module:' is seen the section must parse
        TokenCondition isGlobalAttributeSection = static (ref SyntaxParser p) => p.IsGlobalAttributeSectionStart();
        var globalAttributes = ZeroOrMany(Lookahead(isGlobalAttributeSection).SkipAnd(parts.GlobalAttributeSection))
            .AndSkip(Lookahead(static (ref SyntaxParser p) => !p.IsGlobalAttributeSectionStart()));

        // The compilation unit spans the whole input, with the trivia around the tokens
        return externsAndUsings.And(globalAttributes).And(ZeroOrMany(unitMember)).And(endOfFile)
            .Then(static x => new CSharp.CompilationUnit(NullIfEmpty(x.Item1.Externs), NullIfEmpty(x.Item1.Usings), NullIfEmpty(x.Item2), NullIfEmpty(x.Item3))
            {
                EndNullableDirectives = x.Item4.NullableDirectives,
                Span = new TextSpan(0, x.Item4.End),
            });
    }

    private static List<T> NullIfEmpty<T>(List<T> list) => list != null && list.Count != 0 ? list : null;

    private static UsingDirective BuildUsingDirective(bool isGlobal, bool isStatic, bool isUnsafe, string alias, TypeReference type)
    {
        var name = SyntaxParser.AsName(type);
        if (alias != null)
        {
            return isStatic ? null : new UsingAliasDirective(alias, name, name == null ? type : null) { IsGlobal = isGlobal, IsUnsafe = isUnsafe };
        }

        if (isStatic)
        {
            return new UsingStaticDirective(name, name == null ? type : null) { IsGlobal = isGlobal, IsUnsafe = isUnsafe };
        }

        return name != null && name.TypeArguments == null
            ? new UsingNamespaceDirective(name) { IsGlobal = isGlobal, IsUnsafe = isUnsafe }
            : null;
    }

    private static MemberDeclaration BuildTypeDeclaration(
        MemberPrefix prefix,
        TypeKeyword keyword,
        string name,
        List<TypeParameter> typeParameters,
        List<Parameter> parameters,
        List<TypeReference> baseTypes,
        List<Argument> baseArguments,
        List<TypeParameterConstraint> constraints,
        TypeBody body)
    {
        var (attributes, modifiers, modifierList) = prefix;
        var constraintList = NullIfEmpty(constraints);
        var members = NullIfEmpty(body.Members);

        return keyword.Keyword switch
        {
            "class" => new ClassDeclaration(name, attributes, modifiers, typeParameters, baseTypes, constraintList, members, parameters)
            {
                ModifierList = modifierList, BaseArguments = baseArguments, HasBody = body.HasBody, HasTrailingSemicolon = body.HasTrailingSemicolon,
                OpenBraceNullableDirectives = body.OpenBraceDirectives, CloseBraceNullableDirectives = body.CloseBraceDirectives,
            },
            "struct" => new StructDeclaration(name, attributes, modifiers, typeParameters, baseTypes, constraintList, members, parameters)
            {
                ModifierList = modifierList, BaseArguments = baseArguments, HasBody = body.HasBody, HasTrailingSemicolon = body.HasTrailingSemicolon,
                OpenBraceNullableDirectives = body.OpenBraceDirectives, CloseBraceNullableDirectives = body.CloseBraceDirectives,
            },
            "interface" => new InterfaceDeclaration(name, attributes, modifiers, typeParameters, baseTypes, constraintList, members)
            {
                ModifierList = modifierList, BaseArguments = baseArguments, HasBody = body.HasBody, HasTrailingSemicolon = body.HasTrailingSemicolon,
                OpenBraceNullableDirectives = body.OpenBraceDirectives, CloseBraceNullableDirectives = body.CloseBraceDirectives,
            },
            _ => new RecordDeclaration(name, keyword.IsRecordStruct, attributes, modifiers, typeParameters, parameters, baseTypes, constraintList, members)
            {
                ModifierList = modifierList, BaseArguments = baseArguments, HasBody = body.HasBody, HasTrailingSemicolon = body.HasTrailingSemicolon,
                OpenBraceNullableDirectives = body.OpenBraceDirectives, CloseBraceNullableDirectives = body.CloseBraceDirectives,
                HasClassKeyword = keyword.HasClassKeyword,
            },
        };
    }

    private static MemberDeclaration BuildEventDeclaration(
        MemberPrefix prefix,
        TypeReference type,
        TypeReference explicitInterface,
        SyntaxToken name,
        List<EventAccessor> accessors,
        List<VariableDeclarator> variables)
    {
        if (accessors != null)
        {
            var variable = new VariableDeclarator(name.Text) { Span = new TextSpan(name.Start, name.End) };
            return new EventDeclaration(type, [variable], prefix.Attributes, prefix.Modifiers, accessors)
            {
                ModifierList = prefix.ModifierList,
                ExplicitInterface = explicitInterface,
            };
        }

        // Only an event with accessors implements an interface member explicitly
        return explicitInterface != null
            ? null
            : new EventDeclaration(type, variables, prefix.Attributes, prefix.Modifiers) { ModifierList = prefix.ModifierList };
    }
}
