# C# Parser

`CSharpParser` parses C# source code (C# 1–14) into a semantic AST (`CSharpAst.cs`), and `CSharpWriter`
prints an AST back as C#. Every node knows its position in the input.

**Scope:** valid code. Any file that Roslyn parses without syntax errors is expected to parse, and the
tree is expected to match Roslyn's. By default invalid input makes `TryParse` return `false` with a
`ParseError` at the token where parsing stopped (`Unexpected ';'`, with line and column). With
`CSharpParseOptions.ErrorRecovery` invalid members and statements are skipped instead, and the rest of
the file still parses (see [Error recovery](#error-recovery)).

## How it is verified

The parser is checked against Roslyn (`src/PaspanParsers.Tests/CSharp/RoslynOracle.cs`). For each valid file:

1. `CSharpParser.TryParse` must succeed.
2. `CSharpWriter` prints the AST, Roslyn parses the printed code, and the result must be equivalent to
   Roslyn's tree of the original file (trivia ignored). The writer prints the tree literally, keeping
   parentheses, trailing commas and modifier order, so this compares the structure of the whole tree.
3. The span of every node must be the span of a Roslyn node or token (`SpanChecker.cs`), apart from the
   few places where the AST has nodes Roslyn does not.

| Corpus | Files | Result |
|---|---|---|
| Built-in: `src/**` of this repository and `CSharp/Corpus/*.cs` (one file per feature area) | 182 | 182 pass |
| Every statement of every method body of the built-in corpus, checked on its own | 8 587 | 8 587 pass |
| Roslyn compiler sources (`src/Compilers/{CSharp,Core}/Portable`, `src/Workspaces/CSharp/Portable`) | 2 176 | 2 176 pass |
| dotnet/runtime libraries (`System.Private.CoreLib`, `System.Linq`, `System.Collections`, `System.Text.Json`, `System.Net.Http`, …) | 2 488 | 2 488 pass (also with `NET;NETCOREAPP;DEBUG;TARGET_64BIT;TARGET_WINDOWS`) |
| ASP.NET Core (`src/Http`, `Mvc.Core`, `Kestrel/Core`, `Components`, `SignalR/server/Core`) | 2 015 | 2 015 pass |

Files Roslyn rejects (for example an `#error` in an active branch) are skipped.

## Usage

```csharp
using PaspanParsers.CSharp;

var code = """
    #if DEBUG
    using System.Diagnostics;
    #endif

    namespace Demo;

    public record Point(int X, int Y)
    {
        public double Length => Math.Sqrt(X * X + Y * Y);
    }
    """;

// Preprocessor symbols decide which #if branches are parsed
var options = new CSharpParseOptions(CSharpLanguageVersion.CSharp14, preprocessorSymbols: ["DEBUG"]);

if (CSharpParser.TryParse(code, options, out var unit, out var error))
{
    var ns = (NamespaceDeclaration)unit.Members[0];
    var point = (RecordDeclaration)ns.Members[0];
    Console.WriteLine(point.Name);                               // Point
    Console.WriteLine(point.PrimaryConstructorParameters.Count); // 2

    // Print the tree back as C#
    var writer = new CSharpWriter();
    writer.WriteCompilationUnit(unit);
    Console.WriteLine(writer.GetResult());
}
else
{
    Console.WriteLine($"({error.Line},{error.Column}): {error.Message}");
}

// Without options: C# 14, no preprocessor symbols; null when the input does not parse
var simple = CSharpParser.Parse("class C { }");
```

`CSharpParseOptions.LanguageVersion` is informational: the parser accepts the syntax of all versions up to C# 14.

### Positions

Every node implements `ICSharpNode.Span`: a `TextSpan` from the first token of the node to the end of its
last token, without the whitespace and comments around them (like `SyntaxNode.Span` in Roslyn). The compilation
unit spans the whole input, and a `#nullable` directive spans its line.

Offsets are in **UTF-8 bytes** of the input without its byte order mark: the parser reads UTF-8.
`CSharpParser.GetUtf8Source(string)` returns these bytes, and `TextSpan.GetText` returns the text of a span.

```csharp
var source = "class C { int M() => a + b * c; }";
var unit = CSharpParser.Parse(source);
var method = (MethodDeclaration)((ClassDeclaration)unit.Members[0]).Members[0];
var sum = ((ExpressionMethodBody)method.Body).Expression;

Console.WriteLine(sum.Span);                   // [21..30)
Console.WriteLine(sum.Span.GetText(source));   // a + b * c

// For many spans of one input, encode it once
var utf8 = CSharpParser.GetUtf8Source(source);
Console.WriteLine(method.Span.GetText(utf8));  // int M() => a + b * c;
```

Nodes built in code have an empty span at 0; `Span` has a setter for tools that build trees.

### UTF-8 input, lines and columns

Files can be parsed from their bytes, without decoding them to a string. A leading byte order mark is
skipped, and spans are offsets into the bytes after it (`CSharpParser.GetUtf8Source(ReadOnlyMemory<byte>)`).
`LineMap` converts offsets to 1-based lines and columns and back; columns count UTF-16 code units, like
editors, Roslyn and the Language Server Protocol.

```csharp
var bytes = File.ReadAllBytes("Program.cs");
if (CSharpParser.TryParse(bytes, options, out var unit, out var error))
{
    var utf8 = CSharpParser.GetUtf8Source(bytes);
    var lines = new LineMap(utf8.Span);
    var (line, column) = lines.GetLineAndColumn(unit.Members[0].Span.Start);
    var offset = lines.GetOffset(line, column);    // back to the byte offset
}
```

### Documentation comments

`MemberDeclaration.LeadingTrivia` and `EnumMember.LeadingTrivia` are the whitespace, comments and directives
before a declaration. `DocumentationComment.GetXml` reads the `///` and `/** */` comments in them and returns
their XML without the comment markers, or null:

```csharp
var xml = DocumentationComment.GetXml(utf8.Span, method);   // "<summary>\nDoes it.\n</summary>"
```

It is checked against Roslyn's documentation comment trivia on the built-in corpus.

### Error recovery

For code that is being edited, `new CSharpParseOptions(errorRecovery: true)` makes the parser return a tree
for any input:

- A member declaration or statement that does not parse is skipped up to and including the next `;` or
  balanced `{...}` block, or up to the `}` that closes the enclosing body, the next declaration keyword
  (`class`, `public`, …) or, for a statement, the next statement keyword (`if`, `return`, …). It is kept as an
  `IncompleteMemberDeclaration` or `IncompleteStatement` with the skipped text.
- Recovery happens in the innermost body: a broken statement costs that statement, not the method, and a
  broken member costs that member, not the type.
- A type, namespace or block left open at the end of the input is closed there.
- `CompilationUnit.Errors` lists the errors (`SyntaxError`: the unexpected token and a message), or is null.

```csharp
var options = new CSharpParseOptions(errorRecovery: true);
CSharpParser.TryParse("class C { void A() { a(); b(; c(); } void B() { } }", options, out var unit, out _);
// A has three statements: a(); an IncompleteStatement "b(;" and c(); B parses normally
Console.WriteLine(unit.Errors[0].Message);   // Unexpected ';'
```

Valid input parses to the same tree with and without recovery: `ErrorRecoveryTests` checks this on the
built-in corpus (and on `CSHARP_CORPUS_DIR` when set), and parses thousands of damaged copies of the corpus
files (truncated, with characters removed or inserted), which must always succeed.

## What is supported

| Area | Support |
|---|---|
| Lexical | Unicode identifiers and escapes (`A`), `@` identifiers, contextual keywords as identifiers (`var select = 1;`), all numeric literal forms (hex, binary, `_`, suffixes), character escapes including `\e`, verbatim, raw (`"""`), interpolated (`$"…"`, `$@"…"`, `$$"""…"""`) and UTF-8 (`"…"u8`) strings |
| Preprocessor | `#if`/`#elif`/`#else`/`#endif` with expressions, `#define`/`#undef`, disabled text; `#nullable` is kept in the AST (it changes the meaning of the code), other directives (`#region`, `#pragma`, `#line`, `#error`, `#warning`, `#!`, `#:`) are skipped |
| Compilation unit | `extern alias`, `global using`, `using static`, `using unsafe`, aliases to any type, global attributes, block and file-scoped namespaces, top-level statements |
| Types | classes, structs (`ref`, `readonly`), interfaces, enums, delegates, records (`record class`, `record struct`), primary constructors, nested types, variance, all constraints including `allows ref struct` |
| Members | fields (`fixed` buffers, `ref` fields), constants, methods, properties (accessors with modifiers and bodies, initializers, `field`), indexers, events, constructors with initializers, destructors, operators (`checked`, `>>>`, C# 14 compound assignment and `++`), conversion operators, explicit interface implementations, C# 14 extension blocks, partial members |
| Statements | all statements: local declarations (`const`, `ref`, `scoped`, `using`, `await using`), local functions, `if`, `switch`, loops including `await foreach` and deconstruction, `try`/`catch`/`finally` with filters, `goto case`, `yield`, `checked`, `unsafe`, `fixed`, `lock` |
| Expressions | all operators with Roslyn's precedence, `?.`/`?[]`, null-conditional assignment, `!`, ranges, `switch` and `with` expressions, object, collection and array creation with every initializer form, collection expressions with spreads, `stackalloc`, lambdas (attributes, `static`, `async`, explicit return types, default parameter values), anonymous methods, LINQ queries with `into` continuations, tuples and deconstruction, declaration expressions, `typeof`/`sizeof`/`nameof`/`default`, `__arglist` and friends |
| Patterns | constant, type, declaration, `var`, discard, positional, property (extended `A.B:`), list and slice, relational, parenthesized, `not`/`and`/`or` |
| Types in code | predefined, generic, qualified (`A<B>.C<D>`), alias-qualified, nullable, arrays, pointers, function pointers, tuples, `ref`/`ref readonly`, `scoped` |

**Limitations**

- Error recovery is optional and coarse: it skips whole members and statements, where Roslyn inserts
  missing tokens and keeps partial nodes. It is meant to keep the rest of a file usable, not to describe
  the invalid code.
- Trivia is not kept: comments, whitespace and directives other than `#nullable` are not in the AST, so
  `CSharpWriter` output is formatted by the writer, not like the input.
- Preview features after C# 14 are not a target (only the `safe` modifier is parsed).

## Performance

Measured with `CSharpPerformanceTests.Benchmark_Corpus` (Release build, one thread, best of three runs,
4-core Xeon 2.8 GHz). Roslyn is `CSharpSyntaxTree.ParseText(...).GetRoot()` on the same files.

| Corpus | Size | PaspanParsers | Roslyn |
|---|---|---|---|
| Roslyn compiler sources | 34.0 MB | 23.8 MB/s, 14.3 bytes allocated per source byte | 20.1 MB/s, 4.8 bytes per byte |
| dotnet/runtime libraries | 37.2 MB | 31.6 MB/s, 10.9 bytes per byte | 16.9 MB/s, 7.3 bytes per byte |
| ASP.NET Core | 14.9 MB | 18.6 MB/s, 19.9 bytes per byte | 19.5 MB/s, 6.8 bytes per byte |

Parsing time is linear in the input, also for deeply nested code: 8 000 nested parentheses parse in about
40 ms, a chain of 50 000 `a + a + …` in about 35 ms. Input nested deeper than the caller's stack allows
(about 500 levels of nested blocks and lambdas, or 600 nested parentheses, on a 1 MB stack) is parsed again on a thread with a
256 MB stack instead of overflowing the stack; `CSharpWriter` does the same for deep trees.

## Files

| File | Contents |
|---|---|
| `CSharpParser.cs` | Entry points: `Parse`, `TryParse`, `GetUtf8Source`, `CompilationUnitParser` |
| `CSharpParseOptions.cs`, `CSharpParseContext.cs` | Options (language version, preprocessor symbols, error recovery) and per-parse state |
| `CSharpAst.cs` | AST nodes, `TextSpan` |
| `CSharpWriter.cs` | Prints an AST as C# (see `CSharpWriter.README.md`) |
| `LineMap.cs`, `DocumentationComment.cs` | Lines and columns of offsets; documentation comments of declarations |
| `Parser/Lexer.cs`, `Parser/Tokens.cs` | Tokens: identifiers, keywords, literals, interpolated strings |
| `Parser/Preprocessor.cs` | Trivia: whitespace, comments and preprocessor directives |
| `Parser/SyntaxParser*.cs` | Hand-written recursive descent parser: types, expressions, patterns, statements, declarations, compilation unit, error recovery (`SyntaxParser.Recovery.cs`) |
| `CSharpGrammarSpecification.txt` | The C# grammar in EBNF, for reference |

The parser is a hand-written recursive descent parser (`SyntaxParser`) that follows Roslyn's disambiguation
rules (generic names, casts, lambdas, declarations versus expressions); it runs as a Paspan parser through
`CSharpParser.CompilationUnitParser`. Tokens are scanned lazily and cached by position. The plan and the
history of the work are in `docs/csharp-parser-roslyn-level-plan.md`.

## Tests

```bash
dotnet run --project src/PaspanParsers.Tests -- --filter "FullyQualifiedName~PaspanParsers.Tests.CSharp"
```

- `LexicalTests`, `TypeTests`, `ExpressionTests`, `PatternTests`, `StatementTests`, `DeclarationTests`,
  `PreprocessorTests`, `SpanTests`: unit tests by area, most also checked against Roslyn.
- `CSharpCorpusTests`: the oracle on the built-in corpus. Set `CSHARP_CORPUS_DIR` to a directory of `.cs` files
  to measure an external corpus (and `CSHARP_CORPUS_SYMBOLS`, for example `NET;DEBUG`, for preprocessor symbols).
- `CSharpPerformanceTests`: deep nesting, and a benchmark against Roslyn when `CSHARP_CORPUS_DIR` is set
  (run it with `-c Release`).
- `ErrorRecoveryTests`: recovery from invalid input; valid corpus files parse the same with recovery; damaged
  corpus files always parse.
- `SourceTextTests`: UTF-8 input, `LineMap` and `DocumentationComment`, checked against Roslyn on the corpus.
- `CSharpParserTests`, `CSharpWriterTests`: the original tests of the parser and the writer.
