using System;
using System.Linq.Expressions;

namespace Paspan.Fluent;

public sealed class PatternLiteral : Parser<>
{
    private readonly Func<char, bool> _predicate;
    private readonly int _minSize;
    private readonly int _maxSize;

    public PatternLiteral(Func<char, bool> predicate, int minSize = 1, int maxSize = 0)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        _minSize = minSize;
        _maxSize = maxSize;

        Name = "PatternLiteral";
    }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        if (context.Scanner.Cursor.Eof || !_predicate(context.Scanner.Cursor.Current))
        {
            context.ExitParser(this);
            return false;
        }

        var startPosition = context.Scanner.Cursor.Position;
        var start = startPosition.Offset;

        context.Scanner.Cursor.Advance();
        var size = 1;

        while (!context.Scanner.Cursor.Eof && (_maxSize <= 0 || size < _maxSize) && _predicate(context.Scanner.Cursor.Current))
        {
            context.Scanner.Cursor.Advance();
            size++;
        }

        if (size >= _minSize)
        {
            var end = context.Scanner.Cursor.Offset;
            result.Set(start, end, new TextSpan(context.Scanner.Buffer, start, end - start));

            context.ExitParser(this);
            return true;
        }

        // When the size constraint has not been met the parser may still have advanced the cursor.
        context.Scanner.Cursor.ResetPosition(startPosition);

        return false;
    }
}
