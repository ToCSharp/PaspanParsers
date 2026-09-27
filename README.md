# PaspanParsers

Paspan is a [Parlot](https://github.com/sebastienros/parlot) fork - a __fast__, __lightweight__, and easy-to-use .NET parser combinator library optimized for `Span<byte>` and UTF-8 parsing.

This repository contains improved Paspan core library and a collection of production-ready language parsers written in pure C# using Paspan combinators.

## 🚀 Features

- **High Performance**: Direct UTF-8 byte parsing without string conversion
- **Zero-Allocation**: Uses `Span<T>` and `ref struct` for minimal memory overhead
- **Fluent API**: Readable parser combinator syntax
- **Production-Ready Parsers**: Complete implementations for popular languages

## 📦 What's Included

### Core Library
- **Paspan** - Core parser combinator library with `SpanReader`
- **PaspanCommon** - Shared fluent API and parser combinators

### Language Parsers (`src/PaspanParsers`)

| Parser | Status | Description |
|--------|--------|-------------|
| **C#** | ✅ Complete | Full C# parser (C# 1.0 - 12.0) with AST and code generator |
| **Python** | ✅ Complete | Python 3.6-3.12 parser with pattern matching, async/await, type hints |
| **Java** | ✅ Complete | Java parser with AST and code generator |
| **JSON** | ✅ Complete | Fast JSON parser with Region-based zero-copy support |
| **SQL** | 🚧 WIP | SQL parser with CTEs, JOINs, window functions |
| **Calc** | ✅ Complete | Mathematical expression parser and evaluator |

### Key Features by Parser

#### C# Parser
- Full language support: classes, structs, interfaces, enums, delegates, records
- Generics with constraints
- Modern features: pattern matching, nullable reference types, records, primary constructors
- LINQ, lambda expressions, async/await
- Complete AST with code writer

#### Python Parser
- Python 3.6-3.12 syntax
- Pattern matching (match/case)
- Type hints and annotations
- F-strings, walrus operator (`:=`)
- Async/await, comprehensions
- Complete AST with code writer

#### Java Parser
- Classes, interfaces, enums, annotations
- Generics and wildcards
- Lambda expressions
- Complete AST with code writer

## 📚 Documentation

- **[API Differences](docs/fluent-api-differences.md)** - Paspan vs Parlot differences
- **[Migration Guide](docs/parlot-to-paspan-migration-guide.md)** - Porting from Parlot
- **[Parser List](docs/parsers.md)** - Available parser combinators
- **[Writing Parsers](docs/writing.md)** - Best practices
- **[Project Structure](docs/project-structure.md)** - Repository organization

## 🎯 Quick Start

### Using Existing Parsers

```csharp
using PaspanParsers.CSharp;

var code = "public class Hello { }";
var ast = CSharpParser.Parse(code);

// Access parsed elements
if (ast != null && ast.Members[0] is ClassDeclaration cls)
{
    Console.WriteLine($"Class: {cls.Name}");
}
```

### Building Your Own Parser

```csharp
using static Paspan.Fluent.Parsers;

// Simple calculator parser
var number = Terms.Integer();
var add = Terms.Char('+');
var expression = number.And(add).And(number)
    .Then(x => x.Item1 + x.Item3);

var result = expression.Parse("10 + 20"); // 30
```

## 🧪 Testing

All parsers include comprehensive test suites:
- Unit tests in `src/PaspanParsers.Tests`
- 100+ tests for C# parser
- 50+ tests for Python parser
- Real-world code examples

Run tests:
```bash
dotnet test src/PaspanParsers.Tests
```

## 🏗️ Project Structure

```
PaspanParsers/
├── src/
│   ├── Paspan/              # Core library
│   ├── PaspanCommon/        # Shared parser combinators
│   ├── PaspanParsers/       # Language parsers
│   │   ├── CSharp/          # C# parser + AST + code writer
│   │   ├── Python/          # Python parser + AST + code writer
│   │   ├── Java/            # Java parser + AST + code writer
│   │   ├── Json/            # JSON parser
│   │   ├── Sql/             # SQL parser (WIP)
│   │   └── Calc/            # Expression evaluator
│   └── PaspanParsers.Tests/ # Test suite
└── docs/                    # Documentation
```

## 🔧 Requirements

- .NET 10.0+
- C# 12.0+

## 📄 License

BSD 3-Clause License (same as Parlot)

## 🙏 Credits

- **Parlot** by [Sébastien Ros](https://github.com/sebastienros) - Original parser combinator library
- Parsers implemented with AI assistance

## 🔗 Links

- [Parlot](https://github.com/sebastienros/parlot) - Original project
- [Documentation](docs/) - Full documentation
