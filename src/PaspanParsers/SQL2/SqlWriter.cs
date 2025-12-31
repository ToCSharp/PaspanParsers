using System.Text;

namespace PaspanParsers.SQL2;

/// <summary>
/// SQL code generator that converts SQL AST back to SQL code.
/// Supports standard SQL syntax with customizable formatting.
/// </summary>
public class SqlWriter(SqlWriterOptions options = null)
{
    private readonly StringBuilder _builder = new();
    private readonly SqlWriterOptions _options = options ?? new SqlWriterOptions();
    private int _indentLevel = 0;

    public string GetResult() => _builder.ToString();

    private void Write(string text) => _builder.Append(text);
    private void WriteLine() => _builder.AppendLine();
    
    private void WriteIndent()
    {
        if (_options.UsePrettyPrint)
        {
            _builder.Append(new string(' ', _indentLevel * _options.IndentSize));
        }
    }

    private void Indent() => _indentLevel++;
    private void Dedent() => _indentLevel = Math.Max(0, _indentLevel - 1);

    private void WriteKeyword(string keyword)
    {
        Write(_options.UppercaseKeywords ? keyword.ToUpperInvariant() : keyword.ToLowerInvariant());
    }

    // ========================================
    // Public API
    // ========================================

    public void WriteScript(SqlScript script)
    {
        for (int i = 0; i < script.Statements.Count; i++)
        {
            if (i > 0 && _options.UsePrettyPrint)
            {
                WriteLine();
            }
            WriteStatement(script.Statements[i]);
            Write(";");
            if (_options.UsePrettyPrint)
            {
                WriteLine();
            }
        }
    }

    public void WriteStatement(SqlStatement statement)
    {
        switch (statement)
        {
            case SelectStatement select:
                WriteSelectStatement(select);
                break;
            case InsertStatement insert:
                WriteInsertStatement(insert);
                break;
            case UpdateStatement update:
                WriteUpdateStatement(update);
                break;
            case DeleteStatement delete:
                WriteDeleteStatement(delete);
                break;
            case CreateTableStatement createTable:
                WriteCreateTableStatement(createTable);
                break;
            case DropTableStatement dropTable:
                WriteDropTableStatement(dropTable);
                break;
            case CreateIndexStatement createIndex:
                WriteCreateIndexStatement(createIndex);
                break;
            case DropIndexStatement dropIndex:
                WriteDropIndexStatement(dropIndex);
                break;
            default:
                throw new NotSupportedException($"Unsupported statement type: {statement.GetType().Name}");
        }
    }

    public void WriteExpression(SqlExpression expression)
    {
        switch (expression)
        {
            case LiteralExpression literal:
                WriteLiteralExpression(literal);
                break;
            case ColumnReference column:
                WriteColumnReference(column);
                break;
            case AllColumnsExpression allCols:
                WriteAllColumnsExpression(allCols);
                break;
            case BinaryExpression binary:
                WriteBinaryExpression(binary);
                break;
            case UnaryExpression unary:
                WriteUnaryExpression(unary);
                break;
            case FunctionCall func:
                WriteFunctionCall(func);
                break;
            case CaseExpression caseExpr:
                WriteCaseExpression(caseExpr);
                break;
            case SubqueryExpression subquery:
                WriteSubqueryExpression(subquery);
                break;
            case ListExpression list:
                WriteListExpression(list);
                break;
            case BetweenExpression between:
                WriteBetweenExpression(between);
                break;
            case CastExpression cast:
                WriteCastExpression(cast);
                break;
            default:
                throw new NotSupportedException($"Unsupported expression type: {expression.GetType().Name}");
        }
    }

    // ========================================
    // Statements
    // ========================================

