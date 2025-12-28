# SQL AST Compliance Report
## Анализ соответствия SqlAst.cs спецификации SqlGrammarSpecification.txt

**Дата анализа:** 2025-12-07  
**Файлы:** 
- `SqlAst.cs` - реализация AST
- `SqlGrammarSpecification.txt` - SQL грамматика (BNF)

---

## ✅ Что реализовано корректно

### 1. Базовая структура SELECT
- ✅ `SelectStatement` с основными клаузами
- ✅ `SelectRestriction` (ALL, DISTINCT, NotSpecified)
- ✅ `ColumnItem` и `ColumnSource`
- ✅ `FromClause`, `WhereClause`, `GroupByClause`, `HavingClause`
- ✅ `OrderByClause`, `LimitClause`, `OffsetClause`

### 2. JOIN операции (частично)
- ✅ `JoinStatement` с условиями
- ✅ `JoinKind`: None, Inner, Left, Right

### 3. Common Table Expressions (CTE)
- ✅ `WithClause` с поддержкой множественных CTE
- ✅ `CommonTableExpression` с именем, колонками и запросом

### 4. UNION операции (частично)
- ✅ `UnionStatement` и `UnionClause`
- ✅ Поддержка `UNION ALL`

### 5. Выражения
- ✅ `BinaryExpression` с широким набором операторов
- ✅ `UnaryExpression` (NOT, Plus, Minus, BitwiseNot)
- ✅ `BetweenExpression` с поддержкой NOT
- ✅ `InExpression` с поддержкой NOT
- ✅ `IdentifierExpression`
- ✅ `LiteralExpression<T>`
- ✅ `FunctionCall` с различными типами аргументов

### 6. Table Sources
- ✅ `TableSourceItem` - обычные таблицы с алиасами
- ✅ `TableSourceSubQuery` - подзапросы

### 7. Window Functions (частично)
- ✅ `OverClause` с PARTITION BY и ORDER BY
- ✅ `PartitionByClause`
- ✅ `ColumnSourceFunction` с поддержкой OVER

---

## ❌ Критические несоответствия и отсутствующие элементы

### 1. Set Operations (UNION, INTERSECT, EXCEPT)

**Проблема:**
- ❌ Отсутствует поддержка `INTERSECT`
- ❌ Отсутствует поддержка `EXCEPT`
- ⚠️ `UnionClause` поддерживает только `IsAll`, нет явной поддержки `DISTINCT`

**Спецификация:**
```
<query expression> ::=
    <query term> [ { UNION [ ALL | DISTINCT ] <query term> } ]

<query term> ::=
    <query primary>
  | <query term> INTERSECT [ ALL | DISTINCT ] <query primary>
  | <query term> EXCEPT [ ALL | DISTINCT ] <query primary>
```

**Рекомендация:**
```csharp
public enum SetOperator
{
    Union,
    Intersect,
    Except
}

public enum SetQuantifier
{
    Distinct,  // По умолчанию
    All
}

public sealed class SetOperationClause : ISqlNode
{
    public SetOperator Operator { get; }
    public SetQuantifier Quantifier { get; }
    
    public SetOperationClause(SetOperator op, SetQuantifier quantifier = SetQuantifier.Distinct)
    {
        Operator = op;
        Quantifier = quantifier;
    }
}

// Изменить UnionStatement на:
public sealed class QueryCombination : ISqlNode
{
    public Statement Statement { get; }
    public SetOperationClause? SetOperation { get; }
    
    public QueryCombination(Statement statement, SetOperationClause? setOperation = null)
    {
        Statement = statement;
        SetOperation = setOperation;
    }
}
```

---

### 2. WITH RECURSIVE

**Проблема:**
- ❌ В `WithClause` отсутствует флаг `IsRecursive`

**Спецификация:**
```
<with clause> ::=
    WITH [ RECURSIVE ] <common table expression> [ { ',' <common table expression> } ]
```

**Рекомендация:**
```csharp
public sealed class WithClause : ISqlNode
{
    public bool IsRecursive { get; }  // ← Добавить
    public IReadOnlyList<CommonTableExpression> CTEs { get; }

    public WithClause(IReadOnlyList<CommonTableExpression> ctes, bool isRecursive = false)
    {
        CTEs = ctes;
        IsRecursive = isRecursive;
    }
}
```

