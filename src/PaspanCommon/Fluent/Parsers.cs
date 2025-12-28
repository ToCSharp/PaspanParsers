namespace Paspan.Fluent;

using System.Numerics;
using static ParsersPlus;

public static partial class Parsers
{
    /// <summary>
    /// Provides parsers for literals. Literals do not skip spaces before being parsed and can be combined to
    /// parse composite terms.
    /// </summary>
    public static LiteralBuilder Literals => new();

    /// <summary>
    /// Provides parsers for terms. Terms skip spaces before being parsed.
    /// </summary>
    public static TermBuilder Terms => new();

    /// <summary>
    /// Builds a parser that looks for zero or many times a parser separated by another one.
    /// </summary>
    public static Parser<List<T>> Separated<U, T>(Parser<U> separator, Parser<T> parser) => new Separated<U, T>(separator, parser);

    /// <summary>
    /// Builds a parser that skips white spaces before another one.
    /// </summary>
    public static Parser<T> SkipWhiteSpace<T>(Parser<T> parser) => new SkipWhiteSpace<T>(parser);

    /// <summary>
    /// Builds a parser that looks for zero or one time the specified parser.
    /// </summary>
    public static Parser<T> ZeroOrOne<T>(Parser<T> parser, T defaultValue) => new ZeroOrOne<T>(parser, defaultValue);

    /// <summary>
    /// Builds a parser that looks for zero or one time the specified parser.
    /// </summary>
    public static Parser<T> ZeroOrOne<T>(Parser<T> parser) where T : notnull => new ZeroOrOne<T>(parser, default!);

    /// <summary>
    /// Builds a parser that looks for zero or many times the specified parser.
    /// </summary>
    public static Parser<List<T>> ZeroOrMany<T>(Parser<T> parser) => new ZeroOrMany<T>(parser);

    /// <summary>
    /// Builds a parser that looks for one or many times the specified parser.
    /// </summary>
    public static Parser<List<T>> OneOrMany<T>(Parser<T> parser) => new OneOrMany<T>(parser);

    public static Parser<Option<T>> Optional<T>(this Parser<T> parser) => new Optional<T>(parser);

    /// <summary>
    /// Builds a parser that succeeds when the specified parser fails to match.
    /// </summary>
    public static Parser<T> Not<T>(Parser<T> parser) => new Not<T>(parser);

    /// <summary>
    /// Builds a parser that invoked the next one if a condition is true.
    /// </summary>
    [Obsolete("Use the Select parser instead.")]
    public static Parser<T> If<C, S, T>(Func<C, S, bool> predicate, S state, Parser<T> parser) where C : ParseContext => new If<C, S, T>(parser, predicate, state);

    /// <summary>
    /// Builds a parser that invoked the next one if a condition is true.
    /// </summary>
    [Obsolete("Use the Select parser instead.")]
    public static Parser<T> If<S, T>(Func<ParseContext, S, bool> predicate, S state, Parser<T> parser) => new If<ParseContext, S, T>(parser, predicate, state);

    /// <summary>
    /// Builds a parser that invoked the next one if a condition is true.
    /// </summary>
    [Obsolete("Use the Select parser instead.")]
    public static Parser<T> If<C, T>(Func<C, bool> predicate, Parser<T> parser) where C : ParseContext => new If<C, object, T>(parser, (c, s) => predicate(c), null);

    /// <summary>
    /// Builds a parser that invoked the next one if a condition is true.
    /// </summary>
    [Obsolete("Use the Select parser instead.")]
    public static Parser<T> If<T>(Func<ParseContext, bool> predicate, Parser<T> parser) => new If<ParseContext, object, T>(parser, (c, s) => predicate(c), null);

    /// <summary>
    /// Builds a parser that selects another parser using custom logic.
    /// </summary>
    public static Parser<T> Select<C, T>(Func<C, Parser<T>> selector) where C : ParseContext => new Select<C, T>(selector);

    /// <summary>
    /// Builds a parser that selects another parser using custom logic.
    /// </summary>
    public static Parser<T> Select<T>(Func<ParseContext, Parser<T>> selector) => new Select<ParseContext, T>(selector);

