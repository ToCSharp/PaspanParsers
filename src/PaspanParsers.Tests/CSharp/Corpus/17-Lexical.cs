// Lexical features written with the grammar the parser already supports.
/* Block comment
   spanning lines */
namespace Corpus.Lexical
{
    /// <summary>Documentation comment.</summary>
    public class Literals
    {
        public object Decimal = 1_000_000;
        public object Hex = 0xFF_FF;
        public object HexPrefix = 0x_1;
        public object Binary = 0b1010_1010;
        public object Unsigned = 42u;
        public object Long = 42L;
        public object ULong = 42UL;
        public object ULong2 = 42lu;
        public object Big = 18446744073709551615;
        public object Real = 1.5;
        public object Fraction = .5;
        public object Exponent = 1.5e-3;
        public object ExponentPlus = 2E+10;
        public object Float = 1.5f;
        public object Double = 1d;
        public object Money = 19.99m;
        public object Char = 'a';
        public object Quote = '\'';
        public object Escape = '\n';
        public object Alert = '\a';
        public object Esc = '\e';
        public object HexChar = '\x41';
        public object UnicodeChar = '\u0041';
        public object Cyrillic = 'я';
        public object Regular = "tab\there \"quoted\" \\ \0 \a \b \f \v \r \U0001F600";
        public object Verbatim = @"C:\path\""quoted""
second line";
        public object Raw = """He said "hi".""";
        public object RawQuotes = """"Contains """ inside"""";
        public object RawMultiline = """
            {
              "name": "value"
            }
            """;
        public object Utf8 = "abc"u8;
        public object Utf8Verbatim = @"abc"u8;
        public object Empty = "";
        public object Interpolated = $"x = {x}, y = {y,5:F2} {{braces}}";
        public object InterpolatedVerbatim = $@"{name}\path ""q""";
        public object InterpolatedVerbatim2 = @$"{name}\path";
        public object InterpolatedRaw = $$"""{"value": {{x}}}""";
        public object InterpolatedMultiline = $"""
            Name: {name}
              Indented {x:X8}
            """;
        public object Nested = $"outer {$"inner {x}"} {"literal"}";
        public object Conditional = $"{(x > 0 ? "positive" : "negative")}";
        public object Yes = true;
        public object No = false;
        public object Nothing = null;
    }

    public class Identifiers
    {
        public int @class;
        public int @event;
        public int \u0061bc;
        public int имя;
        public int _underscore1;
        public int int1;
        public int var;
        public int get;
        public int set;
        public int value;
        public int where;
        public int select;
        public int from;
        public int async;
        public int await;
        public int record;
        public int field;
        public int nameof;

        public int Operators()
        {
            int a = 1 << 2 >> 1 >>> 1;
            bool b = a <= 3 && a >= 1 || a < 0 & a > 5 | a == 2 ^ a != 4;
            return a;
        }

        public List<List<Dictionary<string, int>>> Nested;
    }
}
