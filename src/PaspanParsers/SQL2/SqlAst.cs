namespace PaspanParsers.SQL2;

/// <summary>
/// Abstract Syntax Tree (AST) nodes for SQL language
/// Supports standard SQL (SQL-92/SQL:2003) syntax
/// </summary>

// ========================================
// Base Interfaces
// ========================================

public interface ISqlNode
{
    // Marker interface for all SQL AST nodes
}

// ========================================
// Root Node
// ========================================

public sealed class SqlScript(IReadOnlyList<SqlStatement> statements) : ISqlNode
{
    public IReadOnlyList<SqlStatement> Statements { get; } = statements ?? [];
}

// ========================================
// Statements
// ========================================

public abstract class SqlStatement : ISqlNode
{
}

// SELECT Statement
public sealed class SelectStatement(
    bool distinct,
    IReadOnlyList<SelectColumn> columns,
    IReadOnlyList<TableReference> from,
    SqlExpression where = null,
    IReadOnlyList<SqlExpression> groupBy = null,
    SqlExpression having = null,
    IReadOnlyList<OrderByClause> orderBy = null,
    int? limit = null,
    int? offset = null) : SqlStatement
{
    public bool Distinct { get; } = distinct;
    public IReadOnlyList<SelectColumn> Columns { get; } = columns;
    public IReadOnlyList<TableReference> From { get; } = from;
    public SqlExpression Where { get; } = where;
    public IReadOnlyList<SqlExpression> GroupBy { get; } = groupBy;
    public SqlExpression Having { get; } = having;
    public IReadOnlyList<OrderByClause> OrderBy { get; } = orderBy;
    public int? Limit { get; } = limit;
    public int? Offset { get; } = offset;
}

public sealed class SelectColumn(SqlExpression expression, string alias = null) : ISqlNode
{
    public SqlExpression Expression { get; } = expression;
    public string Alias { get; } = alias;
}

public sealed class OrderByClause(SqlExpression expression, bool ascending = true) : ISqlNode
{
    public SqlExpression Expression { get; } = expression;
    public bool Ascending { get; } = ascending;
}

// INSERT Statement
public sealed class InsertStatement(
    string tableName,
    IReadOnlyList<string> columns,
    IReadOnlyList<IReadOnlyList<SqlExpression>> values) : SqlStatement
{
    public string TableName { get; } = tableName;
    public IReadOnlyList<string> Columns { get; } = columns;
    public IReadOnlyList<IReadOnlyList<SqlExpression>> Values { get; } = values;
}

// UPDATE Statement
public sealed class UpdateStatement(
    string tableName,
    IReadOnlyList<Assignment> assignments,
    SqlExpression where = null) : SqlStatement
{
    public string TableName { get; } = tableName;
    public IReadOnlyList<Assignment> Assignments { get; } = assignments;
    public SqlExpression Where { get; } = where;
}

public sealed class Assignment(string columnName, SqlExpression value) : ISqlNode
{
    public string ColumnName { get; } = columnName;
    public SqlExpression Value { get; } = value;
}

// DELETE Statement
public sealed class DeleteStatement(string tableName, SqlExpression where = null) : SqlStatement
{
    public string TableName { get; } = tableName;
    public SqlExpression Where { get; } = where;
}

// CREATE TABLE Statement
public sealed class CreateTableStatement(
    string tableName,
    IReadOnlyList<ColumnDefinition> columns,
    IReadOnlyList<TableConstraint> constraints = null,
    bool ifNotExists = false) : SqlStatement
{
    public string TableName { get; } = tableName;
    public IReadOnlyList<ColumnDefinition> Columns { get; } = columns;
    public IReadOnlyList<TableConstraint> Constraints { get; } = constraints;
    public bool IfNotExists { get; } = ifNotExists;
}

public sealed class ColumnDefinition(
    string name,
    SqlDataType dataType,
    bool nullable = true,
    SqlExpression defaultValue = null,
    bool primaryKey = false,
    bool unique = false,
    bool autoIncrement = false) : ISqlNode
{
    public string Name { get; } = name;
    public SqlDataType DataType { get; } = dataType;
    public bool Nullable { get; } = nullable;
    public SqlExpression DefaultValue { get; } = defaultValue;
    public bool PrimaryKey { get; } = primaryKey;
    public bool Unique { get; } = unique;
    public bool AutoIncrement { get; } = autoIncrement;
}

public sealed class SqlDataType(string name, int? length = null, int? precision = null, int? scale = null) : ISqlNode
{
    public string Name { get; } = name;
    public int? Length { get; } = length;
    public int? Precision { get; } = precision;
    public int? Scale { get; } = scale;
}

public abstract class TableConstraint : ISqlNode
{
    public string Name { get; set; }
}

public sealed class PrimaryKeyConstraint(IReadOnlyList<string> columns, string name = null) : TableConstraint
{
    public IReadOnlyList<string> Columns { get; } = columns;
}

public sealed class ForeignKeyConstraint(
    IReadOnlyList<string> columns,
    string referencedTable,
    IReadOnlyList<string> referencedColumns,
    string name = null) : TableConstraint
{
    public IReadOnlyList<string> Columns { get; } = columns;
    public string ReferencedTable { get; } = referencedTable;
    public IReadOnlyList<string> ReferencedColumns { get; } = referencedColumns;
}

public sealed class UniqueConstraint(IReadOnlyList<string> columns, string name = null) : TableConstraint
{
    public IReadOnlyList<string> Columns { get; } = columns;
}

