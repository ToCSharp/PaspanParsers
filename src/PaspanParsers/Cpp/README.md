# C++ Parser

A C++23 parser for valid code, checked against clang. **Work in progress:** it is being built by stages
following [the plan](../../../docs/cpp-parser-clang-level-plan.md), and today it parses only a small subset
of the language (see [Support](#support)).

Like the C# parser, it is a hand-written recursive descent parser (`SyntaxParser`) over lazily scanned,
cached tokens, and builds a semantic AST whose nodes have positions (`Span`). `CppWriter` prints the AST
back as C++.

Macros are not expanded and `#include` is not read: the parser works on a single file, as written, and
sees the names of macros where the code uses them. Directives are trivia (see [Preprocessor](#preprocessor)).

Limitations of the lexer: characters named with `\N{...}` are not decoded (.NET has no table of Unicode
character names), so literals with them have no value; a line splice inside the `//` or `/*` that starts a
comment is not supported.

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

## Preprocessor

Before the first token is scanned, one pass over the file processes the directives in source order, so
that the lazily scanned tokens can skip directives and inactive branches in any order.

- A directive starts with `#` or `%:` that is the first token of a line, and ends at the end of the logical
  line; a block comment inside it may span lines.
- `#if`, `#ifdef`, `#ifndef`, `#elif`, `#elifdef`, `#elifndef`, `#else` and `#endif` choose the branches.
  Conditions are evaluated with the macros of `CppParseOptions.Macros` (the predefined macros of a
  compiler and `-D` options; `F(x)` as a name defines a function-like macro) and those of the `#define`
  and `#undef` before them. Macros are expanded in conditions: object-like and function-like, `#`, `##`,
  `__VA_ARGS__`, `__VA_OPT__`; `defined`, `__LINE__` and `__COUNTER__` work. Values are `intmax_t` or
  `uintmax_t`, other names are 0.
- `__has_include` looks for the header in `CppParseOptions.SourceDirectory` (quoted names) and
  `IncludeDirectories`. `__has_cpp_attribute` knows the standard attributes; `__has_builtin`,
  `__has_feature`, `__has_extension` and `__has_attribute` use tables and name patterns that approximate
  clang; `__has_c_attribute`, `__has_declspec_attribute`, `__building_module` and `__has_embed` are 0.
- `TranslationUnit.Directives` lists every processed directive (`PreprocessorDirective`: kind, name,
  arguments, source text, `IsBranchTaken`, and `Macro` for `#define`). Directives inside inactive
  branches are not processed.
- The other directives are kept for the writer as `LeadingDirectives` of the nodes that start at the next
  token, as `CompoundStatement.CloseBraceDirectives` before a `}` and as `TranslationUnit.EndDirectives`.
  `CppWriter` prints them as written, on their own lines. Conditional directives are not printed: the tree
  holds only the active code. A directive before a token that does not start a node, other than a
  closing brace or the end of the file (`a\n#define X\n+ b`), is only in `TranslationUnit.Directives`.
- Macros defined in included headers are unknown, since headers are not read.

## Support

| Area | Status |
|---|---|
| Lexical | Tokens of C++23: punctuators (longest first, digraphs, alternative tokens, `<::`), numbers, character and string literals with encoding prefixes, raw strings and user-defined suffixes, comments, line splices (also inside tokens), Unicode identifiers and universal character names |
| Literals | Values of integer, floating, character and string literals (`LiteralExpression.Value`), encodings, suffixes; adjacent strings are one `ConcatenatedStringExpression` |
| Names | Qualified names (`::a::b<int>::c`, `decltype(x)::type`, `T::template f<int>`), template-ids with type and expression arguments, operator, conversion and literal operator functions, destructors |
| Types | Declaration specifiers in any order: fundamental types, cv-qualifiers, storage classes, `typedef`, named types, `typename`, elaborated types (`struct X`), `decltype`, `decltype(auto)`, constrained placeholders (`C auto`), GNU `__int128` and friends; type-ids |
| Declarators | Pointers, references, pointers to members, arrays, functions with cv- and ref-qualifiers, `noexcept`, trailing return types and variadic parameters, parentheses, parameter packs, abstract declarators; `requires` after a declarator |
| Declarations | Function definitions (also constructors, destructors and conversion functions without specifiers) and simple declarations, `struct X;`, parameters with default values; initializers `= x`, `= { }`, `(x)` and `{ }` (`int a(b);` declares a variable when `b` is a value) |
| Names and scopes | A symbol table tells types from values: `a * b;` declares `b` when `a` is a type; unknown names use heuristics (`X y`, `X *y;`, `X &y = z;`, `vector<int> v;`), and `CppParseOptions.TypeNames`/`TemplateNames` add names from headers |
| Statements | Compound, declaration, expression, null, `if`/`else`, `while`, `return` (also with a braced list) |
| Expressions | All operators of C++23 with their precedence and associativity (including `<=>`, `.*`, `->*`, the comma, `?:` and GNU `?:`, `throw`, `co_await`, `co_yield`); calls, subscripts with any number of arguments, member access (`a.template f<int>`, `p->~T()`); C-style, named and functional casts (`int(x)`, `T{x}`, `auto(x)`, `typename T::x()`, `decltype(x)(y)`); `sizeof`, `sizeof...`, `alignof`, `noexcept`, `typeid`; `new` (placement, parenthesized types, array bounds, initializers) and `delete`; `this`; braced lists with designators; pack expansions; fold expressions; lambdas (captures, init-captures, template parameters, attributes, specifiers, `noexcept`, trailing return types, `requires`); requires-expressions |
| Templates | Template parameters of lambdas: type, non-type, template template, packs, defaults, constrained (`std::integral T`) |
| Attributes | `[[...]]` with namespaces, `using`, arguments (kept as written) and `...`; in lambdas |
| Preprocessor | Directives as trivia, conditional compilation, macro expansion in conditions, `__has_include` and other feature tests |
| Classes, enums, namespaces, template declarations, statements other than the above, ... | Not yet |

Ambiguities of expressions are resolved like clang, with the symbol table:

- `(T)x` is a cast when `T` is a type: it has a keyword, a pointer or reference declarator, or names a
  type declared in the file. A single name that is not declared in the file (from a header) is a type when
  an identifier, a literal or a keyword like `sizeof` follows: `(size_t)n` is a cast, `(x)(y)` a call and
  `(x) - y` a subtraction.
- `sizeof(x)` and `typeid(x)` take a type unless `x` is declared as a value.
- `<` after a name starts template arguments when the name is a known template, and after an unknown name
  when the arguments are followed by a token that cannot follow a comparison `a < b > c`: `(`, `)`, `{`,
  `::`, `;`, `,`, ... (`get<0>(t)`, `std::array<int, 3>{}`).
- A name followed by `{` is a type (a functional cast) unless it is declared as a value; followed by `(`,
  it is a type only when declared as one: `std::string("a")` is a call, since names from headers are not known.
- In template parameters, a name that is not declared in the file is a concept (`std::integral T`) unless it
  ends with `_t` (`std::size_t N`).

## Checking against clang

`src/PaspanParsers.Tests/Cpp` runs clang as an oracle (see `CLAUDE.md`). For each valid file of the corpus
the parser must succeed, the printed tree must compile to the same clang AST, and every node must have
the span and a matching kind of a clang node (`CppKindMap.cs`), so that, for example, `a * b;` read as a
declaration instead of a multiplication is caught even though it prints the same. The values of literals
must also be clang's (`CppLiteralChecker.cs`).

## Files

| File | Contents |
|---|---|
| `CppParser.cs` | Entry points: `Parse`, `TryParse`, `TranslationUnitParser` |
| `CppParseOptions.cs`, `CppParseContext.cs` | Options (language version, macros, include directories, names from headers) and per-parse state |
| `CppAst.cs` | AST nodes |
| `CppWriter.cs` | Prints an AST as C++ |
| `Parser/Lexer.cs`, `Parser/SyntaxToken.cs` | Tokens and trivia |
| `Parser/Literals.cs` | Values of literals |
| `Parser/Preprocessor*.cs` | Directives, conditions and macro expansion in them |
| `Parser/SyntaxParser*.cs` | Recursive descent parser: names, types, declarators, declarations, statements, expressions, lambdas and requires-expressions, template parameters, attributes |
| `Parser/Symbols.cs` | Scopes and the kinds of declared names |
