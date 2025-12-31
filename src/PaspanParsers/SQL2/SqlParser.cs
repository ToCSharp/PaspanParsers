using Paspan;
using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.SQL2;

/// <summary>
/// Parser result wrapper for compatibility with tests.
/// </summary>
public class ParseResult<T>(T value, bool success, ParseError error)
{
    public T Value { get; } = value;
    public bool Success { get; } = success;
    public ParseError Error { get; } = error;
}

/// <summary>
/// SQL parser implementation using Parlot Fluent API.
/// Supports standard SQL (SQL-92/SQL:2003) syntax.
/// This is an educational implementation - for production use, consider specialized SQL parsers.
/// </summary>
public class SqlParser
{
    public static readonly Parser<SqlScript> ScriptParser;
    public static readonly Parser<SqlStatement> StatementParser;
    public static readonly Parser<SqlExpression> ExpressionParser;

    static SqlParser()
    {
        // ========================================
        // Keywords
        // ========================================

        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "FROM", "WHERE", "INSERT", "INTO", "VALUES", "UPDATE", "SET",
            "DELETE", "CREATE", "TABLE", "DROP", "ALTER", "INDEX", "ON",
            "AND", "OR", "NOT", "NULL", "TRUE", "FALSE", "IS", "IN", "LIKE",
            "BETWEEN", "CASE", "WHEN", "THEN", "ELSE", "END", "AS",
            "JOIN", "INNER", "LEFT", "RIGHT", "FULL", "CROSS", "OUTER",
            "GROUP", "BY", "HAVING", "ORDER", "ASC", "DESC",
            "LIMIT", "OFFSET", "DISTINCT", "ALL",
            "PRIMARY", "KEY", "FOREIGN", "REFERENCES", "UNIQUE", "CHECK",
            "DEFAULT", "AUTO_INCREMENT", "AUTOINCREMENT", "NOT", "IF", "EXISTS",
            "INT", "INTEGER", "VARCHAR", "CHAR", "TEXT", "BOOLEAN", "BOOL",
            "DATE", "TIME", "DATETIME", "TIMESTAMP", "DECIMAL", "NUMERIC",
            "FLOAT", "DOUBLE", "REAL", "SMALLINT", "BIGINT",
            "COUNT", "SUM", "AVG", "MIN", "MAX", "CAST"
        };

        // ========================================
        // Helper Functions
        // ========================================

        bool IsKeyword(string text) => keywords.Contains(text);

        static SqlBinaryOperator ParseBinaryOp(string op) => op.ToUpperInvariant() switch
        {
            "+" => SqlBinaryOperator.Add,
            "-" => SqlBinaryOperator.Subtract,
            "*" => SqlBinaryOperator.Multiply,
            "/" => SqlBinaryOperator.Divide,
            "%" => SqlBinaryOperator.Modulo,
            "=" => SqlBinaryOperator.Equal,
            "<>" => SqlBinaryOperator.NotEqual,
            "!=" => SqlBinaryOperator.NotEqual,
            "<" => SqlBinaryOperator.LessThan,
            "<=" => SqlBinaryOperator.LessThanOrEqual,
            ">" => SqlBinaryOperator.GreaterThan,
            ">=" => SqlBinaryOperator.GreaterThanOrEqual,
            "AND" => SqlBinaryOperator.And,
            "OR" => SqlBinaryOperator.Or,
            "LIKE" => SqlBinaryOperator.Like,
            "IN" => SqlBinaryOperator.In,
            _ => SqlBinaryOperator.Equal
        };

        // ========================================
        // Basic Terminals
        // ========================================

        var LPAREN = Terms.Char('(');
        var RPAREN = Terms.Char(')');
        var COMMA = Terms.Char(',');
        var SEMICOLON = Terms.Char(';');
        var DOT = Terms.Char('.');
        var STAR = Terms.Char('*');

        // ========================================
        // Keywords (case-insensitive)
        // ========================================

