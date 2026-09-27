using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using PaspanParsers.Cpp;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// Checks the values of our literals against the <c>value</c> clang writes for the literal with the same
/// span. Literals with a user-defined suffix and literals without a value are not checked.
/// </summary>
public static class CppLiteralChecker
{
    /// <summary>
    /// Returns null when the values are right, otherwise a description of the first wrong value.
    /// </summary>
    public static string Check(TranslationUnit unit, IReadOnlyList<ClangNode> clangNodes)
    {
        // Literals in macro uses are not compared
        var bySpan = clangNodes.Where(n => !n.FromMacro).ToLookup(n => n.Span);
        var stack = new Stack<ICppNode>([unit]);
        while (stack.Count != 0)
        {
            var node = stack.Pop();
            var problem = node switch
            {
                LiteralExpression { UserDefinedSuffix: null, Value: not null } literal => CheckLiteral(literal, bySpan[literal.Span]),
                ConcatenatedStringExpression { UserDefinedSuffix: null, Value: not null } concatenation =>
                    CheckString(concatenation.Value, concatenation.Encoding, bySpan[concatenation.Span]),
                _ => null,
            };

            if (problem != null)
            {
                return $"{node.GetType().Name} {node.Span}: {problem}";
            }

            // The parts of a concatenation are not literals of their own
            if (node is not ConcatenatedStringExpression)
            {
                foreach (var child in CppSpanChecker.Children(node))
                {
                    stack.Push(child);
                }
            }
        }

        return null;
    }

    private static string CheckLiteral(LiteralExpression literal, IEnumerable<ClangNode> nodes)
    {
        switch (literal.Kind)
        {
            case LiteralKind.Integer:
            {
                var node = nodes.FirstOrDefault(n => n.Kind == "IntegerLiteral");
                var expected = node?.Value?.GetValue<string>();
                return node == null || expected == ((ulong)literal.Value).ToString(CultureInfo.InvariantCulture)
                    ? null
                    : $"integer {literal.Text} is {literal.Value}, clang has {expected}";
            }

            case LiteralKind.Floating:
            {
                var node = nodes.FirstOrDefault(n => n.Kind == "FloatingLiteral");
                return node == null ? null : CheckFloating(literal, node);
            }

            case LiteralKind.Character:
            {
                var node = nodes.FirstOrDefault(n => n.Kind == "CharacterLiteral");
                if (node == null)
                {
                    return null;
                }

                var expected = node.Value.GetValue<long>();
                var value = (long)literal.Value;

                // The signedness of char depends on the target
                var matches = (uint)value == expected || (node.Type == "char" && (uint)(sbyte)value == expected);
                return matches ? null : $"character {literal.Text} is {value}, clang has {expected}";
            }

            case LiteralKind.String:
                return CheckString(literal.Value, literal.Encoding, nodes);

            default:
                return null;
        }
    }

    private static string CheckFloating(LiteralExpression literal, ClangNode node)
    {
        var expected = node.Value.GetValue<string>();
        var clang = ParseClangFloating(expected);
        var value = (double)literal.Value;
        bool matches;
        switch (node.Type)
        {
            case "float":
                matches = (float)value == (float)clang || (double.IsNaN(value) && double.IsNaN(clang));
                break;
            case "double":
            case "long double":
                matches = value == clang || (double.IsNaN(value) && double.IsNaN(clang));
                break;
            default:
                // Other floating types (_Float16, __bf16) are not compared
                return null;
        }

        return matches ? null : $"floating {literal.Text} is {value:R}, clang has {expected}";
    }

