using PaspanParsers.SQL2;

namespace PaspanParsers.Tests.SQL2;

[TestClass]
public class SqlParserTests
{
    // ========================================
    // Literals
    // ========================================

    [TestMethod]
    public void Parse_IntegerLiteral()
    {
        var result = SqlParser.ParseExpression("42");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<LiteralExpression>(result.Value);
        var lit = (LiteralExpression)result.Value;
        Assert.AreEqual(42, lit.Value);
        Assert.AreEqual(SqlLiteralKind.Integer, lit.Kind);
    }

    [TestMethod]
    public void Parse_FloatLiteral()
    {
        var result = SqlParser.ParseExpression("3.14");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<LiteralExpression>(result.Value);
        var lit = (LiteralExpression)result.Value;
        Assert.AreEqual(3.14, lit.Value);
        Assert.AreEqual(SqlLiteralKind.Float, lit.Kind);
    }

    [TestMethod]
    public void Parse_StringLiteral()
    {
        var result = SqlParser.ParseExpression("'hello'");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<LiteralExpression>(result.Value);
        var lit = (LiteralExpression)result.Value;
        Assert.AreEqual("hello", lit.Value);
        Assert.AreEqual(SqlLiteralKind.String, lit.Kind);
    }

    [TestMethod]
    public void Parse_StringLiteral_WithEscapedQuote()
    {
        var result = SqlParser.ParseExpression("'don''t'");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<LiteralExpression>(result.Value);
        var lit = (LiteralExpression)result.Value;
        Assert.AreEqual("don't", lit.Value);
    }

    [TestMethod]
    public void Parse_BooleanTrue()
    {
        var result = SqlParser.ParseExpression("TRUE");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<LiteralExpression>(result.Value);
        var lit = (LiteralExpression)result.Value;
        Assert.IsTrue((bool)lit.Value);
        Assert.AreEqual(SqlLiteralKind.Boolean, lit.Kind);
    }

    [TestMethod]
    public void Parse_BooleanFalse()
    {
        var result = SqlParser.ParseExpression("FALSE");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<LiteralExpression>(result.Value);
        var lit = (LiteralExpression)result.Value;
        Assert.IsFalse((bool)lit.Value);
        Assert.AreEqual(SqlLiteralKind.Boolean, lit.Kind);
    }

    [TestMethod]
    public void Parse_Null()
    {
        var result = SqlParser.ParseExpression("NULL");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<LiteralExpression>(result.Value);
        var lit = (LiteralExpression)result.Value;
        Assert.IsNull(lit.Value);
        Assert.AreEqual(SqlLiteralKind.Null, lit.Kind);
    }

    // ========================================
    // Column References
    // ========================================

    [TestMethod]
    public void Parse_ColumnReference()
    {
        var result = SqlParser.ParseExpression("name");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<ColumnReference>(result.Value);
        var col = (ColumnReference)result.Value;
        Assert.AreEqual("name", col.ColumnName);
        Assert.IsNull(col.TableName);
    }

    [TestMethod]
    public void Parse_QualifiedColumnReference()
    {
        var result = SqlParser.ParseExpression("users.name");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<ColumnReference>(result.Value);
        var col = (ColumnReference)result.Value;
        Assert.AreEqual("name", col.ColumnName);
        Assert.AreEqual("users", col.TableName);
    }

    [TestMethod]
    public void Parse_AllColumns()
    {
        var result = SqlParser.ParseExpression("*");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<AllColumnsExpression>(result.Value);
        var all = (AllColumnsExpression)result.Value;
        Assert.IsNull(all.TableName);
    }

    [TestMethod]
    public void Parse_QualifiedAllColumns()
    {
        var result = SqlParser.ParseExpression("users.*");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<AllColumnsExpression>(result.Value);
        var all = (AllColumnsExpression)result.Value;
        Assert.AreEqual("users", all.TableName);
    }

    // ========================================
    // Binary Expressions
    // ========================================