        var SELECT = Terms.Text("SELECT", caseInsensitive: true);
        var FROM = Terms.Text("FROM", caseInsensitive: true);
        var WHERE = Terms.Text("WHERE", caseInsensitive: true);
        var INSERT = Terms.Text("INSERT", caseInsensitive: true);
        var INTO = Terms.Text("INTO", caseInsensitive: true);
        var VALUES = Terms.Text("VALUES", caseInsensitive: true);
        var UPDATE = Terms.Text("UPDATE", caseInsensitive: true);
        var SET = Terms.Text("SET", caseInsensitive: true);
        var DELETE = Terms.Text("DELETE", caseInsensitive: true);
        var CREATE = Terms.Text("CREATE", caseInsensitive: true);
        var TABLE = Terms.Text("TABLE", caseInsensitive: true);
        var DROP = Terms.Text("DROP", caseInsensitive: true);
        var INDEX = Terms.Text("INDEX", caseInsensitive: true);
        var ON = Terms.Text("ON", caseInsensitive: true);
        var AND = Terms.Text("AND", caseInsensitive: true);
        var OR = Terms.Text("OR", caseInsensitive: true);
        var NOT = Terms.Text("NOT", caseInsensitive: true);
        var NULL = Terms.Text("NULL", caseInsensitive: true);
        var TRUE = Terms.Text("TRUE", caseInsensitive: true);
        var FALSE = Terms.Text("FALSE", caseInsensitive: true);
        var IS = Terms.Text("IS", caseInsensitive: true);
        var IN = Terms.Text("IN", caseInsensitive: true);
        var LIKE = Terms.Text("LIKE", caseInsensitive: true);
        var BETWEEN = Terms.Text("BETWEEN", caseInsensitive: true);
        var CASE = Terms.Text("CASE", caseInsensitive: true);
        var WHEN = Terms.Text("WHEN", caseInsensitive: true);
        var THEN = Terms.Text("THEN", caseInsensitive: true);
        var ELSE = Terms.Text("ELSE", caseInsensitive: true);
        var END = Terms.Text("END", caseInsensitive: true);
        var AS = Terms.Text("AS", caseInsensitive: true);
        var JOIN = Terms.Text("JOIN", caseInsensitive: true);
        var INNER = Terms.Text("INNER", caseInsensitive: true);
        var LEFT = Terms.Text("LEFT", caseInsensitive: true);
        var RIGHT = Terms.Text("RIGHT", caseInsensitive: true);
        var FULL = Terms.Text("FULL", caseInsensitive: true);
        var CROSS = Terms.Text("CROSS", caseInsensitive: true);
        var OUTER = Terms.Text("OUTER", caseInsensitive: true);
        var GROUP = Terms.Text("GROUP", caseInsensitive: true);
        var BY = Terms.Text("BY", caseInsensitive: true);
        var HAVING = Terms.Text("HAVING", caseInsensitive: true);
        var ORDER = Terms.Text("ORDER", caseInsensitive: true);
        var ASC = Terms.Text("ASC", caseInsensitive: true);
        var DESC = Terms.Text("DESC", caseInsensitive: true);
        var LIMIT = Terms.Text("LIMIT", caseInsensitive: true);
        var OFFSET = Terms.Text("OFFSET", caseInsensitive: true);
        var DISTINCT = Terms.Text("DISTINCT", caseInsensitive: true);
        var ALL = Terms.Text("ALL", caseInsensitive: true);
        var PRIMARY = Terms.Text("PRIMARY", caseInsensitive: true);
        var KEY = Terms.Text("KEY", caseInsensitive: true);
        var FOREIGN = Terms.Text("FOREIGN", caseInsensitive: true);
        var REFERENCES = Terms.Text("REFERENCES", caseInsensitive: true);
        var UNIQUE = Terms.Text("UNIQUE", caseInsensitive: true);
        var DEFAULT = Terms.Text("DEFAULT", caseInsensitive: true);
        var AUTO_INCREMENT = Terms.Text("AUTO_INCREMENT", caseInsensitive: true)
            .Or(Terms.Text("AUTOINCREMENT", caseInsensitive: true));
        var IF = Terms.Text("IF", caseInsensitive: true);
        var EXISTS = Terms.Text("EXISTS", caseInsensitive: true);
        var CAST = Terms.Text("CAST", caseInsensitive: true);

        // ========================================
        // Literals
        // ========================================

        // Integer
        var integer = Terms.Integer()
            .Then<SqlExpression>(static x => new LiteralExpression((int)x, SqlLiteralKind.Integer));

        // Decimal/Float
        var floatLiteral = Terms.Decimal()
            .Then<SqlExpression>(static x => new LiteralExpression((double)x, SqlLiteralKind.Float));

