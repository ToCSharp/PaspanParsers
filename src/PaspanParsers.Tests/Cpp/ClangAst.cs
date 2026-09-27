using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// A node of clang's AST in the main file: its kind, its source range and, for declarations, the
/// location of the declared name. Offsets are bytes of the parsed input without the byte order mark.
/// <see cref="FromMacro"/> is set when the range starts or ends in a macro expansion: clang's location there is
/// the name of the macro in the source, and the span ends at the end of the macro use, after the arguments of
/// a function-like macro. <see cref="InMacro"/> is set when the node comes from a single macro use, whose
/// span it then has. <see cref="Value"/> is the <c>value</c> of a literal and <see cref="Type"/> the type of
/// an expression, as clang writes them.
/// </summary>
public sealed record ClangNode(string Kind, TextSpan Span, int NameOffset, bool FromMacro, JsonNode Value = null, string Type = null)
{
    public bool InMacro { get; init; }
}

/// <summary>
/// The names of the types, class and alias templates, function and variable templates and concepts that the
/// headers included by a file declare, collected
/// from clang's AST (<see cref="ClangAst.HeaderNames"/>): what our parser, which does not read headers,
/// would know if it did.
/// </summary>
public sealed record HeaderNames(
    IReadOnlySet<string> TypeNames, IReadOnlySet<string> TemplateNames, IReadOnlySet<string> FunctionTemplateNames, IReadOnlySet<string> ConceptNames)
{
    /// <summary>
    /// The variables and functions that the classes of the headers have as members, with those of their bases,
    /// by the unqualified names of the classes.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> ClassMembers { get; init; } = new Dictionary<string, IReadOnlySet<string>>();
}

/// <summary>
/// Reads clang's JSON AST dump (<c>-Xclang -ast-dump=json</c>): the top-level declarations of the main
/// file, normalized for comparison, and the ranges of all their nodes.
/// </summary>
/// <remarks>
/// A dump that includes standard headers is about 100 MB, and one of a file that includes much of LLVM
/// more than 2 GB. It is read with <see cref="Utf8JsonReader"/> while clang writes it, and only the
/// declarations of the main file are materialized: a location in the main file has no
/// <c>includedFrom</c> (clang omits the <c>file</c> of a location when it did not change, but always
/// writes <c>includedFrom</c>), and implicit declarations have empty locations.
/// </remarks>
public sealed partial class ClangAst
{
    /// <summary>
    /// Keys whose values depend on positions or on addresses of the clang process, not on the tree.
    /// </summary>
    private static readonly HashSet<string> IgnoredKeys = ["id", "loc", "range", "previousDecl", "parentDeclContextId", "referencedMemberDecl", "typeAliasDeclId", "targetLabelDeclId", "declId", "temp"];

    private ClangAst(List<JsonObject> declarations, HeaderNames headerNames)
    {
        Declarations = declarations;
        HeaderNames = headerNames;
    }

    /// <summary>
    /// The top-level declarations of the main file, as dumped.
    /// </summary>
    public IReadOnlyList<JsonObject> Declarations { get; }

    /// <summary>
    /// The names declared by the included headers, when they were collected; otherwise null.
    /// </summary>
    public HeaderNames HeaderNames { get; }

    public static ClangAst Read(byte[] json, bool collectHeaderNames = false)
    {
        return Read(new MemoryStream(json, writable: false), collectHeaderNames);
    }

