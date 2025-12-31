using PaspanParsers.SQL2;

namespace PaspanParsers.Tests.SQL2;

[TestClass]
public class SqlWriterTests
{
    // ========================================
    // SELECT Statement
    // ========================================

    [TestMethod]
    public void Write_Select_AllColumns()
    {
        var select = new SelectStatement(
            distinct: false,
            columns:
            [
                new SelectColumn(new AllColumnsExpression())
            ],
            from:
            [
                new TableName("users")
            ],
            where: null,
            groupBy: null,
            having: null,
            orderBy: null,
            limit: null,
            offset: null
        );

        var writer = new SqlWriter(new SqlWriterOptions { UsePrettyPrint = false });
        writer.WriteStatement(select);
        var result = writer.GetResult();

        Assert.AreEqual("SELECT * FROM users", result);
    }

    [TestMethod]
    public void Write_Select_SpecificColumns()
    {
        var select = new SelectStatement(
            distinct: false,
            columns:
            [
                new SelectColumn(new ColumnReference("id")),
                new SelectColumn(new ColumnReference("name")),
                new SelectColumn(new ColumnReference("email"))
            ],
            from:
            [
                new TableName("users")
            ],
            where: null,
            groupBy: null,
            having: null,
            orderBy: null,
            limit: null,
            offset: null
        );

        var writer = new SqlWriter(new SqlWriterOptions { UsePrettyPrint = false });
        writer.WriteStatement(select);
        var result = writer.GetResult();

        Assert.AreEqual("SELECT id, name, email FROM users", result);
    }

    [TestMethod]
    public void Write_Select_WithAlias()
    {
        var select = new SelectStatement(
            distinct: false,
            columns:
            [
                new SelectColumn(new ColumnReference("name"), "user_name")
            ],
            from:
            [
                new TableName("users")
            ],
            where: null,
            groupBy: null,
            having: null,
            orderBy: null,
            limit: null,
            offset: null
        );

        var writer = new SqlWriter(new SqlWriterOptions { UsePrettyPrint = false });
        writer.WriteStatement(select);
        var result = writer.GetResult();

        Assert.AreEqual("SELECT name AS user_name FROM users", result);
    }

    [TestMethod]
    public void Write_Select_Distinct()
    {
        var select = new SelectStatement(
            distinct: true,
            columns:
            [
                new SelectColumn(new ColumnReference("category"))
            ],
            from:
            [
                new TableName("products")
            ],
            where: null,
            groupBy: null,
            having: null,
            orderBy: null,
            limit: null,
            offset: null
        );

        var writer = new SqlWriter(new SqlWriterOptions { UsePrettyPrint = false });
        writer.WriteStatement(select);
        var result = writer.GetResult();

        Assert.AreEqual("SELECT DISTINCT category FROM products", result);
    }

    [TestMethod]
    public void Write_Select_WithWhere()
    {
        var select = new SelectStatement(
            distinct: false,
            columns:
            [
                new SelectColumn(new AllColumnsExpression())
            ],
            from:
            [
                new TableName("users")
            ],
            where: new BinaryExpression(
                new ColumnReference("age"),
                SqlBinaryOperator.GreaterThanOrEqual,
                new LiteralExpression(18, SqlLiteralKind.Integer)
            ),
            groupBy: null,
            having: null,
            orderBy: null,
            limit: null,
            offset: null
        );

        var writer = new SqlWriter(new SqlWriterOptions { UsePrettyPrint = false });
        writer.WriteStatement(select);
        var result = writer.GetResult();

        Assert.AreEqual("SELECT * FROM users WHERE age >= 18", result);
    }

    [TestMethod]
    public void Write_Select_WithOrderBy()
    {
        var select = new SelectStatement(
            distinct: false,
            columns:
            [
                new SelectColumn(new AllColumnsExpression())
            ],
            from:
            [
                new TableName("users")
            ],
            where: null,
            groupBy: null,
            having: null,
            orderBy:
            [
                new OrderByClause(new ColumnReference("name"), ascending: true)
            ],
            limit: null,
            offset: null
        );

        var writer = new SqlWriter(new SqlWriterOptions { UsePrettyPrint = false });
        writer.WriteStatement(select);
        var result = writer.GetResult();

        Assert.AreEqual("SELECT * FROM users ORDER BY name ASC", result);
    }

    // ========================================
    // INSERT Statement
    // ========================================

