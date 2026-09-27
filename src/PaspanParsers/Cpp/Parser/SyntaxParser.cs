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

    public SyntaxParser(ReadOnlySpan<byte> source, int position, ParseContext context)
    {
        _source = source;
        _position = position;
        _cache = SyntaxCache.For(context);
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
        if (_cache.Tokens.TryGetValue(position, out var token))
        {
            return token;
        }

        token = ScanToken(position);
        _cache.Tokens[position] = token;
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

        length = Lexer.ScanQuotedLiteral(s, out var isString);
        if (length > 0)
        {
            Lexer.ScanLiteralPrefix(s, out isRawString);
            text = Encoding.UTF8.GetString(s[..length]);
            return isString ? TokenKind.StringLiteral : TokenKind.CharacterLiteral;
        }

        length = Lexer.ScanNumber(s, out _);
        if (length > 0)
        {
            text = Encoding.UTF8.GetString(s[..length]);
            return TokenKind.NumericLiteral;
        }

        length = Lexer.ScanIdentifier(s);
        if (length > 0)
        {
            var identifier = s[..length];
            text = identifier.IndexOfAnyExceptInRange((byte)0, (byte)0x7F) < 0 && identifier.IndexOf((byte)'\\') < 0
                ? _cache.Intern(identifier)
                : Lexer.IdentifierValue(identifier);

            if (Lexer.AlternativeTokens.TryGetValue(text, out var @operator))
            {
                text = @operator;
                return TokenKind.Punctuator;
            }

            return Lexer.Keywords.Contains(text) ? TokenKind.Keyword : TokenKind.Identifier;
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
        (error.Line, error.Column) = new SpanReader(source).GetLineAndColumn(token.Start);
        return error;
    }
}

/// <summary>
/// Token caches shared by all <see cref="SyntaxParser"/> runs over the same input.
/// </summary>
internal sealed class SyntaxCache(CppParseOptions options)
{
    public Dictionary<int, SyntaxToken> Tokens { get; } = [];

    /// <summary>
    /// The names declared so far.
    /// </summary>
    public Symbols Symbols { get; } = new(options);

    /// <summary>
    /// The positions of '&lt;' where template arguments failed to parse, with the state they were parsed in.
    /// </summary>
    public HashSet<(int Position, int Symbols, bool InTemplateArguments, bool InConstraint)> FailedTemplateArguments { get; } = [];

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

    /// <summary>
    /// The value of an identifier or keyword; ASCII names get one string per distinct name.
    /// </summary>
    public string Intern(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length > 256 || utf8.IndexOfAnyExceptInRange((byte)0, (byte)0x7F) >= 0)
        {
            return Encoding.UTF8.GetString(utf8);
        }

        Span<char> chars = stackalloc char[utf8.Length];
        for (var i = 0; i < utf8.Length; i++)
        {
            chars[i] = (char)utf8[i];
        }

        var lookup = _strings.GetAlternateLookup<ReadOnlySpan<char>>();
        if (!lookup.TryGetValue(chars, out var text))
        {
            text = new string(chars);
            _strings.Add(text);
        }

        return text;
    }

    public static SyntaxCache For(ParseContext context)
    {
        // A CppParseContext is created for one input; any other context may be reused
        // for different inputs, so its runs do not share caches.
        return context is CppParseContext cpp ? cpp.SyntaxCache : new SyntaxCache(CppParseOptions.Default);
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
