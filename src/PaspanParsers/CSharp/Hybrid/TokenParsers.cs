using Paspan;
using Paspan.Fluent;

namespace PaspanParsers.CSharp;

/// <summary>
/// Combinator parsers of single C# tokens for the grammar of <see cref="CSharpHybridParser"/>.
/// </summary>
/// <remarks>
/// They read the tokens that <see cref="SyntaxParser"/> scans and caches by position, so the hybrid grammar
/// and the hand-written rules it calls share one scan of the input. A token parser skips the trivia before
/// its token: the result starts at the token and the reader stops after it. The value is the token, which
/// carries the <c>#nullable</c> directives of the trivia before it.
/// </remarks>
internal static class TokenParsers
{
    public static Parser<SyntaxToken> Punctuator(string text) =>
        text.Length > 1 && text[0] == '>' && text[1] is '>' or '='
            ? new ComposedPunctuatorParser(text)
            : new TokenParser(TokenKind.Punctuator, text);

    /// <summary>
    /// A reserved keyword.
    /// </summary>
    public static Parser<SyntaxToken> Keyword(string text) => new TokenParser(TokenKind.Keyword, text);

    /// <summary>
    /// An identifier spelled like the contextual keyword <paramref name="text"/>, without '@' or escapes.
    /// </summary>
    public static Parser<SyntaxToken> Contextual(string text) => new ContextualParser(text);

    /// <summary>
    /// Any identifier, contextual keywords included.
    /// </summary>
    public static Parser<SyntaxToken> Identifier { get; } = new TokenParser(TokenKind.Identifier, null);

    /// <summary>
    /// A token of any kind that matches <paramref name="predicate"/>.
    /// </summary>
    public static Parser<SyntaxToken> Token(Func<SyntaxToken, bool> predicate) => new PredicateParser(predicate);

    private abstract class SingleTokenParser : Parser<SyntaxToken>
    {
        protected abstract bool Matches(SyntaxToken token);

        public sealed override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<SyntaxToken> result)
        {
            var token = SyntaxParser.CurrentToken(ref reader, context);
            if (!Matches(token))
            {
                return false;
            }

            SyntaxParser.Consume(ref reader, context, token);
            result.Set(token.Start, token.End, token);
            return true;
        }
    }

    private sealed class TokenParser(TokenKind kind, string text) : SingleTokenParser
    {
        protected override bool Matches(SyntaxToken token) => token.Kind == kind && (text == null || token.Text == text);

        public override string ToString() => text ?? kind.ToString();
    }

    private sealed class ContextualParser(string text) : SingleTokenParser
    {
        protected override bool Matches(SyntaxToken token) => token.IsContextual(text);

        public override string ToString() => text;
    }

    private sealed class PredicateParser(Func<SyntaxToken, bool> predicate) : SingleTokenParser
    {
        protected override bool Matches(SyntaxToken token) => predicate(token);
    }

    /// <summary>
    /// '&gt;&gt;', '&gt;&gt;&gt;', '&gt;&gt;=' and '&gt;&gt;&gt;=': '&gt;' is always a token of its own
    /// (see <see cref="SyntaxToken"/>), so these are adjacent tokens without trivia between them.
    /// </summary>
    private sealed class ComposedPunctuatorParser(string text) : Parser<SyntaxToken>
    {
        public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<SyntaxToken> result)
        {
            var parser = SyntaxParser.At(ref reader, context);
            var first = parser.Current;
            var last = first;
            var matched = 0;

            for (var offset = 0; matched < text.Length; offset++)
            {
                var token = parser.Peek(offset);
                if (token.Kind != TokenKind.Punctuator
                    || (offset > 0 && !SyntaxParser.AreAdjacent(last, token))
                    || matched + token.Text.Length > text.Length
                    || string.CompareOrdinal(text, matched, token.Text, 0, token.Text.Length) != 0)
                {
                    return false;
                }

                matched += token.Text.Length;
                last = token;
            }

            while (parser.Position < last.End)
            {
                parser.EatToken();
            }

            reader.RollBackState(last.End);
            result.Set(first.Start, last.End, new SyntaxToken(TokenKind.Punctuator, first.Start, last.End, text).WithNullableDirectives(first.NullableDirectives));
            return true;
        }

        public override string ToString() => text;
    }
}
