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
        string sourceDirectory = null,
        IReadOnlyCollection<string> typeNames = null,
        IReadOnlyCollection<string> templateNames = null,
        IReadOnlyCollection<string> conceptNames = null,
        IReadOnlyCollection<string> functionTemplateNames = null,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> classMembers = null)
    {
        ClassMembers = classMembers ?? new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
        FunctionTemplateNames = new HashSet<string>(functionTemplateNames ?? [], StringComparer.Ordinal);
        TypeNames = new HashSet<string>(typeNames ?? [], StringComparer.Ordinal);
        TemplateNames = new HashSet<string>(templateNames ?? [], StringComparer.Ordinal);
        ConceptNames = new HashSet<string>(conceptNames ?? [], StringComparer.Ordinal);
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

    /// <summary>
    /// Identifiers of types declared outside the parsed file, such as in headers (<c>size_t</c>, <c>string</c>):
    /// they tell declarations from expressions where the parser cannot see the declaration. A qualified name of
    /// the code whose qualifier the file does not declare is looked up by the qualified name (these names may
    /// be qualified by their namespaces and classes, <c>std::system_error</c>), then by its last identifier.
    /// The same holds for the other names of the options.
    /// </summary>
    public IReadOnlyCollection<string> TypeNames { get; }

    /// <summary>
    /// Identifiers of class and alias templates declared outside the parsed file (<c>vector</c>), so that
    /// a '&lt;' after them starts template arguments in expressions, and their template-ids are types.
    /// </summary>
    public IReadOnlyCollection<string> TemplateNames { get; }

    /// <summary>
    /// Identifiers of function and variable templates declared outside the parsed file (<c>get</c>,
    /// <c>is_same_v</c>), so that a '&lt;' after them starts template arguments, and their template-ids are
    /// expressions: <c>get&lt;0&gt;(t);</c> is a call.
    /// </summary>
    public IReadOnlyCollection<string> FunctionTemplateNames { get; }

    /// <summary>
    /// Identifiers of concepts declared outside the parsed file (<c>integral</c>), so that a template parameter
    /// or a placeholder type they constrain is recognized.
    /// </summary>
    public IReadOnlyCollection<string> ConceptNames { get; }

    /// <summary>
    /// The variables and functions that classes declared outside the parsed file have as members, with those
    /// of their bases, by the unqualified name of the class. The body of a member function defined in the
    /// file (<c>void raw_ostream::f() { indent(2); }</c>) and a class of the file derived from such a class see
    /// them: a member hides a type of the same name.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> ClassMembers { get; }
}