        // String (single quotes in SQL)
        var stringLiteral = Between(
            Terms.Char('\''),
            ZeroOrMany(Terms.Char('\'').AndSkip(Terms.Char('\'')).Then(static c => c.ToString())
                .Or(AnyCharBefore(Terms.Char('\''), consumeDelimiter: false)
                    .Then(static c => c.ToString())))
                .Then(static parts => string.Concat(parts)),
            Terms.Char('\''))
            .Then<SqlExpression>(static s => new LiteralExpression(s, SqlLiteralKind.String));

        // Boolean
        var booleanLiteral = TRUE.Then<SqlExpression>(static _ => new LiteralExpression(true, SqlLiteralKind.Boolean))
            .Or(FALSE.Then<SqlExpression>(static _ => new LiteralExpression(false, SqlLiteralKind.Boolean)));

        // Null
        var nullLiteral = NULL.Then<SqlExpression>(static _ => new LiteralExpression(null, SqlLiteralKind.Null));

        // ========================================
        // Identifiers
        // ========================================

        // Regular identifier or quoted identifier
        var plainIdentifier = Terms.Identifier().Then(static x => x.ToString());
        var identifier = Between(
            Terms.Char('"'),
            ZeroOrMany(Terms.Char('"').AndSkip(Terms.Char('"'))
            .Then(static c => c.ToString())
                    .Or(AnyCharBefore(Terms.Char('"'), consumeDelimiter: false)
                        .Then(static c => c.ToString())))
                    .Then(static parts => string.Concat(parts)),
            Terms.Char('"')
            )
            .Or(Between(
                Terms.Char('['),
                ZeroOrMany(AnyCharBefore(Terms.Char(']'), consumeDelimiter: false)
                    .Then(static c => c.ToString()))
                    .Then(static parts => string.Concat(parts)),
                Terms.Char(']'))
            )
            .Or(Between(
                Terms.Char('`'),
                ZeroOrMany(AnyCharBefore(Terms.Char('`'), consumeDelimiter: false)
                    .Then(static c => c.ToString()))
                    .Then(static parts => string.Concat(parts)),
                Terms.Char('`'))
            )
            .Or<string>(plainIdentifier)
            .Then(name => !IsKeyword(name) ? name : null)
            .When(name => name != null);

        // ========================================
        // Data Types
        // ========================================

        var dataType = Deferred<SqlDataType>();

        var dataTypeName = Terms.Text("INT", caseInsensitive: true)
            .Or(Terms.Text("INTEGER", caseInsensitive: true))
            .Or(Terms.Text("VARCHAR", caseInsensitive: true))
            .Or(Terms.Text("CHAR", caseInsensitive: true))
            .Or(Terms.Text("TEXT", caseInsensitive: true))
            .Or(Terms.Text("BOOLEAN", caseInsensitive: true))
            .Or(Terms.Text("BOOL", caseInsensitive: true))
            .Or(Terms.Text("DATE", caseInsensitive: true))
            .Or(Terms.Text("TIME", caseInsensitive: true))
            .Or(Terms.Text("DATETIME", caseInsensitive: true))
            .Or(Terms.Text("TIMESTAMP", caseInsensitive: true))
            .Or(Terms.Text("DECIMAL", caseInsensitive: true))
            .Or(Terms.Text("NUMERIC", caseInsensitive: true))
            .Or(Terms.Text("FLOAT", caseInsensitive: true))
            .Or(Terms.Text("DOUBLE", caseInsensitive: true))
            .Or(Terms.Text("REAL", caseInsensitive: true))
            .Or(Terms.Text("SMALLINT", caseInsensitive: true))
            .Or(Terms.Text("BIGINT", caseInsensitive: true));

        dataType.Parser = dataTypeName.And(
            LPAREN.SkipAnd(Terms.Integer())
                .And(COMMA.SkipAnd(Terms.Integer()).Optional())
                .AndSkip(RPAREN)
                .Optional())
            .Then(static result =>
            {
                var (typeName, sizeInfo) = result;
                if (sizeInfo.HasValue)
                {
                    var (len, scale) = sizeInfo.Value;
                    return new SqlDataType(typeName, (int)len,
                        scale.HasValue ? (int?)scale.Value : null, null);
                }
                return new SqlDataType(typeName);
            });

        // ========================================
        // Expressions
        // ========================================

        var expression = Deferred<SqlExpression>();

        // Column reference: [table.]column
        var columnReference = identifier.And(DOT.SkipAnd(identifier).Optional())
            .Then<SqlExpression>(static result =>
            {
                var (first, second) = result;
                if (second.HasValue)
                {
                    return new ColumnReference(second.Value, first);
                }
                return new ColumnReference(first);
            });

