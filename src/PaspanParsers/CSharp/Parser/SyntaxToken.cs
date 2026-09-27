namespace PaspanParsers.CSharp;

internal enum TokenKind : byte
{
    EndOfFile,
    Identifier,
    Keyword,
    Punctuator,
    NumericLiteral,
    CharacterLiteral,
    StringLiteral,
    InterpolatedString,
    Bad,
}

/// <summary>
/// A C# token scanned by <see cref="SyntaxParser"/>. <see cref="Start"/> is the first byte of the token
/// after leading trivia, <see cref="End"/> the byte after it.
/// </summary>
/// <remarks>
/// <see cref="Text"/> is the identifier value (without '@', escapes decoded), the keyword or the punctuator;
/// literal tokens carry their <see cref="Literal"/> node. '&gt;' is always a single token: the expression
/// parser composes '&gt;&gt;', '&gt;&gt;&gt;', '&gt;&gt;=' and '&gt;&gt;&gt;=' from adjacent tokens, so
/// <c>List&lt;List&lt;int&gt;&gt;</c> needs no special case, like in Roslyn.
/// </remarks>
internal readonly struct SyntaxToken(TokenKind kind, int start, int end, string text, Expression literal = null, bool isVerbatim = false)
{
    private SyntaxToken(SyntaxToken token, IReadOnlyList<NullableDirective> nullableDirectives)
        : this(token.Kind, token.Start, token.End, token.Text, token.Literal, token.IsVerbatim)
    {
        NullableDirectives = nullableDirectives;
    }

    public TokenKind Kind { get; } = kind;
    public int Start { get; } = start;
    public int End { get; } = end;
    public string Text { get; } = text;
    public Expression Literal { get; } = literal;

    /// <summary>
    /// The identifier was written with '@' or with a Unicode escape, so it is never a contextual keyword.
    /// </summary>
    public bool IsVerbatim { get; } = isVerbatim;

    /// <summary>
    /// The <c>#nullable</c> directives in the trivia before the token, or null.
    /// </summary>
    public IReadOnlyList<NullableDirective> NullableDirectives { get; }

    public SyntaxToken WithNullableDirectives(IReadOnlyList<NullableDirective> nullableDirectives) => new(this, nullableDirectives);

    public bool IsIdentifier => Kind == TokenKind.Identifier;

    public bool IsPunctuator(string text) => Kind == TokenKind.Punctuator && Text == text;

    public bool IsKeyword(string text) => Kind == TokenKind.Keyword && Text == text;

    /// <summary>
    /// An identifier spelled like the contextual keyword <paramref name="text"/>.
    /// </summary>
    public bool IsContextual(string text) => Kind == TokenKind.Identifier && !IsVerbatim && Text == text;

    public override string ToString() => $"{Kind} '{Text}' [{Start}..{End})";
}
