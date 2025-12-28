# Integration Tests Plan for Paspan

## Current Project State Analysis

### Project Structure
- `src/Paspan` - main library with `SpanReader`
- `src/PaspanCommon` - shared code with parsers and fluent API
- `test/Paspan.Tests` - unit tests (JSON, calculator, fluent API, SpanReader)
- `test/Paspan.Benchmarks` - performance benchmarks

### Current Test Coverage
- ✅ Unit tests for individual parsers
- ✅ Tests for combinators (And, Or, Then, etc.)
- ✅ Tests for SpanReader (reading strings, numbers, escape sequences)
- ✅ Examples of JSON and mathematical expression parsing
- ❌ Integration tests for end-to-end scenarios
- ❌ Tests for large files and edge cases
- ❌ Error handling tests in complex grammars

## Proposed Integration Test Structure

```
test/Paspan.IntegrationTests/
├── Paspan.IntegrationTests.csproj
├── TestData/                          # Test data files
│   ├── json/
│   │   ├── large_file.json           # Large JSON (>1MB)
│   │   ├── deeply_nested.json        # Deeply nested JSON
│   │   ├── malformed.json            # Malformed JSON for error tests
│   │   └── unicode_heavy.json        # JSON with unicode characters
│   ├── expressions/
│   │   ├── complex_math.txt          # Complex mathematical expressions
│   │   └── edge_cases.txt
│   └── custom/
│       ├── csv_samples.csv
│       └── log_samples.log
├── Infrastructure/
│   ├── TestDataLoader.cs             # Load test data
│   ├── PerformanceMonitor.cs         # Performance monitoring
│   └── TestFixtures.cs               # Common fixtures
├── EndToEnd/
│   ├── JsonParsingTests.cs           # E2E JSON parsing tests
│   ├── ExpressionEvaluationTests.cs  # E2E calculator tests
│   └── CustomGrammarTests.cs         # Custom grammars
├── RealWorld/
│   ├── CsvParserTests.cs             # Real CSV parser
│   ├── LogParserTests.cs             # Log parser
│   ├── ConfigParserTests.cs          # Config parser (INI, TOML)
│   └── MarkdownParserTests.cs        # Simple Markdown parser
├── ErrorHandling/
│   ├── ComplexErrorRecoveryTests.cs  # Error recovery
│   ├── ErrorMessageTests.cs          # Error message quality
│   └── PartialParsingTests.cs        # Partial parsing
├── Performance/
│   ├── LargeFileTests.cs             # Large file tests
│   ├── MemoryUsageTests.cs           # Memory consumption
│   └── StreamingTests.cs             # Streaming parsing
└── EdgeCases/
    ├── EncodingTests.cs              # UTF-8, UTF-16, binary
    ├── BoundaryTests.cs              # Boundary cases
    └── ConcurrencyTests.cs           # Concurrency (if applicable)
```

## Integration Test Categories

### 1. End-to-End Tests (EndToEnd/)

**Goal:** Verify complete grammars work from start to finish

#### JsonParsingTests.cs
- Parse real JSON files of various sizes
- Compare results with System.Text.Json
- Verify JSON can be reconstructed from AST
- Parse JSON with different encodings (UTF-8, UTF-16)
- Work with Region-based parsers

#### ExpressionEvaluationTests.cs
- Parse and evaluate complex mathematical expressions
- Verify operator precedence in complex cases
- Combinations of unary and binary operators
- Deeply nested parentheses (100+ levels)
- Expressions with many operations (1000+)

#### CustomGrammarTests.cs
- Create and test custom grammars
- Combine different parsers
- Test recursive grammars
- Circular parser dependencies

### 2. Real-World Scenarios (RealWorld/)

#### CsvParserTests.cs
CSV parser with support for:
- Escaped quotes
- Multiline fields
- Different delimiters
- Headers
- Empty fields

#### LogParserTests.cs
Parse logs of various formats:
- Apache/Nginx access logs
- Application logs (timestamp, level, message)
- Structured logs (JSON)
- Multi-line stack traces

#### ConfigParserTests.cs
Configuration file parsers:
- INI format
- Simple TOML subset
- Key-value pairs
- Sections and subsections

