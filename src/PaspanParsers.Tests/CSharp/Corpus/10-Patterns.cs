using System;

namespace Corpus.Patterns;

public record Point(int X, int Y);

public class Patterns
{
    public string All(object o, int n, int[] arr, Point p)
    {
        if (o is null) return "null";
        if (o is not null && o is string s) return s;
        if (o is int i and > 0 and < 10) return "digit";
        if (o is var anything) { }
        if (p is { X: 0, Y: > 0 }) return "on axis";
        if (p is (0, 0)) return "origin";
        if (p is Point(var x, var y) { X: 1 } named) return "one";
        if (o is Point { X: 1 or 2 } or null) return "small";
        if (arr is [1, 2, .. var rest, 5]) return "list";
        if (arr is [_, _] or []) return "two or empty";
        if (arr is [.., > 0]) return "ends positive";
        if (o is Exception { InnerException.Message: "inner" }) return "extended";
        if (n is (> 0 and < 5) or 10) return "parenthesized";
        if (o is int or long) return "integer";
        if (o is not (string or int)) return "other";

        return n switch
        {
            < 0 => "negative",
            0 => "zero",
            > 0 and <= 10 when n % 2 == 0 => "small even",
            int v when v > 1000 => "large",
            _ => "other",
        };
    }

    public int Deconstruct(object o) => o switch
    {
        Point(var x, _) => x,
        { } => 1,
        null => 0,
    };
}