---

### 3. JOIN Types - неполная поддержка

**Проблема:**
- ❌ Отсутствует `FULL OUTER JOIN`
- ❌ Отсутствует `CROSS JOIN`
- ❌ Отсутствует `NATURAL JOIN`
- ❌ Отсутствует `USING` clause (альтернатива `ON`)

**Спецификация:**
```
<join type> ::=
    INNER
  | <outer join type> [ OUTER ]

<outer join type> ::=
    LEFT | RIGHT | FULL

<cross join> ::=
    <table reference> CROSS JOIN <table primary>

<natural join> ::=
    <table reference> NATURAL [ <join type> ] JOIN <table primary>

<join specification> ::=
    <join condition>
  | <named columns join>

<join condition> ::=
    ON <search condition>

<named columns join> ::=
    USING '(' <column name list> ')'
```

**Рекомендация:**
```csharp
public enum JoinKind
{
    None,
    Inner,
    Left,
    Right,
    Full,     // ← Добавить
    Cross,    // ← Добавить
    Natural   // ← Добавить (можно комбинировать с Inner/Left/Right)
}

// Добавить тип для спецификации JOIN
public abstract class JoinSpecification : ISqlNode { }

public sealed class OnJoinSpecification : JoinSpecification
{
    public Expression Condition { get; }
    
    public OnJoinSpecification(Expression condition)
    {
        Condition = condition;
    }
}

public sealed class UsingJoinSpecification : JoinSpecification
{
    public IReadOnlyList<string> ColumnNames { get; }
    
    public UsingJoinSpecification(IReadOnlyList<string> columnNames)
    {
        ColumnNames = columnNames;
    }
}

// Изменить JoinStatement:
public sealed class JoinStatement : ISqlNode
{
    public JoinKind JoinKind { get; }
    public IReadOnlyList<TableSourceItem> Tables { get; }
    public JoinSpecification? Specification { get; }  // Изменить на абстрактный тип
    
    public JoinStatement(IReadOnlyList<TableSourceItem> tables, JoinSpecification? specification, JoinKind joinKind = JoinKind.Inner)
    {
        Tables = tables;
        Specification = specification;
        JoinKind = joinKind;
    }
}
```

---

### 4. IS NULL / IS NOT NULL Predicate

**Проблема:**
- ❌ Нет специального класса для `IS NULL` / `IS NOT NULL`
- ⚠️ Возможно реализовано через `BinaryExpression`, что не совсем корректно

**Спецификация:**
```
<null predicate> ::=
    <value expression> IS [ NOT ] NULL
```

**Рекомендация:**
```csharp
public sealed class IsNullExpression : Expression
{
    public Expression Expression { get; }
    public bool IsNot { get; }
    
    public IsNullExpression(Expression expression, bool isNot = false)
    {
        Expression = expression;
        IsNot = isNot;
    }
}
```

---

### 5. EXISTS Predicate

**Проблема:**
- ❌ Полностью отсутствует поддержка `EXISTS`

**Спецификация:**
```
<exists predicate> ::=
    EXISTS '(' <query expression> ')'
```

**Рекомендация:**
```csharp
public sealed class ExistsExpression : Expression
{
    public SelectStatement SubQuery { get; }
    public bool IsNot { get; }  // Для NOT EXISTS
    
    public ExistsExpression(SelectStatement subQuery, bool isNot = false)
    {
        SubQuery = subQuery;
        IsNot = isNot;
    }
}
```

---

### 6. Quantified Comparison (ALL, SOME, ANY)

**Проблема:**
- ❌ Отсутствует поддержка `ALL`, `SOME`, `ANY` в сравнениях

**Спецификация:**
```
<quantified comparison predicate> ::=
    <value expression> <comp op> <quantifier> '(' <query expression> ')'

<quantifier> ::=
    ALL | SOME | ANY
```

**Рекомендация:**
```csharp
public enum ComparisonQuantifier
{
    None,
    All,
    Some,
    Any
}

public sealed class QuantifiedComparisonExpression : Expression
{
    public Expression Left { get; }
    public BinaryOperator Operator { get; }
    public ComparisonQuantifier Quantifier { get; }
    public SelectStatement SubQuery { get; }
    
    public QuantifiedComparisonExpression(
        Expression left, 
        BinaryOperator op, 
        ComparisonQuantifier quantifier,
        SelectStatement subQuery)
    {
        Left = left;
        Operator = op;
        Quantifier = quantifier;
        SubQuery = subQuery;
    }
}
```

