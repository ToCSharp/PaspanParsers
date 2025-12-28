using System.Text;

namespace PaspanParsers.Python;

/// <summary>
/// Writes Python code from AST nodes
/// </summary>
public class PythonWriter(string indentString = "    ")
{
    private readonly StringBuilder _builder = new StringBuilder();
    private int _indentLevel = 0;
    private readonly string _indentString = indentString;

    public string GetResult() => _builder.ToString();

    private void Write(string text)
    {
        _builder.Append(text);
    }

    private void WriteLine()
    {
        _builder.AppendLine();
    }

    private void WriteLine(string text)
    {
        WriteIndent();
        _builder.AppendLine(text);
    }

    private void WriteIndent()
    {
        for (int i = 0; i < _indentLevel; i++)
        {
            _builder.Append(_indentString);
        }
    }

    private void Indent()
    {
        _indentLevel++;
    }

    private void Unindent()
    {
        _indentLevel--;
    }

    // ========================================
    // Module
    // ========================================

    public void WriteModule(Module module)
    {
        for (int i = 0; i < module.Body.Count; i++)
        {
            WriteStatement(module.Body[i]);
            if (i < module.Body.Count - 1 && NeedsBlankLine(module.Body[i], module.Body[i + 1]))
            {
                WriteLine();
            }
        }
    }

    private bool NeedsBlankLine(Statement current, Statement next)
    {
        return (current is FunctionDef or ClassDef) || (next is FunctionDef or ClassDef);
    }

    // ========================================
    // Statements
    // ========================================

    public void WriteStatement(Statement stmt)
    {
        switch (stmt)
        {
            case ExpressionStatement exprStmt:
                WriteIndent();
                WriteExpression(exprStmt.Value);
                WriteLine();
                break;
            case AssignmentStatement assign:
                WriteAssignment(assign);
                break;
            case AnnotatedAssignment annAssign:
                WriteAnnotatedAssignment(annAssign);
                break;
            case AugmentedAssignment augAssign:
                WriteAugmentedAssignment(augAssign);
                break;
            case PassStatement:
                WriteLine("pass");
                break;
            case BreakStatement:
                WriteLine("break");
                break;
            case ContinueStatement:
                WriteLine("continue");
                break;
            case ReturnStatement ret:
                WriteReturn(ret);
                break;
            case RaiseStatement raise:
                WriteRaise(raise);
                break;
            case ImportStatement import:
                WriteImport(import);
                break;
            case ImportFromStatement importFrom:
                WriteImportFrom(importFrom);
                break;
            case IfStatement ifStmt:
                WriteIf(ifStmt);
                break;
            case WhileStatement whileStmt:
                WriteWhile(whileStmt);
                break;
            case ForStatement forStmt:
                WriteFor(forStmt);
                break;
            case FunctionDef funcDef:
                WriteFunctionDef(funcDef);
                break;
            case AsyncFunctionDef asyncFunc:
                WriteAsyncFunctionDef(asyncFunc);
                break;
            case ClassDef classDef:
                WriteClassDef(classDef);
                break;
            case WithStatement withStmt:
                WriteWith(withStmt);
                break;
            case TryStatement tryStmt:
                WriteTry(tryStmt);
                break;
        }
    }

    private void WriteAssignment(AssignmentStatement assign)
    {
        WriteIndent();
        for (int i = 0; i < assign.Targets.Count; i++)
        {
            if (i > 0) Write(" = ");
            WriteExpression(assign.Targets[i]);
        }
        Write(" = ");
        WriteExpression(assign.Value);
        WriteLine();
    }

    private void WriteAnnotatedAssignment(AnnotatedAssignment assign)
    {
        WriteIndent();
        WriteExpression(assign.Target);
        Write(": ");
        WriteExpression(assign.Annotation);
        if (assign.Value != null)
        {
            Write(" = ");
            WriteExpression(assign.Value);
        }
        WriteLine();
    }

    private void WriteAugmentedAssignment(AugmentedAssignment assign)
    {
        WriteIndent();
        WriteExpression(assign.Target);
        Write($" {GetAugmentOperator(assign.Op)} ");
        WriteExpression(assign.Value);
        WriteLine();
    }

    private string GetAugmentOperator(AugmentOperator op) => op switch
    {
        AugmentOperator.Add => "+=",
        AugmentOperator.Sub => "-=",
        AugmentOperator.Mult => "*=",
        AugmentOperator.Div => "/=",
        AugmentOperator.Mod => "%=",
        AugmentOperator.Pow => "**=",
        AugmentOperator.FloorDiv => "//=",
        AugmentOperator.LShift => "<<=",
        AugmentOperator.RShift => ">>=",
        AugmentOperator.BitAnd => "&=",
        AugmentOperator.BitOr => "|=",
        AugmentOperator.BitXor => "^=",
        _ => "+="
    };

