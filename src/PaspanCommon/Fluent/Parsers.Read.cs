namespace Paspan.Fluent;

public static partial class Parsers
{
    /// <summary>
    /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
    /// </summary>
    public static Parser<Unit> Read(int count) => new Read(count);
    public static Parser<Unit> Read<T>(this Parser<T> parser, int count) => new Read(count);

    public static Parser<T> Read<T>(this Parser<T> parser, Func<T, int> action) => new Read<T>(parser, action);

}
