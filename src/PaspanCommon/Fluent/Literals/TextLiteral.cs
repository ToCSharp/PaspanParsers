using Paspan.Common;
using System.Buffers;
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

        var start = reader.CaptureState();

        if (_comparisonType == StringComparison.Ordinal)
        {
            if (reader.Skip(new ReadOnlySpan<byte>(TextBytes)))
            {
                var end = reader.GetCurrentPosition();

                // Expose the matched bytes to value readers like AsChar()
                reader.SetValue(start, end);
                result.Set(start, end, Text);
                return true;
            }
        }
        else if (_comparisonType == StringComparison.OrdinalIgnoreCase)
        {
            var length = MatchCaseInsensitive(reader.GetRemaining(), TextBytes);

            if (length >= 0)
            {
                var end = start + length;

                reader.RollBackState(end);
                reader.SetValue(start, end);

                // Возвращаем фактически прочитанный текст (с сохранением регистра из входных данных)
                result.Set(start, end, reader.GetString(start, length));
                return true;
            }
        }
        else
        {
            throw new NotImplementedException($"{nameof(TextLiteral)} {_comparisonType}");
        }

        return false;
    }

    /// <summary>
    /// Returns the number of input bytes matching <paramref name="expectedBytes"/> ignoring case, or -1.
    /// </summary>
    /// <remarks>
    /// Compares chars and not bytes since the upper and lower case forms of a non-ASCII char
    /// have different UTF-8 encodings, which can even have different lengths.
    /// </remarks>
    private static int MatchCaseInsensitive(ReadOnlySpan<byte> input, ReadOnlySpan<byte> expectedBytes)
    {
        var inputIndex = 0;
        var expectedIndex = 0;

        while (expectedIndex < expectedBytes.Length)
        {
            if (inputIndex >= input.Length)
            {
                return -1;
            }

            var a = input[inputIndex];
            var b = expectedBytes[expectedIndex];

            if (a < 0x80 && b < 0x80)
            {
                if (!ByteEqualsIgnoreCase(a, b))
                {
                    return -1;
                }

                inputIndex++;
                expectedIndex++;
                continue;
            }

            if (Rune.DecodeFromUtf8(input[inputIndex..], out var inputRune, out var inputLength) != OperationStatus.Done
                || Rune.DecodeFromUtf8(expectedBytes[expectedIndex..], out var expectedRune, out var expectedLength) != OperationStatus.Done)
            {
                return -1;
            }

            if (inputRune != expectedRune && Rune.ToUpperInvariant(inputRune) != Rune.ToUpperInvariant(expectedRune))
            {
                return -1;
            }

            inputIndex += inputLength;
            expectedIndex += expectedLength;
        }

        return inputIndex;
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
