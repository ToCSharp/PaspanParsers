using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PaspanParsers.Tests.CSharp;

/// <summary>
/// Runs the Roslyn oracle over corpora of valid C# files.
/// </summary>
/// <remarks>
/// The built-in corpus is the C# example files, the repository's own sources and the
/// thematic files in <c>CSharp/Corpus</c>. Files known to pass are listed in
/// <c>CSharp/Corpus/oracle-baseline.txt</c>; a listed file that stops passing fails the run.
/// Set <c>UPDATE_ORACLE_BASELINE=1</c> to rewrite the baseline from the current results.
/// Set <c>CSHARP_CORPUS_DIR</c> to additionally measure an external corpus, and
/// <c>CSHARP_CORPUS_SYMBOLS</c> (for example <c>NET;DEBUG</c>) to parse it with preprocessor symbols.
/// </remarks>
[TestClass]
public class CSharpCorpusTests
{
    private static readonly TimeSpan FileTimeout = TimeSpan.FromSeconds(10);

    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string CorpusDirectory = Path.Combine(RepositoryRoot, "src", "PaspanParsers.Tests", "CSharp", "Corpus");
    private static readonly string BaselinePath = Path.Combine(CorpusDirectory, "oracle-baseline.txt");

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void CorpusFiles_AreValidCSharp()
    {
        var invalid = Directory.EnumerateFiles(CorpusDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(path => (path, result: RoslynOracle.Check(File.ReadAllText(path))))
            .Where(x => x.result.Status == OracleStatus.Invalid)
            .Select(x => $"{Relative(x.path)} {x.result.Detail}")
            .ToList();

        Assert.IsEmpty(invalid, "Corpus files must be valid C#:\n" + string.Join("\n", invalid));
    }

    [TestMethod]
    public void Oracle_BuiltInCorpus_HasNoRegressions()
    {
        var results = Run(BuiltInCorpus());
        var report = Report("Built-in corpus", results);
        TestContext.WriteLine(report);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "oracle-report.txt"), report);

        var passing = results.Where(r => r.Result.Status == OracleStatus.Passed).Select(r => r.Name).ToHashSet();
        var baseline = ReadBaseline();

        if (Environment.GetEnvironmentVariable("UPDATE_ORACLE_BASELINE") == "1")
        {
            File.WriteAllLines(BaselinePath, passing.Order(StringComparer.Ordinal));
            return;
        }

        var regressions = results
            .Where(r => baseline.Contains(r.Name) && r.Result.Status != OracleStatus.Passed)
            .Select(r => $"{r.Name}: {r.Result.Status} {r.Result.Detail}")
            .ToList();

        Assert.IsEmpty(regressions, "Files from oracle-baseline.txt no longer pass:\n" + string.Join("\n", regressions));