    [TestMethod]
    public void Write_Insert()
    {
        var insert = new InsertStatement(
            tableName: "users",
            columns: ["name", "email"],
            values:
            [
                [
                    new LiteralExpression("John", SqlLiteralKind.String),
                    new LiteralExpression("john@example.com", SqlLiteralKind.String)
                ]
            ]
        );

        var writer = new SqlWriter(new SqlWriterOptions { UsePrettyPrint = false });
        writer.WriteStatement(insert);
        var result = writer.GetResult();

        Assert.AreEqual("INSERT INTO users (name, email) VALUES ('John', 'john@example.com')", result);
    }

    // ========================================
    // UPDATE Statement
    // ========================================

    [TestMethod]
    public void Write_Update()
    {
        var update = new UpdateStatement(
            tableName: "users",
            assignments:
            [
                new Assignment("name", new LiteralExpression("John Doe", SqlLiteralKind.String))
            ],
            where: new BinaryExpression(
                new ColumnReference("id"),
                SqlBinaryOperator.Equal,
                new LiteralExpression(1, SqlLiteralKind.Integer)
            )
        );

        var writer = new SqlWriter(new SqlWriterOptions { UsePrettyPrint = false });
        writer.WriteStatement(update);
        var result = writer.GetResult();

        Assert.AreEqual("UPDATE users SET name = 'John Doe' WHERE id = 1", result);
    }

    // ========================================
    // DELETE Statement
    // ========================================

    [TestMethod]
    public void Write_Delete()
    {
        var delete = new DeleteStatement(
            tableName: "users",
            where: new BinaryExpression(
                new ColumnReference("id"),
                SqlBinaryOperator.Equal,
                new LiteralExpression(1, SqlLiteralKind.Integer)
            )
        );

        var writer = new SqlWriter(new SqlWriterOptions { UsePrettyPrint = false });
        writer.WriteStatement(delete);
        var result = writer.GetResult();

        Assert.AreEqual("DELETE FROM users WHERE id = 1", result);
    }

    // ========================================
    // CREATE TABLE Statement
    // ========================================

    [TestMethod]
    public void Write_CreateTable()
    {
        var create = new CreateTableStatement(
            tableName: "users",
            columns:
            [
                new ColumnDefinition("id", new SqlDataType("INT"), nullable: false, primaryKey: true),
                new ColumnDefinition("name", new SqlDataType("VARCHAR", 100), nullable: false)
            ],
            constraints: null,
            ifNotExists: false
        );

        var writer = new SqlWriter(new SqlWriterOptions { UsePrettyPrint = false });
        writer.WriteStatement(create);
        var result = writer.GetResult();

        Assert.Contains("CREATE TABLE users", result);
        Assert.Contains("id INT NOT NULL PRIMARY KEY", result);
        Assert.Contains("name VARCHAR(100) NOT NULL", result);
    }

    // ========================================
    // DROP TABLE Statement
    // ========================================

    [TestMethod]
    public void Write_DropTable()
    {
        var drop = new DropTableStatement("users", ifExists: false);

        var writer = new SqlWriter();
        writer.WriteStatement(drop);
        var result = writer.GetResult();

        Assert.AreEqual("DROP TABLE users", result);
    }

    [TestMethod]
    public void Write_DropTable_IfExists()
    {
        var drop = new DropTableStatement("users", ifExists: true);

        var writer = new SqlWriter();
        writer.WriteStatement(drop);
        var result = writer.GetResult();

        Assert.AreEqual("DROP TABLE IF EXISTS users", result);
    }

    // ========================================
    // CREATE INDEX Statement
    // ========================================

    [TestMethod]
    public void Write_CreateIndex()
    {
        var create = new CreateIndexStatement(
            indexName: "idx_email",
            tableName: "users",
            columns: ["email"],
            unique: false,
            ifNotExists: false
        );

        var writer = new SqlWriter();
        writer.WriteStatement(create);
        var result = writer.GetResult();

        Assert.AreEqual("CREATE INDEX idx_email ON users (email)", result);
    }

    [TestMethod]
    public void Write_CreateIndex_Unique()
    {
        var create = new CreateIndexStatement(
            indexName: "idx_email",
            tableName: "users",
            columns: ["email"],
            unique: true,
            ifNotExists: false
        );

        var writer = new SqlWriter();
        writer.WriteStatement(create);
        var result = writer.GetResult();

        Assert.AreEqual("CREATE UNIQUE INDEX idx_email ON users (email)", result);
    }

    // ========================================
    // DROP INDEX Statement
    // ========================================