    [TestMethod]
    public void Parse_Addition()
    {
        var result = SqlParser.ParseExpression("1 + 2");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<BinaryExpression>(result.Value);
        var bin = (BinaryExpression)result.Value;
        Assert.AreEqual(SqlBinaryOperator.Add, bin.Operator);
    }

    [TestMethod]
    public void Parse_Multiplication()
    {
        var result = SqlParser.ParseExpression("price * quantity");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<BinaryExpression>(result.Value);
        var bin = (BinaryExpression)result.Value;
        Assert.AreEqual(SqlBinaryOperator.Multiply, bin.Operator);
    }

    [TestMethod]
    public void Parse_Comparison_Equal()
    {
        var result = SqlParser.ParseExpression("age = 18");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<BinaryExpression>(result.Value);
        var bin = (BinaryExpression)result.Value;
        Assert.AreEqual(SqlBinaryOperator.Equal, bin.Operator);
    }

    [TestMethod]
    public void Parse_Comparison_LessThan()
    {
        var result = SqlParser.ParseExpression("price < 100");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<BinaryExpression>(result.Value);
        var bin = (BinaryExpression)result.Value;
        Assert.AreEqual(SqlBinaryOperator.LessThan, bin.Operator);
    }

    [TestMethod]
    public void Parse_LogicalAnd()
    {
        var result = SqlParser.ParseExpression("age >= 18 AND age < 65");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<BinaryExpression>(result.Value);
        var bin = (BinaryExpression)result.Value;
        Assert.AreEqual(SqlBinaryOperator.And, bin.Operator);
    }

    [TestMethod]
    public void Parse_LogicalOr()
    {
        var result = SqlParser.ParseExpression("status = 'active' OR status = 'pending'");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<BinaryExpression>(result.Value);
        var bin = (BinaryExpression)result.Value;
        Assert.AreEqual(SqlBinaryOperator.Or, bin.Operator);
    }

    [TestMethod]
    public void Parse_Like()
    {
        var result = SqlParser.ParseExpression("name LIKE 'John%'");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<BinaryExpression>(result.Value);
        var bin = (BinaryExpression)result.Value;
        Assert.AreEqual(SqlBinaryOperator.Like, bin.Operator);
    }

    // ========================================
    // Unary Expressions
    // ========================================

    [TestMethod]
    public void Parse_Not()
    {
        var result = SqlParser.ParseExpression("NOT active");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<UnaryExpression>(result.Value);
        var unary = (UnaryExpression)result.Value;
        Assert.AreEqual(SqlUnaryOperator.Not, unary.Operator);
    }

    [TestMethod]
    public void Parse_IsNull()
    {
        var result = SqlParser.ParseExpression("email IS NULL");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<UnaryExpression>(result.Value);
        var unary = (UnaryExpression)result.Value;
        Assert.AreEqual(SqlUnaryOperator.IsNull, unary.Operator);
    }

    [TestMethod]
    public void Parse_IsNotNull()
    {
        var result = SqlParser.ParseExpression("email IS NOT NULL");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<UnaryExpression>(result.Value);
        var unary = (UnaryExpression)result.Value;
        Assert.AreEqual(SqlUnaryOperator.IsNotNull, unary.Operator);
    }

    // ========================================
    // Function Calls
    // ========================================

    [TestMethod]
    public void Parse_FunctionCall_NoArgs()
    {
        var result = SqlParser.ParseExpression("NOW()");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<FunctionCall>(result.Value);
        var func = (FunctionCall)result.Value;
        Assert.AreEqual("NOW", func.Name);
        Assert.IsEmpty(func.Arguments);
    }

    [TestMethod]
    public void Parse_FunctionCall_OneArg()
    {
        var result = SqlParser.ParseExpression("COUNT(*)");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<FunctionCall>(result.Value);
        var func = (FunctionCall)result.Value;
        Assert.AreEqual("COUNT", func.Name);
        Assert.HasCount(1, func.Arguments);
    }

