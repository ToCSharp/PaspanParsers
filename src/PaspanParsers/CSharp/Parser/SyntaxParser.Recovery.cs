using System.Text;

namespace PaspanParsers.CSharp;

// Error recovery (CSharpParseOptions.ErrorRecovery): invalid members and statements are skipped
// and kept as incomplete nodes, so the rest of the input still parses.
internal ref partial struct SyntaxParser
{
    /// <summary>
    /// True when the parse recovers from errors instead of failing.
    /// </summary>
    private readonly bool RecoversErrors => _context is CSharpParseContext { Options.ErrorRecovery: true };

    /// <summary>
    /// Starts an attempt that may be recovered from: resets the furthest position, so that a failure
    /// is reported where this attempt stopped, and returns the value to pass to <see cref="EndAttempt"/>.
    /// </summary>
    private readonly int BeginAttempt()
    {
        var furthest = _cache.FurthestPosition;
        _cache.FurthestPosition = _position;
        return furthest;
    }

    /// <summary>
    /// Ends an attempt started by <see cref="BeginAttempt"/> and returns where it stopped.
    /// </summary>
    private readonly int EndAttempt(int previousFurthest)
    {
        var furthest = _cache.FurthestPosition;
        _cache.FurthestPosition = Math.Max(previousFurthest, furthest);
        return furthest;
    }

    /// <summary>
    /// Parses a member declaration of a body; with error recovery, skips an invalid one and returns an
    /// <see cref="IncompleteMemberDeclaration"/>. <paramref name="inBraces"/> tells whether a '}' closes the body.
    /// </summary>
    private MemberDeclaration ParseMemberDeclarationOrRecover(MemberContext context, bool inBraces)
    {
        if (!RecoversErrors)
        {
            return ParseMemberDeclaration(context);
        }

        var start = _position;
        var previousFurthest = BeginAttempt();
        var member = ParseMemberDeclaration(context);
        var failedAt = EndAttempt(previousFurthest);
        if (member != null)
        {
            return member;
        }

        var spanStart = SkipInvalid(start, inBraces, failedAt, isStatement: false, out var text);
        return Finish(new IncompleteMemberDeclaration(text), spanStart);
    }

    /// <summary>
    /// Parses a statement of a block; with error recovery, skips an invalid one and returns an
    /// <see cref="IncompleteStatement"/>.
    /// </summary>
    private Statement ParseStatementOrRecover()
    {
        if (!RecoversErrors)
        {
            return ParseStatement();
        }

        var start = _position;
        var previousFurthest = BeginAttempt();
        var statement = ParseStatement();
        var failedAt = EndAttempt(previousFurthest);
        if (statement != null)
        {
            return statement;
        }

        var spanStart = SkipInvalid(start, inBraces: true, failedAt, isStatement: true, out var text);
        return Finish(new IncompleteStatement(text), spanStart);
    }

    /// <summary>
    /// With error recovery, true at the end of the input inside a body that '}' should close: records
    /// the missing '}' and lets the body end there. Without it, the body fails as before.
    /// </summary>
    private bool IsMissingCloseBrace()
    {
        if (Current.Kind != TokenKind.EndOfFile || !RecoversErrors)
        {
            return false;
        }

        AddError(Current, "'}' expected");
        return true;
    }

    /// <summary>
    /// Keywords that start a member declaration and never occur inside one: skipping a member stops there.
    /// </summary>
    private static readonly HashSet<string> MemberStartKeywords =
        ["class", "struct", "interface", "enum", "namespace", "public", "private", "protected", "internal"];

    /// <summary>
    /// Keywords that start a statement and never occur inside an expression: skipping a statement stops there.
    /// </summary>
    private static readonly HashSet<string> StatementStartKeywords =
        ["if", "for", "foreach", "while", "do", "return", "try", "break", "continue", "using", "lock", "goto", "fixed", "unsafe"];

    /// <summary>
    /// Skips invalid source from <paramref name="start"/>: tokens up to and including a ';' or a balanced
    /// '{...}' block, stepping over balanced parentheses and brackets. Stops before a '}' that closes the
    /// enclosing body (when <paramref name="inBraces"/>), before a keyword that starts the next member or,
    /// for a statement, the next statement, and at the end of the input. Consumes at least one token when
    /// there is one to consume. Records the error at the token after <paramref name="failedAt"/> and returns
    /// the start of the skipped span.
    /// </summary>
    private int SkipInvalid(int start, bool inBraces, int failedAt, bool isStatement, out string text)
    {
        var errorToken = TokenAt(Math.Max(start, failedAt));
        AddError(errorToken, errorToken.Kind == TokenKind.EndOfFile ? "Unexpected end of file" : $"Unexpected {Describe(errorToken)}");

        _position = start;
        var spanStart = NodeStart;

        // The braces opened in the skipped source, and the text of the previous skipped token
        var depth = 0;
        var previous = "";
        while (true)
        {
            var token = Current;
            if (token.Kind == TokenKind.EndOfFile)
            {
                break;
            }

            // The next member starts here, even inside braces left open (except 'where T : class'), and at
            // the level of the skipped statement the next statement
            if (token.Kind == TokenKind.Keyword && _position != start
                && ((MemberStartKeywords.Contains(token.Text) && previous is not (":" or ","))
                    || (isStatement && depth == 0 && StatementStartKeywords.Contains(token.Text))))
            {
                break;
            }

            previous = token.Kind == TokenKind.Punctuator ? token.Text : "";

            if (token.Kind == TokenKind.Punctuator)
            {
                switch (token.Text)
                {
                    case "{":
                        depth++;
                        EatToken();
                        continue;

                    case "}" when depth > 0:
                        depth--;
                        EatToken();
                        if (depth == 0)
                        {
                            // A balanced block ends the skipped source
                            break;
                        }

                        continue;

                    case "}":
                        // The '}' of the enclosing body ends the skipped source; outside any body it is stray
                        if (!inBraces)
                        {
                            EatToken();
                        }

                        break;

                    case ";":
                        EatToken();
                        if (depth == 0)
                        {
                            break;
                        }

                        continue;

                    case "(":
                    case "[":
                        var end = SkipBalanced(_position);
                        if (end < 0)
                        {
                            // Not closed: step over the bracket alone
                            EatToken();
                        }
                        else
                        {
                            _position = end;
                        }

                        continue;

                    default:
                        EatToken();
                        continue;
                }

                break;
            }

            EatToken();
        }

        // The position is the end of the last skipped token
        var spanEnd = Math.Max(spanStart, _position);
        text = Encoding.UTF8.GetString(_source[spanStart..spanEnd]);
        return spanStart;
    }

    private readonly void AddError(SyntaxToken token, string message)
    {
        if (_context is CSharpParseContext csharp)
        {
            csharp.Errors.Add(new SyntaxError(new TextSpan(token.Start, token.End), message));
        }
    }

    /// <summary>
    /// The token as it reads in an error message.
    /// </summary>
    private readonly string Describe(SyntaxToken token)
    {
        var text = Encoding.UTF8.GetString(_source[token.Start..token.End]).ReplaceLineEndings(" ");
        return $"'{(text.Length <= 40 ? text : text[..37] + "...")}'";
    }
}
