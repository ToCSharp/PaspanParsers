using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using PaspanParsers.CSharp;

namespace PaspanParsers.Benchmarks;

/// <summary>
/// The cost of one declaration or statement: a class with <see cref="Count"/> copies of a member, or a method
/// with <see cref="Count"/> copies of a statement. The time is per copy, so the two parsers can be compared
/// construct by construct; the corpus benchmarks (<see cref="CSharpParserBenchmarks"/>) only give the sum.
/// </summary>
/// <remarks>
/// <c>dotnet run -c Release --project src/PaspanParsers.Benchmarks -- --filter "*CSharpConstructBenchmarks*"</c>
/// </remarks>
[Config(typeof(Config))]
public class CSharpConstructBenchmarks
{
    public const int Count = 20000;

    private static readonly Dictionary<string, Func<int, string>> s_members = new()
    {
        ["field"] = i => $"private int f{i};",
        ["field with initializer"] = i => $"private static readonly int f{i} = {i};",
        ["auto property"] = i => $"public int P{i} {{ get; set; }}",
        ["empty method"] = i => $"public void M{i}() {{ }}",
        ["method with 2 parameters"] = i => $"public void M{i}(int a, string b) {{ }}",
        ["method with an attribute"] = i => $"[Obsolete] public void M{i}() {{ }}",
        ["expression-bodied method"] = i => $"public int M{i}() => {i};",
        ["nested class"] = i => $"private sealed class C{i} {{ }}",
    };

    private static readonly Dictionary<string, Func<int, string>> s_statements = new()
    {
        ["call statement"] = _ => "a.b(c);",
        ["var local"] = i => $"var x{i} = y;",
        ["typed local"] = i => $"int x{i} = y;",
        ["if statement"] = _ => "if (a) b();",
        ["return"] = _ => "return;",
        ["empty block"] = _ => "{ }",
    };

    private string _source;

    [ParamsSource(nameof(Constructs))]
    public string Construct { get; set; }

    public static IEnumerable<string> Constructs => s_members.Keys.Concat(s_statements.Keys);

    [GlobalSetup]
    public void Setup()
    {
        var builder = new StringBuilder();
        if (s_members.TryGetValue(Construct, out var member))
        {
            builder.Append("class C\n{\n");
            for (var i = 0; i < Count; i++)
            {
                builder.Append("    ").Append(member(i)).Append('\n');
            }

            builder.Append("}\n");
        }
        else
        {
            builder.Append("class C\n{\n    void M()\n    {\n");
            for (var i = 0; i < Count; i++)
            {
                builder.Append("        ").Append(s_statements[Construct](i)).Append('\n');
            }

            builder.Append("    }\n}\n");
        }

        _source = builder.ToString();
        if (CSharpParser.Parse(_source) == null || CSharpHybridParser.Parse(_source) == null)
        {
            throw new InvalidOperationException($"A parser rejects the input of '{Construct}'.");
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = Count)]
    public CompilationUnit RecursiveDescent() => CSharpParser.Parse(_source);

    [Benchmark(OperationsPerInvoke = Count)]
    public CompilationUnit Hybrid() => CSharpHybridParser.Parse(_source);

    private sealed class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.Default.WithWarmupCount(3).WithIterationCount(15));
            AddDiagnoser(MemoryDiagnoser.Default);
            AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByParams);
        }
    }
}
