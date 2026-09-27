# План: довести C#-парсер PaspanParsers до уровня Roslyn (только для корректных, компилируемых файлов)

## Контекст

`src/PaspanParsers/CSharp/CSharpParser.cs` (1309 строк, один статический конструктор) — учебный парсер. README заявляет «C# 1–12, Complete», но на деле:

- **Нет целых категорий синтаксиса.** Не строятся Record, Delegate, Event, Indexer, Operator, Destructor. Нет вложенных типов: `memberDeclaration` не включает `typeDeclaration`. Нет `foreach`, `switch`-оператора, `try`, `using`, `lock`, `yield`, `goto`, локальных функций. Нет `new`, `this`/`base`, cast, `as`, `await`, `??`, составных присваиваний, `++`/`--`, `?.`, кортежей, диапазонов, `with`, коллекционных выражений. Нет интерполированных, verbatim и raw строк. Нет top-level statements, file-scoped namespace, `global using`, препроцессора.
- **Цепочки постфиксов не работают.** `memberAccess` и `elementAccess` объявлены, но не используются (к тому же леворекурсивны). Поэтому `a.b().c[0]` не разбирается.
- **Контекстные слова считаются зарезервированными.** `var`, `get`, `set`, `where`, `from`, `select`, `group`, `value`, `async`, `await`, `record` и другие добавлены в `keywords`, из-за чего корректный код вида `var select = 1;` падает.
- **Проблемы в лексике Paspan** (подтверждены при исследовании):
  - `Keyword` проверяет границу слова только по буквам, поэтому `int1` и `int_x` совпадают с `int`.
  - `Terms.Identifier` понимает только ASCII и допускает `$`.
  - `Terms.String` не знает `\a`, verbatim, raw и interpolated строк.
  - `Terms.Integer`/`Number` не поддерживают hex, binary, `_` и суффиксы; в режиме по умолчанию `1,2` читается как 12, а `1.` принимается как число.
  - Нет «максимального захвата» операторов: `Terms.Char('<')` совпадает с началом `<<` и `<=`.
- **Библиотека не страхует от неудачного перебора.** В Paspan нет мемоизации. Цикл-детекция в `Deferred` молча отдаёт `false` при повторном входе в той же позиции, так что грамматика с глубоким перебором будет и медленной, и хрупкой.

