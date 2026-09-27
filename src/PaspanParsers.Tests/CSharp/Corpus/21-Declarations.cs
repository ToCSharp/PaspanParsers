extern alias Legacy;
global using unsafe Pointer = int*;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Numbers = System.Collections.Generic.List<(int Value, string Name)>;
using static System.Collections.Generic.EqualityComparer<int>;

[assembly: CLSCompliant(false)]

namespace Corpus.Declarations
{
    using System.Text;

    public interface IShape
    {
        double Area { get; }

        string Describe() => $"Area {Area}";

        static abstract IShape Create();

        static virtual int Sides => 0;

        event EventHandler Changed;

        int this[int index] { get; set; }

        public static IShape operator +(IShape a, IShape b) => a;

        abstract static bool operator ==(IShape a, IShape b);

        abstract static bool operator !=(IShape a, IShape b);
    }

    public abstract class Base<T> : IEnumerable<T>, IDisposable
        where T : class, IComparable<T>, new()
    {
        protected Base() { }

        protected Base(T first) : this() => First = first;

        public T First { get; protected set; }

        public abstract IEnumerator<T> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        void IDisposable.Dispose() { }

        public virtual ref readonly T Peek() => throw null;

        ~Base() { }
    }

    public sealed class Derived : Base<string>, IEquatable<Derived>
    {
        private static readonly Derived s_default = new();
        private readonly List<string> _items = [];
        private event EventHandler _changed;

        public Derived() : base("first") { }

        public new string First => base.First;

        public override IEnumerator<string> GetEnumerator() => _items.GetEnumerator();

        public event EventHandler Changed
        {
            add => _changed += value;
            remove { _changed -= value; }
        }

        public string this[int index]
        {
            get { return _items[index]; }
            set => _items[index] = value;
        }

        [return: System.Diagnostics.CodeAnalysis.NotNull]
        public string Get([param: System.Diagnostics.CodeAnalysis.NotNull] string key) => key;

        [field: NonSerialized]
        public int Counter { get; set; }

        public int Limited { get; private init; } = 10;

        bool IEquatable<Derived>.Equals(Derived other) => ReferenceEquals(this, other);

        public static Derived operator +(Derived a, Derived b) => a;

        public static Derived operator checked +(Derived a, Derived b) => a;

        public static Derived operator ++(Derived a) => a;

        public static bool operator true(Derived a) => true;

        public static bool operator false(Derived a) => false;

        public static Derived operator <<(Derived a, int shift) => a;

        public static Derived operator >>(Derived a, int shift) => a;

        public static Derived operator >>>(Derived a, int shift) => a;

        public static implicit operator string(Derived d) => d.First;

        public static explicit operator checked int(Derived d) => d._items.Count;

        public static explicit operator Derived(int count) => new();

        public void operator +=(Derived other) { }

        public void operator >>>=(int shift) { }

        public void operator ++() { }

        protected internal class Nested<[Serializable] TItem>
            where TItem : struct
        {
            private protected enum Kind : byte { A = 1, B = A << 1, C = A | B, }

            internal delegate TResult Map<in TIn, out TResult>(TIn value);

            public record struct Entry(TItem Item, Kind Kind);
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct Native
    {
        [FieldOffset(0)]
        public fixed byte Buffer[16];

        [FieldOffset(16)]
        public delegate* unmanaged[Cdecl]<int, void> Callback;

        public readonly int Length => 16;

        public readonly override string ToString() => "";
    }

    public ref struct RefHolder
    {
        public ref int Value;
        public ref readonly int ReadOnlyValue;

        public RefHolder(ref int value)
        {
            Value = ref value;
            ReadOnlyValue = ref value;
        }

        public void Update(scoped ref int other, params ReadOnlySpan<int> rest) { }
    }

    public readonly partial record struct Money(decimal Amount, string Currency)
    {
        public static Money Zero { get; } = new(0, "");
    }

    public abstract record Animal(string Name)
    {
        public abstract string Sound { get; }
    }

    public sealed record Dog(string Name) : Animal(Name)
    {
        public override string Sound => "Woof";
    }

    public record class Empty;

    public class Point(int x, int y) : IEquatable<Point>
    {
        public int X => x;
        public int Y => y;

        public bool Equals(Point other) => other is not null && other.X == X;
    }

    public struct Unit;

    public interface IMarker;

    public static partial class Extensions
    {
        public static bool IsEmpty<T>(this ICollection<T> collection) => collection.Count == 0;

        extension(string text)
        {
            public bool IsBlank => string.IsNullOrWhiteSpace(text);

            public static string Empty => "";
        }

        extension<T>(IEnumerable<T>)
        {
            public static IEnumerable<T> None => [];
        }

        static partial void Log(string message);
    }

    file sealed class FileLocal
    {
        partial class NotPartial { }
    }
};

namespace Corpus.Declarations.Other
{
    delegate void Handler(object sender, EventArgs e);

    enum Empty { }

    class Contextual
    {
        record R;
        async A;
        partial P;
        var v;
    }

    class async { }
    class partial { }
    class required { }
    class var { }
}
