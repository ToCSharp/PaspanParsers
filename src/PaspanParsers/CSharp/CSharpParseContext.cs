using Paspan.Fluent;

namespace PaspanParsers.CSharp;

/// <summary>
/// Parse state specific to C#: options and the preprocessor symbols in effect.
/// </summary>
public sealed class CSharpParseContext(CSharpParseOptions options) : ParseContext
{
    public CSharpParseOptions Options { get; } = options ?? CSharpParseOptions.Default;

    /// <summary>
    /// Preprocessor symbols currently defined; <c>#define</c>/<c>#undef</c> update this set.
    /// </summary>
    public HashSet<string> DefinedSymbols { get; } = new(options?.PreprocessorSymbols ?? [], StringComparer.Ordinal);

    /// <summary>
    /// Tokens and lookahead results of the input being parsed, by position.
    /// </summary>
    internal SyntaxCache SyntaxCache { get; } = new();
}
