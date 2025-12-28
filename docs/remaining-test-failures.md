# Remaining Test Failures in Paspan

## Status: 113 passed, 0 failed (out of 113 FluentTests) ✅

---

## 🎉 ALL TESTS PASSING!

All 113 FluentTests tests are successfully passing!

## Fixed Issues

### 1. Custom Number Separators (2 tests) ✅
**Fix:**
Problem was incorrect `byte` to string conversion in `NumberLiteral.cs`:
```csharp
// Was:
_culture.NumberFormat.NumberDecimalSeparator = decimalSeparator.ToString();

// Now:
_culture.NumberFormat.NumberDecimalSeparator = ((char)decimalSeparator).ToString();
```

### 2. WhiteSpace Parser (2 tests) ✅
**Fix:**
1. `WhiteSpaceLiteral` now correctly creates `Region` with `(start, length)` instead of `(start, end)`
2. `Terms.WhiteSpace()` now uses `WhiteSpaceParser`, which checks `ParseContext.WhiteSpaceParser`
3. Fixed `Region` constructor in all places:
   - `Capture.cs` - `new Region(s, end - s)` instead of `new Region(s, end)`
   - `RegionBefore.cs` - similarly
   - `StringRegion.cs` - similarly

### 3. Keyword Matching (1 test) ✅
Fixed by adding word boundary check in `Keyword.cs`.

### 4. Recursive Parser (1 test) ✅
Fixed in previous commits.

### 5. Region Size Constraints (1 test) ✅
Fixed in previous commits.

### 6. Case-Insensitive Parsing (2 tests) ✅
Implemented `StringComparison.OrdinalIgnoreCase` support in `TextLiteral`.

---

## Summary

**Progress: 113/113 tests (100%)**

All FluentTests tests pass successfully!

### Main Fixes in This Session:
- ✅ Case-insensitive parsing (2 tests)
- ✅ Custom number separators (2 tests)  
- ✅ WhiteSpace parser configuration (2 tests)
- ✅ Keyword matching (1 test)
- ✅ Recursive parser (1 test)
- ✅ Region size constraints (1 test)
- ✅ Region constructor fixes (multiple places)

### Key Changes:

1. **`Region` struct**: Constructor takes `(start, length)`, not `(start, end)`
2. **`WhiteSpaceParser`**: Uses custom parser from `ParseContext.WhiteSpaceParser`
3. **`NumberLiteral`**: Correct `byte` to `char` conversion for separators
4. **`TextLiteral`**: Case-insensitive comparison support at byte level
5. **`Keyword`**: Word boundary check after keyword
