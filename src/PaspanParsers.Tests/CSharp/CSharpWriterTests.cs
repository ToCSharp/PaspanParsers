using PaspanParsers.CSharp;

namespace PaspanParsers.Tests.CSharp;

[TestClass]
public class CSharpWriterTests
{
    private static string WriteNode(Action<CSharpWriter> writeAction)
    {
        var writer = new CSharpWriter();
        writeAction(writer);
        return writer.GetResult();
    }

    [TestMethod]
    public void WriteSimpleClass()
    {
        var classDecl = new ClassDeclaration(
            name: "Person",
            modifiers: Modifiers.Public
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            members: [classDecl]
        )));

        Assert.Contains("public class Person", result);
    }

    [TestMethod]
    public void WriteClassWithProperty()
    {
        var property = new PropertyDeclaration(
            type: new PredefinedTypeReference(PredefinedType.String),
            name: "Name",
            modifiers: Modifiers.Public,
            accessors:
            [
                new Accessor(AccessorKind.Get),
                new Accessor(AccessorKind.Set)
            ]
        );

        var classDecl = new ClassDeclaration(
            name: "Person",
            modifiers: Modifiers.Public,
            members: [property]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            members: [classDecl]
        )));

        Assert.Contains("public class Person", result);
        Assert.Contains("public string Name", result);
        Assert.Contains("get;", result);
        Assert.Contains("set;", result);
    }

    [TestMethod]
    public void WriteMethodWithBody()
    {
        var returnStmt = new ReturnStatement(
            new BinaryExpression(
                new NameExpression(["a"]),
                BinaryOperator.Add,
                new NameExpression(["b"])
            )
        );

        var method = new MethodDeclaration(
            returnType: new PredefinedTypeReference(PredefinedType.Int),
            name: "Add",
            modifiers: Modifiers.Public,
            parameters:
            [
                new Parameter(new PredefinedTypeReference(PredefinedType.Int), "a"),
                new Parameter(new PredefinedTypeReference(PredefinedType.Int), "b")
            ],
            body: new BlockMethodBody(new BlockStatement([returnStmt]))
        );

        var classDecl = new ClassDeclaration(
            name: "Calculator",
            modifiers: Modifiers.Public,
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            members: [classDecl]
        )));

        Assert.Contains("public int Add(int a, int b)", result);
        Assert.Contains("return a + b;", result);
    }

    [TestMethod]
    public void WriteUsingDirectives()
    {
        var usingDirective = new UsingNamespaceDirective(
            new NameExpression(["System"])
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            usings: [usingDirective]
        )));

        Assert.Contains("using System;", result);
    }

    [TestMethod]
    public void WriteNamespace()
    {
        var classDecl = new ClassDeclaration(
            name: "Test",
            modifiers: Modifiers.Public
        );

        var ns = new NamespaceDeclaration(
            name: new NameExpression(["MyNamespace"]),
            members: [classDecl]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            members: [ns]
        )));

        Assert.Contains("namespace MyNamespace", result);
        Assert.Contains("public class Test", result);
    }

    [TestMethod]
    public void WriteFileScopedNamespace()
    {
        var classDecl = new ClassDeclaration(
            name: "Test",
            modifiers: Modifiers.Public
        );

        var ns = new NamespaceDeclaration(
            name: new NameExpression(["MyNamespace"]),
            members: [classDecl],
            isFileScopedNamespace: true
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            members: [ns]
        )));

        Assert.Contains("namespace MyNamespace;", result);
        Assert.Contains("public class Test", result);
    }

    [TestMethod]
    public void WriteEnum()
    {
        var enumDecl = new EnumDeclaration(
            name: "Color",
            modifiers: Modifiers.Public,
            members:
            [
                new EnumMember("Red"),
                new EnumMember("Green"),
                new EnumMember("Blue")
            ]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            members: [enumDecl]
        )));

        Assert.Contains("public enum Color", result);
        Assert.Contains("Red", result);
        Assert.Contains("Green", result);
        Assert.Contains("Blue", result);
    }

    [TestMethod]
    public void WriteRecord()
    {
        var recordDecl = new RecordDeclaration(
            name: "Person",
            modifiers: Modifiers.Public,
            primaryConstructorParameters:
            [
                new Parameter(new PredefinedTypeReference(PredefinedType.String), "FirstName"),
                new Parameter(new PredefinedTypeReference(PredefinedType.String), "LastName")
            ]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            members: [recordDecl]
        )));

        Assert.Contains("public record Person(string FirstName, string LastName);", result);
    }

    [TestMethod]
    public void WriteInterface()
    {
        var method = new MethodDeclaration(
            returnType: new PredefinedTypeReference(PredefinedType.Void),
            name: "DoSomething"
        );

        var interfaceDecl = new InterfaceDeclaration(
            name: "IService",
            modifiers: Modifiers.Public,
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            members: [interfaceDecl]
        )));

        Assert.Contains("public interface IService", result);
        Assert.Contains("void DoSomething();", result);
    }

    [TestMethod]
    public void WriteIfStatement()
    {
        var ifStmt = new IfStatement(
            condition: new BinaryExpression(
                new NameExpression(["x"]),
                BinaryOperator.GreaterThan,
                new LiteralExpression(0, LiteralKind.Integer)
            ),
            thenStatement: new ReturnStatement(new LiteralExpression(true, LiteralKind.Boolean)),
            elseStatement: new ReturnStatement(new LiteralExpression(false, LiteralKind.Boolean))
        );

        var method = new MethodDeclaration(
            returnType: new PredefinedTypeReference(PredefinedType.Bool),
            name: "IsPositive",
            modifiers: Modifiers.Public,
            parameters: [new Parameter(new PredefinedTypeReference(PredefinedType.Int), "x")],
            body: new BlockMethodBody(new BlockStatement([ifStmt]))
        );

        var classDecl = new ClassDeclaration(
            name: "Test",
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            members: [classDecl]
        )));

        Assert.Contains("if (x > 0)", result);
        Assert.Contains("return true;", result);
        Assert.Contains("else", result);
        Assert.Contains("return false;", result);
    }

    [TestMethod]
    public void WriteLambdaExpression()
    {
        var lambda = new LambdaExpression(
            body: new ExpressionLambdaBody(
                new BinaryExpression(
                    new NameExpression(["x"]),
                    BinaryOperator.Multiply,
                    new LiteralExpression(2, LiteralKind.Integer)
                )
            ),
            parameters: [new Parameter(null, "x")]
        );

        var field = new FieldDeclaration(
            type: new NamedTypeReference(new NameExpression(["Func"])),
            variables: [new VariableDeclarator("Double", lambda)],
            modifiers: Modifiers.Private
        );

        var classDecl = new ClassDeclaration(
            name: "Test",
            members: [field]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            members: [classDecl]
        )));

        Assert.Contains("x => x * 2", result);
    }

    [TestMethod]
    public void WriteAttribute()
    {
        var attribute = new AttributeSection(
            [
                new AttributeNode(new NameExpression(["Serializable"]))
            ]
        );

        var classDecl = new ClassDeclaration(
            name: "Data",
            attributes: [attribute],
            modifiers: Modifiers.Public
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            members: [classDecl]
        )));

        Assert.Contains("[Serializable]", result);
        Assert.Contains("public class Data", result);
    }

    [TestMethod]
    public void WriteConstructor()
    {
        var assignment = new ExpressionStatement(
            new BinaryExpression(
                new MemberAccessExpression("Name", new NameExpression(["this"])),
                BinaryOperator.Assign,
                new NameExpression(["name"])
            )
        );

        var ctor = new ConstructorDeclaration(
            name: "Person",
            modifiers: Modifiers.Public,
            parameters: [new Parameter(new PredefinedTypeReference(PredefinedType.String), "name")],
            body: new BlockMethodBody(new BlockStatement([assignment]))
        );

        var classDecl = new ClassDeclaration(
            name: "Person",
            modifiers: Modifiers.Public,
            members: [ctor]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            members: [classDecl]
        )));

        Assert.Contains("public Person(string name)", result);
        Assert.Contains("this.Name = name;", result);
    }

    [TestMethod]
    public void WriteGenericClass()
    {
        var classDecl = new ClassDeclaration(
            name: "Container",
            modifiers: Modifiers.Public,
            typeParameters: [new TypeParameter("T")]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            members: [classDecl]
        )));

        Assert.Contains("public class Container<T>", result);
    }

    [TestMethod]
    public void WriteSwitchExpression()
    {
        var switchExpr = new SwitchExpression(
            governingExpression: new NameExpression(["x"]),
            arms:
            [
                new SwitchExpressionArm(
                    new ConstantPattern(new LiteralExpression(1, LiteralKind.Integer)),
                    new LiteralExpression("one", LiteralKind.String)
                ),
                new SwitchExpressionArm(
                    new ConstantPattern(new LiteralExpression(2, LiteralKind.Integer)),
                    new LiteralExpression("two", LiteralKind.String)
                ),
                new SwitchExpressionArm(
                    new DiscardPattern(),
                    new LiteralExpression("other", LiteralKind.String)
                )
            ]
        );

        var returnStmt = new ReturnStatement(switchExpr);

        var method = new MethodDeclaration(
            returnType: new PredefinedTypeReference(PredefinedType.String),
            name: "GetName",
            modifiers: Modifiers.Public,
            parameters: [new Parameter(new PredefinedTypeReference(PredefinedType.Int), "x")],
            body: new BlockMethodBody(new BlockStatement([returnStmt]))
        );

        var classDecl = new ClassDeclaration(
            name: "Test",
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            members: [classDecl]
        )));

        Assert.Contains("x switch", result);
        Assert.Contains("1 => \"one\"", result);
        Assert.Contains("2 => \"two\"", result);
        Assert.Contains("_ => \"other\"", result);
    }

    [TestMethod]
    public void WriteNullableType()
    {
        var field = new FieldDeclaration(
            type: new PredefinedTypeReference(PredefinedType.Int, isNullable: true),
            variables: [new VariableDeclarator("Age")],
            modifiers: Modifiers.Private
        );

        var classDecl = new ClassDeclaration(
            name: "Person",
            members: [field]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            members: [classDecl]
        )));

        Assert.Contains("private int? Age;", result);
    }

    [TestMethod]
    public void WriteArrayType()
    {
        var field = new FieldDeclaration(
            type: new ArrayTypeReference(new PredefinedTypeReference(PredefinedType.String)),
            variables: [new VariableDeclarator("names")],
            modifiers: Modifiers.Private
        );

        var classDecl = new ClassDeclaration(
            name: "Test",
            members: [field]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            members: [classDecl]
        )));

        Assert.Contains("private string[] names;", result);
    }
}