    [TestMethod]
    public void Write_DropIndex()
    {
        var drop = new DropIndexStatement("idx_email", ifExists: false);

        var writer = new SqlWriter();
        writer.WriteStatement(drop);
        var result = writer.GetResult();

        Assert.AreEqual("DROP INDEX idx_email", result);
    }

    // ========================================
    // Expressions
    // ========================================

    [TestMethod]
    public void Write_BinaryExpression()
    {
        var expr = new BinaryExpression(
            new LiteralExpression(1, SqlLiteralKind.Integer),
            SqlBinaryOperator.Add,
            new LiteralExpression(2, SqlLiteralKind.Integer)
        );

        var writer = new SqlWriter();
        writer.WriteExpression(expr);
        var result = writer.GetResult();

        Assert.AreEqual("1 + 2", result);
    }

    [TestMethod]
    public void Write_FunctionCall()
    {
        var func = new FunctionCall(
            "COUNT",
            [new AllColumnsExpression()],
            distinct: false
        );

        var writer = new SqlWriter();
        writer.WriteExpression(func);
        var result = writer.GetResult();

        Assert.AreEqual("COUNT(*)", result);
    }

    [TestMethod]
    public void Write_CaseExpression()
    {
        var caseExpr = new CaseExpression(
            testExpression: null,
            whenClauses:
            [
                new WhenClause(
                    new BinaryExpression(
                        new ColumnReference("age"),
                        SqlBinaryOperator.LessThan,
                        new LiteralExpression(18, SqlLiteralKind.Integer)
                    ),
                    new LiteralExpression("minor", SqlLiteralKind.String)
                ),
                new WhenClause(
                    new BinaryExpression(
                        new ColumnReference("age"),
                        SqlBinaryOperator.GreaterThanOrEqual,
                        new LiteralExpression(18, SqlLiteralKind.Integer)
                    ),
                    new LiteralExpression("adult", SqlLiteralKind.String)
                )
            ],
            elseClause: null
        );

        var writer = new SqlWriter();
        writer.WriteExpression(caseExpr);
        var result = writer.GetResult();

        Assert.Contains("CASE", result);
        Assert.Contains("WHEN", result);
        Assert.Contains("THEN", result);
        Assert.Contains("END", result);
    }

    // ========================================
    // Round-trip Tests (Parse → Write → Parse)
    // ========================================

    [TestMethod]
    public void RoundTrip_SimpleSelect()
    {
        var sql = "SELECT * FROM users";
        var parseResult = SqlParser.ParseStatement(sql);
        Assert.IsTrue(parseResult.Success);

        var writer = new SqlWriter(new SqlWriterOptions { UsePrettyPrint = false });
        writer.WriteStatement(parseResult.Value);
        var generatedSql = writer.GetResult();

        var parseResult2 = SqlParser.ParseStatement(generatedSql);
        Assert.IsTrue(parseResult2.Success);
    }

    [TestMethod]
    public void RoundTrip_SelectWithWhere()
    {
        var sql = "SELECT id, name FROM users WHERE age >= 18";
        var parseResult = SqlParser.ParseStatement(sql);
        Assert.IsTrue(parseResult.Success);

        var writer = new SqlWriter(new SqlWriterOptions { UsePrettyPrint = false });
        writer.WriteStatement(parseResult.Value);
        var generatedSql = writer.GetResult();

        var parseResult2 = SqlParser.ParseStatement(generatedSql);
        Assert.IsTrue(parseResult2.Success);
    }

    [TestMethod]
    public void RoundTrip_Insert()
    {
        var sql = "INSERT INTO users (name, email) VALUES ('John', 'john@example.com')";
        var parseResult = SqlParser.ParseStatement(sql);
        Assert.IsTrue(parseResult.Success);

        var writer = new SqlWriter(new SqlWriterOptions { UsePrettyPrint = false });
        writer.WriteStatement(parseResult.Value);
        var generatedSql = writer.GetResult();

        var parseResult2 = SqlParser.ParseStatement(generatedSql);
        Assert.IsTrue(parseResult2.Success);
    }

    [TestMethod]
    public void RoundTrip_Update()
    {
        var sql = "UPDATE users SET name = 'John Doe' WHERE id = 1";
        var parseResult = SqlParser.ParseStatement(sql);
        Assert.IsTrue(parseResult.Success);

        var writer = new SqlWriter(new SqlWriterOptions { UsePrettyPrint = false });
        writer.WriteStatement(parseResult.Value);
        var generatedSql = writer.GetResult();

        var parseResult2 = SqlParser.ParseStatement(generatedSql);
        Assert.IsTrue(parseResult2.Success);
    }

