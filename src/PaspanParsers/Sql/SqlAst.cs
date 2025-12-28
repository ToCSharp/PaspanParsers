#nullable enable

namespace Parlot.Tests.Sql;
// Base interface
public interface ISqlNode
{
}

// Statements
public sealed class StatementList(IReadOnlyList<StatementLine> statements) : ISqlNode
{
    public IReadOnlyList<StatementLine> Statements { get; } = statements;
}

public sealed class StatementLine(IReadOnlyList<UnionStatement> unionStatements) : ISqlNode
{
    public IReadOnlyList<UnionStatement> UnionStatements { get; } = unionStatements;
}

public sealed class UnionStatement(Statement statement, UnionClause? unionClause = null) : ISqlNode
{
    public Statement Statement { get; } = statement;
    public UnionClause? UnionClause { get; } = unionClause;
}

public sealed class UnionClause(bool isAll = false) : ISqlNode
{
    public bool IsAll { get; } = isAll;
}

public sealed class Statement(SelectStatement selectStatement, WithClause? withClause = null) : ISqlNode
{
    public WithClause? WithClause { get; } = withClause;
    public SelectStatement SelectStatement { get; } = selectStatement;
}

public sealed class WithClause(IReadOnlyList<CommonTableExpression> ctes) : ISqlNode
{
    public IReadOnlyList<CommonTableExpression> CTEs { get; } = ctes;
}

public sealed class CommonTableExpression(string name, IReadOnlyList<UnionStatement> query, IReadOnlyList<string>? columnNames = null) : ISqlNode
{
    public string Name { get; } = name;
    public IReadOnlyList<string>? ColumnNames { get; } = columnNames;
    public IReadOnlyList<UnionStatement> Query { get; } = query;
}

public sealed class SelectStatement(
    IReadOnlyList<ColumnItem> columnItemList,
    SelectRestriction? restriction = null,
    FromClause? fromClause = null,
    WhereClause? whereClause = null,
    GroupByClause? groupByClause = null,
    HavingClause? havingClause = null,
    OrderByClause? orderByClause = null,
    LimitClause? limitClause = null,
    OffsetClause? offsetClause = null) : ISqlNode
{
    public SelectRestriction Restriction { get; } = restriction ?? SelectRestriction.NotSpecified;
    public IReadOnlyList<ColumnItem> ColumnItemList { get; } = columnItemList;
    public FromClause? FromClause { get; } = fromClause;
    public WhereClause? WhereClause { get; } = whereClause;
    public GroupByClause? GroupByClause { get; } = groupByClause;
    public HavingClause? HavingClause { get; } = havingClause;
    public OrderByClause? OrderByClause { get; } = orderByClause;
    public LimitClause? LimitClause { get; } = limitClause;
    public OffsetClause? OffsetClause { get; } = offsetClause;
}

public enum SelectRestriction
{
    NotSpecified,
    All,
    Distinct,
}

public sealed class ColumnItem(ColumnSource source, Identifier? alias = null) : ISqlNode
{
    public ColumnSource Source { get; } = source;
    public Identifier? Alias { get; } = alias;
}

public abstract class ColumnSource : ISqlNode
{
}

public sealed class ColumnSourceIdentifier(Identifier identifier) : ColumnSource
{
    public Identifier Identifier { get; } = identifier;
}

public sealed class ColumnSourceFunction(FunctionCall functionCall, OverClause? overClause = null) : ColumnSource
{
    public FunctionCall FunctionCall { get; } = functionCall;
    public OverClause? OverClause { get; } = overClause;
}

// Clauses
public sealed class FromClause(IReadOnlyList<TableSource> tableSources, IReadOnlyList<JoinStatement>? joins = null) : ISqlNode
{
    public IReadOnlyList<TableSource> TableSources { get; } = tableSources;
    public IReadOnlyList<JoinStatement>? Joins { get; } = joins;
}

public abstract class TableSource : ISqlNode
{
}

public sealed class TableSourceItem(Identifier identifier, Identifier? alias = null) : TableSource
{
    public Identifier Identifier { get; } = identifier;
    public Identifier? Alias { get; } = alias;
}

public sealed class TableSourceSubQuery(IReadOnlyList<UnionStatement> query, string alias) : TableSource
{
    public IReadOnlyList<UnionStatement> Query { get; } = query;
    public string Alias { get; } = alias;
}

public sealed class JoinStatement(IReadOnlyList<TableSourceItem> tables, Expression conditions, JoinKind? joinKind = null) : ISqlNode
{
    public JoinKind? JoinKind { get; } = joinKind;
    public IReadOnlyList<TableSourceItem> Tables { get; } = tables;
    public Expression Conditions { get; } = conditions;
}

public enum JoinKind
{
    None,
    Inner,
    Left,
    Right,
}

public sealed class WhereClause(Expression expression) : ISqlNode
{
    public Expression Expression { get; } = expression;
}

public sealed class GroupByClause(IReadOnlyList<ColumnSource> columns) : ISqlNode
{
    public IReadOnlyList<ColumnSource> Columns { get; } = columns;
}

