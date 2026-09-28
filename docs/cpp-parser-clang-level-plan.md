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

> **Статус: выполнен.**
> - **Общий слой** `src/PaspanParsers/Common/` (namespace `PaspanParsers`): `TextSpan`, `LineMap` (параметр `unicodeLineBreaks`:
>   U+0085/U+2028/U+2029 — переводы строк в C#, но не в C++), `Utf8Source` (UTF-8 и снятие BOM), `LargeStack` (повтор на потоке со стеком 256 МБ).
>   C#-парсер и `CSharpWriter` перешли на них, `CSharpParser.GetUtf8Source` оставлен как обёртка. C#-тесты и оракул Roslyn зелёные.
>   **Отклонение от плана:** `SyntaxRuleParser<T>` не вынесен — он привязан к `ref struct SyntaxParser` своего языка; у C++ своя копия.
> - **Скелет** `src/PaspanParsers/Cpp/`: `CppParser`, `CppParseOptions` (`LanguageVersion`, `Macros`), `CppParseContext`, `CppAst`, `CppWriter`,
>   `Parser/Lexer.cs`, `Parser/SyntaxToken.cs`, `Parser/SyntaxParser*.cs`, `README.md`. `CppGrammarSpecification.txt` не добавлялся.
> - **Лексер** уже полон по токенам: пунктуаторы с максимальным захватом, диграфы и альтернативные токены (как операторы), правило `<::`,
>   pp-number, символы и строки с префиксами, raw-строки, пользовательские суффиксы, комментарии и склейка строк в trivia.
>   `>` — всегда одиночный токен, `>>`, `>=`, `>>=` собирает парсер.
> - **Минимальный парсер:** определения функций и простые объявления со спецификаторами-ключевыми словами, параметры со значениями по умолчанию,
>   `=`-инициализаторы; операторы compound, объявление, выражение, `;`, `if`/`else`, `while`, `return`; выражения — литералы, имена, скобки,
>   все бинарные операторы с приоритетами C++, `?:`, присваивания, префиксные и постфиксные операторы, вызовы.
> - **Оракул** (`src/PaspanParsers.Tests/Cpp/`):
>   - `Clang.cs` запускает `clang++ -std=c++23 -w` с исходником на stdin; рабочая папка задаёт поиск `#include "…"`.
>     Предопределённые макросы берутся из `-dM -E` и вместе с `-D` передаются парсеру (`ClangOracleOptions.ParseOptions`).
>   - `ClangAst.cs` читает JSON-дамп через `Utf8JsonReader` и материализует только декларации главного файла:
>     у их `loc` (или `expansionLoc`) нет `includedFrom`. Нормализация убирает `id`, `loc`, `range`, ссылки на декларации по адресу,
>     узлы `*Comment` и позиции в типах лямбд (`(lambda at <stdin>:3:12)`).
>   - `CppSpanChecker.cs` + `CppKindMap.cs` — три вида правил: `ExactRule` (узел Clang с тем же span и одного из видов; `WithoutSemicolon` —
>     Clang не включает `;` в диапазон `return`, выражений и `if`/циклов, которые ими кончаются), `DeclarationRule` (декларация Clang с `loc`
>     на объявленном имени и тем же концом: диапазон `VarDecl` у Clang начинается со спецификаторов, общих для всех деклараторов),
>     `TokensRule` (границы токенов из `-dump-raw-tokens`). Узлы из раскрытия макросов принимаются любого вида. Узел без правила — ошибка.
>   - `ClangOracleTests.cs` проверяет сам оракул, в том числе что проверка видов ловит `a * 2`, прочитанное как объявление.
>   - `CppCorpusTests.cs`: `CorpusFiles_AreValidCpp`, `Oracle_BuiltInCorpus_HasNoRegressions` (база `Cpp/Corpus/oracle-baseline.txt`),
>     `Oracle_ExternalCorpus` (`CPP_CORPUS_DIR`, `CPP_CORPUS_DEFINES`, `CPP_CORPUS_INCLUDE`); файлы проверяются параллельно.
> - **Корпус** `Cpp/Corpus/00-Basics.cpp` … `19-Cpp23.cpp`: лексика, препроцессор, типы, деклараторы, выражения, лямбды, операторы, классы,
>   специальные члены, шаблоны, вариативные шаблоны, концепты, namespace, enum, атрибуты, инициализация, неоднозначности, корутины, C++23.
>   Стандартные заголовки подключены только там, где их требует язык (`<compare>`, `<new>`, `<typeinfo>`, `<initializer_list>`, `<coroutine>`),
>   и в `02-Preprocessor.cpp` (`<cstddef>`). Модули C++20 перенесены в этап 6: им нужен режим модулей Clang.
> - **База:** 1/20 (`00-Basics.cpp`). Остальные файлы останавливаются на первой конструкции следующих этапов (`#`, `struct`, `template`, ...).
> - Юнит-тесты: `CppParserEntryPointTests.cs`, `CppLexicalTests.cs`, `ClangOracleTests.cs`.

- Вынести общий слой из `CSharp/`. Все C#-тесты и `Oracle_BuiltInCorpus*` должны остаться зелёными.
- Добавить скелет `Cpp/` и тестовую инфраструктуру из раздела «Архитектура».
- Минимальный парсер: пустая единица трансляции, `int main() { return 0; }`.
- Файлы корпуса `00-Basics.cpp` … `20-*.cpp`, по одному на этап и область. Зафиксировать базовый процент.

### Этап 1. Лексика (фазы трансляции 1–3)

> **Статус: выполнен.**
> - **Склейка строк внутри токенов.** Токен, который прерывает склейка `\⏎` (или который её содержит), сканируется заново по логической строке без склеек:
>   span — в исходных байтах, текст — логический (`ma\⏎in` → `main`, `<\⏎=` → `<=`, `retu\⏎rn` — ключевое слово).
>   В raw-строках склейки сохраняются, как требует стандарт.
>   Clang начинает токен со склейки прямо перед ним (`+\⏎2`: токен `2` у него начинается с `\`); у нас склейки — trivia, и оракул сдвигает начала диапазонов Clang за склейки.
> - **Идентификаторы:** Unicode XID_Start/XID_Continue (по категориям Unicode и Other_ID_Start/Other_ID_Continue), `_` и `$`,
>   UCN `\uXXXX`, `\UXXXXXXXX`, `\u{…}` — значение декодируется (`caf\u00e9` == `café`). `\N{…}` принимается, но не декодируется: в .NET нет таблицы имён Unicode.
> - **Значения литералов** (`Parser/Literals.cs`): целые — `ulong` (`null` при переполнении 64 бит), плавающие — `double` (включая hex-float),
>   символы — значение кодовой единицы (`long`, многосимвольные — по правилу GCC/Clang), строки — `byte[]` UTF-8 для обычных и `u8`, `string` для `u`/`U`/`L`.
>   Все escape C++23, включая `\x{…}`, `\o{…}`, `\u{…}` и расширение `\e`. `Suffix` и `UserDefinedSuffix` разделены.
> - **AST:** у `LiteralExpression` — `Value`, `Encoding` (`CharacterEncoding`), `IsRaw`, `Suffix`, `UserDefinedSuffix`;
>   новый `ConcatenatedStringExpression` (части, значение в общей кодировке — префиксе частей).
> - **Оракул:**
>   - `ClangAst.RawTokens` понимает флаг `[UnClean='…']` склеенных токенов;
>   - `ClangNode` хранит `value` и тип;
>   - `CppKindMap.RuleFor(node, parent)`: части конкатенации проверяются по границам токенов, сама конкатенация — как `StringLiteral`;
>   - новый `CppLiteralChecker` сравнивает значения с Clang: целые и символы точно (для `char` допускается знаковое расширение),
>     плавающие — по типу (`float`, `double`, `long double`), строки — через то же представление, что пишет Clang (`StringLiteral::outputString`).
>     Новый статус `OracleStatus.ValueMismatch`.
> - **Корпус:** `01-Lexical.cpp` переписан под грамматику этапа 0 (только объявления со встроенными типами): все виды литералов и escape,
>   конкатенации с разными кодировками, Unicode и UCN-идентификаторы, альтернативные токены, диграфы `<% %>`, склейки в идентификаторах,
>   числах, операторах, строках и ключевых словах. `<: :>`, `%:` и `<::` проверяются юнит-тестами и файлами следующих этапов.
> - **База:** 2/20 (`00-Basics.cpp`, `01-Lexical.cpp`) с проверкой значений. Встроенный C#-корпус пополнился новыми `.cs`-файлами репозитория.
> - **Ограничения:** склейка внутри начала комментария (`/\⏎/`) не поддерживается; `\N{…}` не декодируется (у таких литералов `Value == null`).
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

> **Статус: выполнен.**
> - **Проход по директивам** (`Parser/Preprocessor.cs`). В C++ `#define` может стоять где угодно, поэтому состояние макросов нельзя
>   восстановить по позиции, как в C#. Перед первым токеном один проход по файлу в порядке исходника обрабатывает директивы и
>   записывает для каждой, где продолжаются trivia: конец строки директивы или, если директива открывает неактивную ветку, следующая
>   обработанная директива. `SyntaxParser.ScanToken` пропускает директивы и ветки по этой таблице, поэтому ленивые токены по-прежнему
>   сканируются в любом порядке. Файл без `#` и `%:` не обрабатывается.
> - **Начало директивы** — `#` или `%:` первым токеном строки. Проход лексирует литералы (включая многострочные raw-строки),
>   числа и комментарии, так что `#` в них директивой не считается. Как в Clang, блочный комментарий не меняет признак начала строки:
>   `int a; /*⏎*/ #define` — не директива, а `/*⏎*/ #define` в начале строки — директива. Директива кончается в конце логической
>   строки; блочный комментарий внутри неё может занимать несколько строк.
> - **Условия** (`Preprocessor.Conditions.cs`): `#if`/`#ifdef`/`#ifndef`/`#elif`/`#elifdef`/`#elifndef`/`#else`/`#endif`, вложенность
>   в неактивных ветках. Выражение — `intmax_t`/`uintmax_t` со всеми операторами C, `?:`, запятой, символами и числами с суффиксами,
>   `true`/`false`, альтернативными токенами; прочие имена — 0; деление на ноль в вычисляемом операнде делает условие ложным.
> - **Макросы** (`Preprocessor.Macros.cs`) раскрываются в условиях по алгоритму с hide set: object-like и function-like, `#`, `##`,
>   `__VA_ARGS__`, именованный вариадический параметр, `__VA_OPT__`, `defined` (в том числе из раскрытия), `__LINE__`, `__COUNTER__`.
>   Макросы опций (предопределённые Clang и `-D`) токенизируются один раз на объект `CppParseOptions`; имя вида `F(x)` задаёт
>   function-like макрос.
> - **Проверки возможностей:** `__has_include`/`__has_include_next` ищут файл в `CppParseOptions.SourceDirectory` (для `"…"`) и
>   `IncludeDirectories` (новые опции); `__has_cpp_attribute` — таблица стандартных атрибутов со значениями Clang 18, `gnu::`/`clang::` — 1;
>   `__has_builtin`, `__has_feature`/`__has_extension`, `__has_attribute` — таблицы и шаблоны имён, приближающие Clang;
>   `__has_warning` — 1; `__is_identifier` — не ключевое слово.
> - **AST:** `PreprocessorDirective` (`Kind`, `Name`, `Arguments`, `Text`, `IsBranchTaken`, `Macro` — `MacroDefinition` для `#define`).
>   `TranslationUnit.Directives` — все обработанные директивы. Остальные (не условные) директивы — trivia для Writer: `CppNode.LeadingDirectives`
>   у всех узлов, начинающихся со следующего токена (ставит `Finish`), `CompoundStatement.CloseBraceDirectives`, `TranslationUnit.EndDirectives`.
>   `CppWriter` печатает их дословно на отдельных строках, каждую один раз. Условные директивы не печатаются: в дереве только активный код.
>   **Отклонение от плана:** директивы не становятся элементами списков объявлений и операторов — одна модель «trivia перед узлом» покрывает
>   и списки, и середину выражений (`1 +⏎#define TWO 2⏎TWO`).
> - **Оракул:**
>   - `ClangOracleOptions.ParseOptions` передаёт парсеру системные каталоги include Clang (`Clang.SystemIncludeDirectories`, из `-E -v`),
>     каталоги `-I` и рабочую папку;
>   - `CppKindMap`: `PreprocessorDirective` проверяется по границам сырых токенов; `CppSpanChecker` не обходит `LeadingDirectives`;
>   - `WithoutSemicolon` не учитывает токены директив и неактивных веток;
>   - узлы из раскрытия макроса у Clang имеют позицию имени макроса: `ClangAst` продлевает конец такого узла до конца вызова (после аргументов),
>     а наши узлы строго внутри вызова макроса не сверяются (узлы Clang из раскрытия уже принимались любого вида); `CppLiteralChecker`
>     не сверяет литералы из раскрытий.
> - **Корпус:** `02-Preprocessor.cpp` переписан под текущую грамматику: guard, `#pragma once`, `#include`, условия со всеми видами директив,
>   раскрытие макросов в условиях, `__has_include`, директивы с отступом, через `%:`, со склейками и многострочными комментариями,
>   `#` в raw-строке, директивы внутри функций, списка параметров и выражения, перед `}`, `#line`.
> - **База:** 3/20 (`00-Basics.cpp`, `01-Lexical.cpp`, `02-Preprocessor.cpp`). Остальные файлы останавливаются на `struct`, `template`,
>   `namespace`, `enum`, `[[` — ни один больше не останавливается на директиве. Юнит-тесты: `CppPreprocessorTests.cs`.
> - Проход по 4891 файлу `/usr/include` (72 МБ) — без исключений, около 2 с в Release.
> - **Ограничения:** макросы из включённых заголовков неизвестны; директива перед токеном, который не начинает узел (кроме `}` и конца файла),
>   есть только в `TranslationUnit.Directives` и не печатается Writer'ом; `#embed` и `__has_embed` не поддерживаются (их нет в Clang 18 для C++).
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

> **Статус: выполнен.**
> - **Имена** (`Parser/SyntaxParser.Names.cs`, AST `Name`): `IdentifierName`, `TemplateIdName` (аргументы — `TypeId` или `Expression`),
>   `OperatorFunctionName` (включая `new[]`, `delete[]`, `()`, `[]`, `co_await`, составные `>>=`/`>=`), `ConversionFunctionName`,
>   `LiteralOperatorName`, `DestructorName`, `DecltypeName` (квалификатор `decltype(x)::`), `QualifiedName` (вложенность слева,
>   `Qualifier == null` — глобальный `::`, `IsTemplate` — `T::template f<int>`). `NameExpression` и `NameDeclarator` хранят `Name`.
>   `::` перед `*` (указатель на член) остаётся вызывающему.
> - **Аргументы шаблона:** type-id, если разбирается как тип и за ним `,`, `>` или `...` ([temp.arg]), иначе выражение; имя, известное как
>   значение, — выражение. Внутри аргументов `>`, `>>`, `>>=` вне скобок закрывают список (`_inTemplateArguments`), `>=` — оператор.
>   `<` после имени начинает аргументы: в типе — всегда; в выражении — у известного шаблона или концепта (`Symbols`,
>   `CppParseOptions.TemplateNames`); у неизвестного имени — если после `>` идёт `::` или внутри `requires`.
>   Неудачные попытки разобрать `<…>` запоминаются по позиции: цепочки `a < b < c …` иначе разбирались бы экспоненциально долго.
> - **Спецификаторы** (`SyntaxParser.Types.cs`): любой порядок; имя становится спецификатором типа, только пока другого типа нет
>   (`unsigned x` объявляет `x`), и не становится, если это конструктор, деструктор или функция преобразования (`S::S`, `S::~S`,
>   `S::operator int`) — у таких объявлений `Specifiers == null`. Новые узлы: `NamedTypeSpecifier` (`IsTypename`), `ElaboratedTypeSpecifier`,
>   `DecltypeSpecifier` (`decltype(auto)` — без выражения), `PlaceholderTypeSpecifier` (`C auto`, `C decltype(auto)`); `typedef`, `friend` и
>   GNU-типы `__int128`, `__float128`, `_Float16`, `__bf16` — `KeywordSpecifier`. `TypeId` — спецификаторы и абстрактный декларатор.
> - **Деклараторы** (`SyntaxParser.Declarators.cs`) вложены как в грамматике: `PointerDeclarator` (cv), `ReferenceDeclarator`,
>   `MemberPointerDeclarator`, `ArrayDeclarator`, `FunctionDeclarator` (`IsVariadic` для `, ...`, `...` и `(...)`, cv, ref-qualifier,
>   `NoexceptSpecifier`, trailing return type), `ParenthesizedDeclarator`, `PackDeclarator` (`Ts&&... args`); у абстрактного декларатора
>   внутренний `Inner == null`. `(` в начале декларатора параметра — вложенный декларатор, если дальше указатель или имя, не известное как тип
>   ([dcl.ambig.res]: `int (x)` и `int (T)`). `requires` после декларатора — `InitDeclarator.RequiresClause`/`FunctionDefinition.RequiresClause`.
>   Тело функции следует, если первым к имени применён список параметров (`int (*f())[3] { … }`).
> - **Объявления:** `struct X;` — `SimpleDeclaration` без деклараторов; `typedef`. **Таблица символов** `Parser/Symbols.cs`: области
>   (файл, функция, блок), виды имён (значение, тип, шаблон, концепт, namespace), откат спекулятивных разборов (`Checkpoint`/`Rollback`).
>   Объявляются: имена деклараторов (тип после `typedef`) — сразу после декларатора, до инициализатора; имена из `struct X`; параметры —
>   в области тела. Квалифицированное имя ищется по последнему идентификатору. Новые опции `CppParseOptions.TypeNames`/`TemplateNames`.
> - **Объявление или выражение в блоке:** объявление, если оператор начинается с ключевого слова-спецификатора или с имени, известного как тип;
>   для неизвестного имени — если дальше имя (`X y`), `X *y;`/`X &y =`, `::*` или аргументы шаблона и имя (`vector<int> v;`). Сначала
>   пробуется объявление, при неудаче — выражение. Полное [stmt.ambig] — этап 5.
> - **Writer** печатает имена, спецификаторы, деклараторы, `...`, квалификаторы, `noexcept`, `->`, `requires`.
> - **Оракул:**
>   - `CppKindMap.RuleFor(node, Ancestry)` получает цепочку предков;
>   - выражения внутри типов (границы массивов, `decltype`, аргументы шаблонов, `noexcept`) проверяются по токенам: Clang их не выводит;
>   - `ParameterDeclaration` ↔ `ParmVarDecl`, только если декларатор функции применён прямо к объявленному имени и объявление — не typedef,
>     параметр или type-id (у `int (*f)(int)` и параметров параметров их нет), и не `(void)`;
>   - `InitDeclarator` ↔ ещё и `TypedefDecl`; место объявления — неквалифицированное имя, `operator` или `~`;
>   - нормализация игнорирует адрес `typeAliasDeclId`.
> - **Корпус:** `03-Types.cpp` и `04-Declarators.cpp` переписаны под грамматику этапа: вместо определений классов — неполные типы и `typedef`.
>   Определения классов, `&S::member`, `sizeof(type-id)` и `{}`-инициализация из прежних версий покрываются файлами этапов 4–6 (`05`, `08`, `16`).
> - **База:** 5/20 (`00`–`04`). Юнит-тесты: `CppDeclaratorTests.cs`.
> - **Ограничения:** абстрактный пакет `Ts...` в параметрах читается как многоточие вариадической функции — различить их можно,
>   только зная пакеты параметров шаблона (этап 6); инициализаторы `()` и `{}` — этапы 4–6; члены namespace и классов в таблицу символов
>   не попадают (этап 6).
- nested-name-specifier, включая `::`, `decltype(...)::` и `template` в квалификаторе.
- template-id; operator-function-id, conversion-function-id, literal-operator-id; деструктор `~T`.
- decl-specifier-seq в любом порядке: cv, `signed`/`unsigned`/`long long`, `auto`, `decltype(auto)`, elaborated `struct X`, `typename T::x`, placeholder с concept `std::integral auto`.
- Деклараторы:
  - указатели, ссылки, `&&`, указатели на член `C::*`;
  - массивы, функции с cv/ref-qualifier, `noexcept`, trailing return, `requires` после декларатора;
  - вложенные `(*f)(int)`, абстрактные деклараторы, pack `...`.
- `Symbols.cs`: области видимости и объявление имён по ходу разбора, эвристики для неизвестных имён.

### Этап 4. Выражения

> **Статус: выполнен.**
> - **Операторы** (`Parser/SyntaxParser.Expressions.cs`): подъём по приоритетам C++23, включая `<=>`, `.*`, `->*` и запятую; присваивание
>   и `?:` правоассоциативны, правый операнд присваивания может быть braced-init-list; `throw` (и без операнда), `co_yield`, `co_await`,
>   GNU `a ?: b` (`ConditionalExpression.WhenTrue == null`). Оператор, за которым идёт `...`, завершает операнд fold-выражения.
> - **Postfix:** вызовы, subscript с любым числом аргументов (C++23, в том числе `a[]` и `a[{1}]`), доступ к членам (`a.template f<int>`,
>   `a.Base::m`, псевдодеструктор `p->~T()`), `++`/`--`; `[[` никогда не subscript.
> - **Primary:** `this`, fold-выражения `(xs op ...)`, `(... op xs)`, `(e op ... op xs)` со скобками (как `CXXFoldExpr` у Clang), лямбды,
>   requires-выражения, `*_cast<>`, `typeid`, функциональные касты `int(x)`, `T{x}`, `auto(x)`, `typename T::x()`, `decltype(x)(y)`;
>   `sizeof`/`sizeof...`/`alignof` (и `_Alignof`, `__alignof`, `__alignof__`), `noexcept(e)`, `new` (placement, `new (T)`, массивы с любым
>   первым размером, `()`/`{}`) и `delete`/`delete[]`, `::new`/`::delete`. Pack expansion `x...` в аргументах, списках и аргументах шаблона.
> - **Braced-init-list** (`InitializerListExpression`): вложенные списки, завершающая запятая, designated initializers `.x = 1`, `.x{1}`,
>   расширения C99 `.a.b` и `[i] =`. Инициализаторы объявлений: `EqualsInitializer` (значение может быть списком),
>   `ParenthesizedInitializer`, `BracedInitializer`; те же узлы у `new`, функциональных кастов и init-capture.
> - **Лямбды** (`SyntaxParser.Lambdas.cs`): capture-default, захваты `x`, `&x`, `x...`, `this`, `*this`, init-capture `x = e`, `&x = e`,
>   `...xs = e`, `x{e}`, `x(e)`; template-параметры и `requires` после них; атрибуты до параметров и после спецификаторов;
>   `mutable`/`constexpr`/`consteval`/`static` в любом порядке и без скобок (C++23); `noexcept`, trailing return, `requires` после декларатора.
>   Параметры шаблона, параметры и init-capture объявляются в области лямбды.
> - **requires-выражения:** параметры в своей области; simple, type, compound (`{ e } noexcept -> C<int>`) и nested requirement.
>   `requires`-clause теперь разбирается по грамматике (constraint-logical-or-expression из primary-выражений), поэтому в
>   `[]<class T> requires (sizeof(T) > 1) (T x)` скобки параметров не становятся вызовом.
> - **Параметры шаблона** (`SyntaxParser.Templates.cs`) сделаны заранее для лямбд и пригодятся на этапе 6: type (`typename`/`class`, pack,
>   default), non-type (`NonTypeTemplateParameter` вокруг `ParameterDeclaration`), template template, constrained (`std::integral T`).
> - **Атрибуты** (`SyntaxParser.Attributes.cs`): `[[using ns: a, ns::b(аргументы)...]]`; аргументы хранятся текстом из исходника.
>   Класс называется `CppAttribute`, чтобы не конфликтовать с `System.Attribute`.
> - **Дизамбигуации:**
>   - C-cast или скобки: `(T)x` — каст, если type-id заведомо тип (ключевое слово, указатель или ссылка, имя, известное как тип).
>     Неизвестное имя (из заголовка) — тип, если за `)` идёт идентификатор, литерал или ключевое слово вроде `sizeof`: `(size_t)n` — каст,
>     `(x)(y)` — вызов, `(x) - y` — вычитание. Если после заведомого типа операнд не разбирается, это выражение: `(T())`.
>   - `sizeof(T)`/`typeid(T)` или выражение: type-id, если это не имя значения; неизвестное имя — тип.
>   - `<` у неизвестного имени — аргументы шаблона, если за `>` идёт токен, который не может продолжить сравнение `a < b > c`:
>     `(`, `)`, `[`, `]`, `{`, `}`, `;`, `,`, `:`, `?`, `==`, `!=`, `&&`, `||`, `|`, `^`, `...`, `::`.
>   - Имя перед `{` — тип (функциональный каст), если оно не значение; перед `(` — только если известно как тип (иначе вызов:
>     `std::string("a")` из заголовка — вызов). В `requires`-clause `{` после ограничения начинает тело функции.
>   - Имя, известное как значение, не становится спецификатором типа: `int a(b);` объявляет переменную, если `b` — переменная.
>   - `X && y;` и `X & y;` с неизвестным `X` — выражения: ссылке нужен инициализатор (`X &y = z;` — объявление).
>   - В параметрах шаблона неизвестное имя — концепт, если оно не кончается на `_t` (`std::size_t N` — non-type).
> - **Таблица символов:** `Lookup` для template-id с неизвестным шаблоном возвращает «неизвестно», а не «тип», поэтому `get<0>(a);` —
>   выражение. `Mark` сохраняет и `_inConstraint`; скобки (`EnterBrackets`) сбрасывают оба флага контекста.
> - **Writer** печатает все новые узлы; общие части (`WriteParameters`, `WriteNoexceptSpecifier`, `WriteDeclSpecifier`) вынесены.
> - **Оракул:**
>   - правила для всех новых узлов в `CppKindMap`; `ExactRule.OrAbsent` — узел без узла Clang с тем же span проверяется по токенам
>     (у Clang нет `InitListExpr` для скобок вызова конструктора); `ExactRule.ClangStart` — Clang начинает constrained-параметр с
>     неквалифицированного концепта (`integral T` в `std::integral T`);
>   - `DeclarationRule.OtherEnd`: `VarDecl` с `(…)`-инициализатором скалярного типа кончается на последнем аргументе (`int a(1`);
>   - параметры лямбд и requires-выражений ↔ `ParmVarDecl`; выражения в designator проверяются по токенам;
>   - `NameExpression` ↔ ещё `PredefinedExpr` (`__func__`) и `ConceptSpecializationExpr`.
> - **Корпус:** `05-Expressions.cpp` и `06-Lambdas.cpp` переписаны под грамматику этапа: без определений классов, члены — через классы
>   стандартной библиотеки (`std::type_info`, `std::initializer_list`), указатели на члены — `&std::type_info::before` и
>   `&decltype(lambda)::operator()`; fold-выражения, `sizeof...` и requires-выражения — в обобщённых лямбдах. Выражения с классами файла и
>   лямбды с `this` перенесены в конец `08-Classes.cpp` (этап 6).
> - **База:** 7/20 (`00`–`06`). Юнит-тесты: `CppExpressionTests.cs` (приоритеты, касты, `sizeof`, аргументы шаблонов, функциональные
>   касты, `new`, лямбды, параметры шаблона, fold, pack expansion, requires, списки, инициализаторы, round-trip Writer'а, оракул на фрагментах).
> - Проход по 4891 файлу `/usr/include`: без исключений, около 3 с в Release; все файлы, которые разбирались на этапе 3, разбираются.
> - **Ограничения:** `co_await`/`co_yield` проверены только юнит-тестами (для оракула нужен тип корутины — этап 6); символы не различают
>   шаблоны классов и функций, поэтому известный template-id перед `(` — вызов, а не функциональный каст; GNU statement expressions
>   `({ … })`, compound literals `(T){…}`, `&&label` и встроенные функции с типами в аргументах (`__builtin_offsetof`, `__builtin_va_arg`)
>   не поддерживаются.

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

> **Статус: выполнен.**
> - **Диспетчер** (`Parser/SyntaxParser.Statements.cs`) по первому токену: ключевое слово оператора, `{`, `;`, метка
>   (`идентификатор :`), `[[`; всё остальное — объявление или выражение.
> - **Операторы и AST:**
>   - `IfStatement` — `InitStatement`, условие (`Expression` или `ConditionDeclaration`), `IsConstexpr`, `IsConsteval`/`IsNegated`
>     для `if consteval`/`if !consteval` (условия нет); `SwitchStatement` с init-statement; `CaseStatement` (`RangeEnd` —
>     GNU `case 1 ... 3:`), `DefaultStatement`, `LabeledStatement`; у меток в конце блока (C++23: `end: }`) оператор `null`.
>   - `WhileStatement`, `DoStatement`, `ForStatement` (у `for (;;)` нет init-statement, как у Clang), `RangeForStatement`
>     (`ForRangeDeclaration`, диапазон — выражение или braced-init-list, init-statement).
>   - `BreakStatement`, `ContinueStatement`, `ReturnStatement`, `CoReturnStatement`, `GotoStatement`, `TryStatement` с `CatchClause`
>     (объявление — `ParameterDeclaration`, `null` у `catch (...)`), `AttributedStatement`.
>   - Структурные привязки — декларатор `StructuredBindingDeclarator` (в том числе под `&`/`&&` и в range-`for`);
>     `StaticAssertDeclaration` — в блоке и на уровне namespace; `DeclarationStatement.Declaration` теперь `Declaration`.
>   - Атрибуты: у объявлений — `SimpleDeclaration.Attributes`/`FunctionDefinition.Attributes` (Clang включает их в `DeclStmt`),
>     у остальных операторов — `AttributedStatement`, у метки — тоже `AttributedStatement` вокруг `LabeledStatement`.
> - **[stmt.ambig]:** оператор, который начинается с ключевого слова-спецификатора или известного типа, сначала разбирается
>   как объявление, при неудаче — как выражение; с `typedef int T;` `T(x);`, `T(*p);`, `T(c) = 7;`, `T(g)(int);`, `T(f), h;` —
>   объявления, `T(a) + 1;`, `T(a)->m;`, `T{a};` — выражения. Для неизвестного имени добавлено `X const y`/`X volatile`.
>   Условие — объявление, если начинается как объявление и после декларатора идёт `=` или `{`. Init-statement есть, если в скобках
>   `if`/`switch`/`for` есть `;` вне скобок; `for` с двумя такими `;` — обычный, иначе range-based.
> - **Области видимости:** у `if`/`switch`/`while`/`for` своя область для init-statement и условия, у каждого подоператора —
>   своя, у обработчика — область параметра; имена привязок — значения; переменная range-`for` не видна в диапазоне.
> - **Writer** печатает все операторы; `else if` — на одной строке.
> - **Оракул:**
>   - правила для всех новых узлов: операторы, которые кончаются `;`, — `WithoutSemicolon`; `AttributedStatement` — `OrAbsent`
>     (Clang отбрасывает неизвестные атрибуты вместе с `AttributedStmt`; атрибуты метки принадлежат `LabelDecl`, и `LabelStmt`
>     начинается после них);
>   - `ConditionDeclaration` ↔ `VarDecl`; `ForRangeDeclaration` ↔ `VarDecl`/`DecompositionDecl` с новым `DeclarationRule.AnyEnd`
>     (Clang заканчивает переменную range-`for` на `:`); декларация привязки находится на `[`, имена ↔ `BindingDecl`;
>     параметр `catch` ↔ `VarDecl`; `FunctionDefinition` с атрибутами начинается у Clang после них (`ClangStart`);
>   - нормализация игнорирует адреса `targetLabelDeclId` и `declId` меток.
> - **Корпус:** `07-Statements.cpp` переписан под грамматику этапа: вместо классов — массивы и `std::initializer_list`,
>   привязки к массивам. Range-`for` по классу, привязки к полям и function-try-block перенесены в конец `08-Classes.cpp` (этап 6).
> - **База:** 8/20 (`00`–`07`). Юнит-тесты: `CppStatementTests.cs` (все операторы, области видимости, [stmt.ambig], round-trip
>   Writer'а, оракул на фрагментах).
> - Проход по 4891 файлу `/usr/include`: без исключений, около 2,5 с в Release; разбирается 1555 файлов (на этапе 4 — 1553),
>   все прежние в их числе.
> - **Ограничения:** alias-declaration в init-statement (`for (using T = int; …)`), `using`-объявления, локальные классы и
>   function-try-block — этап 6; GNU `asm`, `__label__`, computed goto `goto *p;` не поддерживаются; `co_return` проверен только
>   юнит-тестами (оракулу нужен тип корутины — этап 6).
- Диспетчер по первому токену. «Объявление или выражение» решается по [stmt.ambig] с опорой на таблицу символов.
- Операторы:
  - `if`/`if constexpr`/`if consteval` с init-statement; `switch`;
  - `for` и range-`for` с init-statement, `while`, `do`;
  - `break`, `continue`, `return`, `goto`, метки, `case`/`default`;
  - `try`/`catch(...)`, `co_return`, структурные привязки `auto& [a, b] =`, `static_assert` в блоке, атрибуты у операторов.

### Этап 6. Объявления и единица трансляции

> **Статус: выполнен.**
> - **Диспетчер объявлений** (`Parser/SyntaxParser.Declarations.cs`): `ParseDeclaration(DeclarationContext)` — namespace, класс
>   или блок; по первому токену — `namespace`, `using`, `template`, `extern template`, `extern "C"`, `export`, `asm`, спецификаторы
>   доступа, пустое объявление `;`; иначе атрибуты, спецификаторы, деклараторы. Модули (`SyntaxParser.Namespaces.cs`) распознаются
>   на уровне единицы трансляции: `module` и `import` — идентификаторы, если за ними не идут имя модуля, `;`, `:` или имя заголовка.
> - **AST:** `NamespaceDefinition` (вложенные имена `a::inline b`, `inline`, безымянные, атрибуты), `NamespaceAliasDefinition`,
>   `UsingDirective`, `UsingDeclaration` (`UsingDeclarator` с `typename` и `...`), `UsingEnumDeclaration`, `AliasDeclaration`,
>   `LinkageSpecification`, `TemplateDeclaration` (без параметров — явная специализация), `ExplicitInstantiation` (`IsExtern`),
>   `ConceptDefinition`, `AsmDeclaration`, `ModuleDeclaration` (`module;`, `export module a.b:part`, `module :private;`),
>   `ImportDeclaration`, `ExportDeclaration`, `EmptyDeclaration`, `AccessSpecifier`. Спецификаторы: `ClassSpecifier` (ключ,
>   атрибуты, имя — в том числе квалифицированное и template-id, `final`, `BaseSpecifier` с доступом, `virtual` и `...`, члены),
>   `EnumSpecifier` (scoped, базовый тип, opaque-объявление, `Enumerator` с атрибутами), `ExplicitSpecifier` (`explicit(bool)`),
>   `AttributeDeclSpecifier`. Члены: у `InitDeclarator` — ширина битового поля (безымянное поле — без декларатора),
>   `override`/`final`, `= 0`, GNU asm-метка и атрибуты; у `FunctionDefinition` — ctor-initializer (`MemberInitializer`),
>   function-try-block (`Handlers`), `= default`/`= delete`, virt-specifiers. Атрибуты: `AlignasSpecifier`, `GnuAttributeSpecifier`
>   (аргументы текстом), атрибуты после имени декларатора (`NameDeclarator.Attributes`) и у параметров, `this` явного объектного
>   параметра, `__restrict` как cv-квалификатор, `throw()` как `NoexceptSpecifier.IsThrow`.
> - **Разбор:** конструктор — имя класса перед `(`, кроме `S (*f)()`; deduction guide — известный шаблон, `( … ) ->`; у них и у
>   деструкторов нет спецификаторов, и имя не объявляется значением. Определение класса или перечисления — только в объявлении,
>   когда за заголовком идёт `{` или `:` (у scoped enum и `;`), иначе elaborated. Битовое поле — `:` после декларатора члена
>   (ширина — conditional-expression, затем инициализатор). Абстрактный пакет `Ts...` в параметрах — пакет, если тип называет пакет
>   параметров шаблона (ограничение этапа 3 снято), иначе многоточие. В аргументах шаблона `>` после неизвестного `a<b>` закрывает
>   template-id (`bool = is_integral_v<T>>`). Известный template-id шаблона класса перед `(` — функциональный каст.
> - **Таблица символов** (`Parser/Symbols.cs`) переписана: области namespace, классов и перечислений сохраняются и открываются
>   повторно; квалифицированные имена ищутся в области квалификатора (при неизвестном квалификаторе — по последнему идентификатору,
>   как раньше); using-директивы, `using enum`, inline и безымянные namespace, известные базы классов и безымянные классы делают
>   имена видимыми; псевдонимы namespace; тело функции, определённой вне класса (`int S::f()`), видит имена `S`. Параметры шаблона —
>   в отдельной области; сущность, объявленная шаблоном, — `Template`. Пакеты помечаются. `using`-объявление объявляет имя с видом из
>   его области. **Отклонение:** тела функций-членов разбираются сразу, а не после класса: члены, объявленные ниже, неизвестны.
> - **Writer** печатает все новые узлы; члены класса — с отступом, спецификаторы доступа — с отступом класса.
> - **Оракул:**
>   - правила для всех новых узлов в `CppKindMap`; `ClassSpecifier` ↔ `CXXRecordDecl` и специализации, `TemplateDeclaration` ↔
>     шаблоны и то, что Clang начинает с `template` (специализации, члены шаблонов вне класса), `UsingDeclarator` — по имени;
>     функция и класс внутри шаблона могут начинаться у внешнего `template` (`ClangStart`);
>   - новое `ExactRule.ClangEndBefore`: Clang заканчивает параметр и перечислитель до атрибутов после имени, безымянный пакет —
>     на `typename`; объявление — до asm-метки и GNU-атрибутов (`DeclarationRule.OtherEnd`);
>   - обходы особенностей Clang 18: вызов функции с явным объектным параметром (`obj.f()`) начинается у `f` — `ClangAst` исправляет
>     начало такого вызова и выражений, которые с него начинаются, а `MemberAccessExpression`-callee может не иметь узла;
>     alias-declaration в init-statement не имеет `DeclStmt` (или он кончается после `;`); параметры шаблона класса у члена,
>     определённого вне класса, и внутренний `template <>` не выводятся; у явного инстанцирования нет объявлений по именам;
>   - сырые токены `>>`, `>=`, `>>=` делятся на `>`, как у парсера; нормализация игнорирует адрес `temp`.
> - **Корпус:** файлы `08`–`19` проходят без изменений; новые `20-Declarations.cpp` (члены шаблонов вне класса, вложенные шаблоны,
>   классы-члены вне класса, пакеты абстрактных параметров, `using` в блоках, локальные классы, `throw()`), `21-Modules.cpp`
>   (глобальный фрагмент, `export`, `module :private`; `import` требует собранных модулей и проверен юнит-тестами),
>   `22-GnuExtensions.cpp` (`__attribute__`, asm-объявления и метки, `__restrict`).
> - **База:** 23/23 — встроенный корпус проходит полностью. Юнит-тесты: `CppDeclarationTests.cs`.
> - Проход по 4891 файлу `/usr/include`: без исключений, около 4 с в Release; разбирается 2784 файла (на этапе 5 — 1555), все прежние
>   в их числе. Почти все оставшиеся неудачи — макросы в синтаксических позициях (`_GLIBCXX_VISIBILITY`, `__THROW`, `G_BEGIN_DECLS`) — этап 7.
> - **Ограничения:** GNU-атрибуты после декларатора параметра, `__decltype`, `__extension__`, `throw(T)` (ошибка в C++17+),
>   определения классов в alias-declaration (`using X = struct { … };`) и отложенный разбор тел функций-членов не поддерживаются.
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

> **Статус: выполнен.**
> - **Корпуса.** `src/PaspanParsers.Tests/Cpp/fetch-external-corpora.sh DIR` клонирует `{fmt}`, `nlohmann/json` (single_include) и
>   LLVM (`llvm/include`, `llvm/lib/Support`, sparse checkout), пишет заголовки `llvm/Config/*.h` из их `.cmake`-шаблонов с настройками
>   x86-64 Linux и печатает команды прогона. `CPP_CORPUS_DIR` принимает несколько каталогов через разделитель путей (`fmt/include/fmt` и `fmt/src`).
> - **Потоковое чтение дампа.** JSON-дамп Clang для файла, включающего много LLVM, больше 2 ГБ (`OutOfMemoryException` в `MemoryStream`).
>   `Clang.DumpAst` читает stdout Clang во время записи (`ClangAst.Read(Stream)`, `Utf8JsonReader` с `JsonReaderState`): декларация
>   строится как `JsonNode`, пока не прочитан её `loc`, декларации заголовков дальше только просматриваются. Опущенные Clang имена файлов
>   позиций восстанавливаются по предыдущей позиции.
> - **Режим «с именами»** (`ClangOracleOptions.Headers`, `HeaderKnowledge`):
>   - `ClangAst` собирает из деклараций заголовков (без тел функций и выражений) имена типов, шаблонов классов и псевдонимов, шаблонов
>     функций и переменных, концептов и членов классов (переменные и функции, с членами баз по `bases`). Тип, который заголовки объявляют
>     и функцией, переменной или шаблоном функции вне класса (`struct stat` и `stat()`), в неквалифицированные имена типов не попадает;
>     члены классов, функции внутри `FunctionTemplateDecl` и перечислители `enum class` типы не прячут; конструктор-шаблон и шаблон члена,
>     определённый вне класса, не делают имя шаблоном функции. Каждое имя добавляется и квалифицированным своими namespace и классами
>     без inline и безымянных namespace (`std::system_error`, `llvm::json::Array`): квалифицированное имя кода, чей квалификатор файлу
>     неизвестен или является namespace файла без этого имени, сначала ищется целиком, потом по последнему идентификатору;
>   - `ClangPreprocessor` (`clang -E -dD`) даёт макросы заголовков для `#if` (опции парсера определяют их до файла) и вариант файла с
>     раскрытыми макросами: строки файла сохранены, условные директивы вычислены, `#define`/`#undef`/`#pragma` на месте, `#include` —
>     исходные директивы (включение по полному пути ломает `#include_next` обёрток вроде `stdint.h` Clang);
>   - новые опции парсера: `CppParseOptions.FunctionTemplateNames` (шаблоны функций и переменных: их template-id — выражения;
>     `TemplateNames` теперь — шаблоны классов и псевдонимов, их template-id — типы), `ConceptNames`, `ClassMembers` (члены классов заголовков:
>     их видят тела функций-членов, определённых в файле, `void raw_ostream::f() { indent(2); }`, и производные классы файла).
> - **Отчёт внешнего корпуса.** Неудачный файл проверяется ещё раз с именами и макросами заголовков, затем с раскрытыми макросами (и с
>   именами). Категории: «эвристики неизвестных имён или условия на макросах заголовков» (проходит с именами), «макросы в синтаксических
>   позициях» (проходит с раскрытыми макросами), «ошибки парсера» (не проходит и так; в отчёте — результат раскрытого варианта),
>   «не классифицировано» (Clang не принимает раскрытый вариант). Три процента: как есть, с именами, с раскрытыми макросами.
> - **Исправления парсера**, найденные на реальном коде:
>   - тела функций-членов, определённых в классе, пропускаются по парным скобкам и разбираются, когда самый внешний класс завершён
>     (complete-class context), в сохранённых областях (вложенные классы, параметры шаблонов членов); ctor-initializer разбирается сразу.
>     Ошибка в отложенном теле указывается в нём, а не в конце класса. Ограничение этапа 6 снято;
>   - `SymbolKind.ValueTemplate`: шаблоны функций и переменных отделены от шаблонов классов — `reset_color<Char>(buf);` выражение,
>     `Box<int>(1)` функциональный каст; имя шаблона класса перед `(` — тип (injected-class-name, CTAD);
>     функция, перегружающая шаблон функции, оставляет имя шаблоном;
>   - квалифицированное имя с неизвестным квалификатором ищется только в опциях (`std::equal_to<>{}` не находит `llvm::equal_to`);
>     в известном namespace, где имени нет, — тоже в опциях (namespace мог продолжаться в заголовке); typedef и alias класса делают
>     квалифицированный поиск через себя (`using json = basic_json<>;`, `json::json_pointer`), класс может быть определён после псевдонима;
>   - имя после `.`/`->` перед `<` не прячется переменными файла (`path.leaf<Leaf>()`); квалифицированное имя, известное как значение,
>     не становится спецификатором типа (`basic_json v(value_t::array);`);
>   - неизвестное имя, которое служит типом объявления с именем (`StringRef s;`, `SmallVector<int> v;`), до конца файла — тип
>     (или шаблон класса) в глобальной области: `StringRef(s)` дальше — функциональный каст (откатывается вместе со спекулятивным разбором);
>   - в блоке `T x(y);` с неизвестным `y` (параметры — только неизвестные имена, `(y)`, `(f(x))`, `(g())`) — переменная с инициализатором:
>     `std::lock_guard<std::mutex> lock(mutex);`;
>   - `friend X;` и `friend typename T::type;` без ключа класса; шаблонные аргументы у имён операторов (`operator()<bool>(v)`);
>     параметр шаблона `detail::fixed_string S` — non-type (концепт ищется по квалифицированному имени);
>   - GNU/Clang: `__extension__` в выражениях и перед объявлениями, `_BitInt(N)` (`BitIntSpecifier`), GNU-атрибуты после декларатора
>     определения функции (`FunctionDefinition.DeclaratorAttributes`, `S(const char *s) __attribute((enable_if(…))) : …`), встроенные
>     функции с типами в аргументах (`BuiltinCallExpression`: `__builtin_offsetof`, `__builtin_bit_cast`, `__builtin_va_arg`,
>     `__builtin_convertvector`, `__array_extent`, `__is_lvalue_expr`, трейты типов Clang `__is_same`, `__is_constructible`, …).
> - **Оракул:**
>   - Clang делит `>>`, закрывающий два списка аргументов шаблона, в scratch space — это не макрос; конец узла на таком `>` — один символ;
>   - литералы, созданные препроцессором (`__LINE__`, `__FILE__`, `#x`, их позиции в scratch space), нормализация сравнивает без значения и
>     типа: они зависят от раскладки, которую Writer не сохраняет (`llvm_unreachable`, `assert`); пустой после удаления комментариев `inner` опускается;
>   - правила: `{…}` класса — `CXXConstructExpr`; `__builtin_LINE()` и подобные — `SourceLocExpr`; `BuiltinCallExpression` — `OffsetOfExpr`,
>     `BuiltinBitCastExpr`, `VAArgExpr`, `TypeTraitExpr`, …; функция с атрибутами в шаблоне начинается после атрибутов; удалённый или
>     defaulted шаблон функции и функция, defaulted вне класса, кончаются на деклараторе; псевдоним `_BitInt(N)` кончается на `_BitInt`;
>     `ClangStart` и `ClangEndBefore` пробуются по отдельности и вместе; `ClangEndBefore` — последний токен, кончающийся не позже смещения.
> - **Результаты** (Clang 18, фиксированные ревизии корпусов на 2026-09-27):
>
>   | Корпус | Файлов | Как есть | С именами | С раскрытыми макросами |
>   |---|---|---|---|---|
>   | `{fmt}` 5da4e9a (`include/fmt`, `src`) | 20 | 2 (10,0%) | 2 (10,0%) | 20 (100%) |
>   | `nlohmann/json` 509c070 (`single_include`) | 2 | 0 | 0 | 2 (100%) |
>   | LLVM 20bbd88 `llvm/include/llvm/ADT` | 115 | 73 (63,5%) | 83 (72,2%) | 115 (100%) |
>   | LLVM 20bbd88 `llvm/lib/Support` | 177 | 91 (51,4%) | 151 (85,3%) | 176 (99,4%) |
>   | Всего | 314 | 166 (52,9%) | 236 (75,2%) | 313 (99,7%) |
>
>   Ошибок парсера нет: все файлы, кроме одного, проходят с раскрытыми макросами. `CrashRecoveryContext.cpp` не классифицирован: Clang
>   раскрывает макрос glibc `sa_handler` в раскрытом тексте ещё раз и отвергает его. Неудачи «как есть»: 148 файлов, из них 70 проходят
>   с именами заголовков (эвристики или `#if` на макросах заголовков), 77 — с раскрытыми макросами (`FMT_BEGIN_NAMESPACE`,
>   `NLOHMANN_JSON_NAMESPACE_BEGIN`, `LLVM_ABI`, `LLVM_NO_UNIQUE_ADDRESS`). До этапа (сборка этапа 6, без классификации): ADT 28/115,
>   `{fmt}` 2/20, json 0/2, Support падал с `OutOfMemoryException`. Проход по 4891 файлу `/usr/include`: без исключений, 2788 файлов
>   (на этапе 6 — 2784), все прежние в их числе.
>
> - Юнит-тесты: отложенные тела, `friend`, псевдонимы, расширения GNU/Clang, шаблоны функций и классов, встроенные функции
>   (`CppDeclarationTests`, `CppExpressionTests`); сбор имён, препроцессирование, нормализация, режим с именами (`ClangOracleTests`).
>   Встроенный корпус — 23/23.
> - **Ограничения:** неквалифицированные имена заголовков плоские: имя, которое в одном namespace тип, а в другом функция
>   (`system_error`), без квалификатора неизвестно; члены одноимённых классов разных namespace объединяются; макросы в синтаксических
>   позициях не раскрываются (по плану); члены классов из заголовков известны только в режиме с именами. Раскрытый вариант Clang может
>   раскрыть ещё раз, если макрос заголовка раскрывается в текст со своим именем (`sa_handler` в glibc) — такой файл не классифицируется.
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

> **Статус: выполнен.**
> - **Содержимое span.** Узлы, которых у Clang нет или для которых в JSON-дампе нет диапазонов (типы, деклараторы, спецификаторы,
>   инициализаторы, `ExpressionStatement`, базы, ctor-initializer, параметры шаблонов, атрибуты), проверялись только по границам токенов.
>   Теперь у каждого узла токены его span должны совпадать с тем, что пишет для узла `CppWriter.WriteNode` (`CppNodeText.cs`,
>   `CppSpanChecker.CheckText`): без комментариев, пробелов и склеек строк, диграфы и альтернативные токены — как операторы, UCN в
>   идентификаторах раскодированы, директивы и неактивные ветки пропущены; span директивы — дословно её `Text`. Проверка нашла и
>   исправлены: span имени в `sizeof...(x)` включал `)`; Writer печатал `(int x...)` как `(int x, ...)` (новое
>   `FunctionDeclarator.EllipsisWithoutComma`, так же у лямбд) и asm-метку `__asm__("s")` как `asm("s")` (новое `InitDeclarator.AsmKeyword`).
> - **Writer:** `CppWriter.WriteNode(CppNode)` пишет любой узел отдельно, как в его дереве: декларатор без спецификаторов,
>   инициализатор с `=`, перечислитель, базу, ctor-initializer, требование, захват, обработчик. Из больших методов выделены
>   `WriteEnumerator`, `WriteBaseSpecifier`, `WriteMemberInitializer`, `WriteUsingDeclarator`, `WriteRequirement`, `WriteDesignator`,
>   `WriteAttribute`, `WriteForRangeDeclaration`.
> - **`LineMap` для C++** — общий класс этапа 0 с `unicodeLineBreaks: false`: строки кончаются на `\n`, `\r\n`, `\r`, столбцы — в
>   единицах UTF-16. Позиция `ParseError` считается через него (раньше через `SpanReader`: только `\n`, столбцы в символах). Тест
>   сверяет строки и столбцы всех позиций главного файла в JSON-дампе Clang на корпусе (столбцы Clang — в байтах).
> - **Doc-комментарии.** `Declaration.LeadingTrivia` и `Enumerator.LeadingTrivia` — trivia от конца предыдущего токена. Новый
>   `DocumentationComment` (`Find`, `GetText`) следует правилам, по которым Clang привязывает комментарии к объявлениям
>   (`ASTContext::getRawCommentForDeclNoCache`), выясненным опытами с Clang 18:
>   - doc-комментарии — `///`, `//!`, `/** */`, `/*! */`, также `////` и `/**/`; соседние, разделённые только пробелами и не более
>     чем одним переводом строки, сливаются в один, даже разных видов;
>   - комментарий объявления — последний перед ним, если в тексте между ними нет `;{}#@` (обычные комментарии между ними не мешают,
>     если не содержат этих символов); спецификаторы доступа прозрачны: `/// x` перед `public:` документирует и `public:`, и следующий член;
>   - trailing-комментарий (`///<`, `//!<`, `/**<`, `/*!<`) на строке имени переменной, поля или перечислителя документирует его и
>     важнее комментария перед объявлением; у функций и typedef его нет;
>   - `Find(utf8, declaration)` проверяет текст до начала объявления, `Find(utf8, simpleDeclaration, initDeclarator)` — до имени
>     декларатора, как Clang: `/// Doc.\nint a{1}, b;` документирует `a`, но не `b`.
> - **Оракул:** новый статус `CommentMismatch`; `ClangAst.DocumentationComments` читает `FullComment` деклараций главного файла,
>   `CppDocumentationChecker` сравнивает в обе стороны: наш комментарий узла должен быть у декларации Clang внутри узла, а комментарий
>   Clang — у нашего узла вокруг его декларации, кроме мест, где мы не ищем (внутри декларации до имени, в условиях, обработчиках,
>   захватах). У комментария без текста диапазона у Clang нет, наш должен быть пустым.
> - **Корпус:** новый `23-DocumentationComments.cpp` (группы, виды, разделители, trailing, перечислители, члены, шаблоны,
>   спецификаторы доступа, локальные переменные). База: 24/24.
> - **Внешние корпуса** со всеми проверками (Clang 18, ревизии на 2026-09-28; json и LLVM новее, чем на этапе 7):
>
>   | Корпус | Файлов | Как есть | С именами | С раскрытыми макросами |
>   |---|---|---|---|---|
>   | `{fmt}` 5da4e9a (`include/fmt`, `src`) | 20 | 2 (10,0%) | 2 (10,0%) | 20 (100%) |
>   | `nlohmann/json` 373005f (`single_include`) | 2 | 0 | 0 | 2 (100%) |
>   | LLVM a98dcad4 `llvm/include/llvm/ADT` | 115 | 73 (63,5%) | 82 (71,3%) | 115 (100%) |
>   | LLVM a98dcad4 `llvm/lib/Support` | 178 | 91 (51,1%) | 151 (84,8%) | 177 (99,4%) |
>   | Всего | 315 | 166 (52,7%) | 235 (74,6%) | 314 (99,7%) |
>
>   Новые проверки ничего не отняли: расхождений в содержимом span и doc-комментариях нет. Первый прогон нашёл 16 файлов, где
>   `CommentMismatch` давал сам оракул: диапазон `FullComment` у Clang не включает пустые строки `///` по краям и кончается на
>   последнем абзаце или команде (до блока `\code`, после `\p` в `\p RHS`), поэтому сравнивается начало текста, а конец Clang
>   должен лежать внутри нашего комментария. Неклассифицирован по-прежнему только `CrashRecoveryContext.cpp`.
> - Проход по 4891 файлу `/usr/include`: разбирается 2787 (столько же, сколько до этапа), проверка содержимого span на всех их узлах
>   без расхождений, `DocumentationComment` — без исключений.
> - **Документация:** `src/PaspanParsers/Cpp/README.md` (позиции, `LineMap`, doc-комментарии, Writer, проверки оракула), строка и
>   раздел C++ в корневом `README.md`, `docs/project-structure.md` (дерево решения, разделы `PaspanParsers` и `PaspanParsers.Tests`),
>   раздел «C++ parser oracle» в `CLAUDE.md`.
> - Юнит-тесты: `CppSourceTextTests.cs` (`LineMap` и Clang, позиции ошибок, doc-комментарии, проверка оракула, `WriteNode`).
> - **Ограничения:** комментарии в условиях, обработчиках и захватах лямбд не ищутся; `GetText` оставляет команды Doxygen как есть
>   и не разбирает их; у узлов, построенных в коде, `LeadingTrivia` пуста.
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
   Корпуса этапа 7 скачивает `src/PaspanParsers.Tests/Cpp/fetch-external-corpora.sh DIR` и печатает команды для них.
5. Бенчмарк — с `-c Release` и фильтром `Benchmark_Corpus`.
