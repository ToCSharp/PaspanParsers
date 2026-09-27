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

    /// <summary>
    /// True when <paramref name="second"/> directly follows <paramref name="first"/> without trivia.
    /// </summary>
    private static bool AreAdjacent(SyntaxToken first, SyntaxToken second) => first.End == second.Start;

    /// <summary>
    /// Guards the recursion of nested expressions and statements against stack overflow.
    /// </summary>
    private static bool HasSufficientStack() => RuntimeHelpers.TryEnsureSufficientExecutionStack();

    /// <summary>
    /// The position after the token that closes the bracket at <paramref name="open"/>, or -1.
    /// Parentheses, brackets and braces must nest; the result is cached by position.
    /// </summary>
    private int SkipBalanced(int open)
    {
        if (_cache.BalancedEnds.TryGetValue(open, out var end))
        {
            return end;
        }

        end = -1;
        var stack = new Stack<string>();
        var position = open;
        while (true)
        {
            var token = TokenAt(position);
            if (token.Kind == TokenKind.EndOfFile)
            {
                break;
            }

            position = token.End;

            if (token.Kind != TokenKind.Punctuator)
            {
                continue;
            }

            switch (token.Text)
            {
                case "(":
                    stack.Push(")");
                    break;
                case "[":
                    stack.Push("]");
                    break;
                case "{":
                    stack.Push("}");
                    break;
                case ")":
                case "]":
                case "}":
                    if (stack.Count == 0 || stack.Pop() != token.Text)
                    {
                        _cache.BalancedEnds[open] = -1;
                        return -1;
                    }

                    break;
            }

            if (stack.Count == 0)
            {
                end = position;
                break;
            }
        }

        _cache.BalancedEnds[open] = end;
        return end;
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
                var value = Lexer.IdentifierValue(s[..length], isVerbatim, hasEscape);
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
}

/// <summary>
/// Token and lookahead caches shared by all <see cref="SyntaxParser"/> runs over the same input,
/// and the preprocessor that scans the trivia between tokens.
/// </summary>
internal sealed class SyntaxCache(HashSet<string> preprocessorSymbols)
{
    public Dictionary<int, SyntaxToken> Tokens { get; } = [];

    public Dictionary<int, int> BalancedEnds { get; } = [];

    public Preprocessor Preprocessor { get; } = new(preprocessorSymbols);

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