        var newlyPassing = passing.Where(name => !baseline.Contains(name)).Order(StringComparer.Ordinal).ToList();
        if (newlyPassing.Count != 0)
        {
            TestContext.WriteLine("Newly passing (run with UPDATE_ORACLE_BASELINE=1 to record):\n  " + string.Join("\n  ", newlyPassing));
        }
    }

    /// <summary>
    /// Every statement of every method and accessor body in the built-in corpus, on its own in a
    /// method, must pass the oracle. This measures types, expressions, patterns and statements
    /// independently of the declarations around them.
    /// </summary>
    [TestMethod]
    public void Oracle_BuiltInCorpus_Statements()
    {
        var options = new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(LanguageVersion.CSharp14);
        var failures = new List<string>();
        var checkedCount = 0;

        foreach (var (name, path) in BuiltInCorpus())
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path), options).GetRoot();
            var bodies = root.DescendantNodes().OfType<BlockSyntax>()
                .Where(block => block.Parent is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax);

            foreach (var statement in bodies.SelectMany(body => body.Statements))
            {
                var result = RoslynOracle.Check(SyntaxTestHelper.InMethod(statement.ToString()));
                if (result.Status == OracleStatus.Invalid)
                {
                    continue;
                }

                checkedCount++;
                if (result.Status != OracleStatus.Passed)
                {
                    var line = statement.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    failures.Add($"{name}:{line}: {result.Status} {result.Detail}");
                }
            }
        }

        TestContext.WriteLine($"Statements: {checkedCount - failures.Count}/{checkedCount} pass");
        Assert.IsEmpty(failures, "Statements that do not pass the oracle:\n" + string.Join("\n", failures));
    }

    [TestMethod]
    public void Oracle_ExternalCorpus()
    {
        var directory = Environment.GetEnvironmentVariable("CSHARP_CORPUS_DIR");
        if (string.IsNullOrEmpty(directory))
        {
            Assert.Inconclusive("Set CSHARP_CORPUS_DIR to a directory of .cs files to measure an external corpus.");
            return;
        }

        var files = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Select(path => (Path.GetRelativePath(directory, path).Replace('\\', '/'), path));

        var symbols = (Environment.GetEnvironmentVariable("CSHARP_CORPUS_SYMBOLS") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var title = symbols.Length == 0 ? $"External corpus {directory}" : $"External corpus {directory} with {string.Join(";", symbols)}";

        var report = Report(title, Run(files, symbols));
        TestContext.WriteLine(report);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "oracle-external-report.txt"), report);
    }

    private sealed record FileResult(string Name, OracleResult Result);

    private static List<FileResult> Run(IEnumerable<(string Name, string Path)> files, string[] preprocessorSymbols = null)
    {
        var results = new List<FileResult>();

        foreach (var (name, path) in files)
        {
            var source = File.ReadAllText(path);

            // A pathological input must not hang the whole run
            var check = Task.Run(() => RoslynOracle.Check(source, preprocessorSymbols));
            var result = check.Wait(FileTimeout)
                ? check.Result
                : new OracleResult(OracleStatus.ParseFailed, $"timed out after {FileTimeout.TotalSeconds}s");

            results.Add(new FileResult(name, result));
        }

        return results;
    }

    private static string Report(string title, List<FileResult> results)
    {
        var valid = results.Where(r => r.Result.Status != OracleStatus.Invalid).ToList();
        var passed = valid.Count(r => r.Result.Status == OracleStatus.Passed);
        var percent = valid.Count == 0 ? 0 : 100.0 * passed / valid.Count;

        var builder = new StringBuilder();
        builder.AppendLine($"{title}: {passed}/{valid.Count} valid files pass ({percent:F1}%), {results.Count - valid.Count} invalid skipped");

        foreach (var group in valid.Where(r => r.Result.Status != OracleStatus.Passed).GroupBy(r => r.Result.Status).OrderBy(g => g.Key))
        {
            builder.AppendLine($"  {group.Key}: {group.Count()}");
        }

        foreach (var r in results.Where(r => r.Result.Status != OracleStatus.Passed).OrderBy(r => r.Name, StringComparer.Ordinal))
        {
            builder.AppendLine($"  {r.Result.Status,-11} {r.Name} {r.Result.Detail}");
        }

        return builder.ToString();
    }

    private static IEnumerable<(string Name, string Path)> BuiltInCorpus()
    {
        var sources = Path.Combine(RepositoryRoot, "src");

        return Directory.EnumerateFiles(sources, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".xcs", StringComparison.Ordinal))
            .Where(path => !IsBuildOutput(path))
            .Select(path => (Relative(path), path))
            .OrderBy(x => x.Item1, StringComparer.Ordinal);
    }

    private static bool IsBuildOutput(string path)
    {
        var parts = Path.GetRelativePath(RepositoryRoot, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Contains("bin") || parts.Contains("obj");
    }

    private static HashSet<string> ReadBaseline()
    {
        return File.Exists(BaselinePath)
            ? File.ReadAllLines(BaselinePath).Select(l => l.Trim()).Where(l => l.Length != 0 && !l.StartsWith('#')).ToHashSet()
            : [];
    }

    private static string Relative(string path) => Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PaspanParsers.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not find the repository root (PaspanParsers.slnx).");
    }
}
