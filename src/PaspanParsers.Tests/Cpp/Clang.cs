using System.Diagnostics;
using System.Text;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// The output of a clang run: the exit code, the standard output as bytes and the standard error.
/// </summary>
public sealed record ClangRun(int ExitCode, byte[] Output, string Errors)
{
    public bool Succeeded => ExitCode == 0;

    /// <summary>
    /// The first error message, like <c>(3,5): error: expected ';'</c>.
    /// </summary>
    public string FirstError()
    {
        foreach (var line in Errors.Split('\n'))
        {
            var index = line.IndexOf(": error: ", StringComparison.Ordinal);
            if (index < 0)
            {
                index = line.IndexOf(": fatal error: ", StringComparison.Ordinal);
            }

            if (index >= 0)
            {
                // <stdin>:3:5: error: ...
                var location = line[..index].Split(':');
                var position = location.Length >= 3 ? $"({location[^2]},{location[^1]}): " : "";
                return position + line[(index + 2)..].Trim();
            }
        }

        return Errors.Trim();
    }
}

/// <summary>
/// Runs the clang compiler, the reference parser of the C++ oracle. The executable is <c>CLANG_PATH</c>
/// or <c>clang++</c> on the <c>PATH</c>. Source is passed on the standard input, so locations are in
/// the file <c>&lt;stdin&gt;</c> and quoted includes are resolved from the working directory.
/// </summary>
public static class Clang
{
    /// <summary>
    /// The language standard of the oracle.
    /// </summary>
    public const string Standard = "-std=c++23";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    private static readonly Lazy<string> ExecutablePath = new(FindExecutable);

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Predefined = new(ReadPredefinedMacros);

    /// <summary>
    /// The clang executable, or null when it is not installed.
    /// </summary>
    public static string Path => ExecutablePath.Value;

    public static bool IsAvailable => Path != null;

    /// <summary>
    /// The object-like macros clang predefines for <see cref="Standard"/> (<c>__cplusplus</c>,
    /// <c>__clang__</c>, ...), by name.
    /// </summary>
    public static IReadOnlyDictionary<string, string> PredefinedMacros => Predefined.Value;

    /// <summary>
    /// Marks the test inconclusive when clang is not installed.
    /// </summary>
    public static void RequireClang()
    {
        if (!IsAvailable)
        {
            Assert.Inconclusive("clang++ was not found: install clang or set CLANG_PATH to run the C++ oracle.");
        }
    }

    /// <summary>
    /// Runs clang in C++ mode over <paramref name="source"/> with the <see cref="Standard"/> and
    /// <paramref name="arguments"/>, without warnings.
    /// </summary>
    public static ClangRun Run(byte[] source, IEnumerable<string> arguments, string workingDirectory = null)
    {
        var startInfo = new ProcessStartInfo(Path)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        };

        startInfo.ArgumentList.Add(Standard);
        startInfo.ArgumentList.Add("-w");
        startInfo.ArgumentList.Add("-fno-color-diagnostics");
        startInfo.ArgumentList.Add("-ferror-limit=1");
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add("-x");
        startInfo.ArgumentList.Add("c++");
        startInfo.ArgumentList.Add("-");

        using var process = Process.Start(startInfo)!;
        var output = new MemoryStream();
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(output);
        var errorsTask = process.StandardError.ReadToEndAsync();

        try
        {
            process.StandardInput.BaseStream.Write(source);
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // clang exited before reading all of its input
        }

        if (!process.WaitForExit(Timeout))
        {
            process.Kill(entireProcessTree: true);
            return new ClangRun(-1, [], $"clang timed out after {Timeout.TotalSeconds}s");
        }

        outputTask.Wait();
        return new ClangRun(process.ExitCode, output.ToArray(), errorsTask.Result);
    }

    /// <summary>
    /// The JSON AST dump of <paramref name="source"/>.
    /// </summary>
    public static ClangRun DumpAst(byte[] source, IEnumerable<string> arguments, string workingDirectory = null)
    {
        return Run(source, ["-fsyntax-only", "-Xclang", "-ast-dump=json", .. arguments], workingDirectory);
    }

    private static string FindExecutable()
    {
        var configured = Environment.GetEnvironmentVariable("CLANG_PATH");
        if (!string.IsNullOrEmpty(configured))
        {
            return File.Exists(configured) ? configured : null;
        }

        var names = OperatingSystem.IsWindows() ? new[] { "clang++.exe", "clang.exe" } : ["clang++", "clang"];
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var directory in directories)
        {
            foreach (var name in names)
            {
                var candidate = System.IO.Path.Combine(directory, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> ReadPredefinedMacros()
    {
        var macros = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!IsAvailable)
        {
            return macros;
        }

        var run = Run([], ["-dM", "-E"]);
        foreach (var line in Encoding.UTF8.GetString(run.Output).Split('\n'))
        {
            const string Define = "#define ";
            if (!line.StartsWith(Define, StringComparison.Ordinal))
            {
                continue;
            }

            var rest = line[Define.Length..].TrimEnd('\r');
            var space = rest.IndexOf(' ');
            var name = space < 0 ? rest : rest[..space];
            if (!name.Contains('('))
            {
                macros[name] = space < 0 ? "" : rest[(space + 1)..];
            }
        }

        return macros;
    }
}
