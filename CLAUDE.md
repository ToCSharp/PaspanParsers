# PaspanParsers

## Build and test

- Build: `dotnet build PaspanParsers.slnx`
- Run tests: `dotnet run --project src/PaspanParsers.Tests`

Do not use `dotnet test`: the test project uses Microsoft.Testing.Platform, and the VSTest-based `dotnet test` is rejected by the .NET 10 SDK.

## C# parser oracle

- `src/PaspanParsers.Tests/CSharp/CSharpCorpusTests.cs` checks valid C# files against Roslyn: parse, write back with `CSharpWriter`, compare with Roslyn's tree, and check that every node's `Span` matches a Roslyn node or token (`SpanChecker.cs`).
- Files listed in `src/PaspanParsers.Tests/CSharp/Corpus/oracle-baseline.txt` must keep passing. After improving the parser, record newly passing files with `UPDATE_ORACLE_BASELINE=1 dotnet run --project src/PaspanParsers.Tests -- --filter "FullyQualifiedName~Oracle_BuiltInCorpus"`.
- `Oracle_BuiltInCorpus_Statements` checks every statement of every method body in the corpus on its own, wrapped in a method; all must pass.
- Every node the parser creates must get its span: build it with `SyntaxParser.Finish(node, start)`.
- The per-file report is written to `src/PaspanParsers.Tests/bin/Debug/net10.0/oracle-report.txt`. Set `CSHARP_CORPUS_DIR` to also measure an external corpus (`--filter "FullyQualifiedName~Oracle_ExternalCorpus"`, report in `oracle-external-report.txt`), and `CSHARP_CORPUS_SYMBOLS` (for example `NET;DEBUG`) to parse it with preprocessor symbols. With `CSHARP_CORPUS_DIR` set, `dotnet run -c Release --project src/PaspanParsers.Tests -- --filter "FullyQualifiedName~Benchmark_Corpus"` compares speed and allocations with Roslyn.

## C++ parser oracle

- The C++ parser (`src/PaspanParsers/Cpp`) is being built by stages following `docs/cpp-parser-clang-level-plan.md`; record the status of each stage there.
- `src/PaspanParsers.Tests/Cpp/CppCorpusTests.cs` checks valid C++ files against clang (`clang++`, or `CLANG_PATH`): parse, write back with `CppWriter`, compare clang's JSON AST of both (`ClangAst.cs`), and check every node's span and kind against clang's nodes (`CppSpanChecker.cs`, `CppKindMap.cs`). Without clang these tests are inconclusive.
- Every new node type needs a rule in `CppKindMap.cs`, and every node gets its span from `SyntaxParser.Finish(node, start)`.
- Files listed in `src/PaspanParsers.Tests/Cpp/Corpus/oracle-baseline.txt` must keep passing; record newly passing files with the same `UPDATE_ORACLE_BASELINE=1` command as for C# (the filter `Oracle_BuiltInCorpus` covers both). The report is `cpp-oracle-report.txt` next to `oracle-report.txt`.
- Set `CPP_CORPUS_DIR` to also measure an external corpus (`--filter "FullyQualifiedName~Cpp.CppCorpusTests.Oracle_ExternalCorpus"`, report in `cpp-oracle-external-report.txt`), with `CPP_CORPUS_DEFINES` (for example `NDEBUG;VERSION=3`) and `CPP_CORPUS_INCLUDE` (include directories for clang, separated by the path separator).