    private void WriteSelectStatement(SelectStatement select)
    {
        WriteKeyword("SELECT");
        Write(" ");

        if (select.Distinct)
        {
            WriteKeyword("DISTINCT");
            Write(" ");
        }

        // Columns
        for (int i = 0; i < select.Columns.Count; i++)
        {
            if (i > 0)
            {
                Write(", ");
            }
            WriteSelectColumn(select.Columns[i]);
        }

        // FROM clause
        if (select.From != null && select.From.Count > 0)
        {
            if (_options.UsePrettyPrint)
            {
                WriteLine();
                WriteIndent();
            }
            else
            {
                Write(" ");
            }
            WriteKeyword("FROM");
            Write(" ");

            for (int i = 0; i < select.From.Count; i++)
            {
                if (i > 0)
                {
                    Write(", ");
                }
                WriteTableReference(select.From[i]);
            }
        }

        // WHERE clause
        if (select.Where != null)
        {
            if (_options.UsePrettyPrint)
            {
                WriteLine();
                WriteIndent();
            }
            else
            {
                Write(" ");
            }
            WriteKeyword("WHERE");
            Write(" ");
            WriteExpression(select.Where);
        }

        // GROUP BY clause
        if (select.GroupBy != null && select.GroupBy.Count > 0)
        {
            if (_options.UsePrettyPrint)
            {
                WriteLine();
                WriteIndent();
            }
            else
            {
                Write(" ");
            }
            WriteKeyword("GROUP BY");
            Write(" ");

            for (int i = 0; i < select.GroupBy.Count; i++)
            {
                if (i > 0)
                {
                    Write(", ");
                }
                WriteExpression(select.GroupBy[i]);
            }
        }

        // HAVING clause
        if (select.Having != null)
        {
            if (_options.UsePrettyPrint)
            {
                WriteLine();
                WriteIndent();
            }
            else
            {
                Write(" ");
            }
            WriteKeyword("HAVING");
            Write(" ");
            WriteExpression(select.Having);
        }

        // ORDER BY clause
        if (select.OrderBy != null && select.OrderBy.Count > 0)
        {
            if (_options.UsePrettyPrint)
            {
                WriteLine();
                WriteIndent();
            }
            else
            {
                Write(" ");
            }
            WriteKeyword("ORDER BY");
            Write(" ");

            for (int i = 0; i < select.OrderBy.Count; i++)
            {
                if (i > 0)
                {
                    Write(", ");
                }
                WriteOrderByClause(select.OrderBy[i]);
            }
        }

        // LIMIT clause
        if (select.Limit.HasValue)
        {
            Write(" ");
            WriteKeyword("LIMIT");
            Write($" {select.Limit.Value}");
        }

        // OFFSET clause
        if (select.Offset.HasValue)
        {
            Write(" ");
            WriteKeyword("OFFSET");
            Write($" {select.Offset.Value}");
        }
    }

    private void WriteInsertStatement(InsertStatement insert)
    {
        WriteKeyword("INSERT INTO");
        Write($" {insert.TableName}");

        if (insert.Columns != null && insert.Columns.Count > 0)
        {
            Write(" (");
            for (int i = 0; i < insert.Columns.Count; i++)
            {
                if (i > 0)
                {
                    Write(", ");
                }
                Write(insert.Columns[i]);
            }
            Write(")");
        }

        if (_options.UsePrettyPrint)
        {
            WriteLine();
            WriteIndent();
        }
        else
        {
            Write(" ");
        }

        WriteKeyword("VALUES");

        for (int i = 0; i < insert.Values.Count; i++)
        {
            if (i > 0)
            {
                Write(",");
            }

            if (_options.UsePrettyPrint)
            {
                WriteLine();
                WriteIndent();
            }
            else
            {
                Write(" ");
            }

            Write("(");
            var row = insert.Values[i];
            for (int j = 0; j < row.Count; j++)
            {
                if (j > 0)
                {
                    Write(", ");
                }
                WriteExpression(row[j]);
            }
            Write(")");
        }
    }

