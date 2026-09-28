# Paspan Project Structure

## Project Overview

**Paspan** is a fast, lightweight parser combinator library for .NET, a fork of the [Parlot](https://github.com/sebastienros/parlot) project. Unlike Parlot, Paspan is based on `Span<T>` and `ReadOnlySequence<T>`, making it optimal for parsing large UTF-8 or binary files.

**Key Features:**
- Works with Span/ReadOnlySequence for high performance
- Supports fluent API for readable grammars
- Optimized for UTF-8 parsing (byte-level search without string conversion)
- SpanReader based on Utf8JsonReader from .NET

## Solution Structure

```
PaspanParsers.slnx
├── src/
│   ├── Paspan/                # Main library project (SpanReader)
│   ├── PaspanCommon/          # Shared project: parser combinators and fluent API
│   ├── PaspanParsers/         # Language parsers
│   └── PaspanParsers.Tests/   # Tests of the library and the parsers, with the Roslyn and clang oracles
│
└── docs/                      # Documentation and the plans of the C# and C++ parsers
```

## Detailed Directory Structure

### 📁 src/Paspan/
**Main library project** - contains public API for working with Span-based reader.

**Key Files:**
- `SpanReader.cs` - main reader for working with Span/ReadOnlySequence
- `SpanReader.TryGet.cs` - TryGet* methods for reading data
- `Region.cs` - representation of region in parsed data
- `Paspan.csproj` - project file (multi-target: net6.0, net8.0)

### 📁 src/PaspanCommon/
**Shared project** - contains main parsing logic used by different target platforms.

#### Substructure:

**Common/** - common utilities and constants
- `Character.cs` - character operations (checking character types)
- `Constants.cs` - library constants
- `HexConverter.cs` - hex value conversion
- `Tuples.cs` / `Tuples.tt` - tuple generation (T4 template)

**Fluent/** - Fluent API for building parsers
- `Parser.cs` - base parser class
- `Parser.TryParse.cs` - TryParse methods
- `ParseContext.cs` - parsing context
- `Parsers.cs` - main parser combinators
- `Parsers.And.cs` / `Parsers.And.tt` - And combinator (parser chain)
- `Parsers.OneOf.cs` - OneOf combinator (alternatives)
- `Parsers.Read.cs` - read operations
- `Parsers.Skip.cs` - skip operations
- `Parsers.SkipAnd.cs` - Skip and And combination
- `Parsers.Values.cs` / `Parsers.Values.tt` - value operations

**Fluent/Literals/** - literal parsers
- `CharLiteral.cs` - character parsing
- `TextLiteral.cs` - text parsing
- `StringLiteral.cs` - string parsing
- `IntegerLiteral.cs` / `Integer64Literal.cs` - integer parsing
- `DecimalLiteral.cs` - decimal parsing
- `Identifier.cs` - identifier parsing
- `PatternLiteral.cs` - pattern parsing
- `WhiteSpaceLiteral.cs` / `NonWhiteSpaceLiteral.cs` - whitespace
- `NumberOptions.cs` - number parsing options
- `StringRegion.cs` - string region

**Fluent/Combinators** (directly in Fluent/)
- `Between.cs` - parse between two delimiters
- `BytesBefore.cs` / `TextBefore.cs` / `ReadBefore.cs` / `RegionBefore.cs` - parse until delimiter
- `TextBeforeEof.cs` - parse until end of file
- `Capture.cs` - capture result
- `Deferred.cs` - deferred parser definition (for recursion)
- `Discard.cs` - discard result
- `Empty.cs` - empty parser
- `Eof.cs` - end of file
- `Error.cs` - error handling
- `Labelled.cs` - named parser
- `Not.cs` - negation
- `OneOf.cs` / `OneOf.ABT.cs` - choice from alternatives
- `OneOrMany.cs` / `ZeroOrMany.cs` / `ZeroOrOne.cs` - quantifiers
- `Separated.cs` - separated elements
- `Sequence.cs` / `SequenceAndSkip.cs` / `SequenceSkipAnd.cs` - sequences
- `SkipWhiteSpace.cs` - skip whitespace
- `Switch.cs` - switch
- `Then.cs` - result transformation
- `Unit.cs` - unit value
- `Values.cs` - value operations
- `When.cs` - conditional parser

**Root PaspanCommon/**
- `ParseError.cs` - parsing error
- `ParseException.cs` - parsing exception
- `ParseResult.cs` - parsing result
- `SpanReaderHelpers.cs` - SpanReader helper methods
- `PaspanCommon.shproj` - shared project file
- `PaspanCommon.projitems` - shared project items

### 📁 src/PaspanParsers/
**Language parsers** (`net10.0`), each in its own folder and namespace:

| Folder | Parser |
|---|---|
| `Common/` | Shared by the C# and C++ parsers: `TextSpan`, `LineMap` (lines and columns of offsets), `Utf8Source`, `LargeStack` |
| `CSharp/` | C# 1–14 parser for valid code, checked against Roslyn: hand-written `SyntaxParser`, AST with positions, `CSharpWriter`, documentation comments, error recovery ([README](../src/PaspanParsers/CSharp/README.md)) |
| `Cpp/` | C++23 parser for valid code, checked against clang: hand-written `SyntaxParser`, preprocessor as trivia, symbol table, AST with positions, `CppWriter`, Doxygen documentation comments ([README](../src/PaspanParsers/Cpp/README.md)) |
| `Python/`, `Java/` | Parser combinator grammars with ASTs and code writers |
| `Json/`, `Calc/` | JSON parser, expression parser and evaluator |
| `Sql/`, `SQL2/` | SQL parsers (work in progress) |

### 📁 src/PaspanParsers.Tests/
**Tests** (MSTest on Microsoft.Testing.Platform: run with `dotnet run --project src/PaspanParsers.Tests`, not `dotnet test`)

- `SpanReaderTests.cs`, `FluentTests.cs`, `CoreRegressionTests.cs` - the library
- `CSharp/` - unit tests of the C# parser and the Roslyn oracle (`RoslynOracle.cs`, `SpanChecker.cs`, `CSharpCorpusTests.cs`, corpus in `CSharp/Corpus`)
- `Cpp/` - unit tests of the C++ parser and the clang oracle: `Clang.cs` runs `clang++`, `ClangAst.cs` reads its JSON AST, `ClangOracle.cs` compares trees, `CppSpanChecker.cs` and `CppKindMap.cs` check spans and kinds, `CppNodeText.cs` the tokens of spans, `CppLiteralChecker.cs` literal values, `CppDocumentationChecker.cs` documentation comments; `CppCorpusTests.cs` runs the corpus in `Cpp/Corpus` and external corpora (`fetch-external-corpora.sh`)
- `Python/`, `Java/`, `Json/`, `Calc/`, `SQL2/` - the other parsers

### 📁 docs/
**Project documentation**

- `parsers.md` (714 lines) - detailed description of all parsers with usage examples
- `writing.md` (65 lines) - best practices for writing custom parsers
- `integration-tests-plan.md` (445 lines) - integration testing plan
- `csharp-parser-roslyn-level-plan.md` - the stages of the C# parser, checked against Roslyn
- `cpp-parser-clang-level-plan.md` - the stages of the C++ parser, checked against clang

### 📄 Root Files

- `README.md` - main project documentation with examples and benchmarks
- `LICENSE` - license (BSD 3-Clause, same as Parlot)
- `PaspanParsers.slnx` - solution file
- `CLAUDE.md` - build and test commands, the Roslyn and clang oracles

## Technology Stack

- **.NET:** net10.0
- **Testing:** MSTest on Microsoft.Testing.Platform; Roslyn and clang (`clang++`) as oracles for the C# and C++ parsers
- **T4 Templates:** for code generation (Tuples.tt, Parsers.And.tt, Parsers.Values.tt)

## Codebase Patterns

### Shared Project Pattern
`PaspanCommon` is a shared project (.shproj) compiled into each target project. This allows using same code for different .NET versions without duplication.

### Partial Classes
Many classes are split into partials:
- `SpanReader.cs` + `SpanReader.TryGet.cs`
- `Parser.cs` + `Parser.TryParse.cs`
- `Parsers.cs` + `Parsers.And.cs` + `Parsers.OneOf.cs` + etc.

### T4 Code Generation
T4 templates used for generating repetitive code (tuples, And combinators with different parameter counts).

## Key Concepts

### SpanReader
Central class for reading data from `Span<byte>` or `ReadOnlySequence<byte>`. Rewritten version of `Utf8JsonReader`.

### Parser<T>
Base generic parser class returning value of type `T`.

### Fluent API
Fluent interface for building parser combinators:
```csharp
Parser<T> = Literal.And(Other).Skip(WhiteSpace).Then(Transform);
```

### ParseResult<T>
Parsing result - either success with value `T`, or error with `ParseError`.

### Region
Representation of section (region) in source data, used for zero-copy parsing.

## Main Entry Points for AI

1. **For API understanding:** `src/PaspanCommon/Fluent/Parser.cs`, `src/PaspanCommon/Fluent/Parsers.cs`
2. **For usage examples:** `src/PaspanParsers/Calc/`, `src/PaspanParsers/Json/`
3. **For the C# and C++ parsers:** `src/PaspanParsers/CSharp/README.md`, `src/PaspanParsers/Cpp/README.md` and their plans in `docs/`
4. **For documentation:** `docs/parsers.md`, `README.md`
5. **For testing:** `CLAUDE.md`, `src/PaspanParsers.Tests/`

## Implementation Features

### Differences from Parlot
- Based on `Span<T>` and `ReadOnlySequence<T>` instead of strings
- Works directly with UTF-8 bytes
- Removed `Compile()` function (planned replacement with Source Generators)
- API changes due to Span work

### Known Issues (TODO)
- Performance issues when creating `Region` (see WideJson_PaspanUtf8Region benchmarks)
- Overall performance can be improved
- Planned: "Parser Generator from Parser Combinator with Source Generators"

## Project Goals

Paspan created to provide:
1. **High performance** - faster than Pidgin, Sprache, comparable with Parlot
2. **Efficient memory usage** - minimal allocations thanks to Span
3. **Ease of use** - readable Fluent API
4. **Large file support** - optimization for parsing huge UTF-8/binary files
