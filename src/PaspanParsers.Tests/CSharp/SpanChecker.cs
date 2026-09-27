using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using PaspanParsers.CSharp;

namespace PaspanParsers.Tests.CSharp;

/// <summary>
/// Checks the spans of our AST against Roslyn: every node lies inside its parent (except the
/// <c>#nullable</c> directives, which are trivia before it), and its span is the
/// span of a Roslyn node or token (converted to UTF-8 offsets), apart from the few places where our
/// AST has nodes that Roslyn does not (see <see cref="HasNoRoslynCounterpart"/>).
/// </summary>
public static class SpanChecker
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ChildProperties = new();

    /// <summary>
    /// Returns null when the spans are right, otherwise a description of the first wrong span.
    /// </summary>
    public static string Check(string source, CompilationUnit unit, SyntaxNode roslynRoot)
    {
        var bom = source.Length > 0 && source[0] == '﻿' ? 1 : 0;
        var utf8 = CSharpParser.GetUtf8Source(source);

        // UTF-8 offset of each UTF-16 offset of the source
        var offsets = new int[source.Length + 1];
        var offset = 0;
        for (var i = bom; i < source.Length; i++)
        {
            offsets[i] = offset;
            if (char.IsHighSurrogate(source[i]) && i + 1 < source.Length && char.IsLowSurrogate(source[i + 1]))
            {
                // A surrogate pair is one 4-byte UTF-8 sequence; no span starts or ends inside it
                offset += 4;
                offsets[++i] = offset;
                continue;
            }

            // A lone surrogate is encoded as U+FFFD, like CSharpParser.GetUtf8Source does
            offset += Encoding.UTF8.GetByteCount(source.AsSpan(i, 1));
        }

        offsets[source.Length] = offset;

        var roslynSpans = new HashSet<TextSpan>();
        foreach (var nodeOrToken in roslynRoot.DescendantNodesAndTokensAndSelf(descendIntoTrivia: true))
        {
            roslynSpans.Add(new TextSpan(offsets[nodeOrToken.Span.Start], offsets[nodeOrToken.Span.End]));
        }

        var stack = new Stack<(ICSharpNode Node, ICSharpNode Parent)>();
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

            // #nullable directives are in the trivia before the tokens of the node that holds them
            if (parent is not CompilationUnit && node is not NullableDirective && !parent.Span.Contains(span))
            {
                return Describe($"outside its parent {parent.GetType().Name} {parent.Span}", node, parent, utf8);
            }

            if (!roslynSpans.Contains(span) && !HasNoRoslynCounterpart(node, parent))
            {
                return Describe("no Roslyn node or token has this span", node, parent, utf8);
            }

            foreach (var child in Children(node))
            {
                stack.Push((child, node));
            }
        }

        return null;
    }

    /// <summary>
    /// Nodes of our AST that Roslyn does not have.
    /// </summary>
    private static bool HasNoRoslynCounterpart(ICSharpNode node, ICSharpNode parent) => node switch
    {
        // The span covers all dotted parts; Roslyn nests them with the alias and the type arguments
        NameExpression when parent is NamedTypeReference named => named.Name.Span != named.Span,

        // int[][] is one array type in Roslyn
        ArrayTypeReference when parent is ArrayTypeReference => true,

        // In a?.b.c Roslyn binds .b.c to the conditional access, we build (a?.b).c
        Expression expression => IsInConditionalAccess(expression),

        _ => false,
    };

    private static bool IsInConditionalAccess(Expression expression)
    {
        while (true)
        {
            switch (expression)
            {
                case MemberAccessExpression { IsConditional: true }:
                case ElementAccessExpression { IsConditional: true }:
                    return true;
                case MemberAccessExpression memberAccess:
                    expression = memberAccess.Target;
                    break;
                case ElementAccessExpression elementAccess:
                    expression = elementAccess.Target;
                    break;
                case InvocationExpression invocation:
                    expression = invocation.Expression;
                    break;
                case UnaryExpression { IsPrefix: false } postfix:
                    expression = postfix.Operand;
                    break;
                default:
                    return false;
            }
        }
    }

    public static IEnumerable<ICSharpNode> Children(ICSharpNode node)
    {
        foreach (var property in ChildProperties.GetOrAdd(node.GetType(), FindChildProperties))
        {
            switch (property.GetValue(node))
            {
                case ICSharpNode child:
                    yield return child;
                    break;
                case IEnumerable list and not string:
                    foreach (var item in list)
                    {
                        if (item is ICSharpNode child)
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
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Where(p => typeof(ICSharpNode).IsAssignableFrom(p.PropertyType)
                || (p.PropertyType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(p.PropertyType)
                    && p.PropertyType.GetGenericArguments().Any(t => typeof(ICSharpNode).IsAssignableFrom(t))))
            .ToArray();
    }

    private static string Describe(string problem, ICSharpNode node, ICSharpNode parent, byte[] utf8)
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

        return $"span of {node.GetType().Name} {span} at line {line} (in {parent.GetType().Name}): {problem}: '{text}'";
    }
}