    [TestMethod]
    public void RoundTrip_Delete()
    {
        var sql = "DELETE FROM users WHERE id = 1";
        var parseResult = SqlParser.ParseStatement(sql);
        Assert.IsTrue(parseResult.Success);

        var writer = new SqlWriter(new SqlWriterOptions { UsePrettyPrint = false });
        writer.WriteStatement(parseResult.Value);
        var generatedSql = writer.GetResult();

        var parseResult2 = SqlParser.ParseStatement(generatedSql);
        Assert.IsTrue(parseResult2.Success);
    }

    [TestMethod]
    public void RoundTrip_CreateTable()
    {
        var sql = "CREATE TABLE users (id INT PRIMARY KEY, name VARCHAR(100) NOT NULL)";
        var parseResult = SqlParser.ParseStatement(sql);
        Assert.IsTrue(parseResult.Success);

        var writer = new SqlWriter(new SqlWriterOptions { UsePrettyPrint = false });
        writer.WriteStatement(parseResult.Value);
        var generatedSql = writer.GetResult();

        var parseResult2 = SqlParser.ParseStatement(generatedSql);
        Assert.IsTrue(parseResult2.Success);
    }

    [TestMethod]
    public void RoundTrip_ComplexSelect()
    {
        var sql = "SELECT u.name, COUNT(o.id) AS order_count FROM users u INNER JOIN orders o ON u.id = o.user_id WHERE o.status = 'completed' GROUP BY u.name HAVING COUNT(o.id) >= 5 ORDER BY order_count DESC LIMIT 10";
        var parseResult = SqlParser.ParseStatement(sql);
        Assert.IsTrue(parseResult.Success);

        var writer = new SqlWriter(new SqlWriterOptions { UsePrettyPrint = false });
        writer.WriteStatement(parseResult.Value);
        var generatedSql = writer.GetResult();

        var parseResult2 = SqlParser.ParseStatement(generatedSql);
        Assert.IsTrue(parseResult2.Success);
    }

    // ========================================
    // Writer Options Tests
    // ========================================

    [TestMethod]
    public void WriterOptions_UppercaseKeywords()
    {
        var select = new SelectStatement(
            distinct: false,
            columns: [new SelectColumn(new AllColumnsExpression())],
            from: [new TableName("users")],
            where: null,
            groupBy: null,
            having: null,
            orderBy: null,
            limit: null,
            offset: null
        );

        var writer = new SqlWriter(new SqlWriterOptions 
        { 
            UsePrettyPrint = false,
            UppercaseKeywords = true 
        });
        writer.WriteStatement(select);
        var result = writer.GetResult();

        Assert.Contains("SELECT", result);
        Assert.Contains("FROM", result);
    }

    [TestMethod]
    public void WriterOptions_LowercaseKeywords()
    {
        var select = new SelectStatement(
            distinct: false,
            columns: [new SelectColumn(new AllColumnsExpression())],
            from: [new TableName("users")],
            where: null,
            groupBy: null,
            having: null,
            orderBy: null,
            limit: null,
            offset: null
        );

        var writer = new SqlWriter(new SqlWriterOptions 
        { 
            UsePrettyPrint = false,
            UppercaseKeywords = false 
        });
        writer.WriteStatement(select);
        var result = writer.GetResult();

        Assert.Contains("select", result);
        Assert.Contains("from", result);
    }

    [TestMethod]
    public void WriterOptions_PrettyPrint()
    {
        var select = new SelectStatement(
            distinct: false,
            columns:
            [
                new SelectColumn(new ColumnReference("id")),
                new SelectColumn(new ColumnReference("name"))
            ],
            from: [new TableName("users")],
            where: new BinaryExpression(
                new ColumnReference("age"),
                SqlBinaryOperator.GreaterThanOrEqual,
                new LiteralExpression(18, SqlLiteralKind.Integer)
            ),
            groupBy: null,
            having: null,
            orderBy: null,
            limit: null,
            offset: null
        );

        var writer = new SqlWriter(new SqlWriterOptions 
        { 
            UsePrettyPrint = true,
            IndentSize = 2
        });
        writer.WriteStatement(select);
        var result = writer.GetResult();

        // Pretty print should include newlines
        Assert.IsTrue(result.Contains('\n') || result.Contains("\r\n"));
    }
}

