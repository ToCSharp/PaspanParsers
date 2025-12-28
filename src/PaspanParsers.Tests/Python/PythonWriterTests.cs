using PaspanParsers.Python;

namespace PaspanParsers.Tests.Python;

[TestClass]
public class PythonWriterTests
{
    private static string WriteNode(Action<PythonWriter> writeAction)
    {
        var writer = new PythonWriter();
        writeAction(writer);
        return writer.GetResult();
    }

    // ========================================
    // Literals
    // ========================================

    [TestMethod]
    public void Write_IntegerLiteral()
    {
        var expr = new LiteralExpression(42, LiteralKind.Integer);
        var result = WriteNode(w => w.WriteExpression(expr));
        Assert.Contains("42", result);
    }

    [TestMethod]
    public void Write_StringLiteral()
    {
        var expr = new LiteralExpression("hello", LiteralKind.String);
        var result = WriteNode(w => w.WriteExpression(expr));
        Assert.Contains("\"hello\"", result);
    }

    [TestMethod]
    public void Write_BooleanTrue()
    {
        var expr = new LiteralExpression(true, LiteralKind.Boolean);
        var result = WriteNode(w => w.WriteExpression(expr));
        Assert.Contains("True", result);
    }

    [TestMethod]
    public void Write_None()
    {
        var expr = new LiteralExpression(null, LiteralKind.None);
        var result = WriteNode(w => w.WriteExpression(expr));
        Assert.Contains("None", result);
    }

    // ========================================
    // Collections
    // ========================================

    [TestMethod]
    public void Write_List()
    {
        var list = new ListExpression(
        [
            new LiteralExpression(1, LiteralKind.Integer),
            new LiteralExpression(2, LiteralKind.Integer),
            new LiteralExpression(3, LiteralKind.Integer)
        ]);
        var result = WriteNode(w => w.WriteExpression(list));
        Assert.Contains("[1, 2, 3]", result);
    }

    [TestMethod]
    public void Write_EmptyList()
    {
        var list = new ListExpression([]);
        var result = WriteNode(w => w.WriteExpression(list));
        Assert.Contains("[]", result);
    }

    [TestMethod]
    public void Write_Tuple()
    {
        var tuple = new TupleExpression(
        [
            new LiteralExpression(1, LiteralKind.Integer),
            new LiteralExpression(2, LiteralKind.Integer)
        ]);
        var result = WriteNode(w => w.WriteExpression(tuple));
        Assert.Contains("(1, 2)", result);
    }

    [TestMethod]
    public void Write_Dict()
    {
        var dict = new DictExpression(
            keys: [new LiteralExpression("key", LiteralKind.String)],
            values: [new LiteralExpression("value", LiteralKind.String)]
        );
        var result = WriteNode(w => w.WriteExpression(dict));
        Assert.Contains("{\"key\": \"value\"}", result);
    }

    // ========================================
    // Operations
    // ========================================

    [TestMethod]
    public void Write_BinaryOperation()
    {
        var binOp = new BinaryOperation(
            left: new LiteralExpression(1, LiteralKind.Integer),
            op: BinaryOperator.Add,
            right: new LiteralExpression(2, LiteralKind.Integer)
        );
        var result = WriteNode(w => w.WriteExpression(binOp));
        Assert.Contains("1 + 2", result);
    }

    [TestMethod]
    public void Write_CompareOperation()
    {
        var cmpOp = new CompareOperation(
            left: new NameExpression("x"),
            ops: [CompareOperator.Lt],
            comparators: [new NameExpression("y")]
        );
        var result = WriteNode(w => w.WriteExpression(cmpOp));
        Assert.Contains("x < y", result);
    }

    [TestMethod]
    public void Write_BooleanAnd()
    {
        var boolOp = new BooleanOperation(
            op: BoolOperator.And,
            values:
            [
                new NameExpression("a"),
                new NameExpression("b")
            ]
        );
        var result = WriteNode(w => w.WriteExpression(boolOp));
        Assert.Contains("a and b", result);
    }

    // ========================================
    // Statements
    // ========================================

    [TestMethod]
    public void Write_Assignment()
    {
        var assign = new AssignmentStatement(
            targets: [new NameExpression("x")],
            value: new LiteralExpression(5, LiteralKind.Integer)
        );
        var result = WriteNode(w => w.WriteStatement(assign));
        Assert.Contains("x = 5", result);
    }

    [TestMethod]
    public void Write_AnnotatedAssignment()
    {
        var assign = new AnnotatedAssignment(
            target: new NameExpression("x"),
            annotation: new NameExpression("int"),
            value: new LiteralExpression(5, LiteralKind.Integer)
        );
        var result = WriteNode(w => w.WriteStatement(assign));
        Assert.Contains("x: int = 5", result);
    }

    [TestMethod]
    public void Write_Return()
    {
        var ret = new ReturnStatement(new LiteralExpression(42, LiteralKind.Integer));
        var result = WriteNode(w => w.WriteStatement(ret));
        Assert.Contains("return 42", result);
    }