        // All columns: * or table.*
        var allColumns = identifier.And(DOT).AndSkip(STAR)
            .Then<SqlExpression>(static result => new AllColumnsExpression(result.Item1))
            .Or(STAR.Then<SqlExpression>(static _ => new AllColumnsExpression()));

        // Function name (allows keywords for function names like COUNT, SUM, etc.)
        var functionName = Between(
            Terms.Char('"'),
            ZeroOrMany(Terms.Char('"').AndSkip(Terms.Char('"'))
            .Then(static c => c.ToString())
                    .Or(AnyCharBefore(Terms.Char('"'), consumeDelimiter: false)
                        .Then(static c => c.ToString())))
                    .Then(static parts => string.Concat(parts)),
            Terms.Char('"')
            )
            .Or(Between(
                Terms.Char('['),
                ZeroOrMany(AnyCharBefore(Terms.Char(']'), consumeDelimiter: false)
                    .Then(static c => c.ToString()))
                    .Then(static parts => string.Concat(parts)),
                Terms.Char(']'))
            )
            .Or(Between(
                Terms.Char('`'),
                ZeroOrMany(AnyCharBefore(Terms.Char('`'), consumeDelimiter: false)
                    .Then(static c => c.ToString()))
                    .Then(static parts => string.Concat(parts)),
                Terms.Char('`'))
            )
            .Or(plainIdentifier);

        // Function call: FUNC(args)
        var functionCall = functionName.And(
            LPAREN.SkipAnd(
                DISTINCT.SkipAnd(Separated(COMMA, expression))
                    .Then(static args => (true, args))
                .Or(Separated(COMMA, expression)
                    .Then(static args => (false, args))))
            .AndSkip(RPAREN))
            .Then<SqlExpression>(static result =>
            {
                var (name, argsInfo) = result;
                var (distinct, args) = argsInfo;
                return new FunctionCall(name, [.. args], distinct);
            });

        // CASE expression
        var whenClause = WHEN.SkipAnd(expression)
            .And(THEN.SkipAnd(expression))
            .Then(static result => new WhenClause(result.Item1, result.Item2));

        var caseExpression = CASE.SkipAnd(expression.Optional())
            .And(OneOrMany(whenClause))
            .And(ELSE.SkipAnd(expression).Optional())
            .AndSkip(END)
            .Then<SqlExpression>(static result =>
            {
                var testExpr = result.Item1;
                var whenClauses = result.Item2;
                var elseExpr = result.Item3;
                return new CaseExpression(testExpr.HasValue ? testExpr.Value : null,
                    [.. whenClauses], elseExpr.HasValue ? elseExpr.Value : null);
            });

        // CAST expression
        var castExpression = CAST.SkipAnd(LPAREN)
            .SkipAnd(expression)
            .AndSkip(AS)
            .And(dataType)
            .AndSkip(RPAREN)
            .Then<SqlExpression>(static result => new CastExpression(result.Item1, result.Item2));

        // Primary expression
        var primaryExpression = integer
            .Or(floatLiteral)
            .Or(stringLiteral)
            .Or(booleanLiteral)
            .Or(nullLiteral)
            .Or(caseExpression)
            .Or(castExpression)
            .Or(functionCall)
            .Or(allColumns)
            .Or(columnReference)
            .Or(Between(LPAREN, expression, RPAREN));

        // Unary operators
        var unaryExpression = NOT.SkipAnd(primaryExpression)
            .Then<SqlExpression>(static expr => new UnaryExpression(SqlUnaryOperator.Not, expr))
            .Or(Terms.Char('-').SkipAnd(primaryExpression)
                .Then<SqlExpression>(static expr => new UnaryExpression(SqlUnaryOperator.Minus, expr)))
            .Or(Terms.Char('+').SkipAnd(primaryExpression)
                .Then<SqlExpression>(static expr => new UnaryExpression(SqlUnaryOperator.Plus, expr)))
            .Or(primaryExpression);

        // IS NULL / IS NOT NULL
        var isNullExpression = unaryExpression.And(
            IS.SkipAnd(NOT).SkipAnd(NULL).Then(static _ => SqlUnaryOperator.IsNotNull)
                .Or(IS.SkipAnd(NULL).Then(static _ => SqlUnaryOperator.IsNull))
                .Optional())
            .Then<SqlExpression>(static result =>
            {
                var (expr, op) = result;
                if (op.HasValue)
                {
                    return new UnaryExpression(op.Value, expr);
                }
                return expr;
            });

