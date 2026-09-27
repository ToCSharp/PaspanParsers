using System;
using System.Collections.Generic;
using System.Linq;

namespace Corpus.Expressions;

public class Expressions
{
    private int[] data = new int[10];
    private int field;

    public object All(object o, string s, int? n, List<int> list)
    {
        var a = 1 + 2 * 3 - 4 / 5 % 6;
        var b = a << 2 >> 1 >>> 3;
        var c = a < b && b <= a || a > b ^ a >= b;
        var d = a == b != c;
        var e = a & b | a ^ ~b;
        var f = !c;
        var g = -a + +b;
        a++; a--; ++a; --a;
        a += 1; a -= 1; a *= 2; a /= 2; a %= 3;
        a &= 1; a |= 2; a ^= 3; a <<= 1; a >>= 1; a >>>= 1;
        s ??= "default";
        var h = n ?? 0;
        var i = c ? a : b;
        var j = s?.Length;
        var k = list?[0];
        var l = s!.Length;
        var m = (int)3.5;
        var p = (object)s;
        var q = o as string;
        var r = o is string;
        var t = typeof(int);
        var u = sizeof(int);
        var v = default(int);
        int w = default;
        var x = nameof(field);
        var y = checked(a * b);
        var z = unchecked(a * b);
        var obj = new object();
        var arr = new int[] { 1, 2, 3 };
        var arr2 = new int[3, 4];
        var jag = new int[2][];
        var implicitArr = new[] { 1, 2 };
        var anon = new { Name = "x", a };
        var init = new List<int> { 1, 2, 3 };
        var dict = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        var dict2 = new Dictionary<string, int> { { "a", 1 } };
        var withInit = new Expressions { field = 1 };
        Expressions targetTyped = new();
        Expressions targetTyped2 = new() { field = 2 };
        int[] collection = [1, 2, .. arr];
        List<int> empty = [];
        var index = arr[^1];
        var range = arr[1..^1];
        var range2 = arr[..];
        Range fullRange = ..;
        var tuple = (1, "two");
        var named = (Id: 1, Name: "two");
        var chained = this.data[0].ToString().Length;
        var call = Math.Max(a, b);
        var generic = list.Select(item => item * 2).Where(item => item > 2).ToList();
        var interpolated = $"a = {a}, b = {b,5:F2}";
        var thrown = s ?? throw new ArgumentNullException(nameof(s));
        var sw = a switch { 0 => "zero", 1 or 2 => "small", _ => "many" };
        var awaited = (Func<int>)(() => 1);
        Span<int> stack = stackalloc int[4];
        var parenthesized = (a + b) * (c ? 1 : 2);
        var outCall = int.TryParse(s, out var parsed);
        var outDiscard = int.TryParse(s, out _);
        base.ToString();
        return this;
    }
}
