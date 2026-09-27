using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Paspan;
using PaspanParsers.CSharp;

namespace PaspanParsers.Tests.CSharp;

/// <summary>
/// The C# parsers kept side by side: the hand-written recursive descent parser and the hybrid parser.
/// </summary>
public enum CSharpParserVariant
{
    RecursiveDescent,
    Hybrid,
}

/// <summary>
/// Runs every variant of the C# parser on the same input. The tests parse through here, so each of them
/// also checks that the hybrid parser gives the same result as the recursive descent parser: both fail,
/// or both succeed with ASTs equal down to the spans.
/// </summary>
public static class ParserVariants
{
    public static readonly CSharpParserVariant[] All = Enum.GetValues<CSharpParserVariant>();

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Properties = new();

    public static bool TryParse(CSharpParserVariant variant, string input, PaspanParsers.CSharp.CSharpParseOptions options, out CompilationUnit result, out ParseError error)
    {
        return variant switch
        {
            CSharpParserVariant.RecursiveDescent => CSharpParser.TryParse(input, options, out result, out error),
            CSharpParserVariant.Hybrid => CSharpHybridParser.TryParse(input, options, out result, out error),
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };
    }

    /// <summary>
    /// Parses with every variant and returns the result of the recursive descent parser, or null.
    /// </summary>
    public static CompilationUnit Parse(string input, PaspanParsers.CSharp.CSharpParseOptions options = null)
    {
        var expected = CSharpParser.Parse(input, options);
        foreach (var variant in All.Where(v => v != CSharpParserVariant.RecursiveDescent))
        {
            TryParse(variant, input, options, out var actual, out _);
            var difference = Compare(expected, actual);
            if (difference != null)
            {
                Assert.Fail($"{variant} differs from {CSharpParserVariant.RecursiveDescent}: {difference}\n{input}");
            }
        }

        return expected;
    }

    /// <summary>
    /// Null when the two ASTs are equal: the same node types with equal properties, spans included.
    /// Otherwise the path of the first difference.
    /// </summary>
    public static string Compare(CompilationUnit expected, CompilationUnit actual)
    {
        var visited = new HashSet<(object, object)>(PairComparer.Instance);
        var stack = new Stack<(object Expected, object Actual, string Path)>();
        stack.Push((expected, actual, "unit"));

        while (stack.Count != 0)
        {
            var (x, y, path) = stack.Pop();
            if (x == null || y == null)
            {
                if (x != y)
                {
                    return $"{path}: {Describe(x)} vs {Describe(y)}";
                }

                continue;
            }

            var type = x.GetType();
            if (type != y.GetType())
            {
                return $"{path}: {type.Name} vs {y.GetType().Name}";
            }

            if (type.IsValueType || x is string)
            {
                if (!x.Equals(y))
                {
                    return $"{path}: {x} vs {y}";
                }

                continue;
            }

            if (!visited.Add((x, y)))
            {
                continue;
            }

            if (x is IEnumerable xs)
            {
                var xl = xs.Cast<object>().ToList();
                var yl = ((IEnumerable)y).Cast<object>().ToList();
                if (xl.Count != yl.Count)
                {
                    return $"{path}: {xl.Count} items vs {yl.Count}";
                }

                for (var i = xl.Count - 1; i >= 0; i--)
                {
                    stack.Push((xl[i], yl[i], $"{path}[{i}]"));
                }

                continue;
            }

            foreach (var property in Properties.GetOrAdd(type, GetProperties))
            {
                stack.Push((property.GetValue(x), property.GetValue(y), $"{path}.{property.Name}"));
            }
        }

        return null;
    }

    private static PropertyInfo[] GetProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .OrderByDescending(p => p.Name, StringComparer.Ordinal)
            .ToArray();

    private static string Describe(object value) => value == null ? "null" : value.GetType().Name;

    private sealed class PairComparer : IEqualityComparer<(object, object)>
    {
        public static readonly PairComparer Instance = new();

        public bool Equals((object, object) a, (object, object) b) => ReferenceEquals(a.Item1, b.Item1) && ReferenceEquals(a.Item2, b.Item2);

        public int GetHashCode((object, object) pair) => HashCode.Combine(RuntimeHelpers.GetHashCode(pair.Item1), RuntimeHelpers.GetHashCode(pair.Item2));
    }
}
