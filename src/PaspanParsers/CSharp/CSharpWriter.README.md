# CSharpWriter

A comprehensive C# code generator that writes C# source code from AST (Abstract Syntax Tree) nodes defined in `CSharpAst.cs`.

## Overview

`CSharpWriter` is a visitor-pattern-based code generator that traverses C# AST nodes and produces formatted C# source code. It supports all major C# language features including:

- Compilation units with using directives
- Namespaces (both traditional and file-scoped)
- Type declarations (classes, structs, interfaces, enums, records, delegates)
- Member declarations (methods, properties, fields, events, indexers, constructors)
- Statements (if, switch, loops, try-catch, using, etc.)
- Expressions (binary, unary, lambda, LINQ queries, switch expressions, etc.)
- Patterns (type, constant, declaration, recursive, relational, logical)
- Attributes and modifiers

## Usage

### Basic Example

```csharp
// Create AST nodes
var classDecl = new ClassDeclaration(
    name: "Person",
    modifiers: Modifiers.Public,
    members: new MemberDeclaration[]
    {
        new PropertyDeclaration(
            type: new PredefinedTypeReference(PredefinedType.String),
            name: "Name",
            modifiers: Modifiers.Public,
            accessors: new[]
            {
                new Accessor(AccessorKind.Get),
                new Accessor(AccessorKind.Set)
            }
        )
    }
);

var compilationUnit = new CompilationUnit(
    usings: new UsingDirective[]
    {
        new UsingNamespaceDirective(new NameExpression(new[] { "System" }))
    },
    members: new MemberDeclaration[] { classDecl }
);

// Write to C# code
var writer = new CSharpWriter();
writer.WriteCompilationUnit(compilationUnit);
string code = writer.GetResult();
```

Output:
```csharp
using System;

public class Person
{
    public string Name
    {
        get;
        set;
    }
}
```

### Advanced Example: Generic Repository Interface

```csharp
var typeParameter = new TypeParameter("T");

var interfaceDecl = new InterfaceDeclaration(
    name: "IRepository",
    modifiers: Modifiers.Public,
    typeParameters: new[] { typeParameter },
    members: new MemberDeclaration[]
    {
        new MethodDeclaration(
            returnType: new NamedTypeReference(new NameExpression(new[] { "T" })),
            name: "GetById",
            parameters: new[]
            {
                new Parameter(new PredefinedTypeReference(PredefinedType.Int), "id")
            }
        ),
        new MethodDeclaration(
            returnType: new NamedTypeReference(
                new NameExpression(new[] { "IEnumerable" }),
                typeArguments: new[] { new NamedTypeReference(new NameExpression(new[] { "T" })) }
            ),
            name: "GetAll"
        )
    }
);

var namespaceDecl = new NamespaceDeclaration(
    name: new NameExpression(new[] { "MyApp", "Data" }),
    members: new MemberDeclaration[] { interfaceDecl },
    isFileScopedNamespace: true
);

var compilationUnit = new CompilationUnit(
    usings: new UsingDirective[]
    {
        new UsingNamespaceDirective(new NameExpression(new[] { "System" })),
        new UsingNamespaceDirective(new NameExpression(new[] { "System", "Collections", "Generic" }))
    },
    members: new MemberDeclaration[] { namespaceDecl }
);

var writer = new CSharpWriter();
writer.WriteCompilationUnit(compilationUnit);
string code = writer.GetResult();
```

Output:
```csharp
using System;
using System.Collections.Generic;

namespace MyApp.Data;

public interface IRepository<T>
{
    T GetById(int id);
    IEnumerable<T> GetAll();
}
```

### Record with Primary Constructor

```csharp
var recordDecl = new RecordDeclaration(
    name: "Product",
    modifiers: Modifiers.Public,
    primaryConstructorParameters: new[]
    {
        new Parameter(new PredefinedTypeReference(PredefinedType.Int), "Id"),
        new Parameter(new PredefinedTypeReference(PredefinedType.String), "Name"),
        new Parameter(new PredefinedTypeReference(PredefinedType.Decimal), "Price")
    }
);

var namespaceDecl = new NamespaceDeclaration(
    name: new NameExpression(new[] { "MyApp", "Models" }),
    members: new MemberDeclaration[] { recordDecl },
    isFileScopedNamespace: true
);

var writer = new CSharpWriter();
writer.WriteCompilationUnit(new CompilationUnit(members: new[] { namespaceDecl }));
string code = writer.GetResult();
```