// DROP TABLE Statement
public sealed class DropTableStatement(string tableName, bool ifExists = false) : SqlStatement
{
    public string TableName { get; } = tableName;
    public bool IfExists { get; } = ifExists;
}

// CREATE INDEX Statement
public sealed class CreateIndexStatement(
    string indexName,
    string tableName,
    IReadOnlyList<string> columns,
    bool unique = false,
    bool ifNotExists = false) : SqlStatement
{
    public string IndexName { get; } = indexName;
    public string TableName { get; } = tableName;
    public IReadOnlyList<string> Columns { get; } = columns;
    public bool Unique { get; } = unique;
    public bool IfNotExists { get; } = ifNotExists;
}

// DROP INDEX Statement
public sealed class DropIndexStatement(string indexName, bool ifExists = false) : SqlStatement
{
    public string IndexName { get; } = indexName;
    public bool IfExists { get; } = ifExists;
}

// ========================================
// Table References
// ========================================

public abstract class TableReference : ISqlNode
{
}

public sealed class TableName(string name, string alias = null) : TableReference
{
    public string Name { get; } = name;
    public string Alias { get; } = alias;
}

public sealed class JoinClause(
    TableReference left,
    TableReference right,
    JoinType joinType,
    SqlExpression condition) : TableReference
{
    public TableReference Left { get; } = left;
    public TableReference Right { get; } = right;
    public JoinType JoinType { get; } = joinType;
    public SqlExpression Condition { get; } = condition;
}

public enum JoinType
{
    Inner,
    Left,
    Right,
    Full,
    Cross
}

// ========================================
// Expressions
// ========================================

public abstract class SqlExpression : ISqlNode
{
}

// Literals
public sealed class LiteralExpression(object value, SqlLiteralKind kind) : SqlExpression
{
    public object Value { get; } = value;
    public SqlLiteralKind Kind { get; } = kind;
}

public enum SqlLiteralKind
{
    Integer,
    Float,
    String,
    Boolean,
    Null,
    Date,
    Time,
    DateTime
}

// Column Reference
public sealed class ColumnReference(string columnName, string tableName = null) : SqlExpression
{
    public string ColumnName { get; } = columnName;
    public string TableName { get; } = tableName;
}

// Binary Operations
public sealed class BinaryExpression(SqlExpression left, SqlBinaryOperator op, SqlExpression right) : SqlExpression
{
    public SqlExpression Left { get; } = left;
    public SqlBinaryOperator Operator { get; } = op;
    public SqlExpression Right { get; } = right;
}

public enum SqlBinaryOperator
{
    // Arithmetic
    Add,        // +
    Subtract,   // -
    Multiply,   // *
    Divide,     // /
    Modulo,     // %
    
    // Comparison
    Equal,              // =
    NotEqual,           // <> or !=
    LessThan,           // <
    LessThanOrEqual,    // <=
    GreaterThan,        // >
    GreaterThanOrEqual, // >=
    
    // Logical
    And,        // AND
    Or,         // OR
    
    // String
    Like,       // LIKE
    NotLike,    // NOT LIKE
    
    // Set
    In,         // IN
    NotIn       // NOT IN
}

// Unary Operations
public sealed class UnaryExpression(SqlUnaryOperator op, SqlExpression operand) : SqlExpression
{
    public SqlUnaryOperator Operator { get; } = op;
    public SqlExpression Operand { get; } = operand;
}

public enum SqlUnaryOperator
{
    Not,        // NOT
    Minus,      // -
    Plus,       // +
    IsNull,     // IS NULL
    IsNotNull   // IS NOT NULL
}

// Function Call
public sealed class FunctionCall(string name, IReadOnlyList<SqlExpression> arguments, bool distinct = false) : SqlExpression
{
    public string Name { get; } = name;
    public IReadOnlyList<SqlExpression> Arguments { get; } = arguments;
    public bool Distinct { get; } = distinct;
}

// CASE Expression
public sealed class CaseExpression(
    SqlExpression testExpression,
    IReadOnlyList<WhenClause> whenClauses,
    SqlExpression elseClause = null) : SqlExpression
{
    public SqlExpression TestExpression { get; } = testExpression;
    public IReadOnlyList<WhenClause> WhenClauses { get; } = whenClauses;
    public SqlExpression ElseClause { get; } = elseClause;
}

public sealed class WhenClause(SqlExpression condition, SqlExpression result) : ISqlNode
{
    public SqlExpression Condition { get; } = condition;
    public SqlExpression Result { get; } = result;
}

// Subquery
public sealed class SubqueryExpression(SelectStatement query) : SqlExpression
{
    public SelectStatement Query { get; } = query;
}

// List Expression (for IN operator)
public sealed class ListExpression(IReadOnlyList<SqlExpression> items) : SqlExpression
{
    public IReadOnlyList<SqlExpression> Items { get; } = items;
}

// BETWEEN Expression
public sealed class BetweenExpression(SqlExpression value, SqlExpression lower, SqlExpression upper, bool notBetween = false) : SqlExpression
{
    public SqlExpression Value { get; } = value;
    public SqlExpression Lower { get; } = lower;
    public SqlExpression Upper { get; } = upper;
    public bool NotBetween { get; } = notBetween;
}

// ALL Columns (*)
public sealed class AllColumnsExpression(string tableName = null) : SqlExpression
{
    public string TableName { get; } = tableName;
}

// CAST Expression
public sealed class CastExpression(SqlExpression expression, SqlDataType targetType) : SqlExpression
{
    public SqlExpression Expression { get; } = expression;
    public SqlDataType TargetType { get; } = targetType;
}

