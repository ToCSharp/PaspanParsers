namespace PaspanParsers.CSharp;

/// <summary>
/// C# language versions understood by <see cref="CSharpParser"/>.
/// </summary>
public enum CSharpLanguageVersion
{
    CSharp1 = 1,
    CSharp2 = 2,
    CSharp3 = 3,
    CSharp4 = 4,
    CSharp5 = 5,
    CSharp6 = 6,
    CSharp7 = 7,
    CSharp7_1 = 701,
    CSharp7_2 = 702,
    CSharp7_3 = 703,
    CSharp8 = 800,
    CSharp9 = 900,
    CSharp10 = 1000,
    CSharp11 = 1100,
    CSharp12 = 1200,
    CSharp13 = 1300,
    CSharp14 = 1400,
    Latest = CSharp14,
}

/// <summary>
/// Options that control how C# source is parsed.
/// </summary>
public sealed class CSharpParseOptions
{
    public static CSharpParseOptions Default { get; } = new();

    public CSharpParseOptions(
        CSharpLanguageVersion languageVersion = CSharpLanguageVersion.Latest,
        IEnumerable<string> preprocessorSymbols = null)
    {
        LanguageVersion = languageVersion;
        PreprocessorSymbols = preprocessorSymbols?.ToArray() ?? [];
    }

    public CSharpLanguageVersion LanguageVersion { get; }

    /// <summary>
    /// Symbols considered defined when evaluating <c>#if</c> directives.
    /// </summary>
    public IReadOnlyList<string> PreprocessorSymbols { get; }
}