---

### 7. CASE Expression

**Проблема:**
- ❌ **ПОЛНОСТЬЮ ОТСУТСТВУЕТ** - критическое упущение для полноценного SQL

**Спецификация:**
```
<case expression> ::=
    <case abbreviation>
  | <case specification>

<case abbreviation> ::=
    NULLIF '(' <value expression> ',' <value expression> ')'
  | COALESCE '(' <value expression> [ { ',' <value expression> } ] ')'

<case specification> ::=
    <simple case>
  | <searched case>

<simple case> ::=
    CASE <case operand>
        <simple when clause> ...
        [ <else clause> ]
    END

<searched case> ::=
    CASE
        <searched when clause> ...
        [ <else clause> ]
    END
```

**Рекомендация:**
```csharp
// Базовый класс для CASE
public abstract class CaseExpression : Expression { }

// CASE value WHEN ... THEN ... END
public sealed class SimpleCaseExpression : CaseExpression
{
    public Expression CaseOperand { get; }
    public IReadOnlyList<SimpleCaseWhen> WhenClauses { get; }
    public Expression? ElseResult { get; }
    
    public SimpleCaseExpression(
        Expression caseOperand, 
        IReadOnlyList<SimpleCaseWhen> whenClauses,
        Expression? elseResult = null)
    {
        CaseOperand = caseOperand;
        WhenClauses = whenClauses;
        ElseResult = elseResult;
    }
}

public sealed class SimpleCaseWhen : ISqlNode
{
    public Expression WhenValue { get; }
    public Expression ThenResult { get; }
    
    public SimpleCaseWhen(Expression whenValue, Expression thenResult)
    {
        WhenValue = whenValue;
        ThenResult = thenResult;
    }
}

// CASE WHEN condition THEN ... END
public sealed class SearchedCaseExpression : CaseExpression
{
    public IReadOnlyList<SearchedCaseWhen> WhenClauses { get; }
    public Expression? ElseResult { get; }
    
    public SearchedCaseExpression(
        IReadOnlyList<SearchedCaseWhen> whenClauses,
        Expression? elseResult = null)
    {
        WhenClauses = whenClauses;
        ElseResult = elseResult;
    }
}

public sealed class SearchedCaseWhen : ISqlNode
{
    public Expression Condition { get; }
    public Expression ThenResult { get; }
    
    public SearchedCaseWhen(Expression condition, Expression thenResult)
    {
        Condition = condition;
        ThenResult = thenResult;
    }
}

// COALESCE(expr1, expr2, ...)
public sealed class CoalesceExpression : Expression
{
    public IReadOnlyList<Expression> Expressions { get; }
    
    public CoalesceExpression(IReadOnlyList<Expression> expressions)
    {
        Expressions = expressions;
    }
}

// NULLIF(expr1, expr2)
public sealed class NullIfExpression : Expression
{
    public Expression First { get; }
    public Expression Second { get; }
    
    public NullIfExpression(Expression first, Expression second)
    {
        First = first;
        Second = second;
    }
}
```

---

### 8. CAST Expression

**Проблема:**
- ❌ Отсутствует поддержка приведения типов

**Спецификация:**
```
<cast specification> ::=
    CAST '(' <value expression> AS <data type> ')'
```

**Рекомендация:**
```csharp
public sealed class DataType : ISqlNode
{
    public string TypeName { get; }
    public int? Length { get; }
    public int? Precision { get; }
    public int? Scale { get; }
    
    public DataType(string typeName, int? length = null, int? precision = null, int? scale = null)
    {
        TypeName = typeName;
        Length = length;
        Precision = precision;
        Scale = scale;
    }
}

public sealed class CastExpression : Expression
{
    public Expression Expression { get; }
    public DataType TargetType { get; }
    
    public CastExpression(Expression expression, DataType targetType)
    {
        Expression = expression;
        TargetType = targetType;
    }
}
```

---

### 9. Window Functions - Frame Clause

**Проблема:**
- ❌ В `OverClause` отсутствует поддержка Frame Specification (ROWS/RANGE/GROUPS BETWEEN)

