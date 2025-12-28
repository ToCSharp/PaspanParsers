# Java Parser - Grammar Specification and AST

This folder contains the Java language grammar specification and Abstract Syntax Tree (AST) implementation for building a Java parser using the Paspan parsing library.

## 📁 Files

### 📄 JavaGrammarSpecification.txt
Complete Java language grammar in BNF/EBNF notation based on:
- **Java Language Specification (JLS)** - Java SE 8 through Java SE 21
- [Oracle Java SE Specifications](https://docs.oracle.com/javase/specs/)
- [OpenJDK Projects](https://openjdk.org/projects/jdk/)

**Covers:**
- Compilation units, packages, and imports
- Type declarations (classes, interfaces, enums, annotations, records)
- Members (fields, methods, constructors, initializer blocks)
- Generics with wildcards (?, extends, super)
- Statements (if, switch, loops, try-catch-finally, synchronized)
- Expressions (operators, lambda, method references, switch expressions)
- Annotations
- Modern Java features (Java 8 through Java 21)

### 📄 JavaAst.cs
Complete AST node definitions for representing Java code structure.

**Key Components:**

#### Type Declarations
- `ClassDeclaration` - Classes with generics and inheritance
- `InterfaceDeclaration` - Interface types with default methods
- `EnumDeclaration` - Enumerations with methods
- `AnnotationDeclaration` - Annotation types (@interface)
- `RecordDeclaration` - Record types (Java 16+)

#### Members
- `FieldDeclaration` - Fields with modifiers
- `MethodDeclaration` - Methods with generics and throws clauses
- `ConstructorDeclaration` - Constructors with initializers
- `InitializerBlock` - Static and instance initializer blocks

#### Statements
- Control flow: `IfStatement`, `SwitchStatement`, `SwitchExpression`, `WhileStatement`, `DoStatement`, `ForStatement`, `EnhancedForStatement`
- Exception handling: `TryStatement` with try-with-resources and multi-catch
- Synchronization: `SynchronizedStatement`
- Jump statements: `BreakStatement`, `ContinueStatement`, `ReturnStatement`, `ThrowStatement`, `YieldStatement`

#### Expressions
- Literals: `LiteralExpression` (integers, floats, strings, chars, booleans, null, text blocks)
- Operators: `BinaryExpression`, `UnaryExpression`, `TernaryExpression`
- Object creation: `NewObjectExpression`, `NewArrayExpression`
- Type operations: `CastExpression`, `InstanceOfExpression`
- Lambdas: `LambdaExpression` (Java 8+)
- Method references: `MethodReferenceExpression` (Java 8+)
- Pattern matching: `InstanceOfExpression` with patterns (Java 16+)

#### Patterns (Java 16+)
- `TypePattern` - Type patterns with variables
- `RecordPattern` - Record deconstruction patterns (Java 21+)
- `GuardedPattern` - Patterns with guards (Java 21+)

#### Type System
- `PrimitiveTypeReference` - Primitive types (int, boolean, byte, etc.)
- `ReferenceTypeReference` - Reference types with generics
- `ArrayTypeReference` - Array types
- `WildcardType` - Wildcard types (?, extends, super)
- `VarTypeReference` - Local variable type inference (Java 10+)
- Type parameters with bounds

### 📄 JavaExamples.cs
Comprehensive examples of Java language features:
- Basic syntax (classes, interfaces, enums)
- Modern features (records, sealed classes, pattern matching)
- Generics with wildcards
- Lambda expressions and method references
- Stream API usage
- Annotations
- Exception handling
- Text blocks (Java 15+)

## Java Language Features Coverage

### Core Features (Java 8)
- ✅ Classes, interfaces, enums, annotations
- ✅ Fields, methods, constructors
- ✅ Generics with wildcards (?, extends, super)
- ✅ Exception handling (try-catch-finally, multi-catch, try-with-resources)
- ✅ Lambda expressions
- ✅ Method references (::)
- ✅ Default interface methods
- ✅ Type annotations

### Modern Features (Java 9 - 13)
- ✅ Modules (Java 9)
- ✅ Private interface methods (Java 9)
- ✅ Local variable type inference - var (Java 10)
- ✅ Switch expressions (Java 12-14)
- ✅ Yield statement (Java 13)
- ✅ Text blocks (Java 13-15)

### Latest Features (Java 14 - 21)
- ✅ Switch expressions (Java 14)
- ✅ Records (Java 16)
- ✅ Pattern matching for instanceof (Java 16)
- ✅ Sealed classes (Java 17)
- ✅ Pattern matching for switch (Java 21)
- ✅ Record patterns (Java 21)
- ✅ Unnamed patterns and variables (Java 21)

## Parser Implementation

### 📄 JavaParser.cs
A working Java parser implementation using Paspan parsing combinators.

**Features:**
- ✅ Full expression parsing with operator precedence
- ✅ Type declarations (class, interface, enum, annotation, record)
- ✅ Member declarations (fields, methods, constructors)
- ✅ Statements (if, switch, loops, try-catch, synchronized)
- ✅ Block statements and local variables
- ✅ Import declarations (regular, static, wildcard)
- ✅ Package declarations
- ✅ Modifiers (public, private, protected, static, final, etc.)
- ✅ Type references (primitive, reference, arrays, generics)
- ✅ Comments (single-line //, multi-line /* */, javadoc /** */)
- ✅ **Generics support** (type parameters, type arguments, wildcards, bounds)
- ✅ **Annotations support** (marker, single-element, normal annotations)
- ✅ **Lambda expressions** (Java 8+)
- ✅ **Method references** (Java 8+)
- ✅ **Switch expressions** (Java 14+)
- ✅ **Pattern matching** (instanceof patterns, switch patterns, record patterns)
- ✅ **Records** (Java 16+)
- ✅ **Sealed classes** (Java 17+)

**Limitations:**
- ⚠️ Simplified implementation for educational purposes
- ⚠️ Some edge cases with complex generics may not parse correctly
- ⚠️ Module system parsing is simplified
- ⚠️ Limited error recovery

**For production Java parsing, use:**
- [Eclipse JDT](https://www.eclipse.org/jdt/) - Eclipse Java Development Tools
- [JavaParser](https://javaparser.org/) - Java parsing and analysis library
- [ANTLR Java grammar](https://github.com/antlr/grammars-v4/tree/master/java)

### 📄 JavaParserTests.cs
Comprehensive test suite with 60+ test cases covering:
- Type declarations (classes, interfaces, enums, records, annotations)
- Member declarations (fields, methods, constructors)
- Statements (control flow, exception handling, synchronization)
- Expressions (operators, method calls, object creation)
- Modifiers and access control
- Type references and generics
- Annotations (marker, single-element, normal)
- Lambda expressions (various forms)
- Method references (all types)
- Pattern matching (instanceof, switch, record patterns)
- Modern Java features (records, sealed classes, text blocks)

## Usage Examples

### Parsing Java Code

```csharp
using Paspan.Tests.Java;

// Parse a simple class
var code = @"
    public class Person {
        private String name;
        private int age;
        
        public Person(String name, int age) {
            this.name = name;
            this.age = age;
        }
    }
";

var compilationUnit = JavaParser.Parse(code);

if (compilationUnit != null)
{
    var classDecl = (ClassDeclaration)compilationUnit.TypeDeclarations[0];
    Console.WriteLine($"Class: {classDecl.Name}");
    Console.WriteLine($"Members: {classDecl.Members.Count}");
}

// Parse with error handling
if (JavaParser.TryParse(code, out var result, out var error))
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
using Paspan.Tests.Java;

// Build a simple class
var classDecl = new ClassDeclaration(
    name: "Person",
    modifiers: Modifiers.Public,
    members: new[]
    {
        new FieldDeclaration(
            type: new ReferenceTypeReference("String"),
            variables: new[]
            {
                new VariableDeclarator("name")
            },
            modifiers: Modifiers.Private
        ),
        new FieldDeclaration(
            type: new PrimitiveTypeReference(PrimitiveType.Int),
            variables: new[]
            {
                new VariableDeclarator("age")
            },
            modifiers: Modifiers.Private
        )
    }
);

// Build a method
var method = new MethodDeclaration(
    returnType: new ReferenceTypeReference("String"),
    name: "getName",
    modifiers: Modifiers.Public,
    body: new BlockStatement(
        new[]
        {
            new ReturnStatement(
                new FieldAccessExpression(
                    new ThisExpression(),
                    "name"
                )
            )
        }
    )
);
```

### Parsing Complex Structures

```csharp
// Parse package with imports
var code = @"
    package com.example.app;
    
    import java.util.List;
    import java.util.ArrayList;
    import static java.lang.Math.*;
    
    public class Calculator {
        public int add(int a, int b) {
            return a + b;
        }
        
        public int multiply(int a, int b) {
            return a * b;
        }
    }
";

var cu = JavaParser.Parse(code);

// Access parsed elements
var packageDecl = cu.PackageDeclaration;      // Package
var imports = cu.ImportDeclarations;          // Imports
var types = cu.TypeDeclarations;              // Type declarations
var cls = (ClassDeclaration)types[0];         // Class
var methods = cls.Members;                    // Methods
```

### Parsing Modern Java Features

```csharp
// Parse a record (Java 16+)
var recordCode = @"
    public record Point(int x, int y) {
        public double distanceFromOrigin() {
            return Math.sqrt(x * x + y * y);
        }
    }
";

var recordAst = JavaParser.Parse(recordCode);

// Parse pattern matching (Java 16+)
var patternCode = @"
    if (obj instanceof String s) {
        System.out.println(s.toUpperCase());
    }
";

// Parse lambda expression (Java 8+)
var lambdaCode = @"
    List<String> names = List.of(""John"", ""Jane"");
    names.stream()
         .filter(n -> n.startsWith(""J""))
         .forEach(System.out::println);
";

// Parse sealed class (Java 17+)
var sealedCode = @"
    public sealed interface Shape permits Circle, Rectangle {
        double area();
    }
    
    public final class Circle implements Shape {
        private final double radius;
        
        public Circle(double radius) {
            this.radius = radius;
        }
        
        @Override
        public double area() {
            return Math.PI * radius * radius;
        }
    }
";
```

## Code Generation

### 📄 JavaWriter.cs
Generates Java source code from AST nodes.

```csharp
using Paspan.Tests.Java;

var writer = new JavaWriter();

// Write a compilation unit
var cu = new CompilationUnit(
    packageDeclaration: new PackageDeclaration("com.example"),
    importDeclarations: new[]
    {
        new ImportDeclaration("java.util.List")
    },
    typeDeclarations: new[]
    {
        new ClassDeclaration(
            name: "Person",
            modifiers: Modifiers.Public,
            members: new[]
            {
                new FieldDeclaration(
                    type: new ReferenceTypeReference("String"),
                    variables: new[] { new VariableDeclarator("name") },
                    modifiers: Modifiers.Private
                )
            }
        )
    }
);

writer.WriteCompilationUnit(cu);
var code = writer.GetResult();

Console.WriteLine(code);
```

**Output:**
```java
package com.example;

import java.util.List;

public class Person {
    private String name;
}
```

## AST Structure Overview

```
CompilationUnit
├── PackageDeclaration
├── ImportDeclarations
│   ├── ImportDeclaration (regular)
│   ├── StaticImportDeclaration
│   └── WildcardImportDeclaration
└── TypeDeclarations
    ├── ClassDeclaration
    ├── InterfaceDeclaration
    ├── EnumDeclaration
    ├── AnnotationDeclaration
    └── RecordDeclaration

Type Members
├── FieldDeclaration
├── MethodDeclaration
├── ConstructorDeclaration
└── InitializerBlock (static/instance)

Statements
├── BlockStatement
├── ExpressionStatement
├── LocalVariableDeclaration
├── IfStatement
├── SwitchStatement / SwitchExpression
├── WhileStatement / DoStatement
├── ForStatement / EnhancedForStatement
├── TryStatement (with resources, multi-catch)
├── SynchronizedStatement
└── Jump statements (break, continue, return, throw, yield)

Expressions
├── LiteralExpression (int, float, string, char, boolean, null, text blocks)
├── BinaryExpression / UnaryExpression / TernaryExpression
├── MethodInvocationExpression
├── FieldAccessExpression
├── ArrayAccessExpression
├── NewObjectExpression / NewArrayExpression
├── CastExpression
├── InstanceOfExpression (with patterns)
├── LambdaExpression
├── MethodReferenceExpression
└── SwitchExpression
```

## Implementation Notes

### Modifiers
The `Modifiers` enum uses flags to support multiple modifiers:
```csharp
var modifiers = Modifiers.Public | Modifiers.Static | Modifiers.Final;
```

Java modifiers include:
- Access: `public`, `protected`, `private`, (default/package-private)
- Class/Method: `abstract`, `final`, `static`, `strictfp`
- Field: `volatile`, `transient`
- Method: `synchronized`, `native`
- Interface: `default` (Java 8+)
- Sealed: `sealed`, `non-sealed` (Java 17+)

### Type References
Type references support:
- Primitive types (boolean, byte, short, int, long, char, float, double)
- Reference types with generics
- Arrays with multi-dimensional support
- Wildcards (?, extends, super)
- Type variable references

### Generics
Full support for Java generics:
- Type parameters with bounds: `<T extends Number & Comparable<T>>`
- Wildcards: `List<?>`, `List<? extends Number>`, `List<? super Integer>`
- Multiple type parameters
- Nested generics

### Lambda Expressions
Java lambda syntax:
- Single parameter: `x -> x * x`
- Multiple parameters: `(a, b) -> a + b`
- Explicit types: `(int a, int b) -> a + b`
- Block body: `x -> { return x * x; }`

### Method References
All forms of method references:
- Static: `ClassName::staticMethod`
- Instance: `instance::instanceMethod`
- Instance arbitrary: `ClassName::instanceMethod`
- Constructor: `ClassName::new`
- Array constructor: `int[]::new`

### Pattern Matching
Modern Java pattern matching:
- Type patterns: `obj instanceof String s`
- Record patterns: `point instanceof Point(int x, int y)`
- Guarded patterns: `case String s when s.length() > 5 -> ...`

## Comparison with C# AST

| Feature | Java | C# |
|---------|------|-----|
| **Purpose** | JVM-based language parsing | .NET language parsing |
| **Package/Namespace** | package (single) | namespace (nested) |
| **Modifiers** | default, strictfp, native | internal, unsafe, volatile |
| **Generics** | Type erasure, wildcards | Reified, variance annotations |
| **Properties** | No properties (getters/setters) | First-class properties |
| **Events** | No events (listeners pattern) | First-class events |
| **Delegates** | Functional interfaces | First-class delegates |
| **Lambda** | Similar syntax | Similar syntax |
| **LINQ** | Stream API | LINQ queries |
| **Nullable** | @Nullable annotations | ? suffix |
| **Records** | Immutable data carriers (Java 16+) | Similar (C# 9+) |
| **Sealed** | sealed/permits (Java 17+) | sealed (C# 9+) |

## Key Differences: Java vs C#

1. **Package vs Namespace**
   - Java: Single package per file
   - C#: Nested namespaces, file-scoped namespaces

2. **Properties**
   - Java: Getter/setter methods
   - C#: First-class property syntax

3. **Generics**
   - Java: Type erasure, runtime types not available
   - C#: Reified generics, full runtime type information

4. **Wildcards**
   - Java: `? extends T`, `? super T`
   - C#: `in T`, `out T` (covariance/contravariance)

5. **Checked Exceptions**
   - Java: Explicit throws clauses
   - C#: No checked exceptions

6. **Default Interface Methods**
   - Java: `default` keyword (Java 8+)
   - C#: Default interface implementations (C# 8+)

7. **Text Blocks**
   - Java: `"""..."""` (Java 15+)
   - C#: `@"..."` or `"""..."""` (C# 11+)

8. **Pattern Matching**
   - Java: instanceof patterns (Java 16+), switch patterns (Java 21+)
   - C#: is patterns, switch expressions (C# 8+)

## Future Enhancements

Potential additions:
- [ ] Module system (module-info.java) full support
- [ ] Advanced annotation processing
- [ ] Javadoc comment parsing and AST
- [ ] Source location information (line, column)
- [ ] Detailed trivia (whitespace, comments, formatting)
- [ ] Error recovery for partial parsing
- [ ] Symbol resolution and type checking
- [ ] Control flow analysis
- [ ] Data flow analysis

## References

### Official Specifications
- [Java Language Specification](https://docs.oracle.com/javase/specs/jls/se21/html/index.html)
- [Java SE Documentation](https://docs.oracle.com/en/java/javase/)
- [OpenJDK](https://openjdk.org/)

### Implementation References
- [Eclipse JDT](https://www.eclipse.org/jdt/) - Eclipse Java Development Tools
- [JavaParser](https://javaparser.org/) - Java parsing and analysis library
- [ANTLR Java Grammar](https://github.com/antlr/grammars-v4/tree/master/java)
- [Roslyn](https://github.com/dotnet/roslyn) - For comparison with C# parsing

### Java Language References
- [Java Tutorials](https://docs.oracle.com/javase/tutorial/)
- [JEP Index](https://openjdk.org/jeps/0) - Java Enhancement Proposals
- [Java Language Updates](https://docs.oracle.com/en/java/javase/21/language/java-language-changes.html)

## License

This specification and AST implementation are provided for educational and development purposes as part of the Paspan parser project.

---

**Note**: This implementation covers the vast majority of Java language features through Java 21. For production parser implementation, refer to Eclipse JDT or JavaParser for complete semantic analysis and advanced scenarios.

