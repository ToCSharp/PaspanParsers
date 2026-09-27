using System.Collections.Concurrent;
using System.Text;
using PaspanParsers.Tests.CSharp;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// Runs the clang oracle over corpora of valid C++ files.
/// </summary>
/// <remarks>
/// The built-in corpus is the thematic files in <c>Cpp/Corpus</c>. Files known to pass are listed in
/// <c>Cpp/Corpus/oracle-baseline.txt</c>; a listed file that stops passing fails the run. Set
/// <c>UPDATE_ORACLE_BASELINE=1</c> to rewrite the baseline from the current results.
/// Set <c>CPP_CORPUS_DIR</c> to additionally measure an external corpus, <c>CPP_CORPUS_DEFINES</c>
/// (for example <c>NDEBUG;VERSION=3</c>) to define macros and <c>CPP_CORPUS_INCLUDE</c> (separated by
/// the path separator) for the include directories clang needs.
/// </remarks>
[TestClass]
public class CppCorpusTests
{
    private static readonly TimeSpan FileTimeout = TimeSpan.FromSeconds(10);

    private static readonly string[] SourceExtensions = [".cpp", ".cc", ".cxx", ".c++", ".h", ".hh", ".hpp", ".hxx", ".ipp", ".inl"];

    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string CorpusDirectory = Path.Combine(RepositoryRoot, "src", "PaspanParsers.Tests", "Cpp", "Corpus");
    private static readonly string BaselinePath = Path.Combine(CorpusDirectory, "oracle-baseline.txt");

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void CorpusFiles_AreValidCpp()
    {
        Clang.RequireClang();

        var invalid = BuiltInCorpus()
            .AsParallel()
            .Select(file => (file.Name, run: Clang.Run(File.ReadAllBytes(file.Path), ["-fsyntax-only"], CorpusDirectory)))
            .Where(x => !x.run.Succeeded)
            .Select(x => $"{x.Name} {x.run.FirstError()}")
            .ToList();

        Assert.IsEmpty(invalid, "Corpus files must be valid C++:\n" + string.Join("\n", invalid));
    }

    [TestMethod]
    public void Oracle_BuiltInCorpus_HasNoRegressions()
    {
        Clang.RequireClang();

        var results = Run(BuiltInCorpus(), new ClangOracleOptions(WorkingDirectory: CorpusDirectory));
        var report = Report("Built-in C++ corpus", results);
        TestContext.WriteLine(report);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "cpp-oracle-report.txt"), report);

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

    [TestMethod]
    public void Oracle_ExternalCorpus()
    {
        var directory = Environment.GetEnvironmentVariable("CPP_CORPUS_DIR");
        if (string.IsNullOrEmpty(directory))
        {
            Assert.Inconclusive("Set CPP_CORPUS_DIR to a directory of C++ files to measure an external corpus.");
            return;
        }

        Clang.RequireClang();

        var files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(IsSourceFile)
            .Select(path => (Path.GetRelativePath(directory, path).Replace('\\', '/'), path));

        var defines = (Environment.GetEnvironmentVariable("CPP_CORPUS_DEFINES") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var includes = (Environment.GetEnvironmentVariable("CPP_CORPUS_INCLUDE") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Path.GetFullPath)
            .ToArray();

        var title = defines.Length == 0 ? $"External C++ corpus {directory}" : $"External C++ corpus {directory} with {string.Join(";", defines)}";
        var report = Report(title, Run(files, new ClangOracleOptions(defines, includes), perFileWorkingDirectory: true));
        TestContext.WriteLine(report);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "cpp-oracle-external-report.txt"), report);
    }

    private sealed record FileResult(string Name, OracleResult Result);

    private static List<FileResult> Run(IEnumerable<(string Name, string Path)> files, ClangOracleOptions options, bool perFileWorkingDirectory = false)
    {
        var results = new ConcurrentBag<FileResult>();

        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, file =>
        {
            var source = File.ReadAllBytes(file.Path);
            var fileOptions = perFileWorkingDirectory ? options with { WorkingDirectory = Path.GetDirectoryName(file.Path) } : options;

            // A pathological input must not hang the whole run; clang has its own timeout
            var check = Task.Run(() => ClangOracle.Check(source, fileOptions));
            var result = check.Wait(FileTimeout + TimeSpan.FromMinutes(5))
                ? check.Result
                : new OracleResult(OracleStatus.ParseFailed, "timed out");

            results.Add(new FileResult(file.Name, result));
        });

        return results.OrderBy(r => r.Name, StringComparer.Ordinal).ToList();
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

        foreach (var r in results.Where(r => r.Result.Status != OracleStatus.Passed))
        {
            builder.AppendLine($"  {r.Result.Status,-11} {r.Name} {r.Result.Detail}");
        }

        return builder.ToString();
    }

    private static IEnumerable<(string Name, string Path)> BuiltInCorpus()
    {
        return Directory.EnumerateFiles(CorpusDirectory, "*.*", SearchOption.AllDirectories)
            .Where(IsSourceFile)
            .Select(path => (Relative(path), path))
            .OrderBy(x => x.Item1, StringComparer.Ordinal);
    }

    private static bool IsSourceFile(string path) => SourceExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

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