#### MarkdownParserTests.cs
Simplified Markdown parser:
- Headers (# ## ###)
- Bold, italic, code
- Lists
- Links
- Code blocks

### 3. Error Handling (ErrorHandling/)

#### ComplexErrorRecoveryTests.cs
- Recovery after errors mid-parsing
- Multiple errors in one document
- Error messages with correct position
- ElseError in complex grammars

#### ErrorMessageTests.cs
- Error message quality and readability
- Error context (position, line, column)
- Error localization in large files

#### PartialParsingTests.cs
- Partial successful parsing on errors
- Skip incorrect sections
- Maximum data extraction from corrupted files

### 4. Performance and Scalability (Performance/)

#### LargeFileTests.cs
- Files sized 1MB, 10MB, 100MB
- JSON arrays with thousands of elements
- Deeply nested structures (1000+ levels)
- Measure parsing time
- Compare with benchmarks

#### MemoryUsageTests.cs
- Monitor memory consumption
- No memory leaks
- GC pressure
- Stack vs heap usage

#### StreamingTests.cs
- Parse from streams
- ReadOnlySequence for large data
- Chunked parsing
- Parse files larger than RAM

### 5. Edge Cases (EdgeCases/)

#### EncodingTests.cs
- UTF-8 with and without BOM
- UTF-16 LE/BE
- ASCII
- Invalid UTF-8 sequences
- Binary data

#### BoundaryTests.cs
- Empty files
- Single character files
- Maximum number sizes (Int64.MaxValue)
- Very long strings (>1GB)
- Special characters and control codes

#### ConcurrencyTests.cs
- Parallel parsing (if supported)
- Thread-safety of parsers
- Concurrent access to readonly data

## Supporting Infrastructure

### TestDataLoader.cs

```csharp
public static class TestDataLoader
{
    public static string LoadText(string fileName);
    public static byte[] LoadBytes(string fileName);
    public static ReadOnlySpan<byte> LoadSpan(string fileName);
    public static IEnumerable<string> LoadLines(string fileName);
}
```

### PerformanceMonitor.cs

```csharp
public class PerformanceMonitor : IDisposable
{
    public TimeSpan Elapsed { get; }
    public long MemoryUsed { get; }
    public void AssertFasterThan(TimeSpan expected);
    public void AssertMemoryLessThan(long bytes);
}
```

## Concrete Test Examples

### JSON E2E Test

```csharp
[Fact]
public void ShouldParseAndRoundTripLargeJsonFile()
{
    // Arrange
    var originalJson = TestDataLoader.LoadText("large_file.json");
    
    // Act
    var parsed = JsonParser.Parse(originalJson);
    var regenerated = parsed.ToString();
    
    // Assert
    Assert.NotNull(parsed);
    JsonAssert.AreEquivalent(originalJson, regenerated);
}
```

### CSV Parser Test

```csharp
[Theory]
[InlineData("simple.csv", 100, 5)]
[InlineData("with_quotes.csv", 50, 3)]
[InlineData("multiline.csv", 25, 4)]
public void ShouldParseCsvCorrectly(string file, int expectedRows, int expectedCols)
{
    var csv = TestDataLoader.LoadText(file);
    var result = CsvParser.Parse(csv);
    
    Assert.Equal(expectedRows, result.Rows.Count);
    Assert.All(result.Rows, row => Assert.Equal(expectedCols, row.Count));
}
```

### Error Handling Test

```csharp
[Fact]
public void ShouldProvideHelpfulErrorMessageForMalformedJson()
{
    var json = "{\"key\": [1, 2, 3}"; // Missing closing bracket
    
    var success = JsonParser.TryParse(json, out var result, out var error);
    
    Assert.False(success);
    Assert.NotNull(error);
    Assert.Contains("Expected ']'", error.Message);
    Assert.Equal(1, error.Position.Line);
    Assert.Equal(17, error.Position.Column);
}
```

### Performance Test

```csharp
[Fact]
public void ShouldParseLargeJsonFileInReasonableTime()
{
    var json = TestDataLoader.LoadText("large_file.json"); // 10MB
    
    using var monitor = new PerformanceMonitor();
    var result = JsonParser.Parse(json);
    
    monitor.AssertFasterThan(TimeSpan.FromSeconds(1));
    monitor.AssertMemoryLessThan(50 * 1024 * 1024); // 50MB
}
```

## CI/CD Integration

### .csproj Configuration

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <IsTestProject>true</IsTestProject>
  <Category>Integration</Category>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
  <PackageReference Include="xunit" Version="2.9.2" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
</ItemGroup>

<ItemGroup>
  <ProjectReference Include="..\..\src\Paspan\Paspan.csproj" />
</ItemGroup>

<!-- Copy test data -->
<ItemGroup>
  <None Update="TestData\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

### Test Categories

```csharp
[Trait("Category", "Integration")]
[Trait("Speed", "Slow")]
public class LargeFileTests { }

[Trait("Category", "Integration")]
[Trait("Speed", "Fast")]
public class ErrorHandlingTests { }
```

### Running Tests

```bash
# All integration tests
dotnet test --filter Category=Integration

# Only fast tests
dotnet test --filter "Category=Integration&Speed=Fast"

# Only slow tests (for CI)
dotnet test --filter "Category=Integration&Speed=Slow"

# Specific category
dotnet test --filter FullyQualifiedName~RealWorld
```

## Metrics and Coverage

### Target Metrics for Integration Tests

- Coverage of main use cases: >90%
- Testing all public APIs in real scenarios
- Minimum 3 real-world parsers (CSV, Log, Config)
- Tests for files >1MB for each format
- >50 error handling tests
- Benchmark comparisons with alternatives (Pidgin, Sprache, Parlot)

### Measurable Indicators

1. **Correctness**
   - Percentage of successfully parsed valid files
   - Correct handling of invalid data
   - Error position accuracy

2. **Performance**
   - Parse time per 1MB of data
   - Memory consumption per 1MB of data
   - Allocation count
   - GC collections

3. **Reliability**
   - No crashes on invalid data
   - Behavior predictability
   - Result stability

## Implementation Prioritization

### Phase 1 (High Priority)

1. ✅ Create Paspan.IntegrationTests project structure
2. ✅ Infrastructure (TestDataLoader, PerformanceMonitor)
3. ✅ JSON E2E tests (extend existing)
4. ✅ Expression evaluation E2E tests
5. ✅ Basic error handling tests

**Time estimate:** 1-2 weeks  
**Priority:** Critical  
**Goal:** Basic coverage of main scenarios

### Phase 2 (Medium Priority)

6. CSV parser and tests
7. Log parser and tests
8. Performance tests for large files
9. Encoding tests (UTF-8, UTF-16, binary)
10. Boundary tests

**Time estimate:** 2-3 weeks  
**Priority:** High  
**Goal:** Real-world applicability

### Phase 3 (Low Priority)

11. Config parsers (INI, TOML)
12. Markdown parser
13. Concurrency tests
14. Streaming tests for very large files
15. Advanced error recovery

**Time estimate:** 2-4 weeks  
**Priority:** Desirable  
**Goal:** Complete coverage and additional examples

## Expected Results

### Quality Improvement

1. **Identify integration issues** between components
2. **Confirm functionality** with real data
3. **Discover edge cases** not covered by unit tests
4. **Validate performance** on large volumes

### Documentation Improvement

1. **Real usage examples** for users
2. **Best practices** for grammar creation
3. **Demonstrate library capabilities**
4. **Collection of typical parsers** (CSV, Log, Config)

### Increase Confidence

1. **Proof of functionality** in production scenarios
2. **Comparison with alternatives** (Parlot, Pidgin, Sprache)
3. **Quality metrics** (coverage, performance)
4. **Regression testing** on changes

## Next Steps

1. Create `test/Paspan.IntegrationTests` project
2. Implement basic infrastructure
3. Prepare test data
4. Start with Phase 1 (high-priority tests)
5. Integrate into CI/CD pipeline
6. Document results and examples

---

**Created:** December 2025  
**Status:** Plan for implementation  
**Author:** Plan created based on analysis of current Paspan project structure
