# C++ Parser

A C++23 parser for valid code, checked against clang. **Work in progress:** it is being built by stages
following [the plan](../../../docs/cpp-parser-clang-level-plan.md); the grammar of C++23 is complete, and
the next stages measure it on real code, whose macros it does not expand (see [Support](#support)).

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
| Declarations | Function definitions (also constructors, destructors, conversion functions and deduction guides without specifiers; ctor-initializers, function-try-blocks, `= default`, `= delete`) and simple declarations with attributes, `struct X;`, parameters with default values, attributes and `this`; initializers `= x`, `= { }`, `(x)` and `{ }` (`int a(b);` declares a variable when `b` is a value); structured bindings (`auto &[a, b] = x;`); `static_assert`; empty declarations |
| Namespaces | Namespace definitions (nested `a::inline b`, inline, unnamed, attributes), namespace aliases, using-directives, using-declarations (lists, `typename`, packs), `using enum`, alias declarations, linkage specifications `extern "C"` (single or braces) |
| Classes | Class, struct and union definitions in any declaration (also local and unnamed): attributes, `final`, bases with access, `virtual` and packs; members with access specifiers, bit-fields (also unnamed), default member initializers, `override`/`final`, `= 0`, friends, nested classes, member templates; `explicit(bool)` |
| Enumerations | Unscoped and scoped (`enum class`, `enum struct`), underlying types, opaque declarations, enumerators with attributes and values, trailing commas |
| Templates | Template declarations with parameters (type, non-type, template template, packs, defaults, constrained) and `requires`-clauses; explicit specializations (also nested `template <> template <>`), partial specializations, explicit instantiations, `extern template`, members defined outside their class template, deduction guides, concepts, variable and alias templates |
| Modules | `module;`, `export module a.b:part;`, `module :private;`, `import` of modules, partitions and headers, `export` declarations and blocks |
| Names and scopes | A symbol table tells types from values: `a * b;` declares `b` when `a` is a type; unknown names use heuristics (`X y`, `X const`, `X *y;`, `X &y = z;`, `vector<int> v;`), and `CppParseOptions.TypeNames`/`TemplateNames` add names from headers. Namespaces, classes and enumerations keep their scopes: qualified names are looked up in them, reopened namespaces and functions defined outside their class see their names, and using-directives, `using enum`, inline namespaces and known bases make names visible. Blocks, statements, their substatements, handlers and template parameters have scopes |
| Statements | All statements of C++23: compound, declaration, expression, null; `if` with init-statements and condition declarations, `if constexpr`, `if consteval`, `if !consteval`; `switch`, `case` (also the GNU range `case 1 ... 3:`), `default`; `while`, `do`, `for`, range-based `for` with init-statements and structured bindings; `break`, `continue`, `return`, `co_return`, `goto`, labels (also at the end of a block); `try`/`catch`; attributes of statements |
| Expressions | All operators of C++23 with their precedence and associativity (including `<=>`, `.*`, `->*`, the comma, `?:` and GNU `?:`, `throw`, `co_await`, `co_yield`); calls, subscripts with any number of arguments, member access (`a.template f<int>`, `p->~T()`); C-style, named and functional casts (`int(x)`, `T{x}`, `auto(x)`, `typename T::x()`, `decltype(x)(y)`); `sizeof`, `sizeof...`, `alignof`, `noexcept`, `typeid`; `new` (placement, parenthesized types, array bounds, initializers) and `delete`; `this`; braced lists with designators; pack expansions; fold expressions; lambdas (captures, init-captures, template parameters, attributes, specifiers, `noexcept`, trailing return types, `requires`); requires-expressions |
| Attributes | `[[...]]` with namespaces, `using`, arguments (kept as written) and `...`; `alignas`; before declarations, statements and parameters, after declared names, in classes, enumerators, namespaces and lambdas |
| GNU extensions | `__attribute__((...))` (before and among specifiers, after declarators, in class heads), asm declarations with qualifiers and asm labels, `__restrict`, `throw()`; not `__extension__` or statement expressions |
| Preprocessor | Directives as trivia, conditional compilation, macro expansion in conditions, `__has_include` and other feature tests |

Ambiguities are resolved like clang, with the symbol table:

- A statement that can be a declaration is one ([stmt.ambig]): when it starts with a declaration specifier
  keyword or a type, it is parsed as a declaration first, and as an expression when that fails. With
  `typedef int T;`, `T(x);`, `T(*p);`, `T(c) = 7;` and `T(g)(int);` are declarations, `T(a) + 1;` and
  `T{a};` expressions. A name that is not declared in the file starts a declaration only when what follows
  can only follow a type: `X y`, `X const y`, `X *y;`, `X &y = z;`; `X(y);` is a call.
- A condition is a declaration when it starts like one and has an initializer: `if (int k = next())`.
  An if, switch or for statement has an init-statement when its parentheses hold a `;` outside brackets,
  and a for statement with two of them is not range-based.

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
- Among the members of a class `S`, `S(` starts a constructor (but `S (*f)();` declares a pointer), and a known
  template followed by parameters and `->` is a deduction guide. A template-id of a class template defined in
  the file followed by `(` is a functional cast. `f(Ts...)` declares a parameter pack when `Ts` is a template
  parameter pack, and a variadic function otherwise.
- Member function bodies are parsed where they are, not after the class: members declared after the body
  are not known in it.

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
| `Parser/SyntaxParser*.cs` | Recursive descent parser: names, types, declarators, declarations, classes and enumerations, namespaces and modules, templates, statements, expressions, lambdas and requires-expressions, attributes |
| `Parser/Symbols.cs` | Scopes and the kinds of declared names |
