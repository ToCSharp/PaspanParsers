# План: C++-парсер в PaspanParsers по образцу C#-парсера (оракул — Clang)

## Контекст

C#-парсер (`src/PaspanParsers/CSharp`) доведён до уровня Roslyn на корректном коде за этапы 0–9 по плану
`docs/csharp-parser-roslyn-level-plan.md`. Его устройство:
- рукописный рекурсивный спуск `SyntaxParser` (`ref struct`) поверх ленивого кэшированного лексера;
- препроцессор работает как trivia;
- семантический AST с `Span` в байтах UTF-8 (`Finish(node, start)`);
- `CSharpWriter` печатает дерево буквально;
- оракул Roslyn в тестах: round-trip, эквивалентность деревьев, `SpanChecker`, ratchet-база `oracle-baseline.txt`, внешний корпус и бенчмарк.

Нужно добавить C++-парсер, построенный так же, с Clang в роли оракула.

**Цель.** Любой файл, который `clang++ -std=c++23 -fsyntax-only` разбирает без ошибок и который не использует макросы в синтаксических позициях, наш парсер должен разобрать. Построенный AST должен быть эквивалентен дереву Clang. Восстановление после ошибок не требуется (это необязательный последний этап).

**Принятые решения** (ответы пользователя и выводы исследования):
- **Стандарт — C++23.** В окружении есть Clang 18.1.3 и libstdc++ 13.
- **Препроцессор «как в C#», без раскрытия.**
  - `#if`/`#ifdef`/`#elif` вычисляются по макросам из опций и по `#define` в файле.
  - `#include` не читается, макросы в коде не раскрываются.
  - Директивы `#include`/`#define`/`#pragma` сохраняются в AST, чтобы Writer их печатал.
  - Имя макроса в коде разбирается как обычное имя: `int a[N];` и `MAX(a, b)` работают, а `DECLARE_X(y)` на уровне namespace — нет.
- **Оракул — процесс `clang++ -Xclang -ast-dump=json`.**
  - Путь к Clang берётся из `CLANG_PATH` или `PATH`. Без Clang тесты оракула дают `Inconclusive`.
  - В NuGet ничего не добавляется, `ClangSharp` не нужен.
- **Архитектура — как у C#: рукописный `SyntaxParser`, а не комбинаторы.**
  - Опыт C#-плана: комбинаторная грамматика была удалена на этапах 2–6.
  - C++ ещё сильнее требует просмотра вперёд и знания имён.

## Главные отличия C++ и как они решаются

