using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using PaspanParsers.CSharp;

namespace PaspanParsers.Tests.CSharp;

/// <summary>
/// Performance of the C# parser (stage 9): deeply nested input must parse in linear time without
/// stack overflow, and <see cref="Benchmark_Corpus"/> measures time and allocations on a corpus
/// against Roslyn's parser.
/// </summary>
[TestClass]
public class CSharpPerformanceTests
{
    private static readonly TimeSpan DeepInputTimeout = TimeSpan.FromSeconds(10);

    public TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow(1000)]
    [DataRow(5000)]
    public void NestedParentheses_Parse(int depth)
    {
        var expression = new string('(', depth) + "a" + new string(')', depth);
        var unit = ParseDeep($"class C {{ object M() => {expression}; }}");

        var body = (ExpressionMethodBody)((MethodDeclaration)((ClassDeclaration)unit.Members[0]).Members[0]).Body;
        var nested = body.Expression;
        for (var i = 0; i < depth; i++)
        {
            nested = ((ParenthesizedExpression)nested).Expression;
        }

        Assert.IsInstanceOfType<NameExpression>(nested);
        Assert.Contains(expression, WriteDeep(unit));
    }

    [TestMethod]
    [DataRow(5000)]
    [DataRow(50000)]
    public void LongBinaryChain_Parses(int count)
    {
        var expression = string.Join(" + ", Enumerable.Repeat("a", count));
        var source = $"class C {{ int M() => {expression}; }}";
        var unit = ParseDeep(source);

        var body = (ExpressionMethodBody)((MethodDeclaration)((ClassDeclaration)unit.Members[0]).Members[0]).Body;
        Assert.AreEqual(expression, body.Expression.Span.GetText(source));
    }

    [TestMethod]
    [DataRow(5000)]
    public void NestedCallsCastsAndTuples_Parse(int depth)
    {
        ParseDeep("class C { int M() => " + string.Concat(Enumerable.Repeat("F(", depth)) + "a" + new string(')', depth) + "; }");
        ParseDeep("class C { object M() => " + string.Concat(Enumerable.Repeat("(T)", depth)) + "a; }");
        ParseDeep("class C { object M() => " + new string('(', depth) + "a, b" + string.Concat(Enumerable.Repeat("), b", depth - 1)) + "); }");
    }

    [TestMethod]
    [DataRow(1000)]
    public void NestedBlocksAndLambdas_Parse(int depth)
    {
        var blocks = new StringBuilder();
        for (var i = 0; i < depth; i++)
        {
            blocks.Append(i % 2 == 0 ? "{ " : "F(() => { ");
        }

        for (var i = depth - 1; i >= 0; i--)
        {
            blocks.Append(i % 2 == 0 ? "} " : "}); ");
        }

        var unit = ParseDeep($"class C {{ void M() {blocks} }}");
        Assert.Contains("F(() =>", WriteDeep(unit));
    }

    /// <summary>
    /// Parses on a thread with a 1 MB stack, like thread pool threads, within a time limit. Input nested
    /// deeper than the stack allows is parsed again on a larger stack by <see cref="CSharpParser"/>.
    /// </summary>
    private static CompilationUnit ParseDeep(string source)
    {
        CompilationUnit unit = null;
        var thread = new Thread(() => unit = CSharpParser.Parse(source), 1024 * 1024);
        var stopwatch = Stopwatch.StartNew();
        thread.Start();
        Assert.IsTrue(thread.Join(DeepInputTimeout), $"did not finish in {DeepInputTimeout.TotalSeconds}s");
        Assert.IsNotNull(unit, "failed to parse");
        Console.WriteLine($"{source.Length} chars in {stopwatch.ElapsedMilliseconds} ms");
        return unit;
    }

    /// <summary>
    /// Writes the tree back on a thread with a 1 MB stack; a tree nested deeper than the stack allows is
    /// written on a larger stack by <see cref="CSharpWriter"/>.
    /// </summary>
    private static string WriteDeep(CompilationUnit unit)
    {
        string written = null;
        var thread = new Thread(
            () =>
            {
                var writer = new CSharpWriter();
                writer.WriteCompilationUnit(unit);
                written = writer.GetResult();
            },
            1024 * 1024);
        thread.Start();
        Assert.IsTrue(thread.Join(DeepInputTimeout), $"did not finish in {DeepInputTimeout.TotalSeconds}s");
        Assert.IsNotNull(written, "failed to write");
        return written;
    }

    /// <summary>
    /// Time and allocations of <see cref="CSharpParser"/> and of Roslyn's parser on the built-in corpus,
    /// or on <c>CSHARP_CORPUS_DIR</c> when it is set. The report is written to <c>benchmark-report.txt</c>.
    /// </summary>
    [TestMethod]
    public void Benchmark_Corpus()
    {
        var directory = Environment.GetEnvironmentVariable("CSHARP_CORPUS_DIR");
        if (string.IsNullOrEmpty(directory))
        {
            Assert.Inconclusive("Set CSHARP_CORPUS_DIR to a directory of .cs files to measure the parser on it.");
            return;
        }

        var sources = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText).ToList();
        var bytes = sources.Sum(s => (long)Encoding.UTF8.GetByteCount(s));

        var report = new StringBuilder();
        report.AppendLine($"Corpus {directory}: {sources.Count} files, {bytes / 1024.0 / 1024.0:F1} MB");

        var failures = 0;
        Measure(report, "PaspanParsers", bytes, () =>
        {
            failures = 0;
            foreach (var source in sources)
            {
                if (CSharpParser.Parse(source) == null)
                {
                    failures++;
                }
            }
        });

        report.AppendLine($"  files that did not parse (including files Roslyn rejects): {failures}");

        var roslynOptions = new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(LanguageVersion.CSharp14);
        Measure(report, "Roslyn", bytes, () =>
        {
            foreach (var source in sources)
            {
                CSharpSyntaxTree.ParseText(source, roslynOptions).GetRoot();
            }
        });

        TestContext.WriteLine(report.ToString());
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "benchmark-report.txt"), report.ToString());
    }

    private static void Measure(StringBuilder report, string name, long bytes, Action run)
    {
        // Warm up, then take the best of three runs
        run();

        var best = TimeSpan.MaxValue;
        long allocated = 0;
        for (var i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var before = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            run();
            stopwatch.Stop();
            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (stopwatch.Elapsed < best)
            {
                best = stopwatch.Elapsed;
            }
        }

        var megabytes = bytes / 1024.0 / 1024.0;
        report.AppendLine($"  {name,-14} {best.TotalMilliseconds,8:F0} ms  {megabytes / best.TotalSeconds,6:F1} MB/s  allocated {allocated / 1024.0 / 1024.0,8:F0} MB ({(double)allocated / bytes:F1} bytes per source byte)");
    }
}
