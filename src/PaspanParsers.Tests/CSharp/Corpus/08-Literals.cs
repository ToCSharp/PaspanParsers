namespace Corpus.Literals;

public static class Literals
{
    public const int Decimal = 1_000_000;
    public const int Hex = 0xFF_FF;
    public const int Binary = 0b1010_1010;
    public const uint Unsigned = 42u;
    public const long Long = 42L;
    public const ulong ULong = 42UL;
    public const ulong ULong2 = 42lu;
    public const float Float = 1.5f;
    public const double Double = 1.5e-3;
    public const double Double2 = .5;
    public const double Double3 = 1d;
    public const decimal Money = 19.99m;
    public const char Letter = 'a';
    public const char Quote = '\'';
    public const char Escape = '\n';
    public const char Unicode = '\u0041';
    public const char Hex4 = '\x41';
    public const string Regular = "tab\there \"quoted\" \\ \0 \a \b \f \v \r \U0001F600";
    public const string Verbatim = @"C:\path\""quoted""
second line";
    public const string Raw = """He said "hi".""";
    public const string RawMultiline = """
        {
          "name": "value"
        }
        """;
    public static readonly byte[] Utf8 = "abc"u8.ToArray();
    public const string Empty = "";
    public const bool Yes = true, No = false;
    public const object Nothing = null;

    public static string Interpolations(int x, string name)
    {
        var a = $"{x}";
        var b = $@"{name}\path";
        var c = @$"{name}\path";
        var d = $"{{literal}} {x:X8} {x,-10} {(x > 0 ? "pos" : "neg")}";
        var e = $$"""{"value": {{x}}}""";
        var f = $"""
            Name: {name}
            """;
        var g = $"nested {$"inner {x}"}";
        return a + b + c + d + e + f + g;
    }
}