1. **Неоднозначности зависят от знания имён**:
   - объявление или выражение (`a * b;`, `T(x);`);
   - most vexing parse `T a(U());`;
   - `<` как шаблон или «меньше»;
   - cast `(T)-x` или скобки.
   Решение:
   - Таблица символов `Parser/Symbols.cs` в `CppParseContext`.
     - Области видимости: namespace, class, function, block, template-параметры.
     - Виды имён: тип, шаблон, concept, namespace, значение.
     - Учитываются `using`/`using namespace`/`typedef`/alias, injected-class-name, члены известных баз, `typename`/`template` у зависимых имён.
   - Правило [stmt.ambig]: что может быть объявлением, то объявление.
   - Для имён, неизвестных в файле (из заголовков), — эвристики:
     - `X * y;` и `X & y;` в начале оператора — объявление;
     - `X<` — шаблон, если список аргументов сканируется и за ним идёт допустимый токен (как правило `<` в C#).
   - `CppParseOptions.TypeNames`/`TemplateNames` дополняют знания.
2. **Round-trip по тексту не ловит неверную трактовку.** `a * b;` печатается одинаково, будь это объявление или умножение.
   Поэтому с этапа 0 работает **проверка вида узла**: `CppSpanChecker` требует, чтобы у каждого нашего узла был узел Clang с тем же span и совместимым видом. Например:
   - `VariableDeclaration` ↔ `VarDecl`/`DeclStmt`;
   - `BinaryExpression` ↔ `BinaryOperator`/`CXXOperatorCallExpr`;
   - `CallExpression` ↔ `CallExpr`/`CXXMemberCallExpr`/`CXXConstructExpr`/`CXXFunctionalCastExpr`/`CXXUnresolvedConstructExpr`.

   Таблица соответствий живёт в тестах.
3. **Для типов в JSON-дампе Clang нет диапазонов** (TypeLoc не выводятся). Span типов, имён и частей деклараторов проверяется по границам токенов из `clang -Xclang -dump-raw-tokens`/`-dump-tokens` и по вложенности в родителя.
4. **Смещения Clang — байтовые.** Они совпадают с нашими UTF-8 span, нужен только сдвиг на BOM. Перевод из UTF-16, как в `SpanChecker` C#, не нужен.
5. **Изолированно операторы не проверить.** Clang выполняет семантический анализ, и оператор, вынутый из функции, не компилируется. Аналога `Oracle_BuiltInCorpus_Statements` не будет. Вместо него:
   - отчёт оракула указывает первую расходящуюся декларацию или оператор (путь в нормализованном JSON);
   - на фрагментах работают юнит-тесты.

## Архитектура

Новые файлы в `src/PaspanParsers/Cpp/` (namespace `PaspanParsers.Cpp`), зеркально `CSharp/`:
- `CppParser.cs` — копия схемы `CSharpParser.cs`:
  - `Parse`, `TryParse(string|ReadOnlyMemory<byte>, CppParseOptions, out TranslationUnit, out ParseError)`;
  - снятие BOM, повтор на потоке со стеком 256 МБ при `InsufficientExecutionStackException`;
  - `ParseError` в самой дальней точке.
- `CppParseOptions.cs` содержит:
  - `LanguageVersion` (Cpp11…Cpp23, `Latest` = Cpp23);
  - `Macros` (имя → значение, для `#if`);
  - `IncludePaths` (только для `__has_include`);
  - `TypeNames`/`TemplateNames`;
  - позже `ErrorRecovery`.
- `CppParseContext.cs` содержит опции, определённые макросы, `SyntaxCache` (токены, парные скобки, интернирование) и таблицу символов.
- `CppAst.cs`:
  - `CppNode : ICppNode { TextSpan Span }`;
  - иерархии `Declaration`, `Statement`, `Expression`, `TypeReference`/`DeclSpecifiers`, `Declarator`, `TemplateParameter`, `Attribute`, `PreprocessorDirective`;
  - `TranslationUnit`.
- `CppWriter.cs` печатает буквально: скобки, форму инициализации `()`/`{}`/`=`, порядок спецификаторов, директивы.
- `Parser/`:
  - `Lexer.cs` (статические сканеры над `ReadOnlySpan<byte>`, как в C#), `SyntaxToken.cs`, `Preprocessor.cs`, `Symbols.cs`;
  - `SyntaxParser.cs` (+ `.Names`/`.Types`/`.Declarators`/`.Expressions`/`.Statements`/`.Declarations`/`.Templates`/`.TranslationUnit`).
- `README.md` и `CppGrammarSpecification.txt` (грамматика из приложения [gram] стандарта C++23).

Общий слой. На этапе 0 из `CSharp/` в `src/PaspanParsers/Common/` (namespace `PaspanParsers`) выносится то, что не зависит от языка:
- `TextSpan`;
- `LineMap` (набор переводов строк — параметр);
- UTF-8/BOM-хелперы `GetUtf8Source`;
- запуск на большом стеке;
- `SyntaxRuleParser<T>`.

C#-парсер переходит на них. Единственное изменение API — namespace `TextSpan`: он виден из `PaspanParsers.CSharp`, но пользователю нужен `using PaspanParsers;`. Базы C#-оракула должны остаться зелёными.

Тесты в `src/PaspanParsers.Tests/Cpp/`:
- `ClangOracle.cs`:
  1. `clang++ -std=c++23 -fsyntax-only -Xclang -ast-dump=json` + предопределённые макросы. Ошибки Clang → статус `Invalid`.
  2. Наш парсер → `ParseFailed`.
  3. `CppWriter` → `WriteFailed`.
  4. Clang на напечатанном тексте. Нормализованные деревья сравниваются: без неравенства → `Mismatch`.
  5. `CppSpanChecker` → `SpanMismatch`, иначе `Passed`.
  Статусы те же, что у `RoslynOracle`.
- `ClangAstReader.cs` — потоковое чтение через `Utf8JsonReader`. С `<vector>` дамп весит около 100 МБ, поэтому:
  - берутся только декларации главного файла;
  - учитывается, что Clang опускает `file`, если файл не сменился;
  - удаляются `id`, `loc`, `range`, `previousDecl`, `parentDeclContextId` и узлы `*Comment`;
  - для `CppSpanChecker` собираются диапазоны `offset .. end.offset + tokLen`.
- `CppSpanChecker.cs`, `CppKindMap.cs` — проверка из пункта 2 выше.
- `CppCorpusTests.cs` — зеркало `CSharpCorpusTests.cs`:
  - `CorpusFiles_AreValidCpp`;
  - `Oracle_BuiltInCorpus_HasNoRegressions` с `Cpp/Corpus/oracle-baseline.txt` и `UPDATE_ORACLE_BASELINE=1`;
  - `Oracle_ExternalCorpus` (`CPP_CORPUS_DIR`, `CPP_CORPUS_DEFINES="A=1;B"`, `CPP_CORPUS_INCLUDE` — пути `-I` только для Clang);
  - отчёты `cpp-oracle-report.txt` и `cpp-oracle-external-report.txt`.
- `CppTestHelper.cs` — аналог `SyntaxTestHelper`: `Expression`, `Statement`, `Declaration`, `AssertOracle`.
- Юнит-тесты по областям: `LexicalTests`, `PreprocessorTests`, `TypeTests`, `ExpressionTests`, `StatementTests`, `DeclarationTests`, `TemplateTests`, `SpanTests`, `WriterTests`, `EntryPointTests`.
- `Corpus/*.cpp` — тематические, самодостаточные файлы без `#include` стандартной библиотеки, чтобы Clang работал быстро, а все имена были известны. В `.csproj` — `Compile/None Remove="Cpp\Corpus\**"`.
- Предопределённые макросы для нашего парсера берутся из `clang++ -std=c++23 -dM -E -x c++ /dev/null`. Так ветки `#if` совпадают с Clang.

## Этапы

Каждый этап добавляет юнит-тесты, тематический файл корпуса, прогон оракула и обновление базы. Статус этапа записывается в plan-документ, как в C#-плане.

### Этап 0. Каркас, общий слой, оракул Clang
- Вынести общий слой из `CSharp/`. Все C#-тесты и `Oracle_BuiltInCorpus*` должны остаться зелёными.
- Добавить скелет `Cpp/` и тестовую инфраструктуру из раздела «Архитектура».
- Минимальный парсер: пустая единица трансляции, `int main() { return 0; }`.
- Файлы корпуса `00-Basics.cpp` … `20-*.cpp`, по одному на этап и область. Зафиксировать базовый процент.

### Этап 1. Лексика (фазы трансляции 1–3)
- Склейка строк `\` + перевод строки внутри токенов и trivia. Span остаётся в исходных байтах.
- Комментарии, пробелы.
- Идентификаторы: Unicode XID по C++23, `\u`/`\U`/`\N{…}`.
- Ключевые слова C++23 и альтернативные токены (`and`, `bitor`, `not_eq`, …).
- Пунктуаторы с максимальным захватом, диграфы `<%`, `%:`, правило `<::`. `>` всегда одиночный токен, `>>`/`>=`/`>>=` собираются парсером — для `A<B<C>>`.
- Литералы:
  - целые dec/hex/oct/bin с `'`-разделителями и суффиксами `u`, `l`, `ll`, `z`;
  - плавающие, включая hex-float;
  - символы и строки с префиксами `u8`, `u`, `U`, `L`;
  - raw-строки `R"d(...)d"`;
  - пользовательские литералы `_x`;
  - конкатенация соседних строк — узел со списком частей.

### Этап 2. Препроцессор как trivia
Этот этап идёт раньше, чем в C#: почти каждый C++-файл начинается с `#include`, `#pragma once` или include guard.
- Директива распознаётся только в начале строки.
- `#if`/`#ifdef`/`#ifndef`/`#elif`/`#elifdef`/`#elifndef`/`#else`/`#endif`:
  - вычисление целочисленного константного выражения: арифметика, сравнения, `?:`, `defined`, числа с суффиксами;
  - `__has_include` проверяет наличие файла по `IncludePaths`;
  - `__has_cpp_attribute` и `__has_builtin` — по таблице, иначе 0.
- `#define`/`#undef` запоминаются только для `#if` и сохраняются в AST. Выборочное состояние по позиции устроено как в C# (`Preprocessor.cs`): ленивые токены сканируются не по порядку.
- `#include`, `#pragma`, `#line`, `#error`, `#warning`, `#embed` — узлы `PreprocessorDirective`, которые Writer печатает дословно.
- Директива стоит в списке объявлений, членов или операторов, либо привязана к следующему узлу (`LeadingDirectives`), как `#nullable` в C#.
- Неактивные ветки пропускаются по строкам с учётом вложенности.

### Этап 3. Имена, таблица символов, типы и деклараторы
- nested-name-specifier, включая `::`, `decltype(...)::` и `template` в квалификаторе.
- template-id; operator-function-id, conversion-function-id, literal-operator-id; деструктор `~T`.
- decl-specifier-seq в любом порядке: cv, `signed`/`unsigned`/`long long`, `auto`, `decltype(auto)`, elaborated `struct X`, `typename T::x`, placeholder с concept `std::integral auto`.
- Деклараторы:
  - указатели, ссылки, `&&`, указатели на член `C::*`;
  - массивы, функции с cv/ref-qualifier, `noexcept`, trailing return, `requires` после декларатора;
  - вложенные `(*f)(int)`, абстрактные деклараторы, pack `...`.
- `Symbols.cs`: области видимости и объявление имён по ходу разбора, эвристики для неизвестных имён.

### Этап 4. Выражения
- Pratt-парсер с приоритетами C++23, включая `<=>`, `.*`, `->*`, запятую, `?:` и присваивание с правой ассоциативностью, `throw`.
- Primary:
  - литералы, `this`, id-expression;
  - лямбды: захваты, включая `[=, this]` и init-capture; template-параметры, атрибуты, `mutable`/`constexpr`/`consteval`/`static`, `requires`;
  - fold-выражения `(... op x)`;
  - `requires`-выражения: simple, type, compound и nested requirement;
  - braced-init-list с designated initializers, `new`/`delete` (placement, `new (T)`, `delete[]`);
  - `sizeof`/`sizeof...`/`alignof`/`noexcept`/`typeid`, `*_cast<>`, функциональный cast `T(x)`/`T{x}`;
  - `co_await`/`co_yield`, `[[` только как начало атрибута.
- Дизамбигуации: C-cast или скобки, `sizeof(T)` или `sizeof(expr)`, `<` как шаблон, `>` внутри аргументов шаблона.

### Этап 5. Операторы
- Диспетчер по первому токену. «Объявление или выражение» решается по [stmt.ambig] с опорой на таблицу символов.
- Операторы:
  - `if`/`if constexpr`/`if consteval` с init-statement; `switch`;
  - `for` и range-`for` с init-statement, `while`, `do`;
  - `break`, `continue`, `return`, `goto`, метки, `case`/`default`;
  - `try`/`catch(...)`, `co_return`, структурные привязки `auto& [a, b] =`, `static_assert` в блоке, атрибуты у операторов.

### Этап 6. Объявления и единица трансляции
- namespace, включая `inline` и вложенные `a::b`; `using`-директивы и -объявления; alias `using X = …`; `typedef`; `extern "C"` (блок и одиночный).
- class/struct/union:
  - базы с `virtual` и доступом, `final`;
  - члены: доступ, bit-field, NSDMI, `friend`, `= default`/`= delete`/`= 0`, `override`/`final`, `explicit(bool)`;
  - ctor-initializer, function-try-block.
- enum, включая `enum class : T` и `using enum`.
- Шаблоны:
  - параметры: type, non-type, template-template, pack, default, constrained;
  - `requires`-clause, явная и частичная специализация, явное инстанцирование, `extern template`;
  - deduction guides, `concept`.
- Модули C++20: `module;`, `export module`, `import`, `export { }`.
- Атрибуты `[[…]]`, включая `using ns:`, `alignas`. GNU-расширения, которые Clang принимает без макросов: `__attribute__`, `asm`, `__restrict`.
- Новые файлы корпуса по объявлениям и шаблонам. После этапа встроенный корпус должен проходить полностью.

### Этап 7. Внешние корпуса и имена из заголовков
- Прогон на реальном коде:
  - `{fmt}` (`include/fmt`, `src`);
  - `nlohmann/json` (single_include);
  - LLVM `llvm/include/llvm/ADT` и `llvm/lib/Support` с `CPP_CORPUS_INCLUDE`.
- Отчёт делит неудачи на категории:
  - макросы в синтаксической позиции (Clang видит раскрытие там, где у нас имя);
  - неверные эвристики неизвестных имён;
  - настоящие ошибки.
- Режим оракула «с именами»:
  - `ClangAstReader` собирает имена типов и шаблонов из включённых заголовков и передаёт их в `TypeNames`/`TemplateNames`;
  - так ошибки грамматики отделяются от ошибок эвристик;
  - в отчёте два процента: с именами и без.

### Этап 8. Позиции, Writer, документация
- `Finish(node, start)` используется с этапа 0. Здесь дорабатываются исключения `CppSpanChecker`/`CppKindMap`: узлы, которых у Clang нет, и границы токенов для типов.
- `LineMap` для C++, doc-комментарии Doxygen (`///`, `/** */`, `//!`) из `LeadingTrivia`.
- Документация:
  - `src/PaspanParsers/Cpp/README.md`: API, опции, ограничения, особенно отсутствие раскрытия макросов, и таблица поддержки;
  - строка C++ в корневом `README.md` и `docs/project-structure.md`;
  - раздел «C++ parser oracle» в `CLAUDE.md`: команды, env-переменные, база.

### Этап 9. Производительность
- `CppPerformanceTests.Benchmark_Corpus` при `CPP_CORPUS_DIR`, в Release. Меряются МБ/с и аллокации на байт.
- Время `clang -fsyntax-only` приводится как справочное: оно включает семантику.
- Линейность: кэш парных скобок, кэш спекулятивных разборов по позиции.
- Тесты вложенности, как в `CSharpPerformanceTests.cs`: 1000 скобок, длинные цепочки `a + a`, глубокие шаблоны `A<A<…>>`.
- `EnsureSufficientStack` на всех рекурсивных путях.

### Этап 10 (необязательный). Восстановление после ошибок
Как в `SyntaxParser.Recovery.cs`: panic-mode на уровне членов и операторов, узлы `Incomplete*`, опция `ErrorRecovery`.

## Порядок работы

- Сначала план сохраняется в репозиторий как `docs/cpp-parser-clang-level-plan.md` (на русском, в формате C#-плана) и коммитится в ветку `claude/gracious-feynman-eesi9m`.
- Затем этапы идут отдельными коммитами. Статус и цифры оракула дописываются в документ после каждого этапа.

## Проверка

1. `dotnet build PaspanParsers.slnx`.
2. `dotnet run --project src/PaspanParsers.Tests`: все старые тесты, включая C#-оракул после выноса общего слоя, новые C++ юнит-тесты и `Oracle_BuiltInCorpus` для C++ на встроенном корпусе.
3. Базу C++ обновлять так: `UPDATE_ORACLE_BASELINE=1 dotnet run --project src/PaspanParsers.Tests -- --filter "FullyQualifiedName~Cpp"`.
4. Внешний корпус: `CPP_CORPUS_DIR=… CPP_CORPUS_INCLUDE=… dotnet run --project src/PaspanParsers.Tests -- --filter "FullyQualifiedName~Cpp.CppCorpusTests.Oracle_ExternalCorpus"`.
5. Бенчмарк — с `-c Release` и фильтром `Benchmark_Corpus`.
