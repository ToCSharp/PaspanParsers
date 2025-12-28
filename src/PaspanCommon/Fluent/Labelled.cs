namespace Paspan.Fluent;

interface ILabelled
{
    string Label { get; }
}
interface IHaveValueObject
{
    object ValueObject { get; }
}

public struct Labelled<T>(T value, string label) : ILabelled, IHaveValueObject
{
    public T Value = value;
    public string Label { get; } = label;
    public object ValueObject => Value;

    public override string ToString() => $"Labelled {typeof(T).Name}: '{Label}'={Value}";
}

public static partial class ParsersPlus
{
    public static Parser<Labelled<T>> Labelled<T>(this Parser<T> parser, string label)
        => new LabelledParser<T>(parser, label);

}

public sealed class LabelledParser<T>(Parser<T> parser, string lable) : Parser<Labelled<T>>
{
    public string Label = lable;
    private readonly Parser<T> _parser = parser;

    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<Labelled<T>> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        if (_parser.Parse(ref reader, context, ref parsed))
        {
            result.Set(new Labelled<T>(parsed.Value, Label));

            return true;
        }

        return false;
    }

    public override string ToString() => $"LabelledParser '{Label}': {_parser}";
}