    /// <summary>
    /// Builds a parser that can be defined later on. Use it when a parser need to be declared before its rule can be set.
    /// </summary>
    public static Deferred<T> Deferred<T>() => new();

    /// <summary>
    /// Builds a parser than needs a reference to itself to be declared.
    /// </summary>
    public static Deferred<T> Recursive<T>(Func<Deferred<T>, Parser<T>> parser) => new(parser);

    /// <summary>
    /// Builds a parser that matches the specified parser between two other ones.
    /// </summary>
    public static Parser<T> Between<A, T, B>(Parser<A> before, Parser<T> parser, Parser<B> after) => new Between<A, T, B>(before, parser, after);

    /// <summary>
    /// Builds a parser that matches any chars before a specific parser.
    /// </summary>
    public static Parser<string> AnyCharBefore<T>(Parser<T> parser, Parser<T> stopOn = null, bool canBeEmpty = false, bool failOnEof = false, bool consumeDelimiter = false)
        => new TextBefore<T>(parser, stopOn, canBeEmpty, failOnEof, consumeDelimiter);

    /// <summary>
    /// Builds a parser that captures the output of another parser.
    /// </summary>
    public static Parser<Region> Capture<T>(Parser<T> parser) => new Capture<T>(parser);

    /// <summary>
    /// Builds a parser that always succeeds.
    /// </summary>
    public static Parser<object> Always() => Always<object>();

    /// <summary>
    /// Builds a parser that always succeeds.
    /// </summary>
    public static Parser<T> Always<T>() => new Always<T>(default);

    /// <summary>
    /// Builds a parser that always succeeds.
    /// </summary>
    public static Parser<T> Always<T>(T value) => new Always<T>(value);

    /// <summary>
    /// Builds a parser that always succeeds.
    /// </summary>
    public static Parser<Unit> Empty<T>() => new Empty<Unit>(Unit.Value);

    /// <summary>
    /// Builds a parser that always succeeds.
    /// </summary>
    public static Parser<Unit> Empty() => new Empty<Unit>(Unit.Value);

    /// <summary>
    /// Builds a parser that always succeeds.
    /// </summary>
    public static Parser<T> Empty<T>(T value) => new Empty<T>(value);

    /// <summary>
    /// Builds a parser that always fails.
    /// </summary>
    public static Parser<T> Fail<T>() => new Fail<T>();

    /// <summary>
    /// Builds a parser that always fails.
    /// </summary>
    public static Parser<object> Fail() => new Fail<object>();
}

public class LiteralBuilder
{
    /// <summary>
    /// Builds a parser that matches whitespaces. This doesn't use the <see cref="ParseContext.WhiteSpaceParser"/>.
    /// </summary>
    public Parser<Region> WhiteSpace(bool includeNewLines = false) => new WhiteSpaceLiteral(includeNewLines);

    /// <summary>
    /// Builds a parser that matches anything until whitespaces. This doesn't use the <see cref="ParseContext.WhiteSpaceParser"/>.
    /// </summary>
    public Parser<Unit> NonWhiteSpace(bool includeNewLines = true) => new NonWhiteSpaceLiteral(includeNewLines: includeNewLines);

