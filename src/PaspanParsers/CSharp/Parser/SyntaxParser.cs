using System.Runtime.CompilerServices;
using System.Text;
using Paspan;
using Paspan.Fluent;

namespace PaspanParsers.CSharp;

/// <summary>
/// Hand-written recursive descent parser for C#: names, types, expressions, patterns, statements,
/// declarations and the compilation unit.
/// </summary>
/// <remarks>
/// These parts of the grammar are mutually recursive (lambdas hold blocks, patterns hold types and
/// expressions, the top level mixes declarations and statements) and need Roslyn's disambiguation rules
/// (generic names, casts, lambdas, declarations), which rely on looking ahead over whole types. They are
/// parsed here on top of <see cref="Lexer"/> and <see cref="Preprocessor"/>, and run as a combinator
/// parser through <see cref="SyntaxRuleParser{T}"/>.
/// <para>
/// Parse methods return null when the input does not match; the position is then unspecified and
/// callers that try alternatives restore it. Tokens are scanned lazily and cached by position.
/// </para>
/// </remarks>
internal ref partial struct SyntaxParser
{
    private readonly ReadOnlySpan<byte> _source;
    private readonly ParseContext _context;
    private readonly SyntaxCache _cache;
    private int _position;

    // Query clauses make their contextual keywords reserved inside the query
    private int _queryDepth;

    // Inside a 'when' clause of a switch expression arm, 'x => ...' ends the clause instead of being a lambda
    private bool _noLambdaArrow;

    // typeof and nameof accept unbound generic types: typeof(Dictionary<,>)
    private bool _allowOmittedTypeArguments;

    public SyntaxParser(ReadOnlySpan<byte> source, int position, ParseContext context)
    {
        _source = source;
        _position = position;
        _context = context;
        _cache = SyntaxCache.For(context);

        // About one token per ten bytes of source code; sizing the cache up front avoids rehashing it
        if (_cache.Tokens.Count == 0)
        {
            _cache.Tokens.EnsureCapacity((source.Length - position) / 9);
        }
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

    private bool IsContextual(string text) => Current.IsContextual(text);

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

    private bool TryEatContextual(string text)
    {
        if (Current.IsContextual(text))
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
    private readonly T Finish<T>(T node, int start) where T : CSharpNode
    {
        if (node != null)
        {
            // A node without tokens (an omitted type argument) is empty at its start
            node.Span = new TextSpan(start, Math.Max(start, _position));
        }

        return node;
    }

    /// <summary>
    /// Sets the span of <paramref name="node"/> and returns the node.
    /// </summary>
    private static T Finish<T>(T node, int start, int end) where T : CSharpNode
    {
        node.Span = new TextSpan(start, end);
        return node;
    }

    /// <summary>
    /// Like <see cref="Finish{T}(T, int)"/> for a node that starts where <paramref name="first"/> starts.
    /// </summary>
    private readonly T Finish<T>(T node, CSharpNode first) where T : CSharpNode => Finish(node, first.Span.Start);

    /// <summary>
    /// True when <paramref name="second"/> directly follows <paramref name="first"/> without trivia.
    /// </summary>
    private static bool AreAdjacent(SyntaxToken first, SyntaxToken second) => first.End == second.Start;

    /// <summary>
    /// Guards the recursion of nested constructs against stack overflow: throws
    /// <see cref="InsufficientExecutionStackException"/>, which <see cref="CSharpParser.TryParse(string, CSharpParseOptions, out CompilationUnit, out ParseError)"/>
    /// handles by parsing again on a larger stack. Failing the whole parse, rather than the current
    /// alternative, keeps the parser from choosing another reading of the input.
    /// </summary>
    private static void EnsureSufficientStack() => RuntimeHelpers.EnsureSufficientExecutionStack();

    /// <summary>
    /// The position after the token that closes the bracket at <paramref name="open"/>, or -1.
    /// Parentheses, brackets and braces must nest. The scan also records the ends of the brackets
    /// nested inside, so looking ahead at each level of deeply nested brackets stays linear.
    /// </summary>
    private int SkipBalanced(int open)
    {
        if (_cache.BalancedEnds.TryGetValue(open, out var end))
        {
            return end;
        }

        // The positions of the open brackets and the closing text they expect
        var stack = new Stack<(int Open, string Close)>();
        var position = open;
        while (true)
        {
            var token = TokenAt(position);
            if (token.Kind == TokenKind.EndOfFile)
            {
                break;
            }

            var tokenPosition = position;
            position = token.End;

            if (token.Kind != TokenKind.Punctuator)
            {
                continue;
            }

            switch (token.Text)
            {
                case "(":
                    stack.Push((tokenPosition, ")"));
                    break;
                case "[":
                    stack.Push((tokenPosition, "]"));
                    break;
                case "{":
                    stack.Push((tokenPosition, "}"));
                    break;
                case ")":
                case "]":
                case "}":
                    if (stack.Count == 0 || stack.Peek().Close != token.Text)
                    {
                        // Brackets still open here have no matching close
                        foreach (var (unclosed, _) in stack)
                        {
                            _cache.BalancedEnds[unclosed] = -1;
                        }

                        _cache.BalancedEnds[open] = -1;
                        return -1;
                    }

                    _cache.BalancedEnds[stack.Pop().Open] = position;
                    break;
            }

            if (stack.Count == 0)
            {
                return position;
            }
        }

        foreach (var (unclosed, _) in stack)
        {
            _cache.BalancedEnds[unclosed] = -1;
        }

        _cache.BalancedEnds[open] = -1;
        return -1;
    }

    // ========================================
    // Scanning
    // ========================================

    private static readonly string[] Punctuators =
    [
        // Longest first; '>>', '>>=', '>>>' and '>>>=' are composed by the parser
        "<<=", "??=",
        "::", "++", "--", "&&", "||", "->", "==", "!=", "<=", ">=", "+=", "-=", "*=", "/=", "%=",
        "&=", "|=", "^=", "<<", "=>", "??", "..",
        "{", "}", "[", "]", "(", ")", ".", ",", ":", ";", "+", "-", "*", "/", "%", "&", "|", "^",
        "!", "~", "=", "<", ">", "?",
    ];

    private static readonly byte[][] PunctuatorBytes = Punctuators.Select(Encoding.UTF8.GetBytes).ToArray();

    private SyntaxToken ScanToken(int position)
    {
        var start = position + _cache.Preprocessor.ScanTrivia(_source, position, out var nullableDirectives);
        var token = ScanTokenAt(start);
        return nullableDirectives == null ? token : token.WithNullableDirectives(nullableDirectives);
    }

    /// <summary>
    /// Scans the token at <paramref name="start"/>, after the trivia.
    /// </summary>
    private SyntaxToken ScanTokenAt(int start)
    {
        if (start >= _source.Length)
        {
            return new SyntaxToken(TokenKind.EndOfFile, _source.Length, _source.Length, "");
        }

        var s = _source[start..];
        var b = s[0];
        var next = s.Length > 1 ? s[1] : (byte)0;

        if (b == '$' || (b == '@' && next == '$'))
        {
            return ScanInterpolatedString(start);
        }

        if (b == '"' || (b == '@' && next == '"'))
        {
            var length = Lexer.ScanStringLiteral(s, out var value, out var isUtf8);
            if (length == 0)
            {
                return Bad(start);
            }

            var text = Encoding.UTF8.GetString(s[..length]);
            var literal = isUtf8
                ? new LiteralExpression(Encoding.UTF8.GetBytes(value), LiteralKind.Utf8String, text)
                : new LiteralExpression(value, LiteralKind.String, text);
            return new SyntaxToken(TokenKind.StringLiteral, start, start + length, text, literal);
        }

        if (b == '\'')
        {
            var length = Lexer.ScanCharacterLiteral(s, out var value);
            if (length == 0)
            {
                return Bad(start);
            }

            var text = Encoding.UTF8.GetString(s[..length]);
            return new SyntaxToken(TokenKind.CharacterLiteral, start, start + length, text, new LiteralExpression(value, LiteralKind.Character, text));
        }

        if (Lexer.IsDecimalDigit(b) || (b == '.' && Lexer.IsDecimalDigit(next)))
        {
            var length = Lexer.ScanNumericLiteral(s, out var value, out var kind);
            if (length == 0)
            {
                return Bad(start);
            }

            var text = Encoding.UTF8.GetString(s[..length]);
            return new SyntaxToken(TokenKind.NumericLiteral, start, start + length, text, new LiteralExpression(value, kind, text));
        }

        {
            var length = Lexer.ScanIdentifierOrKeyword(s, out var isVerbatim, out var hasEscape);
            if (length > 0)
            {
                var value = isVerbatim || hasEscape ? Lexer.IdentifierValue(s[..length], isVerbatim, hasEscape) : _cache.Intern(s[..length]);
                var isKeyword = !isVerbatim && !hasEscape && Lexer.ReservedKeywords.Contains(value);
                return new SyntaxToken(isKeyword ? TokenKind.Keyword : TokenKind.Identifier, start, start + length, value, null, isVerbatim || hasEscape);
            }
        }

        for (var i = 0; i < PunctuatorBytes.Length; i++)
        {
            if (s.StartsWith(PunctuatorBytes[i]))
            {
                return new SyntaxToken(TokenKind.Punctuator, start, start + PunctuatorBytes[i].Length, Punctuators[i]);
            }
        }

        return Bad(start);
    }

    private static SyntaxToken Bad(int start) => new(TokenKind.Bad, start, start + 1, "");

    private SyntaxToken ScanInterpolatedString(int start)
    {
        var reader = new SpanReader(_source);
        reader.RollBackState(start);

        var result = new ParseResult<Expression>();
        if (!InterpolatedStringParser.Parse(ref reader, _context, ref result))
        {
            return Bad(start);
        }

        var end = reader.GetCurrentPosition();
        return new SyntaxToken(TokenKind.InterpolatedString, start, end, "", result.Value);
    }

    private static readonly InterpolatedStringToken InterpolatedStringParser = new(new SyntaxRuleParser<Expression>(ParseExpressionRule));

    // ========================================
    // Entry points run as combinator parsers
    // ========================================

    public delegate T Rule<T>(ref SyntaxParser parser);

    public static CompilationUnit ParseCompilationUnitRule(ref SyntaxParser parser) => parser.ParseCompilationUnit();

    /// <summary>
    /// The expressions in the holes of interpolated strings, which are scanned by <see cref="InterpolatedStringToken"/>.
    /// </summary>
    public static Expression ParseExpressionRule(ref SyntaxParser parser) => parser.ParseExpression();

    /// <summary>
    /// Scans the tokens of <paramref name="source"/> one after another, with the trivia before each of them,
    /// and returns their number. Measures the scanner apart from the parser.
    /// </summary>
    public static int ScanTokens(ReadOnlySpan<byte> source, ParseContext context)
    {
        var parser = new SyntaxParser(source, 0, context);
        var count = 0;
        while (parser.EatToken().Kind != TokenKind.EndOfFile)
        {
            count++;
        }

        return count;
    }

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
/// Token and lookahead caches shared by all <see cref="SyntaxParser"/> runs over the same input,
/// and the preprocessor that scans the trivia between tokens.
/// </summary>
internal sealed class SyntaxCache(HashSet<string> preprocessorSymbols)
{
    /// <summary>
    /// The largest token cache kept for reuse by the next parse on the thread (about 4 MB).
    /// </summary>
    private const int MaxPooledTokens = 1 << 16;

    [ThreadStatic]
    private static Dictionary<int, SyntaxToken> s_pooledTokens;

    public Dictionary<int, SyntaxToken> Tokens { get; } = RentTokens();

    public Dictionary<int, int> BalancedEnds { get; } = [];

    /// <summary>
    /// The end of the furthest token consumed, for the error position of a failed parse.
    /// </summary>
    public int FurthestPosition { get; set; }

    /// <summary>
    /// Tuple types by the position of their '(': the type and the position after it, or a null type where
    /// no tuple type starts. Without and with omitted type arguments (<c>typeof(Dictionary&lt;,&gt;)</c>).
    /// </summary>
    public Dictionary<int, (TypeReference Type, int End)> TupleTypes { get; } = [];

    public Dictionary<int, (TypeReference Type, int End)> TupleTypesWithOmittedArguments { get; } = [];

    public Preprocessor Preprocessor { get; } = new(preprocessorSymbols);

    private readonly HashSet<string> _strings = new(StringComparer.Ordinal);

    /// <summary>
    /// The value of an identifier or keyword written without '@' and escapes; ASCII names get one string per distinct name.
    /// </summary>
    public string Intern(ReadOnlySpan<byte> utf8)
    {
        if (utf8.Length > 256 || utf8.IndexOfAnyExceptInRange((byte)0, (byte)0x7F) >= 0)
        {
            // Non-ASCII names drop formatting characters
            return Lexer.IdentifierValue(utf8, isVerbatim: false, hasEscape: false);
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

    /// <summary>
    /// Called when the parse is over and the cache will not be used again: the token cache, the
    /// largest allocation of a parse, is kept for the next parse on this thread.
    /// </summary>
    public void Release()
    {
        if (Tokens.Capacity <= MaxPooledTokens)
        {
            Tokens.Clear();
            s_pooledTokens = Tokens;
        }
    }

    private static Dictionary<int, SyntaxToken> RentTokens()
    {
        var tokens = s_pooledTokens ?? [];
        s_pooledTokens = null;
        return tokens;
    }

    public static SyntaxCache For(ParseContext context)
    {
        // A CSharpParseContext is created for one input; any other context may be reused
        // for different inputs, so its runs do not share caches.
        return context is CSharpParseContext csharp ? csharp.SyntaxCache : new SyntaxCache(new HashSet<string>(StringComparer.Ordinal));
    }
}

/// <summary>
/// Runs a <see cref="SyntaxParser"/> rule as a combinator parser. Like other token parsers it
/// skips leading trivia and stops after the last token of the rule.
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