    private void WriteReturn(ReturnStatement ret)
    {
        WriteIndent();
        Write("return");
        if (ret.Value != null)
        {
            Write(" ");
            WriteExpression(ret.Value);
        }
        WriteLine();
    }

    private void WriteRaise(RaiseStatement raise)
    {
        WriteIndent();
        Write("raise");
        if (raise.Exc != null)
        {
            Write(" ");
            WriteExpression(raise.Exc);
            if (raise.Cause != null)
            {
                Write(" from ");
                WriteExpression(raise.Cause);
            }
        }
        WriteLine();
    }

    private void WriteImport(ImportStatement import)
    {
        WriteIndent();
        Write("import ");
        for (int i = 0; i < import.Names.Count; i++)
        {
            if (i > 0) Write(", ");
            WriteAlias(import.Names[i]);
        }
        WriteLine();
    }

    private void WriteImportFrom(ImportFromStatement import)
    {
        WriteIndent();
        Write("from ");
        for (int i = 0; i < import.Level; i++)
        {
            Write(".");
        }
        if (import.Module != null)
        {
            Write(import.Module);
        }
        Write(" import ");
        for (int i = 0; i < import.Names.Count; i++)
        {
            if (i > 0) Write(", ");
            WriteAlias(import.Names[i]);
        }
        WriteLine();
    }

    private void WriteAlias(Alias alias)
    {
        Write(alias.Name);
        if (alias.AsName != null)
        {
            Write($" as {alias.AsName}");
        }
    }

    private void WriteIf(IfStatement ifStmt)
    {
        WriteIndent();
        Write("if ");
        WriteExpression(ifStmt.Test);
        WriteLine(":");
        Indent();
        WriteStatements(ifStmt.Body);
        Unindent();
        
        if (ifStmt.OrElse.Count > 0)
        {
            WriteLine("else:");
            Indent();
            WriteStatements(ifStmt.OrElse);
            Unindent();
        }
    }

    private void WriteWhile(WhileStatement whileStmt)
    {
        WriteIndent();
        Write("while ");
        WriteExpression(whileStmt.Test);
        WriteLine(":");
        Indent();
        WriteStatements(whileStmt.Body);
        Unindent();
    }

    private void WriteFor(ForStatement forStmt)
    {
        WriteIndent();
        Write("for ");
        WriteExpression(forStmt.Target);
        Write(" in ");
        WriteExpression(forStmt.Iter);
        WriteLine(":");
        Indent();
        WriteStatements(forStmt.Body);
        Unindent();
    }

    private void WriteWith(WithStatement withStmt)
    {
        WriteIndent();
        Write("with ");
        for (int i = 0; i < withStmt.Items.Count; i++)
        {
            if (i > 0) Write(", ");
            WriteWithItem(withStmt.Items[i]);
        }
        WriteLine(":");
        Indent();
        WriteStatements(withStmt.Body);
        Unindent();
    }

    private void WriteWithItem(WithItem item)
    {
        WriteExpression(item.ContextExpr);
        if (item.OptionalVars != null)
        {
            Write(" as ");
            WriteExpression(item.OptionalVars);
        }
    }

    private void WriteTry(TryStatement tryStmt)
    {
        WriteLine("try:");
        Indent();
        WriteStatements(tryStmt.Body);
        Unindent();
        
        foreach (var handler in tryStmt.Handlers)
        {
            WriteExceptHandler(handler);
        }
        
        if (tryStmt.OrElse.Count > 0)
        {
            WriteLine("else:");
            Indent();
            WriteStatements(tryStmt.OrElse);
            Unindent();
        }
        
        if (tryStmt.FinalBody.Count > 0)
        {
            WriteLine("finally:");
            Indent();
            WriteStatements(tryStmt.FinalBody);
            Unindent();
        }
    }

    private void WriteExceptHandler(ExceptHandler handler)
    {
        WriteIndent();
        Write("except");
        if (handler.Type != null)
        {
            Write(" ");
            WriteExpression(handler.Type);
            if (handler.Name != null)
            {
                Write($" as {handler.Name}");
            }
        }
        WriteLine(":");
        Indent();
        WriteStatements(handler.Body);
        Unindent();
    }

