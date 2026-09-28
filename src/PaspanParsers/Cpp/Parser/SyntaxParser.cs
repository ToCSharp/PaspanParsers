using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using Paspan;
using Paspan.Fluent;

namespace PaspanParsers.Cpp;

/// <summary>
/// Hand-written recursive descent parser for C++: declarations, declarators, statements and expressions.
/// </summary>
/// <remarks>
/// Like the C# parser, the grammar is parsed on top of <see cref="Lexer"/> and runs as a combinator parser
/// through <see cref="SyntaxRuleParser{T}"/>.
/// <para>
/// Parse methods return null when the input does not match; the position is then unspecified and
/// callers that try alternatives restore it. Tokens are scanned lazily and cached by position.
/// </para>
/// </remarks>
internal ref partial struct SyntaxParser
{
    private readonly ReadOnlySpan<byte> _source;
    private readonly SyntaxCache _cache;
    private int _position;

    // In template arguments, a '>' outside parentheses closes the arguments instead of comparing
    private bool _inTemplateArguments;

    // In a requires-clause, where a name followed by template arguments is a concept-id
    private bool _inConstraint;

    // In the declarator of a parameter whose type names a pack, where '...' starts a pack: void f(Ts...)
    private bool _parameterTypeIsPack;

    // In the members of a class: the bodies of its member functions, parsed when the outermost class is complete
    private DeferredBodies _deferredBodies;

    // Before the declarator of a declaration in a block, where T x(y) with an unknown y initializes x
    private bool _inBlockDeclarator;

    // The token last looked up and the position it was looked up at
    private int _lastPosition = -1;
    private SyntaxToken _lastToken;

    public SyntaxParser(ReadOnlySpan<byte> source, int position, ParseContext context)
    {
        _source = source;
        _position = position;
        _cache = SyntaxCache.For(context);
        _cache.Tokens.EnsureSourceLength(source.Length);
    }

    /// <summary>
    /// The end of the last consumed token.
    /// </summary>
    public readonly int Position => _position;

    // ========================================
    // Tokens
    // ========================================

    private SyntaxToken TokenAt(int position)
    {
        // The parser asks for the same token many times: the current one
        if (position == _lastPosition)
        {
            return _lastToken;
        }

        if (!_cache.Tokens.TryGet(position, out var token))
        {
            token = ScanToken(position);
            _cache.Tokens.Add(position, token);
        }

        _lastPosition = position;
        _lastToken = token;
        return token;
    }

    private SyntaxToken Current => TokenAt(_position);

    private SyntaxToken Peek(int offset)
    {
        var token = TokenAt(_position);
        for (var i = 0; i < offset; i++)
        {
            token = TokenAt(token.End);
        }

        return token;
    }

    private SyntaxToken EatToken()
    {
        var token = TokenAt(_position);
        _position = token.End;
        if (_position > _cache.FurthestPosition)
        {
            _cache.FurthestPosition = _position;
        }

        return token;
    }

    private bool IsPunctuator(string text) => Current.IsPunctuator(text);

    private bool IsKeyword(string text) => Current.IsKeyword(text);

    private bool TryEatPunctuator(string text)
    {
        if (Current.IsPunctuator(text))
        {
            EatToken();
            return true;
        }

        return false;
    }

    private bool TryEatKeyword(string text)
    {
        if (Current.IsKeyword(text))
        {
            EatToken();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Consumes an identifier and returns its value, or null.
    /// </summary>
    private string TryEatIdentifier()
    {
        var token = Current;
        if (!token.IsIdentifier)
        {
            return null;
        }

        EatToken();
        return token.Text;
    }

    // ========================================
    // Spans
    // ========================================

    /// <summary>
    /// The start of the next token: the start of a node that begins with it.
    /// </summary>
    private int NodeStart => Current.Start;

    /// <summary>
    /// Sets the span of <paramref name="node"/> from <paramref name="start"/> to the end of the last consumed
    /// token and returns the node; a null node stays null.
    /// </summary>
    private readonly T Finish<T>(T node, int start) where T : CppNode
    {
        return node == null ? null : Finish(node, start, Math.Max(start, _position));
    }

    /// <summary>
    /// Sets the span of <paramref name="node"/> and its leading directives, and returns the node.
    /// </summary>
    private readonly T Finish<T>(T node, int start, int end) where T : CppNode
    {
        node.Span = new TextSpan(start, end);
        if (_cache.LeadingDirectives.Count != 0)
        {
            node.LeadingDirectives = _cache.LeadingDirectives.GetValueOrDefault(start);
        }

        return node;
    }

    /// <summary>
    /// The directives before <paramref name="token"/> that the writer writes back, or null.
    /// </summary>
    private IReadOnlyList<PreprocessorDirective> DirectivesBefore(SyntaxToken token)
    {
        return _cache.LeadingDirectives.GetValueOrDefault(token.Start);
    }

    /// <summary>
    /// Like <see cref="Finish{T}(T, int)"/> for a node that starts where <paramref name="first"/> starts.
    /// </summary>
    private readonly T Finish<T>(T node, CppNode first) where T : CppNode => Finish(node, first.Span.Start);

    /// <summary>
    /// True when <paramref name="second"/> directly follows <paramref name="first"/> without trivia.
    /// </summary>
    private static bool AreAdjacent(SyntaxToken first, SyntaxToken second) => first.End == second.Start;

    /// <summary>
    /// Guards the recursion of nested constructs against stack overflow: throws
    /// <see cref="InsufficientExecutionStackException"/>, which <see cref="CppParser"/> handles by parsing
    /// again on a larger stack.
    /// </summary>
    private static void EnsureSufficientStack() => RuntimeHelpers.EnsureSufficientExecutionStack();

    // ========================================
    // Scanning
    // ========================================

    /// <summary>
    /// Scans the trivia at <paramref name="position"/>, including directives and inactive branches, and the
    /// token after it. The directives that the writer writes back are kept as the leading directives of
    /// the token.
    /// </summary>
    private SyntaxToken ScanToken(int position)
    {
        var preprocessor = _cache.Preprocessor(_source);
        var start = position;
        List<PreprocessorDirective> directives = null;
        while (true)
        {
            start += Lexer.ScanTrivia(_source[start..]);
            if (start >= _source.Length || _source[start] is not ((byte)'#' or (byte)'%')
                || !preprocessor.TryGetDirective(start, out var next, out var directive))
            {
                break;
            }

            if (!directive.IsConditional)
            {
                (directives ??= []).Add(directive);
            }

            start = next;
        }

        var token = ScanTokenAt(start);
        if (directives != null)
        {
            _cache.LeadingDirectives[token.Start] = directives;
        }

        return token;
    }

    /// <summary>
    /// Scans the token at <paramref name="start"/>, after the trivia. A token interrupted by a line splice
    /// (<c>ma\⏎in</c>) is scanned again on the logical line without splices: its span covers the source
    /// bytes, its text is the logical text. Splices in raw strings are not removed ([lex.pptoken]).
    /// </summary>
    private SyntaxToken ScanTokenAt(int start)
    {
        if (start >= _source.Length)
        {
            return new SyntaxToken(TokenKind.EndOfFile, _source.Length, _source.Length, "");
        }

        var s = _source[start..];
        var kind = ScanTokenIn(s, out var length, out var text, out var isRaw);
        if (!isRaw && kind != TokenKind.Bad && (Lexer.SpliceLength(s, length) > 0 || Lexer.ContainsSplice(s[..length])))
        {
            var (logical, positions) = Lexer.RemoveSplices(s);
            kind = ScanTokenIn(logical, out var logicalLength, out text, out _);
            length = positions[logicalLength - 1] + 1;
        }

        return new SyntaxToken(kind, start, start + length, text);
    }

    /// <summary>
    /// Scans the token that starts <paramref name="s"/>: its kind, length and text.
    /// </summary>
    private TokenKind ScanTokenIn(ReadOnlySpan<byte> s, out int length, out string text, out bool isRawString)
    {
        isRawString = false;
        if (s.IsEmpty)
        {
            length = 0;
            text = "";
            return TokenKind.Bad;
        }

        var first = s[0];

        // Dispatched on the first byte: a quote or an encoding prefix, a digit, an identifier, a punctuator
        if (first is (byte)'"' or (byte)'\'' or (byte)'u' or (byte)'U' or (byte)'L' or (byte)'R')
        {
            length = Lexer.ScanQuotedLiteral(s, out var isString);
            if (length > 0)
            {
                Lexer.ScanLiteralPrefix(s, out isRawString);
                text = Encoding.UTF8.GetString(s[..length]);
                return isString ? TokenKind.StringLiteral : TokenKind.CharacterLiteral;
            }
        }

        if (Lexer.IsDecimalDigit(first) || first == '.')
        {
            length = Lexer.ScanNumber(s, out _);
            if (length > 0)
            {
                // Short numbers repeat: 0, 1, 0x7f
                text = length <= 8 ? _cache.Intern(s[..length]) : Encoding.UTF8.GetString(s[..length]);
                return TokenKind.NumericLiteral;
            }
        }

        if (Lexer.IsIdentifierStart(first) || first == '\\')
        {
            length = Lexer.ScanIdentifier(s);
            if (length > 0)
            {
                var identifier = s[..length];
                if (identifier.IndexOfAnyExceptInRange((byte)0, (byte)0x7F) < 0 && identifier.IndexOf((byte)'\\') < 0)
                {
                    return _cache.InternIdentifier(identifier, out text);
                }

                return IdentifierKind(Lexer.IdentifierValue(identifier), out text);
            }
        }

        // '<::' not followed by ':' or '>' is '<' and '::' ([lex.pptoken])
        if (s.StartsWith("<::"u8) && (s.Length == 3 || s[3] is not ((byte)':' or (byte)'>')))
        {
            length = 1;
            text = "<";
            return TokenKind.Punctuator;
        }

        length = Lexer.ScanPunctuator(s, out text);
        if (length > 0)
        {
            return TokenKind.Punctuator;
        }

        length = 1;
        text = "";
        return TokenKind.Bad;
    }

    /// <summary>
    /// The kind of the identifier <paramref name="value"/>: a keyword, an alternative token (as a punctuator,
    /// <paramref name="text"/> is the operator it stands for) or an identifier.
    /// </summary>
    internal static TokenKind IdentifierKind(string value, out string text)
    {
        if (Lexer.AlternativeTokens.TryGetValue(value, out var @operator))
        {
            text = @operator;
            return TokenKind.Punctuator;
        }

        text = value;
        return Lexer.Keywords.Contains(value) ? TokenKind.Keyword : TokenKind.Identifier;
    }

    // ========================================
    // Entry points run as combinator parsers
    // ========================================

    public delegate T Rule<T>(ref SyntaxParser parser);

    public static TranslationUnit ParseTranslationUnitRule(ref SyntaxParser parser) => parser.ParseTranslationUnit();

    /// <summary>
    /// The error of a failed parse: the token after the furthest token any alternative consumed.
    /// </summary>
    public static ParseError DescribeFailure(ReadOnlySpan<byte> source, ParseContext context)
    {
        var parser = new SyntaxParser(source, 0, context);
        var token = parser.TokenAt(parser._cache.FurthestPosition);

        string description;
        if (token.Kind == TokenKind.EndOfFile)
        {
            description = "end of file";
        }
        else
        {
            var text = Encoding.UTF8.GetString(source[token.Start..token.End]).ReplaceLineEndings(" ");
            description = $"'{(text.Length <= 40 ? text : text[..37] + "...")}'";
        }

        var error = new ParseError { Message = $"Unexpected {description}", Position = token.Start };
        (error.Line, error.Column) = new LineMap(source, unicodeLineBreaks: false).GetLineAndColumn(token.Start);
        return error;
    }
}

/// <summary>
/// The bodies of member functions defined in a class, which are parsed when the outermost enclosing class is
/// complete ([class.mem]): they see the members declared after them. <see cref="ClassDepth"/> is the
/// number of active scopes up to the scope of that class.
/// </summary>
internal sealed class DeferredBodies(int classDepth)
{
    public int ClassDepth { get; } = classDepth;

    public List<DeferredBody> Bodies { get; } = [];
}

/// <summary>
/// The body of <see cref="Function"/>, which starts at <see cref="Start"/>, and the scopes inside the
/// outermost class that were active at the function, such as those of nested classes and template parameters.
/// </summary>
internal readonly record struct DeferredBody(FunctionDefinition Function, int Start, object[] Scopes);

/// <summary>
/// Token caches shared by all <see cref="SyntaxParser"/> runs over the same input.
/// </summary>
internal sealed class SyntaxCache(CppParseOptions options)
{
    public TokenCache Tokens { get; } = new();

    /// <summary>
    /// The names declared so far.
    /// </summary>
    public Symbols Symbols { get; } = new(options);

    /// <summary>
    /// The template arguments at the positions of '&lt;', with the state they were parsed in: the arguments and
    /// the position after them, or null arguments where none parse.
    /// </summary>
    public Dictionary<(int Position, int Symbols, bool InTemplateArguments, bool InConstraint), (List<CppNode> Arguments, int End)> TemplateArguments { get; } = [];

    /// <summary>
    /// The last range of positions from which no '&gt;' follows before the end of the enclosing brackets or of the
    /// statement: a '&lt;' there starts no template arguments.
    /// </summary>
    public TextSpan NoTemplateClose { get; set; }

    /// <summary>
    /// Whether a '&gt;' follows the '&lt;' at a position before the end of the enclosing brackets or of the statement.
    /// </summary>
    public Dictionary<int, bool> TemplateCloses { get; } = [];

    /// <summary>
    /// The directives that the writer writes back before a token, by the start of the token.
    /// </summary>
    public Dictionary<int, List<PreprocessorDirective>> LeadingDirectives { get; } = [];

    private Preprocessor _preprocessor;

    /// <summary>
    /// The directives of <paramref name="source"/>, processed before its first token is scanned.
    /// </summary>
    public Preprocessor Preprocessor(ReadOnlySpan<byte> source) => _preprocessor ??= Cpp.Preprocessor.Run(source, options);

    /// <summary>
    /// The end of the furthest token consumed, for the error position of a failed parse.
    /// </summary>
    public int FurthestPosition { get; set; }

    private readonly HashSet<string> _strings = new(StringComparer.Ordinal);

    // ASCII identifiers with their kinds and texts (the operator of an alternative token)
    private readonly Dictionary<string, (TokenKind Kind, string Text)> _identifiers = new(StringComparer.Ordinal);

    /// <summary>
    /// The text of a token; ASCII texts get one string per distinct text.
    /// </summary>
    public string Intern(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length > 256 || utf8.IndexOfAnyExceptInRange((byte)0, (byte)0x7F) >= 0)
        {
            return Encoding.UTF8.GetString(utf8);
        }

        Span<char> chars = stackalloc char[utf8.Length];
        Encoding.ASCII.GetChars(utf8, chars);
        var lookup = _strings.GetAlternateLookup<ReadOnlySpan<char>>();
        if (!lookup.TryGetValue(chars, out var text))
        {
            text = new string(chars);
            _strings.Add(text);
        }

        return text;
    }

    /// <summary>
    /// The kind and the text of an ASCII identifier, keyword or alternative token; one string per distinct
    /// name, and its kind is looked up once.
    /// </summary>
    public TokenKind InternIdentifier(ReadOnlySpan<byte> utf8, out string text)
    {
        if (utf8.Length > 256)
        {
            return SyntaxParser.IdentifierKind(Encoding.ASCII.GetString(utf8), out text);
        }

        Span<char> chars = stackalloc char[utf8.Length];
        Encoding.ASCII.GetChars(utf8, chars);
        var lookup = _identifiers.GetAlternateLookup<ReadOnlySpan<char>>();
        if (lookup.TryGetValue(chars, out var entry))
        {
            text = entry.Text;
            return entry.Kind;
        }

        var name = new string(chars);
        var kind = SyntaxParser.IdentifierKind(name, out text);
        _identifiers[name] = (kind, text);
        return kind;
    }

    /// <summary>
    /// Called when the parse is over and the cache will not be used again: the arrays of the token cache
    /// and the log of the symbols, the largest allocations of a parse, are reused by the next parse.
    /// </summary>
    public void Release()
    {
        Tokens.Release();
        Symbols.Release();
    }

    public static SyntaxCache For(ParseContext context)
    {
        // A CppParseContext is created for one input; any other context may be reused
        // for different inputs, so its runs do not share caches.
        return context is CppParseContext cpp ? cpp.SyntaxCache : new SyntaxCache(CppParseOptions.Default);
    }
}

/// <summary>
/// The tokens scanned so far, by the position their scan started at: the end of the previous token, where
/// the trivia before the token starts. Positions index an array as long as the source, so a lookup costs
/// no hashing; the arrays are rented from <see cref="ArrayPool{T}.Shared"/> and returned by
/// <see cref="Release"/>, so that a parse does not allocate them on the large object heap.
/// </summary>
internal sealed class TokenCache
{
    // The index of the token scanned at each position plus one, 0 where no token was scanned
    private int[] _indexes = [];
    private int _length;
    private SyntaxToken[] _tokens = [];
    private int _count;

    /// <summary>
    /// Makes room for the positions of a source of <paramref name="length"/> bytes: 0 to <paramref name="length"/>.
    /// </summary>
    public void EnsureSourceLength(int length)
    {
        if (length < _length)
        {
            return;
        }

        var indexes = ArrayPool<int>.Shared.Rent(length + 1);
        Array.Clear(indexes, 0, length + 1);
        Array.Copy(_indexes, indexes, _length);
        Return(_indexes);
        _indexes = indexes;
        _length = length + 1;
    }

    public bool TryGet(int position, out SyntaxToken token)
    {
        var index = _indexes[position] - 1;
        if (index < 0)
        {
            token = default;
            return false;
        }

        token = _tokens[index];
        return true;
    }

    public void Add(int position, SyntaxToken token)
    {
        if (_count == _tokens.Length)
        {
            var tokens = ArrayPool<SyntaxToken>.Shared.Rent(Math.Max(1024, _count * 2));
            Array.Copy(_tokens, tokens, _count);
            Return(_tokens, _count);
            _tokens = tokens;
        }

        _tokens[_count++] = token;
        _indexes[position] = _count;
    }

    public void Release()
    {
        Return(_indexes);
        Return(_tokens, _count);
        _indexes = [];
        _tokens = [];
        _length = 0;
        _count = 0;
    }

    private static void Return(int[] array)
    {
        if (array.Length != 0)
        {
            ArrayPool<int>.Shared.Return(array);
        }
    }

    private static void Return(SyntaxToken[] array, int count)
    {
        if (array.Length != 0)
        {
            // The texts of the tokens are not kept alive by the pool
            Array.Clear(array, 0, count);
            ArrayPool<SyntaxToken>.Shared.Return(array);
        }
    }
}

/// <summary>
/// Runs a <see cref="SyntaxParser"/> rule as a combinator parser. It skips leading trivia and stops after
/// the last token of the rule.
/// </summary>
internal sealed class SyntaxRuleParser<T>(SyntaxParser.Rule<T> rule) : Parser<T>
{
    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        var start = reader.GetCurrentPosition();

        reader.RollBackState(0);
        var source = reader.GetRemaining();
        reader.RollBackState(start);

        var parser = new SyntaxParser(source, start, context);
        var value = rule(ref parser);
        if (value is null)
        {
            return false;
        }

        reader.RollBackState(parser.Position);
        result.Set(start, parser.Position, value);
        return true;
    }
}
