# C++ Parser

A C++23 parser for valid code, checked against clang. **Work in progress:** it is being built by stages
following [the plan](../../../docs/cpp-parser-clang-level-plan.md), and today it parses only a small subset
of the language (see [Support](#support)).

Like the C# parser, it is a hand-written recursive descent parser (`SyntaxParser`) over lazily scanned,
cached tokens, and builds a semantic AST whose nodes have positions (`Span`). `CppWriter` prints the AST
back as C++.

Macros are not expanded and `#include` is not read: the parser works on a single file, as written.
Conditional directives will be evaluated with the macros given in `CppParseOptions.Macros` (stage 2).

## Usage

```csharp
using PaspanParsers;
using PaspanParsers.Cpp;

var source = "int square(int x) { return x * x; }";
if (CppParser.TryParse(source, out var unit, out var error))
{
    var function = (FunctionDefinition)unit.Declarations[0];
    Console.WriteLine(function.Body.Span.GetText(source));    // { return x * x; }

    var writer = new CppWriter();
    writer.WriteTranslationUnit(unit);
    Console.WriteLine(writer.GetResult());
}
else
{
    Console.WriteLine($"({error.Line},{error.Column}): {error.Message}");
}
```

Spans are offsets in UTF-8 bytes of the input without its byte order mark, from the first token of a
node to the end of its last token; the translation unit spans the whole input. `TryParse` also accepts the
UTF-8 bytes of a file, and `LineMap(utf8, unicodeLineBreaks: false)` converts offsets to lines and columns.

## Support

| Area | Status |
|---|---|
| Lexical | Tokens of C++23: punctuators (longest first, digraphs, alternative tokens, `<::`), numbers, character and string literals with encoding prefixes, raw strings and user-defined suffixes, comments and line splices |
| Declarations | Function definitions and simple declarations with keyword specifiers (`int`, `const`, `static`, ...), function declarators, parameters with default values, `=` initializers |
| Statements | Compound, declaration, expression, null, `if`/`else`, `while`, `return` |
| Expressions | Literals, names, parentheses, all binary operators with C++ precedence, `?:`, assignments, prefix and postfix operators, calls |
| Preprocessor, types, declarators, classes, templates, ... | Not yet |

## Checking against clang

`src/PaspanParsers.Tests/Cpp` runs clang as an oracle (see `CLAUDE.md`). For each valid file of the corpus
the parser must succeed, the printed tree must compile to the same clang AST, and every node must have
the span and a matching kind of a clang node (`CppKindMap.cs`), so that, for example, `a * b;` read as a
declaration instead of a multiplication is caught even though it prints the same.

## Files

| File | Contents |
|---|---|
| `CppParser.cs` | Entry points: `Parse`, `TryParse`, `TranslationUnitParser` |
| `CppParseOptions.cs`, `CppParseContext.cs` | Options (language version, macros) and per-parse state |
| `CppAst.cs` | AST nodes |
| `CppWriter.cs` | Prints an AST as C++ |
| `Parser/Lexer.cs`, `Parser/SyntaxToken.cs` | Tokens and trivia |
| `Parser/SyntaxParser*.cs` | Recursive descent parser: declarations, statements, expressions |
