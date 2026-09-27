using BenchmarkDotNet.Running;

// dotnet run -c Release --project src/PaspanParsers.Benchmarks -- --filter "*"
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
