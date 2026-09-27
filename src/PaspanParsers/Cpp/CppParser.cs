using Paspan;
using Paspan.Fluent;

namespace PaspanParsers.Cpp;

/// <summary>
/// C++ parser. The hand-written <see cref="SyntaxParser"/> (partial files under <c>Cpp/Parser/</c>) parses
/// the translation unit; it is wired in here as a combinator parser.
/// </summary>
public static class CppParser
{
    /// <summary>
    /// The translation unit: the whole input, including the trivia around the declarations.
    /// </summary>
    public static readonly Parser<TranslationUnit> TranslationUnitParser =
        new SyntaxRuleParser<TranslationUnit>(SyntaxParser.ParseTranslationUnitRule);

    public static TranslationUnit Parse(string input, CppParseOptions options = null)
    {
        return TryParse(input, options, out var result, out _) ? result : null;
    }

    public static bool TryParse(string input, out TranslationUnit result, out ParseError error)
    {
        return TryParse(input, null, out result, out error);
    }

    /// <summary>
    /// Parses <paramref name="input"/>. When it is not valid C++, returns false and an <paramref name="error"/>
    /// at the token after the furthest token the parser could consume.
    /// </summary>
    public static bool TryParse(string input, CppParseOptions options, out TranslationUnit result, out ParseError error)
    {
        return TryParseSource(Utf8Source.FromString(input), options, out result, out error);
    }

    /// <summary>
    /// Parses C++ source given as UTF-8 bytes, such as the content of a file, without decoding it to a string.
    /// A leading byte order mark is skipped: node spans are offsets into <paramref name="utf8Source"/>
    /// after it, which is what <see cref="Utf8Source.WithoutByteOrderMark(ReadOnlyMemory{byte})"/> returns.
    /// </summary>
    public static bool TryParse(ReadOnlyMemory<byte> utf8Source, CppParseOptions options, out TranslationUnit result, out ParseError error)
    {
        return TryParseSource(Utf8Source.WithoutByteOrderMark(utf8Source), options, out result, out error);
    }

    private static bool TryParseSource(ReadOnlyMemory<byte> source, CppParseOptions options, out TranslationUnit result, out ParseError error)
    {
        try
        {
            return TryParseCore(source, options, out result, out error);
        }
        catch (InsufficientExecutionStackException)
        {
            // Deeply nested input: parse again on a thread with a large stack
        }

        TranslationUnit largeStackResult = null;
        ParseError largeStackError = null;
        var success = false;
        LargeStack.Run(() =>
        {
            try
            {
                success = TryParseCore(source, options, out largeStackResult, out largeStackError);
            }
            catch (InsufficientExecutionStackException)
            {
                largeStackError = new ParseError { Message = "The input is nested too deeply." };
            }
        });

        result = largeStackResult;
        error = largeStackError;
        return success;
    }

    private static bool TryParseCore(ReadOnlyMemory<byte> source, CppParseOptions options, out TranslationUnit result, out ParseError error)
    {
        var reader = new SpanReader(source.Span);
        var context = new CppParseContext(options);
        if (TranslationUnitParser.TryParse(ref reader, context, out result, out error))
        {
            return true;
        }

        error ??= SyntaxParser.DescribeFailure(source.Span, context);
        return false;
    }
}
