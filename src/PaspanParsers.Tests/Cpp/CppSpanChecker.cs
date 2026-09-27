using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using PaspanParsers.Cpp;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// Checks the spans and the kinds of our AST nodes against clang. Every node lies inside its parent, and
/// by <see cref="CppKindMap"/> it either has the span of a clang node of a matching kind, or declares a
/// name where clang has a declaration of a matching kind, or (for nodes clang's dump has no ranges for,
/// such as types and declarators) starts and ends at token boundaries.
/// </summary>
/// <remarks>
/// Writing the tree back and comparing clang's trees cannot tell <c>a * b;</c> parsed as a declaration
/// from the same text parsed as a multiplication; the kinds of the nodes can.
/// </remarks>
public static class CppSpanChecker
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ChildProperties = new();

    /// <summary>
    /// Returns null when the spans and kinds are right, otherwise a description of the first wrong node.
    /// </summary>
    public static string Check(byte[] utf8, TranslationUnit unit, IReadOnlyList<ClangNode> clangNodes, IReadOnlyList<TextSpan> tokens)
    {
        var bySpan = clangNodes.ToLookup(n => n.Span);
        var byName = clangNodes.Where(n => n.NameOffset >= 0).ToLookup(n => n.NameOffset);
        var tokenStarts = tokens.Select(t => t.Start).ToHashSet();
        var tokenEnds = tokens.Select(t => t.End).ToHashSet();
        var sortedEnds = CodeTokenEnds(tokenEnds, unit);
        var macroUses = new MacroUses(clangNodes.Where(n => n.InMacro).Select(n => n.Span));

        if (unit.Span != new TextSpan(0, utf8.Length))
        {
            return Describe($"the translation unit must span the input [0..{utf8.Length})", unit, unit, utf8);
        }

        var stack = new Stack<(ICppNode Node, ICppNode Parent)>();
        foreach (var child in Children(unit))
        {
            stack.Push((child, unit));
        }

        while (stack.Count != 0)
        {
            var (node, parent) = stack.Pop();
            var span = node.Span;

            if (span.Start < 0 || span.End > utf8.Length || span.Start > span.End)
            {
                return Describe("invalid span", node, parent, utf8);
            }

            if (parent is not TranslationUnit && !parent.Span.Contains(span))
            {
                return Describe($"outside its parent {parent.GetType().Name} {parent.Span}", node, parent, utf8);
            }

            var rule = CppKindMap.RuleFor(node, parent);
            var problem = rule switch
            {
                null => $"no rule in {nameof(CppKindMap)} for {node.GetType().Name}",

                // Clang's nodes from a macro use all have the span of the use: the parts of the use are not checked
                _ when macroUses.StrictlyContain(span) => null,
                ExactRule exact => CheckExact(exact, span, bySpan, utf8, sortedEnds),
                DeclarationRule declaration => CheckDeclaration(declaration, node, byName),
                TokensRule => tokenStarts.Contains(span.Start) && tokenEnds.Contains(span.End)
                    ? null
                    : "the span does not start and end at token boundaries",
                _ => throw new InvalidOperationException(),
            };

            if (problem != null)
            {
                return Describe(problem, node, parent, utf8);
            }

            foreach (var child in Children(node))
            {
                stack.Push((child, node));
            }
        }

        return null;
    }

    private static string CheckExact(ExactRule rule, TextSpan span, ILookup<TextSpan, ClangNode> bySpan, byte[] utf8, int[] sortedTokenEnds)
    {
        bool Matches(TextSpan candidate) => bySpan[candidate].Any(n => n.FromMacro || rule.Kinds.Contains(n.Kind));

        if (Matches(span) || (rule.WithoutSemicolon && Matches(WithoutSemicolon(span, utf8, sortedTokenEnds))))
        {
            return null;
        }

        return $"no clang node of kind {string.Join("/", rule.Kinds)} has this span{Found(bySpan[span])}";
    }

    /// <summary>
    /// <paramref name="span"/> up to the end of the token before its final ';'.
    /// </summary>
    private static TextSpan WithoutSemicolon(TextSpan span, byte[] utf8, int[] sortedTokenEnds)
    {
        if (span.Length == 0 || utf8[span.End - 1] != ';')
        {
            return span;
        }

        var index = Array.BinarySearch(sortedTokenEnds, span.End - 1);
        index = index >= 0 ? index : ~index - 1;
        return index >= 0 && sortedTokenEnds[index] > span.Start ? new TextSpan(span.Start, sortedTokenEnds[index]) : span;
    }

    private static string CheckDeclaration(DeclarationRule rule, ICppNode node, ILookup<int, ClangNode> byName)
    {
        var name = rule.Name(node);
        if (name == null)
        {
            return null;
        }

        var candidates = byName[name.Span.Start].ToList();
        if (candidates.Any(n => n.FromMacro || (rule.Kinds.Contains(n.Kind) && n.Span.End == node.Span.End)))
        {
            return null;
        }

        return $"no clang declaration of kind {string.Join("/", rule.Kinds)} names '{name.Span}' and ends at {node.Span.End}"
            + (candidates.Count == 0 ? "" : $" (found {string.Join(", ", candidates.Select(c => $"{c.Kind} {c.Span}"))})");
    }

    private static string Found(IEnumerable<ClangNode> nodes)
    {
        var kinds = nodes.Select(n => n.Kind).Distinct().ToList();
        return kinds.Count == 0 ? "" : $" (found {string.Join(", ", kinds)})";
    }

    /// <summary>
    /// The sorted ends of the tokens of the code: not those of directives and inactive branches, which
    /// clang's raw tokens include.
    /// </summary>
    private static int[] CodeTokenEnds(IEnumerable<int> tokenEnds, TranslationUnit unit)
    {
        // Directives are in source order; an inactive branch ends at the next processed directive
        var nonCode = new List<TextSpan>();
        var directives = unit.Directives;
        for (var i = 0; i < directives.Count; i++)
        {
            var directive = directives[i];
            var startsInactiveBranch = directive.IsConditional && directive.Kind != PreprocessorDirectiveKind.Endif && !directive.IsBranchTaken;
            var end = startsInactiveBranch
                ? (i + 1 < directives.Count ? directives[i + 1].Span.Start : unit.Span.End)
                : directive.Span.End;
            nonCode.Add(new TextSpan(directive.Span.Start, end));
        }

        var result = new List<int>();
        var range = 0;
        foreach (var end in tokenEnds.Order())
        {
            while (range < nonCode.Count && nonCode[range].End < end)
            {
                range++;
            }

            if (range >= nonCode.Count || end <= nonCode[range].Start)
            {
                result.Add(end);
            }
        }

        return result.ToArray();
    }

    /// <summary>
    /// The spans of the macro uses in the source.
    /// </summary>
    private sealed class MacroUses
    {
        private readonly int[] _starts;
        private readonly int[] _maxEnds;
        private readonly Dictionary<int, int> _maxEndByStart = [];

        public MacroUses(IEnumerable<TextSpan> spans)
        {
            var sorted = spans.Distinct().OrderBy(s => s.Start).ToArray();
            _starts = sorted.Select(s => s.Start).ToArray();
            _maxEnds = new int[sorted.Length];
            for (var i = 0; i < sorted.Length; i++)
            {
                _maxEnds[i] = Math.Max(i > 0 ? _maxEnds[i - 1] : 0, sorted[i].End);
                _maxEndByStart[sorted[i].Start] = Math.Max(_maxEndByStart.GetValueOrDefault(sorted[i].Start), sorted[i].End);
            }
        }

        /// <summary>
        /// A macro use contains <paramref name="span"/> and is longer.
        /// </summary>
        public bool StrictlyContain(TextSpan span)
        {
            if (_maxEndByStart.TryGetValue(span.Start, out var end) && end > span.End)
            {
                return true;
            }

            // The last use that starts before the span
            var index = Array.BinarySearch(_starts, span.Start);
            index = index >= 0 ? index : ~index;
            while (index > 0 && _starts[index - 1] >= span.Start)
            {
                index--;
            }

            return index > 0 && _maxEnds[index - 1] >= span.End;
        }
    }

    public static IEnumerable<ICppNode> Children(ICppNode node)
    {
        foreach (var property in ChildProperties.GetOrAdd(node.GetType(), FindChildProperties))
        {
            switch (property.GetValue(node))
            {
                case ICppNode child:
                    yield return child;
                    break;
                case IEnumerable list and not string:
                    foreach (var item in list)
                    {
                        if (item is ICppNode child)
                        {
                            yield return child;
                        }
                    }

                    break;
            }
        }
    }

    private static PropertyInfo[] FindChildProperties(Type type)
    {
        // Leading directives come before the node; they are children of the translation unit
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.Name != nameof(CppNode.LeadingDirectives))
            .Where(p => typeof(ICppNode).IsAssignableFrom(p.PropertyType)
                || (p.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(p.PropertyType)
                    && p.PropertyType.GetGenericArguments().Any(t => typeof(ICppNode).IsAssignableFrom(t))))
            .ToArray();
    }

    private static string Describe(string problem, ICppNode node, ICppNode parent, byte[] utf8)
    {
        var span = node.Span;
        var text = span.Start >= 0 && span.End <= utf8.Length && span.Start <= span.End ? span.GetText(utf8) : "";
        text = text.ReplaceLineEndings(" ");
        if (text.Length > 60)
        {
            text = text[..57] + "...";
        }

        var line = 1;
        for (var i = 0; i < Math.Min(span.Start, utf8.Length); i++)
        {
            if (utf8[i] == '\n')
            {
                line++;
            }
        }

        return $"{node.GetType().Name} {span} at line {line} (in {parent.GetType().Name}): {problem}: '{text}'";
    }
}
