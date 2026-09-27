using System.Text;

namespace Paspan.Fluent;

public abstract partial class Parser<T>
{
    public Parser<ulong> AsHex() => new HexValue<T>(this);
    public Parser<char> AsChar() => new CharValue<T>(this);
    //public Parser<(T, string)> AsString() => new StringValue<T>(this);
    //public Parser<(T1, string)> AsString() => new StringValue<T>(this);
}
public static partial class Parsers
{
    public static Parser<object> AsObject<T>(this Parser<T> parser) => new AsObject<T>(parser);
    
    /// <summary>
    /// Converts a Parser&lt;Region&gt; to Parser&lt;string&gt; by reading from the buffer
    /// </summary>
    public static Parser<string> AsString(this Parser<Region> parser) => new RegionToString(parser);
    
    /// <summary>
    /// Converts a Parser&lt;Unit&gt; to Parser&lt;string&gt; by reading the ValueSpan
    /// </summary>
    public static Parser<string> AsString(this Parser<Unit> parser) => new UnitToString(parser);

}
public sealed class HexValue<T>(Parser<T> parser) : Parser<ulong>
{
    private readonly Parser<T> _parser = parser ?? throw new ArgumentNullException(nameof(parser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<ulong> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        if (_parser.Parse(ref reader, context, ref parsed))
        {
            if (reader.TryGetHex(out var v))
            {
                result.Set(parsed.Start, parsed.End, v);
                return true;
            }
        }
        return false;
    }
}

public sealed class CharValue<T>(Parser<T> parser) : Parser<char>
{
    private readonly Parser<T> _parser = parser ?? throw new ArgumentNullException(nameof(parser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<char> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        if (_parser.Parse(ref reader, context, ref parsed))
        {
            if (reader.TryGetChar(out var v))
            {
                result.Set(parsed.Start, parsed.End, v);
                return true;
            }
        }
        return false;
    }
}
public sealed class StringValue : Parser<string>
{
    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<string> result)
    {
        context.EnterParser(this);

        if (reader.TryGetString(out var v))
        {
            result.Set(v);
            return true;
        }
        return false;
    }
}

public sealed class PooledStringValue : Parser<string>
{
    //public static DictionaryBytes<string> StringPool = new();
    public static Dictionary<byte[], string> StringPool = new(ByteArrayComparer.Instance);
    private static readonly Lock _lock = new();

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<string> result)
    {
        context.EnterParser(this);

        result.Set(GetString(reader.GetValue()));
        return true;
    }

    public static string GetString(ReadOnlySpan<byte> bytes)
    {
        lock (_lock)
        {
            // Look up by span to avoid allocating a key when the string is already pooled
            if (StringPool.TryGetAlternateLookup<ReadOnlySpan<byte>>(out var lookup))
            {
                if (lookup.TryGetValue(bytes, out var pooled))
                {
                    return pooled;
                }
            }
            else if (StringPool.TryGetValue(bytes.ToArray(), out var pooled))
            {
                return pooled;
            }

            var value = Encoding.UTF8.GetString(bytes);
            StringPool.Add(bytes.ToArray(), value);
            return value;
        }
    }
}

/// <summary>
/// Compares byte arrays by content. Without it a <see cref="Dictionary{TKey, TValue}"/> keyed by byte[] compares references.
/// </summary>
internal sealed class ByteArrayComparer : IEqualityComparer<byte[]>, IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]>
{
    public static readonly ByteArrayComparer Instance = new();

    public bool Equals(byte[] x, byte[] y) => ReferenceEquals(x, y) || (x != null && y != null && x.AsSpan().SequenceEqual(y));

    public int GetHashCode(byte[] obj) => GetHashCode(obj.AsSpan());

    public bool Equals(ReadOnlySpan<byte> alternate, byte[] other) => other != null && alternate.SequenceEqual(other);

    public int GetHashCode(ReadOnlySpan<byte> alternate)
    {
        var hash = new HashCode();
        hash.AddBytes(alternate);
        return hash.ToHashCode();
    }

    public byte[] Create(ReadOnlySpan<byte> alternate) => alternate.ToArray();
}

public sealed class AsObject<T>(Parser<T> parser) : Parser<object>
{
    private readonly Parser<T> _parser = parser ?? throw new ArgumentNullException(nameof(parser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<object> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();
        if (_parser.Parse(ref reader, context, ref parsed))
        {
            result.Set(parsed.Start, parsed.End, parsed.Value);
            return true;
        }
        return false;
    }
}

public sealed class RegionToString(Parser<Region> parser) : Parser<string>
{
    private readonly Parser<Region> _parser = parser ?? throw new ArgumentNullException(nameof(parser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<string> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<Region>();
        if (_parser.Parse(ref reader, context, ref parsed))
        {
            var region = parsed.Value;
            // Регион хранит абсолютную позицию; позиция до разбора может включать пропущенные пробелы (Terms)
            if (reader.TryGetString(region.Start, region.Length, out var str))
            {
                result.Set(parsed.Start, parsed.End, str);
                return true;
            }
        }
        return false;
    }
}

public sealed class UnitToString(Parser<Unit> parser) : Parser<string>
{
    private readonly Parser<Unit> _parser = parser ?? throw new ArgumentNullException(nameof(parser));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<string> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<Unit>();
        if (_parser.Parse(ref reader, context, ref parsed))
        {
            if (reader.TryGetString(out var str))
            {
                result.Set(parsed.Start, parsed.End, str);
                return true;
            }
        }
        return false;
    }
}

//public sealed class StringValue<T> : Parser<(T, string)>
//{
//    private readonly Parser<T> _parser;
//    public StringValue(Parser<T> parser)
//    {
//        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
//    }
//    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<(T, string)> result)
//    {
//        context.EnterParser(this);

//        var parsed = new ParseResult<T>();

//        if (_parser.Parse(ref reader, context, ref parsed))
//        {
//            if (reader.TryGetString(out var v))
//            {
//                result.Set((parsed.Value, v));
//                return true;
//            }
//        }
//        return false;
//    }
//}

