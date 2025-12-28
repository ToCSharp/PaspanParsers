# Migration Guide: Parsers from Parlot to Paspan

## Overview

This document contains practical instructions for fixing tests and parsers from Parlot to work with Paspan, based on real fixes in the codebase.

---

## Main Migration Rules

### 1. Character Parsers: `Char()` Returns `Unit`, Not `char`

**Problem:**
In Parlot `Literals.Char('a')` returns `Parser<char>`, in Paspan - `Parser<Unit>`.

**Solution:**
Use `.AsChar()` to get character value:

```csharp
// Parlot:
var parser = Terms.Char('a');  // Parser<char>
var result = parser.Parse("a");  // result = 'a'

// Paspan:
var parser = Terms.Char('a').AsChar();  // Parser<char>
var result = parser.Parse("a");  // result = 'a'
```

**Fix Examples:**

```csharp
// ❌ Wrong (returns "Paspan.Fluent.Unit"):
var parser = Terms.Char('a').And(Terms.Char('b'));
var result = parser.Parse("ab");
Console.WriteLine(result.Item1.ToString());  // "Paspan.Fluent.Unit"

// ✅ Correct:
var parser = Terms.Char('a').AsChar().And(Terms.Char('b').AsChar());
var result = parser.Parse("ab");
Console.WriteLine(result.Item1.ToString());  // "a"
```

---

### 2. Pattern Parsers: Return `Unit`, Not `TextSpan`

**Problem:**
`Pattern()` in Paspan returns `Parser<Unit>`, need string.

**Solution:**
Use `.AsString()` to get recognized text:

```csharp
// Parlot:
var hex = Terms.Pattern(c => Character.IsHexDigit(c));
var result = hex.Parse("abc");
Console.WriteLine(result.ToString());  // "abc"

// Paspan:
var hex = Terms.Pattern(c => Character.IsHexDigit(c)).AsString();
var result = hex.Parse("abc");
Console.WriteLine(result);  // "abc"
```

**Important:** Predicate in Paspan works with `byte`, not `char`:

```csharp
// Parlot:
var parser = Literals.Pattern(c => c >= 'a' && c <= 'z');  // Func<char, bool>

// Paspan:
var parser = Literals.Pattern(b => b >= 97 && b <= 122);  // Func<byte, bool>
// Or use Character.IsLetter(b) to work with bytes
```

---

### 3. NoneOf/AnyOf: Return `Region`, Not `TextSpan`

**Problem:**
`NoneOf()` and `AnyOf()` return `Parser<Region>`, not `Parser<TextSpan>`.

**Solution:**
Use `.AsString()` to get string value:

```csharp
// Parlot:
var parser = Literals.NoneOf("abc");
var result = parser.Parse("xyz");
Console.WriteLine(result.ToString());  // "xyz"

// Paspan:
var parser = Literals.NoneOf("abc").AsString();
var result = parser.Parse("xyz");
Console.WriteLine(result);  // "xyz"
```

**Explanation:**
- `Region` is a struct with `Start`, `End`, `Length` fields
- `Region.ToString()` returns `"Region 0..3"`, not contents
- To get string use `.AsString()` on parser (not on result!)

---

### 4. Sequences with `Unit` Types

**Problem:**
When using `And()` with `Unit` parsers, result will contain `Unit` objects.

**Solution:**
Convert each parser to needed type before combining:

```csharp
// ❌ Wrong:
var parser = Terms.Char('a').And(Terms.Char('b')).And(Terms.Char('c'));
// parser has type Parser<(Unit, Unit, Unit)>

// ✅ Correct:
var parser = Terms.Char('a').AsChar()
    .And(Terms.Char('b').AsChar())
    .And(Terms.Char('c').AsChar());
// parser has type Parser<(char, char, char)>
```

**Real Example from Tests:**

```csharp
// Parlot:
var choice = OneOf(
    Literals.Char('a').And(Literals.Char('b')).And(Literals.Char('c')),
    Literals.Char('a').And(Literals.Char('b')).And(Literals.Char('e'))
).Then(x => x.Item1.ToString() + x.Item2.ToString() + x.Item3.ToString());

// Paspan:
var choice = OneOf(
    Literals.Char('a').AsChar().And(Literals.Char('b').AsChar()).And(Literals.Char('c').AsChar()),
    Literals.Char('a').AsChar().And(Literals.Char('b').AsChar()).And(Literals.Char('e').AsChar())
).Then(x => x.Item1.ToString() + x.Item2.ToString() + x.Item3.ToString());
```

---

### 5. Working with Numbers Remains Similar

**No Changes:**
```csharp
// Parlot and Paspan identical:
var parser = Terms.Integer();
var result = parser.Parse("123");  // result = 123L
```

---

### 6. String Literals Remain Similar

**No Changes:**
```csharp
// Parlot and Paspan identical:
var parser = Terms.String();
var result = parser.Parse("\"hello\"");  // result = "hello"
```

---

## New Paspan Features

### 1. Zero-copy Parsing with `Region`

