# SQL Parser and Writer

A comprehensive SQL parser and code generator built with the Parlot parser combinator library. Supports standard SQL (SQL-92/SQL:2003) syntax.

## 🚀 Features

### Supported SQL Statements

#### Data Query Language (DQL)
- ✅ **SELECT** statements with full support for:
  - `DISTINCT` keyword
  - Column selection (including `*` and `table.*`)
  - Column aliases (`AS`)
  - `FROM` clause with multiple tables
  - `WHERE` conditions
  - `GROUP BY` with multiple columns
  - `HAVING` clause
  - `ORDER BY` with `ASC`/`DESC`
  - `LIMIT` and `OFFSET` for pagination
  - **JOINs**: `INNER`, `LEFT`, `RIGHT`, `FULL`, `CROSS`
  - Complex conditions and nested expressions

#### Data Manipulation Language (DML)
- ✅ **INSERT** statements
  - Single and multiple row inserts
  - Column list specification
  - `VALUES` clause

- ✅ **UPDATE** statements
  - Multiple column updates
  - `WHERE` conditions
  - Expression-based assignments

- ✅ **DELETE** statements
  - `WHERE` conditions
  - Conditional deletion

#### Data Definition Language (DDL)
- ✅ **CREATE TABLE** statements
  - Column definitions with data types
  - Column constraints: `NOT NULL`, `PRIMARY KEY`, `UNIQUE`, `AUTO_INCREMENT`
  - Default values
  - Table constraints: `PRIMARY KEY`, `FOREIGN KEY`, `UNIQUE`
  - `IF NOT EXISTS` clause

- ✅ **DROP TABLE** statements
  - `IF EXISTS` clause

- ✅ **CREATE INDEX** statements
  - `UNIQUE` indexes
  - Multi-column indexes
  - `IF NOT EXISTS` clause

- ✅ **DROP INDEX** statements
  - `IF EXISTS` clause

### Supported Expressions

#### Literals
- ✅ Integer literals: `42`, `-10`
- ✅ Float literals: `3.14`, `-0.5`
- ✅ String literals: `'hello'`, `'don''t'` (escaped quotes)
- ✅ Boolean literals: `TRUE`, `FALSE`
- ✅ NULL literal: `NULL`

#### Operators
- ✅ **Arithmetic**: `+`, `-`, `*`, `/`, `%`
- ✅ **Comparison**: `=`, `<>`, `!=`, `<`, `<=`, `>`, `>=`
- ✅ **Logical**: `AND`, `OR`, `NOT`
- ✅ **Pattern matching**: `LIKE`, `NOT LIKE`
- ✅ **Set membership**: `IN`, `NOT IN`
- ✅ **Range**: `BETWEEN`, `NOT BETWEEN`
- ✅ **Null check**: `IS NULL`, `IS NOT NULL`

#### Advanced Expressions
- ✅ **Function calls**: `COUNT(*)`, `SUM(amount)`, `AVG(price)`, etc.
  - Support for `DISTINCT` in aggregate functions: `COUNT(DISTINCT column)`
- ✅ **CASE expressions**:
  - Simple CASE: `CASE status WHEN 'active' THEN 1 ELSE 0 END`
  - Searched CASE: `CASE WHEN age < 18 THEN 'minor' ELSE 'adult' END`
- ✅ **CAST expressions**: `CAST(price AS INTEGER)`
- ✅ **Subqueries**: `WHERE id IN (SELECT user_id FROM orders)`
- ✅ **Column references**: `users.name`, `name`
- ✅ **Parenthesized expressions**: `(price * quantity)`

### Data Types
- ✅ Integer types: `INT`, `INTEGER`, `SMALLINT`, `BIGINT`
- ✅ String types: `VARCHAR(n)`, `CHAR(n)`, `TEXT`
- ✅ Numeric types: `DECIMAL(p,s)`, `NUMERIC(p,s)`, `FLOAT`, `DOUBLE`, `REAL`
- ✅ Boolean type: `BOOLEAN`, `BOOL`
- ✅ Date/Time types: `DATE`, `TIME`, `DATETIME`, `TIMESTAMP`

### Identifiers
- ✅ Regular identifiers: `users`, `user_id`
- ✅ Quoted identifiers:
  - Double quotes: `"select"`, `"table name"`
  - Brackets: `[order]`, `[user name]`
  - Backticks: `` `user` ``, `` `table name` ``

## 📖 Usage

### Parsing SQL

