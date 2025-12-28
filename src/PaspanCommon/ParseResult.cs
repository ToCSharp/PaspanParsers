namespace Paspan;

public ref struct ParseResult<T>
{
    public int Start;
    public int End;
    public T Value;
    public int Length => End - Start;

    public ParseResult(int start, int end, T value)
    {
        Start = start;
        End = end;
        Value = value;
    }

    public ParseResult(T value)
    {
        Start = 0;
        End = 0;
        Value = value;
    }

    public void Set(T value)
    {
        Value = value;
    }
    public void Set(int start, int end, T value)
    {
        Start = start;
        End = end;
        Value = value;
    }
}
