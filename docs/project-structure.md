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
Paspan.sln
├── src/                          # Library source code
│   ├── Paspan/                   # Main library project
│   └── PaspanCommon/             # Shared project
│
├── test/                         # Tests and benchmarks
│   ├── Paspan.Tests/             # Unit tests
│   ├── Paspan.Benchmarks/        # Performance benchmarks
│   └── Paspan.IntegrationTests/  # Integration tests
│
└── docs/                         # Documentation
    ├── parsers.md                # Parser descriptions and examples
    ├── writing.md                # Best practices for writing parsers
    └── integration-tests-plan.md # Integration test plan
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

### 📁 test/Paspan.Tests/
**Library unit tests**

**Key Test Suites:**
- `SpanReaderTests.cs` - SpanReader tests
- `FluentTests.cs` - Fluent API tests

**Calc/** - mathematical expression parser tests
- `Expression.cs` - mathematical expression AST
- `FluentParser.cs` - Fluent API expression parser
- `FluentParserTests.cs` - Fluent parser tests
- `Parser.cs` - manual expression parser
- `ParserTest.cs` - manual parser tests
- `Interpreter.cs` - expression interpreter
- `InterpreterTest.cs` - interpreter tests
- `CalcTests.cs` - general calculator tests

**Json/** - JSON parser tests
- `JsonModel.cs` - JSON data model
- `JsonParser.cs` - JSON parser (strings)
- `JsonParserRegion.cs` - JSON parser (regions)
- `JsonParserTests.cs` - JSON parser tests

### 📁 test/Paspan.Benchmarks/
**Performance benchmarks** - comparison with Parlot, Pidgin, Sprache, and System.Text.Json

**Main Benchmarks:**
- `ExprBench.cs` - mathematical expression parsing benchmark
- `JsonBench.cs` - JSON parsing benchmark
- `RegexBenchmarks.cs` - regex comparison benchmark
- `Program.cs` - benchmark entry point

**Parlot/** - Parlot implementations for comparison
- `FluentParser.cs` - Fluent parser
- `JsonParser.cs` - JSON parser
- `ParlotParser.cs` - base parser

**PidginParsers/** - Pidgin implementations for comparison
- `ExpressionParser.cs` - expression parser
- `PidginJsonParser.cs` - JSON parser

**SpracheParsers/** - Sprache implementations for comparison
- `SpracheJsonParser.cs` - JSON parser

### 📁 test/Paspan.IntegrationTests/
**Integration tests** - end-to-end testing of real scenarios

**EndToEnd/** - end-to-end tests
- `ExpressionEvaluationTests.cs` - expression evaluation tests
- `JsonParsingTests.cs` - JSON parsing tests

**ErrorHandling/** - error handling tests
- `ErrorHandlingTests.cs` - error scenario tests (300 lines)

**Infrastructure/** - test infrastructure
- `TestDataLoader.cs` - test data loading
- `JsonAssert.cs` - JSON assertions
- `PerformanceMonitor.cs` - performance monitoring

**TestData/** - test data
- `expressions/` - mathematical expression files (*.txt)
- `json/` - JSON data files (*.json)

**Project Files:**
- `Paspan.IntegrationTests.csproj` - project file
- `README.md` - integration tests description

### 📁 docs/
**Project documentation**

- `parsers.md` (714 lines) - detailed description of all parsers with usage examples
- `writing.md` (65 lines) - best practices for writing custom parsers
- `integration-tests-plan.md` (445 lines) - integration testing plan

### 📄 Root Files

- `README.md` - main project documentation with examples and benchmarks
- `LICENSE` - license (BSD 3-Clause, same as Parlot)
- `Paspan.sln` - Visual Studio solution file

## Technology Stack

- **.NET Multi-targeting:** net6.0, net8.0
- **Testing:** xUnit (presumably, based on test project structure)
- **Benchmarks:** BenchmarkDotNet
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
2. **For usage examples:** `test/Paspan.Tests/Calc/FluentParser.cs`, `test/Paspan.Tests/Json/JsonParser.cs`
3. **For performance:** `test/Paspan.Benchmarks/`
4. **For documentation:** `docs/parsers.md`, `README.md`
5. **For testing:** `test/Paspan.IntegrationTests/ErrorHandling/ErrorHandlingTests.cs`

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