```csharp
using ParlotParsers.SQL;

// Parse a complete SQL script (multiple statements)
var script = @"
    CREATE TABLE users (
        id INT PRIMARY KEY AUTO_INCREMENT,
        name VARCHAR(100) NOT NULL,
        email VARCHAR(255) UNIQUE
    );

    INSERT INTO users (name, email) VALUES ('John', 'john@example.com');
    SELECT * FROM users WHERE name = 'John';
";

var result = SqlParser.Parse(script);
if (result.Success)
{
    foreach (var statement in result.Value.Statements)
    {
        Console.WriteLine($"Parsed: {statement.GetType().Name}");
    }
}

// Parse a single SQL statement
var selectResult = SqlParser.ParseStatement("SELECT * FROM users WHERE age > 18");
if (selectResult.Success)
{
    var selectStmt = (SelectStatement)selectResult.Value;
    Console.WriteLine($"Columns: {selectStmt.Columns.Count}");
    Console.WriteLine($"Has WHERE: {selectStmt.Where != null}");
}

// Parse a SQL expression
var exprResult = SqlParser.ParseExpression("price * quantity * 1.1");
if (exprResult.Success)
{
    Console.WriteLine("Expression parsed successfully");
}
```

### Generating SQL Code

```csharp
using ParlotParsers.SQL;

// Parse and regenerate SQL
var parseResult = SqlParser.ParseStatement(
    "SELECT id, name, email FROM users WHERE age >= 18 ORDER BY name ASC"
);

if (parseResult.Success)
{
    var writer = new SqlWriter(new SqlWriterOptions
    {
        UsePrettyPrint = true,
        UppercaseKeywords = true,
        IndentSize = 2
    });

    writer.WriteStatement(parseResult.Value);
    var generatedSql = writer.GetResult();
    Console.WriteLine(generatedSql);
}

// Output:
// SELECT id, name, email
// FROM users
// WHERE age >= 18
// ORDER BY name ASC
```

### Building SQL Programmatically

```csharp
using ParlotParsers.SQL;

// Build a SELECT statement
var selectStmt = new SelectStatement(
    distinct: false,
    columns: new List<SelectColumn>
    {
        new SelectColumn(new ColumnReference("id")),
        new SelectColumn(new ColumnReference("name")),
        new SelectColumn(new ColumnReference("email"))
    },
    from: new List<TableReference>
    {
        new TableName("users")
    },
    where: new BinaryExpression(
        new ColumnReference("age"),
        SqlBinaryOperator.GreaterThanOrEqual,
        new LiteralExpression(18, SqlLiteralKind.Integer)
    ),
    groupBy: null,
    having: null,
    orderBy: new List<OrderByClause>
    {
        new OrderByClause(new ColumnReference("name"), ascending: true)
    },
    limit: null,
    offset: null
);

// Generate SQL
var writer = new SqlWriter();
writer.WriteStatement(selectStmt);
Console.WriteLine(writer.GetResult());

// Build an INSERT statement
var insertStmt = new InsertStatement(
    tableName: "users",
    columns: new List<string> { "name", "email", "age" },
    values: new List<IReadOnlyList<SqlExpression>>
    {
        new List<SqlExpression>
        {
            new LiteralExpression("John Doe", SqlLiteralKind.String),
            new LiteralExpression("john@example.com", SqlLiteralKind.String),
            new LiteralExpression(25, SqlLiteralKind.Integer)
        }
    }
);

writer = new SqlWriter();
writer.WriteStatement(insertStmt);
Console.WriteLine(writer.GetResult());
```

### Writer Options

```csharp
var options = new SqlWriterOptions
{
    // Use line breaks and indentation (default: true)
    UsePrettyPrint = true,
    
    // Number of spaces per indent level (default: 2)
    IndentSize = 4,
    
    // Uppercase keywords (default: true)
    UppercaseKeywords = true
};

var writer = new SqlWriter(options);
```

## 📝 Examples

### Complex SELECT Query

```csharp
var sql = @"
SELECT 
    u.name,
    u.email,
    COUNT(o.id) AS order_count,
    SUM(o.total) AS total_spent,
    AVG(o.total) AS avg_order
FROM users u
INNER JOIN orders o ON u.id = o.user_id
WHERE o.status = 'completed'
  AND o.created_at >= '2024-01-01'
GROUP BY u.id, u.name, u.email
HAVING COUNT(o.id) >= 5
ORDER BY total_spent DESC
LIMIT 10 OFFSET 0
";

var result = SqlParser.ParseStatement(sql);
// Success! All parts of the query are parsed correctly
```

### CREATE TABLE with Constraints

```csharp
var sql = @"
CREATE TABLE IF NOT EXISTS orders (
    id INT PRIMARY KEY AUTO_INCREMENT,
    user_id INT NOT NULL,
    product_id INT NOT NULL,
    quantity INT DEFAULT 1,
    total DECIMAL(10, 2) NOT NULL,
    status VARCHAR(50) DEFAULT 'pending',
    created_at DATETIME DEFAULT NOW(),
    FOREIGN KEY (user_id) REFERENCES users(id),
    FOREIGN KEY (product_id) REFERENCES products(id),
    UNIQUE (user_id, product_id, created_at)
)
";

var result = SqlParser.ParseStatement(sql);
```