**Спецификация:**
```
<window specification> ::=
    '(' [ <partition clause> ] [ <order by clause> ] [ <frame clause> ] ')'

<frame clause> ::=
    <frame units> <frame extent> [ <frame exclusion> ]

<frame units> ::=
    ROWS | RANGE | GROUPS

<frame extent> ::=
    <frame start>
  | <frame between>

<frame between> ::=
    BETWEEN <frame bound 1> AND <frame bound 2>

<frame bound> ::=
    UNBOUNDED PRECEDING
  | <unsigned value specification> PRECEDING
  | CURRENT ROW
  | UNBOUNDED FOLLOWING
  | <unsigned value specification> FOLLOWING
```

**Рекомендация:**
```csharp
public enum FrameUnit
{
    Rows,
    Range,
    Groups
}

public enum FrameBoundType
{
    UnboundedPreceding,
    Preceding,
    CurrentRow,
    Following,
    UnboundedFollowing
}

public sealed class FrameBound : ISqlNode
{
    public FrameBoundType Type { get; }
    public Expression? Offset { get; }  // Для N PRECEDING/FOLLOWING
    
    public FrameBound(FrameBoundType type, Expression? offset = null)
    {
        Type = type;
        Offset = offset;
    }
}

public sealed class FrameClause : ISqlNode
{
    public FrameUnit Unit { get; }
    public FrameBound Start { get; }
    public FrameBound? End { get; }  // Если null, то только Start
    
    public FrameClause(FrameUnit unit, FrameBound start, FrameBound? end = null)
    {
        Unit = unit;
        Start = start;
        End = end;
    }
}

// Изменить OverClause:
public sealed class OverClause : ISqlNode
{
    public PartitionByClause? PartitionBy { get; }
    public OrderByClause? OrderBy { get; }
    public FrameClause? Frame { get; }  // ← Добавить
    
    public OverClause(
        PartitionByClause? partitionBy = null, 
        OrderByClause? orderBy = null,
        FrameClause? frame = null)
    {
        PartitionBy = partitionBy;
        OrderBy = orderBy;
        Frame = frame;
    }
}
```

---

### 10. ORDER BY - NULLS FIRST/LAST

**Проблема:**
- ❌ Отсутствует поддержка `NULLS FIRST` / `NULLS LAST`

**Спецификация:**
```
<sort specification> ::=
    <sort key> [ <ordering specification> ] [ <null ordering> ]

<null ordering> ::=
    NULLS FIRST | NULLS LAST
```

**Рекомендация:**
```csharp
public enum NullOrdering
{
    NotSpecified,
    First,
    Last
}

// Изменить OrderByItem:
public sealed class OrderByItem : ISqlNode
{
    public Identifier Identifier { get; }
    public FunctionArguments? Arguments { get; }
    public OrderDirection Direction { get; }
    public NullOrdering NullOrdering { get; }  // ← Добавить
    
    public OrderByItem(
        Identifier identifier, 
        FunctionArguments? arguments, 
        OrderDirection direction,
        NullOrdering nullOrdering = NullOrdering.NotSpecified)
    {
        Identifier = identifier;
        Arguments = arguments;
        Direction = direction;
        NullOrdering = nullOrdering;
    }
}
```

---

### 11. LIMIT - Alternative Syntax (FETCH FIRST)

**Проблема:**
- ⚠️ Нет поддержки стандартного синтаксиса `FETCH FIRST ... ROWS ONLY`

**Спецификация:**
```
<limit clause> ::=
    LIMIT <numeric value expression>
  | FETCH FIRST <numeric value expression> ROWS ONLY
```

**Рекомендация:**
```csharp
public enum LimitSyntax
{
    Limit,      // LIMIT n
    FetchFirst  // FETCH FIRST n ROWS ONLY
}

public sealed class LimitClause : ISqlNode
{
    public Expression Expression { get; }
    public LimitSyntax Syntax { get; }  // ← Добавить (опционально)
    
    public LimitClause(Expression expression, LimitSyntax syntax = LimitSyntax.Limit)
    {
        Expression = expression;
        Syntax = syntax;
    }
}
```

---

### 12. GROUP BY - Advanced Features