        // BETWEEN
        var betweenExpression = isNullExpression.And(
            NOT.SkipAnd(BETWEEN).SkipAnd(isNullExpression).And(AND.SkipAnd(isNullExpression))
                .Then(static result => (true, result.Item1, result.Item2))
            .Or(BETWEEN.SkipAnd(isNullExpression).And(AND.SkipAnd(isNullExpression))
                .Then(static result => (false, result.Item1, result.Item2)))
            .Optional())
            .Then<SqlExpression>(static result =>
            {
                var (expr, betweenInfo) = result;
                if (betweenInfo.HasValue)
                {
                    var (notBetween, lower, upper) = betweenInfo.Value;
                    return new BetweenExpression(expr, lower, upper, notBetween);
                }
                return expr;
            });

        // IN operator
        var inExpression = betweenExpression.And(
            NOT.SkipAnd(IN).SkipAnd(LPAREN)
                .SkipAnd(Separated(COMMA, expression))
                .AndSkip(RPAREN)
                .Then<SqlExpression>(static items => new ListExpression([.. items]))
                .Then(static list => (true, list))
            .Or(IN.SkipAnd(LPAREN)
                .SkipAnd(Separated(COMMA, expression))
                .AndSkip(RPAREN)
                .Then<SqlExpression>(static items => new ListExpression([.. items]))
                .Then(static list => (false, list)))
            .Optional())
            .Then<SqlExpression>(static result =>
            {
                var (expr, inInfo) = result;
                if (inInfo.HasValue)
                {
                    var (notIn, list) = inInfo.Value;
                    return new BinaryExpression(expr,
                        notIn ? SqlBinaryOperator.NotIn : SqlBinaryOperator.In, list);
                }
                return expr;
            });

        // LIKE operator
        var likeExpression = inExpression.And(
            NOT.SkipAnd(LIKE).SkipAnd(inExpression)
                .Then(static right => (SqlBinaryOperator.NotLike, right))
            .Or(LIKE.SkipAnd(inExpression)
                .Then(static right => (SqlBinaryOperator.Like, right)))
            .Optional())
            .Then<SqlExpression>(static result =>
            {
                var (left, opRight) = result;
                if (opRight.HasValue)
                {
                    var (op, right) = opRight.Value;
                    return new BinaryExpression(left, op, right);
                }
                return left;
            });

        // Multiplicative operators
        var multiplicativeExpression = likeExpression.And(
            ZeroOrMany(
                Terms.Char('*').Then(static _ => "*")
                    .Or(Terms.Char('/').Then(static _ => "/"))
                    .Or(Terms.Char('%').Then(static _ => "%"))
                    .And(likeExpression)))
            .Then<SqlExpression>(result =>
            {
                var (first, rest) = result;
                var expr = first;
                foreach (var (op, right) in rest)
                {
                    expr = new BinaryExpression(expr, ParseBinaryOp(op), right);
                }
                return expr;
            });

        // Additive operators
        var additiveExpression = multiplicativeExpression.And(
            ZeroOrMany(
                Terms.Char('+').Then(static _ => "+")
                    .Or(Terms.Char('-').Then(static _ => "-"))
                    .And(multiplicativeExpression)))
            .Then<SqlExpression>(result =>
            {
                var (first, rest) = result;
                var expr = first;
                foreach (var (op, right) in rest)
                {
                    expr = new BinaryExpression(expr, ParseBinaryOp(op), right);
                }
                return expr;
            });

        // Comparison operators
        var comparisonExpression = additiveExpression.And(
            Terms.Text("<=").Then(static _ => "<=")
                .Or(Terms.Text(">=").Then(static _ => ">="))
                .Or(Terms.Text("<>").Then(static _ => "<>"))
                .Or(Terms.Text("!=").Then(static _ => "!="))
                .Or(Terms.Char('<').Then(static _ => "<"))
                .Or(Terms.Char('>').Then(static _ => ">"))
                .Or(Terms.Char('=').Then(static _ => "="))
                .And(additiveExpression)
                .Optional())
            .Then<SqlExpression>(result =>
            {
                var (left, opRight) = result;
                if (opRight.HasValue)
                {
                    var (op, right) = opRight.Value;
                    return new BinaryExpression(left, ParseBinaryOp(op), right);
                }
                return left;
            });