### CASE Expression

```csharp
var sql = @"
SELECT 
    name,
    age,
    CASE
        WHEN age < 18 THEN 'Minor'
        WHEN age >= 18 AND age < 65 THEN 'Adult'
        ELSE 'Senior'
    END AS age_group,
    CASE status
        WHEN 'active' THEN 1
        WHEN 'inactive' THEN 0
        ELSE -1
    END AS status_code
FROM users
";

var result = SqlParser.ParseStatement(sql);
```

### Subquery

```csharp
var sql = @"
SELECT name, email
FROM users
WHERE id IN (
    SELECT user_id 
    FROM orders 
    WHERE total > 1000 
      AND status = 'completed'
)
";

var result = SqlParser.ParseStatement(sql);
```

## 🏗️ Architecture

### AST Structure

The parser creates a hierarchical Abstract Syntax Tree (AST) with the following main node types:

- **SqlScript**: Root node containing multiple statements
- **Statements**: `SelectStatement`, `InsertStatement`, `UpdateStatement`, `DeleteStatement`, `CreateTableStatement`, etc.
- **Expressions**: `LiteralExpression`, `ColumnReference`, `BinaryExpression`, `UnaryExpression`, `FunctionCall`, `CaseExpression`, etc.
- **Table References**: `TableName`, `JoinClause`
- **Supporting Nodes**: `SelectColumn`, `OrderByClause`, `Assignment`, `ColumnDefinition`, `TableConstraint`, etc.

All AST nodes implement the `ISqlNode` marker interface.

### Parser Implementation

The parser is built using Parlot's fluent combinator API:

1. **Lexical Analysis**: Tokenization of keywords, identifiers, operators, literals
2. **Recursive Descent Parsing**: Top-down parsing with operator precedence
3. **Expression Parsing**: Proper handling of operator precedence and associativity
4. **Statement Parsing**: Dedicated parsers for each SQL statement type

### Writer Implementation

The SQL writer provides:

1. **AST Traversal**: Recursive visit of AST nodes
2. **Code Generation**: Conversion of AST back to SQL text
3. **Formatting**: Configurable indentation and keyword casing
4. **SQL Escaping**: Proper escaping of string literals

## ⚠️ Limitations

This is an educational implementation. For production use, consider:

- **Dialect Differences**: This parser targets standard SQL, but different databases (MySQL, PostgreSQL, SQL Server, etc.) have dialect-specific extensions
- **Comments**: SQL comments (`--` and `/* */`) are not currently parsed
- **Advanced Features**: Some advanced SQL features are not yet supported:
  - Window functions (OVER, PARTITION BY)
  - CTEs (WITH clause)
  - SET operations (UNION, INTERSECT, EXCEPT)
  - Advanced ALTER TABLE operations
  - Stored procedures, triggers, views
  - Transaction control (BEGIN, COMMIT, ROLLBACK)
  - Database and schema management

## 📚 Grammar Reference

See [SqlGrammarSpecification.txt](SqlGrammarSpecification.txt) for the complete EBNF grammar.

## 🧪 Testing

The parser is thoroughly tested with unit tests covering:

- All statement types (SELECT, INSERT, UPDATE, DELETE, CREATE TABLE, etc.)
- Expression parsing (literals, operators, functions, CASE, CAST)
- Complex queries (JOINs, subqueries, aggregates)
- Edge cases and error conditions
- Round-trip testing (parse → write → parse)

## 📖 References

- [SQL-92 Standard](https://www.contrib.andrew.cmu.edu/~shadow/sql/sql1992.txt)
- [SQL:2003 Standard](https://www.iso.org/standard/34132.html)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)
- [MySQL Documentation](https://dev.mysql.com/doc/)
- [SQL Server Documentation](https://docs.microsoft.com/en-us/sql/)
- [Parlot Parser Library](https://github.com/sebastienros/parlot)

## 🎯 Use Cases

This SQL parser can be used for:

1. **SQL Analysis**: Parse and analyze SQL queries programmatically
2. **Query Transformation**: Transform queries (e.g., adding WHERE clauses, changing table names)
3. **Code Generation**: Generate SQL from object models
4. **SQL Formatting**: Reformat SQL code with consistent style
5. **Educational Purposes**: Learn about parsing and SQL syntax
6. **Migration Tools**: Analyze and transform SQL during database migrations
7. **Query Builders**: Foundation for type-safe SQL query builders
8. **Database Tools**: Build SQL editors, validators, or analyzers

## 🤝 Contributing

This parser is part of the ParlotParsers educational project. Contributions are welcome!

## 📄 License

See the main project LICENSE file.