**Проблема:**
- ❌ Отсутствует поддержка `ROLLUP`
- ❌ Отсутствует поддержка `CUBE`
- ❌ Отсутствует поддержка `GROUPING SETS`

**Спецификация:**
```
<grouping element> ::=
    <ordinary grouping set>
  | <rollup list>
  | <cube list>
  | <grouping sets specification>
  | <empty grouping set>

<rollup list> ::=
    ROLLUP '(' <grouping element list> ')'

<cube list> ::=
    CUBE '(' <grouping element list> ')'

<grouping sets specification> ::=
    GROUPING SETS '(' <grouping element list> ')'
```

**Рекомендация:**
```csharp
public abstract class GroupingElement : ISqlNode { }

public sealed class OrdinaryGrouping : GroupingElement
{
    public ColumnSource Column { get; }
    
    public OrdinaryGrouping(ColumnSource column)
    {
        Column = column;
    }
}

public sealed class RollupGrouping : GroupingElement
{
    public IReadOnlyList<GroupingElement> Elements { get; }
    
    public RollupGrouping(IReadOnlyList<GroupingElement> elements)
    {
        Elements = elements;
    }
}

public sealed class CubeGrouping : GroupingElement
{
    public IReadOnlyList<GroupingElement> Elements { get; }
    
    public CubeGrouping(IReadOnlyList<GroupingElement> elements)
    {
        Elements = elements;
    }
}

public sealed class GroupingSetsGrouping : GroupingElement
{
    public IReadOnlyList<GroupingElement> Elements { get; }
    
    public GroupingSetsGrouping(IReadOnlyList<GroupingElement> elements)
    {
        Elements = elements;
    }
}

public sealed class EmptyGrouping : GroupingElement
{
    public static readonly EmptyGrouping Instance = new();
    private EmptyGrouping() { }
}

// Изменить GroupByClause:
public sealed class GroupByClause : ISqlNode
{
    public IReadOnlyList<GroupingElement> Elements { get; }
    
    public GroupByClause(IReadOnlyList<GroupingElement> elements)
    {
        Elements = elements;
    }
}
```

---

### 13. Aggregate Functions - FILTER Clause

**Проблема:**
- ❌ Отсутствует поддержка `FILTER (WHERE ...)` для агрегатных функций

**Спецификация:**
```
<set function specification> ::=
    <aggregate function> '(' [ <set quantifier> ] <value expression> ')' [ <filter clause> ]
  | COUNT '(' '*' ')' [ <filter clause> ]

<filter clause> ::=
    FILTER '(' WHERE <search condition> ')'
```

**Рекомендация:**
```csharp
public sealed class AggregateExpression : Expression
{
    public Identifier FunctionName { get; }
    public FunctionArguments Arguments { get; }
    public SelectRestriction? Quantifier { get; }  // DISTINCT/ALL
    public Expression? FilterCondition { get; }     // FILTER (WHERE ...)
    
    public AggregateExpression(
        Identifier functionName,
        FunctionArguments arguments,
        SelectRestriction? quantifier = null,
        Expression? filterCondition = null)
    {
        FunctionName = functionName;
        Arguments = arguments;
        Quantifier = quantifier;
        FilterCondition = filterCondition;
    }
}
```

---

### 14. DML Statements (INSERT, UPDATE, DELETE)

**Проблема:**
- ❌ **ПОЛНОСТЬЮ ОТСУТСТВУЮТ** все операторы модификации данных

**Спецификация:**
```
<insert statement> ::=
    INSERT INTO <table name> [ '(' <column name list> ')' ]
    <insert values>
  | INSERT INTO <table name> [ '(' <column name list> ')' ]
    <query expression>

<update statement> ::=
    UPDATE <table name>
    SET <set clause list>
    [ <where clause> ]

<delete statement> ::=
    DELETE FROM <table name>
    [ <where clause> ]
```