```csharp
// Get only coordinates without creating string:
var parser = Terms.StringRegion();
var region = parser.Parse("\"hello world\"");
// region: Region { Start = 1, End = 12, Length = 11 }

// Later can get string:
var str = region.ToString(originalBytes);
```

### 2. Special Conversion Methods

```csharp
// AsChar() - to get character
var charParser = Literals.Char('a').AsChar();  // Parser<char>

// AsHex() - to get hex number
var hexParser = Literals.Pattern(c => Character.IsHexDigit(c)).AsHex();  // Parser<ulong>

// AsString() - to get string
var strParser = Literals.Pattern(c => c > 32).AsString();  // Parser<string>

// AsObject() - to get object
var objParser = Terms.Integer().AsObject();  // Parser<object>
```

---

## Typical Migration Mistakes

### Mistake 1: Using `.ToString()` on Parser Result

```csharp
// ❌ Wrong:
var parser = Literals.NoneOf("abc");
var result = parser.Parse("xyz");
Console.WriteLine(result.ToString());  // "Region 0..3" - not what we need!

// ✅ Correct:
var parser = Literals.NoneOf("abc").AsString();
var result = parser.Parse("xyz");
Console.WriteLine(result);  // "xyz"
```

### Mistake 2: Forgotten `.AsChar()` in Sequences

```csharp
// ❌ Wrong:
var parser = Terms.Char('a').And(Terms.Char('b'));
var (a, b) = parser.Parse("ab");
Console.WriteLine(a);  // "Paspan.Fluent.Unit"

// ✅ Correct:
var parser = Terms.Char('a').AsChar().And(Terms.Char('b').AsChar());
var (a, b) = parser.Parse("ab");
Console.WriteLine(a);  // 'a'
```

### Mistake 3: Pattern Predicate with char Instead of byte

```csharp
// ❌ Wrong (compilation error):
var parser = Literals.Pattern(c => c >= 'a' && c <= 'z');  // char doesn't convert to byte

// ✅ Correct:
var parser = Literals.Pattern(b => b >= 97 && b <= 122);  // byte ASCII codes

// ✅ Even better:
var parser = Literals.Pattern(b => Character.IsLetter(b));  // Use Character class
```

---

## Test Migration Checklist

When porting a test from Parlot to Paspan:

- [ ] Replace `Literals.Char('x')` with `Literals.Char('x').AsChar()` where need `char`
- [ ] Replace `Terms.Pattern(...)` with `Terms.Pattern(...).AsString()` where need string
- [ ] Replace `Literals.NoneOf(...)` with `Literals.NoneOf(...).AsString()` where need string
- [ ] Check `Pattern` predicates - they should work with `byte`, not `char`
- [ ] Remove `.ToString()` on `Region` parser results
- [ ] Check `And()` sequences - ensure all elements are properly converted
- [ ] Check `OneOf` - ensure all branches return same type
- [ ] Check case-insensitive parsing - now supported for ASCII letters

---

## Known Limitations

### 1. ✅ Case-insensitive Parsing (IMPLEMENTED)

```csharp
// In Parlot:
var parser = Literals.Text("hello", StringComparison.OrdinalIgnoreCase);

// In Paspan (now works!):
var parser = Literals.Text("hello", caseInsensitive: true);
// or
var parser = Terms.Text("hello", caseInsensitive: true);

// Usage examples:
var parser = Literals.Text("not", caseInsensitive: true);
Assert.Equal("not", parser.Parse("not"));   // ✅
Assert.Equal("nOt", parser.Parse("nOt"));   // ✅
Assert.Equal("NOT", parser.Parse("NOT"));   // ✅
```

**Implementation:**
- Supports `StringComparison.OrdinalIgnoreCase`
- Works only for ASCII letters (A-Z, a-z)
- Returns actually read text preserving casing from input data
- Optimized for performance with `AggressiveInlining`

### 2. Custom Number Separators ✅ **IMPLEMENTED**

```csharp
// ✅ Works with custom separators:
var parser1 = Literals.Number<decimal>(NumberOptions.Any, decimalSeparator: (byte)'|');
Assert.Equal(123.456m, parser1.Parse("123|456"));

var parser2 = Literals.Number<decimal>(NumberOptions.Any, (byte)'.', groupSeparator: (byte)'|');
Assert.Equal(123456m, parser2.Parse("123|456"));
```

**Note:** In constructor separators are passed as `byte`, not `char` (Paspan works with UTF-8 bytes).

### 3. Working with Multi-byte UTF-8 Characters in Pattern

```csharp
// Difficult in Paspan as Pattern works with bytes:
// Cyrillic 'а' = 0xD0 0xB0 (2 bytes)
// For Unicode better use Identifier or String parsers
```

---

## Links

- [Complete API differences documentation](fluent-api-differences.md)
- [Migration examples in tests](../src/Paspan.Tests/FluentTests.cs)
- [Values.cs implementation](../src/PaspanCommon/Fluent/Values.cs)

---

