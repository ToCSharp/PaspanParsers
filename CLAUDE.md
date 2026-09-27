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