    /// <summary>
    /// Reads the dump from <paramref name="json"/> while it is written: only the declarations of the main
    /// file are kept, and with <paramref name="collectHeaderNames"/> the names of the others.
    /// </summary>
    public static ClangAst Read(Stream json, bool collectHeaderNames = false)
    {
        var builder = new DumpReader(collectHeaderNames);
        var buffer = ArrayPool<byte>.Shared.Rent(1 << 20);
        var length = 0;
        var state = new JsonReaderState(new JsonReaderOptions { MaxDepth = 100_000 });
        try
        {
            var isFinal = false;
            while (!isFinal)
            {
                if (length == buffer.Length)
                {
                    // A token longer than the buffer
                    var larger = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                    buffer.AsSpan(0, length).CopyTo(larger);
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = larger;
                }

                var read = json.Read(buffer, length, buffer.Length - length);
                isFinal = read == 0;
                length += read;

                var reader = new Utf8JsonReader(buffer.AsSpan(0, length), isFinal, state);
                while (reader.Read())
                {
                    builder.Token(ref reader);
                }

                state = reader.CurrentState;
                var consumed = (int)reader.BytesConsumed;
                buffer.AsSpan(consumed, length - consumed).CopyTo(buffer);
                length -= consumed;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return new ClangAst(builder.Declarations, collectHeaderNames ? builder.HeaderNames() : null);
    }

    /// <summary>
    /// Receives the tokens of the dump: <c>{ "id": ..., "kind": "TranslationUnitDecl", ..., "inner": [
    /// declarations ] }</c>. A declaration is built as a <see cref="JsonNode"/> until its location is read
    /// (it comes before the children); one outside the main file is then dropped, and only the kinds and
    /// names of its nodes are looked at.
    /// </summary>
    private sealed class DumpReader(bool collectNames)
    {
        private enum Mode
        {
            /// <summary>Outside the top-level declarations.</summary>
            Root,

            /// <summary>In a top-level declaration whose location is not read yet.</summary>
            Pending,

            /// <summary>In a declaration of the main file.</summary>
            Main,

            /// <summary>In a declaration of a header.</summary>
            Header,
        }

        // A node of a header declaration: its kind and name, and whether it is inside a statement or an
        // expression, where declarations are local and references name declarations made elsewhere
        private sealed class Frame
        {
            public int Depth;
            public Frame Parent;
            public string Kind;
            public string Name;
            public bool IsLocal;
            public bool IsScopedEnum;
            public bool IsInline;

            /// <summary>A template of a member function, constructor or conversion function.</summary>
            public bool DeclaresMember;

            public bool IsRecord => Kind is "CXXRecordDecl" or "ClassTemplateSpecializationDecl" or "ClassTemplatePartialSpecializationDecl";

            /// <summary>A member of a class.</summary>
            public bool IsMember => Parent?.IsRecord == true;

            /// <summary>The function or variable that a template declares, whose name is the template's.</summary>
            public bool IsTemplated => Parent?.Kind is "FunctionTemplateDecl" or "VarTemplateDecl";
        }

        private readonly Stack<JsonNode> _containers = new();
        private readonly Stack<Frame> _frames = new();
        private readonly HashSet<string> _types = new(StringComparer.Ordinal);
        private readonly HashSet<string> _templates = new(StringComparer.Ordinal);
        private readonly HashSet<string> _functionTemplates = new(StringComparer.Ordinal);
        private readonly HashSet<string> _namespaceFunctionTemplates = new(StringComparer.Ordinal);
        private readonly HashSet<string> _concepts = new(StringComparer.Ordinal);
        private readonly HashSet<string> _values = new(StringComparer.Ordinal);

        // The names qualified by their namespaces and classes: std::system_error, llvm::json::Array
        private readonly HashSet<string> _qualifiedTypes = new(StringComparer.Ordinal);
        private readonly HashSet<string> _qualifiedTemplates = new(StringComparer.Ordinal);
        private readonly HashSet<string> _qualifiedFunctionTemplates = new(StringComparer.Ordinal);
        private readonly HashSet<string> _qualifiedConcepts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _members = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _bases = new(StringComparer.Ordinal);

        // The class whose base specifiers are being read, and the depth of their array
        private Frame _basesOwner;
        private int _basesDepth;
        private Mode _mode;
        private int _rootDepth;
        private bool _inDeclarations;
        private string _property;
        private int _headerDepth;

        // The file of the last location of the declaration being built: clang writes the file of a location only
        // when it differs from the previous one
        private string _lastFile;

        public List<JsonObject> Declarations { get; } = [];

        public void Token(ref Utf8JsonReader reader)
        {
            switch (_mode)
            {
                case Mode.Root:
                    RootToken(ref reader);
                    break;
                case Mode.Pending:
                case Mode.Main:
                    BuildToken(ref reader);
                    break;
                case Mode.Header:
                    HeaderToken(ref reader);
                    break;
            }
        }

        private void RootToken(ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject when _inDeclarations && _rootDepth == 2:
                    _mode = Mode.Pending;
                    _property = null;
                    BuildToken(ref reader);
                    return;
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    _rootDepth++;
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    _rootDepth--;
                    _inDeclarations = false;
                    break;
                case JsonTokenType.PropertyName when _rootDepth == 1:
                    _inDeclarations = reader.ValueTextEquals("inner"u8);
                    break;
            }
        }

        private void BuildToken(ref Utf8JsonReader reader)
        {
            JsonNode node;
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    _property = reader.GetString();
                    if (_mode == Mode.Pending && _containers.Count == 1 && _property == "inner")
                    {
                        // The children come before a location: not a declaration of the main file
                        StartHeader();
                    }

                    return;
                case JsonTokenType.StartObject:
                    node = new JsonObject();
                    break;
                case JsonTokenType.StartArray:
                    node = new JsonArray();
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                {
                    var finished = _containers.Pop();
                    if (finished is JsonObject { Parent: JsonObject } location && location.ContainsKey("offset") && location.GetPropertyName() != "includedFrom")
                    {
                        // Give every location its file; the file an include comes from is no location
                        if (location["file"] is JsonValue file)
                        {
                            _lastFile = file.GetValue<string>();
                        }
                        else if (_lastFile != null)
                        {
                            location["file"] = _lastFile;
                        }
                    }

                    if (_containers.Count == 0)
                    {
                        if (_mode == Mode.Main)
                        {
                            Declarations.Add(finished.AsObject());
                        }

                        _mode = Mode.Root;
                    }
                    else if (_mode == Mode.Pending && _containers.Count == 1 && finished.GetPropertyName() == "loc")
                    {
                        if (IsInMainFile(finished.AsObject()))
                        {
                            _mode = Mode.Main;
                        }
                        else
                        {
                            StartHeader();
                        }
                    }

                    return;
                }

                case JsonTokenType.String:
                    node = JsonValue.Create(reader.GetString());
                    break;
                case JsonTokenType.Number:
                    // As a JsonElement, which converts to any numeric type like the values of JsonNode.Parse
                    node = JsonValue.Create(JsonElement.ParseValue(ref reader));
                    break;
                case JsonTokenType.True:
                case JsonTokenType.False:
                    node = JsonValue.Create(reader.GetBoolean());
                    break;
                default:
                    node = null;
                    break;
            }

            if (_containers.TryPeek(out var parent))
            {
                if (parent is JsonObject obj)
                {
                    obj[_property!] = node;
                }
                else
                {
                    parent.AsArray().Add(node);
                }
            }

            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            {
                _containers.Push(node);
            }
        }

        private static bool IsInMainFile(JsonObject location)
        {
            if (location["expansionLoc"] is JsonObject expansion)
            {
                location = expansion;
            }

            return location.ContainsKey("offset") && !location.ContainsKey("includedFrom");
        }

        /// <summary>
        /// Drops the declaration being built, which is not in the main file, and reads the rest of it for names.
        /// </summary>
        private void StartHeader()
        {
            var declaration = _containers.Last().AsObject();
            _headerDepth = _containers.Count;
            _containers.Clear();
            _lastFile = null;
            _mode = Mode.Header;
            _frames.Clear();
            _frames.Push(new Frame
            {
                Depth = _headerDepth,
                Kind = declaration["kind"]?.GetValue<string>(),
                Name = declaration["name"]?.GetValue<string>(),
            });
        }

        private void HeaderToken(ref Utf8JsonReader reader)
        {
            var property = _property;
            _property = null;
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    if (collectNames && _frames.Peek().Depth == _headerDepth)
                    {
                        _property = reader.ValueTextEquals("kind"u8) ? "kind"
                            : reader.ValueTextEquals("name"u8) ? "name"
                            : reader.ValueTextEquals("scopedEnumTag"u8) ? "scopedEnumTag"
                            : reader.ValueTextEquals("isInline"u8) ? "isInline"
                            : reader.ValueTextEquals("qualType"u8) && _basesOwner != null ? "qualType"
                            : null;
                        if (reader.ValueTextEquals("bases"u8) && _frames.Peek().IsRecord && !_frames.Peek().IsLocal)
                        {
                            _basesOwner = _frames.Peek();
                            _basesDepth = _headerDepth;
                        }
                    }

                    return;
                case JsonTokenType.StartObject:
                    _headerDepth++;
                    if (collectNames)
                    {
                        var parent = _frames.Peek();
                        _frames.Push(new Frame { Depth = _headerDepth, Parent = parent, IsLocal = parent.IsLocal || IsStatement(parent.Kind) });
                    }

                    return;
                case JsonTokenType.StartArray:
                    _headerDepth++;
                    return;
                case JsonTokenType.EndArray:
                    _headerDepth--;
                    if (_basesOwner != null && _headerDepth == _basesDepth)
                    {
                        _basesOwner = null;
                    }

                    return;
                case JsonTokenType.EndObject:
                    _headerDepth--;
                    if (collectNames)
                    {
                        var frame = _frames.Pop();
                        if (frame.IsTemplated && frame.Kind is "CXXMethodDecl" or "CXXConstructorDecl" or "CXXConversionDecl" or "CXXDestructorDecl")
                        {
                            frame.Parent.DeclaresMember = true;
                        }

                        if (!frame.IsLocal && !string.IsNullOrEmpty(frame.Name) && frame.Kind != null)
                        {
                            AddName(frame);
                        }
                    }

                    if (_headerDepth == 0)
                    {
                        _mode = Mode.Root;
                        _basesOwner = null;
                    }

                    return;
                case JsonTokenType.String when property == "kind":
                    _frames.Peek().Kind = reader.GetString();
                    return;
                case JsonTokenType.String when property == "name":
                    _frames.Peek().Name = reader.GetString();
                    return;
                case JsonTokenType.String when property == "scopedEnumTag":
                    _frames.Peek().IsScopedEnum = true;
                    return;
                case JsonTokenType.True when property == "isInline":
                    _frames.Peek().IsInline = true;
                    return;
                case JsonTokenType.String when property == "qualType" && _basesOwner?.Name is { } derived:
                    Add(_bases, derived, UnqualifiedClassName(reader.GetString()));
                    return;
            }
        }

