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
    /// The combinator grammar of the hybrid parser (<see cref="CSharpHybridParser"/>), which the hand-written
    /// parser runs for blocks and statements; null for the hand-written parser.
    /// </summary>
    internal HybridGrammar Grammar { get; init; }

    /// <summary>
    /// The state of the innermost hand-written parser that called a combinator parser.
    /// </summary>
    internal SyntaxState SyntaxState { get; set; }

    /// <summary>
    /// The parts parsed before the rule that is running, by type (see <see cref="SyntaxRules.WithPrefix{TPrefix, T}"/>).
    /// </summary>
    internal Stack<T> PrefixStack<T>()
    {
        _prefixStacks ??= [];
        if (!_prefixStacks.TryGetValue(typeof(T), out var stack))
        {
            _prefixStacks[typeof(T)] = stack = new Stack<T>();
        }

        return (Stack<T>)stack;
    }

    private Dictionary<Type, object> _prefixStacks;

    /// <summary>
    /// Lets the next parse reuse the caches; the context must not be used to parse again.
    /// </summary>
    internal void ReleaseCaches()
    {
        _syntaxCache?.Release();
        _syntaxCache = null;
    }
}
