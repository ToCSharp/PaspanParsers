using Paspan.Fluent;

namespace PaspanParsers.Cpp;

/// <summary>
/// Parse state specific to C++: options and the caches of the input being parsed.
/// </summary>
public sealed class CppParseContext(CppParseOptions options) : ParseContext
{
    public CppParseOptions Options { get; } = options ?? CppParseOptions.Default;

    /// <summary>
    /// Tokens and lookahead results of the input being parsed, by position.
    /// </summary>
    internal SyntaxCache SyntaxCache => _syntaxCache ??= new SyntaxCache();

    private SyntaxCache _syntaxCache;
}
