using Paspan;
using Paspan.Fluent;

namespace PaspanParsers.CSharp;

/// <summary>
/// C# parser. The hand-written <see cref="SyntaxParser"/> (partial files under <c>CSharp/Parser/</c>) parses
/// the compilation unit; it is wired in here as a combinator parser that also skips the trivia around it.
/// </summary>
public partial class CSharpParser
{
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

    public static bool TryParse(string input, CSharpParseOptions options, out CompilationUnit result, out ParseError error)
    {
        input ??= string.Empty;

        // A byte order mark is not part of the source text
        if (input.Length > 0 && input[0] == '\uFEFF')
        {
            input = input[1..];
        }

        var reader = new SpanReader(input);
        return CompilationUnitParser.TryParse(ref reader, new CSharpParseContext(options), out result, out error);
    }
}
