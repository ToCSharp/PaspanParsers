using Paspan;
using Paspan.Fluent;

namespace PaspanParsers.CSharp;

/// <summary>
/// C# parser. The hand-written <see cref="SyntaxParser"/> (partial files under <c>CSharp/Parser/</c>) parses
/// the compilation unit; it is wired in here as a combinator parser that also skips the trivia around it.
/// </summary>
public partial class CSharpParser
{
    /// <summary>
    /// The stack of the thread that parses input nested too deeply for the caller's stack.
    /// </summary>
    private const int LargeStackSize = 256 * 1024 * 1024;

    public static readonly Parser<CompilationUnit> CompilationUnitParser =
        WithTrivia(new SyntaxRuleParser<CompilationUnit>(SyntaxParser.ParseCompilationUnitRule));

    public static CompilationUnit Parse(string input, CSharpParseOptions options = null)
    {
        return TryParse(input, options, out var result, out _) ? result : null;
    }

    public static bool TryParse(string input, out CompilationUnit result, out ParseError error)
    {
        return TryParse(input, null, out result, out error);
    }

    /// <summary>
    /// Parses <paramref name="input"/>. When it is not valid C#, returns false and an <paramref name="error"/>
    /// at the token after the furthest token the parser could consume.
    /// </summary>
    public static bool TryParse(string input, CSharpParseOptions options, out CompilationUnit result, out ParseError error)
    {
        return TryParse(input, options, CompilationUnitParser, blockParser: null, out result, out error);
    }

    /// <summary>
    /// Parses <paramref name="input"/> with <paramref name="parser"/>; <paramref name="blockParser"/> parses the
    /// blocks of lambdas and anonymous methods (see <see cref="CSharpParseContext.BlockParser"/>). Shared with
    /// <see cref="CSharpHybridParser"/>.
    /// </summary>
    internal static bool TryParse(
        string input,
        CSharpParseOptions options,
        Parser<CompilationUnit> parser,
        Parser<BlockStatement> blockParser,
        out CompilationUnit result,
        out ParseError error)
    {
        var source = GetUtf8Source(input);
        try
        {
            return TryParse(source, options, parser, blockParser, out result, out error);
        }
        catch (InsufficientExecutionStackException)
        {
            // Deeply nested input: parse again on a thread with a large stack
        }

        CompilationUnit largeStackResult = null;
        ParseError largeStackError = null;
        var success = false;
        var thread = new Thread(
            () =>
            {
                try
                {
                    success = TryParse(source, options, parser, blockParser, out largeStackResult, out largeStackError);
                }
                catch (InsufficientExecutionStackException)
                {
                    largeStackError = new ParseError { Message = "The input is nested too deeply." };
                }
            },
            LargeStackSize);
        thread.Start();
        thread.Join();

        result = largeStackResult;
        error = largeStackError;
        return success;
    }

    private static bool TryParse(
        byte[] source,
        CSharpParseOptions options,
        Parser<CompilationUnit> parser,
        Parser<BlockStatement> blockParser,
        out CompilationUnit result,
        out ParseError error)
    {
        var reader = new SpanReader(source);
        var context = new CSharpParseContext(options) { BlockParser = blockParser };
        try
        {
            if (parser.TryParse(ref reader, context, out result, out error))
            {
                return true;
            }

            error ??= SyntaxParser.DescribeFailure(source, context);
            return false;
        }
        finally
        {
            context.ReleaseCaches();
        }
    }

    /// <summary>
    /// Scans the tokens of <paramref name="input"/> without parsing it and returns their number. Used by the
    /// benchmarks to measure the scanner (lexer and preprocessor), which every variant of the parser shares.
    /// </summary>
    internal static int ScanTokens(string input, CSharpParseOptions options = null)
    {
        var source = GetUtf8Source(input);
        var context = new CSharpParseContext(options);
        try
        {
            return SyntaxParser.ScanTokens(source, context);
        }
        finally
        {
            context.ReleaseCaches();
        }
    }

    /// <summary>
    /// The bytes the parser reads for <paramref name="input"/>: its UTF-8 encoding without the byte order mark.
    /// Node spans (<see cref="CSharpNode.Span"/>) are offsets into them.
    /// </summary>
    public static byte[] GetUtf8Source(string input)
    {
        input ??= string.Empty;

        // A byte order mark is not part of the source text
        var start = input.Length > 0 && input[0] == '\uFEFF' ? 1 : 0;
        return System.Text.Encoding.UTF8.GetBytes(input, start, input.Length - start);
    }
}
