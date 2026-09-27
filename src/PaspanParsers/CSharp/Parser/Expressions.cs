using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.CSharp;

// Expressions, lambdas and query expressions.
public partial class CSharpParser
{
    private static Parser<Expression> primary, relational, assignment;

    private static void InitializeExpressions()
    {
        var parenExpr = Between(LPAREN, expression, RPAREN)
            .Then<Expression>(expr => new ParenthesizedExpression(expr));

        // Arguments
        var argument = expression.Then(expr => new Argument(expr));
        var argumentList = Separated(COMMA, argument);

        // Invocation
        var invocation = nameExpr.And(Between(LPAREN, argumentList.Else([]), RPAREN))
            .Then<Expression>(result =>
            {
                var (expr, args) = result;
                return new InvocationExpression(expr, args);
            });

        // Primary expressions
        primary = invocation
            .Or(parenExpr)
            .Or(literal)
            .Or(nameExpr);

        // Unary expressions
        var unaryPlus = Terms.Char('+').SkipAnd(primary)
            .Then<Expression>(expr => new UnaryExpression(UnaryOperator.Plus, expr));
        var unaryMinus = Terms.Char('-').SkipAnd(primary)
            .Then<Expression>(expr => new UnaryExpression(UnaryOperator.Minus, expr));
        var logicalNot = Terms.Char('!').SkipAnd(primary)
            .Then<Expression>(expr => new UnaryExpression(UnaryOperator.Not, expr));
        var bitwiseNot = Terms.Char('~').SkipAnd(primary)
            .Then<Expression>(expr => new UnaryExpression(UnaryOperator.BitwiseNot, expr));

        var unary = unaryPlus.Or(unaryMinus).Or(logicalNot).Or(bitwiseNot).Or(primary);

        // Switch expression: expr switch { pattern => expr, ... }
        var switchExpressionArms = Separated(COMMA, switchExpressionArm);

        var switchExpression = unary.And(SWITCH.SkipAnd(Between(LBRACE, switchExpressionArms, RBRACE)).Optional())
            .Then<Expression>(result =>
            {
                var (expr, arms) = result;
                if (arms.HasValue && arms.Value.Count != 0)
                {
                    return new SwitchExpression(expr, (IReadOnlyList<SwitchExpressionArm>)arms.Value);
                }
                return expr;
            });

        // Binary expressions with precedence
        var multiplicative = switchExpression.LeftAssociative(
            (Terms.Char('*'), (a, b) => new BinaryExpression(a, BinaryOperator.Multiply, b)),
            (Terms.Char('/'), (a, b) => new BinaryExpression(a, BinaryOperator.Divide, b)),
            (Terms.Char('%'), (a, b) => new BinaryExpression(a, BinaryOperator.Modulo, b))
        );

        var additive = multiplicative.LeftAssociative(
            (Terms.Char('+'), (a, b) => new BinaryExpression(a, BinaryOperator.Add, b)),
            (Terms.Char('-'), (a, b) => new BinaryExpression(a, BinaryOperator.Subtract, b))
        );

        var shift = additive.LeftAssociative(
            (Terms.Text("<<"), (a, b) => new BinaryExpression(a, BinaryOperator.LeftShift, b)),
            (Terms.Text(">>"), (a, b) => new BinaryExpression(a, BinaryOperator.RightShift, b))
        );

        relational = shift.LeftAssociative(
            (Terms.Text("<="), (a, b) => new BinaryExpression(a, BinaryOperator.LessThanOrEqual, b)),
            (Terms.Text(">="), (a, b) => new BinaryExpression(a, BinaryOperator.GreaterThanOrEqual, b)),
            (Terms.Text("<"), (a, b) => new BinaryExpression(a, BinaryOperator.LessThan, b)),
            (Terms.Text(">"), (a, b) => new BinaryExpression(a, BinaryOperator.GreaterThan, b))
        );

        // isExpression (relational [is pattern]) is defined in Patterns.cs
        var equality = isExpression.LeftAssociative(
            (Terms.Text("=="), (a, b) => new BinaryExpression(a, BinaryOperator.Equal, b)),
            (Terms.Text("!="), (a, b) => new BinaryExpression(a, BinaryOperator.NotEqual, b))
        );

        var bitwiseAnd = equality.LeftAssociative(
            (Terms.Char('&'), (a, b) => new BinaryExpression(a, BinaryOperator.BitwiseAnd, b))
        );

        var bitwiseXor = bitwiseAnd.LeftAssociative(
            (Terms.Char('^'), (a, b) => new BinaryExpression(a, BinaryOperator.BitwiseXor, b))
        );

        var bitwiseOr = bitwiseXor.LeftAssociative(
            (Terms.Char('|'), (a, b) => new BinaryExpression(a, BinaryOperator.BitwiseOr, b))
        );

        var logicalAnd = bitwiseOr.LeftAssociative(
            (Terms.Text("&&"), (a, b) => new BinaryExpression(a, BinaryOperator.And, b))
        );

        var logicalOr = logicalAnd.LeftAssociative(
            (Terms.Text("||"), (a, b) => new BinaryExpression(a, BinaryOperator.Or, b))
        );

        // Conditional expression
        var conditional = logicalOr.And(QUESTION.SkipAnd(expression).AndSkip(COLON).And(expression).Optional())
            .Then<Expression>(result =>
            {
                var (condition, rest) = result;
                if (rest.HasValue)
                {
                    var (trueExpr, falseExpr) = rest.Value;
                    return new ConditionalExpression(condition, trueExpr, falseExpr);
                }
                return condition;
            });

        // Assignment
        assignment = conditional.And(EQ.SkipAnd(expression).Optional())
            .Then<Expression>(result =>
            {
                var (left, right) = result;
                if (right.HasValue)
                {
                    return new BinaryExpression(left, BinaryOperator.Assign, right.Value);
                }
                return left;
            });
    }

