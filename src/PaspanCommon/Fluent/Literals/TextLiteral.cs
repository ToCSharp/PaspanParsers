using Paspan.Common;
using System.Runtime.CompilerServices;
using System.Text;

namespace Paspan.Fluent;

public sealed class TextLiteral : Parser<string>
{
    private readonly StringComparison _comparisonType;
    private readonly bool _hasNewLines;

    public TextLiteral(string text, StringComparison comparisonType)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        TextBytes = Encoding.UTF8.GetBytes(Text);
        _comparisonType = comparisonType;
        _hasNewLines = TextBytes.Any(Character.IsNewLine);

    }

    public string Text { get; }
    public byte[] TextBytes { get; }

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<string> result)
    {
        context.EnterParser(this);

        if (_comparisonType == StringComparison.Ordinal)
        {
            if (reader.Skip(new ReadOnlySpan<byte>(TextBytes)))
            {
                result.Set(Text);
                return true;
            }
        }
        else if (_comparisonType == StringComparison.OrdinalIgnoreCase)
        {
            var start = reader.CaptureState();
            
            if (SkipCaseInsensitive(ref reader, TextBytes))
            {
                // Возвращаем фактически прочитанный текст (с сохранением регистра из входных данных)
                var actualText = reader.GetString(start, TextBytes.Length);
                result.Set(actualText);
                return true;
            }
            
            reader.RollBackState(start);
        }
        else
        {
            throw new NotImplementedException($"{nameof(TextLiteral)} {_comparisonType}");
        }

        return false;
    }
    
    private static bool SkipCaseInsensitive(ref SpanReader reader, byte[] expectedBytes)
    {
        var position = reader.CaptureState();
        
        // Проверяем, хватит ли байтов
        if (position + expectedBytes.Length > reader.Length + position)
        {
            return false;
        }
        
        // Сравниваем байт за байтом без учета регистра
        for (int i = 0; i < expectedBytes.Length; i++)
        {
            if (reader.Eof())
            {
                return false;
            }
            
            byte currentByte = reader.Current;
            byte expectedByte = expectedBytes[i];
            
            // Сравниваем с учетом регистра для ASCII букв (a-z, A-Z)
            if (!ByteEqualsIgnoreCase(currentByte, expectedByte))
            {
                return false;
            }
            
            reader.Read(1);
        }
        
        return true;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ByteEqualsIgnoreCase(byte a, byte b)
    {
        // Если байты равны - сразу возвращаем true
        if (a == b)
        {
            return true;
        }
        
        // Проверяем ASCII буквы (A-Z = 65-90, a-z = 97-122)
        // Разница между заглавной и строчной буквой = 32
        if (IsAsciiLetter(a) && IsAsciiLetter(b))
        {
            // Приводим оба байта к нижнему регистру и сравниваем
            return ToLowerAscii(a) == ToLowerAscii(b);
        }
        
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiLetter(byte b)
    {
        return (b >= 65 && b <= 90) || (b >= 97 && b <= 122);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ToLowerAscii(byte b)
    {
        // Если это заглавная буква (A-Z), преобразуем в строчную
        if (b >= 65 && b <= 90)
        {
            return (byte)(b + 32);
        }
        return b;
    }
    public override string ToString() => $"TextLiteral '{Text}'";
}
