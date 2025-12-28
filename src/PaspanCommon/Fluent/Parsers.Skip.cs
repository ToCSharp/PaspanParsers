namespace Paspan.Fluent;

public static partial class Parsers
{
    /// <summary>
    /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
    /// </summary>
    public static Parser<T1> Skip<T1, T2>(this Parser<T1> parser, Parser<T2> and) => new SequenceAndSkip<T1, T2>(parser, and);
    public static Parser<T1> AndSkip<T1, T2>(this Parser<T1> parser, Parser<T2> and) => Skip(parser, and);

    /// <summary>
    /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
    /// </summary>
    public static Parser<(T1, T2)> Skip<T1, T2, T3>(this Parser<(T1, T2)> parser, Parser<T3> and) => new SequenceAndSkip<T1, T2, T3>(parser, and);
    public static Parser<(T1, T2)> AndSkip<T1, T2, T3>(this Parser<(T1, T2)> parser, Parser<T3> and) => Skip(parser, and);

    /// <summary>
    /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
    /// </summary>
    public static Parser<(T1, T2, T3)> Skip<T1, T2, T3, T4>(this Parser<(T1, T2, T3)> parser, Parser<T4> and) => new SequenceAndSkip<T1, T2, T3, T4>(parser, and);
    public static Parser<(T1, T2, T3)> AndSkip<T1, T2, T3, T4>(this Parser<(T1, T2, T3)> parser, Parser<T4> and) => Skip(parser, and);

    /// <summary>
    /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
    /// </summary>
    public static Parser<(T1, T2, T3, T4)> Skip<T1, T2, T3, T4, T5>(this Parser<(T1, T2, T3, T4)> parser, Parser<T5> and) => new SequenceAndSkip<T1, T2, T3, T4, T5>(parser, and);
    public static Parser<(T1, T2, T3, T4)> AndSkip<T1, T2, T3, T4, T5>(this Parser<(T1, T2, T3, T4)> parser, Parser<T5> and) => Skip(parser, and);

    /// <summary>
    /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
    /// </summary>
    public static Parser<(T1, T2, T3, T4, T5)> Skip<T1, T2, T3, T4, T5, T6>(this Parser<(T1, T2, T3, T4, T5)> parser, Parser<T6> and) => new SequenceAndSkip<T1, T2, T3, T4, T5, T6>(parser, and);
    public static Parser<(T1, T2, T3, T4, T5)> AndSkip<T1, T2, T3, T4, T5, T6>(this Parser<(T1, T2, T3, T4, T5)> parser, Parser<T6> and) => Skip(parser, and);

    /// <summary>
    /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
    /// </summary>
    public static Parser<(T1, T2, T3, T4, T5, T6)> Skip<T1, T2, T3, T4, T5, T6, T7>(this Parser<(T1, T2, T3, T4, T5, T6)> parser, Parser<T7> and) => new SequenceAndSkip<T1, T2, T3, T4, T5, T6, T7>(parser, and);
    public static Parser<(T1, T2, T3, T4, T5, T6)> AndSkip<T1, T2, T3, T4, T5, T6, T7>(this Parser<(T1, T2, T3, T4, T5, T6)> parser, Parser<T7> and) => Skip(parser, and);

    /// <summary>
    /// Builds a parser that ensure the specified parsers match consecutively. The last parser's result is then ignored.
    /// </summary>
    public static Parser<(T1, T2, T3, T4, T5, T6, T7)> Skip<T1, T2, T3, T4, T5, T6, T7, T8>(this Parser<(T1, T2, T3, T4, T5, T6, T7)> parser, Parser<T8> and) => new SequenceAndSkip<T1, T2, T3, T4, T5, T6, T7, T8>(parser, and);
    public static Parser<(T1, T2, T3, T4, T5, T6, T7)> AndSkip<T1, T2, T3, T4, T5, T6, T7, T8>(this Parser<(T1, T2, T3, T4, T5, T6, T7)> parser, Parser<T8> and) => Skip(parser, and);


    public static Parser<T1> Skip<T1>(this Parser<T1> parser, char ch) => new SequenceAndSkip<T1, Unit>(parser, Literals.Char(ch));
    public static Parser<T1> Skip<T1>(this Parser<T1> parser, string str) => new SequenceAndSkip<T1, string>(parser, Literals.Text(str));

    public static Parser<(T1, T2)> Skip<T1, T2>(this Parser<(T1, T2)> parser, char ch) => new SequenceAndSkip<T1, T2, Unit>(parser, Literals.Char(ch));
    public static Parser<(T1, T2)> Skip<T1, T2>(this Parser<(T1, T2)> parser, string str) => new SequenceAndSkip<T1, T2, string>(parser, Literals.Text(str));

    public static Parser<(T1, T2, T3)> Skip<T1, T2, T3>(this Parser<(T1, T2, T3)> parser, char ch) => new SequenceAndSkip<T1, T2, T3, Unit>(parser, Literals.Char(ch));
    public static Parser<(T1, T2, T3)> Skip<T1, T2, T3>(this Parser<(T1, T2, T3)> parser, string str) => new SequenceAndSkip<T1, T2, T3, string>(parser, Literals.Text(str));

    public static Parser<(T1, T2, T3, T4)> Skip<T1, T2, T3, T4>(this Parser<(T1, T2, T3, T4)> parser, char ch) => new SequenceAndSkip<T1, T2, T3, T4, Unit>(parser, Literals.Char(ch));
    public static Parser<(T1, T2, T3, T4)> Skip<T1, T2, T3, T4>(this Parser<(T1, T2, T3, T4)> parser, string str) => new SequenceAndSkip<T1, T2, T3, T4, string>(parser, Literals.Text(str));

    public static Parser<(T1, T2, T3, T4, T5)> Skip<T1, T2, T3, T4, T5>(this Parser<(T1, T2, T3, T4, T5)> parser, char ch) => new SequenceAndSkip<T1, T2, T3, T4, T5, Unit>(parser, Literals.Char(ch));
    public static Parser<(T1, T2, T3, T4, T5)> Skip<T1, T2, T3, T4, T5>(this Parser<(T1, T2, T3, T4, T5)> parser, string str) => new SequenceAndSkip<T1, T2, T3, T4, T5, string>(parser, Literals.Text(str));

    public static Parser<(T1, T2, T3, T4, T5, T6)> Skip<T1, T2, T3, T4, T5, T6>(this Parser<(T1, T2, T3, T4, T5, T6)> parser, char ch) => new SequenceAndSkip<T1, T2, T3, T4, T5, T6, Unit>(parser, Literals.Char(ch));
    public static Parser<(T1, T2, T3, T4, T5, T6)> Skip<T1, T2, T3, T4, T5, T6>(this Parser<(T1, T2, T3, T4, T5, T6)> parser, string str) => new SequenceAndSkip<T1, T2, T3, T4, T5, T6, string>(parser, Literals.Text(str));

    public static Parser<(T1, T2, T3, T4, T5, T6, T7)> Skip<T1, T2, T3, T4, T5, T6, T7>(this Parser<(T1, T2, T3, T4, T5, T6, T7)> parser, char ch) => new SequenceAndSkip<T1, T2, T3, T4, T5, T6, T7, Unit>(parser, Literals.Char(ch));
    public static Parser<(T1, T2, T3, T4, T5, T6, T7)> Skip<T1, T2, T3, T4, T5, T6, T7>(this Parser<(T1, T2, T3, T4, T5, T6, T7)> parser, string str) => new SequenceAndSkip<T1, T2, T3, T4, T5, T6, T7, string>(parser, Literals.Text(str));

}
