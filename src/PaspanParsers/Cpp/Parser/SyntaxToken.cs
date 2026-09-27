namespace PaspanParsers.Cpp;

internal enum TokenKind : byte
{
    EndOfFile,
    Identifier,
    Keyword,
    Punctuator,
    NumericLiteral,
    CharacterLiteral,
    StringLiteral,
    Bad,
}

/// <summary>
/// A C++ token scanned by <see cref="SyntaxParser"/>. <see cref="Start"/> is the first byte of the token
/// after leading trivia, <see cref="End"/> the byte after it.
/// </summary>
/// <remarks>
/// <see cref="Text"/> is the identifier, the keyword, the punctuator (digraphs and alternative tokens are
/// replaced with the operators they stand for) or the source text of a literal. '&gt;' is always a single
/// token: the parser composes '&gt;&gt;', '&gt;=' and '&gt;&gt;=' from adjacent tokens, so the '&gt;&gt;'
/// that closes two template argument lists needs no special case.
/// </remarks>
internal readonly struct SyntaxToken(TokenKind kind, int start, int end, string text)
{
    public TokenKind Kind { get; } = kind;
    public int Start { get; } = start;
    public int End { get; } = end;
    public string Text { get; } = text;

    public bool IsIdentifier => Kind == TokenKind.Identifier;

    public bool IsPunctuator(string text) => Kind == TokenKind.Punctuator && Text == text;

    public bool IsKeyword(string text) => Kind == TokenKind.Keyword && Text == text;

    public override string ToString() => $"{Kind} '{Text}' [{Start}..{End})";
}