    private void WriteUpdateStatement(UpdateStatement update)
    {
        WriteKeyword("UPDATE");
        Write($" {update.TableName}");

        if (_options.UsePrettyPrint)
        {
            WriteLine();
            WriteIndent();
        }
        else
        {
            Write(" ");
        }

        WriteKeyword("SET");
        Write(" ");

        for (int i = 0; i < update.Assignments.Count; i++)
        {
            if (i > 0)
            {
                Write(", ");
            }
            WriteAssignment(update.Assignments[i]);
        }

        if (update.Where != null)
        {
            if (_options.UsePrettyPrint)
            {
                WriteLine();
                WriteIndent();
            }
            else
            {
                Write(" ");
            }
            WriteKeyword("WHERE");
            Write(" ");
            WriteExpression(update.Where);
        }
    }

    private void WriteDeleteStatement(DeleteStatement delete)
    {
        WriteKeyword("DELETE FROM");
        Write($" {delete.TableName}");

        if (delete.Where != null)
        {
            if (_options.UsePrettyPrint)
            {
                WriteLine();
                WriteIndent();
            }
            else
            {
                Write(" ");
            }
            WriteKeyword("WHERE");
            Write(" ");
            WriteExpression(delete.Where);
        }
    }

    private void WriteCreateTableStatement(CreateTableStatement createTable)
    {
        WriteKeyword("CREATE TABLE");
        Write(" ");

        if (createTable.IfNotExists)
        {
            WriteKeyword("IF NOT EXISTS");
            Write(" ");
        }

        Write(createTable.TableName);

        if (_options.UsePrettyPrint)
        {
            WriteLine();
            WriteIndent();
        }
        else
        {
            Write(" ");
        }

        Write("(");

        if (_options.UsePrettyPrint)
        {
            WriteLine();
            Indent();
        }

        // Columns
        for (int i = 0; i < createTable.Columns.Count; i++)
        {
            if (i > 0)
            {
                Write(",");
                if (_options.UsePrettyPrint)
                {
                    WriteLine();
                }
                else
                {
                    Write(" ");
                }
            }

            if (_options.UsePrettyPrint)
            {
                WriteIndent();
            }

            WriteColumnDefinition(createTable.Columns[i]);
        }

        // Constraints
        if (createTable.Constraints != null && createTable.Constraints.Count > 0)
        {
            for (int i = 0; i < createTable.Constraints.Count; i++)
            {
                Write(",");
                if (_options.UsePrettyPrint)
                {
                    WriteLine();
                    WriteIndent();
                }
                else
                {
                    Write(" ");
                }
                WriteTableConstraint(createTable.Constraints[i]);
            }
        }

        if (_options.UsePrettyPrint)
        {
            WriteLine();
            Dedent();
            WriteIndent();
        }

        Write(")");
    }

    private void WriteDropTableStatement(DropTableStatement dropTable)
    {
        WriteKeyword("DROP TABLE");
        Write(" ");

        if (dropTable.IfExists)
        {
            WriteKeyword("IF EXISTS");
            Write(" ");
        }

        Write(dropTable.TableName);
    }

    private void WriteCreateIndexStatement(CreateIndexStatement createIndex)
    {
        WriteKeyword("CREATE");
        Write(" ");

        if (createIndex.Unique)
        {
            WriteKeyword("UNIQUE");
            Write(" ");
        }

        WriteKeyword("INDEX");
        Write(" ");

        if (createIndex.IfNotExists)
        {
            WriteKeyword("IF NOT EXISTS");
            Write(" ");
        }

        Write($"{createIndex.IndexName} ");
        WriteKeyword("ON");
        Write($" {createIndex.TableName} (");

        for (int i = 0; i < createIndex.Columns.Count; i++)
        {
            if (i > 0)
            {
                Write(", ");
            }
            Write(createIndex.Columns[i]);
        }

        Write(")");
    }

    private void WriteDropIndexStatement(DropIndexStatement dropIndex)
    {
        WriteKeyword("DROP INDEX");
        Write(" ");

        if (dropIndex.IfExists)
        {
            WriteKeyword("IF EXISTS");
            Write(" ");
        }

        Write(dropIndex.IndexName);
    }

    // ========================================
    // Components
    // ========================================

