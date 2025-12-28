using PaspanParsers.Python;

namespace PaspanParsers.Tests.Python;

[TestClass]
public class PythonParserTests
{
    private readonly PythonParser _parser = new();

    // ========================================
    // Literals
    // ========================================

    [TestMethod]
    public void Parse_IntegerLiteral()
    {
        var result = PythonParser.ParseExpression("42");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<LiteralExpression>(result.Value);
        var lit = (LiteralExpression)result.Value;
        Assert.AreEqual(42, lit.Value);
        Assert.AreEqual(LiteralKind.Integer, lit.Kind);
    }

    [TestMethod]
    public void Parse_FloatLiteral()
    {
        var result = PythonParser.ParseExpression("3.14");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<LiteralExpression>(result.Value);
        var lit = (LiteralExpression)result.Value;
        Assert.AreEqual(3.14, lit.Value);
        Assert.AreEqual(LiteralKind.Float, lit.Kind);
    }

    [TestMethod]
    public void Parse_StringLiteral()
    {
        var result = PythonParser.ParseExpression("\"hello\"");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<LiteralExpression>(result.Value);
        var lit = (LiteralExpression)result.Value;
        Assert.AreEqual("hello", lit.Value);
        Assert.AreEqual(LiteralKind.String, lit.Kind);
    }

    [TestMethod]
    public void Parse_BooleanTrue()
    {
        var result = PythonParser.ParseExpression("True");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<LiteralExpression>(result.Value);
        var lit = (LiteralExpression)result.Value;
        Assert.IsTrue((bool)lit.Value);
        Assert.AreEqual(LiteralKind.Boolean, lit.Kind);
    }

    [TestMethod]
    public void Parse_BooleanFalse()
    {
        var result = PythonParser.ParseExpression("False");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<LiteralExpression>(result.Value);
        var lit = (LiteralExpression)result.Value;
        Assert.IsFalse((bool)lit.Value);
        Assert.AreEqual(LiteralKind.Boolean, lit.Kind);
    }

    [TestMethod]
    public void Parse_None()
    {
        var result = PythonParser.ParseExpression("None");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<LiteralExpression>(result.Value);
        var lit = (LiteralExpression)result.Value;
        Assert.IsNull(lit.Value);
        Assert.AreEqual(LiteralKind.None, lit.Kind);
    }

    // ========================================
    // Names
    // ========================================

    [TestMethod]
    public void Parse_Name()
    {
        var result = PythonParser.ParseExpression("variable");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<NameExpression>(result.Value);
        var name = (NameExpression)result.Value;
        Assert.AreEqual("variable", name.Id);
    }

    // ========================================
    // Binary Operations
    // ========================================

    [TestMethod]
    public void Parse_Addition()
    {
        var result = PythonParser.ParseExpression("1 + 2");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<BinaryOperation>(result.Value);
        var binOp = (BinaryOperation)result.Value;
        Assert.AreEqual(BinaryOperator.Add, binOp.Op);
    }

    [TestMethod]
    public void Parse_Subtraction()
    {
        var result = PythonParser.ParseExpression("5 - 3");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<BinaryOperation>(result.Value);
        var binOp = (BinaryOperation)result.Value;
        Assert.AreEqual(BinaryOperator.Sub, binOp.Op);
    }

    [TestMethod]
    public void Parse_Multiplication()
    {
        var result = PythonParser.ParseExpression("2 * 3");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<BinaryOperation>(result.Value);
        var binOp = (BinaryOperation)result.Value;
        Assert.AreEqual(BinaryOperator.Mult, binOp.Op);
    }

    [TestMethod]
    public void Parse_Division()
    {
        var result = PythonParser.ParseExpression("10 / 2");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<BinaryOperation>(result.Value);
        var binOp = (BinaryOperation)result.Value;
        Assert.AreEqual(BinaryOperator.Div, binOp.Op);
    }

    // ========================================
    // Collections
    // ========================================

    [TestMethod]
    public void Parse_List()
    {
        var result = PythonParser.ParseExpression("[1, 2, 3]");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<ListExpression>(result.Value);
        var list = (ListExpression)result.Value;
        Assert.HasCount(3, list.Elements);
    }

    [TestMethod]
    public void Parse_EmptyList()
    {
        var result = PythonParser.ParseExpression("[]");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<ListExpression>(result.Value);
        var list = (ListExpression)result.Value;
        Assert.IsEmpty(list.Elements);
    }

