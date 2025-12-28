using PaspanParsers.CSharp;

namespace PaspanParsers.Tests.CSharp;

[TestClass]
public class CSharpParserTests
{
    [TestMethod]
    public void Parse_SimpleClass()
    {
        var code = @"
            public class Person
            {
                public string Name { get; set; }
                public int Age { get; set; }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Members);
        Assert.HasCount(1, result.Members);

        Assert.IsInstanceOfType<ClassDeclaration>(result.Members[0]);
        var classDecl = (ClassDeclaration)result.Members[0];
        Assert.AreEqual("Person", classDecl.Name);
        Assert.AreEqual(Modifiers.Public, classDecl.Modifiers);
        Assert.IsNotNull(classDecl.Members);
        Assert.HasCount(2, classDecl.Members);
    }

    [TestMethod]
    public void Parse_UsingDirectives()
    {
        var code = @"
            using System;
            using System.Collections.Generic;
            using static System.Math;
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Usings);
        Assert.HasCount(3, result.Usings);

        Assert.IsInstanceOfType<UsingNamespaceDirective>(result.Usings[0]);
        Assert.IsInstanceOfType<UsingNamespaceDirective>(result.Usings[1]);
        Assert.IsInstanceOfType<UsingStaticDirective>(result.Usings[2]);
    }

    [TestMethod]
    public void Parse_MethodDeclaration()
    {
        var code = @"
            public class Calculator
            {
                public int Add(int a, int b)
                {
                    return a + b;
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsInstanceOfType<MethodDeclaration>(classDecl.Members![0]);

        var methodDecl = (MethodDeclaration)classDecl.Members![0];

        Assert.AreEqual("Add", methodDecl.Name);
        Assert.IsNotNull(methodDecl.Parameters);
        Assert.HasCount(2, methodDecl.Parameters);
        Assert.IsNotNull(methodDecl.Body);
    }

    [TestMethod]
    public void Parse_ExpressionBodiedMethod()
    {
        var code = @"
            public class Calculator
            {
                public int Add(int a, int b) => a + b;
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);
        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsInstanceOfType<MethodDeclaration>(classDecl.Members![0]);
        var methodDecl = (MethodDeclaration)classDecl.Members![0];

        Assert.AreEqual("Add", methodDecl.Name);
        Assert.IsInstanceOfType<ExpressionMethodBody>(methodDecl.Body);
    }

    [TestMethod]
    public void Parse_FieldDeclaration()
    {
        var code = @"
            public class Example
            {
                private int _count;
                public const double PI = 3.14159;
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);
        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsNotNull(classDecl.Members);
        Assert.HasCount(2, classDecl.Members);

        Assert.IsInstanceOfType<FieldDeclaration>(classDecl.Members[0]);
        var field1 = (FieldDeclaration)classDecl.Members[0];
        Assert.AreEqual(Modifiers.Private, field1.Modifiers);

        Assert.IsInstanceOfType<FieldDeclaration>(classDecl.Members[1]);
        var field2 = (FieldDeclaration)classDecl.Members[1];
        Assert.IsTrue(field2.Modifiers.HasFlag(Modifiers.Public));
        Assert.IsTrue(field2.Modifiers.HasFlag(Modifiers.Const));
    }

    [TestMethod]
    public void Parse_PropertyWithAccessors()
    {
        var code = @"
            public class Person
            {
                private int _age;
                public int Age
                {
                    get { return _age; }
                    set { _age = value; }
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);
        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsInstanceOfType<PropertyDeclaration>(classDecl.Members![1]);
        var propertyDecl = (PropertyDeclaration)classDecl.Members![1];

        Assert.AreEqual("Age", propertyDecl.Name);
        Assert.IsNotNull(propertyDecl.Accessors);
        Assert.HasCount(2, propertyDecl.Accessors);
        Assert.AreEqual(AccessorKind.Get, propertyDecl.Accessors[0].Kind);
        Assert.AreEqual(AccessorKind.Set, propertyDecl.Accessors[1].Kind);
    }

    [TestMethod]
    public void Parse_IfStatement()
    {
        var code = @"
            public class Test
            {
                public void Method(int x)
                {
                    if (x > 0)
                    {
                        return;
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsInstanceOfType<MethodDeclaration>(classDecl.Members![0]);

        var methodDecl = (MethodDeclaration)classDecl.Members![0];
        Assert.IsInstanceOfType<BlockMethodBody>(methodDecl.Body);

        var blockBody = (BlockMethodBody)methodDecl.Body;

        Assert.IsInstanceOfType<IfStatement>(blockBody.Block.Statements![0]);


        var ifStmt = (IfStatement)blockBody.Block.Statements![0];
        Assert.IsNotNull(ifStmt.Condition);
        Assert.IsNotNull(ifStmt.ThenStatement);
        Assert.IsNotNull(ifStmt.ElseStatement);
    }

    [TestMethod]
    public void Parse_WhileLoop()
    {
        var code = @"
            public class Test
            {
                public void Method()
                {
                    while (true)
                    {
                        break;
                    }
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsInstanceOfType<MethodDeclaration>(classDecl.Members![0]);

        var methodDecl = (MethodDeclaration)classDecl.Members![0];
        Assert.IsInstanceOfType<BlockMethodBody>(methodDecl.Body);

        var blockBody = (BlockMethodBody)methodDecl.Body;

        Assert.IsInstanceOfType<WhileStatement>(blockBody.Block.Statements![0]);


        var whileStmt = (WhileStatement)blockBody.Block.Statements![0];
        Assert.IsNotNull(whileStmt.Condition);
        Assert.IsInstanceOfType<BlockStatement>(whileStmt.Body);
    }

    [TestMethod]
    public void Parse_ForLoop()
    {
        var code = @"
            public class Test
            {
                public void Method()
                {
                    for (int i = 0; i < 10; i = i + 1)
                    {
                        continue;
                    }
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsInstanceOfType<MethodDeclaration>(classDecl.Members![0]);

        var methodDecl = (MethodDeclaration)classDecl.Members![0];
        Assert.IsInstanceOfType<BlockMethodBody>(methodDecl.Body);

        var blockBody = (BlockMethodBody)methodDecl.Body;

        Assert.IsInstanceOfType<ForStatement>(blockBody.Block.Statements![0]);


        var forStmt = (ForStatement)blockBody.Block.Statements![0];
        Assert.IsNotNull(forStmt.Initializers);
        Assert.IsNotNull(forStmt.Condition);
        Assert.IsNotNull(forStmt.Iterators);
    }

    [TestMethod]
    public void Parse_Interface()
    {
        var code = @"
            public interface IAnimal
            {
                string Name { get; set; }
                void MakeSound();
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<InterfaceDeclaration>(result.Members![0]);

        var interfaceDecl = (InterfaceDeclaration)result.Members![0];
        Assert.AreEqual("IAnimal", interfaceDecl.Name);
        Assert.AreEqual(Modifiers.Public, interfaceDecl.Modifiers);
        Assert.IsNotNull(interfaceDecl.Members);
        Assert.HasCount(2, interfaceDecl.Members);
    }

    [TestMethod]
    public void Parse_Enum()
    {
        var code = @"
            public enum Color
            {
                Red,
                Green,
                Blue
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<EnumDeclaration>(result.Members![0]);

        var enumDecl = (EnumDeclaration)result.Members![0];
        Assert.AreEqual("Color", enumDecl.Name);
        Assert.IsNotNull(enumDecl.Members);
        Assert.HasCount(3, enumDecl.Members);
        Assert.AreEqual("Red", enumDecl.Members[0].Name);
        Assert.AreEqual("Green", enumDecl.Members[1].Name);
        Assert.AreEqual("Blue", enumDecl.Members[2].Name);
    }

    [TestMethod]
    public void Parse_EnumWithValues()
    {
        var code = @"
            public enum StatusCode
            {
                Success = 200,
                NotFound = 404,
                ServerError = 500
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<EnumDeclaration>(result.Members![0]);

        var enumDecl = (EnumDeclaration)result.Members![0];
        Assert.HasCount(3, enumDecl.Members);

        var success = enumDecl.Members[0];
        Assert.IsNotNull(success.Value);
        Assert.IsInstanceOfType<LiteralExpression>(success.Value);

        var literal = (LiteralExpression)success.Value;
        // Numeric value should be 200, regardless of whether it's int or decimal
        Assert.AreEqual(200m, Convert.ToDecimal(literal.Value));
    }

    [TestMethod]
    public void Parse_Struct()
    {
        var code = @"
            public struct Point
            {
                public int X { get; set; }
                public int Y { get; set; }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<StructDeclaration>(result.Members![0]);

        var structDecl = (StructDeclaration)result.Members![0];
        Assert.AreEqual("Point", structDecl.Name);
        Assert.AreEqual(Modifiers.Public, structDecl.Modifiers);
    }

    [TestMethod]
    public void Parse_Namespace()
    {
        var code = @"
            namespace MyApp.Domain
            {
                public class Person
                {
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<NamespaceDeclaration>(result.Members![0]);

        var namespaceDecl = (NamespaceDeclaration)result.Members![0];
        Assert.IsNotNull(namespaceDecl.Members);
        Assert.HasCount(1, namespaceDecl.Members);
    }

    [TestMethod]
    public void Parse_Constructor()
    {
        var code = @"
            public class Person
            {
                public Person(string name)
                {
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsInstanceOfType<ConstructorDeclaration>(classDecl.Members![0]);

        var ctorDecl = (ConstructorDeclaration)classDecl.Members![0];

        Assert.AreEqual("Person", ctorDecl.Name);
        Assert.IsNotNull(ctorDecl.Parameters);
        Assert.HasCount(1, ctorDecl.Parameters);
    }

    [TestMethod]
    public void Parse_BinaryExpression()
    {
        var code = @"
            public class Math
            {
                public int Calculate()
                {
                    return 2 + 3 * 4;
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsInstanceOfType<MethodDeclaration>(classDecl.Members![0]);

        var methodDecl = (MethodDeclaration)classDecl.Members![0];
        Assert.IsInstanceOfType<BlockMethodBody>(methodDecl.Body);

        var blockBody = (BlockMethodBody)methodDecl.Body;
        Assert.IsInstanceOfType<ReturnStatement>(blockBody.Block.Statements![0]);

        var returnStmt = (ReturnStatement)blockBody.Block.Statements![0];

        Assert.IsNotNull(returnStmt.Expression);
        Assert.IsInstanceOfType<BinaryExpression>(returnStmt.Expression);

        var addExpr = (BinaryExpression)returnStmt.Expression;
        Assert.AreEqual(BinaryOperator.Add, addExpr.Operator);
    }

    [TestMethod]
    public void Parse_MethodInvocation()
    {
        var code = @"
            public class Test
            {
                public void Method()
                {
                    Console.WriteLine(123);
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsInstanceOfType<MethodDeclaration>(classDecl.Members![0]);

        var methodDecl = (MethodDeclaration)classDecl.Members![0];
        Assert.IsInstanceOfType<BlockMethodBody>(methodDecl.Body);

        var blockBody = (BlockMethodBody)methodDecl.Body;
        Assert.IsInstanceOfType<ExpressionStatement>(blockBody.Block.Statements![0]);

        var exprStmt = (ExpressionStatement)blockBody.Block.Statements![0];

        Assert.IsInstanceOfType<InvocationExpression>(exprStmt.Expression);


        var invocation = (InvocationExpression)exprStmt.Expression;
        Assert.IsNotNull(invocation.Arguments);
        Assert.HasCount(1, invocation.Arguments);
    }

    [TestMethod]
    public void Parse_ArrayType()
    {
        var code = @"
            public class Test
            {
                public int[] Numbers { get; set; }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsInstanceOfType<PropertyDeclaration>(classDecl.Members![0]);

        var propertyDecl = (PropertyDeclaration)classDecl.Members![0];

        Assert.IsInstanceOfType<ArrayTypeReference>(propertyDecl.Type);


        var arrayType = (ArrayTypeReference)propertyDecl.Type;
        Assert.AreEqual(1, arrayType.Rank);
        Assert.IsInstanceOfType<PredefinedTypeReference>(arrayType.ElementType);
    }

    [TestMethod]
    public void Parse_NullableType()
    {
        var code = @"
            public class Test
            {
                public int? NullableInt { get; set; }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsInstanceOfType<PropertyDeclaration>(classDecl.Members![0]);

        var propertyDecl = (PropertyDeclaration)classDecl.Members![0];

        Assert.IsInstanceOfType<PredefinedTypeReference>(propertyDecl.Type);


        var nullableType = (PredefinedTypeReference)propertyDecl.Type;
        Assert.IsTrue(nullableType.IsNullable);
    }

    [TestMethod]
    public void Parse_LocalVariable()
    {
        var code = @"
            public class Test
            {
                public void Method()
                {
                    int x = 10;
                    string name = ""John"";
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsInstanceOfType<MethodDeclaration>(classDecl.Members![0]);

        var methodDecl = (MethodDeclaration)classDecl.Members![0];
        Assert.IsInstanceOfType<BlockMethodBody>(methodDecl.Body);

        var blockBody = (BlockMethodBody)methodDecl.Body;

        Assert.HasCount(2, blockBody.Block.Statements);
        Assert.IsInstanceOfType<LocalDeclarationStatement>(blockBody.Block.Statements[0]);
        Assert.IsInstanceOfType<LocalDeclarationStatement>(blockBody.Block.Statements[1]);
    }

    [TestMethod]
    public void Parse_ConditionalExpression()
    {
        var code = @"
            public class Test
            {
                public int Method(bool flag)
                {
                    return flag ? 1 : 0;
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsInstanceOfType<MethodDeclaration>(classDecl.Members![0]);

        var methodDecl = (MethodDeclaration)classDecl.Members![0];
        Assert.IsInstanceOfType<BlockMethodBody>(methodDecl.Body);

        var blockBody = (BlockMethodBody)methodDecl.Body;
        Assert.IsInstanceOfType<ReturnStatement>(blockBody.Block.Statements![0]);

        var returnStmt = (ReturnStatement)blockBody.Block.Statements![0];

        Assert.IsInstanceOfType<ConditionalExpression>(returnStmt.Expression);


        var conditional = (ConditionalExpression)returnStmt.Expression;
        Assert.IsNotNull(conditional.Condition);
        Assert.IsNotNull(conditional.TrueExpression);
        Assert.IsNotNull(conditional.FalseExpression);
    }

    [TestMethod]
    public void Parse_ClassWithBaseType()
    {
        var code = @"
            public class Employee : Person
            {
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsNotNull(classDecl.BaseTypes);
        Assert.HasCount(1, classDecl.BaseTypes);
    }

    [TestMethod]
    public void Parse_MultipleModifiers()
    {
        var code = @"
            public abstract class BaseClass
            {
                public virtual void Method() { }
                protected internal static readonly int Value = 42;
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsTrue(classDecl.Modifiers.HasFlag(Modifiers.Public));
        Assert.IsTrue(classDecl.Modifiers.HasFlag(Modifiers.Abstract));

        Assert.IsInstanceOfType<MethodDeclaration>(classDecl.Members![0]);


        var methodDecl = (MethodDeclaration)classDecl.Members![0];
        Assert.IsTrue(methodDecl.Modifiers.HasFlag(Modifiers.Public));
        Assert.IsTrue(methodDecl.Modifiers.HasFlag(Modifiers.Virtual));

        Assert.IsInstanceOfType<FieldDeclaration>(classDecl.Members![1]);


        var fieldDecl = (FieldDeclaration)classDecl.Members![1];
        Assert.IsTrue(fieldDecl.Modifiers.HasFlag(Modifiers.Protected));
        Assert.IsTrue(fieldDecl.Modifiers.HasFlag(Modifiers.Internal));
        Assert.IsTrue(fieldDecl.Modifiers.HasFlag(Modifiers.Static));
        Assert.IsTrue(fieldDecl.Modifiers.HasFlag(Modifiers.Readonly));
    }

    [TestMethod]
    public void Parse_GenericClass()
    {
        var code = @"
            public class List<T>
            {
                public void Add(T item) { }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.AreEqual("List", classDecl.Name);
        Assert.IsNotNull(classDecl.TypeParameters);
        Assert.HasCount(1, classDecl.TypeParameters);
        Assert.AreEqual("T", classDecl.TypeParameters[0].Name);
    }

    [TestMethod]
    public void Parse_GenericClassWithMultipleTypeParameters()
    {
        var code = @"
            public class Dictionary<TKey, TValue>
            {
                public void Add(TKey key, TValue value) { }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.AreEqual("Dictionary", classDecl.Name);
        Assert.IsNotNull(classDecl.TypeParameters);
        Assert.HasCount(2, classDecl.TypeParameters);
        Assert.AreEqual("TKey", classDecl.TypeParameters[0].Name);
        Assert.AreEqual("TValue", classDecl.TypeParameters[1].Name);
    }

    [TestMethod]
    public void Parse_GenericClassWithConstraints()
    {
        // Simplified test - just class constraint without new() for now
        var code = @"
            public class Repository<T> where T : class
            {
                public T Get() { return null; }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.AreEqual("Repository", classDecl.Name);
        Assert.IsNotNull(classDecl.TypeParameters);
        Assert.HasCount(1, classDecl.TypeParameters);
        Assert.AreEqual("T", classDecl.TypeParameters[0].Name);

        Assert.IsNotNull(classDecl.Constraints);
        Assert.HasCount(1, classDecl.Constraints);
        Assert.AreEqual("T", classDecl.Constraints[0].TypeParameterName);
        Assert.IsNotNull(classDecl.Constraints[0].Constraints);
        Assert.HasCount(1, classDecl.Constraints[0].Constraints);
        Assert.IsInstanceOfType<ClassConstraint>(classDecl.Constraints[0].Constraints[0]);
    }

    [TestMethod]
    public void Parse_GenericMethod()
    {
        var code = @"
            public class Utils
            {
                public T GetValue<T>(T defaultValue) { return defaultValue; }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsInstanceOfType<MethodDeclaration>(classDecl.Members![0]);

        var methodDecl = (MethodDeclaration)classDecl.Members![0];
        Assert.AreEqual("GetValue", methodDecl.Name);
        Assert.IsNotNull(methodDecl.TypeParameters);
        Assert.HasCount(1, methodDecl.TypeParameters);
        Assert.AreEqual("T", methodDecl.TypeParameters[0].Name);
    }

    [TestMethod]
    public void Parse_GenericMethodWithConstraints()
    {
        var code = @"
            public class Utils
            {
                public void Process<T>(T item) where T : struct { }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsInstanceOfType<MethodDeclaration>(classDecl.Members![0]);

        var methodDecl = (MethodDeclaration)classDecl.Members![0];
        Assert.AreEqual("Process", methodDecl.Name);
        Assert.IsNotNull(methodDecl.TypeParameters);
        Assert.HasCount(1, methodDecl.TypeParameters);
        Assert.IsNotNull(methodDecl.Constraints);
        Assert.HasCount(1, methodDecl.Constraints);
        Assert.IsInstanceOfType<StructConstraint>(methodDecl.Constraints[0].Constraints[0]);
    }

    [TestMethod]
    public void Parse_GenericTypeUsage()
    {
        var code = @"
            public class MyClass
            {
                public List<int> Numbers { get; set; }
                public Dictionary<string, int> Map { get; set; }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsNotNull(classDecl.Members);
        Assert.HasCount(2, classDecl.Members);

        var prop1 = (PropertyDeclaration)classDecl.Members[0];
        Assert.IsInstanceOfType<NamedTypeReference>(prop1.Type);
        var type1 = (NamedTypeReference)prop1.Type;
        Assert.IsNotNull(type1.TypeArguments);
        Assert.HasCount(1, type1.TypeArguments);

        var prop2 = (PropertyDeclaration)classDecl.Members[1];
        Assert.IsInstanceOfType<NamedTypeReference>(prop2.Type);
        var type2 = (NamedTypeReference)prop2.Type;
        Assert.IsNotNull(type2.TypeArguments);
        Assert.HasCount(2, type2.TypeArguments);
    }

    [TestMethod]
    public void Parse_GenericInterface()
    {
        var code = @"
            public interface IRepository<T> where T : class
            {
                void Add(T item);
                T Get(int id);
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<InterfaceDeclaration>(result.Members![0]);

        var interfaceDecl = (InterfaceDeclaration)result.Members![0];
        Assert.AreEqual("IRepository", interfaceDecl.Name);
        Assert.IsNotNull(interfaceDecl.TypeParameters);
        Assert.HasCount(1, interfaceDecl.TypeParameters);
        Assert.AreEqual("T", interfaceDecl.TypeParameters[0].Name);
        Assert.IsNotNull(interfaceDecl.Constraints);
        Assert.HasCount(1, interfaceDecl.Constraints);
    }

    [TestMethod]
    public void Parse_GenericStruct()
    {
        var code = @"
            public struct Result<T, E>
            {
                public T Value { get; set; }
                public E Error { get; set; }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<StructDeclaration>(result.Members![0]);

        var structDecl = (StructDeclaration)result.Members![0];
        Assert.AreEqual("Result", structDecl.Name);
        Assert.IsNotNull(structDecl.TypeParameters);
        Assert.HasCount(2, structDecl.TypeParameters);
        Assert.AreEqual("T", structDecl.TypeParameters[0].Name);
        Assert.AreEqual("E", structDecl.TypeParameters[1].Name);
    }

    [TestMethod]
    public void Parse_ClassWithAttributes()
    {
        var code = @"
            [Serializable]
            [Custom(""Test"")]
            public class Person
            {
                public string Name { get; set; }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsInstanceOfType<ClassDeclaration>(result.Members![0]);

        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.AreEqual("Person", classDecl.Name);
        Assert.IsNotNull(classDecl.Attributes);
        Assert.HasCount(2, classDecl.Attributes);
        
        Assert.AreEqual("Serializable", classDecl.Attributes[0].Attributes[0].Name.Parts[0]);
        Assert.AreEqual("Custom", classDecl.Attributes[1].Attributes[0].Name.Parts[0]);
        Assert.IsNotNull(classDecl.Attributes[1].Attributes[0].Arguments);
        Assert.HasCount(1, classDecl.Attributes[1].Attributes[0].Arguments);
    }

    [TestMethod]
    public void Parse_MethodWithAttributes()
    {
        var code = @"
            public class TestClass
            {
                [Obsolete(""Use NewMethod instead"")]
                [Custom(""Description"")]
                public void OldMethod() { }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsNotNull(classDecl.Members);
        
        var method = (MethodDeclaration)classDecl.Members[0];
        Assert.AreEqual("OldMethod", method.Name);
        Assert.IsNotNull(method.Attributes);
        Assert.HasCount(2, method.Attributes);
        
        Assert.AreEqual("Obsolete", method.Attributes[0].Attributes[0].Name.Parts[0]);
        Assert.AreEqual("Custom", method.Attributes[1].Attributes[0].Name.Parts[0]);
    }

    [TestMethod]
    public void Parse_AttributeWithMultipleArguments()
    {
        var code = @"
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
            public class CustomAttribute
            {
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        Assert.IsNotNull(classDecl.Attributes);
        Assert.HasCount(1, classDecl.Attributes);
        
        var attr = classDecl.Attributes[0].Attributes[0];
        Assert.AreEqual("AttributeUsage", attr.Name.Parts[0]);
        Assert.IsNotNull(attr.Arguments);
        Assert.HasCount(2, attr.Arguments);
        
        // First argument is positional
        Assert.IsNull(attr.Arguments[0].Name);
        // Second argument is named
        Assert.AreEqual("AllowMultiple", attr.Arguments[1].Name);
    }

    [TestMethod]
    public void Parse_AttributeWithTarget()
    {
        var code = @"
            public class TestClass
            {
                [return: Custom(""Return value"")]
                public int Calculate(int value)
                {
                    return value * 2;
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var method = (MethodDeclaration)classDecl.Members![0];
        
        Assert.IsNotNull(method.Attributes);
        Assert.HasCount(1, method.Attributes);
        Assert.AreEqual(AttributeTarget.Return, method.Attributes[0].Target);
    }

    [TestMethod]
    public void Parse_PropertyWithAttributes()
    {
        var code = @"
            public class Person
            {
                [Required]
                [MaxLength(100)]
                public string Name { get; set; }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var property = (PropertyDeclaration)classDecl.Members![0];
        
        Assert.AreEqual("Name", property.Name);
        Assert.IsNotNull(property.Attributes);
        Assert.HasCount(2, property.Attributes);
        
        Assert.AreEqual("Required", property.Attributes[0].Attributes[0].Name.Parts[0]);
        Assert.AreEqual("MaxLength", property.Attributes[1].Attributes[0].Name.Parts[0]);
    }

    [TestMethod]
    public void Parse_FieldWithAttributes()
    {
        var code = @"
            public class TestClass
            {
                [NonSerialized]
                private int _value;
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var field = (FieldDeclaration)classDecl.Members![0];
        
        Assert.IsNotNull(field.Attributes);
        Assert.HasCount(1, field.Attributes);
        Assert.AreEqual("NonSerialized", field.Attributes[0].Attributes[0].Name.Parts[0]);
    }

    [TestMethod]
    public void Parse_MultipleAttributesInSection()
    {
        var code = @"
            [Serializable, Custom(""Test""), Obsolete]
            public class TestClass
            {
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        
        Assert.IsNotNull(classDecl.Attributes);
        Assert.HasCount(1, classDecl.Attributes);
        Assert.HasCount(3, classDecl.Attributes[0].Attributes);
        
        Assert.AreEqual("Serializable", classDecl.Attributes[0].Attributes[0].Name.Parts[0]);
        Assert.AreEqual("Custom", classDecl.Attributes[0].Attributes[1].Name.Parts[0]);
        Assert.AreEqual("Obsolete", classDecl.Attributes[0].Attributes[2].Name.Parts[0]);
    }

    [TestMethod]
    public void Parse_GlobalAssemblyAttribute()
    {
        var code = @"
            [assembly: AssemblyVersion(""1.0.0.0"")]
            [assembly: AssemblyTitle(""My Assembly"")]
            
            public class TestClass
            {
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.GlobalAttributes);
        Assert.HasCount(2, result.GlobalAttributes);
        
        Assert.AreEqual(AttributeTarget.Assembly, result.GlobalAttributes[0].Target);
        Assert.AreEqual("AssemblyVersion", result.GlobalAttributes[0].Attributes[0].Name.Parts[0]);
        
        Assert.AreEqual(AttributeTarget.Assembly, result.GlobalAttributes[1].Target);
        Assert.AreEqual("AssemblyTitle", result.GlobalAttributes[1].Attributes[0].Name.Parts[0]);
    }

    [TestMethod]
    public void Parse_EnumWithAttributes()
    {
        var code = @"
            [Flags]
            public enum FileAccess
            {
                [Description(""Read access"")]
                Read = 1,
                
                [Description(""Write access"")]
                Write = 2
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var enumDecl = (EnumDeclaration)result.Members![0];
        
        Assert.AreEqual("FileAccess", enumDecl.Name);
        Assert.IsNotNull(enumDecl.Attributes);
        Assert.HasCount(1, enumDecl.Attributes);
        Assert.AreEqual("Flags", enumDecl.Attributes[0].Attributes[0].Name.Parts[0]);
        
        Assert.IsNotNull(enumDecl.Members);
        Assert.HasCount(2, enumDecl.Members);
        
        var readMember = enumDecl.Members[0];
        Assert.AreEqual("Read", readMember.Name);
        Assert.IsNotNull(readMember.Attributes);
        Assert.AreEqual("Description", readMember.Attributes[0].Attributes[0].Name.Parts[0]);
    }

    [TestMethod]
    public void Parse_ConstructorWithAttributes()
    {
        var code = @"
            public class TestClass
            {
                [Obsolete]
                public TestClass() { }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var constructor = (ConstructorDeclaration)classDecl.Members![0];
        
        Assert.AreEqual("TestClass", constructor.Name);
        Assert.IsNotNull(constructor.Attributes);
        Assert.HasCount(1, constructor.Attributes);
        Assert.AreEqual("Obsolete", constructor.Attributes[0].Attributes[0].Name.Parts[0]);
    }

    [TestMethod]
    public void Parse_SimpleLambdaExpression()
    {
        var code = @"
            public class TestClass
            {
                public int square = x => x * x;
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var field = (FieldDeclaration)classDecl.Members![0];
        var lambda = (LambdaExpression)field.Variables[0].Initializer!;
        
        Assert.IsNotNull(lambda.Parameters);
        Assert.HasCount(1, lambda.Parameters);
        Assert.AreEqual("x", lambda.Parameters[0].Name);
        Assert.IsInstanceOfType<ExpressionLambdaBody>(lambda.Body);
    }

    [TestMethod]
    public void Parse_LambdaWithMultipleParameters()
    {
        var code = @"
            public class TestClass
            {
                public int add = (a, b) => a + b;
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var field = (FieldDeclaration)classDecl.Members![0];
        var lambda = (LambdaExpression)field.Variables[0].Initializer!;
        
        Assert.IsNotNull(lambda.Parameters);
        Assert.HasCount(2, lambda.Parameters);
        Assert.AreEqual("a", lambda.Parameters[0].Name);
        Assert.AreEqual("b", lambda.Parameters[1].Name);
    }

    [TestMethod]
    public void Parse_LambdaWithNoParameters()
    {
        var code = @"
            public class TestClass
            {
                public int greet = () => 42;
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var field = (FieldDeclaration)classDecl.Members![0];
        var lambda = (LambdaExpression)field.Variables[0].Initializer!;
        
        Assert.IsNotNull(lambda.Parameters);
        Assert.HasCount(0, lambda.Parameters);
    }

    [TestMethod]
    public void Parse_LambdaWithBlockBody()
    {
        var code = @"
            public class TestClass
            {
                public int cube = x => { return x * x * x; };
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var field = (FieldDeclaration)classDecl.Members![0];
        var lambda = (LambdaExpression)field.Variables[0].Initializer!;
        
        Assert.IsInstanceOfType<BlockLambdaBody>(lambda.Body);
        var lambdaBlock = ((BlockLambdaBody)lambda.Body).Block;
        Assert.IsNotNull(lambdaBlock.Statements);
    }

    [TestMethod]
    public void Parse_AsyncLambda()
    {
        var code = @"
            public class TestClass
            {
                public int fetch = async () => 42;
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var field = (FieldDeclaration)classDecl.Members![0];
        var lambda = (LambdaExpression)field.Variables[0].Initializer!;
        
        Assert.IsTrue(lambda.IsAsync);
    }

    [TestMethod]
    public void Parse_LambdaWithExplicitTypes()
    {
        var code = @"
            public class TestClass
            {
                public int add = (int a, int b) => a + b;
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var field = (FieldDeclaration)classDecl.Members![0];
        var lambda = (LambdaExpression)field.Variables[0].Initializer!;
        
        Assert.HasCount(2, lambda.Parameters);
        Assert.IsNotNull(lambda.Parameters[0].Type);
        Assert.IsNotNull(lambda.Parameters[1].Type);
    }

    [TestMethod]
    public void Parse_BasicLinqQuery()
    {
        var code = @"
            public class TestClass
            {
                public int evens = from n in numbers where n > 0 select n;
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var field = (FieldDeclaration)classDecl.Members![0];
        var query = (QueryExpression)field.Variables[0].Initializer!;
        
        Assert.IsNotNull(query.FromClause);
        Assert.AreEqual("n", query.FromClause.Identifier);
        Assert.IsNotNull(query.BodyClauses);
        Assert.HasCount(1, query.BodyClauses);
        Assert.IsInstanceOfType<WhereClause>(query.BodyClauses[0]);
        Assert.IsInstanceOfType<SelectClause>(query.SelectOrGroupClause);
    }

    [TestMethod]
    public void Parse_LinqQueryWithLet()
    {
        var code = @"
            public class TestClass
            {
                public int query = from n in numbers let squared = n * n where squared > 10 select squared;
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var field = (FieldDeclaration)classDecl.Members![0];
        var query = (QueryExpression)field.Variables[0].Initializer!;
        
        Assert.HasCount(2, query.BodyClauses);
        Assert.IsInstanceOfType<LetClause>(query.BodyClauses[0]);
        Assert.IsInstanceOfType<WhereClause>(query.BodyClauses[1]);
        
        var letClause = (LetClause)query.BodyClauses[0];
        Assert.AreEqual("squared", letClause.Identifier);
    }

    [TestMethod]
    public void Parse_LinqQueryWithOrderBy()
    {
        var code = @"
            public class TestClass
            {
                public int query = from n in numbers orderby n descending select n;
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var field = (FieldDeclaration)classDecl.Members![0];
        var query = (QueryExpression)field.Variables[0].Initializer!;
        
        Assert.HasCount(1, query.BodyClauses);
        Assert.IsInstanceOfType<OrderByClause>(query.BodyClauses[0]);
        
        var orderBy = (OrderByClause)query.BodyClauses[0];
        Assert.HasCount(1, orderBy.Orderings);
        Assert.AreEqual(OrderDirection.Descending, orderBy.Orderings[0].Direction);
    }

    [TestMethod]
    public void Parse_LinqQueryWithGroupBy()
    {
        var code = @"
            public class TestClass
            {
                public int groups = from n in numbers group n by n;
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var field = (FieldDeclaration)classDecl.Members![0];
        var query = (QueryExpression)field.Variables[0].Initializer!;
        
        Assert.IsInstanceOfType<GroupClause>(query.SelectOrGroupClause);
        
        var groupClause = (GroupClause)query.SelectOrGroupClause;
        Assert.IsNotNull(groupClause.GroupExpression);
        Assert.IsNotNull(groupClause.ByExpression);
    }

    [TestMethod]
    public void Parse_LinqQueryWithJoin()
    {
        var code = @"
            public class TestClass
            {
                public int query = from p in people join o in orders on p equals o select p;
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var field = (FieldDeclaration)classDecl.Members![0];
        var query = (QueryExpression)field.Variables[0].Initializer!;
        
        Assert.HasCount(1, query.BodyClauses);
        Assert.IsInstanceOfType<JoinClause>(query.BodyClauses[0]);
        
        var joinClause = (JoinClause)query.BodyClauses[0];
        Assert.AreEqual("o", joinClause.Identifier);
    }

    [TestMethod]
    public void Parse_LinqQueryWithMultipleClauses()
    {
        var code = @"
            public class TestClass
            {
                public void Method()
                {
                    var result = from n in numbers where n > 5 orderby n descending select n * 2;
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var method = (MethodDeclaration)classDecl.Members![0];
        var block = ((BlockMethodBody)method.Body).Block;
        var varDecl = (LocalDeclarationStatement)block.Statements![0];
        var query = (QueryExpression)varDecl.Variables[0].Initializer!;
        
        Assert.HasCount(2, query.BodyClauses);
        Assert.IsInstanceOfType<WhereClause>(query.BodyClauses[0]);
        Assert.IsInstanceOfType<OrderByClause>(query.BodyClauses[1]);
    }

    // ========================================
    // Pattern Matching Tests
    // ========================================

    [TestMethod]
    public void Parse_IsExpressionWithTypePattern()
    {
        var code = @"
            public class TestClass
            {
                public void Test(object obj)
                {
                    if (obj is string)
                    {
                        return;
                    }
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var method = (MethodDeclaration)classDecl.Members![0];
        var ifStmt = (IfStatement)((BlockMethodBody)method.Body!).Block.Statements![0];
        var isExpr = (IsExpression)ifStmt.Condition;
        
        Assert.IsNotNull(isExpr.Pattern);
        Assert.IsInstanceOfType<TypePattern>(isExpr.Pattern);
    }

    [TestMethod]
    public void Parse_IsExpressionWithDeclarationPattern()
    {
        var code = @"
            public class TestClass
            {
                public void Test(object obj)
                {
                    if (obj is string s)
                    {
                        return;
                    }
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var method = (MethodDeclaration)classDecl.Members![0];
        var ifStmt = (IfStatement)((BlockMethodBody)method.Body!).Block.Statements![0];
        var isExpr = (IsExpression)ifStmt.Condition;
        
        Assert.IsInstanceOfType<DeclarationPattern>(isExpr.Pattern);
        var declPattern = (DeclarationPattern)isExpr.Pattern;
        Assert.AreEqual("s", declPattern.Identifier);
    }

    [TestMethod]
    public void Parse_IsExpressionWithConstantPattern()
    {
        var code = @"
            public class TestClass
            {
                public void Test(int x)
                {
                    if (x is 42)
                    {
                        return;
                    }
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var method = (MethodDeclaration)classDecl.Members![0];
        var ifStmt = (IfStatement)((BlockMethodBody)method.Body!).Block.Statements![0];
        var isExpr = (IsExpression)ifStmt.Condition;
        
        Assert.IsInstanceOfType<ConstantPattern>(isExpr.Pattern);
    }

    [TestMethod]
    public void Parse_IsExpressionWithVarPattern()
    {
        var code = @"
            public class TestClass
            {
                public void Test(object obj)
                {
                    if (obj is var x)
                    {
                        return;
                    }
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var method = (MethodDeclaration)classDecl.Members![0];
        var ifStmt = (IfStatement)((BlockMethodBody)method.Body!).Block.Statements![0];
        var isExpr = (IsExpression)ifStmt.Condition;
        
        Assert.IsInstanceOfType<VarPattern>(isExpr.Pattern);
        var varPattern = (VarPattern)isExpr.Pattern;
        Assert.AreEqual("x", varPattern.Identifier);
    }

    [TestMethod]
    public void Parse_IsExpressionWithDiscardPattern()
    {
        var code = @"
            public class TestClass
            {
                public void Test(object obj)
                {
                    if (obj is _)
                    {
                        return;
                    }
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var method = (MethodDeclaration)classDecl.Members![0];
        var ifStmt = (IfStatement)((BlockMethodBody)method.Body!).Block.Statements![0];
        var isExpr = (IsExpression)ifStmt.Condition;
        
        Assert.IsInstanceOfType<DiscardPattern>(isExpr.Pattern);
    }

    [TestMethod]
    public void Parse_SwitchExpression()
    {
        var code = @"
            public class TestClass
            {
                public int result = x switch
                {
                    1 => 10,
                    2 => 20,
                    _ => 0
                };
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var field = (FieldDeclaration)classDecl.Members![0];
        var switchExpr = (SwitchExpression)field.Variables[0].Initializer!;
        
        Assert.IsNotNull(switchExpr.Arms);
        Assert.HasCount(3, switchExpr.Arms);
    }

    [TestMethod]
    public void Parse_SwitchExpressionWithGuard()
    {
        var code = @"
            public class TestClass
            {
                public int result = x switch
                {
                    int n when n > 0 => 1,
                    _ => 0
                };
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var field = (FieldDeclaration)classDecl.Members![0];
        var switchExpr = (SwitchExpression)field.Variables[0].Initializer!;
        
        Assert.IsNotNull(switchExpr.Arms);
        Assert.HasCount(2, switchExpr.Arms);
        Assert.IsNotNull(switchExpr.Arms[0].Guard);
    }

    [TestMethod]
    public void Parse_RecursivePattern()
    {
        var code = @"
            public class TestClass
            {
                public void Test(object obj)
                {
                    if (obj is Person { Name: ""John"", Age: 30 })
                    {
                        return;
                    }
                }
            }
        ";

        var result = CSharpParser.Parse(code);

        Assert.IsNotNull(result);
        var classDecl = (ClassDeclaration)result.Members![0];
        var method = (MethodDeclaration)classDecl.Members![0];
        var ifStmt = (IfStatement)((BlockMethodBody)method.Body!).Block.Statements![0];
        var isExpr = (IsExpression)ifStmt.Condition;
        
        Assert.IsInstanceOfType<RecursivePattern>(isExpr.Pattern);
        var recursivePattern = (RecursivePattern)isExpr.Pattern;
        Assert.IsNotNull(recursivePattern.PropertyPatterns);
        Assert.HasCount(2, recursivePattern.PropertyPatterns);
    }
}