**Рекомендация:**
```csharp
// INSERT
public sealed class InsertStatement : ISqlNode
{
    public Identifier TableName { get; }
    public IReadOnlyList<string>? ColumnNames { get; }
    public InsertSource Source { get; }
    
    public InsertStatement(
        Identifier tableName, 
        InsertSource source,
        IReadOnlyList<string>? columnNames = null)
    {
        TableName = tableName;
        Source = source;
        ColumnNames = columnNames;
    }
}

public abstract class InsertSource : ISqlNode { }

public sealed class ValuesInsertSource : InsertSource
{
    public IReadOnlyList<IReadOnlyList<Expression>> ValueRows { get; }
    
    public ValuesInsertSource(IReadOnlyList<IReadOnlyList<Expression>> valueRows)
    {
        ValueRows = valueRows;
    }
}

public sealed class SelectInsertSource : InsertSource
{
    public SelectStatement SelectStatement { get; }
    
    public SelectInsertSource(SelectStatement selectStatement)
    {
        SelectStatement = selectStatement;
    }
}

// UPDATE
public sealed class UpdateStatement : ISqlNode
{
    public Identifier TableName { get; }
    public IReadOnlyList<SetClause> SetClauses { get; }
    public WhereClause? WhereClause { get; }
    
    public UpdateStatement(
        Identifier tableName,
        IReadOnlyList<SetClause> setClauses,
        WhereClause? whereClause = null)
    {
        TableName = tableName;
        SetClauses = setClauses;
        WhereClause = whereClause;
    }
}

public sealed class SetClause : ISqlNode
{
    public string ColumnName { get; }
    public Expression Value { get; }
    
    public SetClause(string columnName, Expression value)
    {
        ColumnName = columnName;
        Value = value;
    }
}

// DELETE
public sealed class DeleteStatement : ISqlNode
{
    public Identifier TableName { get; }
    public WhereClause? WhereClause { get; }
    
    public DeleteStatement(Identifier tableName, WhereClause? whereClause = null)
    {
        TableName = tableName;
        WhereClause = whereClause;
    }
}
```

---

### 15. DDL Statements (CREATE, DROP, ALTER)

**Проблема:**
- ❌ **ПОЛНОСТЬЮ ОТСУТСТВУЮТ** все операторы определения данных

**Замечание:** Это может быть преднамеренно, если парсер предназначен только для SELECT запросов. Однако для полноценной SQL грамматики DDL критически важны.

**Рекомендация:** Если цель - полная поддержка SQL, необходимо добавить:
- `CREATE TABLE` с ограничениями (PRIMARY KEY, FOREIGN KEY, UNIQUE, CHECK, NOT NULL)
- `DROP TABLE` с CASCADE/RESTRICT
- `ALTER TABLE` (ADD/DROP COLUMN, ADD/DROP CONSTRAINT)
- `CREATE INDEX` / `DROP INDEX`
- Определения типов данных

(Полная реализация займёт значительный объём кода)

---

### 16. Transaction Control

**Проблема:**
- ❌ Отсутствует поддержка транзакций

**Спецификация:**
```
<begin transaction> ::= BEGIN [ TRANSACTION | WORK ]
<commit statement> ::= COMMIT [ TRANSACTION | WORK ]
<rollback statement> ::= ROLLBACK [ TRANSACTION | WORK ]
<savepoint statement> ::= SAVEPOINT <savepoint name>
```

**Рекомендация:** Добавить если нужна полная поддержка SQL.

---

## ⚠️ Структурные замечания

### 1. Statement vs SelectStatement

**Текущая структура:**
```csharp
Statement -> SelectStatement
```

**Проблема:** В реальном SQL `Statement` должен быть базовым типом для всех типов операторов (SELECT, INSERT, UPDATE, DELETE, CREATE, etc.)

**Рекомендация:**
```csharp
public abstract class SqlStatement : ISqlNode { }

public sealed class SelectStatementWrapper : SqlStatement
{
    public WithClause? WithClause { get; }
    public SelectStatement SelectStatement { get; }
    // ...
}

public sealed class InsertStatement : SqlStatement { }
public sealed class UpdateStatement : SqlStatement { }
public sealed class DeleteStatement : SqlStatement { }
// и т.д.
```

### 2. OrderByItem - странная структура

**Текущая реализация:**
```csharp
public sealed class OrderByItem : ISqlNode
{
    public Identifier Identifier { get; }
    public FunctionArguments? Arguments { get; }  // ← Это странно
    public OrderDirection Direction { get; }
}
```

**Проблема:** `FunctionArguments` в `ORDER BY` выглядит нелогично. По спецификации:

```
<sort key> ::=
    <value expression>
```

