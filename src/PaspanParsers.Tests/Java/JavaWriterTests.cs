using PaspanParsers.Java;

namespace PaspanParsers.Tests.Java;

[TestClass]
public class JavaWriterTests
{
    private static string WriteNode(Action<JavaWriter> writeAction)
    {
        var writer = new JavaWriter();
        writeAction(writer);
        return writer.GetResult();
    }

    // ========================================
    // Basic Type Declarations
    // ========================================

    [TestMethod]
    public void WriteSimpleClass()
    {
        var classDecl = new ClassDeclaration(
            name: "Person",
            modifiers: Modifiers.Public
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("public class Person", result);
        Assert.Contains("{", result);
        Assert.Contains("}", result);
    }

    [TestMethod]
    public void WriteSimpleInterface()
    {
        var interfaceDecl = new InterfaceDeclaration(
            name: "Comparable",
            modifiers: Modifiers.Public
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [interfaceDecl]
        )));

        Assert.Contains("public interface Comparable", result);
    }

    [TestMethod]
    public void WriteSimpleEnum()
    {
        var enumDecl = new EnumDeclaration(
            name: "Color",
            modifiers: Modifiers.Public,
            constants:
            [
                new EnumConstant("RED"),
                new EnumConstant("GREEN"),
                new EnumConstant("BLUE")
            ]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [enumDecl]
        )));

        Assert.Contains("public enum Color", result);
        Assert.Contains("RED", result);
        Assert.Contains("GREEN", result);
        Assert.Contains("BLUE", result);
    }

    [TestMethod]
    public void WriteRecord()
    {
        var recordDecl = new RecordDeclaration(
            name: "Point",
            modifiers: Modifiers.Public,
            components:
            [
                new RecordComponent(
                    type: new PrimitiveTypeReference(PrimitiveType.Int),
                    name: "x"
                ),
                new RecordComponent(
                    type: new PrimitiveTypeReference(PrimitiveType.Int),
                    name: "y"
                )
            ]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [recordDecl]
        )));

        Assert.Contains("public record Point", result);
        Assert.Contains("int x", result);
        Assert.Contains("int y", result);
    }

    [TestMethod]
    public void WriteAnnotationDeclaration()
    {
        var annotationDecl = new AnnotationDeclaration(
            name: "Author",
            modifiers: Modifiers.Public
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [annotationDecl]
        )));

        Assert.Contains("public @interface Author", result);
    }

    // ========================================
    // Package and Imports
    // ========================================

    [TestMethod]
    public void WritePackageDeclaration()
    {
        var pkg = new PackageDeclaration(
            name: new QualifiedName(["com", "example"])
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            packageDeclaration: pkg
        )));

        Assert.Contains("package com.example;", result);
    }

    [TestMethod]
    public void WriteSingleTypeImport()
    {
        var import = new SingleTypeImportDeclaration(
            typeName: new QualifiedName(["java", "util", "List"])
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            importDeclarations: [import]
        )));

        Assert.Contains("import java.util.List;", result);
    }

    [TestMethod]
    public void WriteOnDemandImport()
    {
        var import = new TypeImportOnDemandDeclaration(
            packageOrTypeName: new QualifiedName(["java", "util"])
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            importDeclarations: [import]
        )));

        Assert.Contains("import java.util.*;", result);
    }

    [TestMethod]
    public void WriteStaticImport()
    {
        var import = new SingleTypeImportDeclaration(
            typeName: new QualifiedName(["java", "lang", "Math", "PI"]),
            isStatic: true
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            importDeclarations: [import]
        )));

        Assert.Contains("import static java.lang.Math.PI;", result);
    }

    // ========================================
    // Fields
    // ========================================

    [TestMethod]
    public void WriteSimpleField()
    {
        var field = new FieldDeclaration(
            type: new PrimitiveTypeReference(PrimitiveType.Int),
            modifiers: Modifiers.Private,
            variables:
            [
                new VariableDeclarator("count")
            ]
        );

        var classDecl = new ClassDeclaration(
            name: "Counter",
            modifiers: Modifiers.Public,
            members: [field]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("private int count;", result);
    }

    [TestMethod]
    public void WriteFieldWithInitializer()
    {
        var field = new FieldDeclaration(
            type: new PrimitiveTypeReference(PrimitiveType.Int),
            modifiers: Modifiers.Private,
            variables:
            [
                new VariableDeclarator(
                    name: "count",
                    initializer: new LiteralExpression(0, LiteralKind.Integer)
                )
            ]
        );

        var classDecl = new ClassDeclaration(
            name: "Counter",
            modifiers: Modifiers.Public,
            members: [field]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("private int count = 0;", result);
    }

    [TestMethod]
    public void WriteStaticFinalField()
    {
        var field = new FieldDeclaration(
            type: new PrimitiveTypeReference(PrimitiveType.Int),
            modifiers: Modifiers.Public | Modifiers.Static | Modifiers.Final,
            variables:
            [
                new VariableDeclarator(
                    name: "MAX_SIZE",
                    initializer: new LiteralExpression(100, LiteralKind.Integer)
                )
            ]
        );

        var classDecl = new ClassDeclaration(
            name: "Constants",
            modifiers: Modifiers.Public,
            members: [field]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("public static final int MAX_SIZE = 100;", result);
    }

    // ========================================
    // Methods
    // ========================================

    [TestMethod]
    public void WriteSimpleMethod()
    {
        var method = new MethodDeclaration(
            returnType: new PrimitiveTypeReference(PrimitiveType.Int),
            name: "getValue",
            modifiers: Modifiers.Public
        );

        var classDecl = new ClassDeclaration(
            name: "Test",
            modifiers: Modifiers.Public,
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("public int getValue();", result);
    }

    [TestMethod]
    public void WriteMethodWithParameters()
    {
        var method = new MethodDeclaration(
            returnType: new PrimitiveTypeReference(PrimitiveType.Int),
            name: "add",
            modifiers: Modifiers.Public,
            parameters:
            [
                new Parameter(
                    type: new PrimitiveTypeReference(PrimitiveType.Int),
                    name: "a"
                ),
                new Parameter(
                    type: new PrimitiveTypeReference(PrimitiveType.Int),
                    name: "b"
                )
            ]
        );

        var classDecl = new ClassDeclaration(
            name: "Calculator",
            modifiers: Modifiers.Public,
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("public int add(int a, int b);", result);
    }

    [TestMethod]
    public void WriteMethodWithBody()
    {
        var returnStmt = new ReturnStatement(
            new BinaryExpression(
                left: new NameExpression(new QualifiedName(["a"])),
                op: BinaryOperator.Add,
                right: new NameExpression(new QualifiedName(["b"]))
            )
        );

        var method = new MethodDeclaration(
            returnType: new PrimitiveTypeReference(PrimitiveType.Int),
            name: "add",
            modifiers: Modifiers.Public,
            parameters:
            [
                new Parameter(
                    type: new PrimitiveTypeReference(PrimitiveType.Int),
                    name: "a"
                ),
                new Parameter(
                    type: new PrimitiveTypeReference(PrimitiveType.Int),
                    name: "b"
                )
            ],
            body: new BlockStatement([returnStmt])
        );

        var classDecl = new ClassDeclaration(
            name: "Calculator",
            modifiers: Modifiers.Public,
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("public int add(int a, int b) {", result);
        Assert.Contains("return a + b;", result);
    }

    [TestMethod]
    public void WriteAbstractMethod()
    {
        var method = new MethodDeclaration(
            returnType: new PrimitiveTypeReference(PrimitiveType.Int),
            name: "calculate",
            modifiers: Modifiers.Public | Modifiers.Abstract
        );

        var classDecl = new ClassDeclaration(
            name: "Calculator",
            modifiers: Modifiers.Public | Modifiers.Abstract,
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("public abstract int calculate();", result);
    }

    // ========================================
    // Constructors
    // ========================================

    [TestMethod]
    public void WriteSimpleConstructor()
    {
        var ctor = new ConstructorDeclaration(
            name: "Person",
            modifiers: Modifiers.Public
        );

        var classDecl = new ClassDeclaration(
            name: "Person",
            modifiers: Modifiers.Public,
            members: [ctor]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("public Person();", result);
    }

    [TestMethod]
    public void WriteConstructorWithParameters()
    {
        var ctor = new ConstructorDeclaration(
            name: "Person",
            modifiers: Modifiers.Public,
            parameters:
            [
                new Parameter(
                    type: new ReferenceTypeReference(new QualifiedName(["String"])),
                    name: "name"
                ),
                new Parameter(
                    type: new PrimitiveTypeReference(PrimitiveType.Int),
                    name: "age"
                )
            ]
        );

        var classDecl = new ClassDeclaration(
            name: "Person",
            modifiers: Modifiers.Public,
            members: [ctor]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("public Person(String name, int age);", result);
    }

    // ========================================
    // Generics
    // ========================================

    [TestMethod]
    public void WriteGenericClass()
    {
        var classDecl = new ClassDeclaration(
            name: "Box",
            modifiers: Modifiers.Public,
            typeParameters:
            [
                new TypeParameter("T")
            ]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("public class Box<T>", result);
    }

    [TestMethod]
    public void WriteGenericClassWithBounds()
    {
        var classDecl = new ClassDeclaration(
            name: "Box",
            modifiers: Modifiers.Public,
            typeParameters:
            [
                new TypeParameter(
                    name: "T",
                    bounds:
                    [
                        new ReferenceTypeReference(new QualifiedName(["Number"]))
                    ]
                )
            ]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("public class Box<T extends Number>", result);
    }

    [TestMethod]
    public void WriteGenericMethod()
    {
        var method = new MethodDeclaration(
            returnType: new ReferenceTypeReference(new QualifiedName(["T"])),
            name: "identity",
            modifiers: Modifiers.Public | Modifiers.Static,
            typeParameters:
            [
                new TypeParameter("T")
            ],
            parameters:
            [
                new Parameter(
                    type: new ReferenceTypeReference(new QualifiedName(["T"])),
                    name: "value"
                )
            ]
        );

        var classDecl = new ClassDeclaration(
            name: "Utils",
            modifiers: Modifiers.Public,
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("public static <T> T identity(T value);", result);
    }

    // ========================================
    // Inheritance
    // ========================================

    [TestMethod]
    public void WriteClassWithSuperClass()
    {
        var classDecl = new ClassDeclaration(
            name: "Dog",
            modifiers: Modifiers.Public,
            superClass: new ReferenceTypeReference(new QualifiedName(["Animal"]))
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("public class Dog extends Animal", result);
    }

    [TestMethod]
    public void WriteClassWithInterfaces()
    {
        var classDecl = new ClassDeclaration(
            name: "ArrayList",
            modifiers: Modifiers.Public,
            superInterfaces:
            [
                new ReferenceTypeReference(new QualifiedName(["List"])),
                new ReferenceTypeReference(new QualifiedName(["Cloneable"]))
            ]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("public class ArrayList implements List, Cloneable", result);
    }

    [TestMethod]
    public void WriteInterfaceExtends()
    {
        var interfaceDecl = new InterfaceDeclaration(
            name: "List",
            modifiers: Modifiers.Public,
            extendsInterfaces:
            [
                new ReferenceTypeReference(new QualifiedName(["Collection"]))
            ]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [interfaceDecl]
        )));

        Assert.Contains("public interface List extends Collection", result);
    }

    // ========================================
    // Sealed Classes (Java 17+)
    // ========================================

    [TestMethod]
    public void WriteSealedClass()
    {
        var classDecl = new ClassDeclaration(
            name: "Shape",
            modifiers: Modifiers.Public | Modifiers.Sealed,
            permittedSubclasses:
            [
                new ReferenceTypeReference(new QualifiedName(["Circle"])),
                new ReferenceTypeReference(new QualifiedName(["Rectangle"]))
            ]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("public sealed class Shape permits Circle, Rectangle", result);
    }

    // ========================================
    // Annotations
    // ========================================

    [TestMethod]
    public void WriteMarkerAnnotation()
    {
        var annotation = new Annotation(
            name: new QualifiedName(["Override"])
        );

        var method = new MethodDeclaration(
            returnType: new ReferenceTypeReference(new QualifiedName(["String"])),
            name: "toString",
            modifiers: Modifiers.Public,
            annotations: [annotation]
        );

        var classDecl = new ClassDeclaration(
            name: "Test",
            modifiers: Modifiers.Public,
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("@Override", result);
        Assert.Contains("public String toString();", result);
    }

    [TestMethod]
    public void WriteSingleElementAnnotation()
    {
        var annotation = new Annotation(
            name: new QualifiedName(["SuppressWarnings"]),
            elements:
            [
                new AnnotationElement(
                    value: new LiteralExpression("unchecked", LiteralKind.String),
                    name: null
                )
            ]
        );

        var method = new MethodDeclaration(
            returnType: new PrimitiveTypeReference(PrimitiveType.Int),
            name: "test",
            modifiers: Modifiers.Public,
            annotations: [annotation]
        );

        var classDecl = new ClassDeclaration(
            name: "Test",
            modifiers: Modifiers.Public,
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("@SuppressWarnings(\"unchecked\")", result);
    }

    [TestMethod]
    public void WriteNormalAnnotation()
    {
        var annotation = new Annotation(
            name: new QualifiedName(["RequestMapping"]),
            elements:
            [
                new AnnotationElement(
                    value: new LiteralExpression("/users", LiteralKind.String),
                    name: "path"
                ),
                new AnnotationElement(
                    value: new NameExpression(new QualifiedName(["GET"])),
                    name: "method"
                )
            ]
        );

        var method = new MethodDeclaration(
            returnType: new ReferenceTypeReference(new QualifiedName(["String"])),
            name: "getUsers",
            modifiers: Modifiers.Public,
            annotations: [annotation]
        );

        var classDecl = new ClassDeclaration(
            name: "Controller",
            modifiers: Modifiers.Public,
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("@RequestMapping(path = \"/users\", method = GET)", result);
    }

    // ========================================
    // Statements
    // ========================================

    [TestMethod]
    public void WriteIfStatement()
    {
        var ifStmt = new IfStatement(
            condition: new BinaryExpression(
                left: new NameExpression(new QualifiedName(["x"])),
                op: BinaryOperator.GreaterThan,
                right: new LiteralExpression(0, LiteralKind.Integer)
            ),
            thenStatement: new ReturnStatement(
                new LiteralExpression(true, LiteralKind.Boolean)
            )
        );

        var method = new MethodDeclaration(
            returnType: new PrimitiveTypeReference(PrimitiveType.Boolean),
            name: "isPositive",
            modifiers: Modifiers.Public,
            parameters:
            [
                new Parameter(
                    type: new PrimitiveTypeReference(PrimitiveType.Int),
                    name: "x"
                )
            ],
            body: new BlockStatement([ifStmt])
        );

        var classDecl = new ClassDeclaration(
            name: "Test",
            modifiers: Modifiers.Public,
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("if (x > 0)", result);
        Assert.Contains("return true;", result);
    }

    [TestMethod]
    public void WriteWhileStatement()
    {
        var whileStmt = new WhileStatement(
            condition: new BinaryExpression(
                left: new NameExpression(new QualifiedName(["i"])),
                op: BinaryOperator.LessThan,
                right: new LiteralExpression(10, LiteralKind.Integer)
            ),
            body: new ExpressionStatement(
                new UnaryExpression(
                    op: UnaryOperator.PreIncrement,
                    operand: new NameExpression(new QualifiedName(["i"])),
                    isPrefix: true
                )
            )
        );

        var method = new MethodDeclaration(
            returnType: new PrimitiveTypeReference(PrimitiveType.Int),
            name: "test",
            modifiers: Modifiers.Public,
            body: new BlockStatement([whileStmt])
        );

        var classDecl = new ClassDeclaration(
            name: "Test",
            modifiers: Modifiers.Public,
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("while (i < 10)", result);
        Assert.Contains("++i;", result);
    }

    [TestMethod]
    public void WriteForStatement()
    {
        var forStmt = new ForStatement(
            initializers:
            [
                new LocalVariableDeclarationStatement(
                    type: new PrimitiveTypeReference(PrimitiveType.Int),
                    variables:
                    [
                    new VariableDeclarator(
                        name: "i",
                        initializer: new LiteralExpression(0, LiteralKind.Integer)
                    )
                    ]
                )
            ],
            condition: new BinaryExpression(
                left: new NameExpression(new QualifiedName(["i"])),
                op: BinaryOperator.LessThan,
                right: new LiteralExpression(10, LiteralKind.Integer)
            ),
            updates:
            [
                new UnaryExpression(
                    op: UnaryOperator.PreIncrement,
                    operand: new NameExpression(new QualifiedName(["i"])),
                    isPrefix: true
                )
            ],
            body: new EmptyStatement()
        );

        var method = new MethodDeclaration(
            returnType: new PrimitiveTypeReference(PrimitiveType.Int),
            name: "test",
            modifiers: Modifiers.Public,
            body: new BlockStatement([forStmt])
        );

        var classDecl = new ClassDeclaration(
            name: "Test",
            modifiers: Modifiers.Public,
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("for (int i = 0; i < 10; ++i)", result);
    }

    // ========================================
    // Expressions
    // ========================================

    [TestMethod]
    public void WriteBinaryExpression()
    {
        var expr = new BinaryExpression(
            left: new LiteralExpression(1, LiteralKind.Integer),
            op: BinaryOperator.Add,
            right: new LiteralExpression(2, LiteralKind.Integer)
        );

        var stmt = new ReturnStatement(expr);

        var method = new MethodDeclaration(
            returnType: new PrimitiveTypeReference(PrimitiveType.Int),
            name: "test",
            modifiers: Modifiers.Public,
            body: new BlockStatement([stmt])
        );

        var classDecl = new ClassDeclaration(
            name: "Test",
            modifiers: Modifiers.Public,
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("return 1 + 2;", result);
    }

    [TestMethod]
    public void WriteTernaryExpression()
    {
        var expr = new TernaryExpression(
            condition: new BinaryExpression(
                left: new NameExpression(new QualifiedName(["x"])),
                op: BinaryOperator.GreaterThan,
                right: new LiteralExpression(0, LiteralKind.Integer)
            ),
            trueExpr: new LiteralExpression(1, LiteralKind.Integer),
            falseExpr: new LiteralExpression(-1, LiteralKind.Integer)
        );

        var stmt = new ReturnStatement(expr);

        var method = new MethodDeclaration(
            returnType: new PrimitiveTypeReference(PrimitiveType.Int),
            name: "sign",
            modifiers: Modifiers.Public,
            parameters:
            [
                new Parameter(
                    type: new PrimitiveTypeReference(PrimitiveType.Int),
                    name: "x"
                )
            ],
            body: new BlockStatement([stmt])
        );

        var classDecl = new ClassDeclaration(
            name: "Test",
            modifiers: Modifiers.Public,
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("return x > 0 ? 1 : -1;", result);
    }

    [TestMethod]
    public void WriteMethodInvocation()
    {
        var expr = new MethodInvocationExpression(
            target: new NameExpression(new QualifiedName(["obj"])),
            methodName: "doSomething",
            arguments:
            [
                new LiteralExpression(42, LiteralKind.Integer)
            ]
        );

        var stmt = new ExpressionStatement(expr);

        var method = new MethodDeclaration(
            returnType: new PrimitiveTypeReference(PrimitiveType.Int),
            name: "test",
            modifiers: Modifiers.Public,
            body: new BlockStatement([stmt])
        );

        var classDecl = new ClassDeclaration(
            name: "Test",
            modifiers: Modifiers.Public,
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("obj.doSomething(42);", result);
    }

    [TestMethod]
    public void WriteNewObjectExpression()
    {
        var expr = new NewObjectExpression(
            type: new ReferenceTypeReference(new QualifiedName(["ArrayList"]))
        );

        var localVar = new LocalVariableDeclarationStatement(
            type: new ReferenceTypeReference(new QualifiedName(["List"])),
            variables:
            [
                new VariableDeclarator(
                    name: "list",
                    initializer: expr
                )
            ]
        );

        var method = new MethodDeclaration(
            returnType: new PrimitiveTypeReference(PrimitiveType.Int),
            name: "test",
            modifiers: Modifiers.Public,
            body: new BlockStatement([localVar])
        );

        var classDecl = new ClassDeclaration(
            name: "Test",
            modifiers: Modifiers.Public,
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("List list = new ArrayList();", result);
    }

    [TestMethod]
    public void WriteNewArrayExpression()
    {
        var expr = new NewArrayExpression(
            elementType: new PrimitiveTypeReference(PrimitiveType.Int),
            dimensionExpressions:
            [
                new LiteralExpression(10, LiteralKind.Integer)
            ]
        );

        var localVar = new LocalVariableDeclarationStatement(
            type: new ArrayTypeReference(new PrimitiveTypeReference(PrimitiveType.Int)),
            variables:
            [
                new VariableDeclarator(
                    name: "arr",
                    initializer: expr
                )
            ]
        );

        var method = new MethodDeclaration(
            returnType: new PrimitiveTypeReference(PrimitiveType.Int),
            name: "test",
            modifiers: Modifiers.Public,
            body: new BlockStatement([localVar])
        );

        var classDecl = new ClassDeclaration(
            name: "Test",
            modifiers: Modifiers.Public,
            members: [method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("int[] arr = new int[10];", result);
    }

    [TestMethod]
    public void WriteCompleteJavaFile()
    {
        var pkg = new PackageDeclaration(
            name: new QualifiedName(["com", "example"])
        );

        var import1 = new SingleTypeImportDeclaration(
            typeName: new QualifiedName(["java", "util", "List"])
        );

        var field = new FieldDeclaration(
            type: new ReferenceTypeReference(new QualifiedName(["String"])),
            modifiers: Modifiers.Private,
            variables:
            [
                new VariableDeclarator("name")
            ]
        );

        var method = new MethodDeclaration(
            returnType: new ReferenceTypeReference(new QualifiedName(["String"])),
            name: "getName",
            modifiers: Modifiers.Public,
            body: new BlockStatement(
            [
                new ReturnStatement(
                    new NameExpression(new QualifiedName(["name"]))
                )
            ])
        );

        var classDecl = new ClassDeclaration(
            name: "Person",
            modifiers: Modifiers.Public,
            members: [field, method]
        );

        var result = WriteNode(w => w.WriteCompilationUnit(new CompilationUnit(
            packageDeclaration: pkg,
            importDeclarations: [import1],
            typeDeclarations: [classDecl]
        )));

        Assert.Contains("package com.example;", result);
        Assert.Contains("import java.util.List;", result);
        Assert.Contains("public class Person", result);
        Assert.Contains("private String name;", result);
        Assert.Contains("public String getName()", result);
        Assert.Contains("return name;", result);
    }
}