    public void WriteFunctionDef(FunctionDef func)
    {
        WriteIndent();
        Write($"def {func.Name}(");
        WriteArguments(func.Args);
        Write(")");
        if (func.Returns != null)
        {
            Write(" -> ");
            WriteExpression(func.Returns);
        }
        WriteLine(":");
        Indent();
        WriteStatements(func.Body);
        Unindent();
    }

    private void WriteAsyncFunctionDef(AsyncFunctionDef func)
    {
        WriteIndent();
        Write($"async def {func.Name}(");
        WriteArguments(func.Args);
        Write(")");
        if (func.Returns != null)
        {
            Write(" -> ");
            WriteExpression(func.Returns);
        }
        WriteLine(":");
        Indent();
        WriteStatements(func.Body);
        Unindent();
    }

    private void WriteArguments(Arguments args)
    {
        var allArgs = new List<(Arg arg, Expression defaultValue)>();
        
        for (int i = 0; i < args.Args.Count; i++)
        {
            var defaultIndex = i - (args.Args.Count - args.Defaults.Count);
            var defaultValue = defaultIndex >= 0 ? args.Defaults[defaultIndex] : null;
            allArgs.Add((args.Args[i], defaultValue));
        }
        
        for (int i = 0; i < allArgs.Count; i++)
        {
            if (i > 0) Write(", ");
            WriteArg(allArgs[i].arg, allArgs[i].defaultValue);
        }
        
        if (args.VarArg != null)
        {
            if (allArgs.Count > 0) Write(", ");
            Write("*");
            WriteArg(args.VarArg, null);
        }
        
        if (args.KwArg != null)
        {
            if (allArgs.Count > 0 || args.VarArg != null) Write(", ");
            Write("**");
            WriteArg(args.KwArg, null);
        }
    }

    private void WriteArg(Arg arg, Expression defaultValue)
    {
        Write(arg.Arg_);
        if (arg.Annotation != null)
        {
            Write(": ");
            WriteExpression(arg.Annotation);
        }
        if (defaultValue != null)
        {
            Write(" = ");
            WriteExpression(defaultValue);
        }
    }

    public void WriteClassDef(ClassDef cls)
    {
        WriteIndent();
        Write($"class {cls.Name}");
        if (cls.Bases.Count > 0 || cls.Keywords.Count > 0)
        {
            Write("(");
            for (int i = 0; i < cls.Bases.Count; i++)
            {
                if (i > 0) Write(", ");
                WriteExpression(cls.Bases[i]);
            }
            Write(")");
        }
        WriteLine(":");
        Indent();
        WriteStatements(cls.Body);
        Unindent();
    }

    private void WriteStatements(IReadOnlyList<Statement> statements)
    {
        if (statements.Count == 0)
        {
            WriteLine("pass");
            return;
        }
        
        foreach (var stmt in statements)
        {
            WriteStatement(stmt);
        }
    }

    // ========================================
    // Expressions
    // ========================================

    public void WriteExpression(Expression expr)
    {
        switch (expr)
        {
            case LiteralExpression lit:
                WriteLiteral(lit);
                break;
            case NameExpression name:
                Write(name.Id);
                break;
            case AttributeExpression attr:
                WriteExpression(attr.Value);
                Write($".{attr.Attr}");
                break;
            case SubscriptExpression subscript:
                WriteExpression(subscript.Value);
                Write("[");
                WriteExpression(subscript.Slice);
                Write("]");
                break;
            case ListExpression list:
                WriteList(list);
                break;
            case TupleExpression tuple:
                WriteTuple(tuple);
                break;
            case SetExpression set:
                WriteSet(set);
                break;
            case DictExpression dict:
                WriteDict(dict);
                break;
            case BinaryOperation binOp:
                WriteBinaryOp(binOp);
                break;
            case UnaryOperation unOp:
                WriteUnaryOp(unOp);
                break;
            case BooleanOperation boolOp:
                WriteBooleanOp(boolOp);
                break;
            case CompareOperation cmpOp:
                WriteCompareOp(cmpOp);
                break;
            case CallExpression call:
                WriteCall(call);
                break;
            case ConditionalExpression cond:
                WriteConditional(cond);
                break;
            case LambdaExpression lambda:
                WriteLambda(lambda);
                break;
        }
    }

    private void WriteLiteral(LiteralExpression lit)
    {
        switch (lit.Kind)
        {
            case LiteralKind.Integer:
            case LiteralKind.Float:
                Write(lit.Value?.ToString() ?? "0");
                break;
            case LiteralKind.String:
                Write($"\"{lit.Value}\"");
                break;
            case LiteralKind.Boolean:
                Write((bool)(lit.Value ?? false) ? "True" : "False");
                break;
            case LiteralKind.None:
                Write("None");
                break;
            case LiteralKind.Ellipsis:
                Write("...");
                break;
        }
    }