    private static void InitializeLambdasAndQueries()
    {
        // ========================================
        // Lambda Expressions
        // ========================================

        // Lambda parameter (explicit with type or implicit without type)
        var explicitLambdaParam = parameterModifier.Else(ParameterModifier.None)
            .And(typeReference)
            .And(anyIdentifier)
            .Then(result =>
            {
                var (modifier, type, name) = result;
                return new Parameter(type, name, modifier, null);
            });

        var implicitLambdaParam = anyIdentifier
            .Then(name => new Parameter(null, name, ParameterModifier.None, null));

        // Single parameter without parentheses: x => x * x
        var singleImplicitParam = implicitLambdaParam;

        // Multiple parameters with parentheses: (x, y) => x + y  or  (int x, int y) => x + y
        // Try implicit param first (simpler pattern) to avoid consuming input with explicit param
        var lambdaParamList = Separated(COMMA, implicitLambdaParam.Or(explicitLambdaParam));
        var multipleParams = Between(LPAREN, lambdaParamList.Else([]), RPAREN);

        // Lambda with parenthesized parameters: (x, y) => ...  or  () => ...
        var lambdaWithParens = ASYNC.Optional()
            .And(multipleParams)
            .AndSkip(ARROW)
            .And(lambdaBody)
            .Then<Expression>(result =>
            {
                var (isAsync, parameters, body) = result;
                return new LambdaExpression(body, parameters, isAsync.HasValue);
            });

        // Lambda with single parameter: x => ...  or  async x => ...
        var lambdaWithSingleParam = ASYNC.Optional()
            .And(singleImplicitParam)
            .AndSkip(ARROW)
            .And(lambdaBody)
            .Then<Expression>(result =>
            {
                var (isAsync, parameter, body) = result;
                return new LambdaExpression(body, [parameter], isAsync.HasValue);
            });

        // Try parenthesized first (more specific), then single parameter
        var lambda = lambdaWithParens.Or(lambdaWithSingleParam);

        // ========================================
        // LINQ Query Expressions
        // ========================================

        // From clause: from x in collection  or  from int x in collection
        var fromClause = FROM.SkipAnd(typeReference.Optional())
            .And(anyIdentifier)
            .AndSkip(IN)
            .And(assignment)
            .Then(result =>
            {
                var (type, identifier, expr) = result;
                return new FromClause(identifier, expr, type.OrSome(null));
            });

        // Where clause: where condition
        var whereClause = WHERE_KW.SkipAnd(assignment)
            .Then<QueryClause>(expr => new WhereClause(expr));

        // Let clause: let x = expression
        var letClause = LET.SkipAnd(anyIdentifier).AndSkip(EQ).And(assignment)
            .Then<QueryClause>(result =>
            {
                var (identifier, expr) = result;
                return new LetClause(identifier, expr);
            });

        // Ordering: expression [ascending | descending]
        var ordering = assignment.And(DESCENDING.Then(OrderDirection.Descending)
                .Or(ASCENDING.Then(OrderDirection.Ascending))
                .Optional())
            .Then(result =>
            {
                var (expr, direction) = result;
                return new Ordering(expr, direction.HasValue ? direction.Value : OrderDirection.Ascending);
            });

        var orderings = Separated(COMMA, ordering);

        // OrderBy clause: orderby expression [, expression]
        var orderByClause = ORDERBY.SkipAnd(orderings)
            .Then<QueryClause>(orders => new OrderByClause(orders));

        // Join clause: join x in collection on expr1 equals expr2 [into identifier]
        var joinClause = JOIN.SkipAnd(typeReference.Optional())
            .And(anyIdentifier)
            .AndSkip(IN)
            .And(assignment)
            .AndSkip(ON)
            .And(assignment)
            .AndSkip(EQUALS)
            .And(assignment)
            .And(INTO.SkipAnd(anyIdentifier).Optional())
            .Then<QueryClause>(result =>
            {
                var (type, identifier, collection, leftExpr, rightExpr, into) = result;
                return new JoinClause(
                    identifier,
                    collection,
                    leftExpr,
                    rightExpr,
                    type.OrSome(null),
                    into.OrSome(null)
                );
            });

        // Additional from clause in body
        var fromBodyClause = fromClause.Then<QueryClause>(f => f);

        // Query body clauses
        var queryBodyClause = fromBodyClause
            .Or(letClause)
            .Or(whereClause)
            .Or(joinClause)
            .Or(orderByClause);

        var queryBodyClauses = ZeroOrMany(queryBodyClause);

        // Select clause: select expression
        var selectClause = SELECT.SkipAnd(assignment)
            .Then<SelectOrGroupClause>(expr => new SelectClause(expr));

        // Group clause: group expression by expression
        var groupClause = GROUP.SkipAnd(assignment).AndSkip(BY).And(assignment)
            .Then<SelectOrGroupClause>(result =>
            {
                var (groupExpr, byExpr) = result;
                return new GroupClause(groupExpr, byExpr);
            });

        var selectOrGroupClause = selectClause.Or(groupClause);

        // Query continuation: into identifier queryBody
        var queryContinuation = Deferred<QueryContinuation>();

        var queryBodyWithContinuation = queryBodyClauses.And(selectOrGroupClause).And(queryContinuation.Optional())
            .Then(result =>
            {
                var (clauses, selectOrGroup, continuation) = result;
                return (clauses, selectOrGroup, continuation.OrSome(null));
            });

        queryContinuation.Parser = INTO.SkipAnd(anyIdentifier).And(queryBodyClauses).And(selectOrGroupClause)
            .Then(result =>
            {
                var (identifier, clauses, selectOrGroup) = result;
                return new QueryContinuation(identifier, clauses, selectOrGroup);
            });

        // Query expression: from ... [where/let/orderby/join] ... select/group [into]
        var queryExpression = fromClause.And(queryBodyWithContinuation)
            .Then<Expression>(result =>
            {
                var (from, body) = result;
                var (bodyClauses, selectOrGroup, continuation) = body;

                if (continuation != null)
                {
                    // Add continuation as additional body clauses
                    var allClauses = bodyClauses.ToList();
                    allClauses.AddRange(continuation.BodyClauses);
                    return new QueryExpression(from, allClauses, continuation.SelectOrGroupClause);
                }

                return new QueryExpression(from, bodyClauses, selectOrGroup);
            });

        // Expression with lambda and query support
        expression.Parser = queryExpression.Or(lambda).Or(assignment);

        // Lambda body uses assignment rather than expression to avoid infinite recursion
        var lambdaExprBody = assignment
            .Then<LambdaBody>(expr => new ExpressionLambdaBody(expr));

        var lambdaBlockBody = block
            .Then<LambdaBody>(b => new BlockLambdaBody(b));

        lambdaBody.Parser = lambdaBlockBody.Or(lambdaExprBody);
    }
}
