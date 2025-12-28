using System.Runtime.CompilerServices;

namespace Paspan.Fluent;

/// <summary>
/// Парсер для ключевых слов. Проверяет, что после ключевого слова не идет буква или цифра (word boundary).
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
            var nextByte = reader.Current;
            
            // Если следующий байт - буква, это не keyword
            if (IsLetter(nextByte))
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
    private static bool IsLetter(byte b)
    {
        // Проверяем ASCII буквы (A-Z, a-z)
        return (b >= 65 && b <= 90) ||   // A-Z
               (b >= 97 && b <= 122);    // a-z
    }

    public override string ToString() => $"Keyword '{_text}'";
}

