namespace Paspan.Fluent;

public abstract partial class Parser<T>
{
    public string Name { get; set; }

    public T Parse(string text, ParseContext context = null)
    {
        var reader = new SpanReader(text);

        return Parse(ref reader, context);
    }

    public T Parse(byte[] text, ParseContext context = null)
    {
        var reader = new SpanReader(text);

        return Parse(ref reader, context);
    }

    public T Parse(ref SpanReader reader, ParseContext context = null)
    {
        context ??= new ParseContext();

        var localResult = new ParseResult<T>();

        var success = Parse(ref reader, context, ref localResult);

        if (success)
        {
            return localResult.Value;
        }

        return default;
    }

    //public bool TryParse(ref SpanReader reader, ParseContext context = null, T result)
    //{
    //    context ??= new ParseContext();

    //    var localResult = new ParseResult<T>();

    //    var success = Parse(ref reader, context, ref localResult);

    //    if (success)
    //    {
    //        result = localResult.Value;
    //        return true;
    //    }

    //    result = default;
    //    return false;
    //}

    public bool TryParse(string text, out T value)
    {
        return TryParse(text, out value, out _);
    }

    public bool TryParse(string text, int index, int count, out T value)
    {
        return TryParse(text, index, count, out value, out _);
    }

    public bool TryParse(byte[] text, out T value)
    {
        return TryParse(text, out value, out _);
    }

    public bool TryParse(ref SpanReader reader, out T value)
    {
        return TryParse(ref reader, out value, out _);
    }

    public bool TryParse(string text, out T value, out ParseError error)
    {
        var reader = new SpanReader(text);
        return TryParse(ref reader, new ParseContext(), out value, out error);
    }

    public bool TryParse(string text, int index, int count, out T value, out ParseError error)
    {
        var reader = new SpanReader(text, index, count);
        return TryParse(ref reader, new ParseContext(), out value, out error);
    }

    public bool TryParse(byte[] text, out T value, out ParseError error)
    {
        var reader = new SpanReader(text);
        return TryParse(ref reader, new ParseContext(), out value, out error);
    }

    public bool TryParse(ref SpanReader reader, out T value, out ParseError error)
    {
        return TryParse(ref reader, new ParseContext(), out value, out error);
    }

    public bool TryParse(ref SpanReader reader, ParseContext context, out T value, out ParseError error)
    {
        error = null;

        try
        {

            var localResult = new ParseResult<T>();

            var success = this.Parse(ref reader, context, ref localResult);

            if (success)
            {
                value = localResult.Value;
                return true;
            }
        }
        catch (ParseException e)
        {
            error = new ParseError
            {
                Message = e.Message,
                //Position = e.Position
            };
        }

        value = default;
        return false;
    }
}
