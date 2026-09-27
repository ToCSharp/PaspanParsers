using Paspan.Fluent;
using static Paspan.Fluent.Parsers;
using static PaspanParsers.CSharp.SyntaxRules;
using static PaspanParsers.CSharp.TokenParsers;

namespace PaspanParsers.CSharp;

/// <summary>
/// The combinator grammar of <see cref="CSharpHybridParser"/>: the compilation unit, declarations, blocks and statements.
/// </summary>
/// <remarks>
/// It builds the same nodes as the hand-written <c>SyntaxParser.Statements.cs</c>, <c>SyntaxParser.Members.cs</c>,
/// <c>SyntaxParser.Declarations.cs</c> and <c>SyntaxParser.CompilationUnit.cs</c>, with the same spans and
/// <c>#nullable</c> directives, and accepts the same inputs. Expressions, types, patterns and the lookahead-heavy
/// choices (explicit interfaces, contextual modifiers, local declarations) are hand-written rules
/// (<see cref="SyntaxRules.Rule{T}"/>). The hand-written parser runs <see cref="Block"/> and <see cref="Statement"/>
/// wherever it parses a block or a statement (<see cref="CSharpParseContext.Grammar"/>): lambdas and anonymous methods.
/// </remarks>
internal sealed partial class HybridGrammar
{
    public static HybridGrammar Instance { get; } = new();

    /// <summary>
    /// <c>'{' statement* '}'</c>
    /// </summary>
    public Parser<BlockStatement> Block { get; }

    public Parser<Statement> Statement { get; }

