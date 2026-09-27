using Paspan;
using Paspan.Fluent;

namespace PaspanParsers.CSharp;

// Trivia: white space and comments between tokens.
public partial class CSharpParser
{
    /// <summary>
    /// Makes <paramref name="parser"/> skip C# trivia before every token and consume
    /// trailing trivia before the end of the input.
    /// </summary>
    private static Parser<T> WithTrivia<T>(Parser<T> parser)
    {
        return new EndOfInput<T>(parser).WithComments(comments =>
        {
            comments
                .WithWhiteSpaceOrNewLine()
                .WithSingleLine("//")
                .WithMultiLine("/*", "*/")
                ;
        });
    }

    /// <summary>
    /// Succeeds when the inner parser succeeds and only trivia remains after it.
    /// </summary>
    private sealed class EndOfInput<T>(Parser<T> parser) : Parser<T>
    {
        public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
        {
            var start = reader.CaptureState();

            if (parser.Parse(ref reader, context, ref result))
            {
                context.SkipWhiteSpace(ref reader);

                if (reader.Eof())
                {
                    return true;
                }
            }

            reader.RollBackState(start);
            return false;
        }
    }
}
