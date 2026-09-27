using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// A node of clang's AST in the main file: its kind, its source range and, for declarations, the
/// location of the declared name. Offsets are bytes of the parsed input without the byte order mark.
/// <see cref="FromMacro"/> is set when the range comes from a macro expansion: it is then the range of
/// the macro use in the source. <see cref="Value"/> is the <c>value</c> of a literal and
/// <see cref="Type"/> the type of an expression, as clang writes them.
/// </summary>
public sealed record ClangNode(string Kind, TextSpan Span, int NameOffset, bool FromMacro, JsonNode Value = null, string Type = null);

/// <summary>
/// Reads clang's JSON AST dump (<c>-Xclang -ast-dump=json</c>): the top-level declarations of the main
/// file, normalized for comparison, and the ranges of all their nodes.
/// </summary>
/// <remarks>
/// A dump that includes standard headers is about 100 MB. It is read with <see cref="Utf8JsonReader"/>,
/// and only the declarations of the main file are materialized: a location in the main file has no
/// <c>includedFrom</c> (clang omits the <c>file</c> of a location when it did not change, but always
/// writes <c>includedFrom</c>), and implicit declarations have empty locations.
/// </remarks>
public sealed partial class ClangAst
{
    /// <summary>
    /// Keys whose values depend on positions or on addresses of the clang process, not on the tree.
    /// </summary>
    private static readonly HashSet<string> IgnoredKeys = ["id", "loc", "range", "previousDecl", "parentDeclContextId", "referencedMemberDecl"];

    private ClangAst(List<JsonObject> declarations)
    {
        Declarations = declarations;
    }

    /// <summary>
    /// The top-level declarations of the main file, as dumped.
    /// </summary>
    public IReadOnlyList<JsonObject> Declarations { get; }

    public static ClangAst Read(byte[] json)
    {
        var declarations = new List<JsonObject>();
        var reader = new Utf8JsonReader(json, new JsonReaderOptions { MaxDepth = 100_000 });

        // { "id": ..., "kind": "TranslationUnitDecl", ..., "inner": [ declarations ] }
        reader.Read();
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (!reader.ValueTextEquals("inner"u8))
            {
                reader.Read();
                reader.Skip();
                continue;
            }

            reader.Read();
            while (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
            {
                if (IsInMainFile(reader))
                {
                    declarations.Add(JsonNode.Parse(ref reader, new JsonNodeOptions()).AsObject());
                }
                else
                {
                    reader.Skip();
                }
            }
        }

        return new ClangAst(declarations);
    }

    /// <summary>
    /// Looks ahead at the location of the declaration that starts at the reader, which is not moved.
    /// </summary>
    private static bool IsInMainFile(Utf8JsonReader reader)
    {
        var depth = reader.CurrentDepth;
        while (reader.Read() && reader.CurrentDepth > depth)
        {
            if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != depth + 1)
            {
                continue;
            }

            if (reader.ValueTextEquals("loc"u8))
            {
                reader.Read();
                var location = JsonNode.Parse(ref reader)?.AsObject();
                if (location?["expansionLoc"] is JsonObject expansion)
                {
                    location = expansion;
                }

                return location != null && location.ContainsKey("offset") && !location.ContainsKey("includedFrom");
            }

            if (reader.ValueTextEquals("inner"u8))
            {
                // The location comes before the children
                return false;
            }

            reader.Read();
            reader.Skip();
        }

        return false;
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
                foreach (var (key, value) in obj)
                {
                    if (IgnoredKeys.Contains(key))
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

                        result[key] = children;
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
        var nodes = new List<ClangNode>();
        var stack = new Stack<JsonNode>(Declarations);
        while (stack.Count != 0)
        {
            switch (stack.Pop())
            {
                case JsonObject obj:
                    if (obj["kind"] is JsonValue kind && obj["range"] is JsonObject range
                        && TryGetOffset(range["begin"], out var begin, out var beginFromMacro)
                        && TryGetOffset(range["end"], out var end, out var endFromMacro))
                    {
                        var name = TryGetOffset(obj["loc"], out var nameLocation, out _) ? SkipSplices(source, nameLocation.Offset) - bomLength : -1;
                        var span = new TextSpan(SkipSplices(source, begin.Offset) - bomLength, end.Offset + end.TokenLength - bomLength);
                        var type = obj["type"]?["qualType"]?.GetValue<string>();
                        nodes.Add(new ClangNode(kind.GetValue<string>(), span, name, beginFromMacro || endFromMacro, obj["value"], type));
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
    /// macro use in the source.
    /// </summary>
    private static bool TryGetOffset(JsonNode location, out (int Offset, int TokenLength) result, out bool fromMacro)
    {
        result = default;
        fromMacro = false;
        if (location is not JsonObject obj)
        {
            return false;
        }

        if (obj["expansionLoc"] is JsonObject expansion)
        {
            obj = expansion;
            fromMacro = true;
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
            tokens.Add(new TextSpan(SkipSplices(source, start) - bomLength, end - bomLength));
        }

        return tokens;
    }

    // raw_identifier 'int'	 [StartOfLine]	Loc=<<stdin>:1:1>; the spelling of a raw string literal spans lines,
    // and so does the flag [UnClean='ma\⏎in'] of a token with line splices
    [GeneratedRegex(@"^(?<kind>\w+) '(?<spelling>.*?)'\t(?: \[(?:UnClean='(?<unclean>.*?)'|[^\]\n]*)\])*\tLoc=<(?<file>[^\n]*?):(?<line>\d+):(?<column>\d+)>\r?$", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex RawToken();
}