    /// <summary>
    /// Builds a parser that matches the specified text.
    /// </summary>
    public Parser<string> Text(string text, bool caseInsensitive = false) => new TextLiteral(text, caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    /// Builds a parser that matches a keyword by ensuring the following character is not a letter or digit.
    /// </summary>
    public Parser<string> Keyword(string text, bool caseInsensitive = false) => new Keyword(text, caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    /// Builds a parser that matches the specified char.
    /// </summary>
    public Parser<Unit> Char(char c) => new CharLiteral(c);

    /// <summary>
    /// Builds a parser that matches a number and returns any numeric type.
    /// </summary>
    public Parser<T> Number<T>(NumberOptions numberOptions = NumberOptions.Number, byte decimalSeparator = (byte)'.', byte groupSeparator = (byte)',')
    where T : INumber<T>
    => NumberLiterals.CreateNumberLiteralParser<T>(numberOptions, decimalSeparator, groupSeparator);
    public Parser<T> Number<T>(NumberOptions numberOptions, char decimalSeparator)
    where T : INumber<T>
    => Number<T>(numberOptions, (byte)decimalSeparator);
    public Parser<T> Number<T>(NumberOptions numberOptions, char decimalSeparator, char groupSeparator)
    where T : INumber<T>
    => Number<T>(numberOptions, (byte)decimalSeparator, (byte)groupSeparator);

    /// <summary>
    /// Builds a parser that matches an integer with an option leading sign.
    /// </summary>
    public Parser<long> Integer(NumberOptions numberOptions = NumberOptions.Integer) => Number<long>(numberOptions);

    /// <summary>
    /// Builds a parser that matches a floating point number represented as a <lang cref="decimal"/> value.
    /// </summary>
    public Parser<decimal> Decimal(NumberOptions numberOptions = NumberOptions.Float) => Number<decimal>(numberOptions);

    /// <summary>
    /// Builds a parser that matches a floating point number represented as a <lang cref="float"/> value.
    /// </summary>
    [Obsolete("Prefer Number<float>(NumberOptions.Float) instead.")]
    public Parser<float> Float(NumberOptions numberOptions = NumberOptions.Float) => Number<float>(numberOptions);

    /// <summary>
    /// Builds a parser that matches a floating point number represented as a <lang cref="double"/> value.
    /// </summary>
    [Obsolete("Prefer Number<double>(NumberOptions.Float) instead.")]
    public Parser<double> Double(NumberOptions numberOptions = NumberOptions.Float) => Number<double>(numberOptions);

    /// <summary>
    /// Builds a parser that matches an quoted string that can be escaped.
    /// </summary>
    public Parser<string> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => new StringLiteral(quotes);
    public Parser<Region> StringRegion(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => new StringRegion(quotes);

    /// <summary>
    /// Builds a parser that matches an identifier.
    /// </summary>
    public Parser<string> Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null) => new Identifier(extraStart, extraPart);

    /// <summary>
    /// Builds a parser that matches a char against a predicate.
    /// </summary>
    /// <param name="predicate">The predicate to match against each char.</param>
    /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<Unit> Pattern(Func<byte, bool> predicate, int minSize = 1, int maxSize = 0)
        => new PatternLiteral(predicate, minSize, maxSize);
    /// <summary>
    /// Builds a parser that matches a list of chars.
    /// </summary>
    /// <param name="values">The set of chars to match.</param>
    /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<Region> AnyOf(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0) => new ListOfChars(values, minSize, maxSize);

    /// <summary>
    /// Builds a parser that matches anything but a list of chars.
    /// </summary>
    /// <param name="values">The set of chars not to match.</param>
    /// <param name="minSize">The minimum number of required chars. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of chars it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<Region> NoneOf(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0) => new ListOfChars(values, minSize, maxSize, negate: true);

    /// <summary>
    /// Builds a parser that matches single line comments.
    /// </summary>
    /// <param name="singleLineStart">The text that starts the single line comment, e.g., <code>"//"</code>, <code>"--"</code>, <code>"#"</code></param>
    /// <returns></returns>
    //public Parser<Region> Comments(string singleLineStart) => Capture(Text(singleLineStart).And(AnyCharBefore(Text("\r\n").Or(Text("\n")), canBeEmpty: true, failOnEof: false, consumeDelimiter: false)));
    public Parser<Region> Comments(string singleLineStart) => Parsers.Capture(Text(singleLineStart).And(BytesBefore(Text("\r\n").Or(Text("\n")))));

    /// <summary>
    /// Builds a parser that matches multi line comments.
    /// </summary>
    /// <param name="multiLineStart">The text that starts the multi line comment, e.g., <code>"/*"</code></param>
    /// <param name="multiLineEnd">The text that ends the multi line comment, e.g., <code>"*/"</code></param>
    /// <returns></returns>
    public Parser<Region> Comments(string multiLineStart, string multiLineEnd) => Parsers.Capture(Text(multiLineStart).And(BytesBefore(Text(multiLineEnd), canBeEmpty: true, /*failOnEof: true,*/ consumeDelimiter: true).ElseError($"End-of-file found, '{multiLineEnd}' expected")));
}

public class TermBuilder
{
    /// <summary>
    /// Builds a parser that matches whitespaces as defined in <see cref="ParseContext.WhiteSpaceParser"/>.
    /// </summary>
    public Parser<Region> WhiteSpace() => new WhiteSpaceParser();

    /// <summary>
    /// Builds a parser that matches anything until whitespaces. This doesn't use the <see cref="ParseContext.WhiteSpaceParser"/>.
    /// </summary>
    public Parser<Unit> NonWhiteSpace(bool includeNewLines = true) => Parsers.SkipWhiteSpace(new NonWhiteSpaceLiteral(includeNewLines: includeNewLines));

    /// <summary>
    /// Builds a parser that matches the specified text.
    /// </summary>
    public Parser<string> Text(string text, bool caseInsensitive = false) => Parsers.SkipWhiteSpace(new TextLiteral(text, caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));

    /// <summary>
    /// Builds a parser that matches a keyword by ensuring the following character is not a letter or digit.
    /// </summary>
    public Parser<string> Keyword(string text, bool caseInsensitive = false) => Parsers.SkipWhiteSpace(new Keyword(text, caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));

    /// <summary>
    /// Builds a parser that matches the specified char.
    /// </summary>
    public Parser<Unit> Char(char c) => Parsers.SkipWhiteSpace(new CharLiteral(c));

    /// <summary>
    /// Builds a parser that matches a number and returns any numeric type.
    /// </summary>
    public Parser<T> Number<T>(NumberOptions numberOptions = NumberOptions.Number, byte decimalSeparator = (byte)'.', byte groupSeparator = (byte)',')
        where T : INumber<T>
        => Parsers.SkipWhiteSpace(NumberLiterals.CreateNumberLiteralParser<T>(numberOptions, decimalSeparator, groupSeparator));

    /// <summary>
    /// Builds a parser that matches an integer with an option leading sign.
    /// </summary>
    public Parser<long> Integer(NumberOptions numberOptions = NumberOptions.Integer) => Number<long>(numberOptions);

    /// <summary>
    /// Builds a parser that matches a floating point number represented as a <lang cref="decimal"/> value.
    /// </summary>
    public Parser<decimal> Decimal(NumberOptions numberOptions = NumberOptions.Float) => Number<decimal>(numberOptions);

    /// <summary>
    /// Builds a parser that matches a floating point number represented as a <lang cref="float"/> value.
    /// </summary>
    [Obsolete("Prefer Number<float>(NumberOptions.Float) instead.")]
    public Parser<float> Float(NumberOptions numberOptions = NumberOptions.Float) => Number<float>(numberOptions);

    /// <summary>
    /// Builds a parser that matches a floating point number represented as a <lang cref="double"/> value.
    /// </summary>
    [Obsolete("Prefer Number<double>(NumberOptions.Float) instead.")]
    public Parser<double> Double(NumberOptions numberOptions = NumberOptions.Float) => Number<double>(numberOptions);

    /// <summary>
    /// Builds a parser that matches an quoted string that can be escaped.
    /// </summary>
    public Parser<string> String(StringLiteralQuotes quotes = StringLiteralQuotes.SingleOrDouble) => Parsers.SkipWhiteSpace(new StringLiteral(quotes));

    /// <summary>
    /// Builds a parser that matches an identifier which can have a different starting value that the rest of its chars.
    /// </summary>
    public Parser<string> Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null)
    {
        //if (extraStart == null && extraPart == null)
        //{
        //    return Parsers.SkipWhiteSpace(new IdentifierLiteral(Character._identifierStart, Character._identifierPart));
        //}
        //else
        //{
            // IdentifierLiteral doesn't support the Func<,> overload
            return Parsers.SkipWhiteSpace(new Identifier(extraStart, extraPart));
        //}
    }

    /// <summary>
    /// Builds a parser that matches an identifier which can have a different starting value that the rest of its chars.
    /// </summary>
    //public Parser<Region> Identifier(SearchValues<char> identifierStart, SearchValues<char> identifierPart) => Parsers.SkipWhiteSpace(new IdentifierLiteral(identifierStart, identifierPart));

    /// <summary>
    /// Builds a parser that matches an identifier which can have a different starting value that the rest of its chars.
    /// </summary>
    //public Parser<Region> Identifier(ReadOnlySpan<char> identifierStart, ReadOnlySpan<char> identifierPart) => Parsers.SkipWhiteSpace(new IdentifierLiteral(SearchValues.Create(identifierStart), SearchValues.Create(identifierPart)));

    /// <summary>
    /// Builds a parser that matches a char against a predicate.
    /// </summary>
    /// <param name="predicate">The predicate to match against each char.</param>
    /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<Unit> Pattern(Func<byte, bool> predicate, int minSize = 1, int maxSize = 0) => Parsers.SkipWhiteSpace(new PatternLiteral(predicate, minSize, maxSize));

    /// <summary>
    /// Builds a parser that matches a list of chars.
    /// </summary>
    /// <param name="searchValues">The <see cref="SearchValues{T}"/> instance to match against each char.</param>
    /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    //public Parser<Region> AnyOf(SearchValues<char> searchValues, int minSize = 1, int maxSize = 0) => Parsers.SkipWhiteSpace(new SearchValuesCharLiteral(searchValues, minSize, maxSize));

    /// <summary>
    /// Builds a parser that matches a list of chars.
    /// </summary>
    /// <param name="values">The set of char to match.</param>
    /// <param name="minSize">The minimum number of matches required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of matches it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<Region> AnyOf(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0) => Parsers.SkipWhiteSpace(new ListOfChars(values, minSize, maxSize));

    /// <summary>
    /// Builds a parser that matches anything but a list of chars.
    /// </summary>
    /// <param name="searchValues">The <see cref="SearchValues{T}"/> instance to ignore against each char.</param>
    /// <param name="minSize">The minimum number of chars required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of chars it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    //public Parser<Region> NoneOf(SearchValues<char> searchValues, int minSize = 1, int maxSize = 0) => Parsers.SkipWhiteSpace(new SearchValuesCharLiteral(searchValues, minSize, maxSize, negate: true));

    /// <summary>
    /// Builds a parser that matches anything but a list of chars.
    /// </summary>
    /// <param name="values">The set of chars not to match.</param>
    /// <param name="minSize">The minimum number of chars required. Defaults to 1.</param>
    /// <param name="maxSize">When the parser reaches the maximum number of chars it returns <see langword="True"/>. Defaults to 0, i.e. no maximum size.</param>
    public Parser<Region> NoneOf(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0) => Parsers.SkipWhiteSpace(new ListOfChars(values, minSize, maxSize, negate: true));

    /// <summary>
    /// Builds a parser that matches single line comments.
    /// </summary>
    /// <param name="singleLineStart">The text that starts the single line comment, e.g., <code>"//"</code>, <code>"--"</code>, <code>"#"</code></param>
    /// <returns></returns>
    public Parser<Region> Comments(string singleLineStart) => Parsers.Literals.WhiteSpace(includeNewLines: true).Optional().SkipAnd(Parsers.Literals.Comments(singleLineStart));

    /// <summary>
    /// Builds a parser that matches multi line comments.
    /// </summary>
    /// <param name="multiLineStart">The text that starts the multi line comment, e.g., <code>"/*"</code></param>
    /// <param name="multiLineEnd">The text that ends the multi line comment, e.g., <code>"*/"</code></param>
    /// <returns></returns>
    public Parser<Region> Comments(string multiLineStart, string multiLineEnd) => Parsers.Literals.WhiteSpace(includeNewLines: true).Optional().SkipAnd(Parsers.Literals.Comments(multiLineStart, multiLineEnd));
}