        private static void Add(Dictionary<string, HashSet<string>> map, string key, string value)
        {
            if (!map.TryGetValue(key, out var set))
            {
                map[key] = set = new HashSet<string>(StringComparer.Ordinal);
            }

            set.Add(value);
        }

        /// <summary>
        /// The name of a class as written in a type, without qualifiers and template arguments:
        /// <c>raw_ostream</c> for <c>llvm::raw_ostream</c>, <c>base</c> for <c>detail::base&lt;T, 2&gt;</c>.
        /// </summary>
        private static string UnqualifiedClassName(string type)
        {
            var arguments = type.IndexOf('<');
            if (arguments >= 0)
            {
                type = type[..arguments];
            }

            var separator = type.LastIndexOf("::", StringComparison.Ordinal);
            return (separator >= 0 ? type[(separator + 2)..] : type).Trim();
        }

        private static bool IsStatement(string kind) => kind != null
            && (kind.EndsWith("Stmt", StringComparison.Ordinal) || kind.EndsWith("Expr", StringComparison.Ordinal)
                || kind.EndsWith("Operator", StringComparison.Ordinal));

        private void AddName(Frame frame)
        {
            var (kind, name) = (frame.Kind, frame.Name);
            switch (kind)
            {
                case "CXXRecordDecl" or "RecordDecl" or "EnumDecl" or "TypedefDecl" or "TypeAliasDecl":
                    _types.Add(name);
                    AddQualified(_qualifiedTypes, frame);
                    break;
                case "ClassTemplateDecl" or "TypeAliasTemplateDecl" or "BuiltinTemplateDecl":
                    _templates.Add(name);
                    AddQualified(_qualifiedTemplates, frame);
                    break;

                // A constructor template has the name of its class, and a member template defined outside its class
                // is declared in it; a deduction guide has spaces in its name
                case "FunctionTemplateDecl" or "VarTemplateDecl" when !(frame.IsMember && name == frame.Parent.Name)
                        && !(frame.DeclaresMember && !frame.IsMember) && !name.Contains(' '):
                    _functionTemplates.Add(name);
                    AddQualified(_qualifiedFunctionTemplates, frame);
                    if (!frame.IsMember)
                    {
                        _namespaceFunctionTemplates.Add(name);
                    }

                    break;
                case "ConceptDecl":
                    _concepts.Add(name);
                    AddQualified(_qualifiedConcepts, frame);
                    break;

                // Members are named after '.', '->' or a qualifier, where they do not hide the types of namespaces;
                // so are the enumerators of scoped enumerations
                case "FunctionDecl" or "VarDecl" when !frame.IsMember && !frame.IsTemplated:
                    _values.Add(name);
                    break;
                case "EnumConstantDecl" when frame.Parent is { IsScopedEnum: false, IsMember: false }:
                    _values.Add(name);
                    break;
            }

            // The variables and functions of classes, and the enumerators of their unscoped enumerations
            if (frame.IsMember && frame.Parent.Name is { Length: > 0 } className
                && kind is "FieldDecl" or "CXXMethodDecl" or "VarDecl" or "FunctionTemplateDecl" or "IndirectFieldDecl")
            {
                Add(_members, className, name);
            }
            else if (kind == "EnumConstantDecl" && frame.Parent is { IsScopedEnum: false, IsMember: true } enumeration
                && enumeration.Parent.Name is { Length: > 0 } enclosing)
            {
                Add(_members, enclosing, name);
            }
        }