    [TestMethod]
    public void Parse_FunctionCall_MultipleArgs()
    {
        var result = SqlParser.ParseExpression("SUBSTRING(name, 1, 10)");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<FunctionCall>(result.Value);
        var func = (FunctionCall)result.Value;
        Assert.AreEqual("SUBSTRING", func.Name);
        Assert.HasCount(3, func.Arguments);
    }

    [TestMethod]
    public void Parse_FunctionCall_Distinct()
    {
        var result = SqlParser.ParseExpression("COUNT(DISTINCT user_id)");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<FunctionCall>(result.Value);
        var func = (FunctionCall)result.Value;
        Assert.AreEqual("COUNT", func.Name);
        Assert.IsTrue(func.Distinct);
    }

    // ========================================
    // BETWEEN Expression
    // ========================================

    [TestMethod]
    public void Parse_Between()
    {
        var result = SqlParser.ParseExpression("price BETWEEN 10 AND 100");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<BetweenExpression>(result.Value);
        var between = (BetweenExpression)result.Value;
        Assert.IsFalse(between.NotBetween);
    }

    [TestMethod]
    public void Parse_NotBetween()
    {
        var result = SqlParser.ParseExpression("age NOT BETWEEN 18 AND 65");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<BetweenExpression>(result.Value);
        var between = (BetweenExpression)result.Value;
        Assert.IsTrue(between.NotBetween);
    }

    // ========================================
    // IN Expression
    // ========================================

    [TestMethod]
    public void Parse_In()
    {
        var result = SqlParser.ParseExpression("status IN ('active', 'pending')");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<BinaryExpression>(result.Value);
        var bin = (BinaryExpression)result.Value;
        Assert.AreEqual(SqlBinaryOperator.In, bin.Operator);
        Assert.IsInstanceOfType<ListExpression>(bin.Right);
        var list = (ListExpression)bin.Right;
        Assert.HasCount(2, list.Items);
    }

    [TestMethod]
    public void Parse_NotIn()
    {
        var result = SqlParser.ParseExpression("id NOT IN (1, 2, 3)");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<BinaryExpression>(result.Value);
        var bin = (BinaryExpression)result.Value;
        Assert.AreEqual(SqlBinaryOperator.NotIn, bin.Operator);
    }

    // ========================================
    // CASE Expression
    // ========================================

    [TestMethod]
    public void Parse_CaseExpression_Simple()
    {
        var sql = "CASE status WHEN 'active' THEN 1 WHEN 'inactive' THEN 0 ELSE -1 END";
        var result = SqlParser.ParseExpression(sql);
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<CaseExpression>(result.Value);
        var caseExpr = (CaseExpression)result.Value;
        Assert.IsNotNull(caseExpr.TestExpression);
        Assert.HasCount(2, caseExpr.WhenClauses);
        Assert.IsNotNull(caseExpr.ElseClause);
    }

    [TestMethod]
    public void Parse_CaseExpression_Searched()
    {
        var sql = "CASE WHEN age < 18 THEN 'minor' WHEN age >= 18 THEN 'adult' END";
        var result = SqlParser.ParseExpression(sql);
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<CaseExpression>(result.Value);
        var caseExpr = (CaseExpression)result.Value;
        Assert.IsNull(caseExpr.TestExpression);
        Assert.HasCount(2, caseExpr.WhenClauses);
    }

    // ========================================
    // CAST Expression
    // ========================================

    [TestMethod]
    public void Parse_CastExpression()
    {
        var result = SqlParser.ParseExpression("CAST(price AS INTEGER)");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<CastExpression>(result.Value);
        var cast = (CastExpression)result.Value;
        Assert.IsInstanceOfType<ColumnReference>(cast.Expression);
        Assert.AreEqual("INTEGER", cast.TargetType.Name);
    }

    // ========================================
    // SELECT Statement
    // ========================================

