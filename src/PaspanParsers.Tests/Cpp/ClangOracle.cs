using System.Text;
using PaspanParsers.Cpp;
using PaspanParsers.Tests.CSharp;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// How the oracle runs clang and our parser: macros defined on the command line (<c>NAME</c> or
/// <c>NAME=VALUE</c>), include directories (for clang only: our parser does not read headers) and the
/// directory quoted includes are resolved from.
/// </summary>
public sealed record ClangOracleOptions(
    IReadOnlyList<string> Defines = null,
    IReadOnlyList<string> IncludeDirectories = null,
    string WorkingDirectory = null)
{
    public static ClangOracleOptions Default { get; } = new();

    public IEnumerable<string> ClangArguments()
    {
        foreach (var define in Defines ?? [])
        {
            yield return "-D" + define;
        }

        foreach (var directory in IncludeDirectories ?? [])
        {
            yield return "-I" + directory;
        }
    }

    /// <summary>
    /// The macros our parser evaluates conditional directives with: clang's predefined macros and the defines.
    /// </summary>
    public CppParseOptions ParseOptions()
    {
        var macros = new Dictionary<string, string>(Clang.PredefinedMacros, StringComparer.Ordinal);
        foreach (var define in Defines ?? [])
        {
            var equals = define.IndexOf('=');
            if (equals < 0)
            {
                macros[define] = "1";
            }
            else
            {
                macros[define[..equals]] = define[(equals + 1)..];
            }
        }

        return new CppParseOptions(CppLanguageVersion.Cpp23, macros);
    }
}

/// <summary>
/// Uses clang as the reference parser. For a file clang compiles without errors:
/// <list type="number">
/// <item><see cref="CppParser"/> must parse it;</item>
/// <item>the tree printed back by <see cref="CppWriter"/> must compile to the same clang AST, apart from
/// positions and comments (see <see cref="ClangAst.Normalize"/>);</item>
/// <item>the spans and kinds of the nodes must match clang's (<see cref="CppSpanChecker"/>);</item>
/// <item>the values of literals must be clang's (<see cref="CppLiteralChecker"/>).</item>
/// </list>
/// Only the declarations of the main file are compared: our parser does not read included headers.
/// </summary>
public static class ClangOracle
{
    public static OracleResult Check(string source, ClangOracleOptions options = null)
    {
        return Check(Encoding.UTF8.GetBytes(source), options);
    }

    public static OracleResult Check(byte[] source, ClangOracleOptions options = null)
    {
        options ??= ClangOracleOptions.Default;
        var arguments = options.ClangArguments().ToList();

        var original = Clang.DumpAst(source, arguments, options.WorkingDirectory);
        if (!original.Succeeded)
        {
            return new OracleResult(OracleStatus.Invalid, original.FirstError());
        }

        var bomLength = source.AsSpan().StartsWith("﻿"u8) ? 3 : 0;
        if (!CppParser.TryParse(source, options.ParseOptions(), out var unit, out var error))
        {
            var detail = error != null ? $"({error.Line},{error.Column}): {error.Message}" : null;
            return new OracleResult(OracleStatus.ParseFailed, detail);
        }

        string written;
        try
        {
            var writer = new CppWriter();
            writer.WriteTranslationUnit(unit);
            written = writer.GetResult();
        }
        catch (Exception e)
        {
            return new OracleResult(OracleStatus.WriteFailed, $"{e.GetType().Name}: {e.Message}");
        }

        var regenerated = Clang.DumpAst(Encoding.UTF8.GetBytes(written), arguments, options.WorkingDirectory);
        if (!regenerated.Succeeded)
        {
            return new OracleResult(OracleStatus.Mismatch, "written code is invalid: " + regenerated.FirstError());
        }

        var originalAst = ClangAst.Read(original.Output);
        var difference = ClangAst.FirstDifference(originalAst.Normalize(), ClangAst.Read(regenerated.Output).Normalize());
        if (difference != null)
        {
            return new OracleResult(OracleStatus.Mismatch, difference);
        }

        var utf8 = source[bomLength..];
        var clangNodes = originalAst.Nodes(source, bomLength);
        var spanProblem = CppSpanChecker.Check(utf8, unit, clangNodes, ClangAst.RawTokens(source, bomLength));
        if (spanProblem != null)
        {
            return new OracleResult(OracleStatus.SpanMismatch, spanProblem);
        }

        var valueProblem = CppLiteralChecker.Check(unit, clangNodes);
        if (valueProblem != null)
        {
            return new OracleResult(OracleStatus.ValueMismatch, valueProblem);
        }

        return new OracleResult(OracleStatus.Passed);
    }
}