**Цель:** любой файл, который Roslyn (LanguageVersion = C# 14, `preview` не нужен) разбирает без диагностик, наш парсер разбирает успешно и строит семантический AST, эквивалентный дереву Roslyn. Восстановление после ошибок не требуется.

**Принятые решения:**
- Остаёмся на комбинаторах Paspan, а трудные места делаем собственными подклассами `Parser<T>`.
- AST семантический: расширяем `CSharpAst.cs` и добавляем позиции (span); trivia не сохраняем.
- Целевая версия языка — C# 14.

## Критерий готовности и оракул (строится первым)

1. **Установить .NET 10 SDK в окружение.** Сейчас `dotnet` отсутствует; нужен setup script через `dotnet-install.sh`.
2. **Добавить оракул Roslyn.** В `src/PaspanParsers.Tests` подключить `Microsoft.CodeAnalysis.CSharp` (только в тестовый проект), новый файл `CSharp/RoslynOracle.cs`. Для каждого входного файла:
   - Roslyn разбирает исходник. Если есть диагностики, файл исключается как некорректный.
   - Наш `CSharpParser.TryParse` должен вернуть `true`.
   - Результат печатается через `CSharpWriter`, текст снова разбирается Roslyn, и `SyntaxFactory.AreEquivalent(original, regenerated, topLevel: false)` должен вернуть `true`.
   - Это требует, чтобы Writer печатал дерево буквально: без добавления или удаления скобок, с сохранением `ParenthesizedExpression`. Для этого же Writer будет обновляться вместе с каждым новым узлом.
3. **Корпуса для прогона:**
   - `CSharpExamples.xcs` и `CSharpExamples2.xcs` (уже компилируются в тестовом проекте).
   - Все `.cs` файлы самого репозитория (`src/**`).
   - Новая папка `src/PaspanParsers.Tests/CSharp/Corpus/` с тематическими файлами на каждую фичу C# 1–14 (по файлу на этап ниже).
   - Опционально большой внешний корпус из переменной окружения `CSHARP_CORPUS_DIR` (например, `dotnet/runtime/src/libraries`), не хранится в репозитории. Отчёт — процент успеха и список первых N расхождений.
4. **Цель:** 100% на встроенных корпусах; на внешнем — 100% для файлов без активных `#if`-веток с неизвестными символами. Каждое расхождение разбирается отдельно.

## Архитектура

`CSharpParser` разбивается на partial-файлы в `src/PaspanParsers/CSharp/Parser/`:
- `Lexical.cs`, `Trivia.cs`, `Preprocessor.cs`
- `Names.cs`, `Types.cs`
- `Expressions.cs`, `Patterns.cs`, `Statements.cs`
- `Declarations.cs`, `CompilationUnit.cs`

Статический конструктор остаётся точкой сборки, отдельные части вызываются как `static` методы-фабрики, соединённые через `Deferred`.

Новые инфраструктурные типы:
- `CSharpParseOptions`: `LanguageVersion`, `PreprocessorSymbols`.
- `CSharpParseContext : ParseContext`: символы препроцессора, стек `#if`, флаги «внутри query-выражения», «внутри async» (для `await` как идентификатора), кэш сканирования типов по позиции.
- Для запуска использовать `TryParse(ref SpanReader, ParseContext, …)` (перегрузки со `string` создают `new ParseContext()` сами).

Что переиспользуем из Paspan:
- `Parser<T>.Parse(ref SpanReader, ParseContext, ref ParseResult<T>)` и `SpanReader.CaptureState()`/`RollBackState()` — для своих парсеров с lookahead.
- `Then(Func<ParseContext,int,int,T,U>)` (`src/PaspanCommon/Fluent/Then.cs`) — для span узлов.
- `CommentsBuilder.WithParser(...)` (`ParserExtensions.WhiteSpace.cs`) — для подключения trivia и препроцессора.
- `Parsers.Switch` / `Select` — для диспетчеризации по первому токену.
- `WhenFollowedBy` / `WhenNotFollowedBy` / `Not` — для точечных проверок.

## Этапы

### Этап 0. Каркас и измерение базы
- Разбить `CSharpParser.cs` на partial-файлы без изменения поведения; все 68 текущих тестов должны остаться зелёными.
- Добавить `CSharpParseOptions`, `CSharpParseContext` и оракул; прогнать корпуса и зафиксировать базовый процент.
- Убрать `input.Trim()` из `TryParse`, пропускать BOM.

### Этап 1. Лексический уровень (свои `Parser<T>` в `Lexical.cs`)
- **Trivia:**
  - Пробелы по спецификации, включая Unicode-категорию Zs, `\u0085`, ` `, ` `.
  - Комментарии `//` и `/* */` (doc-комментарии — как обычные).
  - Директивы препроцессора (этап 7).
- **Идентификаторы:**
  - Unicode-категории Lu/Ll/Lt/Lm/Lo/Nl для начала; Mn/Mc/Nd/Pc/Cf для продолжения.
  - Префикс `@`, escape-последовательности `\uXXXX` и `\UXXXXXXXX`; без `$`.
- **Ключевые слова.** Разделить 77 зарезервированных и контекстные (`var`, `dynamic`, `async`, `await`, `yield`, `get`/`set`/`init`/`add`/`remove`, `value`, `where`, `partial`, `record`, `required`, `file`, `scoped`, `managed`/`unmanaged`, `nint`/`nuint`, `and`/`or`/`not`, `with`, `when`, `global`, `nameof`, `args`, `field`, `extension`, `allows`, query-слова). Контекстные распознаются только позиционно.
  - Зарезервированное слово = идентификатор (по правилам выше), равный ключу. Это заодно закрывает ошибку границы `int1`.
- **Операторы.** Таблица пунктуаторов с максимальным захватом (длинные раньше коротких): `>>>=`, `??=`, `?.`, `..`, `->`, `=>`, `::` и т.д.
  - `>` всегда отдельный токен; `>>`, `>>>`, `>=`, `>>=` собираются в парсере выражений из соседних `>` без пробела между ними (как в Roslyn), чтобы работало `List<List<int>>`.
- **Литералы:**
  - Целые: dec/hex/bin, `_`, суффиксы `u`/`l`/`ul`; тип значения выбирается по правилам C# (int → uint → long → ulong).
  - Вещественные: экспонента, суффиксы `f`/`d`/`m`; `1.` не принимается, так что `1..2` и `1.ToString()` работают.
  - Char со всеми escape (`\a`, `\e` из C# 13, `\x`, `\u`, `\U`).
  - Строки: обычные, verbatim `@""`, interpolated `$""` / `$@""` / `@$""` с вложенными выражениями, alignment и format, raw `"""…"""` одно- и многострочные с обрезкой отступа, interpolated raw `$$"""…{{x}}…"""`, UTF-8 суффикс `u8`.
- **Правки ядра** (минимальные, с регрессиями в `src/PaspanParsers.Tests/CoreRegressionTests.cs`):
  - граница в `src/PaspanCommon/Fluent/Literals/Keyword.cs` (цифры и `_` тоже продолжают слово);
  - `\a` в валидаторе `SpanReader.ReadQuotedString` (`src/Paspan/SpanReader.cs`).
  - Остальное C#-специфичное пишем в самом C#-парсере, а не в ядре.

### Этап 2. Имена и типы (`Names.cs`, `Types.cs`)
- **Узлы имён** вместо лоссового `NameExpression(parts)`:
  - `IdentifierName`, `GenericName(name, typeArgs)`, `QualifiedName(left, right)`, `AliasQualifiedName` (`global::X`).
  - Так представимы `A<B>.C<D>` и `x.Foo<int>()`.
- **Типы:**
  - predefined, включая `nint`/`nuint`;
  - nullable на любом типе, в том числе `int?[]` и `T?[]?`;
  - массивы (rank и jagged), pointer `T*`, function pointer `delegate* unmanaged[Cdecl]<int, void>`;
  - tuple `(int a, string b)`, `ref`/`ref readonly`-типы, `scoped`, `var` как имя.
- **`ScanType`** — неаллоцирующий lookahead-парсер, возвращающий конец типа (аналог `ScanType` в Roslyn). Кэш результатов по позиции хранится в `CSharpParseContext`.
- **Разрешение `<`** по правилу Roslyn: после `ScanTypeArgumentList` смотрим на следующий токен — `(`, `)`, `]`, `}`, `:`, `;`, `,`, `.`, `?`, `==`, `!=`, `|`, `^`, `&&`, `||`, `&`, `[`, `=>` и т.д. Это различает `F(G<A, B>(7))` и `a < b`.

### Этап 3. Выражения (`Expressions.cs`) — собственный Pratt-парсер `ExpressionParser : Parser<Expression>`
- **Приоритеты C# 14** (от низкого к высокому):
  - assignment (в том числе составные и `??=`, правоассоциативно)
  - lambda
  - conditional `?:` (с отличием от `T?` в контексте типа)
  - `??` (правоассоциативно)
  - `||`
  - `&&`
  - `|`
  - `^`
  - `&`
  - равенство
  - relational/`is`/`as`
  - сдвиги (`<<`, `>>`, `>>>`)
  - аддитивные
  - мультипликативные
  - `switch` / `with`
  - range `..`
  - унарные (`+`, `-`, `!`, `~`, `++`/`--`, `^`, `*`, `&`, cast, `await`)
  - постфиксные: `.`, `?.`, `->`, `()`, `[]`, `?[]`, `++`/`--`, `!` (null-forgiving)
- **Primary:**
  - литералы, имена, `this`, `base`;
  - `new` (объекты, массивы, `new()`, анонимные типы, object/collection/index-инициализаторы `[i] = x`, вложенные `{ }`);
  - `stackalloc`, коллекционные выражения `[a, ..b]`;
  - `typeof` (в том числе unbound generic `typeof(Dictionary<,>)`), `sizeof`, `default` / `default(T)`, `nameof`, `checked`/`unchecked`;
  - `__arglist`, `__makeref`, `__reftype`, `__refvalue`, анонимные методы `delegate (…) { }`;
  - `throw`-выражение, `ref`-выражения, declaration expressions (`out var x`, `out _`, `var (a, b) = …`), кортежи и деконструкция.
- **Дизамбигуации по правилам Roslyn:**
  - cast vs скобки (`ScanCast` + проверка следующего токена);
  - lambda vs кортеж/скобки (сканирование до `)` и проверка `=>`);
  - генерик vs `<` (этап 2);
  - `?` nullable vs условный;
  - query: `from` начинает запрос, только если дальше «идентификатор in» или «тип идентификатор in»; внутри запроса контекстные query-слова — ключевые.
- **Lambdas:** `static`, `async`, атрибуты, явный тип возврата `int (x) => …`, параметры с модификаторами и значениями по умолчанию, `_`-discards.
- **Query:** все клауза, включая `join … into`, `group … into`, `into` continuation (хранить как вложенную continuation, а не склеивать клауза как сейчас).
- **C# 14:** null-conditional assignment `a?.b = c`.

### Этап 4. Паттерны (`Patterns.cs`)
- Реализовать полный набор паттернов:
  - константный (любое constant-выражение, не только `primary`), type, declaration, `var` с деконструкцией `var (a, b)`, discard;
  - positional `(p1, p2)` и property `{ A.B: p }` (extended);
  - list `[a, .., b]` и slice `..`;
  - relational, parenthesized, логические `not` > `and` > `or` с правильной ассоциативностью (сейчас `a and b or c` строится неверно).
- `is`-выражение на верном уровне приоритетов, `switch`-выражения с `when`.

### Этап 5. Операторы (`Statements.cs`)
- **Диспетчер по первому токену.** Разбор «объявление или выражение» — через `ScanType` + «следующий токен — идентификатор», как `IsPossibleLocalDeclarationStatement` в Roslyn.
- **Полный набор:**
  - block, empty `;`, labeled, local declaration (`const`, `ref`, `ref readonly`, `scoped`, `using`, `await using`);
  - локальные функции (`static`/`async`/`unsafe`/`extern`, generic, constraints, атрибуты);
  - `if`/`else`;
  - `switch` (секции, `case` с паттернами и `when`, `default`);
  - `while`, `do`, `for` (несколько инициализаторов и итераторов, объявления);
  - `foreach` и `await foreach` (включая деконструкцию `var (k, v)`);
  - `break`, `continue`, `goto` / `goto case` / `goto default`, `return`, `throw`, `yield return` / `yield break`;
  - `try`/`catch` (с типом, без него, `when`-фильтр)/`finally`;
  - `checked`/`unchecked` блоки, `lock`, `using` (выражение и объявление), `fixed`, `unsafe`.

### Этап 6. Объявления и единица компиляции (`Declarations.cs`, `CompilationUnit.cs`)
- **Общий префикс парсится один раз:** атрибуты + модификаторы (включая `file`, `required`, `scoped`, `partial` в любой позиции), затем диспетчер по ключевому слову или форме. Сейчас атрибуты и модификаторы переразбираются в каждой альтернативе `Or`.
- **Типы:**
  - class/struct/interface/enum/delegate;
  - `record` / `record class` / `record struct`, `readonly`/`ref` struct;
  - primary constructors для class и struct;
  - вложенные типы;
  - variance `in`/`out` и атрибуты у type parameters;
  - constraints (`class?`, `struct`, `unmanaged`, `notnull`, `default`, `new()`, `allows ref struct`).
- **Члены:**
  - поля (`fixed`-буферы), `const`;
  - методы (явная реализация интерфейса `IFoo.Bar`, generic, `partial`);
  - свойства: accessor-ы с модификаторами и атрибутами, expression-bodied свойство и accessor-ы, инициализатор `= x;`, `field` (C# 14);
  - индексаторы, события (field-like и с `add`/`remove`);
  - конструкторы с `: base(...)` / `: this(...)`, static-конструкторы, деструкторы;
  - операторы (включая `checked`, `>>>`, операторы true/false, C# 14 составные операторы присваивания);
  - conversion-операторы `implicit`/`explicit`;
  - C# 14 extension-блоки `extension(T x) { … }`.
- **Единица компиляции:**
  - `extern alias`, `global using`, `using static`, `using alias = любой тип` (C# 12, включая кортежи и указатели);
  - глобальные атрибуты;
  - блочные и file-scoped namespace (с собственными `using` внутри);
  - top-level statements (`GlobalStatement`), смешанные с типами в порядке, который допускает Roslyn.

### Этап 7. Препроцессор (`Preprocessor.cs`, подключается как trivia)
- Директивы допустимы только в начале строки после пробелов; позиция «начало строки» проверяется явно.
- Поддерживаемые директивы:
  - `#define`/`#undef`;
  - `#if`/`#elif`/`#else`/`#endif` с вычислением выражений (`!`, `&&`, `||`, `==`, `!=`, скобки, `true`/`false`) по символам из `CSharpParseOptions`;
  - `#region`/`#endregion`, `#pragma`, `#nullable`, `#line`, `#error`/`#warning`;
  - `#!` и `#:` (file-based apps).
- Неактивные ветки пропускаются как «disabled text» (по строкам, с учётом вложенных `#if`). Состояние хранится в `CSharpParseContext`.

### Этап 8. AST, позиции, Writer, документация
- **Изменения AST:**
  - В базовый `ICSharpNode` (или абстрактный класс `CSharpNode`) добавить `Span` (start, end в байтах UTF-8) и заполнять через `Then(ctx, start, end, value)`.
  - `LiteralExpression`: хранить исходный текст токена и типизированное значение (`int`/`uint`/`long`/`ulong`/`float`/`double`/`decimal`/`char`/`string`/`byte[]` для u8).
  - Интерполированная строка — отдельный узел с частями.
  - Расширить узлы, которые сейчас лоссовые:
    - `MemberInitializer` — индексные и вложенные инициализаторы;
    - `Argument` — declaration expression;
    - `LambdaExpression` — `static`, атрибуты, тип возврата;
    - `ForEachStatement` — деконструкция;
    - `UsingStatement` — выражение или объявление;
    - `ConstructorDeclaration` — инициализатор реально заполняется;
    - `Accessor` — expression body.
- **Существующие тесты.** Изменение API узлов ломает часть из 68 тестов в `CSharpParserTests.cs`. Тесты обновляются вместе с кодом, изменения API фиксируются в README.
- **`CSharpWriter.cs`:** поддержать каждый новый узел; печатать буквально (для оракула).
- **Документация.** Обновить `src/PaspanParsers/CSharp/README.md` и корневой `README.md`: честная таблица поддержки, пример с `CSharpParseOptions`. Поправить устаревшие ссылки на `CSharpExamples.cs` и `Paspan.Tests.CSharp`.

### Этап 9. Производительность
- Бенчмарк: время и аллокации на большом корпусе.
- Главные меры: первый токен как ключ диспетчера (`Switch`) вместо длинных цепочек `Or`, отказ от повторного разбора префиксов, кэш `ScanType`.
- Проверить, не мешает ли цикл-детекция `Deferred` lookahead-сканам; при необходимости включить `disableLoopDetection`, когда левой рекурсии уже нет.
- Цель: линейное поведение и отсутствие экспоненциального перебора на глубоко вложенных выражениях. Тест: 1000 вложенных скобок или 5000 элементов `a + a + …` без StackOverflow и за приемлемое время.

## Порядок работы и контроль

- Каждый этап — отдельный коммит или PR в ветке `claude/parser-improvement-plan-1szcac`.
- На каждом этапе добавляются:
  - юнит-тесты в `src/PaspanParsers.Tests/CSharp/` — один тестовый файл на область (`LexicalTests.cs`, `TypeTests.cs`, `ExpressionTests.cs`, …);
  - тематический файл корпуса;
  - прогон оракула.
- Метрика прогресса: процент файлов корпуса, прошедших оракул; фиксируется в README.

## Проверка

1. `dotnet build PaspanParsers.slnx` (после установки .NET 10 SDK в окружении).
2. `dotnet run --project src/PaspanParsers.Tests`. Не `dotnet test`: так требует `CLAUDE.md`. Прогоняются все старые тесты, новые юнит-тесты и тесты оракула на встроенных корпусах.
3. Оракул на внешнем корпусе: `CSHARP_CORPUS_DIR=… dotnet run --project src/PaspanParsers.Tests -- --filter RoslynOracle`. Отчёт: процент успеха и первые расхождения.
4. Регрессии ядра Paspan (`CoreRegressionTests.cs`, `FluentTests.cs`) остаются зелёными после правок `Keyword` и `ReadQuotedString`.
