using PaspanParsers.CSharp;

namespace PaspanParsers.Tests.CSharp;

/// <summary>
/// Example demonstrating how to use CSharpWriter to generate C# code from AST nodes
/// </summary>
public static class CSharpWriterExample
{
    /// <summary>
    /// Generates a simple Person class with properties and a method
    /// </summary>
    public static string GeneratePersonClass()
    {
        // Create properties
        var nameProperty = new PropertyDeclaration(
            type: new PredefinedTypeReference(PredefinedType.String),
            name: "Name",
            modifiers: Modifiers.Public,
            accessors:
            [
                new Accessor(AccessorKind.Get),
                new Accessor(AccessorKind.Set)
            ]
        );

        var ageProperty = new PropertyDeclaration(
            type: new PredefinedTypeReference(PredefinedType.Int),
            name: "Age",
            modifiers: Modifiers.Public,
            accessors:
            [
                new Accessor(AccessorKind.Get),
                new Accessor(AccessorKind.Set)
            ]
        );

        // Create a GetInfo method that returns a formatted string
        var returnStmt = new ReturnStatement(
            new BinaryExpression(
                new BinaryExpression(
                    new BinaryExpression(
                        new LiteralExpression("Name: ", LiteralKind.String),
                        BinaryOperator.Add,
                        new MemberAccessExpression("Name", new NameExpression(["this"]))
                    ),
                    BinaryOperator.Add,
                    new LiteralExpression(", Age: ", LiteralKind.String)
                ),
                BinaryOperator.Add,
                new MemberAccessExpression("Age", new NameExpression(["this"]))
            )
        );

        var getInfoMethod = new MethodDeclaration(
            returnType: new PredefinedTypeReference(PredefinedType.String),
            name: "GetInfo",
            modifiers: Modifiers.Public,
            body: new BlockMethodBody(new BlockStatement([returnStmt]))
        );

        // Create the class
        var classDecl = new ClassDeclaration(
            name: "Person",
            modifiers: Modifiers.Public,
            members: [nameProperty, ageProperty, getInfoMethod]
        );

        // Create namespace
        var namespaceDecl = new NamespaceDeclaration(
            name: new NameExpression(["MyApp", "Models"]),
            members: [classDecl]
        );

        // Create compilation unit with using directives
        var compilationUnit = new CompilationUnit(
            usings:
            [
                new UsingNamespaceDirective(new NameExpression(["System"]))
            ],
            members: [namespaceDecl]
        );

        // Write to C# code
        var writer = new CSharpWriter();
        writer.WriteCompilationUnit(compilationUnit);
        return writer.GetResult();
    }

    /// <summary>
    /// Generates a generic repository interface
    /// </summary>
    public static string GenerateRepositoryInterface()
    {
        // Create type parameter T
        var typeParameter = new TypeParameter("T");

        // Create GetById method
        var getByIdMethod = new MethodDeclaration(
            returnType: new NamedTypeReference(new NameExpression(["T"])),
            name: "GetById",
            parameters:
            [
                new Parameter(new PredefinedTypeReference(PredefinedType.Int), "id")
            ]
        );

        // Create GetAll method
        var getAllMethod = new MethodDeclaration(
            returnType: new NamedTypeReference(
                new NameExpression(["IEnumerable"]),
                typeArguments: [new NamedTypeReference(new NameExpression(["T"]))]
            ),
            name: "GetAll"
        );

        // Create Add method
        var addMethod = new MethodDeclaration(
            returnType: new PredefinedTypeReference(PredefinedType.Void),
            name: "Add",
            parameters:
            [
                new Parameter(new NamedTypeReference(new NameExpression(["T"])), "entity")
            ]
        );

        // Create Delete method
        var deleteMethod = new MethodDeclaration(
            returnType: new PredefinedTypeReference(PredefinedType.Void),
            name: "Delete",
            parameters:
            [
                new Parameter(new PredefinedTypeReference(PredefinedType.Int), "id")
            ]
        );

        // Create interface
        var interfaceDecl = new InterfaceDeclaration(
            name: "IRepository",
            modifiers: Modifiers.Public,
            typeParameters: [typeParameter],
            members: [getByIdMethod, getAllMethod, addMethod, deleteMethod]
        );

        // Create namespace with file-scoped syntax
        var namespaceDecl = new NamespaceDeclaration(
            name: new NameExpression(["MyApp", "Data"]),
            members: [interfaceDecl],
            isFileScopedNamespace: true
        );

        // Create compilation unit
        var compilationUnit = new CompilationUnit(
            usings:
            [
                new UsingNamespaceDirective(new NameExpression(["System"])),
                new UsingNamespaceDirective(new NameExpression(["System", "Collections", "Generic"]))
            ],
            members: [namespaceDecl]
        );

        // Write to C# code
        var writer = new CSharpWriter();
        writer.WriteCompilationUnit(compilationUnit);
        return writer.GetResult();
    }

    /// <summary>
    /// Generates a record with primary constructor
    /// </summary>
    public static string GenerateProductRecord()
    {
        // Create record with primary constructor
        var recordDecl = new RecordDeclaration(
            name: "Product",
            modifiers: Modifiers.Public,
            primaryConstructorParameters:
            [
                new Parameter(new PredefinedTypeReference(PredefinedType.Int), "Id"),
                new Parameter(new PredefinedTypeReference(PredefinedType.String), "Name"),
                new Parameter(new PredefinedTypeReference(PredefinedType.Decimal), "Price")
            ]
        );

        // Create namespace
        var namespaceDecl = new NamespaceDeclaration(
            name: new NameExpression(["MyApp", "Models"]),
            members: [recordDecl],
            isFileScopedNamespace: true
        );

        // Create compilation unit
        var compilationUnit = new CompilationUnit(
            members: [namespaceDecl]
        );

        // Write to C# code
        var writer = new CSharpWriter();
        writer.WriteCompilationUnit(compilationUnit);
        return writer.GetResult();
    }

    /// <summary>
    /// Example showing all generated code
    /// </summary>
    public static void PrintExamples()
    {
        Console.WriteLine("=== Person Class ===");
        Console.WriteLine(GeneratePersonClass());
        Console.WriteLine();

        Console.WriteLine("=== Repository Interface ===");
        Console.WriteLine(GenerateRepositoryInterface());
        Console.WriteLine();

        Console.WriteLine("=== Product Record ===");
        Console.WriteLine(GenerateProductRecord());
    }
}

