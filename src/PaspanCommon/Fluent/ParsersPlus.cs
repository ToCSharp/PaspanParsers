using static Paspan.Fluent.Parsers;

namespace Paspan.Fluent;

public static partial class ParsersPlus
{
    public static Parser<string> TextBeforeLineFeed(int maxLength = -1) => new TextBefore<Unit>(Literals.Char('\n'), maxLength, canBeEmpty: true, consumeDelimiter: true);
    public static Parser<string> TextBefore(char ch, int maxLength = -1, bool consumeDelimiter = true, bool canBeEmpty = true) => new TextBefore<Unit>(Literals.Char(ch), maxLength, consumeDelimiter: consumeDelimiter, canBeEmpty: canBeEmpty);
    public static Parser<string> TextBefore<T>(Parser<T> parser, int maxLength = -1, bool consumeDelimiter = true, bool canBeEmpty = true) => new TextBefore<T>(parser, maxLength, consumeDelimiter: consumeDelimiter, canBeEmpty: canBeEmpty);
    //public static TextBefore<string> TextBefore(string str, bool canBeEmpty)
    //    => new TextBefore<string>(Literals.Text(str), canBeEmpty, consumeDelimiter: true);
    public static TextBefore<string> TextBefore(string str, int maxLength = -1, bool canBeEmpty = true, bool consumeDelimiter = true, bool addDelimiterToResult = false)
        => new TextBefore<string>(Literals.Text(str), maxLength, canBeEmpty: canBeEmpty, consumeDelimiter: consumeDelimiter, addDelimiterToResult: addDelimiterToResult);
    public static Parser<string> TextBeforeSpace => new TextBefore<Unit>(Literals.Char(' '), consumeDelimiter: true);
    public static Parser<string> TextBeforeEof => new TextBeforeEof<string>();
    public static Parser<Unit> Ch(char ch) => Literals.Char(ch);
    public static Parser<byte[]> BytesBefore<T>(Parser<T> parser, int maxLength = -1, bool canBeEmpty = false, bool consumeDelimiter = true)
        => new BytesBefore<byte[], T, string>(parser, maxLength, canBeEmpty: canBeEmpty, consumeDelimiter: consumeDelimiter);
    public static Parser<byte[]> BytesBefore(string str, int maxLength = -1, bool canBeEmpty = false, bool consumeDelimiter = true)
        => new BytesBefore<byte[], string, string>(Literals.Text(str), maxLength, canBeEmpty: canBeEmpty, consumeDelimiter: consumeDelimiter);
    public static Parser<Region> RegionBefore<T>(Parser<T> parser, int maxLength = -1, bool canBeEmpty = false, bool consumeDelimiter = true)
        => new RegionBefore<T, string>(parser, maxLength, canBeEmpty: canBeEmpty, consumeDelimiter: consumeDelimiter);
    public static Parser<Region> RegionBefore(string str, int maxLength = -1, bool canBeEmpty = false, bool consumeDelimiter = true)
        => new RegionBefore<string, string>(Literals.Text(str), maxLength, canBeEmpty: canBeEmpty, consumeDelimiter: consumeDelimiter);
    public static Parser<Region> RegionBefore(char ch, int maxLength = -1, bool canBeEmpty = true, bool consumeDelimiter = true)
        => new RegionBefore<Unit>(Literals.Char(ch), maxLength, canBeEmpty: canBeEmpty, consumeDelimiter: consumeDelimiter);
    public static Parser<Region> RegionBeforeLineFeed(int maxLength = -1) => new RegionBefore<Unit>(Literals.Char('\n'), maxLength, canBeEmpty: true, consumeDelimiter: true);
    public static Parser<T> SkipAnd<T>(this char ch, Parser<T> parser) => Terms.Char(ch).SkipAnd(parser);
    public static Parser<T> And<T>(this char ch, Parser<T> parser) => Terms.Char(ch).SkipAnd(parser);
    public static Parser<Labelled<T>> And<T>(this char ch, string label, Parser<T> parser) => Terms.Char(ch).SkipAnd(parser.Labelled(label));
    public static Parser<T> And<T>(this string str, Parser<T> parser) => Literals.Text(str).SkipAnd(parser);
    public static Parser<string> ToLiteral(this string str) => Literals.Text(str);
    public static Parser<Unit> ToLiteral(this char ch) => Literals.Char(ch);
    public static Parser<string> Skip(this string str) => Literals.Text(str);
    public static Parser<Labelled<T>> And<T>(this string str, string label, Parser<T> parser)
        => Literals.Text(str).SkipAnd(parser.Labelled(label));

