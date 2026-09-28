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
    internal static readonly string CorpusDirectory = Path.Combine(RepositoryRoot, "src", "PaspanParsers.Tests", "Cpp", "Corpus");
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
        var directories = (Environment.GetEnvironmentVariable("CPP_CORPUS_DIR") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Path.GetFullPath)
            .ToList();
        if (directories.Count == 0)
        {
            Assert.Inconclusive("Set CPP_CORPUS_DIR to a directory of C++ files to measure an external corpus.");
            return;
        }

        Clang.RequireClang();

        // Several directories are separated by the path separator; the names of their files start with the directory name
        var files = directories.SelectMany(directory => Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(IsSourceFile)
            .Select(path =>
            {
                var name = Path.GetRelativePath(directory, path).Replace('\\', '/');
                return (directories.Count > 1 ? Path.GetFileName(directory) + "/" + name : name, path);
            }));

        var defines = (Environment.GetEnvironmentVariable("CPP_CORPUS_DEFINES") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var includes = (Environment.GetEnvironmentVariable("CPP_CORPUS_INCLUDE") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Path.GetFullPath)
            .ToArray();

        var corpus = string.Join(Path.PathSeparator, directories);
        var title = defines.Length == 0 ? $"External C++ corpus {corpus}" : $"External C++ corpus {corpus} with {string.Join(";", defines)}";
        var results = Run(files, new ClangOracleOptions(defines, includes), perFileWorkingDirectory: true, CheckWithHeaders);
        var report = ExternalReport(title, results);
        TestContext.WriteLine(report);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "cpp-oracle-external-report.txt"), report);
    }

    /// <summary>
    /// Why a file of an external corpus fails, found by checking it again with more knowledge.
    /// </summary>
    private enum FailureCategory
    {
        /// <summary>
        /// The file passes with the names and macros its headers declare: a heuristic for unknown names is wrong,
        /// or a conditional directive tests a macro of a header.
        /// </summary>
        UnknownNames,

        /// <summary>The file passes with its macros expanded: a macro is used in a syntactic position.</summary>
        Macros,

        /// <summary>The file fails even with its macros expanded: an error of our parser (or of the oracle).</summary>
        ParserError,

        /// <summary>Clang could not preprocess the file, or rejected it with its macros expanded.</summary>
        Unclassified,
    }

    private sealed record FileResult(string Name, OracleResult Result)
    {
        /// <summary>The result with the names and macros of the headers, when the file fails without them.</summary>
        public OracleResult WithNames { get; init; }

        /// <summary>The result with the macros expanded, when the file fails with the names of the headers.</summary>
        public OracleResult Expanded { get; init; }

        public FailureCategory? Category { get; init; }
    }

    /// <summary>
    /// Checks a file of an external corpus. A file that fails is checked again in the mode "with names", with
    /// the names and macros its headers declare, and if it still fails, with its macros expanded by clang
    /// (<see cref="ClangPreprocessor"/>) and the same names.
    /// </summary>
    private static FileResult CheckWithHeaders(string name, byte[] source, ClangOracleOptions options)
    {
        var result = ClangOracle.Check(source, options, out var headerNames);
        if (result.Status is OracleStatus.Passed or OracleStatus.Invalid)
        {
            return new FileResult(name, result);
        }

        var preprocessed = ClangPreprocessor.Preprocess(source, options);
        if (preprocessed == null || headerNames == null)
        {
            return new FileResult(name, result) { Category = FailureCategory.Unclassified };
        }

        var withHeaders = options with { Headers = new HeaderKnowledge(headerNames, preprocessed.HeaderMacros) };
        var withNames = ClangOracle.Check(source, withHeaders);
        if (withNames.Status == OracleStatus.Passed)
        {
            return new FileResult(name, result) { WithNames = withNames, Category = FailureCategory.UnknownNames };
        }

        var expanded = ClangOracle.Check(preprocessed.Expanded, withHeaders);
        var category = expanded.Status switch
        {
            OracleStatus.Passed => FailureCategory.Macros,
            OracleStatus.Invalid => FailureCategory.Unclassified,
            _ => FailureCategory.ParserError,
        };

        return new FileResult(name, result) { WithNames = withNames, Expanded = expanded, Category = category };
    }

    private static List<FileResult> Run(IEnumerable<(string Name, string Path)> files, ClangOracleOptions options, bool perFileWorkingDirectory = false,
        Func<string, byte[], ClangOracleOptions, FileResult> check = null)
    {
        check ??= (name, source, fileOptions) => new FileResult(name, ClangOracle.Check(source, fileOptions));
        var results = new ConcurrentBag<FileResult>();

        Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, file =>
        {
            var source = File.ReadAllBytes(file.Path);
            var fileOptions = perFileWorkingDirectory ? options with { WorkingDirectory = Path.GetDirectoryName(file.Path) } : options;

            // A pathological input must not hang the whole run; clang has its own timeout
            var task = Task.Run(() => check(file.Name, source, fileOptions));
            var result = task.Wait(FileTimeout + TimeSpan.FromMinutes(15))
                ? task.Result
                : new FileResult(file.Name, new OracleResult(OracleStatus.ParseFailed, "timed out"));

            results.Add(result);
        });

        return results.OrderBy(r => r.Name, StringComparer.Ordinal).ToList();
    }

    private static string Report(string title, List<FileResult> results)
    {
        var valid = results.Where(r => r.Result.Status != OracleStatus.Invalid).ToList();
        var passed = valid.Count(r => r.Result.Status == OracleStatus.Passed);

        var builder = new StringBuilder();
        builder.AppendLine($"{title}: {passed}/{valid.Count} valid files pass ({Percent(passed, valid.Count)}), {results.Count - valid.Count} invalid skipped");

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

    /// <summary>
    /// The report of an external corpus: the share of files that pass as they are, with the names of their
    /// headers and with their macros expanded, and the failures by category.
    /// </summary>
    private static string ExternalReport(string title, List<FileResult> results)
    {
        var valid = results.Where(r => r.Result.Status != OracleStatus.Invalid).ToList();
        var passed = valid.Count(r => r.Result.Status == OracleStatus.Passed);
        var withNames = passed + valid.Count(r => r.Category == FailureCategory.UnknownNames);
        var expanded = withNames + valid.Count(r => r.Category == FailureCategory.Macros);

        var builder = new StringBuilder();
        builder.AppendLine($"{title}: {valid.Count} valid files, {results.Count - valid.Count} invalid skipped");
        builder.AppendLine($"  pass as they are:              {passed,5} ({Percent(passed, valid.Count)})");
        builder.AppendLine($"  pass with header names:        {withNames,5} ({Percent(withNames, valid.Count)})");
        builder.AppendLine($"  pass with macros expanded too: {expanded,5} ({Percent(expanded, valid.Count)})");

        var failed = valid.Where(r => r.Category != null).GroupBy(r => r.Category!.Value).OrderBy(g => g.Key).ToList();
        foreach (var group in failed)
        {
            builder.AppendLine($"  {Describe(group.Key)}: {group.Count()}");
        }

        foreach (var group in failed)
        {
            builder.AppendLine();
            builder.AppendLine($"{Describe(group.Key)}:");
            foreach (var r in group)
            {
                builder.AppendLine($"  {r.Result.Status,-13} {r.Name} {r.Result.Detail}");
                if (r.Category is FailureCategory.ParserError or FailureCategory.Unclassified && r.Expanded != null)
                {
                    builder.AppendLine($"    expanded: {r.Expanded.Status} {r.Expanded.Detail}");
                }
            }
        }

        var invalid = results.Where(r => r.Result.Status == OracleStatus.Invalid).ToList();
        if (invalid.Count != 0)
        {
            builder.AppendLine();
            builder.AppendLine("Invalid (clang reports errors):");
            foreach (var r in invalid)
            {
                builder.AppendLine($"  {r.Name} {r.Result.Detail}");
            }
        }

        return builder.ToString();
    }

    private static string Describe(FailureCategory category) => category switch
    {
        FailureCategory.UnknownNames => "Wrong heuristics for unknown names, or conditions on macros of headers (pass with header names)",
        FailureCategory.Macros => "Macros in syntactic positions (pass with macros expanded)",
        FailureCategory.ParserError => "Parser errors (fail with macros expanded)",
        _ => "Unclassified (clang rejects the preprocessed file)",
    };

    private static string Percent(int count, int total) => $"{(total == 0 ? 0 : 100.0 * count / total):F1}%";

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
