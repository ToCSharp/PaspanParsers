using Paspan;
using Paspan.Fluent;

namespace PaspanParsers.CSharp;

/// <summary>
/// Chooses the alternatives to try by the next token, instead of trying every alternative in turn like
/// <c>Or</c>. The alternatives registered for the token are tried first, in the order they were added, then
/// those registered for its kind, then the <see cref="Otherwise"/> ones; the first that succeeds wins.
/// </summary>
/// <remarks>
/// Falling through to the next alternatives when the chosen ones fail keeps the semantics of <c>Or</c> over
/// all of them: an alternative is only skipped when it cannot start with the next token.
/// </remarks>
internal sealed class TokenSwitch<T> : Parser<T>
{
    private readonly Dictionary<(TokenKind Kind, string Text), List<Parser<T>>> _byToken = [];
    private readonly Dictionary<TokenKind, List<Parser<T>>> _byKind = [];
    private readonly List<Parser<T>> _otherwise = [];

    public TokenSwitch<T> OnPunctuator(string text, Parser<T> parser) => On((TokenKind.Punctuator, text), parser);

    public TokenSwitch<T> OnKeyword(string text, Parser<T> parser) => On((TokenKind.Keyword, text), parser);

    /// <summary>
    /// An alternative that starts with the contextual keyword <paramref name="text"/> (an identifier
    /// without '@' or escapes).
    /// </summary>
    public TokenSwitch<T> OnContextual(string text, Parser<T> parser) => On((TokenKind.Identifier, text), parser);

    /// <summary>
    /// An alternative that starts with any token of <paramref name="kind"/>.
    /// </summary>
    public TokenSwitch<T> OnKind(TokenKind kind, Parser<T> parser)
    {
        if (!_byKind.TryGetValue(kind, out var parsers))
        {
            _byKind[kind] = parsers = [];
        }

        parsers.Add(parser);
        return this;
    }

    /// <summary>
    /// An alternative tried for every token, after the ones chosen by the token.
    /// </summary>
    public TokenSwitch<T> Otherwise(Parser<T> parser)
    {
        _otherwise.Add(parser);
        return this;
    }

    private TokenSwitch<T> On((TokenKind, string) key, Parser<T> parser)
    {
        if (!_byToken.TryGetValue(key, out var parsers))
        {
            _byToken[key] = parsers = [];
        }

        parsers.Add(parser);
        return this;
    }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        var token = SyntaxParser.At(ref reader, context).Current;

        // Contextual keywords written with '@' or escapes are plain identifiers
        if ((token.Kind != TokenKind.Identifier || !token.IsVerbatim)
            && _byToken.TryGetValue((token.Kind, token.Text), out var byToken)
            && TryParse(byToken, ref reader, context, ref result))
        {
            return true;
        }

        if (_byKind.TryGetValue(token.Kind, out var byKind) && TryParse(byKind, ref reader, context, ref result))
        {
            return true;
        }

        return TryParse(_otherwise, ref reader, context, ref result);
    }

    private static bool TryParse(List<Parser<T>> parsers, ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        var start = reader.GetCurrentPosition();
        foreach (var parser in parsers)
        {
            if (parser.Parse(ref reader, context, ref result))
            {
                return true;
            }

            reader.RollBackState(start);
        }

        return false;
    }
}