    private void WriteSelectColumn(SelectColumn column)
    {
        WriteExpression(column.Expression);

        if (!string.IsNullOrEmpty(column.Alias))
        {
            Write(" ");
            WriteKeyword("AS");
            Write($" {column.Alias}");
        }
    }

    private void WriteOrderByClause(OrderByClause orderBy)
    {
        WriteExpression(orderBy.Expression);
        Write(" ");
        WriteKeyword(orderBy.Ascending ? "ASC" : "DESC");
    }

    private void WriteAssignment(Assignment assignment)
    {
        Write($"{assignment.ColumnName} = ");
        WriteExpression(assignment.Value);
    }

    private void WriteColumnDefinition(ColumnDefinition column)
    {
        Write($"{column.Name} ");
        WriteDataType(column.DataType);

        if (!column.Nullable)
        {
            Write(" ");
            WriteKeyword("NOT NULL");
        }

        if (column.DefaultValue != null)
        {
            Write(" ");
            WriteKeyword("DEFAULT");
            Write(" ");
            WriteExpression(column.DefaultValue);
        }

        if (column.PrimaryKey)
        {
            Write(" ");
            WriteKeyword("PRIMARY KEY");
        }

        if (column.Unique)
        {
            Write(" ");
            WriteKeyword("UNIQUE");
        }

        if (column.AutoIncrement)
        {
            Write(" ");
            WriteKeyword("AUTO_INCREMENT");
        }
    }

    private void WriteDataType(SqlDataType dataType)
    {
        Write(dataType.Name.ToUpperInvariant());

        if (dataType.Length.HasValue)
        {
            Write($"({dataType.Length.Value}");
            if (dataType.Scale.HasValue)
            {
                Write($", {dataType.Scale.Value}");
            }
            Write(")");
        }
        else if (dataType.Precision.HasValue)
        {
            Write($"({dataType.Precision.Value}");
            if (dataType.Scale.HasValue)
            {
                Write($", {dataType.Scale.Value}");
            }
            Write(")");
        }
    }

    private void WriteTableConstraint(TableConstraint constraint)
    {
        switch (constraint)
        {
            case PrimaryKeyConstraint pk:
                WriteKeyword("PRIMARY KEY");
                Write(" (");
                for (int i = 0; i < pk.Columns.Count; i++)
                {
                    if (i > 0)
                    {
                        Write(", ");
                    }
                    Write(pk.Columns[i]);
                }
                Write(")");
                break;

            case ForeignKeyConstraint fk:
                WriteKeyword("FOREIGN KEY");
                Write(" (");
                for (int i = 0; i < fk.Columns.Count; i++)
                {
                    if (i > 0)
                    {
                        Write(", ");
                    }
                    Write(fk.Columns[i]);
                }
                Write(") ");
                WriteKeyword("REFERENCES");
                Write($" {fk.ReferencedTable} (");
                for (int i = 0; i < fk.ReferencedColumns.Count; i++)
                {
                    if (i > 0)
                    {
                        Write(", ");
                    }
                    Write(fk.ReferencedColumns[i]);
                }
                Write(")");
                break;

            case UniqueConstraint unique:
                WriteKeyword("UNIQUE");
                Write(" (");
                for (int i = 0; i < unique.Columns.Count; i++)
                {
                    if (i > 0)
                    {
                        Write(", ");
                    }
                    Write(unique.Columns[i]);
                }
                Write(")");
                break;
        }
    }

    private void WriteTableReference(TableReference tableRef)
    {
        switch (tableRef)
        {
            case TableName table:
                Write(table.Name);
                if (!string.IsNullOrEmpty(table.Alias))
                {
                    Write($" {table.Alias}");
                }
                break;

            case JoinClause join:
                WriteTableReference(join.Left);
                Write(" ");
                WriteJoinType(join.JoinType);
                Write(" ");
                WriteTableReference(join.Right);
                Write(" ");
                WriteKeyword("ON");
                Write(" ");
                WriteExpression(join.Condition);
                break;
        }
    }