**Рекомендация:**
```csharp
public sealed class OrderByItem : ISqlNode
{
    public Expression Expression { get; }  // Любое выражение, включая функции
    public OrderDirection Direction { get; }
    public NullOrdering NullOrdering { get; }
    
    public OrderByItem(
        Expression expression, 
        OrderDirection direction = OrderDirection.NotSpecified,
        NullOrdering nullOrdering = NullOrdering.NotSpecified)
    {
        Expression = expression;
        Direction = direction;
        NullOrdering = nullOrdering;
    }
}
```

### 3. Unused imports

**Проблема:**
```csharp
using System.Collections.Specialized;  // Не используется
using System.ComponentModel.Design;    // Не используется
using System.Linq;                     // Не используется
```

**Рекомендация:** Удалить неиспользуемые директивы.

---

## 📊 Статистика покрытия

| Категория | Реализовано | Частично | Отсутствует | Процент |
|-----------|-------------|----------|-------------|---------|
| **SELECT Statement** | ✅ | - | - | 100% |
| **Set Operations** | - | ⚠️ UNION | ❌ INTERSECT, EXCEPT | 33% |
| **CTE** | - | ⚠️ WITH | ❌ RECURSIVE | 80% |
| **JOIN** | - | ⚠️ Inner, Left, Right | ❌ Full, Cross, Natural, USING | 50% |
| **WHERE Predicates** | ⚠️ | - | ❌ IS NULL, EXISTS, Quantified | 60% |
| **Expressions** | ⚠️ | - | ❌ CASE, CAST | 70% |
| **Window Functions** | - | ⚠️ Basic OVER | ❌ Frame Clause | 60% |
| **ORDER BY** | - | ⚠️ Basic | ❌ NULLS FIRST/LAST | 80% |
| **GROUP BY** | - | ⚠️ Basic | ❌ ROLLUP, CUBE, GROUPING SETS | 40% |
| **DML** | - | - | ❌ INSERT, UPDATE, DELETE | 0% |
| **DDL** | - | - | ❌ CREATE, DROP, ALTER | 0% |
| **Transactions** | - | - | ❌ BEGIN, COMMIT, ROLLBACK | 0% |

**Общее покрытие спецификации:** ~45-50%

---

## 🎯 Приоритизация доработок

### Критичные (для SELECT запросов):
1. ⭐⭐⭐ **CASE Expression** - используется очень часто
2. ⭐⭐⭐ **IS NULL / IS NOT NULL** - базовая функциональность
3. ⭐⭐⭐ **EXISTS** - важно для подзапросов
4. ⭐⭐ **INTERSECT / EXCEPT** - стандартные операции
5. ⭐⭐ **FULL JOIN** - стандартный тип JOIN
6. ⭐⭐ **WITH RECURSIVE** - для иерархических запросов

### Желательные:
7. ⭐ **CAST** - приведение типов
8. ⭐ **Window Frame Clause** - для сложных аналитических запросов
9. ⭐ **NULLS FIRST/LAST** - управление сортировкой NULL
10. ⭐ **Quantified Comparison (ALL/ANY/SOME)** - редко используется

### Для полной поддержки SQL:
11. **DML** (INSERT, UPDATE, DELETE)
12. **DDL** (CREATE TABLE, ALTER TABLE, DROP TABLE, CREATE INDEX)
13. **ROLLUP/CUBE/GROUPING SETS**
14. **Transaction Control**

### Рефакторинг:
- Исправить структуру `OrderByItem`
- Пересмотреть иерархию `Statement`
- Удалить неиспользуемые imports
- Добавить XML-комментарии к публичным классам

---

## 📝 Выводы

**Текущая реализация** хорошо покрывает базовые SELECT запросы с JOIN, подзапросами, агрегацией и оконными функциями. Однако для полного соответствия SQL спецификации **критически не хватает**:

1. **CASE выражений** - одна из самых используемых конструкций SQL
2. **IS NULL предиката** - базовая проверка
3. **EXISTS предиката** - важно для оптимизации подзапросов
4. **DML операторов** - если цель не только SELECT

Рекомендуется в первую очередь добавить поддержку CASE, IS NULL и EXISTS, так как они используются в большинстве реальных SQL запросов.

---

**Подготовлено:** SQL AST Analyzer  
**Версия отчёта:** 1.0

