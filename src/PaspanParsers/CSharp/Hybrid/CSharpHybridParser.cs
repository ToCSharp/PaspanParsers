using Paspan;
using Paspan.Fluent;

namespace PaspanParsers.CSharp;

/// <summary>
/// The hybrid C# parser: the grammar that combinators express well is written with Paspan combinators
/// (<see cref="HybridGrammar"/>), the rest (types, expressions, patterns and the lookahead-heavy choices) is the
/// hand-written <see cref="SyntaxParser"/>. It produces the same AST as <see cref="CSharpParser"/>; the two are
/// kept side by side to compare their speed (see <c>docs/csharp-hybrid-parser-plan.md</c>).
/// </summary>
/// <remarks>
/// The two styles call each other: the grammar runs hand-written rules through <see cref="SyntaxRuleParser{T}"/>,
/// and the hand-written parser runs the grammar wherever it parses a block or a statement
/// (<see cref="CSharpParseContext.Grammar"/>). Both read the same cached tokens (<see cref="TokenParsers"/>).
/// The grammar covers the compilation unit, declarations, blocks and statements (stage H3 of the plan).
/// </remarks>
public static class CSharpHybridParser
{
    public static readonly Parser<CompilationUnit> CompilationUnitParser =
        CSharpParser.WithTrivia(HybridGrammar.Instance.CompilationUnit);

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
        return CSharpParser.TryParse(input, options, CompilationUnitParser, HybridGrammar.Instance, out result, out error);
    }
}
