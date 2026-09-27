namespace Paspan;

public class ParseException(string message, int position = -1) : Exception(message)
{
    /// <summary>
    /// The byte offset in the input where the error was raised, or <c>-1</c> when unknown.
    /// </summary>
    public int Position { get; } = position;
}
