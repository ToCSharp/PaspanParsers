using System;
using System.Collections.Generic;

namespace Corpus.Generics;

public class Box<T> where T : struct
{
    public T Value;
}

public class Registry<TKey, TValue>
    where TKey : notnull, IComparable<TKey>
    where TValue : class, new()
{
    private readonly Dictionary<TKey, List<TValue>> items = new Dictionary<TKey, List<TValue>>();

    public TValue Create<TOther>(TOther other) where TOther : unmanaged => new TValue();

    public void Nested(List<List<Dictionary<string, int[]>>> deep) { }

    public Dictionary<string, List<int>>.KeyCollection Keys() => null;
}

public interface IVariant<in TIn, out TOut> { }

public class Constraints<T, U, V>
    where T : class?
    where U : Enum
    where V : Delegate, allows ref struct
{
    public void Method<W>() where W : default { }
}

public static class Generic
{
    public static void Calls()
    {
        var list = new List<int>();
        var type = typeof(Dictionary<,>);
        var name = nameof(List<int>);
        int result = Math.Max<int>(1, 2);
        bool comparison = 1 < 2;
        F(G<A, B>(7));
        F(a < b, c > d);
        var x = Array.Empty<string>();
    }

    static void F(params object[] args) { }
    static object G<T1, T2>(int x) => x;
    static int a, b, c, d;
    class A { }
    class B { }
}
