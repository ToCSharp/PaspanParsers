namespace Paspan;

public class ParseError
{
    public string Message { get; set; }

    /// <summary>
    /// The byte offset in the input where the error was raised, or <c>-1</c> when unknown.
    /// </summary>
    public int Position { get; set; } = -1;

    /// <summary>
    /// The 1-based line of <see cref="Position"/>, or <c>0</c> when unknown.
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// The 1-based column (in characters) of <see cref="Position"/>, or <c>0</c> when unknown.
    /// </summary>
    public int Column { get; set; }

    public override string ToString() => Line > 0 ? $"{Message} at ({Line}:{Column})" : Message;
}