        // AND operator
        var andExpression = comparisonExpression.And(
            ZeroOrMany(AND.SkipAnd(comparisonExpression)))
            .Then<SqlExpression>(static result =>
            {
                var (first, rest) = result;
                var expr = first;
                foreach (var right in rest)
                {
                    expr = new BinaryExpression(expr, SqlBinaryOperator.And, right);
                }
                return expr;
            });

        // OR operator
        var orExpression = andExpression.And(
            ZeroOrMany(OR.SkipAnd(andExpression)))
            .Then<SqlExpression>(static result =>
            {
                var (first, rest) = result;
                var expr = first;
                foreach (var right in rest)
                {
                    expr = new BinaryExpression(expr, SqlBinaryOperator.Or, right);
                }
                return expr;
            });

        expression.Parser = orExpression;

        // ========================================
        // SELECT Statement
        // ========================================

        var selectColumn = expression.And(AS.SkipAnd(identifier).Optional())
            .Then(static result => new SelectColumn(result.Item1,
                result.Item2.HasValue ? result.Item2.Value : null));

        var tableReference = Deferred<TableReference>();

        var tableName = identifier.And(AS.SkipAnd(identifier).Optional())
            .Then<TableReference>(static result => new TableName(result.Item1,
                result.Item2.HasValue ? result.Item2.Value : null));

        var joinType = INNER.SkipAnd(JOIN).Then(static _ => JoinType.Inner)
            .Or(LEFT.And(OUTER.Optional()).SkipAnd(JOIN).Then(static _ => JoinType.Left))
            .Or(RIGHT.And(OUTER.Optional()).SkipAnd(JOIN).Then(static _ => JoinType.Right))
            .Or(FULL.And(OUTER.Optional()).SkipAnd(JOIN).Then(static _ => JoinType.Full))
            .Or(CROSS.SkipAnd(JOIN).Then(static _ => JoinType.Cross))
            .Or(JOIN.Then(static _ => JoinType.Inner));

        var joinClause = tableName.And(
            ZeroOrMany(joinType.And(tableName).And(ON.SkipAnd(expression))))
            .Then<TableReference>(static result =>
            {
                var (first, joins) = result;
                TableReference current = first;
                foreach (var (jType, right, condition) in joins)
                {
                    current = new JoinClause(current, right, jType, condition);
                }
                return current;
            });

        tableReference.Parser = joinClause;

        var orderByClause = expression.And(DESC.Then(static _ => false).Or(ASC.Then(static _ => true)).Optional())
            .Then(static result => new OrderByClause(result.Item1,
                !result.Item2.HasValue || result.Item2.Value));

        var selectStatement = SELECT.SkipAnd(DISTINCT.Then(static _ => true).Optional())
            .And(Separated(COMMA, selectColumn))
            .And(FROM.SkipAnd(Separated(COMMA, tableReference)).Optional())
            .And(WHERE.SkipAnd(expression).Optional())
            .And(GROUP.SkipAnd(BY).SkipAnd(Separated(COMMA, expression)).Optional())
            .And(HAVING.SkipAnd(expression).Optional())
            .And(ORDER.SkipAnd(BY).SkipAnd(Separated(COMMA, orderByClause)).Optional())
            .And(LIMIT.SkipAnd(Terms.Integer()).Optional())
            .And(OFFSET.SkipAnd(Terms.Integer()).Optional())
            .Then<SqlStatement>(static result =>
            {
                var distinct = result.Item1.Value;
                IReadOnlyList<SelectColumn> columns = result.Item2?.ToArray();
                IReadOnlyList<TableReference> from = result.Item3.Value?.ToArray();
                var where = result.Item4.Value;
                IReadOnlyList<SqlExpression> groupBy = result.Item5.Value?.ToArray();
                var having = result.Item6.Value;
                IReadOnlyList<OrderByClause> orderBy = result.Item7.Value?.ToArray();
                var limit = (int?)result.Item8.Value;
                var offset = (int?)result.Item9.Value;

                return new SelectStatement(distinct, columns, from, where, groupBy, having, orderBy, limit, offset);
            });

        // ========================================
        // INSERT Statement
        // ========================================