    [TestMethod]
    public void Parse_Select_AllColumns()
    {
        var result = SqlParser.ParseStatement("SELECT * FROM users");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<SelectStatement>(result.Value);
        var select = (SelectStatement)result.Value;
        Assert.HasCount(1, select.Columns);
        Assert.IsInstanceOfType<AllColumnsExpression>(select.Columns[0].Expression);
    }

    [TestMethod]
    public void Parse_Select_SpecificColumns()
    {
        var result = SqlParser.ParseStatement("SELECT id, name, email FROM users");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<SelectStatement>(result.Value);
        var select = (SelectStatement)result.Value;
        Assert.HasCount(3, select.Columns);
    }

    [TestMethod]
    public void Parse_Select_WithAlias()
    {
        var result = SqlParser.ParseStatement("SELECT id, name AS user_name FROM users");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<SelectStatement>(result.Value);
        var select = (SelectStatement)result.Value;
        Assert.HasCount(2, select.Columns);
        Assert.AreEqual("user_name", select.Columns[1].Alias);
    }

    [TestMethod]
    public void Parse_Select_Distinct()
    {
        var result = SqlParser.ParseStatement("SELECT DISTINCT category FROM products");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<SelectStatement>(result.Value);
        var select = (SelectStatement)result.Value;
        Assert.IsTrue(select.Distinct);
    }

    [TestMethod]
    public void Parse_Select_WithWhere()
    {
        var result = SqlParser.ParseStatement("SELECT * FROM users WHERE age >= 18");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<SelectStatement>(result.Value);
        var select = (SelectStatement)result.Value;
        Assert.IsNotNull(select.Where);
    }

    [TestMethod]
    public void Parse_Select_WithOrderBy()
    {
        var result = SqlParser.ParseStatement("SELECT * FROM users ORDER BY name ASC, age DESC");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<SelectStatement>(result.Value);
        var select = (SelectStatement)result.Value;
        Assert.IsNotNull(select.OrderBy);
        Assert.HasCount(2, select.OrderBy);
        Assert.IsTrue(select.OrderBy[0].Ascending);
        Assert.IsFalse(select.OrderBy[1].Ascending);
    }

    [TestMethod]
    public void Parse_Select_WithGroupBy()
    {
        var result = SqlParser.ParseStatement("SELECT category, COUNT(*) FROM products GROUP BY category");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<SelectStatement>(result.Value);
        var select = (SelectStatement)result.Value;
        Assert.IsNotNull(select.GroupBy);
        Assert.HasCount(1, select.GroupBy);
    }

    [TestMethod]
    public void Parse_Select_WithHaving()
    {
        var result = SqlParser.ParseStatement(
            "SELECT category, COUNT(*) FROM products GROUP BY category HAVING COUNT(*) > 5");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<SelectStatement>(result.Value);
        var select = (SelectStatement)result.Value;
        Assert.IsNotNull(select.Having);
    }

    [TestMethod]
    public void Parse_Select_WithLimit()
    {
        var result = SqlParser.ParseStatement("SELECT * FROM users LIMIT 10");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<SelectStatement>(result.Value);
        var select = (SelectStatement)result.Value;
        Assert.AreEqual(10, select.Limit);
    }

    [TestMethod]
    public void Parse_Select_WithLimitAndOffset()
    {
        var result = SqlParser.ParseStatement("SELECT * FROM users LIMIT 10 OFFSET 20");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<SelectStatement>(result.Value);
        var select = (SelectStatement)result.Value;
        Assert.AreEqual(10, select.Limit);
        Assert.AreEqual(20, select.Offset);
    }

    [TestMethod]
    public void Parse_Select_WithJoin()
    {
        var sql = "SELECT u.name, o.total FROM users u INNER JOIN orders o ON u.id = o.user_id";
        var result = SqlParser.ParseStatement(sql);
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<SelectStatement>(result.Value);
        var select = (SelectStatement)result.Value;
        Assert.IsNotNull(select.From);
        Assert.HasCount(1, select.From);
        Assert.IsInstanceOfType<JoinClause>(select.From[0]);
    }

