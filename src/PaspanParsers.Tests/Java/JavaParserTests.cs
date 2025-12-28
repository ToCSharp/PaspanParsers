using PaspanParsers.Java;

namespace PaspanParsers.Tests.Java;

[TestClass]
public class JavaParserTests
{
    // ========================================
    // Basic Type Declarations
    // ========================================

    [TestMethod]
    public void Parse_SimpleClass()
    {
        var code = @"
            public class Person {
                private String name;
                private int age;
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.TypeDeclarations);
        Assert.HasCount(1, result.TypeDeclarations);

        Assert.IsInstanceOfType<ClassDeclaration>(result.TypeDeclarations[0]);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.AreEqual("Person", classDecl.Name);
        Assert.IsTrue(classDecl.Modifiers.HasFlag(Modifiers.Public));
        Assert.IsNotNull(classDecl.Members);
        Assert.HasCount(2, classDecl.Members);
    }

    [TestMethod]
    public void Parse_SimpleInterface()
    {
        var code = @"
            public interface Comparable {
                int compareTo(Object other);
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<InterfaceDeclaration>(result.TypeDeclarations[0]);

        var interfaceDecl = (InterfaceDeclaration)result.TypeDeclarations[0];
        Assert.AreEqual("Comparable", interfaceDecl.Name);
        Assert.IsTrue(interfaceDecl.Modifiers.HasFlag(Modifiers.Public));
    }

    [TestMethod]
    public void Parse_SimpleEnum()
    {
        var code = @"
            public enum Color {
                RED,
                GREEN,
                BLUE
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<EnumDeclaration>(result.TypeDeclarations[0]);

        var enumDecl = (EnumDeclaration)result.TypeDeclarations[0];
        Assert.AreEqual("Color", enumDecl.Name);
        Assert.IsNotNull(enumDecl.Constants);
        Assert.HasCount(3, enumDecl.Constants);
        Assert.AreEqual("RED", enumDecl.Constants[0].Name);
        Assert.AreEqual("GREEN", enumDecl.Constants[1].Name);
        Assert.AreEqual("BLUE", enumDecl.Constants[2].Name);
    }

    [TestMethod]
    public void Parse_RecordDeclaration()
    {
        var code = @"
            public record Point(int x, int y) {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<RecordDeclaration>(result.TypeDeclarations[0]);

        var recordDecl = (RecordDeclaration)result.TypeDeclarations[0];
        Assert.AreEqual("Point", recordDecl.Name);
        Assert.IsNotNull(recordDecl.Components);
        Assert.HasCount(2, recordDecl.Components);
        Assert.AreEqual("x", recordDecl.Components[0].Name);
        Assert.AreEqual("y", recordDecl.Components[1].Name);
    }

    [TestMethod]
    public void Parse_EmptyClass()
    {
        var code = @"
            class Empty {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.AreEqual("Empty", classDecl.Name);
        Assert.AreEqual(Modifiers.None, classDecl.Modifiers);
    }

    // ========================================
    // Package and Imports
    // ========================================

    [TestMethod]
    public void Parse_PackageDeclaration()
    {
        var code = @"
            package com.example.app;
            
            public class Test {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.PackageDeclaration);
        Assert.AreEqual("com.example.app", result.PackageDeclaration.Name.ToString());
    }

    [TestMethod]
    public void Parse_ImportDeclarations()
    {
        var code = @"
            import java.util.List;
            import java.util.ArrayList;
            import java.util.*;
            
            public class Test {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.ImportDeclarations);
        Assert.HasCount(3, result.ImportDeclarations);

        Assert.IsInstanceOfType<SingleTypeImportDeclaration>(result.ImportDeclarations[0]);
        Assert.IsInstanceOfType<SingleTypeImportDeclaration>(result.ImportDeclarations[1]);
        Assert.IsInstanceOfType<TypeImportOnDemandDeclaration>(result.ImportDeclarations[2]);
    }

    [TestMethod]
    public void Parse_StaticImport()
    {
        var code = @"
            import static java.lang.Math.PI;
            import static java.lang.Math.*;
            
            public class Test {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.HasCount(2, result.ImportDeclarations);

        var import1 = (SingleTypeImportDeclaration)result.ImportDeclarations[0];
        Assert.IsTrue(import1.IsStatic);

        var import2 = (TypeImportOnDemandDeclaration)result.ImportDeclarations[1];
        Assert.IsTrue(import2.IsStatic);
    }

    // ========================================
    // Fields and Methods
    // ========================================

    [TestMethod]
    public void Parse_FieldDeclaration()
    {
        var code = @"
            public class Example {
                private int count;
                public static final double PI = 3.14;
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.HasCount(2, classDecl.Members);

        var field1 = (FieldDeclaration)classDecl.Members[0];
        Assert.IsTrue(field1.Modifiers.HasFlag(Modifiers.Private));

        var field2 = (FieldDeclaration)classDecl.Members[1];
        Assert.IsTrue(field2.Modifiers.HasFlag(Modifiers.Public));
        Assert.IsTrue(field2.Modifiers.HasFlag(Modifiers.Static));
        Assert.IsTrue(field2.Modifiers.HasFlag(Modifiers.Final));
    }

    [TestMethod]
    public void Parse_MethodDeclaration()
    {
        var code = @"
            public class Calculator {
                public int add(int a, int b) {
                    return a + b;
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.IsInstanceOfType<MethodDeclaration>(classDecl.Members[0]);

        var method = (MethodDeclaration)classDecl.Members[0];
        Assert.AreEqual("add", method.Name);
        Assert.IsNotNull(method.Parameters);
        Assert.HasCount(2, method.Parameters);
        Assert.IsNotNull(method.Body);
    }

    [TestMethod]
    public void Parse_Constructor()
    {
        var code = @"
            public class Person {
                public Person(String name, int age) {
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.IsInstanceOfType<ConstructorDeclaration>(classDecl.Members[0]);

        var ctor = (ConstructorDeclaration)classDecl.Members[0];
        Assert.AreEqual("Person", ctor.Name);
        Assert.HasCount(2, ctor.Parameters);
    }

    [TestMethod]
    public void Parse_AbstractMethod()
    {
        var code = @"
            public abstract class Shape {
                public abstract double area();
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];

        Assert.IsTrue(method.Modifiers.HasFlag(Modifiers.Abstract));
        Assert.IsNull(method.Body);
    }

    // ========================================
    // Generics
    // ========================================

    [TestMethod]
    public void Parse_GenericClass()
    {
        var code = @"
            public class Box<T> {
                private T value;
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.AreEqual("Box", classDecl.Name);
        Assert.IsNotNull(classDecl.TypeParameters);
        Assert.HasCount(1, classDecl.TypeParameters);
        Assert.AreEqual("T", classDecl.TypeParameters[0].Name);
    }

    [TestMethod]
    public void Parse_GenericClassWithBounds()
    {
        var code = @"
            public class NumberBox<T extends Number> {
                private T value;
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.IsNotNull(classDecl.TypeParameters);
        Assert.HasCount(1, classDecl.TypeParameters);

        var typeParam = classDecl.TypeParameters[0];
        Assert.AreEqual("T", typeParam.Name);
        Assert.IsNotNull(typeParam.Bounds);
        Assert.HasCount(1, typeParam.Bounds);
    }

    [TestMethod]
    public void Parse_GenericClassWithMultipleBounds()
    {
        var code = @"
            public class ComparableBox<T extends Number & Comparable> {
                private T value;
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var typeParam = classDecl.TypeParameters[0];

        Assert.IsNotNull(typeParam.Bounds);
        Assert.HasCount(2, typeParam.Bounds);
    }

    [TestMethod]
    public void Parse_MultipleTypeParameters()
    {
        var code = @"
            public class Pair<K, V> {
                private K key;
                private V value;
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.HasCount(2, classDecl.TypeParameters);
        Assert.AreEqual("K", classDecl.TypeParameters[0].Name);
        Assert.AreEqual("V", classDecl.TypeParameters[1].Name);
    }

    [TestMethod]
    public void Parse_GenericMethod()
    {
        var code = @"
            public class Utils {
                public <T> T identity(T value) {
                    return value;
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];

        Assert.IsNotNull(method.TypeParameters);
        Assert.HasCount(1, method.TypeParameters);
        Assert.AreEqual("T", method.TypeParameters[0].Name);
    }

    [TestMethod]
    public void Parse_GenericInterface()
    {
        var code = @"
            public interface Comparable<T> {
                int compareTo(T other);
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var interfaceDecl = (InterfaceDeclaration)result.TypeDeclarations[0];
        Assert.IsNotNull(interfaceDecl.TypeParameters);
        Assert.HasCount(1, interfaceDecl.TypeParameters);
    }

    [TestMethod]
    public void Parse_WildcardTypeArgument()
    {
        var code = @"
            public class Test {
                private List<?> list1;
                private List<? extends Number> list2;
                private List<? super Integer> list3;
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.HasCount(3, classDecl.Members);

        var field1 = (FieldDeclaration)classDecl.Members[0];
        var type1 = (ReferenceTypeReference)field1.Type;
        Assert.IsNotNull(type1.TypeArguments);
        Assert.IsInstanceOfType<WildcardTypeArgument>(type1.TypeArguments[0]);

        var field2 = (FieldDeclaration)classDecl.Members[1];
        var type2 = (ReferenceTypeReference)field2.Type;
        var wildcard2 = (WildcardTypeArgument)type2.TypeArguments[0];
        Assert.IsInstanceOfType<ExtendsWildcardBounds>(wildcard2.Bounds);

        var field3 = (FieldDeclaration)classDecl.Members[2];
        var type3 = (ReferenceTypeReference)field3.Type;
        var wildcard3 = (WildcardTypeArgument)type3.TypeArguments[0];
        Assert.IsInstanceOfType<SuperWildcardBounds>(wildcard3.Bounds);
    }

    // ========================================
    // Expressions
    // ========================================

    [TestMethod]
    public void Parse_BinaryExpression()
    {
        var code = @"
            public class Math {
                public int calculate() {
                    return 2 + 3 * 4;
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];
        var returnStmt = (ReturnStatement)method.Body.Statements[0];

        Assert.IsInstanceOfType<BinaryExpression>(returnStmt.Expression);
        var binExpr = (BinaryExpression)returnStmt.Expression;
        Assert.AreEqual(BinaryOperator.Add, binExpr.Operator);
    }

    [TestMethod]
    public void Parse_TernaryExpression()
    {
        var code = @"
            public class Test {
                public int method(boolean flag) {
                    return flag ? 1 : 0;
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];
        var returnStmt = (ReturnStatement)method.Body.Statements[0];

        Assert.IsInstanceOfType<TernaryExpression>(returnStmt.Expression);
        var ternary = (TernaryExpression)returnStmt.Expression;
        Assert.IsNotNull(ternary.Condition);
        Assert.IsNotNull(ternary.TrueExpression);
        Assert.IsNotNull(ternary.FalseExpression);
    }

    [TestMethod]
    public void Parse_MethodInvocation()
    {
        var code = @"
            public class Test {
                public void method() {
                    System.out.println(123);
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];
        var exprStmt = (ExpressionStatement)method.Body.Statements[0];

        Assert.IsInstanceOfType<MethodInvocationExpression>(exprStmt.Expression);
        var invocation = (MethodInvocationExpression)exprStmt.Expression;
        Assert.IsNotNull(invocation.Arguments);
    }

    [TestMethod]
    public void Parse_FieldAccess()
    {
        var code = @"
            public class Test {
                public void method() {
                    int x = this.value;
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];
        var localVar = (LocalVariableDeclarationStatement)method.Body.Statements[0];

        Assert.IsInstanceOfType<FieldAccessExpression>(localVar.Variables[0].Initializer);
    }

    [TestMethod]
    public void Parse_ArrayAccess()
    {
        var code = @"
            public class Test {
                public void method(int[] arr) {
                    int x = arr[0];
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];
        var localVar = (LocalVariableDeclarationStatement)method.Body.Statements[0];

        Assert.IsInstanceOfType<ArrayAccessExpression>(localVar.Variables[0].Initializer);
    }

    [TestMethod]
    public void Parse_InstanceOfExpression()
    {
        var code = @"
            public class Test {
                public void method(Object obj) {
                    if (obj instanceof String) {
                        return;
                    }
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];
        var ifStmt = (IfStatement)method.Body.Statements[0];

        Assert.IsInstanceOfType<InstanceOfExpression>(ifStmt.Condition);
        var instanceof = (InstanceOfExpression)ifStmt.Condition;
        Assert.IsNotNull(instanceof.Type);
    }

    [TestMethod]
    public void Parse_Literals()
    {
        var code = @"
            public class Test {
                int a = 42;
                double b = 3.14;
                String c = ""hello"";
                char d = 'x';
                boolean e = true;
                Object f = null;
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.HasCount(6, classDecl.Members);

        var field1 = (FieldDeclaration)classDecl.Members[0];
        Assert.IsInstanceOfType<LiteralExpression>(field1.Variables[0].Initializer);
        var lit1 = (LiteralExpression)field1.Variables[0].Initializer;
        Assert.AreEqual(LiteralKind.Integer, lit1.Kind);

        var field2 = (FieldDeclaration)classDecl.Members[1];
        var lit2 = (LiteralExpression)field2.Variables[0].Initializer;
        Assert.AreEqual(LiteralKind.FloatingPoint, lit2.Kind);

        var field3 = (FieldDeclaration)classDecl.Members[2];
        var lit3 = (LiteralExpression)field3.Variables[0].Initializer;
        Assert.AreEqual(LiteralKind.String, lit3.Kind);

        var field4 = (FieldDeclaration)classDecl.Members[3];
        var lit4 = (LiteralExpression)field4.Variables[0].Initializer;
        Assert.AreEqual(LiteralKind.Character, lit4.Kind);

        var field5 = (FieldDeclaration)classDecl.Members[4];
        var lit5 = (LiteralExpression)field5.Variables[0].Initializer;
        Assert.AreEqual(LiteralKind.Boolean, lit5.Kind);

        var field6 = (FieldDeclaration)classDecl.Members[5];
        var lit6 = (LiteralExpression)field6.Variables[0].Initializer;
        Assert.AreEqual(LiteralKind.Null, lit6.Kind);
    }

    [TestMethod]
    public void Parse_ThisExpression()
    {
        var code = @"
            public class Test {
                public void method() {
                    Test t = this;
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];
        var localVar = (LocalVariableDeclarationStatement)method.Body.Statements[0];

        Assert.IsInstanceOfType<ThisExpression>(localVar.Variables[0].Initializer);
    }

    [TestMethod]
    public void Parse_ParenthesizedExpression()
    {
        var code = @"
            public class Test {
                public int method() {
                    return (1 + 2) * 3;
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];
        var returnStmt = (ReturnStatement)method.Body.Statements[0];

        var binExpr = (BinaryExpression)returnStmt.Expression;
        Assert.IsInstanceOfType<ParenthesizedExpression>(binExpr.Left);
    }

    // ========================================
    // Statements
    // ========================================

    [TestMethod]
    public void Parse_IfStatement()
    {
        var code = @"
            public class Test {
                public void method(int x) {
                    if (x > 0) {
                        return;
                    } else {
                        throw null;
                    }
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];
        var ifStmt = (IfStatement)method.Body.Statements[0];

        Assert.IsNotNull(ifStmt.Condition);
        Assert.IsNotNull(ifStmt.ThenStatement);
        Assert.IsNotNull(ifStmt.ElseStatement);
    }

    [TestMethod]
    public void Parse_WhileLoop()
    {
        var code = @"
            public class Test {
                public void method() {
                    while (true) {
                        break;
                    }
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];
        var whileStmt = (WhileStatement)method.Body.Statements[0];

        Assert.IsNotNull(whileStmt.Condition);
        Assert.IsNotNull(whileStmt.Body);
    }

    [TestMethod]
    public void Parse_DoWhileLoop()
    {
        var code = @"
            public class Test {
                public void method() {
                    do {
                        break;
                    } while (true);
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];
        var doStmt = (DoStatement)method.Body.Statements[0];

        Assert.IsNotNull(doStmt.Body);
        Assert.IsNotNull(doStmt.Condition);
    }

    [TestMethod]
    public void Parse_ForLoop()
    {
        var code = @"
            public class Test {
                public void method() {
                    for (int i = 0; i < 10; i = i + 1) {
                        continue;
                    }
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];
        var forStmt = (ForStatement)method.Body.Statements[0];

        Assert.IsNotNull(forStmt.Initializers);
        Assert.IsNotNull(forStmt.Condition);
        Assert.IsNotNull(forStmt.Updates);
    }

    [TestMethod]
    public void Parse_BreakStatement()
    {
        var code = @"
            public class Test {
                public void method() {
                    while (true) {
                        break;
                    }
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];
        var whileStmt = (WhileStatement)method.Body.Statements[0];
        var block = (BlockStatement)whileStmt.Body;

        Assert.IsInstanceOfType<BreakStatement>(block.Statements[0]);
    }

    [TestMethod]
    public void Parse_ContinueStatement()
    {
        var code = @"
            public class Test {
                public void method() {
                    while (true) {
                        continue;
                    }
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];
        var whileStmt = (WhileStatement)method.Body.Statements[0];
        var block = (BlockStatement)whileStmt.Body;

        Assert.IsInstanceOfType<ContinueStatement>(block.Statements[0]);
    }

    [TestMethod]
    public void Parse_ReturnStatement()
    {
        var code = @"
            public class Test {
                public int method() {
                    return 42;
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];
        var returnStmt = (ReturnStatement)method.Body.Statements[0];

        Assert.IsNotNull(returnStmt.Expression);
    }

    [TestMethod]
    public void Parse_ThrowStatement()
    {
        var code = @"
            public class Test {
                public void method() {
                    throw new RuntimeException();
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];
        var throwStmt = (ThrowStatement)method.Body.Statements[0];

        Assert.IsNotNull(throwStmt.Expression);
    }

    [TestMethod]
    public void Parse_LocalVariableDeclaration()
    {
        var code = @"
            public class Test {
                public void method() {
                    int x = 10;
                    String name = ""John"";
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];

        Assert.HasCount(2, method.Body.Statements);
        Assert.IsInstanceOfType<LocalVariableDeclarationStatement>(method.Body.Statements[0]);
        Assert.IsInstanceOfType<LocalVariableDeclarationStatement>(method.Body.Statements[1]);
    }

    [TestMethod]
    public void Parse_FinalLocalVariable()
    {
        var code = @"
            public class Test {
                public void method() {
                    final int x = 10;
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];
        var localVar = (LocalVariableDeclarationStatement)method.Body.Statements[0];

        Assert.IsTrue(localVar.IsFinal);
    }

    [TestMethod]
    public void Parse_BlockStatement()
    {
        var code = @"
            public class Test {
                public void method() {
                    {
                        int x = 10;
                    }
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];

        Assert.IsInstanceOfType<BlockStatement>(method.Body.Statements[0]);
    }

    // ========================================
    // Annotations
    // ========================================

    [TestMethod]
    public void Parse_MarkerAnnotation()
    {
        var code = @"
            @Override
            public class Test {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.IsNotNull(classDecl.Annotations);
        Assert.HasCount(1, classDecl.Annotations);
        Assert.AreEqual("Override", classDecl.Annotations[0].Name.ToString());
    }

    [TestMethod]
    public void Parse_SingleElementAnnotation()
    {
        var code = @"
            @SuppressWarnings(""unchecked"")
            public class Test {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.IsNotNull(classDecl.Annotations);
        Assert.HasCount(1, classDecl.Annotations);

        var annotation = classDecl.Annotations[0];
        Assert.AreEqual("SuppressWarnings", annotation.Name.ToString());
        Assert.IsNotNull(annotation.Elements);
        Assert.HasCount(1, annotation.Elements);
    }

    [TestMethod]
    public void Parse_NormalAnnotation()
    {
        var code = @"
            @Author(name = ""John"", date = ""2024"")
            public class Test {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var annotation = classDecl.Annotations[0];

        Assert.AreEqual("Author", annotation.Name.ToString());
        Assert.IsNotNull(annotation.Elements);
        Assert.HasCount(2, annotation.Elements);
        Assert.AreEqual("name", annotation.Elements[0].Name);
        Assert.AreEqual("date", annotation.Elements[1].Name);
    }

    [TestMethod]
    public void Parse_MultipleAnnotations()
    {
        var code = @"
            @Override
            @Deprecated
            @SuppressWarnings(""all"")
            public class Test {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.HasCount(3, classDecl.Annotations);
    }

    [TestMethod]
    public void Parse_AnnotationsOnMethod()
    {
        var code = @"
            public class Test {
                @Override
                public String toString() {
                    return """";
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];

        Assert.IsNotNull(method.Annotations);
        Assert.HasCount(1, method.Annotations);
    }

    [TestMethod]
    public void Parse_AnnotationsOnField()
    {
        var code = @"
            public class Test {
                @Deprecated
                private int value;
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var field = (FieldDeclaration)classDecl.Members[0];

        Assert.IsNotNull(field.Annotations);
        Assert.HasCount(1, field.Annotations);
    }

    // ========================================
    // Modifiers
    // ========================================

    [TestMethod]
    public void Parse_PublicClass()
    {
        var code = @"
            public class Test {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.IsTrue(classDecl.Modifiers.HasFlag(Modifiers.Public));
    }

    [TestMethod]
    public void Parse_AbstractClass()
    {
        var code = @"
            public abstract class Test {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.IsTrue(classDecl.Modifiers.HasFlag(Modifiers.Abstract));
    }

    [TestMethod]
    public void Parse_FinalClass()
    {
        var code = @"
            public final class Test {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.IsTrue(classDecl.Modifiers.HasFlag(Modifiers.Final));
    }

    [TestMethod]
    public void Parse_MultipleModifiers()
    {
        var code = @"
            public abstract class Test {
                protected static final int VALUE = 42;
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.IsTrue(classDecl.Modifiers.HasFlag(Modifiers.Public));
        Assert.IsTrue(classDecl.Modifiers.HasFlag(Modifiers.Abstract));

        var field = (FieldDeclaration)classDecl.Members[0];
        Assert.IsTrue(field.Modifiers.HasFlag(Modifiers.Protected));
        Assert.IsTrue(field.Modifiers.HasFlag(Modifiers.Static));
        Assert.IsTrue(field.Modifiers.HasFlag(Modifiers.Final));
    }

    // ========================================
    // Array Types
    // ========================================

    [TestMethod]
    public void Parse_ArrayType()
    {
        var code = @"
            public class Test {
                private int[] numbers;
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var field = (FieldDeclaration)classDecl.Members[0];

        Assert.IsInstanceOfType<ArrayTypeReference>(field.Type);
        var arrayType = (ArrayTypeReference)field.Type;
        Assert.IsInstanceOfType<PrimitiveTypeReference>(arrayType.ElementType);
    }

    [TestMethod]
    public void Parse_MultiDimensionalArray()
    {
        var code = @"
            public class Test {
                private int[][] matrix;
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var field = (FieldDeclaration)classDecl.Members[0];

        Assert.IsInstanceOfType<ArrayTypeReference>(field.Type);
        var arrayType1 = (ArrayTypeReference)field.Type;
        Assert.IsInstanceOfType<ArrayTypeReference>(arrayType1.ElementType);
    }

    // ========================================
    // Parameters
    // ========================================

    [TestMethod]
    public void Parse_VarargsParameter()
    {
        var code = @"
            public class Test {
                public void method(String... args) {
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];

        Assert.HasCount(1, method.Parameters);
        var param = method.Parameters[0];
        Assert.IsTrue(param.IsVarArgs);
    }

    [TestMethod]
    public void Parse_FinalParameter()
    {
        var code = @"
            public class Test {
                public void method(final int x) {
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];

        var param = method.Parameters[0];
        Assert.IsTrue(param.IsFinal);
    }

    // ========================================
    // Class Inheritance
    // ========================================

    [TestMethod]
    public void Parse_ClassExtendsClause()
    {
        var code = @"
            public class Employee extends Person {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.IsNotNull(classDecl.SuperClass);
    }

    [TestMethod]
    public void Parse_ClassImplementsClause()
    {
        var code = @"
            public class MyList implements List, Serializable {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.IsNotNull(classDecl.SuperInterfaces);
        Assert.HasCount(2, classDecl.SuperInterfaces);
    }

    [TestMethod]
    public void Parse_InterfaceExtendsClause()
    {
        var code = @"
            public interface MyInterface extends List, Collection {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var interfaceDecl = (InterfaceDeclaration)result.TypeDeclarations[0];
        Assert.IsNotNull(interfaceDecl.ExtendsInterfaces);
        Assert.HasCount(2, interfaceDecl.ExtendsInterfaces);
    }

    // ========================================
    // Sealed Classes (Java 17+)
    // ========================================

    [TestMethod]
    public void Parse_SealedClass()
    {
        var code = @"
            public sealed class Shape permits Circle, Rectangle {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.IsTrue(classDecl.Modifiers.HasFlag(Modifiers.Sealed));
        Assert.IsNotNull(classDecl.PermittedSubclasses);
        Assert.HasCount(2, classDecl.PermittedSubclasses);
    }

    [TestMethod]
    public void Parse_SealedInterface()
    {
        var code = @"
            public sealed interface Shape permits Circle, Rectangle {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var interfaceDecl = (InterfaceDeclaration)result.TypeDeclarations[0];
        Assert.IsTrue(interfaceDecl.Modifiers.HasFlag(Modifiers.Sealed));
        Assert.IsNotNull(interfaceDecl.PermittedSubtypes);
        Assert.HasCount(2, interfaceDecl.PermittedSubtypes);
    }

    // ========================================
    // Records (Java 16+)
    // ========================================

    [TestMethod]
    public void Parse_RecordWithMethods()
    {
        var code = @"
            public record Point(int x, int y) {
                public double distance() {
                    return 0.0;
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var recordDecl = (RecordDeclaration)result.TypeDeclarations[0];
        Assert.IsNotNull(recordDecl.Members);
        Assert.HasCount(1, recordDecl.Members);
    }

    [TestMethod]
    public void Parse_RecordWithGenericTypes()
    {
        var code = @"
            public record Box<T>(T value) {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var recordDecl = (RecordDeclaration)result.TypeDeclarations[0];
        Assert.IsNotNull(recordDecl.TypeParameters);
        Assert.HasCount(1, recordDecl.TypeParameters);
    }

    // ========================================
    // Enums with Methods
    // ========================================

    [TestMethod]
    public void Parse_EnumWithMethods()
    {
        var code = @"
            public enum Color {
                RED,
                GREEN,
                BLUE;
                
                public String getName() {
                    return """";
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var enumDecl = (EnumDeclaration)result.TypeDeclarations[0];
        Assert.HasCount(3, enumDecl.Constants);
        Assert.IsNotNull(enumDecl.Members);
        Assert.HasCount(1, enumDecl.Members);
    }

    // ========================================
    // Var Type (Java 10+)
    // ========================================

    [TestMethod]
    public void Parse_VarLocalVariable()
    {
        var code = @"
            public class Test {
                public void method() {
                    var x = 10;
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var method = (MethodDeclaration)classDecl.Members[0];
        var localVar = (LocalVariableDeclarationStatement)method.Body.Statements[0];

        Assert.IsInstanceOfType<VarTypeReference>(localVar.Type);
    }

    // ========================================
    // Integration Tests
    // ========================================

    [TestMethod]
    public void Parse_CompleteJavaFile()
    {
        var code = @"
            package com.example.app;
            
            import java.util.List;
            import java.util.ArrayList;
            
            public class Person {
                private String name;
                private int age;
                
                public Person(String name, int age) {
                    this.name = name;
                    this.age = age;
                }
                
                public String getName() {
                    return name;
                }
                
                public int getAge() {
                    return age;
                }
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.PackageDeclaration);
        Assert.HasCount(2, result.ImportDeclarations);
        Assert.HasCount(1, result.TypeDeclarations);

        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        Assert.AreEqual("Person", classDecl.Name);
        Assert.HasCount(5, classDecl.Members); // 2 fields + 1 constructor + 2 methods
    }

    [TestMethod]
    public void Parse_MultipleClasses()
    {
        var code = @"
            class Class1 {
            }
            
            class Class2 {
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.HasCount(2, result.TypeDeclarations);
    }

    [TestMethod]
    public void Parse_ComplexGenerics()
    {
        var code = @"
            public class Test {
                private Map<String, List<Integer>> map;
            }
        ";

        var result = JavaParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.TypeDeclarations[0];
        var field = (FieldDeclaration)classDecl.Members[0];
        var type = (ReferenceTypeReference)field.Type;

        Assert.IsNotNull(type.TypeArguments);
        Assert.HasCount(2, type.TypeArguments);
    }
}