        /// <summary>
        /// Adds the name of <paramref name="frame"/> qualified by the names of its enclosing namespaces and classes,
        /// without inline and unnamed namespaces; nothing for a name that is not in a namespace or class.
        /// </summary>
        private static void AddQualified(HashSet<string> names, Frame frame)
        {
            var qualifiers = new List<string>();
            for (var scope = frame.Parent; scope != null; scope = scope.Parent)
            {
                if ((scope.Kind == "NamespaceDecl" && !scope.IsInline) || scope.IsRecord)
                {
                    if (string.IsNullOrEmpty(scope.Name))
                    {
                        // An unnamed class: its members are not named through it
                        if (scope.IsRecord)
                        {
                            return;
                        }

                        continue;
                    }

                    qualifiers.Add(scope.Name);
                }
            }

            if (qualifiers.Count != 0)
            {
                qualifiers.Reverse();
                names.Add(string.Join("::", qualifiers) + "::" + frame.Name);
            }
        }

        /// <summary>
        /// The members of a class and of its bases, by the unqualified names of the classes.
        /// </summary>
        private IReadOnlySet<string> MembersWithBases(string className, HashSet<string> visited)
        {
            var members = new HashSet<string>(_members.GetValueOrDefault(className) ?? [], StringComparer.Ordinal);
            if (visited.Add(className) && _bases.TryGetValue(className, out var bases))
            {
                foreach (var @base in bases)
                {
                    members.UnionWith(MembersWithBases(@base, visited));
                }
            }

            return members;
        }

