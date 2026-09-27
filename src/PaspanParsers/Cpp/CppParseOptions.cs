namespace PaspanParsers.Cpp;

/// <summary>
/// C++ standard versions understood by <see cref="CppParser"/>.
/// </summary>
public enum CppLanguageVersion
{
    Cpp11 = 11,
    Cpp14 = 14,
    Cpp17 = 17,
    Cpp20 = 20,
    Cpp23 = 23,
    Latest = Cpp23,
}

/// <summary>
/// Options that control how C++ source is parsed.
/// </summary>
public sealed class CppParseOptions
{
    public static CppParseOptions Default { get; } = new();

    public CppParseOptions(
        CppLanguageVersion languageVersion = CppLanguageVersion.Latest,
        IReadOnlyDictionary<string, string> macros = null,
        IReadOnlyList<string> includeDirectories = null,
        string sourceDirectory = null)
    {
        LanguageVersion = languageVersion;
        Macros = macros ?? new Dictionary<string, string>(StringComparer.Ordinal);
        IncludeDirectories = includeDirectories ?? [];
        SourceDirectory = sourceDirectory;
    }

    public CppLanguageVersion LanguageVersion { get; }

    /// <summary>
    /// Object-like macros defined before the input, by name, with their replacement text: the predefined
    /// macros of a compiler (<c>__cplusplus</c>, <c>__GNUC__</c>) and <c>-D</c> options. They are used to
    /// evaluate conditional directives; macros are never expanded in the code itself. A name with parameters,
    /// like <c>F(x)</c>, defines a function-like macro.
    /// </summary>
    public IReadOnlyDictionary<string, string> Macros { get; }

    /// <summary>
    /// The directories <c>__has_include</c> looks for headers in, in order, like <c>-I</c> options and the
    /// system include directories of a compiler. Headers are never read.
    /// </summary>
    public IReadOnlyList<string> IncludeDirectories { get; }

    /// <summary>
    /// The directory of the parsed file, where <c>__has_include("header")</c> looks first; null when unknown.
    /// </summary>
    public string SourceDirectory { get; }
}