    private HybridGrammar()
    {
        var statement = new RecursiveRule<Statement>();
        var block = new RecursiveRule<BlockStatement>();

        // Hand-written rules
        var expression = Rule(SyntaxParser.ParseExpressionRule);
        var nestedExpression = Rule(SyntaxParser.ParseNestedExpressionRule);
        var type = Rule(SyntaxParser.ParseTypeRule);
        var localType = Rule(SyntaxParser.ParseLocalTypeRule);
        var variableInitializer = Rule(SyntaxParser.ParseVariableInitializerRule);
        var variableDeclaration = Rule(SyntaxParser.ParseVariableDeclarationRule);
        var pattern = Rule(SyntaxParser.ParsePatternRule);

        // Attributes, modifiers, parameters and constraints, shared with the declarations
        var parts = new DeclarationParts(nestedExpression, type);
        var attributeSections = parts.AttributeSections;
        var typeParameterList = parts.TypeParameterList;
        var parameterList = parts.ParameterList;
        var constraintClauses = parts.ConstraintClauses;

        var identifier = TokenParsers.Identifier;
        var semicolon = Punctuator(";");
        var comma = Punctuator(",");
        var openParen = Punctuator("(");
        var closeParen = Punctuator(")");

        // '(' expression ')'
        var parenthesized = openParen.SkipAnd(nestedExpression).AndSkip(closeParen);

        // expression (',' expression)*
        var expressionList = Separated(comma, nestedExpression);

        // identifier ['=' initializer] (',' identifier ['=' initializer])*
        var declarator = Node(
            identifier.And(ZeroOrOne(Punctuator("=").SkipAnd(variableInitializer)))
                .Then(x => new VariableDeclarator(x.Item1.Text, x.Item2)));
        var declarators = Separated(comma, declarator);

        var expressionStatement = expression.AndSkip(semicolon).Then(Statement (e) => new ExpressionStatement(e));

        // Blocks
        block.Parser = BlockScope(Node(
            Punctuator("{").And(ZeroOrMany(statement)).And(Punctuator("}"))
                .Then(x => new BlockStatement(x.Item2.Count != 0 ? x.Item2 : null)
                {
                    NullableDirectives = x.Item1.NullableDirectives,
                    CloseBraceNullableDirectives = x.Item3.NullableDirectives,
                })));

        // Local declarations and local functions: Type identifier, then '=', ';' or ',' for a declaration,
        // '(' or '<' for a function
        var functionBody = block.Then(b => (Block: b, Expression: (Expression)null))
            .Or(Punctuator("=>").SkipAnd(expression).AndSkip(semicolon).Then(e => (Block: (BlockStatement)null, Expression: e)))
            .Or(semicolon.Then(_ => (Block: (BlockStatement)null, Expression: (Expression)null)));

        var localFunctionTail = identifier.WhenFollowedBy(Punctuator("(").Or(Punctuator("<")))
            .And(ZeroOrOne(typeParameterList))
            .And(parameterList)
            .And(constraintClauses)
            .And(functionBody)
            .Then(object (x) => new LocalFunctionTail(x.Item1.Text, x.Item2, x.Item3, x.Item4, x.Item5.Block, x.Item5.Expression));

        var localDeclarationTail = Lookahead(static (ref SyntaxParser p) => p.Current.IsIdentifier && p.Peek(1) is { Kind: TokenKind.Punctuator, Text: "=" or ";" or "," })
            .SkipAnd(declarators)
            .AndSkip(semicolon)
            .Then(object (d) => d);

        // Most statements are expressions like a.b(c): the type is not parsed when only a name can be here
        var typedLocal = Lookahead(static (ref SyntaxParser p) => p.MayStartLocalDeclaration())
            .SkipAnd(localType)
            .And(localFunctionTail.Or(localDeclarationTail));

        var localDeclarationOrFunction = typedLocal
            .When(x => !(x.Item1 is ScopedTypeReference && x.Item2 is LocalFunctionTail))
            .Then(x => BuildLocal(null, null, x.Item1, x.Item2));

        // Attributes and modifiers, then a local function; a local declaration only without them
        var localModifier = new TokenSwitch<Modifiers>()
            .OnKeyword("static", Keyword("static").Then(Modifiers.Static))
            .OnKeyword("extern", Keyword("extern").Then(Modifiers.Extern))
            .OnKeyword("unsafe", Keyword("unsafe").Then(Modifiers.Unsafe))
            .OnContextual("async", Contextual("async").Then(Modifiers.Async), when: static (ref SyntaxParser p) => !p.Peek(1).IsPunctuator("(") && !p.Peek(1).IsPunctuator("="));

        var prefixedLocal = attributeSections.And(ZeroOrMany(localModifier)).And(typedLocal)
            .When(x => x.Item3.Item2 is LocalFunctionTail
                ? x.Item3.Item1 is not ScopedTypeReference
                : x.Item1.Count == 0 && x.Item2.Count == 0)
            .Then(x => BuildLocal(x.Item1.Count != 0 ? x.Item1 : null, x.Item2.Count != 0 ? x.Item2 : null, x.Item3.Item1, x.Item3.Item2));

        var declarationOrExpression = localDeclarationOrFunction.Or(expressionStatement);

        // Selection and iteration
        var ifStatement = Keyword("if").SkipAnd(parenthesized).And(statement).And(ZeroOrOne(Keyword("else").SkipAnd(statement)))
            .Then(Statement (x) => new IfStatement(x.Item1, x.Item2, x.Item3));

        var whileStatement = Keyword("while").SkipAnd(parenthesized).And(statement)
            .Then(Statement (x) => new WhileStatement(x.Item1, x.Item2));

        var doStatement = Keyword("do").SkipAnd(statement).AndSkip(Keyword("while")).And(parenthesized).AndSkip(semicolon)
            .Then(Statement (x) => new DoStatement(x.Item1, x.Item2));

        var forInitializer = variableDeclaration.WhenFollowedBy(semicolon).Then(d => new List<Statement> { d })
            .Or(expressionList.Then(expressions => expressions.ConvertAll(Statement (e) => new ExpressionStatement(e) { Span = e.Span })));

        var forStatement = Keyword("for").SkipAnd(openParen).SkipAnd(ZeroOrOne(forInitializer)).AndSkip(semicolon)
            .And(ZeroOrOne(nestedExpression)).AndSkip(semicolon)
            .And(ZeroOrOne(expressionList)).AndSkip(closeParen)
            .And(statement)
            .Then(Statement (x) => new ForStatement(x.Item4, x.Item1, x.Item2, x.Item3));

        var forEachVariable = localType.And(identifier).WhenFollowedBy(Keyword("in"))
                .Then(x => (Type: x.Item1, Identifier: x.Item2.Text, Variable: (Expression)null))
            .Or(nestedExpression.Then(e => (Type: (TypeReference)null, Identifier: (string)null, Variable: e)));

        Parser<Statement> ForEach(bool isAwait) =>
            Keyword("foreach").SkipAnd(openParen).SkipAnd(forEachVariable).AndSkip(Keyword("in"))
                .And(nestedExpression).AndSkip(closeParen)
                .And(statement)
                // And extends the tuple: (type, identifier, variable, collection, body)
                .Then(Statement (x) => new ForEachStatement(x.Item1, x.Item2, x.Item4, x.Item5, isAwait, x.Item3));

        // switch (expression) { ... }, or switch (a, b) { ... } on a tuple without extra parentheses
        var governing = parenthesized.Then(e => (Expression: e, HasParentheses: true))
            .Or(nestedExpression.When(e => e is TupleExpression).Then(e => (Expression: e, HasParentheses: false)));

        var caseLabel = Node(
            Keyword("case").And(pattern).And(ZeroOrOne(Contextual("when").SkipAnd(expression))).AndSkip(Punctuator(":"))
                .Then(SwitchLabel (x) => new CaseSwitchLabel(x.Item2, x.Item3) { NullableDirectives = x.Item1.NullableDirectives }));

        var defaultLabel = Node(
            Keyword("default").AndSkip(Punctuator(":"))
                .Then(SwitchLabel (t) => new DefaultSwitchLabel { NullableDirectives = t.NullableDirectives }));

        var switchSection = Node(
            OneOrMany(caseLabel.Or(defaultLabel)).And(ZeroOrMany(statement))
                .Then(x => new SwitchSection(x.Item1, x.Item2)));

        var switchStatement = Keyword("switch").SkipAnd(governing).AndSkip(Punctuator("{"))
            .And(ZeroOrMany(switchSection)).AndSkip(Punctuator("}"))
            // (expression, has parentheses, sections)
            .Then(Statement (x) => new SwitchStatement(x.Item1, x.Item3.Count != 0 ? x.Item3 : null) { HasParentheses = x.Item2 });

        // try block (catch ['(' Type [identifier] ')'] [when '(' expression ')'] block)* [finally block]
        var catchDeclaration = openParen.SkipAnd(type).And(ZeroOrOne(identifier)).AndSkip(closeParen);

        var catchClause = Node(
            Keyword("catch").SkipAnd(ZeroOrOne(catchDeclaration)).And(ZeroOrOne(Contextual("when").SkipAnd(parenthesized))).And(block)
                // (type, identifier, filter, block)
                .Then(x => new CatchClause(x.Item4, x.Item1, x.Item2.Text, x.Item3)));

        var tryStatement = Keyword("try").SkipAnd(block).And(ZeroOrMany(catchClause)).And(ZeroOrOne(Keyword("finally").SkipAnd(block)))
            .When(x => x.Item2.Count != 0 || x.Item3 != null)
            .Then(Statement (x) => new TryStatement(x.Item1, x.Item2.Count != 0 ? x.Item2 : null, x.Item3));

        // using '(' (declaration | expression) ')' statement, or a using declaration: using Type x = ...;
        var usingResource = variableDeclaration.WhenFollowedBy(closeParen).Then(Statement (d) => d)
            .Or(nestedExpression.Then(Statement (e) => new ExpressionStatement(e) { Span = e.Span }));

        Parser<Statement> Using(bool isAwait) =>
            Keyword("using").SkipAnd(new TokenSwitch<Statement>()
                .OnPunctuator(
                    "(",
                    openParen.SkipAnd(usingResource).AndSkip(closeParen).And(statement)
                        .Then(Statement (x) => new UsingStatement(x.Item1, x.Item2, isAwait)),
                    commit: true)
                .Otherwise(
                    localType.And(declarators).AndSkip(semicolon)
                        .Then(Statement (x) => new LocalDeclarationStatement(x.Item1, x.Item2, isUsing: true, isAwait: isAwait))));

        // Other statements
        var lockStatement = Keyword("lock").SkipAnd(parenthesized).And(statement)
            .Then(Statement (x) => new LockStatement(x.Item1, x.Item2));

        var fixedStatement = Keyword("fixed").SkipAnd(openParen).SkipAnd(type).And(declarators).AndSkip(closeParen).And(statement)
            .Then(Statement (x) => new FixedStatement(x.Item1, x.Item2, x.Item3));

        var gotoStatement = Keyword("goto").SkipAnd(
                Keyword("case").SkipAnd(expression).Then(e => new GotoStatement(null, GotoKind.Case, e))
                    .Or(Keyword("default").Then(_ => new GotoStatement(null, GotoKind.Default)))
                    .Or(identifier.Then(t => new GotoStatement(t.Text))))
            .AndSkip(semicolon)
            .Then(Statement (s) => s);

        var constStatement = Keyword("const").SkipAnd(type).And(declarators).AndSkip(semicolon)
            .Then(Statement (x) => new LocalDeclarationStatement(x.Item1, x.Item2, isConst: true));

        var returnStatement = Keyword("return").SkipAnd(ZeroOrOne(expression)).AndSkip(semicolon).Then(Statement (e) => new ReturnStatement(e));
        var throwStatement = Keyword("throw").SkipAnd(ZeroOrOne(expression)).AndSkip(semicolon).Then(Statement (e) => new ThrowStatement(e));

        var labeledStatement = identifier.AndSkip(Punctuator(":")).And(statement)
            .Then(Statement (x) => new LabeledStatement(x.Item1.Text, x.Item2));

        TokenCondition nextIsOpenBrace = static (ref SyntaxParser p) => p.Peek(1).IsPunctuator("{");
        TokenCondition nextIsColon = static (ref SyntaxParser p) => p.Peek(1).IsPunctuator(":");
        TokenCondition nextIsKeyword(string keyword) => (ref SyntaxParser p) => p.Peek(1).IsKeyword(keyword);

        // The first tokens decide the statement, like SyntaxParser.ParseStatementCore; what they do not
        // decide is a local declaration, a local function or an expression statement
        var statementCore = new TokenSwitch<Statement>()
            .OnPunctuator("{", block.Then(Statement (b) => b), commit: true)
            .OnPunctuator(";", semicolon.Then(Statement (_) => new EmptyStatement()), commit: true)
            .OnPunctuator("[", prefixedLocal, commit: true)
            // Nothing starts with these; failing at once ends lists of statements quickly
            .OnPunctuator("}", Fail<Statement>(), commit: true)
            .OnKeyword("case", Fail<Statement>(), commit: true)
            .OnKeyword("default", Fail<Statement>(), when: nextIsColon, commit: true)
            .OnKeyword("if", ifStatement, commit: true)
            .OnKeyword("while", whileStatement, commit: true)
            .OnKeyword("do", doStatement, commit: true)
            .OnKeyword("for", forStatement, commit: true)
            .OnKeyword("foreach", ForEach(isAwait: false), commit: true)
            .OnKeyword("switch", switchStatement, commit: true)
            .OnKeyword("try", tryStatement, commit: true)
            .OnKeyword("lock", lockStatement, commit: true)
            .OnKeyword("using", Using(isAwait: false), commit: true)
            .OnKeyword("fixed", fixedStatement, commit: true)
            .OnKeyword("goto", gotoStatement, commit: true)
            .OnKeyword("const", constStatement, commit: true)
            .OnKeyword("break", Keyword("break").AndSkip(semicolon).Then(Statement (_) => new BreakStatement()), commit: true)
            .OnKeyword("continue", Keyword("continue").AndSkip(semicolon).Then(Statement (_) => new ContinueStatement()), commit: true)
            .OnKeyword("return", returnStatement, commit: true)
            .OnKeyword("throw", throwStatement, commit: true)
            .OnKeyword("checked", Keyword("checked").SkipAnd(block).Then(Statement (b) => new CheckedStatement(true, b)), when: nextIsOpenBrace, commit: true)
            .OnKeyword("unchecked", Keyword("unchecked").SkipAnd(block).Then(Statement (b) => new CheckedStatement(false, b)), when: nextIsOpenBrace, commit: true)
            .OnKeyword("unsafe", Keyword("unsafe").SkipAnd(block).Then(Statement (b) => new UnsafeStatement(b)), when: nextIsOpenBrace, commit: true)
            .OnKeyword("unsafe", prefixedLocal, commit: true)
            .OnKeyword("static", prefixedLocal, commit: true)
            .OnKeyword("extern", prefixedLocal, commit: true)
            // label: statement, before the contextual keywords
            .OnContextual("yield", Contextual("yield").SkipAnd(Keyword("return")).SkipAnd(expression).AndSkip(semicolon).Then(Statement (e) => new YieldReturnStatement(e)), when: nextIsKeyword("return"), commit: true)
            .OnContextual("yield", Contextual("yield").SkipAnd(Keyword("break")).AndSkip(semicolon).Then(Statement (_) => new YieldBreakStatement()), when: nextIsKeyword("break"), commit: true)
            .OnContextual("await", Contextual("await").SkipAnd(ForEach(isAwait: true)), when: nextIsKeyword("foreach"), commit: true)
            .OnContextual("await", Contextual("await").SkipAnd(Using(isAwait: true)), when: nextIsKeyword("using"), commit: true)
            .OnContextual("await", expressionStatement, when: static (ref SyntaxParser p) => !p.Peek(1).IsPunctuator(":") && p.IsAwaitExpression(), commit: true)
            // An async local function; otherwise a label, a declaration or an expression
            .OnContextual("async", prefixedLocal, when: static (ref SyntaxParser p) => !p.Peek(1).IsPunctuator(":"))
            .OnKind(TokenKind.Identifier, labeledStatement, when: nextIsColon, commit: true)
            .Otherwise(declarationOrExpression);

        statement.Parser = StatementNode(statementCore);

        Block = block;
        Statement = statement;
        CompilationUnit = CreateCompilationUnit(parts, block, statement, declarators);
    }

