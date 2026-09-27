using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis.CSharp;
using PaspanParsers.CSharp;

namespace PaspanParsers.Benchmarks;

/// <summary>
/// Parses every file of a corpus. <see cref="RecursiveDescent"/> is the current C# parser; <see cref="Scanner"/>
/// is the part every variant of the parser shares, so the syntactic layer costs the difference between them.
/// </summary>
[Config(typeof(CorpusConfig))]
public class CSharpParserBenchmarks
{
    private static readonly Microsoft.CodeAnalysis.CSharp.CSharpParseOptions s_roslynOptions = new(LanguageVersion.CSharp14);

    private IReadOnlyList<string> _sources;

    [ParamsSource(nameof(Corpora))]
    public string Corpus { get; set; }

    public static IEnumerable<string> Corpora => CSharpCorpus.Names();

    [GlobalSetup]
    public void Setup()
    {
        var corpus = CSharpCorpus.Load(Corpus);
        _sources = corpus.Sources;
        Console.WriteLine($"// Corpus {Corpus}: {corpus.Sources.Count} files, {corpus.Utf8Bytes / 1024.0:F0} KB, {corpus.Skipped.Count} skipped");
        foreach (var skipped in corpus.Skipped)
        {
            Console.WriteLine("//   skipped " + skipped);
        }
    }

    /// <summary>
    /// Lexer and preprocessor: the tokens with the trivia before them, and the expressions in interpolated strings.
    /// </summary>
    [Benchmark]
    public int Scanner()
    {
        var tokens = 0;
        foreach (var source in _sources)
        {
            tokens += CSharpParser.ScanTokens(source);
        }

        return tokens;
    }

    /// <summary>
    /// The hand-written recursive descent parser (<see cref="CSharpParser"/>).
    /// </summary>
    [Benchmark(Baseline = true)]
    public int RecursiveDescent()
    {
        var length = 0;
        foreach (var source in _sources)
        {
            var unit = CSharpParser.Parse(source) ?? throw new InvalidOperationException("The corpus holds a file the parser rejects.");
            length += unit.Span.Length;
        }

        return length;
    }

    /// <summary>
    /// Roslyn's parser, for reference: the syntax tree with its root node.
    /// </summary>
    [Benchmark]
    public int Roslyn()
    {
        var length = 0;
        foreach (var source in _sources)
        {
            length += CSharpSyntaxTree.ParseText(source, s_roslynOptions).GetRoot().FullSpan.Length;
        }

        return length;
    }
}