    public static Parser<string> Or(this string str, string str2) => Literals.Text(str).Or(Literals.Text(str2));
    public static Parser<Unit> Or(this char ch, char ch2) => Literals.Char(ch).Or(Literals.Char(ch2));

    public static Parser<string> AndString(this string str) => Literals.Text(str).SkipAnd(Terms.String());

    public static Dictionary<string, object> Append(this Dictionary<string, object> dic, string key, object value)
    {
        dic[key] = value;
        return dic;
    }

    static Dictionary<string, object> AsDictionary(object[] objects)
    {
        var i = 1;
        return objects
        .ToDictionary(k => k switch
        {
            ILabelled labelled => labelled.Label,
            _ => $"Item{i++}"
        },
        v => v switch
        {
            IHaveValueObject o => o.ValueObject,
            _ => v
        });
    }
    public static Dictionary<string, object> AsDictionary(this object s)
    => AsDictionary([s]);
    public static Dictionary<string, object> AsDictionary(this (object, object) s)
    => AsDictionary([s.Item1, s.Item2]);
    public static Dictionary<string, object> AsDictionary(this (object, object, object) s)
    => AsDictionary([s.Item1, s.Item2, s.Item3]);
    public static Dictionary<string, object> AsDictionary(this (object, object, object, object) s)
    => AsDictionary([s.Item1, s.Item2, s.Item3, s.Item4]);
    public static Dictionary<string, object> AsDictionary(this (object, object, object, object, object) s)
    => AsDictionary([s.Item1, s.Item2, s.Item3, s.Item4, s.Item5]);
    public static Dictionary<string, object> AsDictionary(this (object, object, object, object, object, object) s)
    => AsDictionary([s.Item1, s.Item2, s.Item3, s.Item4, s.Item5, s.Item6]);
    public static Dictionary<string, object> AsDictionary(this (object, object, object, object, object, object, object) s)
    => AsDictionary([s.Item1, s.Item2, s.Item3, s.Item4, s.Item5, s.Item6, s.Item7]);
    public static Dictionary<string, object> AsDictionary(this (object, object, object, object, object, object, object, object) s)
    => AsDictionary([s.Item1, s.Item2, s.Item3, s.Item4, s.Item5, s.Item6, s.Item7, s.Item8]);
    public static Dictionary<string, object> AsDictionary(this (object, object, object, object, object, object, object, object, object) s)
    => AsDictionary([s.Item1, s.Item2, s.Item3, s.Item4, s.Item5, s.Item6, s.Item7, s.Item8, s.Item9]);
    public static Dictionary<string, object> AsDictionary(this (object, object, object, object, object, object, object, object, object, object) s)
    => AsDictionary([s.Item1, s.Item2, s.Item3, s.Item4, s.Item5, s.Item6, s.Item7, s.Item8, s.Item9, s.Item10]);
    public static Dictionary<string, object> AsDictionary(this (object, object, object, object, object, object, object, object, object, object, object) s)
    => AsDictionary([s.Item1, s.Item2, s.Item3, s.Item4, s.Item5, s.Item6, s.Item7, s.Item8, s.Item9, s.Item10, s.Item11]);
    public static Dictionary<string, object> AsDictionary(this (object, object, object, object, object, object, object, object, object, object, object, object) s)
    => AsDictionary([s.Item1, s.Item2, s.Item3, s.Item4, s.Item5, s.Item6, s.Item7, s.Item8, s.Item9, s.Item10, s.Item11, s.Item12]);
    public static Dictionary<string, object> AsDictionary(this (object, object, object, object, object, object, object, object, object, object, object, object, object) s)
    => AsDictionary([s.Item1, s.Item2, s.Item3, s.Item4, s.Item5, s.Item6, s.Item7, s.Item8, s.Item9, s.Item10, s.Item11, s.Item12, s.Item13]);

    public static Parser<Labelled<T>> ZeroOrOne<T>(string label, Parser<T> parser)
        => Parsers.ZeroOrOne(parser.Labelled(label));

}
