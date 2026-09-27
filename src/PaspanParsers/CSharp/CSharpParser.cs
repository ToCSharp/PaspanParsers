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
    /// at the token after the furthest token the parser could consume; with
    /// <see cref="CSharpParseOptions.ErrorRecovery"/> it returns the tree and <see cref="CompilationUnit.Errors"/>.
    /// </summary>
    public static bool TryParse(string input, CSharpParseOptions options, out CompilationUnit result, out ParseError error)
    {
        return TryParseSource(GetUtf8Source(input), options, out result, out error);
    }

    /// <summary>
    /// Parses C# source given as UTF-8 bytes, such as the content of a file, without decoding it to a string.
    /// A leading byte order mark is skipped: node spans are offsets into <paramref name="utf8Source"/>
    /// after it, which is what <see cref="GetUtf8Source(ReadOnlyMemory{byte})"/> returns.
    /// </summary>
    public static bool TryParse(ReadOnlyMemory<byte> utf8Source, CSharpParseOptions options, out CompilationUnit result, out ParseError error)
    {
        return TryParseSource(GetUtf8Source(utf8Source), options, out result, out error);
    }

    private static bool TryParseSource(ReadOnlyMemory<byte> source, CSharpParseOptions options, out CompilationUnit result, out ParseError error)
    {
        try
        {
            return TryParseCore(source, options, out result, out error);
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
                    success = TryParseCore(source, options, out largeStackResult, out largeStackError);
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

    private static bool TryParseCore(ReadOnlyMemory<byte> source, CSharpParseOptions options, out CompilationUnit result, out ParseError error)
    {
        var reader = new SpanReader(source.Span);
        var context = new CSharpParseContext(options);
        try
        {
            if (CompilationUnitParser.TryParse(ref reader, context, out result, out error))
            {
                if (context.Errors.Count != 0)
                {
                    // An invalid region is skipped again each time an enclosing alternative is retried
                    result.Errors = context.Errors.Distinct().OrderBy(e => e.Span.Start).ThenBy(e => e.Span.End).ToList();
                }

                return true;
            }

            error ??= SyntaxParser.DescribeFailure(source.Span, context);
            return false;
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

    /// <summary>
    /// The bytes the parser reads for the UTF-8 source <paramref name="utf8Source"/>: the source without
    /// its byte order mark. Node spans (<see cref="CSharpNode.Span"/>) are offsets into them.
    /// </summary>
    public static ReadOnlyMemory<byte> GetUtf8Source(ReadOnlyMemory<byte> utf8Source)
    {
        return utf8Source.Span.StartsWith("\uFEFF"u8) ? utf8Source[3..] : utf8Source;
    }
}
