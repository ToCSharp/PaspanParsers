using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PaspanParsers.CSharp;
using RoslynParseOptions = Microsoft.CodeAnalysis.CSharp.CSharpParseOptions;

namespace PaspanParsers.Tests.CSharp;

public enum OracleStatus
{
    /// <summary>Roslyn reports syntax errors: the input is not valid C# and is out of scope.</summary>
    Invalid,
    /// <summary>Our parser rejected valid C#.</summary>
    ParseFailed,
    /// <summary>Our parser accepted the input but CSharpWriter threw.</summary>
    WriteFailed,
    /// <summary>The written AST is not valid C# or not equivalent to the original.</summary>
    Mismatch,
    Passed,
}

public sealed record OracleResult(OracleStatus Status, string Detail = null);

/// <summary>
/// Uses Roslyn as the reference parser: valid C# must parse with <see cref="CSharpParser"/>,
/// and the AST printed back by <see cref="CSharpWriter"/> must be equivalent to the original
/// according to Roslyn (trivia is ignored).
/// </summary>
public static class RoslynOracle
{
    public static OracleResult Check(string source, IEnumerable<string> preprocessorSymbols = null)
    {
        var symbols = preprocessorSymbols?.ToArray() ?? [];
        var roslynOptions = new RoslynParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: symbols);

        var original = CSharpSyntaxTree.ParseText(source, roslynOptions);
        var errors = original.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count != 0)
        {
            return new OracleResult(OracleStatus.Invalid, Describe(errors[0]));
        }

        var options = new PaspanParsers.CSharp.CSharpParseOptions(CSharpLanguageVersion.CSharp14, symbols);
        if (!CSharpParser.TryParse(source, options, out var unit, out var error))
        {
            var detail = error != null ? $"({error.Line},{error.Column}): {error.Message}" : null;
            return new OracleResult(OracleStatus.ParseFailed, detail);
        }

        string written;
        try
        {
            var writer = new CSharpWriter();
            writer.WriteCompilationUnit(unit);
            written = writer.GetResult();
        }
        catch (Exception e)
        {
            return new OracleResult(OracleStatus.WriteFailed, $"{e.GetType().Name}: {e.Message}");
        }

        var regenerated = CSharpSyntaxTree.ParseText(written, roslynOptions);
        var regeneratedErrors = regenerated.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (regeneratedErrors.Count != 0)
        {
            return new OracleResult(OracleStatus.Mismatch, "written code is invalid: " + Describe(regeneratedErrors[0]));
        }

        if (!AreEquivalent(original.GetRoot(), regenerated.GetRoot()))
        {
            return new OracleResult(OracleStatus.Mismatch, FirstDifference(original.GetRoot(), regenerated.GetRoot()));
        }

        return new OracleResult(OracleStatus.Passed);
    }

    /// <summary>
    /// <see cref="SyntaxFactory.AreEquivalent(SyntaxNode, SyntaxNode, bool)"/>, falling back to a comparison
    /// of the red trees when it fails.
    /// </summary>
    /// <remarks>
    /// Roslyn compares green nodes slot by slot, and the parser sometimes stores a one-element list as
    /// a list node and sometimes as the element itself (it depends on the trivia around the statement),
    /// so identical trees can compare as different. <see cref="SyntaxNode.ChildNodesAndTokens"/> flattens
    /// lists, and tokens are compared with Roslyn's rules: kind, missing, value text of identifiers and
    /// the text of literals.
    /// </remarks>
    public static bool AreEquivalent(SyntaxNode expected, SyntaxNode actual)
    {
        return SyntaxFactory.AreEquivalent(expected, actual, topLevel: false) || AreStructurallyEquivalent(expected, actual);
    }

    private static bool AreStructurallyEquivalent(SyntaxNode expected, SyntaxNode actual)
    {
        var stack = new Stack<(SyntaxNodeOrToken Expected, SyntaxNodeOrToken Actual)>();
        stack.Push((expected, actual));

        while (stack.Count != 0)
        {
            var (e, a) = stack.Pop();
            if (e.RawKind != a.RawKind || e.IsToken != a.IsToken)
            {
                return false;
            }

            if (e.IsToken)
            {
                if (!SyntaxFactory.AreEquivalent(e.AsToken(), a.AsToken()))
                {
                    return false;
                }

                continue;
            }

            var expectedChildren = e.ChildNodesAndTokens();
            var actualChildren = a.ChildNodesAndTokens();
            if (expectedChildren.Count != actualChildren.Count)
            {
                return false;
            }

            for (var i = expectedChildren.Count - 1; i >= 0; i--)
            {
                stack.Push((expectedChildren[i], actualChildren[i]));
            }
        }

        return true;
    }

    private static string Describe(Diagnostic diagnostic)
    {
        var position = diagnostic.Location.GetLineSpan().StartLinePosition;
        return $"({position.Line + 1},{position.Character + 1}): {diagnostic.Id} {diagnostic.GetMessage()}";
    }

    /// <summary>
    /// Walks both trees in parallel and describes the first node or token that differs.
    /// </summary>
    private static string FirstDifference(SyntaxNode expected, SyntaxNode actual)
    {
        var expectedChildren = expected.ChildNodesAndTokens();
        var actualChildren = actual.ChildNodesAndTokens();

        for (var i = 0; i < Math.Min(expectedChildren.Count, actualChildren.Count); i++)
        {
            var e = expectedChildren[i];
            var a = actualChildren[i];

            if (e.IsNode && a.IsNode && e.IsKind(a.Kind()))
            {
                if (!AreEquivalent(e.AsNode(), a.AsNode()))
                {
                    return FirstDifference(e.AsNode(), a.AsNode());
                }
            }
            else if (!e.IsKind(a.Kind()) || (e.IsToken && !SyntaxFactory.AreEquivalent(e.AsToken(), a.AsToken())))
            {
                return Mismatch(e, a);
            }
        }

        if (expectedChildren.Count != actualChildren.Count)
        {
            return $"{Location(expected)}: {expected.Kind()} has {expectedChildren.Count} children, written code has {actualChildren.Count}";
        }

        return $"{Location(expected)}: {expected.Kind()} differs";
    }

    private static string Mismatch(SyntaxNodeOrToken expected, SyntaxNodeOrToken actual)
    {
        return $"{Location(expected)}: expected {expected.Kind()} '{Shorten(expected.ToString())}', written {actual.Kind()} '{Shorten(actual.ToString())}'";
    }

    private static string Location(SyntaxNodeOrToken node)
    {
        var position = node.GetLocation()!.GetLineSpan().StartLinePosition;
        return $"({position.Line + 1},{position.Character + 1})";
    }

    private static string Shorten(string text)
    {
        text = text.ReplaceLineEndings(" ");
        return text.Length <= 60 ? text : text[..57] + "...";
    }
}
