
using Paspan.Common;
using System.Runtime.CompilerServices;
using System.Text;
using static Paspan.Common.Constants;

namespace Paspan;
/// <summary>
/// This class is used to return tokens extracted from the input buffer.
/// </summary>
public ref partial struct SpanReader
{
    ReadOnlySpan<byte> _buffer;
    int _consumed;
    //int _line;
    //int _column;

    public ReadOnlySpan<byte> ValueSpan { get; private set; }

    public SpanReader(string text)
        : this(Encoding.UTF8.GetBytes(text).AsSpan())
    {
    }

    public SpanReader(string text, int index, int count)
        : this(Encoding.UTF8.GetBytes(text, index, count).AsSpan())
    {
    }

    public SpanReader(byte[] data)
        : this(data.AsSpan())
    {
    }

    public SpanReader(ReadOnlySpan<byte> data)
    {
        _buffer = data;
        _consumed = 0;
        ValueSpan = [];
    }

    void MoveConsumedTo(int consumed)
    {
        _consumed = consumed;
    }

    public int Length => _buffer.Length - _consumed;

    public int CaptureState() => _consumed;

    public int GetCurrentPosition() => _consumed;

    public int GetCurrentPosition(int offset) => _consumed + offset;

    public void RollBackState(int state) => _consumed = state;

    public int GetPosition(int pos, int offset = 0) => pos + offset;

    public void SetValue(int start, int end) => ValueSpan = _buffer[start..end];

    public bool Skip(byte b)
    {
        if (_consumed >= _buffer.Length)
        {
            return false;
        }

        if (b != _buffer[_consumed])
        {
            return false;
        }
        _consumed++;
        return true;
    }

    public bool PrevByteIs(byte b)
    {
        return _consumed > 0 && _buffer[_consumed - 1] == b;
    }

    public bool SkipOneOf(params byte[] bytes)
    {
        if (_consumed >= _buffer.Length)
        {
            return false;
        }

        var b = _buffer[_consumed];
        if (bytes.Any(it => it == b))
        { // bytes.Contains(b)
            _consumed++;
            return true;
        }
        return false;
    }

    public bool SkipWhile(Func<byte, bool> predicate, int maxSize)
    {
        var start = _consumed;
        if (!SkipWhileInternal(predicate, maxSize))
        {
            return false;
        }
        ValueSpan = _buffer.Slice(start, _consumed - start);
        return true;
    }

    bool SkipWhileInternal(Func<byte, bool> predicate, int maxSize)
    {
        var length = 0;
        while (true)
        {
            if (_consumed >= _buffer.Length)
            {
                return true;
            }
            if (predicate(_buffer[_consumed]))
            {
                _consumed++;
                length++;
                if (length == maxSize)
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }
        return true;
    }

    public bool SkipZeroOrOne(byte b)
    {
        if (_consumed >= _buffer.Length)
        {
            return true;
        }

        if (b == _buffer[_consumed])
        {
            _consumed++;
        }
        return true;
    }
    public bool Skip(ReadOnlySpan<byte> bytes)
    {
        if (_consumed + bytes.Length > _buffer.Length)
        {
            return false;
        }
        if (!_buffer.Slice(_consumed).StartsWith(bytes))
            return false;
        _consumed += bytes.Length;
        return true;
    }

    // remove allocation, 
    /// <summary>
    /// remove allocation, use Skip(ReadOnlySpan<byte> bytes)
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public bool Skip(string str) => Skip(new ReadOnlySpan<byte>(Encoding.UTF8.GetBytes(str)));

    public bool Read(int count)
    {
        if (_consumed + count > _buffer.Length)
        {
            return false;
        }
        ValueSpan = _buffer.Slice(_consumed, count);
        _consumed += count;
        return true;
    }

    public bool ReadByte(out byte b)
    {
        if (_consumed > _buffer.Length)
        {
            b = 0;
            return false;
        }
        b = _buffer[_consumed];
        ValueSpan = _buffer.Slice(_consumed, 1);
        _consumed += 1;
        return true;
    }
    public bool Peek(int count)
    {
        if (_consumed + count > _buffer.Length)
        {
            return false;
        }
        ValueSpan = _buffer.Slice(_consumed, count);
        return true;
    }
    public bool PeekBack(int count)
    {
        if (_consumed < count)
        {
            return false;
        }
        ValueSpan = _buffer.Slice(_consumed - count, count);
        return true;
    }

    public void SkipWhiteSpace()
    {
        // Create local copy to avoid bounds checks.
        ReadOnlySpan<byte> localBuffer = _buffer;
        for (; _consumed < localBuffer.Length; _consumed++)
        {
            byte val = localBuffer[_consumed];

            if (!Character.IsWhiteSpace(val))
            {
                break;
            }
        }
    }
    public void SkipWhiteSpaceOrNewLine()
    {
        // Create local copy to avoid bounds checks.
        ReadOnlySpan<byte> localBuffer = _buffer;
        for (; _consumed < localBuffer.Length; _consumed++)
        {
            byte val = localBuffer[_consumed];

            if (!Character.IsWhiteSpaceOrNewLine(val))
            {
                break;
            }
        }
    }
    public void ReadNonWhiteSpace()
    {
        ReadOnlySpan<byte> localBuffer = _buffer;
        for (; _consumed < localBuffer.Length; _consumed++)
        {
            byte val = localBuffer[_consumed];

            if (Character.IsWhiteSpace(val))
            {
                break;
            }
        }
    }
    public void ReadNonWhiteSpaceOrNewLine()
    {
        ReadOnlySpan<byte> localBuffer = _buffer;
        for (; _consumed < localBuffer.Length; _consumed++)
        {
            byte val = localBuffer[_consumed];

            if (Character.IsWhiteSpaceOrNewLine(val))
            {
                break;
            }
        }
    }

    public byte Current => _consumed >= _buffer.Length ? (byte)0 : _buffer[_consumed];
    public bool Eof() => _consumed >= _buffer.Length;

    public bool ReadToEof()
    {
        var start = _consumed;
        MoveToEof();
        ValueSpan = _buffer.Slice(start, _consumed - start);
        return true;
    }

    public bool MoveToEof()
    {
        _consumed = _buffer.Length;
        return true;
    }

    // TODO: check
    public bool ReadBefore(byte b, int maxLen)
    {
        var start = _consumed;
        if (SkipBeforeInternal(b, maxLen))
        {

            ValueSpan = _buffer.Slice(start, _consumed - start);
            return true;
        }
        else
        {
            _consumed = start;
            return false;
        }
    }

    public bool SkipBefore(byte b, int maxLen)
    {
        var start = _consumed;
        if (!SkipBeforeInternal(b, maxLen))
        {
            _consumed = start;
            return false;
        }
        return true;
    }

    bool SkipBeforeInternal(byte b, int maxLen)
    {
        for (int i = 0; i < maxLen; i++)
        {
            if (_consumed >= _buffer.Length)
            {
                return false;
            }
            if (_buffer[_consumed] == b)
            {
                _consumed++;
                return true;
            }
            _consumed++;
        }
        return false;
    }

    //public const byte NullChar = 0;
    /// <summary>
    /// Advances the cursor and tracks its current location (line and column) with the knowledge there are no new lines (\r or \n).
    /// </summary>
    public void AdvanceNoNewLines(int offset)
    {
        var newOffset = _consumed + offset;
        var length = _buffer.Length - 1;

        // Detect if the cursor will be over Eof
        if (newOffset > length)
        {
            //Eof = true;
            //_column += newOffset - length;
            _consumed = _buffer.Length;
            //Current = NullChar;
            return;
        }

        //Current = Buffer[newOffset];
        _consumed = newOffset;
        //_column += offset;
    }

    public bool ConsumeIntegerDigits()
    {
        byte nextByte;
        var i = _consumed;
        for (; i < _buffer.Length; i++)
        {
            nextByte = _buffer[i];
            if (!SpanReaderHelpers.IsDigit(nextByte))
            {
                break;
            }
        }
        if (i > _consumed)
        {
            var length = i - _consumed;
            ValueSpan = _buffer.Slice(_consumed, length);
            _consumed = i;
            return true;
        }
        return false;
    }
    public bool ConsumeDecimalDigits()
    {
        var start = _consumed;
        if (!ConsumeIntegerDigits())
            return false;
        if (_consumed < _buffer.Length)
        {
            if (_buffer[_consumed] == '.' /*|| _buffer[_consumed] == ','*/)
            {
                var c = _consumed;
                _consumed++;
                if (!ConsumeIntegerDigits())
                {
                    _consumed = c;
                }
            }
        }
        ValueSpan = _buffer.Slice(start, _consumed - start);
        return true;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadSingleQuotedString()
    {
        return ReadQuotedString(SingleQuote);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ReadDoubleQuotedString()
    {
        return ReadQuotedString(Quote);
    }

    public bool ReadQuotedString()
    {
        if (_consumed >= _buffer.Length)
            return false;
        var startChar = _buffer[_consumed];

        if (startChar != SingleQuote && startChar != Quote)
        {
            return false;
        }

        return ReadQuotedString(startChar);
    }

    /// <summary>
    /// Reads a string token enclosed in single or double quotes.
    /// </summary>
    /// <remarks>
    /// This method doesn't escape the string, but only validates its content is syntactically correct.
    /// The resulting Span contains the original quotes.
    /// </remarks>
    private bool ReadQuotedString(byte quoteChar)
    {
        // minimum ""
        if (_consumed + 1 >= _buffer.Length)
            return false;
        var startChar = _buffer[_consumed];

        if (startChar != quoteChar)
        {
            return false;
        }

        var start = _consumed;

        // Check for empty string ""
        if (_consumed + 1 < _buffer.Length && _buffer[_consumed + 1] == quoteChar)
        {
            _consumed += 2;
            ValueSpan = _buffer.Slice(start + 1, 0); // Empty string content
            return true;
        }

        // Fast path if there aren't any escape char until next quote
        var startOffset = _consumed + 2;

        var nextQuote = _buffer[startOffset..].IndexOf(startChar);

        if (nextQuote == -1)
        {
            // There is no end quote, not a string
            return false;
        }

        var nextEscape = _buffer.Slice(startOffset, nextQuote + 1).IndexOf(BackSlash);

        // If the next escape if not before the next quote, we can return the string as-is
        if (nextEscape == -1)
        {
            var length = nextQuote + 1;
            _consumed += length + 2;

            ValueSpan = _buffer.Slice(start + 1, length);
            return true;
        }


        _consumed++;

        while (_consumed < _buffer.Length && _buffer[_consumed] != startChar)
        {
            // We can read Eof if there is an escaped quote sequence and no actual end quote, e.g. "'abc\'def"
            if (_consumed >= _buffer.Length)
            {
                _consumed = start;
                return false;
            }

            if (_buffer[_consumed] == BackSlash)
            {
                _consumed++;
                if (_consumed >= _buffer.Length)
                {
                    _consumed = start;
                    return false;
                }

                switch (_buffer[_consumed])
                {
                    case (byte)'0':
                    case BackSlash:
                    case (byte)'b':
                    case (byte)'f':
                    case (byte)'n':
                    case (byte)'r':
                    case (byte)'t':
                    case (byte)'v':
                    case SingleQuote:
                    case Quote:
                    case (byte)'/':  // JSON allows escaped forward slash
                        break;

                    case (byte)'u':

                        // https://stackoverflow.com/a/32175520/142772
                        // exactly 4 digits

                        var isValidUnicode = false;

                        _consumed++;

                        if (_consumed < _buffer.Length && Character.IsHexDigit(_buffer[_consumed]))
                        {
                            _consumed++;
                            if (_consumed < _buffer.Length && Character.IsHexDigit(_buffer[_consumed]))
                            {
                                _consumed++;
                                if (_consumed < _buffer.Length && Character.IsHexDigit(_buffer[_consumed]))
                                {
                                    _consumed++;
                                    if (_consumed < _buffer.Length && Character.IsHexDigit(_buffer[_consumed]))
                                    {
                                        isValidUnicode = true;
                                    }
                                }
                            }
                        }

                        if (!isValidUnicode)
                        {
                            _consumed = start;

                            return false;
                        }

                        break;
                    case (byte)'x':

                        // https://stackoverflow.com/a/32175520/142772
                        // exactly 4 digits

                        bool isValidHex = false;

                        _consumed++;

                        if (_consumed < _buffer.Length && Character.IsHexDigit(_buffer[_consumed]))
                        {
                            isValidHex = true;
                            _consumed++;
                            if (_consumed < _buffer.Length && Character.IsHexDigit(_buffer[_consumed]))
                            {
                                _consumed++;

                                if (_consumed < _buffer.Length && Character.IsHexDigit(_buffer[_consumed]))
                                {
                                    _consumed++;

                                    if (_consumed < _buffer.Length && Character.IsHexDigit(_buffer[_consumed]))
                                    {
                                        isValidUnicode = true;
                                    }
                                }
                            }
                        }

                        if (!isValidHex)
                        {
                            _consumed = start;

                            return false;
                        }

                        break;
                    default:
                        _consumed = start;

                        return false;
                }
            }

            _consumed++;
        }

        _consumed++;
        if (_consumed > _buffer.Length)
        {
            _consumed = start;
            return false;
        }

        ValueSpan = _buffer.Slice(start + 1, _consumed - start - 2);

        return true;
    }
}