    private void WriteJoinType(JoinType joinType)
    {
        switch (joinType)
        {
            case JoinType.Inner:
                WriteKeyword("INNER JOIN");
                break;
            case JoinType.Left:
                WriteKeyword("LEFT JOIN");
                break;
            case JoinType.Right:
                WriteKeyword("RIGHT JOIN");
                break;
            case JoinType.Full:
                WriteKeyword("FULL JOIN");
                break;
            case JoinType.Cross:
                WriteKeyword("CROSS JOIN");
                break;
        }
    }

    // ========================================
    // Expressions
    // ========================================

    private void WriteLiteralExpression(LiteralExpression literal)
    {
        switch (literal.Kind)
        {
            case SqlLiteralKind.Integer:
            case SqlLiteralKind.Float:
                Write(literal.Value?.ToString() ?? "0");
                break;

            case SqlLiteralKind.String:
                Write($"'{EscapeSqlString(literal.Value?.ToString() ?? "")}'");
                break;

            case SqlLiteralKind.Boolean:
                WriteKeyword((bool)literal.Value ? "TRUE" : "FALSE");
                break;

            case SqlLiteralKind.Null:
                WriteKeyword("NULL");
                break;

            case SqlLiteralKind.Date:
            case SqlLiteralKind.Time:
            case SqlLiteralKind.DateTime:
                Write($"'{literal.Value}'");
                break;
        }
    }

    private static string EscapeSqlString(string str)
    {
        return str.Replace("'", "''");
    }

    private void WriteColumnReference(ColumnReference column)
    {
        if (!string.IsNullOrEmpty(column.TableName))
        {
            Write($"{column.TableName}.");
        }
        Write(column.ColumnName);
    }

    private void WriteAllColumnsExpression(AllColumnsExpression allCols)
    {
        if (!string.IsNullOrEmpty(allCols.TableName))
        {
            Write($"{allCols.TableName}.");
        }
        Write("*");
    }

    private void WriteBinaryExpression(BinaryExpression binary)
    {
        var needsParens = binary.Left is BinaryExpression || binary.Right is BinaryExpression;

        if (needsParens && binary.Left is BinaryExpression)
        {
            Write("(");
        }
        WriteExpression(binary.Left);
        if (needsParens && binary.Left is BinaryExpression)
        {
            Write(")");
        }

        Write(" ");
        WriteBinaryOperator(binary.Operator);
        Write(" ");

        if (binary.Operator == SqlBinaryOperator.In || binary.Operator == SqlBinaryOperator.NotIn)
        {
            // Special handling for IN - the right side is already wrapped
            WriteExpression(binary.Right);
        }
        else
        {
            if (needsParens && binary.Right is BinaryExpression)
            {
                Write("(");
            }
            WriteExpression(binary.Right);
            if (needsParens && binary.Right is BinaryExpression)
            {
                Write(")");
            }
        }
    }

    private void WriteBinaryOperator(SqlBinaryOperator op)
    {
        var opStr = op switch
        {
            SqlBinaryOperator.Add => "+",
            SqlBinaryOperator.Subtract => "-",
            SqlBinaryOperator.Multiply => "*",
            SqlBinaryOperator.Divide => "/",
            SqlBinaryOperator.Modulo => "%",
            SqlBinaryOperator.Equal => "=",
            SqlBinaryOperator.NotEqual => "<>",
            SqlBinaryOperator.LessThan => "<",
            SqlBinaryOperator.LessThanOrEqual => "<=",
            SqlBinaryOperator.GreaterThan => ">",
            SqlBinaryOperator.GreaterThanOrEqual => ">=",
            SqlBinaryOperator.And => "AND",
            SqlBinaryOperator.Or => "OR",
            SqlBinaryOperator.Like => "LIKE",
            SqlBinaryOperator.NotLike => "NOT LIKE",
            SqlBinaryOperator.In => "IN",
            SqlBinaryOperator.NotIn => "NOT IN",
            _ => "="
        };

        if (op == SqlBinaryOperator.And || op == SqlBinaryOperator.Or || 
            op == SqlBinaryOperator.Like || op == SqlBinaryOperator.NotLike ||
            op == SqlBinaryOperator.In || op == SqlBinaryOperator.NotIn)
        {
            WriteKeyword(opStr);
        }
        else
        {
            Write(opStr);
        }
    }