        var insertStatement = INSERT.SkipAnd(INTO).SkipAnd(identifier)
            .And(LPAREN.SkipAnd(Separated(COMMA, identifier)).AndSkip(RPAREN).Optional())
            .AndSkip(VALUES)
            .And(Separated(COMMA,
                LPAREN.SkipAnd(Separated(COMMA, expression)).AndSkip(RPAREN)))
            .Then<SqlStatement>(static result =>
            {
                var tableName = result.Item1;
                var columns = result.Item2.HasValue
                    ? [.. result.Item2.Value]
                    : Array.Empty<string>();
                var values = result.Item3.Select(v => (IReadOnlyList<SqlExpression>)[.. v]).ToArray();
                return new InsertStatement(tableName, columns, values);
            });

        // ========================================
        // UPDATE Statement
        // ========================================

        var assignment = identifier.AndSkip(Terms.Char('=')).And(expression)
            .Then(static result => new Assignment(result.Item1, result.Item2));

        var updateStatement = UPDATE.SkipAnd(identifier)
            .AndSkip(SET)
            .And(Separated(COMMA, assignment))
            .And(WHERE.SkipAnd(expression).Optional())
            .Then<SqlStatement>(static result =>
            {
                var tableName = result.Item1;
                var assignments = result.Item2.ToArray();
                var where = result.Item3.HasValue ? result.Item3.Value : null;
                return new UpdateStatement(tableName, assignments, where);
            });

        // ========================================
        // DELETE Statement
        // ========================================

        var deleteStatement = DELETE.SkipAnd(FROM).SkipAnd(identifier)
            .And(WHERE.SkipAnd(expression).Optional())
            .Then<SqlStatement>(static result => new DeleteStatement(result.Item1,
                result.Item2.HasValue ? result.Item2.Value : null));

        // ========================================
        // CREATE TABLE Statement
        // ========================================

        var columnDefinition = identifier.And(dataType)
            .And(NOT.SkipAnd(NULL).Then(static _ => false).Optional())
            .And(DEFAULT.SkipAnd(expression).Optional())
            .And(PRIMARY.SkipAnd(KEY).Then(static _ => true).Optional())
            .And(UNIQUE.Then(static _ => true).Optional())
            .And(AUTO_INCREMENT.Then(static _ => true).Optional())
            .Then(static result =>
            {
                var colName = result.Item1;
                var dataType = result.Item2;
                var notNull = result.Item3.HasValue && result.Item3.Value;
                var defaultValue = result.Item4.HasValue
                    ? result.Item4.Value
                    : null;
                var primaryKey = result.Item5.HasValue
                    && result.Item6.Value;
                var unique = result.Item6.HasValue && result.Item6.Value;
                var autoIncrement = result.Item7.HasValue && result.Item7.Value;

                return new ColumnDefinition(colName, dataType, !notNull, defaultValue,
                    primaryKey, unique, autoIncrement);
            });

        var primaryKeyConstraint = PRIMARY.SkipAnd(KEY)
            .SkipAnd(LPAREN)
            .SkipAnd(Separated(COMMA, identifier))
            .AndSkip(RPAREN)
            .Then<TableConstraint>(static cols =>
            {
                IReadOnlyList<string> columns = [.. cols];
                return new PrimaryKeyConstraint(columns);
            });

        var foreignKeyConstraint = FOREIGN.SkipAnd(KEY)
            .SkipAnd(LPAREN)
            .SkipAnd(Separated(COMMA, identifier))
            .AndSkip(RPAREN)
            .AndSkip(REFERENCES)
            .And(identifier)
            .And(LPAREN.SkipAnd(Separated(COMMA, identifier)).AndSkip(RPAREN))
            .Then<TableConstraint>(static result =>
            {
                var cols = result.Item1;
                var refTable = result.Item2;
                var refCols = result.Item3;
                IReadOnlyList<string> columns = [.. cols];
                IReadOnlyList<string> refColumns = [.. refCols];
                return new ForeignKeyConstraint(columns, refTable, refColumns);
            });

        var uniqueConstraint = UNIQUE.SkipAnd(LPAREN)
            .SkipAnd(Separated(COMMA, identifier))
            .AndSkip(RPAREN)
            .Then<TableConstraint>(static cols =>
            {
                IReadOnlyList<string> columns = [.. cols];
                return new UniqueConstraint(columns);
            });

        var tableConstraint = primaryKeyConstraint
            .Or(foreignKeyConstraint)
            .Or(uniqueConstraint);

