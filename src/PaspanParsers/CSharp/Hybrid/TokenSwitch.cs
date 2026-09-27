using Paspan;
using Paspan.Fluent;

namespace PaspanParsers.CSharp;

/// <summary>
/// A condition on the tokens at the current position, checked on a copy of the parser: moving it has no effect.
/// </summary>
internal delegate bool TokenCondition(ref SyntaxParser parser);

/// <summary>
/// Chooses the alternatives to try by the next token, instead of trying every alternative in turn like
/// <c>Or</c>. The alternatives registered for the token are tried first, in the order they were added, then
/// those registered for its kind, then the <see cref="Otherwise"/> ones; the first that succeeds wins.
/// </summary>
/// <remarks>
/// An alternative with a <c>when</c> condition is only tried when the condition holds at the next token.
/// By default an alternative that fails lets the next ones be tried, like <c>Or</c>. A committed alternative
/// (<c>commit: true</c>) decides the choice: when it fails, the switch fails, like the dispatch of the
/// hand-written parser once it has recognized a construct by its first tokens.
/// </remarks>
internal sealed class TokenSwitch<T> : Parser<T>
{
    private readonly record struct Alternative(Parser<T> Parser, TokenCondition When, bool Commit);

    private readonly Dictionary<(TokenKind Kind, string Text), List<Alternative>> _byToken = [];
    private readonly Dictionary<TokenKind, List<Alternative>> _byKind = [];
    private readonly List<Alternative> _otherwise = [];

    public TokenSwitch<T> OnPunctuator(string text, Parser<T> parser, TokenCondition when = null, bool commit = false) =>
        On((TokenKind.Punctuator, text), new Alternative(parser, when, commit));

    public TokenSwitch<T> OnKeyword(string text, Parser<T> parser, TokenCondition when = null, bool commit = false) =>
        On((TokenKind.Keyword, text), new Alternative(parser, when, commit));

    /// <summary>
    /// An alternative that starts with the contextual keyword <paramref name="text"/> (an identifier
    /// without '@' or escapes).
    /// </summary>
    public TokenSwitch<T> OnContextual(string text, Parser<T> parser, TokenCondition when = null, bool commit = false) =>
        On((TokenKind.Identifier, text), new Alternative(parser, when, commit));

    /// <summary>
    /// An alternative that starts with any token of <paramref name="kind"/>.
    /// </summary>
    public TokenSwitch<T> OnKind(TokenKind kind, Parser<T> parser, TokenCondition when = null, bool commit = false)
    {
        if (!_byKind.TryGetValue(kind, out var alternatives))
        {
            _byKind[kind] = alternatives = [];
        }

        alternatives.Add(new Alternative(parser, when, commit));
        return this;
    }

    /// <summary>
    /// An alternative tried for every token, after the ones chosen by the token.
    /// </summary>
    public TokenSwitch<T> Otherwise(Parser<T> parser)
    {
        _otherwise.Add(new Alternative(parser, null, false));
        return this;
    }

    private TokenSwitch<T> On((TokenKind, string) key, Alternative alternative)
    {
        if (!_byToken.TryGetValue(key, out var alternatives))
        {
            _byToken[key] = alternatives = [];
        }

        alternatives.Add(alternative);
        return this;
    }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        var token = SyntaxParser.CurrentToken(ref reader, context);

        // Contextual keywords written with '@' or escapes are plain identifiers
        if ((token.Kind != TokenKind.Identifier || !token.IsVerbatim) && _byToken.TryGetValue((token.Kind, token.Text), out var byToken))
        {
            var outcome = TryParse(byToken, ref reader, context, ref result);
            if (outcome.HasValue)
            {
                return outcome.Value;
            }
        }

        if (_byKind.TryGetValue(token.Kind, out var byKind))
        {
            var outcome = TryParse(byKind, ref reader, context, ref result);
            if (outcome.HasValue)
            {
                return outcome.Value;
            }
        }

        return TryParse(_otherwise, ref reader, context, ref result) ?? false;
    }

    /// <summary>
    /// True when an alternative succeeded, false when a committed one failed, null to go on with the next ones.
    /// </summary>
    private static bool? TryParse(List<Alternative> alternatives, ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        var start = reader.GetCurrentPosition();
        foreach (var alternative in alternatives)
        {
            // A condition looks ahead on a parser of its own: moving it has no effect
            if (alternative.When != null)
            {
                var parser = SyntaxParser.At(ref reader, context);
                if (!alternative.When(ref parser))
                {
                    continue;
                }
            }

            if (alternative.Parser.Parse(ref reader, context, ref result))
            {
                return true;
            }

            reader.RollBackState(start);
            if (alternative.Commit)
            {
                return false;
            }
        }

        return null;
    }
}
