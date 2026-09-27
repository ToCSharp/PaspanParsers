using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Paspan.Fluent;

/// <summary>
/// Парсер для ключевых слов. Проверяет, что после ключевого слова не идет буква (word boundary). Цифры и '_' допустимы.
/// </summary>
public sealed class Keyword(string text, StringComparison comparisonType) : Parser<string>
{
    private readonly TextLiteral _textParser = new TextLiteral(text, comparisonType);
    private readonly string _text = text ?? throw new ArgumentNullException(nameof(text));

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<string> result)
    {
        context.EnterParser(this);

        var start = reader.CaptureState();
        var textResult = new ParseResult<string>();

        // Сначала парсим текст
        if (!_textParser.Parse(ref reader, context, ref textResult))
        {
            return false;
        }

        // Проверяем word boundary: следующий символ не должен быть буквой
        // Цифры допустимы (например, "return123" - валидно)
        if (!reader.Eof())
        {
            // Если следующий символ - буква (включая не-ASCII, например кириллицу), это не keyword
            if (IsLetter(reader.GetRemaining()))
            {
                reader.RollBackState(start);
                return false;
            }
        }

        // Все проверки пройдены
        result.Set(textResult.Value);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLetter(ReadOnlySpan<byte> next)
    {
        var b = next[0];

        if (b < 0x80)
        {
            // ASCII буквы (A-Z, a-z)
            return (b >= 65 && b <= 90) ||   // A-Z
                   (b >= 97 && b <= 122);    // a-z
        }

        return Rune.DecodeFromUtf8(next, out var rune, out _) == OperationStatus.Done && Rune.IsLetter(rune);
    }

    public override string ToString() => $"Keyword '{_text}'";
}