    [TestMethod]
    public void Parse_Set()
    {
        var result = PythonParser.ParseExpression("{1, 2, 3}");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<SetExpression>(result.Value);
        var set = (SetExpression)result.Value;
        Assert.HasCount(3, set.Elements);
    }

    // ========================================
    // Comparison Operations
    // ========================================

    [TestMethod]
    public void Parse_Equality()
    {
        var result = PythonParser.ParseExpression("x == y");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<CompareOperation>(result.Value);
        var cmp = (CompareOperation)result.Value;
        Assert.AreEqual(CompareOperator.Eq, cmp.Ops[0]);
    }

    [TestMethod]
    public void Parse_LessThan()
    {
        var result = PythonParser.ParseExpression("x < y");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<CompareOperation>(result.Value);
        var cmp = (CompareOperation)result.Value;
        Assert.AreEqual(CompareOperator.Lt, cmp.Ops[0]);
    }

    // ========================================
    // Boolean Operations
    // ========================================

    [TestMethod]
    public void Parse_And()
    {
        var result = PythonParser.ParseExpression("a and b");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<BooleanOperation>(result.Value);
        var boolOp = (BooleanOperation)result.Value;
        Assert.AreEqual(BoolOperator.And, boolOp.Op);
    }

    [TestMethod]
    public void Parse_Or()
    {
        var result = PythonParser.ParseExpression("a or b");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<BooleanOperation>(result.Value);
        var boolOp = (BooleanOperation)result.Value;
        Assert.AreEqual(BoolOperator.Or, boolOp.Op);
    }

    // ========================================
    // Simple Statements
    // ========================================

    [TestMethod]
    public void Parse_PassStatement()
    {
        var result = PythonParser.ParseStatement("pass");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<PassStatement>(result.Value);
    }

    [TestMethod]
    public void Parse_BreakStatement()
    {
        var result = PythonParser.ParseStatement("break");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<BreakStatement>(result.Value);
    }

    [TestMethod]
    public void Parse_ContinueStatement()
    {
        var result = PythonParser.ParseStatement("continue");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<ContinueStatement>(result.Value);
    }

    [TestMethod]
    public void Parse_ReturnStatement()
    {
        var result = PythonParser.ParseStatement("return 42");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<ReturnStatement>(result.Value);
        var ret = (ReturnStatement)result.Value;
        Assert.IsNotNull(ret.Value);
    }

    [TestMethod]
    public void Parse_ReturnNone()
    {
        var result = PythonParser.ParseStatement("return");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<ReturnStatement>(result.Value);
        var ret = (ReturnStatement)result.Value;
        Assert.IsNull(ret.Value);
    }

    // ========================================
    // Assignment Statements
    // ========================================

    [TestMethod]
    public void Parse_SimpleAssignment()
    {
        var result = PythonParser.ParseStatement("x = 5");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<AssignmentStatement>(result.Value);
        var assign = (AssignmentStatement)result.Value;
        Assert.HasCount(1, assign.Targets);
    }

    [TestMethod]
    public void Parse_AnnotatedAssignment()
    {
        var result = PythonParser.ParseStatement("x: int = 5");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<AnnotatedAssignment>(result.Value);
        var assign = (AnnotatedAssignment)result.Value;
        Assert.IsNotNull(assign.Annotation);
        Assert.IsNotNull(assign.Value);
    }

    [TestMethod]
    public void Parse_AnnotationOnly()
    {
        var result = PythonParser.ParseStatement("x: int");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<AnnotatedAssignment>(result.Value);
        var assign = (AnnotatedAssignment)result.Value;
        Assert.IsNotNull(assign.Annotation);
        Assert.IsNull(assign.Value);
    }

    // ========================================
    // Import Statements
    // ========================================

    [TestMethod]
    public void Parse_SimpleImport()
    {
        var result = PythonParser.ParseStatement("import sys");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<ImportStatement>(result.Value);
        var import = (ImportStatement)result.Value;
        Assert.HasCount(1, import.Names);
        Assert.AreEqual("sys", import.Names[0].Name);
    }

    [TestMethod]
    public void Parse_ImportWithAlias()
    {
        var result = PythonParser.ParseStatement("import numpy as np");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<ImportStatement>(result.Value);
        var import = (ImportStatement)result.Value;
        Assert.AreEqual("numpy", import.Names[0].Name);
        Assert.AreEqual("np", import.Names[0].AsName);
    }

    [TestMethod]
    public void Parse_FromImport()
    {
        var result = PythonParser.ParseStatement("from os import path");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<ImportFromStatement>(result.Value);
        var import = (ImportFromStatement)result.Value;
        Assert.AreEqual("os", import.Module);
        Assert.HasCount(1, import.Names);
    }