    private static double ParseClangFloating(string text) => text switch
    {
        "+Inf" => double.PositiveInfinity,
        "-Inf" => double.NegativeInfinity,
        "NaN" or "+NaN" or "-NaN" => double.NaN,
        _ => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture),
    };

    private static string CheckString(object value, CharacterEncoding encoding, IEnumerable<ClangNode> nodes)
    {
        var node = nodes.FirstOrDefault(n => n.Kind == "StringLiteral");
        if (node == null)
        {
            return null;
        }

        var expected = node.Value.GetValue<string>();
        var written = ClangRepresentation(CodeUnits(value, encoding), encoding);
        return written == expected ? null : $"string is {written}, clang has {expected}";
    }

    /// <summary>
    /// The code units of a string value: bytes of ordinary and UTF-8 strings, UTF-16 code units of
    /// <c>u</c> strings, code points of <c>U</c> and <c>L</c> strings.
    /// </summary>
    private static List<uint> CodeUnits(object value, CharacterEncoding encoding)
    {
        return value switch
        {
            byte[] bytes => bytes.Select(b => (uint)b).ToList(),
            string text when encoding == CharacterEncoding.Utf16 => text.Select(c => (uint)c).ToList(),
            string text => text.EnumerateRunes().Select(r => (uint)r.Value).ToList(),
            _ => throw new InvalidOperationException($"Unexpected string value {value?.GetType().Name}"),
        };
    }

    /// <summary>
    /// The text clang writes for a string literal (<c>StringLiteral::outputString</c>): the prefix, printable
    /// ASCII as is, C escapes, octal escapes for other units up to 0xFF, <c>\u</c>/<c>\U</c> for code points
    /// and <c>\x</c> for wide units above 0xFF.
    /// </summary>
    public static string ClangRepresentation(IReadOnlyList<uint> units, CharacterEncoding encoding)
    {
        const string Hex = "0123456789ABCDEF";
        var builder = new StringBuilder();
        builder.Append(encoding switch
        {
            CharacterEncoding.Wide => "L",
            CharacterEncoding.Utf8 => "u8",
            CharacterEncoding.Utf16 => "u",
            CharacterEncoding.Utf32 => "U",
            _ => "",
        });

        builder.Append('"');
        var lastSlashX = units.Count;
        for (var i = 0; i < units.Count; i++)
        {
            var c = units[i];
            var escaped = c switch
            {
                '\\' => "\\\\",
                '\a' => "\\a",
                '\b' => "\\b",
                '\f' => "\\f",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\v' => "\\v",
                '"' => "\\\"",
                _ => null,
            };

            if (escaped != null)
            {
                builder.Append(escaped);
                continue;
            }

            // Surrogate pairs of UTF-16 strings are written as code points
            if (encoding == CharacterEncoding.Utf16 && i != units.Count - 1 && c is >= 0xD800 and <= 0xDBFF && units[i + 1] is >= 0xDC00 and <= 0xDFFF)
            {
                c = 0x10000 + ((c - 0xD800) << 10) + (units[i + 1] - 0xDC00);
                i++;
            }

            if (c > 0xFF)
            {
                if (encoding == CharacterEncoding.Wide || c is >= 0xD800 and <= 0xDFFF || c >= 0x110000)
                {
                    builder.Append("\\x");
                    var shift = 28;
                    while ((c >> shift) == 0)
                    {
                        shift -= 4;
                    }

                    for (; shift >= 0; shift -= 4)
                    {
                        builder.Append(Hex[(int)((c >> shift) & 15)]);
                    }

                    lastSlashX = i;
                    continue;
                }

                if (c > 0xFFFF)
                {
                    builder.Append("\\U00").Append(Hex[(int)((c >> 20) & 15)]).Append(Hex[(int)((c >> 16) & 15)]);
                }
                else
                {
                    builder.Append("\\u");
                }

                builder.Append(Hex[(int)((c >> 12) & 15)]).Append(Hex[(int)((c >> 8) & 15)]).Append(Hex[(int)((c >> 4) & 15)]).Append(Hex[(int)(c & 15)]);
                continue;
            }

            // A hexadecimal digit after \x... would be read as part of it
            if (lastSlashX + 1 == i && char.IsAsciiHexDigit((char)c))
            {
                builder.Append("\"\"");
            }

            if (c is >= 0x20 and <= 0x7E)
            {
                builder.Append((char)c);
            }
            else
            {
                builder.Append('\\').Append((char)('0' + ((c >> 6) & 7))).Append((char)('0' + ((c >> 3) & 7))).Append((char)('0' + (c & 7)));
            }
        }

        builder.Append('"');
        return builder.ToString();
    }
}
