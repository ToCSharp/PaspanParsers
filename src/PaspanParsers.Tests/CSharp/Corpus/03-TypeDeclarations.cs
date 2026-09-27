namespace Corpus.Types;

public abstract partial class Shape : IComparable<Shape>, IDisposable
{
    public abstract double Area { get; }

    public int CompareTo(Shape other) => Area.CompareTo(other.Area);

    public void Dispose() { }

    protected internal class Nested
    {
        private protected struct Deeper { }
    }
}

public sealed class Circle(double radius) : Shape
{
    public override double Area => System.Math.PI * radius * radius;
}

public readonly struct Point(int x, int y)
{
    public int X { get; } = x;
    public int Y { get; } = y;
}

public ref struct Span2 { }

public readonly ref struct ReadOnlySpan2 { }

public interface IRepository<T> where T : class
{
    T Find(int id);
    void Save(T item) { }
    static abstract int Count { get; }
}

[System.Flags]
public enum Access : byte
{
    None = 0,
    Read = 1 << 0,
    Write = 1 << 1,
    All = Read | Write,
}

public delegate TResult Transformer<in T, out TResult>(T input);

public static class Extensions
{
    public static bool IsEmpty(this string value) => value.Length == 0;
}
