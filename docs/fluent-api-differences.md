# Fluent API Implementation Differences Between Paspan and Parlot

## Overview

Paspan is a fork of [Parlot](https://github.com/sebastienros/parlot) with a fundamental change: instead of working with strings (`string`), it uses `Span<byte>` and `ReadOnlySequence<byte>`. This leads to significant changes in API and internal parser implementation.

---

## Architectural Differences

### 1. Base Type for Parsing

**Parlot:**
- Works with `string` and `ReadOnlySpan<char>`
- Uses `Scanner` to read characters
- Parse method: `bool Parse(ParseContext context, ref ParseCursor cursor, out T result)`

**Paspan:**
- Works with `byte[]`, `Span<byte>`, `ReadOnlySequence<byte>`
- Uses `SpanReader` (rewritten version of `Utf8JsonReader`)
- Parse method: `bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)`

### 2. SpanReader vs Scanner

**SpanReader** (Paspan):
- `ref struct` for zero-allocation work
- Works directly with UTF-8 bytes
- Automatic string to UTF-8 conversion on creation
- Positioning via `CaptureState()` / `RollBackState(int)`

```csharp
public ref partial struct SpanReader
{
    ReadOnlySpan<byte> _buffer;
    int _consumed;
    
    public SpanReader(string text) : this(Encoding.UTF8.GetBytes(text).AsSpan()) { }
    public SpanReader(byte[] data) : this(data.AsSpan()) { }
    public SpanReader(ReadOnlySpan<byte> data) { ... }
}
```

**Scanner** (Parlot):
- Works with `TextSpan` (wrapper over string)
- Works with `char` characters
- Positioning via `Cursor`

### 3. ParseResult<T>

**Paspan:**
- `ref struct ParseResult<T>` - stack-only type
- Contains `Start`, `End` and `Value`
- Passed by reference to parse methods

```csharp
public ref struct ParseResult<T>
{
    public int Start;
    public int End;
    public T Value;
    
    public void Set(T value) { ... }
    public void Set(int start, int end, T value) { ... }
}
```

**Parlot:**
- Uses `out` parameters to return result
- Result is passed directly as `out T result`

### 4. Region vs TextSpan

**Paspan:**
```csharp
public struct Region
{
    public int Start;
    public int End;
    public int Length;
    
    public string? ToString(ReadOnlySpan<byte> data) => 
        Encoding.UTF8.GetString(data.Slice(Start, Length));
}
```

**Parlot:**
```csharp
public readonly struct TextSpan
{
    public int Offset;
    public int Length;
    
    public ReadOnlySpan<char> Span { get; }
    public string ToString() => new string(Span);
}
```

---

## Parser API Differences

### 5. Base Parser Signature

**Paspan:**
```csharp
public abstract partial class Parser<T>
{
    public abstract bool Parse(
        ref SpanReader reader, 
        ParseContext context, 
        ref ParseResult<T> result
    );
}
```

**Parlot:**
```csharp
public abstract class Parser<T>
{
    public abstract bool Parse(
        ParseContext context, 
        ref ParseCursor cursor, 
        out T result
    );
}
```

### 6. Literal Return Types

**Paspan:**
- `Char(char c)` → `Parser<Unit>` (instead of `Parser<char>`)
- `WhiteSpace()` → `Parser<Unit>` (instead of `Parser<TextSpan>`)
- `NonWhiteSpace()` → `Parser<Unit>` (instead of `Parser<TextSpan>`)
- `Pattern(Func<byte, bool>)` → `Parser<Unit>` (works with bytes!)

**Parlot:**
- `Char(char c)` → `Parser<char>`
- `WhiteSpace()` → `Parser<TextSpan>`
- `NonWhiteSpace()` → `Parser<TextSpan>`
- `Pattern(Func<char, bool>)` → `Parser<TextSpan>` (works with characters)

### 7. Unit Type

**Paspan** introduces the `Unit` type to denote "empty" result:

```csharp
public sealed class Unit
{
    private Unit() { }
    public static Unit Value { get; } = new Unit();
}
```

Used for parsers that don't return meaningful data (characters, spaces, separators).

**Parlot** has no `Unit` type, returns concrete values (`char`, `TextSpan`).

### 8. Result Conversion Methods

**Paspan** adds helper methods to convert `Unit` to values:

```csharp
public abstract partial class Parser<T>
{
    public Parser<ulong> AsHex() => new HexValue<T>(this);
    public Parser<char> AsChar() => new CharValue<T>(this);
}

public static partial class Parsers
{
    public static Parser<string> AsString<T>(this Parser<T> parser) => ...;
    public static Parser<object?> AsObject<T>(this Parser<T> parser) => ...;
}
```

Usage example:
```csharp
var divided = Terms.Char('/').AsChar(); // Parser<char>
var hex = Terms.Pattern(c => Character.IsHexDigit(c)).AsHex(); // Parser<ulong>
```

**Parlot** doesn't need such methods since literals immediately return typed values.

### 9. Skip Combinators

**Paspan** has convenient overloads for skipping characters and strings:

```csharp
public static Parser<T1> Skip<T1>(this Parser<T1> parser, char ch) => ...;
public static Parser<T1> Skip<T1>(this Parser<T1> parser, string str) => ...;

// Usage:
var parser = Terms.Identifier().Skip('=').And(Terms.Integer());
```

**Parlot** requires explicit parser creation for skipping:

```csharp
var parser = Terms.Identifier().AndSkip(Terms.Char('=')).And(Terms.Integer());
```

### 10. Integer Number Handling

**Paspan:**
```csharp
public class LiteralBuilder
{
    public Parser<int> Integer() => new IntegerLiteral();
    public Parser<long> Integer64() => new Integer64Literal();
}

public class TermBuilder
{
    public Parser<long> Integer(NumberOptions numberOptions = NumberOptions.Default) 
        => Parsers.SkipWhiteSpace(new Integer64Literal(numberOptions));
}
```

**Parlot:**
```csharp
public Parser<long> Integer() => ...;  // Only long
```

### 11. StringRegion Parser

**Paspan** adds special parser for zero-copy string work:

```csharp
public class LiteralBuilder
{
    public Parser<string> String(StringLiteralQuotes quotes = ...) => ...;
    public Parser<Region> StringRegion(StringLiteralQuotes quotes = ...) => ...;
}

public class TermBuilder
{
    public Parser<string> String(StringLiteralQuotes quotes = ...) => ...;
    public Parser<Region> StringRegion(StringLiteralQuotes quotes = ...) => ...;
}
```

`StringRegion` returns only string coordinates without `string` allocation.

**Parlot** only returns `TextSpan`, which contains `ReadOnlySpan<char>`.

### 12. Labelled Parsers

**Paspan** extends support for named results:

```csharp
public static Parser<(T1, Labelled<T2>)> And<T1, T2>(
    this Parser<T1> parser, 
    string label, 
    Parser<T2> and
) => ...;

// Usage:
var parser = Terms.Text("name").And("value", Terms.Integer());
// Allows getting named values when using .AsDictionary()
```

### 13. ParsersPlus Extensions

**Paspan** adds additional convenience methods:

```csharp
// Strings and characters as parsers
public static Parser<Unit> Skip(this char ch) => Literals.Char(ch);
public static Parser<string> Skip(this string str) => Literals.Text(str);

// String combinations
public static Parser<string> Or(this string str, string str2) => ...;
public static Parser<Unit> Or(this char ch, char ch2) => ...;

// Conversion to dictionaries
public static Dictionary<string, object> AsDictionary(this object s) => ...;
public static Dictionary<string, object> AsDictionary(this (object, object) s) => ...;
```

### 14. ExitParser and ISeekable in Combinators

**Paspan** doesn't use `context.ExitParser(this)` and `ISeekable` in combinator parsers.

**Combinator parsers (Sequence, OneOf, Between, When, WhenFollowedBy, etc.):**
```csharp
public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
{
    context.EnterParser(this);  // ✅ Used
    
    // ... parsing logic ...
    
    // ❌ NOT using context.ExitParser(this)
    return success;
}
```

**Literal parsers (CharLiteral, TextLiteral, NumberLiteral, etc.):**
```csharp
public sealed class TextLiteral : Parser<string>, ISeekable  // ✅ Only literals implement ISeekable
{
    public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<string> result)
    {
        context.EnterParser(this);
        
        if (reader.Skip(_textBytes))
        {
            result.Set(Encoding.UTF8.GetString(_textBytes));
            context.ExitParser(this);  // ✅ Only literals use ExitParser
            return true;
        }
        
        context.ExitParser(this);
        return false;
    }
}
```

**Parlot** uses `context.ExitParser(this)` and `ISeekable` in all parsers:
```csharp
public override bool Parse(ParseContext context, ref ParseResult<T> result)
{
    context.EnterParser(this);
    
    // ... parsing logic ...
    
    context.ExitParser(this);  // ✅ Used everywhere
    return success;
}
```

---

## Parser Operation Differences

### 15. Text Parsing

**Paspan:**
```csharp
public sealed class TextLiteral : Parser<string>
{
    private readonly byte[] _textBytes;
    
    public TextLiteral(string text)
    {
        _textBytes = Encoding.UTF8.GetBytes(text);
    }
    
    public override bool Parse(ref SpanReader reader, ParseContext context, 
                               ref ParseResult<string> result)
    {
        if (reader.Skip(_textBytes))
        {
            // Search happens at byte level!
            result.Set(Encoding.UTF8.GetString(_textBytes));
            return true;
        }
        return false;
    }
}
```

Key difference: text is converted to UTF-8 bytes at parser creation, and search happens **at byte level without converting input data to string**.

**Parlot** works directly with characters in string.

### 16. Character Parsing

**Paspan:**
```csharp
public sealed class CharLiteral : Parser<Unit>
{
    private readonly byte[] CharBytes;
    
    public CharLiteral(char c)
    {
        CharBytes = Encoding.UTF8.GetBytes(new[] { c });
    }
    
    public override bool Parse(ref SpanReader reader, ParseContext context, 
                               ref ParseResult<Unit> result)
    {
        if (reader.Skip(CharBytes))
        {
            result.Set(Unit.Value);
            return true;
        }
        return false;
    }
}
```

Character is converted to UTF-8 bytes. Multi-byte characters (emoji, Cyrillic) are handled correctly.

### 17. Pattern with Bytes

**Paspan:**
```csharp
public Parser<Unit> Pattern(Func<byte, bool> predicate, int minSize = 1, int maxSize = 0)
```

Predicate works with **bytes**, not characters!

```csharp
// Example: parse hex digits at byte level
var hex = Literals.Pattern(b => Character.IsHexDigit(b));
```

**Parlot:**
```csharp
public Parser<TextSpan> Pattern(Func<char, bool> predicate, int minSize = 1, int maxSize = 0)
```

Predicate works with characters.

### 18. NumberOptions

**Paspan** adds options for number parsing:

```csharp
[Flags]
public enum NumberOptions
{
    Default = 0,
    AllowSign = 1,
    // Other options...
}

public Parser<long> Integer(NumberOptions numberOptions = NumberOptions.Default) => ...;
public Parser<decimal> Decimal(NumberOptions numberOptions = NumberOptions.Default) => ...;
```

**Parlot** doesn't have such configuration.

### 19. Parse Methods

**Paspan:**
```csharp
// Parse string
public T? Parse(string text, ParseContext? context = null)

// Parse bytes
public T? Parse(byte[] text, ParseContext? context = null)

// Parse via SpanReader (ref!)
public T? Parse(ref SpanReader reader, ParseContext? context = null)

// TryParse with errors
public bool TryParse(string text, out T? value, out ParseError? error)
public bool TryParse(byte[] text, out T? value, out ParseError? error)
public bool TryParse(ref SpanReader reader, out T? value, out ParseError? error)
```

**Parlot:**
```csharp
public T Parse(string text)
public bool TryParse(string text, out T value)
```

---

## Removed Features

### 20. Compile() Method

**Parlot** has a `Compile()` method that compiles parser to delegate for improved performance.

**Paspan** **removed** this functionality. Plans to replace with Source Generators.

### 21. Scanner Features

**Parlot** `Scanner` has rich set of methods for working with positions and cursors.

**Paspan** `SpanReader` is optimized for span work and has minimalist API:
- `CaptureState()` / `RollBackState()`
- `GetCurrentPosition()` / `GetPosition()`
- Direct work with `_consumed` offset

---

## Performance and Optimizations

### 22. Zero-allocation Parsing

**Paspan:**
- `SpanReader` - `ref struct`, not allocated in heap
- `ParseResult<T>` - `ref struct`, not allocated in heap
- `Region` - `struct`, can be used without string allocation
- Text literals are converted to bytes once at parser creation

**Parlot:**
- Works with `string`, which are already in heap
- Fewer zero-allocation optimizations

### 23. UTF-8 Optimizations

**Paspan:**
```csharp
// Search for "hello" in UTF-8 data
// Literal stores: byte[] { 0x68, 0x65, 0x6C, 0x6C, 0x6F }
// Search: memcmp bytes without decoding
var parser = Literals.Text("hello");
```

For ASCII and simple UTF-8 strings — significantly faster than char-based search.

**Parlot** works with already decoded strings.

### 24. Large Files

**Paspan** is optimized for parsing huge files:
- Support for `ReadOnlySequence<byte>` for streaming
- Minimal allocations
- Direct work with memory-mapped files

**Parlot** is oriented towards strings in memory.

---

## Known Issues and Limitations

### 25. Region Performance

From Paspan documentation:
> Performance issues when creating `Region` (see WideJson_PaspanUtf8Region benchmarks)

With frequent `Region` creation and string conversion, performance may be lower than expected.

### 26. Non-ASCII Character Handling

**Paspan** `Pattern` works with bytes, which complicates work with multi-byte UTF-8 sequences:

```csharp
// In Parlot you can do this:
var russianLetter = Literals.Pattern(c => c >= 'а' && c <= 'я');

// In Paspan you need to work with UTF-8 bytes:
// Cyrillic 'а' = 0xD0 0xB0, 'я' = 0xD1 0x8F
// Pattern is not suitable for this, need different approach
```

For Unicode work, need to use `Identifier` or convert to string.

---

## Migration Examples

### Example 1: Simple Parser

**Parlot:**
```csharp
var parser = Terms.Char('/');  // Parser<char>
var result = parser.Parse("/ test");
// result = '/'
```

**Paspan:**
```csharp
var parser = Terms.Char('/').AsChar();  // Parser<char>
var result = parser.Parse("/ test");
// result = '/'
```

### Example 2: Parsing with Skip

**Parlot:**
```csharp
var parser = Terms.Identifier()
    .AndSkip(Terms.Char('='))
    .And(Terms.Integer());
```

**Paspan:**
```csharp
var parser = Terms.Identifier()
    .Skip('=')  // Simplified version
    .And(Terms.Integer());
```

### Example 3: Working with Regions

**Paspan (zero-allocation):**
```csharp
var parser = Terms.StringRegion();
var result = parser.Parse("\"hello world\"");
// result: Region { Start = 1, End = 12, Length = 11 }
// String not created! Only coordinates.

// Later can get string:
var str = result.ToString(originalBytes);
```

**Parlot (always creates string):**
```csharp
var parser = Terms.String();
var result = parser.Parse("\"hello world\"");
// result: TextSpan contains ReadOnlySpan<char>
```

---

## Selection Recommendations

**Use Paspan if:**
- Parsing large UTF-8 files (logs, JSON, CSV)
- Parsing binary data
- Need maximum performance and minimal allocations
- Working with `Stream`, `PipeReader`, memory-mapped files
- Ready to work at byte level

**Use Parlot if:**
- Parsing strings in memory
- Working with Unicode/multilingual text
- Need `Compile()` function for maximum speed
- Need simpler API without conversions via `AsChar()`, etc.

---

## Conclusion

Main differences between Paspan and Parlot:

1. **Base type**: `Span<byte>` instead of `string`
2. **SpanReader** instead of **Scanner**
3. **Unit type** for parsers without value
4. **Helper methods**: `AsChar()`, `AsHex()`, `AsString()`, `AsObject()`
5. **Region** instead of **TextSpan** for zero-copy
6. **UTF-8 optimizations**: byte-level search
7. **Removed Compile()**, Source Generators planned
8. **Extended API**: `Skip(char)`, `Skip(string)`, `Labelled`, `AsDictionary()`
9. **ExitParser and ISeekable**: combinators don't use `context.ExitParser(this)`, `ISeekable` only for literals

Paspan is a specialized version of Parlot for high-performance UTF-8 and binary data parsing with minimal allocations.
