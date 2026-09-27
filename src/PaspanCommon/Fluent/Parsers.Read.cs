namespace Paspan.Fluent;

public static partial class Parsers
{
    /// <summary>
    /// Builds a parser that reads the specified number of bytes.
    /// </summary>
    public static Parser<Unit> Read(int count) => new Read(count);

    /// <summary>
    /// Builds a parser that applies <paramref name="parser"/> and then reads the specified number of bytes.
    /// </summary>
    public static Parser<T> Read<T>(this Parser<T> parser, int count) => new Read<T>(parser, _ => count);

    public static Parser<T> Read<T>(this Parser<T> parser, Func<T, int> action) => new Read<T>(parser, action);

}