public sealed class HavingClause(Expression expression) : ISqlNode
{
    public Expression Expression { get; } = expression;
}

public sealed class OrderByClause(IReadOnlyList<OrderByItem> items) : ISqlNode
{
    public IReadOnlyList<OrderByItem> Items { get; } = items;
}

public sealed class OrderByItem(Identifier identifier, FunctionArguments? arguments, OrderDirection direction) : ISqlNode
{
    public Identifier Identifier { get; } = identifier;
    public FunctionArguments? Arguments { get; } = arguments;
    public OrderDirection Direction { get; } = direction;
}

public enum OrderDirection
{
    NotSpecified,
    Asc,
    Desc,
}

public sealed class LimitClause(Expression expression) : ISqlNode
{
    public Expression Expression { get; } = expression;
}

public sealed class OffsetClause(Expression expression) : ISqlNode
{
    public Expression Expression { get; } = expression;
}

public sealed class OverClause(PartitionByClause? partitionBy = null, OrderByClause? orderBy = null) : ISqlNode
{
    public PartitionByClause? PartitionBy { get; } = partitionBy;
    public OrderByClause? OrderBy { get; } = orderBy;
}

public sealed class PartitionByClause(IReadOnlyList<ColumnItem> columns) : ISqlNode
{
    public IReadOnlyList<ColumnItem> Columns { get; } = columns;
}

// Expressions
public abstract class Expression : ISqlNode
{
}

public sealed class BinaryExpression(Expression left, BinaryOperator op, Expression right) : Expression
{
    public Expression Left { get; } = left;
    public BinaryOperator Operator { get; } = op;
    public Expression Right { get; } = right;
}

public enum BinaryOperator
{
    // Arithmetic
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    // Bitwise
    BitwiseAnd,
    BitwiseOr,
    BitwiseXor,
    // Comparison
    Equal,
    NotEqual,
    NotEqualAlt,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    NotGreaterThan,
    NotLessThan,
    // Logical
    And,
    Or,
    Like,
    NotLike,
}

public sealed class UnaryExpression(UnaryOperator op, Expression expression) : Expression
{
    public UnaryOperator Operator { get; } = op;
    public Expression Expression { get; } = expression;
}

public enum UnaryOperator
{
    Not,
    Plus,
    Minus,
    BitwiseNot,
}

public sealed class BetweenExpression(Expression expression, Expression lower, Expression upper, bool isNot = false) : Expression
{
    public Expression Expression { get; } = expression;
    public bool IsNot { get; } = isNot;
    public Expression Lower { get; } = lower;
    public Expression Upper { get; } = upper;
}

public sealed class InExpression(Expression expression, FunctionArguments values, bool isNot = false) : Expression
{
    public Expression Expression { get; } = expression;
    public bool IsNot { get; } = isNot;
    public FunctionArguments Values { get; } = values;
}

public sealed class IdentifierExpression(Identifier identifier) : Expression
{
    public Identifier Identifier { get; } = identifier;
}

public sealed class LiteralExpression<T>(T value) : Expression
{
    public T Value { get; } = value;
}

public sealed class FunctionCall(Identifier name, FunctionArguments arguments) : Expression
{
    public Identifier Name { get; } = name;
    public FunctionArguments Arguments { get; } = arguments;
}

public abstract class FunctionArguments : ISqlNode
{
}

public sealed class EmptyArguments : FunctionArguments
{
    public static readonly EmptyArguments Instance = new();

    private EmptyArguments()
    {
    }
}

public sealed class StarArgument : FunctionArguments
{
    public static readonly StarArgument Instance = new();

    private StarArgument()
    {
    }
}

public sealed class SelectStatementArgument(SelectStatement selectStatement) : FunctionArguments
{
    public SelectStatement SelectStatement { get; } = selectStatement;
}

public sealed class ExpressionListArguments(IReadOnlyList<Expression> expressions) : FunctionArguments
{
    public IReadOnlyList<Expression> Expressions { get; } = expressions;
}

public sealed class TupleExpression(IReadOnlyList<Expression> expressions) : Expression
{
    public IReadOnlyList<Expression> Expressions { get; } = expressions;
}

public sealed class ParenthesizedSelectStatement(SelectStatement selectStatement) : Expression
{
    public SelectStatement SelectStatement { get; } = selectStatement;
}

public sealed class ParameterExpression(Identifier name, Expression? defaultValue = null) : Expression
{
    public Identifier Name { get; } = name;
    public Expression? DefaultValue { get; } = defaultValue;
}

// Identifiers
public sealed class Identifier(IReadOnlyList<string> parts) : ISqlNode
{
    public static readonly Identifier STAR = new (["*"]);

    private string _cachedToString = null!;

    public IReadOnlyList<string> Parts { get; } = parts;

    public override string ToString()
    {
        return _cachedToString ??= (Parts.Count == 1 ? Parts[0] : string.Join(".", Parts));
    }
}