    [TestMethod]
    public void Parse_Select_ComplexQuery()
    {
        var sql = @"
            SELECT u.name, COUNT(o.id) AS order_count, SUM(o.total) AS total_spent
            FROM users u
            INNER JOIN orders o ON u.id = o.user_id
            WHERE o.status = 'completed' AND o.created_at >= '2024-01-01'
            GROUP BY u.id, u.name
            HAVING COUNT(o.id) >= 5
            ORDER BY total_spent DESC
            LIMIT 10 OFFSET 0
        ";
        var result = SqlParser.ParseStatement(sql);
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<SelectStatement>(result.Value);
        var select = (SelectStatement)result.Value;
        Assert.HasCount(3, select.Columns);
        Assert.IsNotNull(select.From);
        Assert.IsNotNull(select.Where);
        Assert.IsNotNull(select.GroupBy);
        Assert.IsNotNull(select.Having);
        Assert.IsNotNull(select.OrderBy);
        Assert.AreEqual(10, select.Limit);
        Assert.AreEqual(0, select.Offset);
    }

    // ========================================
    // INSERT Statement
    // ========================================

    [TestMethod]
    public void Parse_Insert_Simple()
    {
        var result = SqlParser.ParseStatement("INSERT INTO users (name, email) VALUES ('John', 'john@example.com')");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<InsertStatement>(result.Value);
        var insert = (InsertStatement)result.Value;
        Assert.AreEqual("users", insert.TableName);
        Assert.HasCount(2, insert.Columns);
        Assert.HasCount(1, insert.Values);
        Assert.HasCount(2, insert.Values[0]);
    }

    [TestMethod]
    public void Parse_Insert_MultipleRows()
    {
        var sql = "INSERT INTO users (name, email) VALUES ('John', 'john@example.com'), ('Jane', 'jane@example.com')";
        var result = SqlParser.ParseStatement(sql);
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<InsertStatement>(result.Value);
        var insert = (InsertStatement)result.Value;
        Assert.HasCount(2, insert.Values);
    }

    // ========================================
    // UPDATE Statement
    // ========================================

    [TestMethod]
    public void Parse_Update_Simple()
    {
        var result = SqlParser.ParseStatement("UPDATE users SET name = 'John Doe' WHERE id = 1");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<UpdateStatement>(result.Value);
        var update = (UpdateStatement)result.Value;
        Assert.AreEqual("users", update.TableName);
        Assert.HasCount(1, update.Assignments);
        Assert.IsNotNull(update.Where);
    }

    [TestMethod]
    public void Parse_Update_MultipleColumns()
    {
        var result = SqlParser.ParseStatement("UPDATE users SET name = 'John', age = 25, email = 'john@example.com' WHERE id = 1");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<UpdateStatement>(result.Value);
        var update = (UpdateStatement)result.Value;
        Assert.HasCount(3, update.Assignments);
    }

    // ========================================
    // DELETE Statement
    // ========================================

    [TestMethod]
    public void Parse_Delete_WithWhere()
    {
        var result = SqlParser.ParseStatement("DELETE FROM users WHERE id = 1");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<DeleteStatement>(result.Value);
        var delete = (DeleteStatement)result.Value;
        Assert.AreEqual("users", delete.TableName);
        Assert.IsNotNull(delete.Where);
    }

    [TestMethod]
    public void Parse_Delete_WithoutWhere()
    {
        var result = SqlParser.ParseStatement("DELETE FROM users");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<DeleteStatement>(result.Value);
        var delete = (DeleteStatement)result.Value;
        Assert.IsNull(delete.Where);
    }

    // ========================================
    // CREATE TABLE Statement
    // ========================================

    [TestMethod]
    public void Parse_CreateTable_Simple()
    {
        var sql = "CREATE TABLE users (id INT PRIMARY KEY, name VARCHAR(100) NOT NULL)";
        var result = SqlParser.ParseStatement(sql);
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<CreateTableStatement>(result.Value);
        var create = (CreateTableStatement)result.Value;
        Assert.AreEqual("users", create.TableName);
        Assert.HasCount(2, create.Columns);
    }