    private void WriteList(ListExpression list)
    {
        Write("[");
        for (int i = 0; i < list.Elements.Count; i++)
        {
            if (i > 0) Write(", ");
            WriteExpression(list.Elements[i]);
        }
        Write("]");
    }

    private void WriteTuple(TupleExpression tuple)
    {
        Write("(");
        for (int i = 0; i < tuple.Elements.Count; i++)
        {
            if (i > 0) Write(", ");
            WriteExpression(tuple.Elements[i]);
        }
        if (tuple.Elements.Count == 1) Write(",");  // Single-element tuple
        Write(")");
    }

    private void WriteSet(SetExpression set)
    {
        Write("{");
        for (int i = 0; i < set.Elements.Count; i++)
        {
            if (i > 0) Write(", ");
            WriteExpression(set.Elements[i]);
        }
        Write("}");
    }

    private void WriteDict(DictExpression dict)
    {
        Write("{");
        for (int i = 0; i < dict.Keys.Count; i++)
        {
            if (i > 0) Write(", ");
            WriteExpression(dict.Keys[i]);
            Write(": ");
            WriteExpression(dict.Values[i]);
        }
        Write("}");
    }

    private void WriteBinaryOp(BinaryOperation binOp)
    {
        WriteExpression(binOp.Left);
        Write($" {GetBinaryOperator(binOp.Op)} ");
        WriteExpression(binOp.Right);
    }

    private string GetBinaryOperator(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Sub => "-",
        BinaryOperator.Mult => "*",
        BinaryOperator.Div => "/",
        BinaryOperator.Mod => "%",
        BinaryOperator.Pow => "**",
        BinaryOperator.FloorDiv => "//",
        BinaryOperator.LShift => "<<",
        BinaryOperator.RShift => ">>",
        BinaryOperator.BitOr => "|",
        BinaryOperator.BitXor => "^",
        BinaryOperator.BitAnd => "&",
        BinaryOperator.MatMult => "@",
        _ => "+"
    };

    private void WriteUnaryOp(UnaryOperation unOp)
    {
        Write(GetUnaryOperator(unOp.Op));
        WriteExpression(unOp.Operand);
    }

    private string GetUnaryOperator(UnaryOperator op) => op switch
    {
        UnaryOperator.Not => "not ",
        UnaryOperator.UAdd => "+",
        UnaryOperator.USub => "-",
        UnaryOperator.Invert => "~",
        _ => ""
    };

    private void WriteBooleanOp(BooleanOperation boolOp)
    {
        for (int i = 0; i < boolOp.Values.Count; i++)
        {
            if (i > 0)
            {
                Write(boolOp.Op == BoolOperator.And ? " and " : " or ");
            }
            WriteExpression(boolOp.Values[i]);
        }
    }

    private void WriteCompareOp(CompareOperation cmpOp)
    {
        WriteExpression(cmpOp.Left);
        for (int i = 0; i < cmpOp.Ops.Count; i++)
        {
            Write($" {GetCompareOperator(cmpOp.Ops[i])} ");
            WriteExpression(cmpOp.Comparators[i]);
        }
    }

    private string GetCompareOperator(CompareOperator op) => op switch
    {
        CompareOperator.Eq => "==",
        CompareOperator.NotEq => "!=",
        CompareOperator.Lt => "<",
        CompareOperator.LtE => "<=",
        CompareOperator.Gt => ">",
        CompareOperator.GtE => ">=",
        CompareOperator.Is => "is",
        CompareOperator.IsNot => "is not",
        CompareOperator.In => "in",
        CompareOperator.NotIn => "not in",
        _ => "=="
    };

    private void WriteCall(CallExpression call)
    {
        WriteExpression(call.Func);
        Write("(");
        for (int i = 0; i < call.Args.Count; i++)
        {
            if (i > 0) Write(", ");
            WriteExpression(call.Args[i]);
        }
        Write(")");
    }

    private void WriteConditional(ConditionalExpression cond)
    {
        WriteExpression(cond.Body);
        Write(" if ");
        WriteExpression(cond.Test);
        Write(" else ");
        WriteExpression(cond.OrElse);
    }

    private void WriteLambda(LambdaExpression lambda)
    {
        Write("lambda ");
        WriteArguments(lambda.Args);
        Write(": ");
        WriteExpression(lambda.Body);
    }
}

