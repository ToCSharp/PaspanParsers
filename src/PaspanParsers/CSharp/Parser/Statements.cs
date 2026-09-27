using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.CSharp;

// Statements.
public partial class CSharpParser
{
    private static Parser<List<VariableDeclarator>> variableDeclarators;

    private static void InitializeStatements()
    {
        // Expression statement
        var expressionStatement = expression.AndSkip(SEMICOLON)
            .Then<Statement>(expr => new ExpressionStatement(expr));

        // Variable declarator
        var variableDeclarator = anyIdentifier.And(EQ.SkipAnd(expression).Optional())
            .Then(result =>
            {
                var (name, init) = result;
                return new VariableDeclarator(name, init.OrSome(null));
            });

        variableDeclarators = Separated(COMMA, variableDeclarator);

        // Local variable declaration
        var localVarDecl = typeReference.And(variableDeclarators).AndSkip(SEMICOLON)
            .Then<Statement>(result =>
            {
                var (type, vars) = result;
                return new LocalDeclarationStatement(type, vars);
            });

        // If statement
        var ifStatement = IF.SkipAnd(Between(LPAREN, expression, RPAREN))
            .And(statement)
            .And(ELSE.SkipAnd(statement).Optional())
            .Then<Statement>(result =>
            {
                var (condition, thenStmt, elseStmt) = result;
                return new IfStatement(condition, thenStmt, elseStmt.OrSome(null));
            });

        // While statement
        var whileStatement = WHILE.SkipAnd(Between(LPAREN, expression, RPAREN))
            .And(statement)
            .Then<Statement>(result =>
            {
                var (condition, body) = result;
                return new WhileStatement(condition, body);
            });

        // Do-while statement
        var doStatement = DO.SkipAnd(statement).AndSkip(WHILE)
            .And(Between(LPAREN, expression, RPAREN)).AndSkip(SEMICOLON)
            .Then<Statement>(result =>
            {
                var (body, condition) = result;
                return new DoStatement(body, condition);
            });

        // For statement
        var forInit = localVarDecl.Or(expressionStatement).Or(SEMICOLON.Then<Statement>(_ => null));
        var forCondition = expression.Optional().AndSkip(SEMICOLON);
        var forIterator = Separated(COMMA, expression);

        var forStatement = FOR.SkipAnd(LPAREN)
            .SkipAnd(forInit.Else((Statement)null))
            .And(forCondition)
            .And(forIterator.Else([]))
            .AndSkip(RPAREN)
            .And(statement)
            .Then<Statement>(result =>
            {
                var (init, condition, iterators, body) = result;
                return new ForStatement(
                    body,
                    init != null ? new[] { init } : null,
                    condition.OrSome(null),
                    iterators.Count != 0 ? iterators : null
                );
            });

        // Return statement
        var returnStatement = RETURN.SkipAnd(expression.Optional()).AndSkip(SEMICOLON)
            .Then<Statement>(expr => new ReturnStatement(expr.OrSome(null)));

        // Break/Continue
        var breakStatement = BREAK.AndSkip(SEMICOLON).Then<Statement>(new BreakStatement());
        var continueStatement = CONTINUE.AndSkip(SEMICOLON).Then<Statement>(new ContinueStatement());

        // Throw statement
        var throwStatement = THROW.SkipAnd(expression.Optional()).AndSkip(SEMICOLON)
            .Then<Statement>(expr => new ThrowStatement(expr.OrSome(null)));

        // Block statement
        var statementList = ZeroOrMany(statement);
        block.Parser = Between(LBRACE, statementList, RBRACE)
            .Then<BlockStatement>(stmts => new BlockStatement(stmts.Count != 0 ? stmts : null));

        statement.Parser = block.Then<Statement>(b => b)
            .Or(ifStatement)
            .Or(whileStatement)
            .Or(doStatement)
            .Or(forStatement)
            .Or(returnStatement)
            .Or(breakStatement)
            .Or(continueStatement)
            .Or(throwStatement)
            .Or(localVarDecl)
            .Or(expressionStatement);
    }
}