    [TestMethod]
    public void Write_PassStatement()
    {
        var pass = new PassStatement();
        var result = WriteNode(w => w.WriteStatement(pass));
        Assert.Contains("pass", result);
    }

    // ========================================
    // Imports
    // ========================================

    [TestMethod]
    public void Write_SimpleImport()
    {
        var import = new ImportStatement([new Alias("sys")]);
        var result = WriteNode(w => w.WriteStatement(import));
        Assert.Contains("import sys", result);
    }

    [TestMethod]
    public void Write_ImportWithAlias()
    {
        var import = new ImportStatement([new Alias("numpy", "np")]);
        var result = WriteNode(w => w.WriteStatement(import));
        Assert.Contains("import numpy as np", result);
    }

    [TestMethod]
    public void Write_FromImport()
    {
        var import = new ImportFromStatement(
            names: [new Alias("path")],
            module: "os"
        );
        var result = WriteNode(w => w.WriteStatement(import));
        Assert.Contains("from os import path", result);
    }

    // ========================================
    // Functions
    // ========================================

    [TestMethod]
    public void Write_SimpleFunction()
    {
        var func = new FunctionDef(
            name: "foo",
            args: new Arguments(),
            body: [new PassStatement()]
        );
        var result = WriteNode(w => w.WriteFunctionDef(func));
        Assert.Contains("def foo():", result);
        Assert.Contains("pass", result);
    }

    [TestMethod]
    public void Write_FunctionWithParameters()
    {
        var func = new FunctionDef(
            name: "add",
            args: new Arguments(
                args:
                [
                    new Arg("a"),
                    new Arg("b")
                ]
            ),
            body:
            [
                new ReturnStatement(
                    new BinaryOperation(
                        new NameExpression("a"),
                        BinaryOperator.Add,
                        new NameExpression("b")
                    )
                )
            ]
        );
        var result = WriteNode(w => w.WriteFunctionDef(func));
        Assert.Contains("def add(a, b):", result);
        Assert.Contains("return a + b", result);
    }

    [TestMethod]
    public void Write_FunctionWithTypeHints()
    {
        var func = new FunctionDef(
            name: "add",
            args: new Arguments(
                args:
                [
                    new Arg("a", new NameExpression("int")),
                    new Arg("b", new NameExpression("int"))
                ]
            ),
            body: [new PassStatement()],
            returns: new NameExpression("int")
        );
        var result = WriteNode(w => w.WriteFunctionDef(func));
        Assert.Contains("def add(a: int, b: int) -> int:", result);
    }

    // ========================================
    // Classes
    // ========================================

    [TestMethod]
    public void Write_SimpleClass()
    {
        var cls = new ClassDef(
            name: "Person",
            body: [new PassStatement()]
        );
        var result = WriteNode(w => w.WriteClassDef(cls));
        Assert.Contains("class Person:", result);
        Assert.Contains("pass", result);
    }

    [TestMethod]
    public void Write_ClassWithBase()
    {
        var cls = new ClassDef(
            name: "Dog",
            body: [new PassStatement()],
            bases: [new NameExpression("Animal")]
        );
        var result = WriteNode(w => w.WriteClassDef(cls));
        Assert.Contains("class Dog(Animal):", result);
    }

    // ========================================
    // Control Flow
    // ========================================

    [TestMethod]
    public void Write_IfStatement()
    {
        var ifStmt = new IfStatement(
            test: new CompareOperation(
                new NameExpression("x"),
                [CompareOperator.Gt],
                [new LiteralExpression(0, LiteralKind.Integer)]
            ),
            body: [new PassStatement()]
        );
        var result = WriteNode(w => w.WriteStatement(ifStmt));
        Assert.Contains("if x > 0:", result);
    }

    [TestMethod]
    public void Write_WhileLoop()
    {
        var whileStmt = new WhileStatement(
            test: new LiteralExpression(true, LiteralKind.Boolean),
            body: [new BreakStatement()]
        );
        var result = WriteNode(w => w.WriteStatement(whileStmt));
        Assert.Contains("while True:", result);
        Assert.Contains("break", result);
    }

    [TestMethod]
    public void Write_ForLoop()
    {
        var forStmt = new ForStatement(
            target: new NameExpression("i"),
            iter: new CallExpression(
                func: new NameExpression("range"),
                args: [new LiteralExpression(10, LiteralKind.Integer)]
            ),
            body: [new PassStatement()]
        );
        var result = WriteNode(w => w.WriteStatement(forStmt));
        Assert.Contains("for i in range(10):", result);
    }

    // ========================================
    // Module
    // ========================================

    [TestMethod]
    public void Write_Module()
    {
        var module = new Module(
            body:
            [
                new ImportStatement([new Alias("sys")]),
                new FunctionDef(
                    name: "main",
                    args: new Arguments(),
                    body: [new PassStatement()]
                )
            ]
        );
        var result = WriteNode(w => w.WriteModule(module));
        Assert.Contains("import sys", result);
        Assert.Contains("def main():", result);
    }
}

