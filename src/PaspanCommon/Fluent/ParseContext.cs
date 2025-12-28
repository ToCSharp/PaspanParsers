using System.Runtime.CompilerServices;

namespace Paspan.Fluent;

public class ParseContext(bool useNewLines = false, bool disableLoopDetection = false)
{
    /// <summary>
    /// Whether new lines are treated as normal chars or white spaces.
    /// </summary>
    /// <remarks>
    /// When <c>false</c>, new lines will be skipped like any other white space.
    /// Otherwise white spaces need to be read explicitely by a rule.
    /// </remarks>
    public bool UseNewLines { get; private set; } = useNewLines;

    /// <summary>
    /// Whether to disable loop detection for recursive parsers. Default is <c>false</c>.
    /// </summary>
    /// <remarks>
    /// When <c>false</c>, loop detection is enabled and will prevent infinite recursion at the same position.
    /// When <c>true</c>, loop detection is disabled. This may be needed when the ParseContext itself is mutated
    /// during loops and can change the end result of parsing at the same location.
    /// </remarks>
    public bool DisableLoopDetection { get; } = disableLoopDetection;

    /// <summary>
    /// Tracks parser-position pairs to detect infinite recursion at the same position.
    /// </summary>
    private readonly HashSet<ParserPosition> _activeParserPositions = !disableLoopDetection ? new HashSet<ParserPosition>(ParserPositionComparer.Instance) : null!;

    /// <summary>
    /// Delegate that is executed whenever a parser is invoked.
    /// </summary>
    public Action<object, ParseContext> OnEnterParser { get; set; }

    /// <summary>
    /// The parser that is used to parse whitespaces and comments.
    /// </summary>
    public Parser<Region> WhiteSpaceParser { get; set; }

    public void SkipWhiteSpace(ref SpanReader reader)
    {
        if (WhiteSpaceParser is null)
        {
            if (UseNewLines)
            {
                reader.SkipWhiteSpace();
            }
            else
            {
                reader.SkipWhiteSpaceOrNewLine();
            }
        }
        else
        {
            ParseResult<Region> _ = new();
            WhiteSpaceParser.Parse(ref reader, this, ref _);
        }
    }

    /// <summary>
    /// Called whenever a parser is invoked. Will be used to detect invalid states and infinite loops.
    /// </summary>
    public void EnterParser<T>(Parser<T> parser)
    {
        //OnEnterParser?.Invoke(parser, this);
    }

    /// <summary>
    /// Called whenever a parser exits.
    /// </summary>
    public void ExitParser<T>(Parser<T> parser)
    {
        // Reserved for future use
    }

    /// <summary>
    /// Checks if a parser is currently active at the current position.
    /// </summary>
    /// <param name="parser">The parser to check.</param>
    /// <param name="position">The current position offset.</param>
    /// <returns>True if the parser is active at this position (infinite recursion detected), false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsParserActiveAtPosition(object parser, int position)
    {
        return _activeParserPositions.Contains(new ParserPosition(parser, position));
    }

    /// <summary>
    /// Marks a parser as active at the current position.
    /// </summary>
    /// <param name="parser">The parser to mark as active.</param>
    /// <param name="position">The current position offset.</param>
    /// <returns>True if the parser was added (not previously active at this position), false if it was already active at this position.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool PushParserAtPosition(object parser, int position)
    {
        return _activeParserPositions.Add(new ParserPosition(parser, position));
    }

    /// <summary>
    /// Marks a parser as inactive at the current position.
    /// </summary>
    /// <param name="parser">The parser to mark as inactive.</param>
    /// <param name="position">The position offset where the parser was entered.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PopParserAtPosition(object parser, int position)
    {
        _activeParserPositions.Remove(new ParserPosition(parser, position));
    }

    /// <summary>
    /// Represents a parser instance at a specific position for cycle detection.
    /// </summary>
    private readonly record struct ParserPosition(object Parser, int Position);

    /// <summary>
    /// Uses reference equality for parsers to avoid calling user GetHashCode overrides.
    /// </summary>
    private sealed class ParserPositionComparer : IEqualityComparer<ParserPosition>
    {
        public static readonly ParserPositionComparer Instance = new();

        public bool Equals(ParserPosition x, ParserPosition y) => ReferenceEquals(x.Parser, y.Parser) && x.Position == y.Position;

        public int GetHashCode(ParserPosition obj)
        {
            unchecked
            {
                var hash = RuntimeHelpers.GetHashCode(obj.Parser);
                hash = (hash * 397) ^ obj.Position;
                return hash;
            }
        }
    }
}
