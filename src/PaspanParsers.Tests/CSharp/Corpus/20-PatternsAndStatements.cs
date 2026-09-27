using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Corpus.PatternsAndStatements
{
    // Stages 4 and 5: patterns and statements
    public class Patterns
    {
        public string All(object o, int n, int[] arr, Point p, byte b)
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
            if (o is Exception { InnerException.Message: "inner", }) return "extended";
            if (n is (> 0 and < 5) or 10) return "parenthesized";
            if (o is int or long) return "integer";
            if (o is not (string or int)) return "other";
            if (b is (byte)'-' or (byte)'+') return "sign";
            if (o is string?[] strings) return "strings";

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

    public class Point
    {
        public int X;
        public int Y;

        public void Deconstruct(out int x, out int y)
        {
            x = X;
            y = Y;
        }
    }

    public class Statements
    {
        public async Task<int> All(int[] items, object gate, IAsyncEnumerable<int> stream, Dictionary<int, int> pairs)
        {
            ;
            int a = 0, b = 1;
            const int limit = 10;
            int[] initialized = { 1, 2 };
            ref int first = ref items[0];
            ref readonly int second = ref items[1];

            if (a > b) a++; else if (a < b) b--; else { }
            if (a > 0) if (b > 0) a++; else b++;

            while (a < limit) { a++; if (a == 5) continue; if (a == 8) break; }
            do { b++; } while (b < limit);
            for (int i = 0, j = 10; i < j; i++, j--) { }
            for (;;) { break; }
            foreach (var item in items) { }
            foreach (var (k, v) in pairs) { }
            await foreach (var value in stream) { }

            switch (a)
            {
                case 0:
                case 1 when b > 0:
                    break;
                case int n and > 100:
                    goto default;
                case > 50:
                    goto case 0;
                default:
                    break;
            }

            switch (a, b)
            {
                case (0, 0):
                    break;
            }

            try
            {
                throw new InvalidOperationException();
            }
            catch (InvalidOperationException ex) when (ex.Message != null)
            {
            }
            catch (Exception)
            {
                throw;
            }
            catch
            {
            }
            finally
            {
            }

            using (var stream1 = new MemoryStream()) { }
            using (new MemoryStream()) { }
            using var stream2 = new MemoryStream();
            await using var stream3 = new MemoryStream();

            lock (gate) { }
            checked { a = a * 2; }
            unchecked { b = b * 2; }

        label:
            a++;
            if (a < 3) goto label;

            int Local(int value) => value * 2;
            static async Task<T> LocalGeneric<T>(T value) where T : struct { await Task.Yield(); return value; }

            return Local(a);
        }

        public IEnumerable<int> Iterator()
        {
            yield return 1;
            yield break;
        }

        public unsafe int Unsafe(int[] values)
        {
            int total = 0;
            fixed (int* p = values)
            {
                int* current = p;
                total += *current;
                current++;
            }

            unsafe { int* stack = stackalloc int[4]; stack[0] = 1; }
            return total;
        }
    }
}