    private void WriteUnaryExpression(UnaryExpression unary)
    {
        switch (unary.Operator)
        {
            case SqlUnaryOperator.Not:
                WriteKeyword("NOT");
                Write(" ");
                WriteExpression(unary.Operand);
                break;

            case SqlUnaryOperator.Minus:
                Write("-");
                WriteExpression(unary.Operand);
                break;

            case SqlUnaryOperator.Plus:
                Write("+");
                WriteExpression(unary.Operand);
                break;

            case SqlUnaryOperator.IsNull:
                WriteExpression(unary.Operand);
                Write(" ");
                WriteKeyword("IS NULL");
                break;

            case SqlUnaryOperator.IsNotNull:
                WriteExpression(unary.Operand);
                Write(" ");
                WriteKeyword("IS NOT NULL");
                break;
        }
    }

    private void WriteFunctionCall(FunctionCall func)
    {
        Write($"{func.Name.ToUpperInvariant()}(");

        if (func.Distinct)
        {
            WriteKeyword("DISTINCT");
            Write(" ");
        }

        for (int i = 0; i < func.Arguments.Count; i++)
        {
            if (i > 0)
            {
                Write(", ");
            }
            WriteExpression(func.Arguments[i]);
        }

        Write(")");
    }

    private void WriteCaseExpression(CaseExpression caseExpr)
    {
        WriteKeyword("CASE");

        if (caseExpr.TestExpression != null)
        {
            Write(" ");
            WriteExpression(caseExpr.TestExpression);
        }

        foreach (var whenClause in caseExpr.WhenClauses)
        {
            Write(" ");
            WriteKeyword("WHEN");
            Write(" ");
            WriteExpression(whenClause.Condition);
            Write(" ");
            WriteKeyword("THEN");
            Write(" ");
            WriteExpression(whenClause.Result);
        }

        if (caseExpr.ElseClause != null)
        {
            Write(" ");
            WriteKeyword("ELSE");
            Write(" ");
            WriteExpression(caseExpr.ElseClause);
        }

        Write(" ");
        WriteKeyword("END");
    }

    private void WriteSubqueryExpression(SubqueryExpression subquery)
    {
        Write("(");
        WriteSelectStatement(subquery.Query);
        Write(")");
    }

    private void WriteListExpression(ListExpression list)
    {
        Write("(");
        for (int i = 0; i < list.Items.Count; i++)
        {
            if (i > 0)
            {
                Write(", ");
            }
            WriteExpression(list.Items[i]);
        }
        Write(")");
    }

    private void WriteBetweenExpression(BetweenExpression between)
    {
        WriteExpression(between.Value);
        Write(" ");

        if (between.NotBetween)
        {
            WriteKeyword("NOT BETWEEN");
        }
        else
        {
            WriteKeyword("BETWEEN");
        }

        Write(" ");
        WriteExpression(between.Lower);
        Write(" ");
        WriteKeyword("AND");
        Write(" ");
        WriteExpression(between.Upper);
    }

    private void WriteCastExpression(CastExpression cast)
    {
        WriteKeyword("CAST");
        Write("(");
        WriteExpression(cast.Expression);
        Write(" ");
        WriteKeyword("AS");
        Write(" ");
        WriteDataType(cast.TargetType);
        Write(")");
    }
}

/// <summary>
/// Options for SQL code generation.
/// </summary>
public class SqlWriterOptions
{
    /// <summary>
    /// Whether to format SQL with line breaks and indentation (default: true).
    /// </summary>
    public bool UsePrettyPrint { get; set; } = true;

    /// <summary>
    /// Number of spaces for each indentation level (default: 2).
    /// </summary>
    public int IndentSize { get; set; } = 2;

    /// <summary>
    /// Whether to write keywords in uppercase (default: true).
    /// </summary>
    public bool UppercaseKeywords { get; set; } = true;
}

