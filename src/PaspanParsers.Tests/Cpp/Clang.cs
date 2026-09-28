using System.Diagnostics;
using System.Text;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// The output of a clang run: the exit code, the standard output as bytes and the standard error. The
/// output of <see cref="Clang.DumpAst"/> is read while clang writes it, into <see cref="Ast"/>.
/// </summary>
public sealed record ClangRun(int ExitCode, byte[] Output, string Errors)
{
    public bool Succeeded => ExitCode == 0;

    /// <summary>
    /// The AST that <see cref="Clang.DumpAst"/> read, or null when clang failed.
    /// </summary>
    public ClangAst Ast { get; init; }

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

    private static readonly Lazy<IReadOnlyList<string>> SystemIncludes = new(ReadSystemIncludeDirectories);

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
    /// The directories clang searches for <c>#include &lt;...&gt;</c> after the <c>-I</c> options.
    /// </summary>
    public static IReadOnlyList<string> SystemIncludeDirectories => SystemIncludes.Value;

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
        var (run, output) = Run(source, arguments, workingDirectory, stream =>
        {
            var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        });

        return run with { Output = output ?? [] };
    }

    /// <summary>
    /// Runs clang like <see cref="Run(byte[], IEnumerable{string}, string)"/> and reads its standard output
    /// with <paramref name="readOutput"/> while clang writes it; the result is default when clang fails.
    /// </summary>
    public static (ClangRun Run, T Output) Run<T>(byte[] source, IEnumerable<string> arguments, string workingDirectory, Func<Stream, T> readOutput)
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
        var outputTask = Task.Run(() =>
        {
            var stream = process.StandardOutput.BaseStream;
            try
            {
                return readOutput(stream);
            }
            finally
            {
                // Let clang finish writing when the reader stops early or fails
                stream.CopyTo(Stream.Null);
            }
        });
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
            return (new ClangRun(-1, [], $"clang timed out after {Timeout.TotalSeconds}s"), default);
        }

        var run = new ClangRun(process.ExitCode, [], errorsTask.Result);
        try
        {
            var output = outputTask.Result;
            return (run, run.Succeeded ? output : default);
        }
        catch (AggregateException) when (!run.Succeeded)
        {
            // The output of a failed run is incomplete
            return (run, default);
        }
    }

    /// <summary>
    /// The AST of <paramref name="source"/>, read from clang's JSON dump into <see cref="ClangRun.Ast"/>;
    /// with <paramref name="collectHeaderNames"/>, including the names the headers declare
    /// (<see cref="ClangAst.HeaderNames"/>). The dump of a file that includes many headers can be larger
    /// than 2 GB, so it is never held in memory.
    /// </summary>
    public static ClangRun DumpAst(byte[] source, IEnumerable<string> arguments, string workingDirectory = null, bool collectHeaderNames = false)
    {
        var (run, ast) = Run(source, ["-fsyntax-only", "-Xclang", "-ast-dump=json", .. arguments], workingDirectory,
            stream => ClangAst.Read(stream, collectHeaderNames));
        return run with { Ast = ast };
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

    private static IReadOnlyList<string> ReadSystemIncludeDirectories()
    {
        var directories = new List<string>();
        if (!IsAvailable)
        {
            return directories;
        }

        // clang -v lists them between these lines of its standard error
        var run = Run([], ["-E", "-v"]);
        var inList = false;
        foreach (var line in run.Errors.Split('\n'))
        {
            var text = line.TrimEnd('\r');
            if (text.StartsWith("#include <...> search starts here:", StringComparison.Ordinal))
            {
                inList = true;
            }
            else if (text.StartsWith("End of search list.", StringComparison.Ordinal))
            {
                break;
            }
            else if (inList && text.StartsWith(' '))
            {
                // A macOS framework directory is followed by " (framework directory)"
                directories.Add(System.IO.Path.GetFullPath(text.Trim().Replace(" (framework directory)", "")));
            }
        }

        return directories;
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
