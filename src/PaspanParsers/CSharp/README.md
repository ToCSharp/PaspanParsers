# C# Parser - Grammar Specification and AST

This folder contains the C# language grammar specification and Abstract Syntax Tree (AST) implementation for building a C# parser.

## Files

### 📄 CSharpGrammarSpecification.txt
Complete C# language grammar in BNF/EBNF notation based on:
- **ECMA-334**: C# Language Specification
- **ISO/IEC 23270**: Information technology — Programming languages — C#
- **Microsoft C# Language Specification**

**Covers:**
- Compilation units and namespaces
- Type declarations (classes, structs, interfaces, enums, delegates, records)
- Members (fields, properties, methods, events, indexers, operators, constructors)
- Generics and type parameters with constraints
- Statements (if, switch, loops, try-catch, using, lock, etc.)
- Expressions (arithmetic, logical, lambda, LINQ, pattern matching)
- Attributes
- Modern C# features (C# 1.0 through C# 12.0)

### 📄 CSharpAst.cs
Complete AST node definitions for representing C# code structure.

**Key Components:**

#### Type Declarations
- `ClassDeclaration` - Classes with inheritance and members
- `StructDeclaration` - Value types
- `InterfaceDeclaration` - Interface types with variance support
- `EnumDeclaration` - Enumerations
- `DelegateDeclaration` - Delegate types
- `RecordDeclaration` - Record types (C# 9+)

#### Members
- `FieldDeclaration` - Fields with modifiers
- `MethodDeclaration` - Methods with type parameters and constraints
- `PropertyDeclaration` - Properties with accessors (get, set, init)
- `IndexerDeclaration` - Indexers
- `EventDeclaration` - Events
- `ConstructorDeclaration` - Constructors with initializers

#### Statements
- Control flow: `IfStatement`, `SwitchStatement`, `WhileStatement`, `DoStatement`, `ForStatement`, `ForEachStatement`
- Exception handling: `TryStatement` with catch clauses and filters
- Resource management: `UsingStatement`, `LockStatement`
- Jump statements: `BreakStatement`, `ContinueStatement`, `ReturnStatement`, `ThrowStatement`
- Iterators: `YieldReturnStatement`, `YieldBreakStatement`

#### Expressions
- Literals: `LiteralExpression`
- Operators: `BinaryExpression`, `UnaryExpression`, `ConditionalExpression`
- Object creation: `ObjectCreationExpression`, `ArrayCreationExpression`
- Type operations: `CastExpression`, `IsExpression`, `AsExpression`
- Lambdas: `LambdaExpression` with expression and block bodies
- LINQ: `QueryExpression` with from, where, select, join, group by
- Modern features: `SwitchExpression`, `RangeExpression`, `WithExpression`

#### Patterns (C# 7+)
- `TypePattern` - Type patterns
- `ConstantPattern` - Constant patterns
- `DeclarationPattern` - Declaration patterns with variables
- `RecursivePattern` - Positional and property patterns
- `RelationalPattern` - Relational patterns (>, <, >=, <=)
- `LogicalPattern` - Logical patterns (and, or, not)

#### Type System
- `NamedTypeReference` - Named types with generic arguments
- `PredefinedTypeReference` - Built-in types (int, string, bool, etc.)
- `ArrayTypeReference` - Array types with rank
- `TupleTypeReference` - Tuple types (C# 7+)
- Type parameters with variance (in, out) and constraints

### 📄 CSharpExamples.cs
Comprehensive examples of C# language features:
- Basic syntax (classes, structs, interfaces, enums)
- Modern features (records, pattern matching, nullable reference types)
- Generics with constraints
- Properties, indexers, and operators
- Async/await and iterators
- LINQ query and method syntax
- Lambda expressions
- Attributes

## C# Language Features Coverage

### Core Features (C# 1.0 - 2.0)
- ✅ Classes, structs, interfaces, enums, delegates
- ✅ Properties, indexers, events
- ✅ Generics with constraints
- ✅ Partial types
- ✅ Anonymous methods
- ✅ Nullable value types
- ✅ Iterators (yield)

### Modern Features (C# 3.0 - 7.3)
- ✅ Auto-properties
- ✅ Object and collection initializers
- ✅ Anonymous types
- ✅ Extension methods
- ✅ Lambda expressions
- ✅ LINQ query expressions
- ✅ Expression-bodied members
- ✅ Tuples and deconstruction
- ✅ Pattern matching
- ✅ Local functions
- ✅ Ref returns and locals
- ✅ Discards

### Latest Features (C# 8.0 - 12.0)
- ✅ Nullable reference types
- ✅ Async streams (IAsyncEnumerable)
- ✅ Ranges and indices
- ✅ Default interface methods
- ✅ Switch expressions
- ✅ Property patterns
- ✅ Records (C# 9)
- ✅ Init-only setters
- ✅ Top-level statements support
- ✅ Global using directives
- ✅ File-scoped namespaces
- ✅ Record structs
- ✅ Required members
- ✅ List patterns
- ✅ Raw string literals support
- ✅ Primary constructors

## Parser Implementation

### 📄 CSharpParser.cs
A working C# parser implementation using Paspan parsing combinators.

**Features:**
- ✅ Full expression parsing with operator precedence
- ✅ Type declarations (class, struct, interface, enum)
- ✅ Member declarations (fields, properties, methods, constructors)
- ✅ Statements (if, while, do, for, return, break, continue, throw)
- ✅ Block statements
- ✅ Using directives (namespace, alias, static)
- ✅ Namespace declarations
- ✅ Modifiers (public, private, static, etc.)
- ✅ Type references (predefined types, named types, arrays, nullable)
- ✅ Comments (single-line // and multi-line /* */)
- ✅ **Generics support** (type parameters, type arguments, constraints)
- ✅ **Attributes support** (class, method, property, field, enum member, global attributes)
- ✅ **Lambda expressions** (expression and block bodies, async lambdas, explicit/implicit parameters)
- ✅ **LINQ query expressions** (from, where, select, let, orderby, join, group by, into)
- ✅ **Pattern matching** (is-expressions, switch expressions, type/constant/var/discard/declaration/recursive patterns)

**Limitations:**
- ⚠️ Simplified implementation for educational purposes
- ⚠️ Some edge cases with lambda expressions, LINQ queries, and pattern matching may not parse correctly
- ⚠️ No preprocessor directives
- ⚠️ Limited error recovery

**For production C# parsing, use [Roslyn](https://github.com/dotnet/roslyn).**

### 📄 CSharpParserTests.cs
Comprehensive test suite with 55+ test cases covering:
- Type declarations
- Member declarations
- Statements
- Expressions
- Modifiers
- Type references
- Attributes (class, method, property, field, enum, global)
- Lambda expressions (simple, multiple parameters, async, block body)
- LINQ query expressions (from, where, select, let, orderby, join, group by)
- Pattern matching (is-expressions, switch expressions, various pattern types)

## Usage Examples

### Parsing C# Code

```csharp
using Paspan.Tests.CSharp;

// Parse a simple class
var code = @"
    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
";

var compilationUnit = CSharpParser.Parse(code);

if (compilationUnit != null)
{
    var classDecl = (ClassDeclaration)compilationUnit.Members[0];
    Console.WriteLine($"Class: {classDecl.Name}");
    Console.WriteLine($"Properties: {classDecl.Members.Count}");
}

// Parse with error handling
if (CSharpParser.TryParse(code, out var result, out var error))
{
    Console.WriteLine("Parsing succeeded!");
}
else
{
    Console.WriteLine($"Parse error: {error}");
}
```

### Building AST Manually

```csharp
using Paspan.Tests.CSharp;

// Build a simple class
var classDecl = new ClassDeclaration(
    name: "Person",
    modifiers: Modifiers.Public,
    members: new[]
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
        ),
        new PropertyDeclaration(
            type: new PredefinedTypeReference(PredefinedType.Int),
            name: "Age",
            modifiers: Modifiers.Public,
            accessors: new[]
            {
                new Accessor(AccessorKind.Get),
                new Accessor(AccessorKind.Set)
            }
        )
    }
);

// Build a method with expression body
var method = new MethodDeclaration(
    returnType: new PredefinedTypeReference(PredefinedType.String),
    name: "GetInfo",
    modifiers: Modifiers.Public,
    body: new ExpressionMethodBody(
        new BinaryExpression(
            left: new LiteralExpression("Name: ", LiteralKind.String),
            op: BinaryOperator.Add,
            right: new NameExpression(new[] { "Name" })
        )
    )
);
```

### Parsing Complex Structures

```csharp
// Parse namespace with using directives
var code = @"
    using System;
    using System.Collections.Generic;
    
    namespace MyApp.Domain
    {
        public class Calculator
        {
            public int Add(int a, int b)
            {
                return a + b;
            }
            
            public int Multiply(int a, int b) => a * b;
        }
    }
";

var cu = CSharpParser.Parse(code);

// Access parsed elements
var usings = cu.Usings; // Using directives
var ns = (NamespaceDeclaration)cu.Members[0]; // Namespace
var cls = (ClassDeclaration)ns.Members[0]; // Class
var methods = cls.Members; // Methods
```

## AST Structure Overview

```
CompilationUnit
├── ExternAliasDirectives
├── UsingDirectives
│   ├── UsingNamespaceDirective
│   ├── UsingAliasDirective
│   └── UsingStaticDirective
├── GlobalAttributes
└── Members
    ├── NamespaceDeclaration
    └── TypeDeclarations
        ├── ClassDeclaration
        ├── StructDeclaration
        ├── InterfaceDeclaration
        ├── EnumDeclaration
        ├── DelegateDeclaration
        └── RecordDeclaration

Type Members
├── FieldDeclaration
├── MethodDeclaration
├── PropertyDeclaration
├── IndexerDeclaration
├── EventDeclaration
├── OperatorDeclaration
└── ConstructorDeclaration

Statements
├── BlockStatement
├── ExpressionStatement
├── LocalDeclarationStatement
├── IfStatement
├── SwitchStatement
├── WhileStatement / DoStatement
├── ForStatement / ForEachStatement
├── TryStatement
├── UsingStatement
└── YieldStatement

Expressions
├── LiteralExpression
├── NameExpression
├── BinaryExpression / UnaryExpression
├── InvocationExpression
├── MemberAccessExpression
├── ObjectCreationExpression
├── LambdaExpression
├── QueryExpression (LINQ)
└── Pattern matching expressions
```

## Implementation Notes

### Modifiers
The `Modifiers` enum uses flags to support multiple modifiers:
```csharp
var modifiers = Modifiers.Public | Modifiers.Static | Modifiers.Readonly;
```

### Type References
Type references support:
- Predefined types (int, string, bool, etc.)
- Named types with generic arguments
- Arrays with multi-dimensional support
- Tuples with named elements
- Nullable annotations

### Method Bodies
Methods can have three types of bodies:
- `BlockMethodBody` - Traditional block with statements
- `ExpressionMethodBody` - Expression-bodied member (=>)
- `null` - Abstract/interface methods

### Patterns
Full support for C# pattern matching:
- Type patterns with variables
- Property patterns with nested patterns
- Relational patterns for comparisons
- Logical patterns (and, or, not)
- List patterns (C# 11+)

## Comparison with SQL AST

| Feature | SQL AST | C# AST |
|---------|---------|--------|
| **Purpose** | Query language parsing | Programming language parsing |
| **Complexity** | Moderate (focused on data queries) | High (full programming language) |
| **Type System** | Limited (data types) | Rich (classes, generics, constraints) |
| **Expressions** | SQL-specific (aggregates, joins) | General-purpose (OOP, functional) |
| **Statements** | DML/DDL operations | Control flow, declarations, expressions |
| **Modern Features** | CTEs, window functions | Pattern matching, async/await, LINQ |

## Future Enhancements

Potential additions:
- [ ] Preprocessor directives (#if, #define, etc.)
- [ ] XML documentation comments
- [ ] Unsafe code blocks with pointers
- [ ] Fixed-size buffers
- [ ] Extern alias advanced scenarios
- [ ] Assembly-level attributes
- [ ] Module-level attributes
- [ ] Detailed trivia (whitespace, comments)
- [ ] Source code location information (line, column)

## References

### Official Specifications
- [ECMA-334 C# Language Specification](https://ecma-international.org/publications-and-standards/standards/ecma-334/)
- [Microsoft C# Language Specification](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/specifications/)
- [C# Language Design](https://github.com/dotnet/csharplang)

### Implementation References
- [Roslyn Compiler](https://github.com/dotnet/roslyn) - Official C# compiler source code
- [C# Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/)
- [C# Language Reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/)

## License

This specification and AST implementation are provided for educational and development purposes as part of the Paspan parser project.

---

**Note**: This implementation covers the vast majority of C# language features through C# 12.0. For production parser implementation, refer to the official [Roslyn](https://github.com/dotnet/roslyn) compiler for complete semantic analysis and advanced scenarios.