Output:
```csharp
namespace MyApp.Models;

public record Product(int Id, string Name, decimal Price);
```

## Features

### Formatting

- **Indentation**: Configurable indentation (default: 4 spaces)
- **Line breaks**: Proper line breaks between declarations
- **Spacing**: Consistent spacing around operators and keywords

### Supported Language Features

#### Type Declarations
- Classes (with inheritance, constraints, generic parameters)
- Structs
- Interfaces
- Enums
- Records (both class and struct)
- Delegates

#### Member Declarations
- Methods (with generic parameters, constraints, expression bodies)
- Properties (auto-properties, expression-bodied, with initializers)
- Fields
- Events
- Indexers
- Constructors (with initializers)

#### Statements
- Block statements
- Expression statements
- Local declarations (including const and using)
- Control flow (if/else, switch, while, do-while, for, foreach)
- Exception handling (try/catch/finally)
- Jump statements (return, throw, break, continue, goto, yield)
- Using statements (including await using)
- Lock statements

#### Expressions
- Literals (null, boolean, integer, real, character, string)
- Names (simple and qualified)
- Binary operations (arithmetic, logical, bitwise, comparison, assignment)
- Unary operations (prefix and postfix)
- Conditional (ternary) operator
- Method invocations
- Member access (including null-conditional)
- Element access (indexers)
- Object creation (with initializers)
- Array creation (with initializers)
- Cast expressions
- Type testing (is, as)
- Lambda expressions (expression and block bodies)
- LINQ queries (from, where, select, orderby, join, let, group by)
- Switch expressions
- Throw expressions
- Default expressions
- typeof, sizeof, nameof
- await expressions
- Tuple expressions
- Range expressions (..)
- with expressions (for records)

#### Patterns
- Type patterns
- Constant patterns
- Declaration patterns
- Var patterns
- Discard patterns
- Recursive patterns (positional and property)
- Relational patterns (<, <=, >, >=)
- Logical patterns (and, or, not)

#### Attributes
- Attribute sections with optional targets
- Multiple attributes per section
- Attribute arguments (positional and named)

#### Modifiers
All C# modifiers are supported:
- Access modifiers: public, private, protected, internal, file
- Inheritance modifiers: abstract, sealed, virtual, override
- Other modifiers: static, readonly, const, extern, unsafe, volatile, async, partial, required, ref

### Customization

The `CSharpWriter` constructor accepts an optional `indentString` parameter:

```csharp
// Use tabs instead of spaces
var writer = new CSharpWriter(indentString: "\t");

// Use 2 spaces for indentation
var writer = new CSharpWriter(indentString: "  ");
```

## Testing

The implementation includes comprehensive unit tests in:
- `CSharpWriterTests.cs`: 17 tests covering all major features
- `CSharpWriterExampleTest.cs`: 3 tests demonstrating real-world usage

All tests pass successfully.

## Implementation Details

### Architecture

The writer uses a visitor pattern with private methods for each AST node type:
- `WriteMemberDeclaration()` - dispatches to specific member writers
- `WriteStatement()` - handles all statement types
- `WriteExpression()` - handles all expression types
- `WritePattern()` - handles all pattern types
- etc.

### Indentation Management

The writer maintains indentation state through:
- `_indentLevel`: Current indentation level
- `_needsIndent`: Flag indicating if indentation is needed on next write
- `Indent()` / `Unindent()`: Methods to adjust indentation level

### String Building

Uses `StringBuilder` internally for efficient string concatenation.

## Examples

See `CSharpWriterExample.cs` for complete working examples:
- `GeneratePersonClass()` - Simple class with properties and methods
- `GenerateRepositoryInterface()` - Generic interface with file-scoped namespace
- `GenerateProductRecord()` - Record with primary constructor

## Future Enhancements

Potential improvements:
- Configurable formatting options (brace style, spacing rules, etc.)
- XML documentation comment support
- Preprocessor directive support (#if, #define, etc.)
- Raw string literals (C# 11+)
- List patterns (C# 11+)
- Required members (C# 11+)
- File-scoped types (C# 11+)

## Related Files

- `CSharpAst.cs` - AST node definitions
- `CSharpParser.cs` - Parser that creates AST from source code
- `CSharpWriterTests.cs` - Unit tests
- `CSharpWriterExample.cs` - Usage examples

