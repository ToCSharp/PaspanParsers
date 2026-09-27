using System;
using System.Collections.Generic;

namespace Corpus.Types
{
    // Stage 2: names and types, written with declarations the parser already supports
    public unsafe class Types<T> where T : struct
    {
        private Dictionary<string, List<int>>.KeyCollection keys;
        private global::System.String aliased;
        private int?[] nullableElements;
        private string[]? nullableArray;
        private int[][,] jagged;
        private (int Id, string Name) tuple;
        private (int, (string, bool)) nested;
        private int* pointer;
        private void** pointerToPointer;
        private delegate*<int, void> managedFunction;
        private delegate* unmanaged[Cdecl]<ref int, int> nativeFunction;
        private nint native;
        private dynamic late;

        public List<List<Dictionary<string, int[]>>> Deep(A<B>.C<D> qualified, T? nullable) => null;

        public ref readonly int Ref(ref int value, in int input, out int output, params int[] rest)
        {
            output = 0;
            ref int local = ref value;
            ref readonly int readOnly = ref input;
            scoped Span<int> span = stackalloc int[1];
            return ref value;
        }

        public void Generics()
        {
            var type = typeof(Dictionary<,>);
            var nested = typeof(List<>.Enumerator);
            var name = nameof(List<>);
            var empty = Array.Empty<List<int>>();
            F(G<A, B>(7));
            F(a < b, c > d);
            F(a < b, c >= d);
            var shifts = a >> b >>> c;
            shifts >>= 1;
            shifts >>>= 2;
            var comparisons = a > b && a >= b;
        }

        private static void F(params object[] args) { }
        private static object G<T1, T2>(int x) => x;
        private static int a, b, c, d;
    }

    public class A<X>
    {
    }

    public class B
    {
    }

    public class D
    {
    }
}
