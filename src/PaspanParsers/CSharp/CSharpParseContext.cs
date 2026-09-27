using Paspan.Fluent;

namespace PaspanParsers.CSharp;

/// <summary>
/// Parse state specific to C#: options and the preprocessor symbols in effect.
/// </summary>
public sealed class CSharpParseContext(CSharpParseOptions options) : ParseContext
{
    public CSharpParseOptions Options { get; } = options ?? CSharpParseOptions.Default;

    /// <summary>
    /// Preprocessor symbols currently defined: the symbols of the options, updated by the
    /// <c>#define</c>/<c>#undef</c> directives at the start of the input once it is scanned.
    /// </summary>
    public HashSet<string> DefinedSymbols { get; } = new(options?.PreprocessorSymbols ?? [], StringComparer.Ordinal);

    /// <summary>
    /// Tokens and lookahead results of the input being parsed, by position.
    /// </summary>
    internal SyntaxCache SyntaxCache => _syntaxCache ??= new SyntaxCache(DefinedSymbols);

    private SyntaxCache _syntaxCache;

    /// <summary>
    /// Lets the next parse reuse the caches; the context must not be used to parse again.
    /// </summary>
    internal void ReleaseCaches()
    {
        _syntaxCache?.Release();
        _syntaxCache = null;
    }
}
