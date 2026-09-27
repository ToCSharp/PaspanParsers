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
| **C#** | ✅ Complete | C# 1–14 parser for valid code, checked against Roslyn; AST with positions and code writer |
| **Python** | ✅ Complete | Python 3.6-3.12 parser with pattern matching, async/await, type hints |
| **Java** | ✅ Complete | Java parser with AST and code generator |
| **JSON** | ✅ Complete | Fast JSON parser with Region-based zero-copy support |
| **SQL** | 🚧 WIP | SQL parser with CTEs, JOINs, window functions |
| **Calc** | ✅ Complete | Mathematical expression parser and evaluator |

### Key Features by Parser

#### C# Parser
- C# 1–14: every file Roslyn parses without syntax errors is expected to parse to an equivalent tree
- Checked against Roslyn on its own compiler sources, dotnet/runtime and ASP.NET Core libraries (6 600+ files, 100%)
- Preprocessor (`#if` with symbols from `CSharpParseOptions`), `#nullable` kept in the AST
- Node positions (`Span`), as fast as Roslyn's parser, linear time on deeply nested code
- Code writer that prints the AST back; see [src/PaspanParsers/CSharp/README.md](src/PaspanParsers/CSharp/README.md)

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
var options = new CSharpParseOptions(CSharpLanguageVersion.CSharp14, preprocessorSymbols: ["DEBUG"]);

// Access parsed elements
if (CSharpParser.TryParse(code, options, out var ast, out var error) && ast.Members[0] is ClassDeclaration cls)
{
    Console.WriteLine($"Class: {cls.Name} at {cls.Span}");
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
- 400+ tests for the C# parser, plus a Roslyn oracle over a corpus of C# files
- 50+ tests for Python parser
- Real-world code examples

Run tests (the test project uses Microsoft.Testing.Platform, so use `dotnet run` rather than `dotnet test`):
```bash
dotnet run --project src/PaspanParsers.Tests
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