    // ========================================
    // Function Definitions
    // ========================================

    [TestMethod]
    public void Parse_SimpleFunctionDef()
    {
        var result = PythonParser.ParseStatement("def foo():\n pass");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<FunctionDef>(result.Value);
        var func = (FunctionDef)result.Value;
        Assert.AreEqual("foo", func.Name);
        Assert.IsEmpty(func.Args.Args);
    }

    [TestMethod]
    public void Parse_FunctionWithParams()
    {
        var result = PythonParser.ParseStatement("def add(a, b):\n return a");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<FunctionDef>(result.Value);
        var func = (FunctionDef)result.Value;
        Assert.AreEqual("add", func.Name);
        Assert.HasCount(2, func.Args.Args);
    }

    [TestMethod]
    public void Parse_FunctionWithTypeHints()
    {
        var result = PythonParser.ParseStatement("def add(a: int, b: int) -> int:\n return a");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<FunctionDef>(result.Value);
        var func = (FunctionDef)result.Value;
        Assert.IsNotNull(func.Returns);
        Assert.HasCount(2, func.Args.Args);
    }

    // ========================================
    // Class Definitions
    // ========================================

    [TestMethod]
    public void Parse_SimpleClassDef()
    {
        var result = PythonParser.ParseStatement("class Person:\n pass");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<ClassDef>(result.Value);
        var cls = (ClassDef)result.Value;
        Assert.AreEqual("Person", cls.Name);
    }

    [TestMethod]
    public void Parse_ClassWithBase()
    {
        var result = PythonParser.ParseStatement("class Dog(Animal):\n pass");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<ClassDef>(result.Value);
        var cls = (ClassDef)result.Value;
        Assert.AreEqual("Dog", cls.Name);
        Assert.HasCount(1, cls.Bases);
    }

    // ========================================
    // Control Flow
    // ========================================

    [TestMethod]
    public void Parse_IfStatement()
    {
        var result = PythonParser.ParseStatement("if x > 0:\n pass");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<IfStatement>(result.Value);
        var ifStmt = (IfStatement)result.Value;
        Assert.IsNotNull(ifStmt.Test);
    }

    [TestMethod]
    public void Parse_WhileLoop()
    {
        var result = PythonParser.ParseStatement("while True:\n pass");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<WhileStatement>(result.Value);
        var whileStmt = (WhileStatement)result.Value;
        Assert.IsNotNull(whileStmt.Test);
    }

    [TestMethod]
    public void Parse_ForLoop()
    {
        var result = PythonParser.ParseStatement("for x in range(10):\n pass");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<ForStatement>(result.Value);
        var forStmt = (ForStatement)result.Value;
        Assert.IsNotNull(forStmt.Target);
        Assert.IsNotNull(forStmt.Iter);
    }

    // ========================================
    // Expressions
    // ========================================

    [TestMethod]
    public void Parse_CallExpression()
    {
        var result = PythonParser.ParseExpression("print(42)");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<CallExpression>(result.Value);
        var call = (CallExpression)result.Value;
        Assert.HasCount(1, call.Args);
    }

    [TestMethod]
    public void Parse_AttributeAccess()
    {
        var result = PythonParser.ParseExpression("obj.method");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<AttributeExpression>(result.Value);
        var attr = (AttributeExpression)result.Value;
        Assert.AreEqual("method", attr.Attr);
    }

    [TestMethod]
    public void Parse_SubscriptAccess()
    {
        var result = PythonParser.ParseExpression("arr[0]");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<SubscriptExpression>(result.Value);
    }

    [TestMethod]
    public void Parse_ConditionalExpression()
    {
        var result = PythonParser.ParseExpression("x if condition else y");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<ConditionalExpression>(result.Value);
        var cond = (ConditionalExpression)result.Value;
        Assert.IsNotNull(cond.Test);
        Assert.IsNotNull(cond.Body);
        Assert.IsNotNull(cond.OrElse);
    }

    // ========================================
    // Module
    // ========================================

    [TestMethod]
    public void Parse_EmptyModule()
    {
        var result = PythonParser.ParseModule("");
        Assert.IsTrue(result.Success);
        Assert.IsInstanceOfType<Module>(result.Value);
    }

    [TestMethod]
    public void Parse_ModuleWithStatements()
    {
        var code = "x = 5\ny = 10";
        var result = PythonParser.ParseModule(code);
        Assert.IsTrue(result.Success);
        var module = result.Value;
        Assert.HasCount(2, module.Body);
    }
}

