using Paspan;
using Paspan.Fluent;
using static Paspan.Fluent.Parsers;
using static PaspanParsers.CSharp.SyntaxRules;
using static PaspanParsers.CSharp.TokenParsers;

namespace PaspanParsers.CSharp;

/// <summary>
/// The hybrid C# parser: the grammar that combinators express well is written with Paspan combinators,
/// the rest (types, expressions, patterns and the lookahead-heavy choices) is the hand-written
/// <see cref="SyntaxParser"/>. It produces the same AST as <see cref="CSharpParser"/>; the two are kept
/// side by side to compare their speed (see <c>docs/csharp-hybrid-parser-plan.md</c>).
/// </summary>
/// <remarks>
/// The two styles call each other: the grammar runs hand-written rules through <see cref="SyntaxRuleParser{T}"/>,
/// and the hand-written parser runs the grammar's block for the bodies of lambdas and anonymous methods
/// (<see cref="CSharpParseContext.BlockParser"/>). Both read the same cached tokens (<see cref="TokenParsers"/>).
/// So far (stage H1 of the plan) the grammar covers blocks; statements, declarations and the compilation unit
/// are still hand-written.
/// </remarks>
public static class CSharpHybridParser
{
    /// <summary>
    /// <c>'{' statement* '}'</c>
    /// </summary>
    internal static readonly Parser<BlockStatement> Block;

    public static readonly Parser<CompilationUnit> CompilationUnitParser;

    static CSharpHybridParser()
    {
        var statement = new SyntaxRuleParser<Statement>(SyntaxParser.ParseStatementRule);

        var block = new RecursiveRule<BlockStatement>();
        block.Parser = BlockScope(Node(
            Punctuator("{").And(ZeroOrMany(statement)).And(Punctuator("}"))
                .Then(x => new BlockStatement(x.Item2.Count != 0 ? x.Item2 : null)
                {
                    NullableDirectives = x.Item1.NullableDirectives,
                    CloseBraceNullableDirectives = x.Item3.NullableDirectives,
                })));
        Block = block;

        CompilationUnitParser = CSharpParser.WithTrivia(new SyntaxRuleParser<CompilationUnit>(SyntaxParser.ParseCompilationUnitRule));
    }

    public static CompilationUnit Parse(string input, CSharpParseOptions options = null)
    {
        return TryParse(input, options, out var result, out _) ? result : null;
    }

    public static bool TryParse(string input, out CompilationUnit result, out ParseError error)
    {
        return TryParse(input, null, out result, out error);
    }

    /// <summary>
    /// Parses <paramref name="input"/> like <see cref="CSharpParser.TryParse(string, CSharpParseOptions, out CompilationUnit, out ParseError)"/>.
    /// </summary>
    public static bool TryParse(string input, CSharpParseOptions options, out CompilationUnit result, out ParseError error)
    {
        return CSharpParser.TryParse(input, options, CompilationUnitParser, Block, out result, out error);
    }
}