        /// <summary>
        /// The names of types, templates and concepts, unqualified and qualified by their namespaces and classes. An
        /// unqualified name that some header also declares as a variable, function, function template or
        /// enumerator outside classes (<c>struct stat</c> and <c>stat()</c>) is left out of the types. A class
        /// template is no function template.
        /// </summary>
        public HeaderNames HeaderNames()
        {
            // A type is left out when a namespace also has a function, variable or function template of its name:
            // std::system_error and fmt::system_error(); std::system_error is then known by its qualified name
            var types = _types.Where(name => !_values.Contains(name) && !_templates.Contains(name) && !_namespaceFunctionTemplates.Contains(name))
                .Concat(_qualifiedTypes)
                .ToHashSet(StringComparer.Ordinal);
            var templates = _templates.Concat(_qualifiedTemplates).ToHashSet(StringComparer.Ordinal);
            var functionTemplates = _functionTemplates.Where(name => !_templates.Contains(name))
                .Concat(_qualifiedFunctionTemplates)
                .ToHashSet(StringComparer.Ordinal);
            var concepts = _concepts.Concat(_qualifiedConcepts).ToHashSet(StringComparer.Ordinal);
            // The class and its bases are named by their injected-class-names, which a member of another class of
            // the same name must not hide
            var members = _members.Keys.Concat(_bases.Keys).Distinct().ToDictionary(
                name => name,
                name =>
                {
                    var classes = new HashSet<string>(StringComparer.Ordinal);
                    var withBases = MembersWithBases(name, classes);
                    return (IReadOnlySet<string>)withBases.Where(member => !classes.Contains(member)).ToHashSet(StringComparer.Ordinal);
                },
                StringComparer.Ordinal);
            return new HeaderNames(types, templates, functionTemplates, concepts) { ClassMembers = members };
        }
    }

    // ========================================
    // Normalized trees
    // ========================================

    /// <summary>
    /// The declarations without positions, clang addresses and comments: equal for two sources that
    /// differ only in formatting.
    /// </summary>
    public JsonArray Normalize()
    {
        var result = new JsonArray();
        foreach (var declaration in Declarations)
        {
            result.Add(Normalize(declaration));
        }

        return result;
    }

    private static JsonNode Normalize(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var result = new JsonObject();
                var fromPreprocessor = IsFromPreprocessor(obj);
                foreach (var (key, value) in obj)
                {
                    if (IgnoredKeys.Contains(key) || (key is "value" or "type" && fromPreprocessor))
                    {
                        continue;
                    }

                    if (key == "inner" && value is JsonArray inner)
                    {
                        var children = new JsonArray();
                        foreach (var child in inner)
                        {
                            // Documentation comments are attached to declarations; the writer does not keep them
                            if (child?["kind"]?.GetValue<string>() is { } kind && kind.EndsWith("Comment", StringComparison.Ordinal))
                            {
                                continue;
                            }

                            children.Add(Normalize(child));
                        }

                        // A declaration whose only children were comments has none
                        if (children.Count != 0)
                        {
                            result[key] = children;
                        }

                        continue;
                    }

                    result[key] = Normalize(value);
                }

                return result;
            }

            case JsonArray array:
            {
                var result = new JsonArray();
                foreach (var item in array)
                {
                    result.Add(Normalize(item));
                }

                return result;
            }

            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
            {
                // Types of lambdas and unnamed classes name their position: (lambda at <stdin>:3:12)
                var text = value.GetValue<string>();
                return JsonValue.Create(StdinLocation().Replace(text, "<stdin>"));
            }

            default:
                return node?.DeepClone();
        }
    }

    /// <summary>
    /// A literal that the preprocessor made, spelled in clang's scratch space: the value and type of
    /// <c>__LINE__</c>, <c>__FILE__</c> or of a stringized macro argument depend on the layout of the source,
    /// which the writer does not keep.
    /// </summary>
    private static bool IsFromPreprocessor(JsonObject node)
    {
        return node["range"]?["begin"]?["spellingLoc"]?["file"]?.GetValue<string>() == "<scratch space>";
    }

    [GeneratedRegex(@"<stdin>:\d+:\d+")]
    private static partial Regex StdinLocation();

    /// <summary>
    /// Describes the first difference between two normalized trees, or returns null when they are equal.
    /// </summary>
    public static string FirstDifference(JsonNode expected, JsonNode actual, string path = "")
    {
        if (JsonNode.DeepEquals(expected, actual))
        {
            return null;
        }

        switch (expected)
        {
            case JsonObject e when actual is JsonObject a:
            {
                var here = e["kind"] is JsonNode kind ? $"{path}/{kind}{Name(e)}" : path;
                foreach (var (key, value) in e)
                {
                    if (!a.TryGetPropertyValue(key, out var other))
                    {
                        return $"{here}: '{key}' is missing in the written code";
                    }

                    var difference = FirstDifference(value, other, key == "inner" ? here : $"{here}.{key}");
                    if (difference != null)
                    {
                        return difference;
                    }
                }

                var extra = a.Select(p => p.Key).FirstOrDefault(k => !e.ContainsKey(k));
                return $"{here}: the written code has '{extra}'";
            }

            case JsonArray e when actual is JsonArray a:
            {
                for (var i = 0; i < Math.Min(e.Count, a.Count); i++)
                {
                    var difference = FirstDifference(e[i], a[i], path);
                    if (difference != null)
                    {
                        return difference;
                    }
                }

                return $"{path}: {e.Count} children, the written code has {a.Count}";
            }

            default:
                return $"{path}: expected {Shorten(expected)}, written {Shorten(actual)}";
        }
    }

    private static string Name(JsonObject node) => node["name"] is JsonNode name ? $" {name}" : "";

    private static string Shorten(JsonNode node)
    {
        var text = node?.ToJsonString() ?? "null";
        return text.Length <= 80 ? text : text[..77] + "...";
    }

    // ========================================
    // Ranges
    // ========================================

    /// <summary>
    /// Every node of the main file's declarations that has a source range. <paramref name="source"/> is the
    /// parsed input and <paramref name="bomLength"/> the length of its byte order mark, which clang's offsets
    /// count and ours do not.
    /// </summary>
    public List<ClangNode> Nodes(byte[] source, int bomLength)
    {
        foreach (var declaration in Declarations)
        {
            CorrectCallStarts(declaration);
        }

        var nodes = new List<ClangNode>();
        var stack = new Stack<JsonNode>(Declarations);
        while (stack.Count != 0)
        {
            switch (stack.Pop())
            {
                case JsonObject obj:
                    if (obj["kind"] is JsonValue kind && obj["range"] is JsonObject range
                        && TryGetOffset(range["begin"], out var begin, out var beginFromMacro, source)
                        && TryGetOffset(range["end"], out var end, out var endFromMacro, source))
                    {
                        var name = TryGetOffset(obj["loc"], out var nameLocation, out _) ? SkipSplices(source, nameLocation.Offset) - bomLength : -1;
                        var endOffset = endFromMacro ? MacroUseEnd(source, end.Offset, end.TokenLength) : end.Offset + EndTokenLength(source, end.Offset, end.TokenLength);
                        var span = new TextSpan(SkipSplices(source, begin.Offset) - bomLength, endOffset - bomLength);
                        var type = obj["type"]?["qualType"]?.GetValue<string>();
                        nodes.Add(new ClangNode(kind.GetValue<string>(), span, name, beginFromMacro || endFromMacro, obj["value"], type)
                        {
                            InMacro = beginFromMacro && endFromMacro && begin.Offset == end.Offset,
                        });
                    }

                    foreach (var (_, value) in obj)
                    {
                        if (value is JsonObject or JsonArray)
                        {
                            stack.Push(value);
                        }
                    }

                    break;

                case JsonArray array:
                    foreach (var item in array)
                    {
                        stack.Push(item);
                    }

                    break;
            }
        }

        return nodes;
    }

    /// <summary>
    /// Clang 18 represents the call of a member function with an explicit object parameter (<c>obj.f()</c>)
    /// as a call of <c>f</c> with the object as an argument, which starts at <c>f</c> instead of the object;
    /// so do the expressions that start with the call. Corrects their ranges to start at the object, and
    /// returns the original start of <paramref name="node"/> (-1 when it has none) and its corrected start.
    /// </summary>
    private static (int Original, JsonObject Begin) CorrectCallStarts(JsonObject node)
    {
        var begin = node["range"]?["begin"] as JsonObject;
        var original = TryGetOffset(begin, out var location, out _) ? location.Offset : -1;
        JsonObject earliest = null;
        var earliestOffset = int.MaxValue;
        JsonObject fromChild = null;
        if (node["inner"] is JsonArray inner)
        {
            foreach (var child in inner.OfType<JsonObject>())
            {
                var (childOriginal, childBegin) = CorrectCallStarts(child);
                if (!TryGetOffset(childBegin, out var childLocation, out _))
                {
                    continue;
                }

                if (childLocation.Offset < earliestOffset)
                {
                    earliestOffset = childLocation.Offset;
                    earliest = childBegin;
                }

                if (childOriginal == original && childLocation.Offset < original)
                {
                    fromChild = childBegin;
                }
            }
        }

        var kind = node["kind"]?.GetValue<string>() ?? "";
        var corrected = kind == "CallExpr" && original >= 0 && earliestOffset < original ? earliest
            : fromChild != null && (kind.EndsWith("Expr", StringComparison.Ordinal) || kind.EndsWith("Operator", StringComparison.Ordinal)) ? fromChild
            : null;
        if (corrected == null)
        {
            return (original, begin);
        }

        var copy = corrected.DeepClone().AsObject();
        node["range"]!["begin"] = copy;
        return (original, copy);
    }

    /// <summary>
    /// The length of the last token of a node at <paramref name="offset"/>. A node that ends with a '&gt;' that
    /// closes template arguments ends at the first character of <c>&gt;&gt;</c>, <c>&gt;=</c> or <c>&gt;&gt;=</c>
    /// (<c>A&lt;B&lt;int&gt;&gt;</c>), for which clang gives the length of the whole token; only the name of an
    /// operator function (<c>operator&gt;&gt;</c>) ends with such a token.
    /// </summary>
    private static int EndTokenLength(byte[] source, int offset, int tokenLength)
    {
        if (tokenLength < 2 || offset >= source.Length || source[offset] != '>')
        {
            return tokenLength;
        }

        var i = offset - 1;
        while (i >= 0 && source[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            i--;
        }

        return i >= 7 && source.AsSpan(i - 7, 8).SequenceEqual("operator"u8) ? tokenLength : 1;
    }

    /// <summary>
    /// The end of the macro use whose name is at <paramref name="offset"/>: after the parenthesized
    /// arguments that follow the name, if any.
    /// </summary>
    private static int MacroUseEnd(byte[] source, int offset, int nameLength)
    {
        var end = offset + nameLength;
        var i = end;
        while (i < source.Length)
        {
            if (source[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\v' or (byte)'\f')
            {
                i++;
            }
            else if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                var close = source.AsSpan(i + 2).IndexOf("*/"u8);
                i = close < 0 ? source.Length : i + close + 4;
            }
            else if (source[i] == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                var newLine = source.AsSpan(i).IndexOf((byte)'\n');
                i = newLine < 0 ? source.Length : i + newLine;
            }
            else
            {
                break;
            }
        }

        if (i >= source.Length || source[i] != '(')
        {
            return end;
        }

        var depth = 0;
        for (; i < source.Length; i++)
        {
            switch (source[i])
            {
                case (byte)'(':
                    depth++;
                    break;
                case (byte)')':
                    if (--depth == 0)
                    {
                        return i + 1;
                    }

                    break;
                case (byte)'"' or (byte)'\'':
                    // Skip a literal, whose parentheses do not count
                    var quote = source[i];
                    for (i++; i < source.Length && source[i] != quote; i++)
                    {
                        if (source[i] == '\\')
                        {
                            i++;
                        }
                    }

                    break;
            }
        }

        return end;
    }

    /// <summary>
    /// The offset after the line splices at <paramref name="offset"/>. Clang's lexer starts a token at a
    /// splice right before it (<c>+\⏎2</c>: the token <c>2</c> starts at the backslash); our tokens start at
    /// their first character, and splices are trivia.
    /// </summary>
    private static int SkipSplices(byte[] source, int offset)
    {
        while (offset < source.Length && source[offset] == '\\')
        {
            var next = offset + 1;
            if (next < source.Length && source[next] == '\r')
            {
                next++;
            }

            if (next < source.Length && source[next] == '\n')
            {
                next++;
            }

            if (next == offset + 1)
            {
                break;
            }

            offset = next;
        }

        return offset;
    }

    /// <summary>
    /// The offset and token length of a location; for a location in a macro expansion, those of the
    /// macro use in the source. Clang splits the <c>&gt;&gt;</c> that closes two template argument lists into
    /// two '&gt;' in its scratch space, which look like a macro expansion located at the <c>&gt;&gt;</c>: with
    /// <paramref name="source"/>, such a location is not from a macro.
    /// </summary>
    private static bool TryGetOffset(JsonNode location, out (int Offset, int TokenLength) result, out bool fromMacro, byte[] source = null)
    {
        result = default;
        fromMacro = false;
        if (location is not JsonObject obj)
        {
            return false;
        }

        if (obj["expansionLoc"] is JsonObject expansion)
        {
            fromMacro = !(source != null && obj["spellingLoc"]?["file"]?.GetValue<string>() == "<scratch space>"
                && expansion["offset"] is JsonValue split && split.GetValue<int>() < source.Length && source[split.GetValue<int>()] == '>');
            obj = expansion;
        }

        if (obj["offset"] is not JsonValue offset || obj.ContainsKey("includedFrom"))
        {
            return false;
        }

        result = (offset.GetValue<int>(), obj["tokLen"]?.GetValue<int>() ?? 0);
        return true;
    }

    // ========================================
    // Tokens
    // ========================================

    /// <summary>
    /// The tokens of <paramref name="source"/> as clang's raw lexer sees them
    /// (<c>-Xclang -dump-raw-tokens</c>), including those of directives and inactive branches.
    /// Offsets are in bytes without the byte order mark.
    /// </summary>
    public static List<TextSpan> RawTokens(byte[] source, int bomLength)
    {
        var run = Clang.Run(source, ["-fsyntax-only", "-Xclang", "-dump-raw-tokens"]);

        // The start of each line: clang ends lines at \n, \r\n and \r
        var lineStarts = new List<int> { 0 };
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] == '\n' || (source[i] == '\r' && (i + 1 >= source.Length || source[i + 1] != '\n')))
            {
                lineStarts.Add(i + 1);
            }
        }

        var tokens = new List<TextSpan>();
        foreach (Match match in RawToken().Matches(run.Errors))
        {
            var kind = match.Groups["kind"].Value;
            if (kind is "unknown" or "comment" or "eof")
            {
                continue;
            }

            var line = int.Parse(match.Groups["line"].Value);
            var column = int.Parse(match.Groups["column"].Value);
            if (line > lineStarts.Count)
            {
                continue;
            }

            // A token with line splices has its spelling without them and a flag with the source text
            var start = lineStarts[line - 1] + column - 1;
            var unclean = match.Groups["unclean"];
            var end = start + Encoding.UTF8.GetByteCount(unclean.Success ? unclean.Value : match.Groups["spelling"].Value);
            start = SkipSplices(source, start);

            // The '>' of '>>', '>=' and '>>=' can close template arguments: A<B<int>>
            while (!unclean.Success && kind is "greatergreater" or "greaterequal" or "greatergreaterequal" && start + 1 < end && source[start] == '>')
            {
                tokens.Add(new TextSpan(start - bomLength, start + 1 - bomLength));
                start++;
                kind = source[start] == '>' ? "greatergreater" : "";
            }

            tokens.Add(new TextSpan(start - bomLength, end - bomLength));
        }

        return tokens;
    }

    // raw_identifier 'int'	 [StartOfLine]	Loc=<<stdin>:1:1>; the spelling of a raw string literal spans lines,
    // and so does the flag [UnClean='ma\⏎in'] of a token with line splices
    [GeneratedRegex(@"^(?<kind>\w+) '(?<spelling>.*?)'\t(?: \[(?:UnClean='(?<unclean>.*?)'|[^\]\n]*)\])*\tLoc=<(?<file>[^\n]*?):(?<line>\d+):(?<column>\d+)>\r?$", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex RawToken();
}
