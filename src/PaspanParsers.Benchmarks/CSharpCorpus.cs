using System.Collections.Concurrent;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PaspanParsers.CSharp;

namespace PaspanParsers.Benchmarks;

/// <summary>
/// A set of C# files the benchmarks parse: the built-in oracle corpus (<c>builtin</c>) and every subdirectory
/// of <c>CSHARP_BENCH_CORPORA</c> (see <c>scripts/get-csharp-bench-corpora.sh</c>).
/// </summary>
/// <remarks>
/// Only files that Roslyn parses without syntax errors and that every compared parser parses are kept,
/// so that all benchmarks do the same work on the same files.
/// </remarks>
internal sealed class CSharpCorpus
{
    public const string BuiltIn = "builtin";

    public const string CorporaVariable = "CSHARP_BENCH_CORPORA";

    private static readonly ConcurrentDictionary<string, CSharpCorpus> s_loaded = new();

    private CSharpCorpus(string name, List<string> sources, List<string> skipped)
    {
        Name = name;
        Sources = sources;
        Skipped = skipped;
        Utf8Bytes = sources.Sum(s => (long)Encoding.UTF8.GetByteCount(s));
    }

    public string Name { get; }

    public IReadOnlyList<string> Sources { get; }

    /// <summary>
    /// Files left out, with the reason: Roslyn reports syntax errors in them or a compared parser fails on them.
    /// </summary>
    public IReadOnlyList<string> Skipped { get; }

    public long Utf8Bytes { get; }

    public static IEnumerable<string> Names()
    {
        yield return BuiltIn;

        var root = Environment.GetEnvironmentVariable(CorporaVariable);
        if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
        {
            foreach (var directory in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
            {
                yield return Path.GetFileName(directory);
            }
        }
    }

    public static CSharpCorpus Load(string name) => s_loaded.GetOrAdd(name, Read);

    private static CSharpCorpus Read(string name)
    {
        var directory = name == BuiltIn
            ? Path.Combine(FindRepositoryRoot(), "src", "PaspanParsers.Tests", "CSharp", "Corpus")
            : Path.Combine(Environment.GetEnvironmentVariable(CorporaVariable) ?? "", name);

        var roslynOptions = new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(LanguageVersion.CSharp14);
        var sources = new List<string>();
        var skipped = new List<string>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(source, roslynOptions);
            var relative = Path.GetRelativePath(directory, file);
            if (tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                skipped.Add(relative + ": Roslyn reports syntax errors");
                continue;
            }

            if (CSharpParser.Parse(source) == null)
            {
                skipped.Add(relative + ": " + nameof(CSharpParser) + " fails");
                continue;
            }

            if (CSharpHybridParser.Parse(source) == null)
            {
                skipped.Add(relative + ": " + nameof(CSharpHybridParser) + " fails");
                continue;
            }

            sources.Add(source);
        }

        return new CSharpCorpus(name, sources, skipped);
    }

    private static string FindRepositoryRoot()
    {
        // Benchmarks run from a build directory below the repository
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PaspanParsers.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("PaspanParsers.slnx not found above " + AppContext.BaseDirectory);
    }
}