    [TestMethod]
    public void Parse_CreateTable_IfNotExists()
    {
        var sql = "CREATE TABLE IF NOT EXISTS users (id INT)";
        var result = SqlParser.ParseStatement(sql);
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<CreateTableStatement>(result.Value);
        var create = (CreateTableStatement)result.Value;
        Assert.IsTrue(create.IfNotExists);
    }

    [TestMethod]
    public void Parse_CreateTable_WithConstraints()
    {
        var sql = @"
            CREATE TABLE orders (
                id INT PRIMARY KEY AUTO_INCREMENT,
                user_id INT NOT NULL,
                total DECIMAL(10, 2),
                FOREIGN KEY (user_id) REFERENCES users(id)
            )
        ";
        var result = SqlParser.ParseStatement(sql);
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<CreateTableStatement>(result.Value);
        var create = (CreateTableStatement)result.Value;
        Assert.HasCount(3, create.Columns);
        Assert.IsNotNull(create.Constraints);
        Assert.HasCount(1, create.Constraints);
        Assert.IsInstanceOfType<ForeignKeyConstraint>(create.Constraints[0]);
    }

    // ========================================
    // DROP TABLE Statement
    // ========================================

    [TestMethod]
    public void Parse_DropTable()
    {
        var result = SqlParser.ParseStatement("DROP TABLE users");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<DropTableStatement>(result.Value);
        var drop = (DropTableStatement)result.Value;
        Assert.AreEqual("users", drop.TableName);
        Assert.IsFalse(drop.IfExists);
    }

    [TestMethod]
    public void Parse_DropTable_IfExists()
    {
        var result = SqlParser.ParseStatement("DROP TABLE IF EXISTS users");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<DropTableStatement>(result.Value);
        var drop = (DropTableStatement)result.Value;
        Assert.IsTrue(drop.IfExists);
    }

    // ========================================
    // CREATE INDEX Statement
    // ========================================

    [TestMethod]
    public void Parse_CreateIndex()
    {
        var result = SqlParser.ParseStatement("CREATE INDEX idx_email ON users(email)");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<CreateIndexStatement>(result.Value);
        var create = (CreateIndexStatement)result.Value;
        Assert.AreEqual("idx_email", create.IndexName);
        Assert.AreEqual("users", create.TableName);
        Assert.HasCount(1, create.Columns);
    }

    [TestMethod]
    public void Parse_CreateIndex_Unique()
    {
        var result = SqlParser.ParseStatement("CREATE UNIQUE INDEX idx_email ON users(email)");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<CreateIndexStatement>(result.Value);
        var create = (CreateIndexStatement)result.Value;
        Assert.IsTrue(create.Unique);
    }

    // ========================================
    // DROP INDEX Statement
    // ========================================

    [TestMethod]
    public void Parse_DropIndex()
    {
        var result = SqlParser.ParseStatement("DROP INDEX idx_email");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<DropIndexStatement>(result.Value);
        var drop = (DropIndexStatement)result.Value;
        Assert.AreEqual("idx_email", drop.IndexName);
    }

    // ========================================
    // Script (Multiple Statements)
    // ========================================

    [TestMethod]
    public void Parse_Script_MultipleStatements()
    {
        var sql = @"
            CREATE TABLE users (id INT PRIMARY KEY, name VARCHAR(100));
            INSERT INTO users (id, name) VALUES (1, 'John');
            SELECT * FROM users;
        ";
        var result = SqlParser.Parse(sql);
        Assert.IsTrue(result.Success);
        Assert.IsNotNull(result.Value);
        Assert.HasCount(3, result.Value.Statements);
        Assert.IsInstanceOfType<CreateTableStatement>(result.Value.Statements[0]);
        Assert.IsInstanceOfType<InsertStatement>(result.Value.Statements[1]);
        Assert.IsInstanceOfType<SelectStatement>(result.Value.Statements[2]);
    }
}