    private HybridGrammar(Parser<BlockStatement> block, Parser<Statement> statement, Parser<CompilationUnit> compilationUnit)
    {
        Block = block;
        Statement = statement;
        CompilationUnit = compilationUnit;
    }

    /// <summary>
    /// The grammar with other entry points for the hand-written parser, for tests that observe them. The
    /// compilation unit and the declarations keep using the grammar's own blocks and statements.
    /// </summary>
    internal HybridGrammar WithEntryPoints(Func<Parser<BlockStatement>, Parser<BlockStatement>> block, Func<Parser<Statement>, Parser<Statement>> statement) =>
        new(block(Block), statement(Statement), CompilationUnit);

    /// <summary>
    /// The part of a local function after its return type.
    /// </summary>
    private sealed record LocalFunctionTail(
        string Name,
        List<TypeParameter> TypeParameters,
        List<Parameter> Parameters,
        List<TypeParameterConstraint> Constraints,
        BlockStatement Body,
        Expression ExpressionBody);

    private static Statement BuildLocal(List<AttributeSection> attributes, List<Modifiers> modifiers, TypeReference type, object tail) => tail switch
    {
        LocalFunctionTail f => new LocalFunctionStatement(
            type,
            f.Name,
            f.Parameters,
            f.Body,
            f.ExpressionBody,
            modifiers,
            f.TypeParameters,
            f.Constraints.Count != 0 ? f.Constraints : null,
            attributes),
        List<VariableDeclarator> variables => new LocalDeclarationStatement(type, variables),
        _ => throw new InvalidOperationException(),
    };
}
