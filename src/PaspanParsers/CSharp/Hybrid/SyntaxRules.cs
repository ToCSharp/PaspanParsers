using System.Runtime.CompilerServices;
using Paspan;
using Paspan.Fluent;

namespace PaspanParsers.CSharp;

/// <summary>
/// Building blocks of the hybrid grammar (<see cref="CSharpHybridParser"/>) that the core combinators do
/// not provide: recursion guarded against stack overflow, node spans, and the parser state of blocks.
/// </summary>
internal static class SyntaxRules
{
    /// <summary>
    /// Sets the span of the node <paramref name="parser"/> returns, like <c>SyntaxParser.Finish</c>: from the
    /// start of the first token to the end of the last consumed token.
    /// </summary>
    public static Parser<T> Node<T>(Parser<T> parser) where T : CSharpNode => new NodeParser<T>(parser);

    /// <summary>
    /// A block resets the state that only holds inside an expression, like <c>SyntaxParser.ParseBlock</c>:
    /// query keywords are not reserved and <c>x =&gt; ...</c> is a lambda again.
    /// </summary>
    public static Parser<T> BlockScope<T>(Parser<T> parser) => new BlockScopeParser<T>(parser);

    private sealed class NodeParser<T>(Parser<T> parser) : Parser<T> where T : CSharpNode
    {
        public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
        {
            var start = SyntaxParser.At(ref reader, context).Current.Start;
            if (!parser.Parse(ref reader, context, ref result))
            {
                return false;
            }

            // A node without tokens is empty at its start
            var end = Math.Max(start, reader.GetCurrentPosition());
            result.Value.Span = new TextSpan(start, end);
            result.Set(start, end, result.Value);
            return true;
        }
    }

    private sealed class BlockScopeParser<T>(Parser<T> parser) : Parser<T>
    {
        public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
        {
            if (context is not CSharpParseContext csharp)
            {
                return parser.Parse(ref reader, context, ref result);
            }

            var outer = csharp.SyntaxState;
            csharp.SyntaxState = outer with { QueryDepth = 0, NoLambdaArrow = false };
            try
            {
                return parser.Parse(ref reader, context, ref result);
            }
            finally
            {
                csharp.SyntaxState = outer;
            }
        }
    }
}

/// <summary>
/// A recursive rule of the hybrid grammar, like <see cref="Deferred{T}"/>, guarded against stack overflow
/// like the hand-written parser: it throws <see cref="InsufficientExecutionStackException"/>, and
/// <see cref="CSharpHybridParser"/> parses again on a larger stack. It has no loop detection: the grammar is
/// not left-recursive, since every rule consumes a token before it recurses.
/// </summary>
internal sealed class RecursiveRule<T> : Parser<T>
{
    public Parser<T> Parser { get; set; }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        return Parser.Parse(ref reader, context, ref result);
    }
}
