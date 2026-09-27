using Paspan;
using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.CSharp;

/// <summary>
/// C# parser. Declarations and the compilation unit are built from Paspan combinators in the
/// partial files under <c>CSharp/Parser/</c>; names, types, expressions, patterns and statements
/// are parsed by the hand-written <see cref="SyntaxParser"/>, wired in here as combinator parsers.
/// </summary>
public partial class CSharpParser
{
    public static readonly Parser<CompilationUnit> CompilationUnitParser;

    // Grammar parts implemented by SyntaxParser
    private static readonly Parser<Expression> expression = new SyntaxRuleParser<Expression>(SyntaxParser.ParseExpressionRule);
    private static readonly Parser<BlockStatement> block = new SyntaxRuleParser<BlockStatement>(SyntaxParser.ParseBlockRule);
    private static readonly Parser<TypeReference> typeReference = new SyntaxRuleParser<TypeReference>(SyntaxParser.ParseTypeRule);
    private static readonly Parser<TypeReference> returnType = new SyntaxRuleParser<TypeReference>(SyntaxParser.ParseReturnTypeRule);

    private static readonly Deferred<MemberDeclaration> memberDeclaration = Deferred<MemberDeclaration>();

    static CSharpParser()
    {
        InitializeLexical();
        InitializeNames();
        InitializeTypes();
        InitializeAttributesAndModifiers();
        InitializeParameters();
        InitializeDeclarations();

        CompilationUnitParser = WithTrivia(InitializeCompilationUnit());
    }

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