        var createTableStatement = CREATE.SkipAnd(TABLE)
            .SkipAnd(IF.SkipAnd(NOT).SkipAnd(EXISTS).Then(static _ => true).Optional())
            .And(identifier)
            .AndSkip(LPAREN)
            .And(Separated(COMMA, columnDefinition.Then<object>(static c => c)
                .Or(tableConstraint.Then<object>(static c => c))))
            .AndSkip(RPAREN)
            .Then<SqlStatement>(static result =>
            {
                var ifNotExists = result.Item1.HasValue && result.Item1.Value;
                var tableName = result.Item2;
                var items = result.Item3.ToArray();

                var columns = items.OfType<ColumnDefinition>().ToArray();
                var constraints = items.OfType<TableConstraint>().ToArray();

                return new CreateTableStatement(tableName, columns, constraints, ifNotExists);
            });

        // ========================================
        // DROP TABLE Statement
        // ========================================

        var dropTableStatement = DROP.SkipAnd(TABLE)
            .SkipAnd(IF.SkipAnd(EXISTS).Then(static _ => true).Optional())
            .And(identifier)
            .Then<SqlStatement>(static result => new DropTableStatement(result.Item2,
                result.Item1.HasValue && result.Item1.Value));

        // ========================================
        // CREATE INDEX Statement
        // ========================================

        var createIndexStatement = CREATE
            .SkipAnd(UNIQUE.Then(static _ => true).Optional())
            .AndSkip(INDEX)
            .And(IF.SkipAnd(NOT).SkipAnd(EXISTS).Then(static _ => true).Optional())
            .And(identifier)
            .AndSkip(ON)
            .And(identifier)
            .And(LPAREN.SkipAnd(Separated(COMMA, identifier)).AndSkip(RPAREN))
            .Then<SqlStatement>(static result =>
            {
                var unique = result.Item1.HasValue
                    && result.Item1.Value;
                var ifNotExists = result.Item2.HasValue
                    && result.Item2.Value;
                var indexName = result.Item3;
                var tableName = result.Item4;
                IReadOnlyList<string> columns = [.. result.Item5];

                return new CreateIndexStatement(indexName, tableName, columns, unique, ifNotExists);
            });

        // ========================================
        // DROP INDEX Statement
        // ========================================

        var dropIndexStatement = DROP.SkipAnd(INDEX)
            .SkipAnd(IF.SkipAnd(EXISTS).Then(static _ => true).Optional())
            .And(identifier)
            .Then<SqlStatement>(static result => new DropIndexStatement(result.Item2,
                result.Item1.HasValue && result.Item1.Value));

        // ========================================
        // Statement Parser
        // ========================================

        var statement = selectStatement
            .Or(insertStatement)
            .Or(updateStatement)
            .Or(deleteStatement)
            .Or(createTableStatement)
            .Or(dropTableStatement)
            .Or(createIndexStatement)
            .Or(dropIndexStatement);

        StatementParser = statement;

        // ========================================
        // Script Parser (multiple statements)
        // ========================================

        var script = Separated(SEMICOLON, statement)
            .AndSkip(SEMICOLON.Optional())
            .Then(static stmts =>
            {
                IReadOnlyList<SqlStatement> statements = [.. stmts];
                return new SqlScript(statements);
            });

        ScriptParser = script;
        ExpressionParser = expression;
    }

    /// <summary>
    /// Parses SQL script containing multiple statements.
    /// </summary>
    public static ParseResult<SqlScript> Parse(string input)
    {
        if (ScriptParser.TryParse(input, out var result, out var error))
        {
            return new ParseResult<SqlScript>(result, true, null);
        }
        return new ParseResult<SqlScript>(default, false, error);
    }

    /// <summary>
    /// Parses a single SQL statement.
    /// </summary>
    public static ParseResult<SqlStatement> ParseStatement(string input)
    {
        if (StatementParser.TryParse(input, out var result, out var error))
        {
            return new ParseResult<SqlStatement>(result, true, null);
        }
        return new ParseResult<SqlStatement>(default, false, error);
    }

    /// <summary>
    /// Parses a SQL expression.
    /// </summary>
    public static ParseResult<SqlExpression> ParseExpression(string input)
    {
        if (ExpressionParser.TryParse(input, out var result, out var error))
        {
            return new ParseResult<SqlExpression>(result, true, null);
        }
        return new ParseResult<SqlExpression>(default, false, error);
    }
}

