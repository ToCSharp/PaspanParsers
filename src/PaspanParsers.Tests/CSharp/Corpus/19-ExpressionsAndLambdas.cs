using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Corpus.ExpressionsAndLambdas
{
    // Stage 3: expressions, lambdas and queries
    public class Expressions
    {
        private int[] data = new int[10];
        private int field;

        public object Operators(int a, int b, bool c, string s, int? n, List<int> list, object o)
        {
            var arithmetic = 1 + 2 * 3 - 4 / 5 % 6;
            var shifts = a << 2 >> 1 >>> 3;
            var logical = a < b && b <= a || a > b ^ a >= b;
            var bits = a & b | a ^ ~b;
            var unary = - -a + + +b - --a + !!c;
            a++; a--; ++a; --a;
            a += 1; a -= 1; a *= 2; a /= 2; a %= 3; a &= 1; a |= 2; a ^= 3; a <<= 1; a >>= 1; a >>>= 1;
            s ??= "default";
            var coalesce = n ?? 0;
            var conditional = c ? a : b > 0 ? 1 : 2;
            var access = s?.Length;
            var element = list?[0];
            var forgiving = s!.Length;
            var casts = (int)3.5 + (int)-b;
            var subtraction = (a) - b;
            var asType = o as string;
            var isType = o is string;
            var nullableAs = o as int? ?? 0;
            var keywords = typeof(int).Name + sizeof(int) + default(int) + checked(a * b) + unchecked(a + b);
            int defaultLiteral = default;
            var name = nameof(field);
            var index = data[^1];
            var range = data[1..^1];
            var all = data[..];
            Range open = ..;
            var tuple = (1, "two");
            var named = (Id: 1, Name: "two");
            (int x, int y) = (1, 2);
            var (p, q) = (3, 4);
            (a, b) = (b, a);
            var chained = this.data[0].ToString().Length;
            var predefined = int.MaxValue + string.Empty.Length;
            var qualified = global::System.Math.PI;
            var interpolated = $"a = {a}, b = {b,5:F2}, c = {(c ? 1 : 2)}";
            var thrown = s ?? throw new ArgumentNullException(nameof(s));
            var parsed = int.TryParse(s, out var result) && int.TryParse(s, out _);
            base.ToString();
            return this;
        }

        public object Creation(int[] arr)
        {
            var obj = new object();
            var array = new int[] { 1, 2, 3, };
            var sized = new int[3, 4];
            var jagged = new int[2][];
            var implicitArray = new[] { 1, 2 };
            var tuples = new (int, string)[3];
            var anonymous = new { Name = "x", arr.Length, };
            var collection = new List<int> { 1, 2, 3 };
            var indexed = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
            var complex = new Dictionary<string, int> { { "a", 1 } };
            var members = new Expressions { field = 1, data = { [0] = 1 } };
            Expressions targetTyped = new();
            Expressions targetTypedWithInitializer = new() { field = 2 };
            int[] spread = [1, 2, .. arr];
            List<int> empty = [];
            Span<int> stack = stackalloc int[4];
            Span<int> stackInitialized = stackalloc[] { 1, 2 };
            return null;
        }

        public async Task Lambdas(int[] numbers, string[] words)
        {
            Func<int, int> square = x => x * x;
            Func<int, int, int> add = (x, y) => x + y;
            Func<int, int, int> typed = (int x, int y) => x + y;
            Action noArgs = () => { };
            Func<Task> asyncLambda = async () => await Task.Delay(1);
            Func<int, Task<int>> asyncSingle = async x => { await Task.Yield(); return x; };
            Func<int, int> staticLambda = static x => x + 1;
            var natural = (int x) => x.ToString();
            var withReturnType = int (string s) => s.Length;
            var withAttribute = [Obsolete] (int x) => x;
            var withDefault = (int x = 5) => x;
            Func<int, int, int> discards = (_, _) => 0;
            Action<int> anonymous = delegate (int x) { };
            Action anonymousNoParameters = delegate { };
            var cast = (Func<int>)(() => 1);
            await Task.Delay(1);

            var query = from n in numbers
                        where n > 0
                        let doubled = n * 2
                        orderby doubled descending, n ascending
                        select new { n, doubled };

            var joined = from n in numbers
                         join w in words on n equals w.Length into grouped
                         from g in grouped
                         select g;

            var groups = from w in words
                         group w by w.Length into byLength
                         where byLength.Count() > 1
                         select byLength.Key;

            var typedFrom = from int n in numbers select n;
        }
    }
}
