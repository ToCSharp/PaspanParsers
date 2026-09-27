using System.Text;
using System.Text.RegularExpressions;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// A file preprocessed by clang: the macros its headers define, by name (<c>F(x)</c> for a function-like
/// macro) with their replacement text, and the file itself with its macros expanded.
/// </summary>
/// <remarks>
/// <see cref="Expanded"/> keeps the lines of the original file: its code with the macros expanded and the
/// conditional directives evaluated, its <c>#define</c>, <c>#undef</c> and <c>#pragma</c> directives, and the
/// <c>#include</c> directives of the headers it included, as written (a header found by its full path would
/// break the <c>#include_next</c> of wrapper headers like clang's <c>stdint.h</c>). Clang compiles it like
/// the original, and our parser meets no macro in it.
/// </remarks>
public sealed record PreprocessedFile(IReadOnlyDictionary<string, string> HeaderMacros, byte[] Expanded);

/// <summary>
/// Preprocesses files with clang (<c>-E -dD</c>) to learn what our parser, which does not read headers and
/// does not expand macros, cannot know: which failures of the oracle come from macros.
/// </summary>
public static partial class ClangPreprocessor
{
    private const string MainFile = "<stdin>";

    /// <summary>
    /// Preprocesses <paramref name="source"/> with the macros and include directories of
    /// <paramref name="options"/>; null when clang fails.
    /// </summary>
    public static PreprocessedFile Preprocess(byte[] source, ClangOracleOptions options)
    {
        var workingDirectory = options.WorkingDirectory ?? Environment.CurrentDirectory;
        var run = Clang.Run(source, ["-E", "-dD", .. options.ClangArguments()], workingDirectory);
        if (!run.Succeeded)
        {
            return null;
        }

        // By name: the name with the parameters, and the replacement
        var macros = new Dictionary<string, (string Key, string Replacement)>(StringComparer.Ordinal);
        var expanded = new StringBuilder();
        var file = "";
        var expandedLines = 0;
        var sourceLines = Encoding.UTF8.GetString(source).Split('\n');

        // Clang writes the lines of a file after a line marker (# 12 "file" flags) naming the file and the number
        // of the next line; short gaps are written as empty lines
        foreach (var line in Encoding.UTF8.GetString(run.Output).Split('\n'))
        {
            var marker = LineMarker().Match(line);
            if (marker.Success)
            {
                var next = Unescape(marker.Groups["file"].Value);
                var lineNumber = int.Parse(marker.Groups["line"].Value);
                if (next == MainFile)
                {
                    // Back in the main file, or further in it: keep the lines of the original
                    for (; expandedLines < lineNumber - 1; expandedLines++)
                    {
                        expanded.Append('\n');
                    }
                }
                else if (file == MainFile && !IsBuiltIn(next) && marker.Groups["flags"].Value.Split(' ').Contains("1"))
                {
                    // The main file includes a header: the line of the #include directive
                    var directive = expandedLines < sourceLines.Length ? sourceLines[expandedLines].Trim() : "";
                    if (directive.StartsWith('#') || directive.StartsWith("%:", StringComparison.Ordinal))
                    {
                        expanded.Append(directive).Append('\n');
                    }
                    else
                    {
                        expanded.Append("#include \"").Append(Path.GetFullPath(next, workingDirectory)).Append("\"\n");
                    }

                    expandedLines++;
                }

                file = next;
                continue;
            }

            if (file == MainFile)
            {
                expanded.Append(line).Append('\n');
                expandedLines++;
            }
            else if (!IsBuiltIn(file))
            {
                ReadDirective(line, macros);
            }
        }

        var headerMacros = macros.Values.ToDictionary(m => m.Key, m => m.Replacement, StringComparer.Ordinal);
        return new PreprocessedFile(headerMacros, Encoding.UTF8.GetBytes(expanded.ToString()));
    }

    /// <summary>
    /// Applies a <c>#define</c> or <c>#undef</c> line of a header to <paramref name="macros"/>.
    /// </summary>
    private static void ReadDirective(string line, Dictionary<string, (string Key, string Replacement)> macros)
    {
        const string Define = "#define ";
        const string Undef = "#undef ";
        if (line.StartsWith(Define, StringComparison.Ordinal))
        {
            var text = line[Define.Length..].TrimEnd('\r');
            var nameEnd = 0;
            while (nameEnd < text.Length && (char.IsLetterOrDigit(text[nameEnd]) || text[nameEnd] is '_' or '$'))
            {
                nameEnd++;
            }

            // The parameters of a function-like macro follow the name without a space
            if (nameEnd < text.Length && text[nameEnd] == '(')
            {
                var close = text.IndexOf(')', nameEnd);
                nameEnd = close < 0 ? text.Length : close + 1;
            }

            var key = text[..nameEnd];
            var parenthesis = key.IndexOf('(');
            macros[parenthesis < 0 ? key : key[..parenthesis]] = (key, nameEnd < text.Length ? text[(nameEnd + 1)..] : "");
        }
        else if (line.StartsWith(Undef, StringComparison.Ordinal))
        {
            macros.Remove(line[Undef.Length..].Trim());
        }
    }

    // The predefined macros and the -D options, which the options give our parser
    private static bool IsBuiltIn(string file) => file is "<built-in>" or "<command line>" or "";

    private static string Unescape(string text) => text.Replace("\\\"", "\"").Replace("\\\\", "\\");

    [GeneratedRegex(@"^# (?<line>\d+) ""(?<file>(?:[^""\\]|\\.)*)""(?<flags>(?: \d)*)\r?$")]
    private static partial Regex LineMarker();
}
