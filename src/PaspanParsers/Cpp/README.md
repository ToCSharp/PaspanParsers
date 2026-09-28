# C++ Parser

A C++23 parser for valid code, checked against clang. It is built by stages following
[the plan](../../../docs/cpp-parser-clang-level-plan.md): the grammar of C++23 is complete and measured on real
code, whose macros it does not expand (see [Support](#support) and [Real code](#real-code)); positions,
documentation comments and the writer are checked against clang, and the stage on performance remains.

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

## Positions

Spans are offsets in UTF-8 bytes of the input without its byte order mark, from the first token of a
node to the end of its last token, without the trivia around them; the translation unit spans the whole
input. Every node's span holds exactly the tokens of the node: those `CppWriter.WriteNode` writes for it.
`TryParse` also accepts the UTF-8 bytes of a file, whose spans are offsets after its byte order mark
(`Utf8Source.WithoutByteOrderMark`).

`LineMap` converts offsets to 1-based lines and columns and back. For C++ it is built with
`unicodeLineBreaks: false`: lines end at `\n`, `\r\n` and `\r` (not at U+2028, as in C#). Columns count
UTF-16 code units, like editors and the Language Server Protocol; `ParseError.Line` and `Column` are the same.
Both are checked against the lines and columns of clang's AST on the corpus.

```csharp
var bytes = File.ReadAllBytes("widget.cpp");
if (CppParser.TryParse(bytes, options, out var unit, out var error))
{
    var utf8 = Utf8Source.WithoutByteOrderMark(bytes).Span;
    var lines = new LineMap(utf8, unicodeLineBreaks: false);
    var (line, column) = lines.GetLineAndColumn(unit.Declarations[0].Span.Start);
    var offset = lines.GetOffset(line, column);    // back to the byte offset
}
```

## Documentation comments

`Declaration.LeadingTrivia` and `Enumerator.LeadingTrivia` are the white space, comments and directives
before a declaration. `DocumentationComment` finds the Doxygen comment of a declaration with the rules clang
uses to attach comments to declarations, and reads its text:

```csharp
// /// Adds two numbers.
// /// \return The sum.
// int add(int a, int b);
var text = DocumentationComment.GetText(utf8, unit.Declarations[0]);   // "Adds two numbers.\n\\return The sum."
```

- Documentation comments are `///` and `//!` lines and `/** */` and `/*! */` blocks; like clang, `////` and
  `/**/` count too. Comments separated only by white space with at most one line break are one comment.
- The comment of a declaration is the last one before it, unless the text between them has a `;`, `{`, `}`,
  `#` or `@`: a declaration or a directive between them. Ordinary comments between them do not matter,
  and neither do access specifiers (`/// The x.` before `public:` documents the member after it too).
- A trailing comment `///<` (also `//!<`, `/**<`, `/*!<`) on the line of the name of a variable, field or
  enumerator documents it, before a comment in front of it: `int x; ///< The x.`
- `Find(utf8, declaration)` gives the span of the comment, `Find(utf8, simpleDeclaration, initDeclarator)` the
  comment of one of its declarators (`/// Doc.\nint a{1}, b;` documents `a` but not `b`), and
  `Find(utf8, enumerator)` that of an enumerator. `GetText` drops the markers, one space after them, the `*`
  that starts the lines of a block and blank first and last lines of a block; Doxygen commands stay as written.
- Comments in conditions, handlers and lambda captures are not looked for.

The oracle checks that the comments found are those clang attaches to the declarations (its `FullComment`).

## Writer

`CppWriter` writes a tree back as C++: `WriteTranslationUnit` a whole file, `WriteNode` any node on its own
(a declarator, an initializer with its `=`, an enumerator, a base specifier). The output is literal: the
parentheses, the forms of initializers, the order of specifiers, digraphs aside, and even `(int...)` and
`__asm__` are those of the source, so that parsing the output again builds the same tree. Comments and
formatting are not kept; directives are written as they are in the source, on their own lines.

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
| Declarations | Function definitions (also constructors, destructors, conversion functions and deduction guides without specifiers; ctor-initializers, function-try-blocks, `= default`, `= delete`) and simple declarations with attributes, `struct X;`, `friend X;`, parameters with default values, attributes and `this`; initializers `= x`, `= { }`, `(x)` and `{ }` (`int a(b);` declares a variable when `b` is a value); structured bindings (`auto &[a, b] = x;`); `static_assert`; empty declarations |
| Namespaces | Namespace definitions (nested `a::inline b`, inline, unnamed, attributes), namespace aliases, using-directives, using-declarations (lists, `typename`, packs), `using enum`, alias declarations, linkage specifications `extern "C"` (single or braces) |
| Classes | Class, struct and union definitions in any declaration (also local and unnamed): attributes, `final`, bases with access, `virtual` and packs; members with access specifiers, bit-fields (also unnamed), default member initializers, `override`/`final`, `= 0`, friends, nested classes, member templates; `explicit(bool)` |
| Enumerations | Unscoped and scoped (`enum class`, `enum struct`), underlying types, opaque declarations, enumerators with attributes and values, trailing commas |
| Templates | Template declarations with parameters (type, non-type, template template, packs, defaults, constrained) and `requires`-clauses; explicit specializations (also nested `template <> template <>`), partial specializations, explicit instantiations, `extern template`, members defined outside their class template, deduction guides, concepts, variable and alias templates |
| Modules | `module;`, `export module a.b:part;`, `module :private;`, `import` of modules, partitions and headers, `export` declarations and blocks |
| Names and scopes | A symbol table tells types from values: `a * b;` declares `b` when `a` is a type; unknown names use heuristics (`X y`, `X const`, `X *y;`, `X &y = z;`, `vector<int> v;`), and `CppParseOptions` adds names from headers: `TypeNames`, `TemplateNames` (class and alias templates), `FunctionTemplateNames` (function and variable templates), `ConceptNames` and `ClassMembers` (the variables and functions of classes, seen by member functions defined in the file and by derived classes); these names may be qualified (`std::system_error`), and a qualified name of the code is looked up with its qualifier first. Namespaces, classes and enumerations keep their scopes: qualified names are looked up in them and in the classes that typedefs and aliases name, reopened namespaces and functions defined outside their class see their names, and using-directives, `using enum`, inline namespaces and known bases make names visible. Member function bodies are parsed when the outermost class is complete. Blocks, statements, their substatements, handlers and template parameters have scopes |
| Statements | All statements of C++23: compound, declaration, expression, null; `if` with init-statements and condition declarations, `if constexpr`, `if consteval`, `if !consteval`; `switch`, `case` (also the GNU range `case 1 ... 3:`), `default`; `while`, `do`, `for`, range-based `for` with init-statements and structured bindings; `break`, `continue`, `return`, `co_return`, `goto`, labels (also at the end of a block); `try`/`catch`; attributes of statements |
| Expressions | All operators of C++23 with their precedence and associativity (including `<=>`, `.*`, `->*`, the comma, `?:` and GNU `?:`, `throw`, `co_await`, `co_yield`); calls, subscripts with any number of arguments, member access (`a.template f<int>`, `p->~T()`); C-style, named and functional casts (`int(x)`, `T{x}`, `auto(x)`, `typename T::x()`, `decltype(x)(y)`); `sizeof`, `sizeof...`, `alignof`, `noexcept`, `typeid`; `new` (placement, parenthesized types, array bounds, initializers) and `delete`; `this`; braced lists with designators; pack expansions; fold expressions; lambdas (captures, init-captures, template parameters, attributes, specifiers, `noexcept`, trailing return types, `requires`); requires-expressions |
| Attributes | `[[...]]` with namespaces, `using`, arguments (kept as written) and `...`; `alignas`; before declarations, statements and parameters, after declared names, in classes, enumerators, namespaces and lambdas |
| GNU and clang extensions | `__attribute__((...))` (before and among specifiers, after declarators, also of function definitions, in class heads), asm declarations with qualifiers and asm labels, `__restrict`, `throw()`, `__extension__`, `_BitInt(N)`; builtins that take types (`__builtin_offsetof`, `__builtin_bit_cast`, `__builtin_va_arg`, `__builtin_convertvector`, type traits such as `__is_same`); not statement expressions |
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
  A template-id of a class or alias template is a type (`Box<int>(1)`), of a function template a call; the
  name of a class template alone is a type (`Box(1)`, the injected-class-name).
- A name qualified by a namespace or class that the file does not know (`std::`, `T::`) is known only from
  the options. After `.` and `->`, a name followed by `<` starts template arguments unless the file or the
  options know it as a template, like an unknown name.
- In a block, `T x(y);` with a name `y` that is not known declares a variable initialized with `y`, not a
  function taking a `y`: `std::lock_guard<std::mutex> lock(mutex);`.
- A name that is not known but is the type of a declaration (`StringRef s;`, `SmallVector<int> v;`) is a
  type, or a class template, in the rest of the file: `StringRef(s)` is then a functional cast.
- In template parameters, a name that is not declared in the file is a concept (`std::integral T`) unless it
  ends with `_t` (`std::size_t N`).
- Among the members of a class `S`, `S(` starts a constructor (but `S (*f)();` declares a pointer), and a known
  template followed by parameters and `->` is a deduction guide. A template-id of a class template defined in
  the file followed by `(` is a functional cast. `f(Ts...)` declares a parameter pack when `Ts` is a template
  parameter pack, and a variadic function otherwise.
- The bodies of member functions defined in a class are skipped and parsed when the outermost class is
  complete, so they see the members declared after them; default member initializers and default arguments
  are parsed where they are.

## Checking against clang

`src/PaspanParsers.Tests/Cpp` runs clang as an oracle (see `CLAUDE.md`). For each valid file of the corpus:

- the parser must succeed, and the printed tree must compile to the same clang AST;
- every node must have the span and a matching kind of a clang node (`CppKindMap.cs`), so that, for example,
  `a * b;` read as a declaration instead of a multiplication is caught even though it prints the same. Clang's
  JSON dump has no ranges for types, declarators, specifiers and initializers: those nodes must start and end
  at the boundaries of clang's tokens;
- the span of every node must hold the tokens that `CppWriter.WriteNode` writes for it (`CppNodeText.cs`),
  which checks the nodes clang has no ranges for beyond their boundaries;
- the values of literals must be clang's (`CppLiteralChecker.cs`), and so must the documentation comments of
  the declarations (`CppDocumentationChecker.cs`).

## Real code

The parser is measured on {fmt}, nlohmann/json and LLVM's ADT and Support libraries
(`src/PaspanParsers.Tests/Cpp/fetch-external-corpora.sh`). The oracle checks each file as it is, then, if it
fails, with the names and macros of its headers (what the parser would know if it read them), and then with
its macros expanded by clang: the failures that remain are errors of the parser.

| Corpus | Valid files | As they are | With header names | With macros expanded too |
|---|---|---|---|---|
| {fmt} (`include/fmt`, `src`) | 20 | 2 (10.0%) | 2 (10.0%) | 20 (100%) |
| nlohmann/json (`single_include`) | 2 | 0 | 0 | 2 (100%) |
| LLVM `llvm/include/llvm/ADT` | 115 | 73 (63.5%) | 82 (71.3%) | 115 (100%) |
| LLVM `llvm/lib/Support` | 178 | 91 (51.1%) | 151 (84.8%) | 177 (99.4%) |
| Total | 315 | 166 (52.7%) | 235 (74.6%) | 314 (99.7%) |

No file fails with its macros expanded: the one file left, `CrashRecoveryContext.cpp`, is not measured that
way, since clang expands a macro of glibc (`sa_handler`) again in the expanded code and rejects it.

The files that fail as they are use macros in syntactic positions (`FMT_BEGIN_NAMESPACE`,
`NLOHMANN_JSON_NAMESPACE_BEGIN`, `LLVM_ABI`), which the parser reads as names, test macros of their headers
in conditional directives, or use names of headers where a heuristic guesses wrong: `Twine(s)` is a call
when the file never declares something of the type `Twine`. The corpora were measured with clang 18 at
their revisions of 2026-09-28 ({fmt} 5da4e9a, nlohmann/json 373005f, LLVM a98dcad4), with all the checks of the
oracle, including the tokens of spans and documentation comments.

## Files

| File | Contents |
|---|---|
| `CppParser.cs` | Entry points: `Parse`, `TryParse`, `TranslationUnitParser` |
| `CppParseOptions.cs`, `CppParseContext.cs` | Options (language version, macros, include directories, names from headers) and per-parse state |
| `CppAst.cs` | AST nodes |
| `CppWriter.cs` | Prints an AST, or any node, as C++ |
| `DocumentationComment.cs` | Documentation comments of declarations |
| `../Common/TextSpan.cs`, `../Common/LineMap.cs` | Spans and lines and columns of offsets, shared with the C# parser |
| `Parser/Lexer.cs`, `Parser/SyntaxToken.cs` | Tokens and trivia |
| `Parser/Literals.cs` | Values of literals |
| `Parser/Preprocessor*.cs` | Directives, conditions and macro expansion in them |
| `Parser/SyntaxParser*.cs` | Recursive descent parser: names, types, declarators, declarations, classes and enumerations, namespaces and modules, templates, statements, expressions, lambdas and requires-expressions, attributes |
| `Parser/Symbols.cs` | Scopes and the kinds of declared names |
