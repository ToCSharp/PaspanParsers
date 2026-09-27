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

`CSharpParser` разбивается на partial-файлы в `src/PaspanParsers/CSharp/Parser/`
(после этапов 2–5 типы, выражения, паттерны и операторы живут в `SyntaxParser*.cs`, см. ниже;
после этапов 6–7 вся грамматика — в `SyntaxParser*.cs`, а trivia и директивы — в `Preprocessor.cs`):
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

> **Статус: выполнен.** Грамматика разбита на partial-файлы в `src/PaspanParsers/CSharp/Parser/`.
> Добавлены `CSharpParseOptions` и `CSharpParseContext`, а также оракул Roslyn (`RoslynOracle.cs`, Roslyn 5.9.0 только в тестах).
> Прогон корпуса с ratchet-базой: `CSharpCorpusTests.cs` и `Corpus/oracle-baseline.txt`.
> Добавлены 17 тематических файлов корпуса `Corpus/00…16`. `input.Trim()` заменён на честный пропуск trivia в конце и BOM.
> **База:** 1/160 валидных файлов проходит оракул (только `Corpus/00-Basics.cs`).
> 8 тестов C#-парсера (6 LINQ, 2 lambda) падали и до этапа 0: поведение грамматики не менялось.
- Разбить `CSharpParser.cs` на partial-файлы без изменения поведения; все 68 текущих тестов должны остаться зелёными.
- Добавить `CSharpParseOptions`, `CSharpParseContext` и оракул; прогнать корпуса и зафиксировать базовый процент.
- Убрать `input.Trim()` из `TryParse`, пропускать BOM.

### Этап 1. Лексический уровень (свои `Parser<T>` в `Lexical.cs`)

> **Статус: выполнен.**
> - Сканер `Parser/Lexer.cs` и токен-парсеры `Parser/Tokens.cs`: trivia с Unicode-пробелами, идентификаторы (Unicode, `@`, `\u`/`\U`, без `$`), ключевые слова (77 зарезервированных + `__arglist` и др.; контекстные — обычные идентификаторы), операторы с максимальным захватом, все литералы. Среди литералов — interpolated (`$`, `$@`, `@$`, raw `$$"""`), `u8`.
> - AST: `LiteralExpression.Text` (текст как в исходнике), `LiteralKind.Utf8String`, `InterpolatedStringExpression` / `InterpolatedStringText` / `Interpolation`.
> - `CSharpWriter` печатает литералы по исходному тексту и экранирует `@` у идентификаторов, совпадающих с ключевыми словами.
> - Правка ядра: `\a` в `SpanReader.ReadQuotedString`.
> - **Отклонение от плана:** граница в `Keyword` ядра не менялась. То, что `return123` совпадает с `return`, закреплено тестом `FluentTests.KeywordShouldMatchNonLetterCharactersAfter`. C#-парсер теперь использует свой `KeywordToken`.
> - **Оракул:** 2/165 (`00-Basics.cs`, новый `17-Lexical.cs`). Остальные файлы корпуса упираются в грамматику следующих этапов.
> - Попутно заработали 2 ранее падавших теста лямбд.
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

> **Этапы 2–5: общее решение.** Имена, типы, выражения, паттерны и операторы разбирает один рукописный
> рекурсивный спуск `SyntaxParser` (`ref struct`, файлы `Parser/SyntaxParser*.cs`).
> - **Отклонение от плана:** это не отдельные `ExpressionParser`, `Types.cs` и `Statements.cs` на комбинаторах.
>   Эти части взаимно рекурсивны: лямбды содержат блоки, паттерны — типы и выражения. Правилам Roslyn нужен просмотр вперёд через целые типы.
> - Токены сканируются лениво поверх `Lexer` и кэшируются по позиции в `CSharpParseContext` (`SyntaxToken`, `SyntaxCache`).
>   `>` всегда отдельный токен, а `>>`, `>>>`, `>>=`, `>>>=` собираются из соседних `>` без trivia между ними.
> - Комбинаторная грамматика объявлений получает эти части через `SyntaxRuleParser<T>`: `expression`, `block`, `typeReference`, `returnType`,
>   атрибуты, параметры, параметры типов, ограничения, декларации переменных.
>   Старые комбинаторные `Expressions.cs`, `Patterns.cs`, `Statements.cs` удалены.
> - **Оракул:** в `RoslynOracle` добавлено структурное сравнение. `SyntaxFactory.AreEquivalent` ложно различает деревья,
>   если Roslyn хранит список из одного оператора то как сам узел, то как список (зависит от trivia).
>   Проверка работает на уровне красного дерева: `ChildNodesAndTokens` и правила Roslyn для токенов.
> - **Проверка по операторам:** `CSharpCorpusTests.Oracle_BuiltInCorpus_Statements` оборачивает каждый оператор каждого тела метода встроенного корпуса в метод
>   и прогоняет оракул. Так этапы 2–5 измеряются независимо от объявлений (этап 6). Итог: **7880/7880**.
>   Операторы с директивами препроцессора пропускаются до этапа 7.
>   Такой же прогон по ~60 тыс. строк исходников Roslyn, dotnet/runtime и ASP.NET Core проходит полностью, кроме операторов с директивами.
> - **Оракул по файлам:** 19/177 (было 2/165). Остальные файлы упираются в объявления (этап 6) и препроцессор (этап 7).
> - Новые файлы корпуса: `18-Types.cs`, `19-ExpressionsAndLambdas.cs`, `20-PatternsAndStatements.cs`.
>   Юнит-тесты: `TypeTests.cs`, `ExpressionTests.cs`, `PatternTests.cs`, `StatementTests.cs` (AST и оракул на фрагментах).
> - Позиции (span) в AST не добавлялись: это этап 8.

### Этап 2. Имена и типы (`Names.cs`, `Types.cs`)

> **Статус: выполнен** (`Parser/SyntaxParser.Types.cs`).
> - Типы: predefined, именованные с `alias::`, generic и квалификатором (`NamedTypeReference.Qualifier` для `A<B>.C<D>`).
>   Также nullable на любом типе (`NullableTypeReference` для массивов и кортежей), массивы, указатели, `delegate*` с соглашениями о вызове.
>   Кортежи, `ref`/`ref readonly` (`RefTypeReference`), `scoped` (`ScopedTypeReference`), unbound generic в `typeof` и `nameof` (`OmittedTypeReference`).
> - **Отклонение от плана:** вместо отдельных узлов `IdentifierName`/`GenericName`/`QualifiedName` имена в выражениях строятся так:
>   `NameExpression` из одного идентификатора (с аргументами типа), цепочки `MemberAccessExpression` (тоже с аргументами типа)
>   и `AliasQualifiedNameExpression`. Так представимы `A<B>.C<D>` и `x.Foo<int>()`.
>   Имена namespace, using и атрибутов остаются `NameExpression(parts)`; у `NameExpression` появился `Alias`.
> - Вместо неаллоцирующего `ScanType` используется спекулятивный `ParseType` с откатом позиции. Кэш сканирования — это кэш токенов и парных скобок.
>   Неаллоцирующий вариант перенесён в этап 9.
> - `<` разрешается по правилу из спецификации: после списка аргументов типа проверяется следующий токен.
>   В режиме `TypeMode.Expression` (после `is`/`as` и в паттернах) `?` — nullable, только если за ним не может начаться выражение.
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

> **Статус: выполнен** (`Parser/SyntaxParser.Expressions.cs`).
> - Pratt-парсер с приоритетами Roslyn, все бинарные, унарные и постфиксные операторы (включая `!`, `?.`, `?[`, `->`), `switch`, `with`, диапазоны.
> - Primary: `new` во всех формах (`InitializerExpression` для object/collection/complex/array инициализаторов,
>   `AnonymousObjectCreationExpression`, `ImplicitArrayCreationExpression`), `stackalloc`, коллекционные выражения со `SpreadElement`.
>   Также `typeof`, `sizeof`, `default`, `nameof`, `checked`, `__arglist`/`__makeref`/`__reftype`/`__refvalue`, анонимные методы,
>   `throw`, `ref`, декларации (`DeclarationExpression` с `VariableDesignation`), кортежи и деконструкция.
> - Cast, лямбды и query разбираются по правилам Roslyn. Лямбды поддерживают `static`/`async`, атрибуты, явный тип возврата,
>   модификаторы и значения по умолчанию параметров. У query есть `into`-continuation (`QueryExpression.Continuation`).
> - Writer печатает буквально: скобки лямбды (`HasParenthesizedParameters`), `new T { }` без скобок, висячие запятые,
>   `ascending`, `Name = value` в атрибутах, порядок модификаторов лямбд и локальных функций.
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

> **Статус: выполнен** (`Parser/SyntaxParser.Patterns.cs`).
> - Все паттерны: константный (выражение уровня shift, в том числе с cast `(byte)'a'`), type, declaration, `var` с деконструкцией, discard.
>   Также positional с именами, property с расширенными путями `A.B:`, list и slice, relational, parenthesized.
>   Приоритет `not` > `and` > `or` с левой ассоциативностью.
> - После `is` простое имя — type pattern, в `case` и в ветках `switch` — константа.
>   Пустой `{ }` и висячие запятые в property и list паттернах сохраняются.
- Реализовать полный набор паттернов:
  - константный (любое constant-выражение, не только `primary`), type, declaration, `var` с деконструкцией `var (a, b)`, discard;
  - positional `(p1, p2)` и property `{ A.B: p }` (extended);
  - list `[a, .., b]` и slice `..`;
  - relational, parenthesized, логические `not` > `and` > `or` с правильной ассоциативностью (сейчас `a and b or c` строится неверно).
- `is`-выражение на верном уровне приоритетов, `switch`-выражения с `when`.

### Этап 5. Операторы (`Statements.cs`)

> **Статус: выполнен** (`Parser/SyntaxParser.Statements.cs`, `Parser/SyntaxParser.Declarations.cs`).
> - Диспетчер по первому токену. «Объявление или выражение» решается спекулятивным разбором типа и проверкой токена после имени.
> - Все операторы из списка ниже. Новые узлы: `EmptyStatement`, `LocalFunctionStatement`, `CheckedStatement`, `UnsafeStatement`, `FixedStatement`.
>   Также `GotoStatement.Kind`, `ForEachStatement.Variable` для деконструкции и `LocalDeclarationStatement.IsAwait`.
> - Атрибуты, параметры (`Parameter.Modifiers` в порядке исходника, `scoped`, `ref readonly`), параметры типов и ограничения
>   (`DefaultConstraint`, `AllowsRefStructConstraint`) разбираются здесь же и используются также объявлениями.
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

> **Статус: выполнен** (`Parser/SyntaxParser.Members.cs`, `Parser/SyntaxParser.CompilationUnit.cs`).
> - **Отклонение от плана:** объявления тоже разбирает рукописный `SyntaxParser`, а не комбинаторы.
>   Верхний уровень файла смешивает объявления и операторы, а объявлениям нужны те же типы, параметры и блоки.
>   Комбинаторная грамматика (`Lexical.cs`, `Names.cs`, `Types.cs`, `Declarations.cs`, `CompilationUnit.cs`)
>   и неиспользуемые токен-парсеры удалены. `CSharpParser.CompilationUnitParser` — это `SyntaxRuleParser` с пропуском trivia вокруг.
> - Атрибуты и модификаторы разбираются один раз, дальше диспетчер по токену. Контекстные модификаторы
>   (`partial`, `async`, `required`, `file`, `safe`) считаются модификаторами, только если за ними идёт объявление
>   (`async Task Run()`), а не когда они сами тип (`async x;`).
> - Все объявления из списка ниже, включая `record class`, primary constructors у class/struct с аргументами базового типа,
>   тела `;` (`class C;`), `;` после `}`, явную реализацию интерфейсов (`bool IEquatable<T>.Equals`, `I.this[...]`, `I.operator +`),
>   fixed-буферы, `ref`-поля, операторы C# 14 (`void operator +=`, `operator ++()`), extension-блоки, партиальные конструкторы.
> - Верхний уровень: всё, что не тип и не namespace, — `GlobalStatement` с оператором внутри. Как в Roslyn, методы и
>   переменные там — локальные функции и локальные объявления. `using` после начала операторов — оператор.
> - AST: `TypeDeclaration` (общая база class/struct/interface/enum/record), `DestructorDeclaration`, `OperatorDeclaration`,
>   `ConversionOperatorDeclaration`, `ExtensionBlockDeclaration`, `GlobalStatement`; `MemberDeclaration.ModifierList`
>   (модификаторы в порядке исходника), `ExplicitInterface`, `UsingDirective.IsGlobal`/`IsUnsafe`, псевдонимы на любой тип,
>   `VariableDeclarator.BracketedArguments`, `EnumDeclaration.HasTrailingComma`, `HasBody`/`HasTrailingSemicolon`.
>   Новые свойства необязательные (`init`, для `#nullable` — `set`), старые конструкторы и тесты не менялись.
> - Внешний корпус нашёл ошибки прежних этапов, они исправлены: `(a * b)` и `(A<T>.B * c)` — умножение, а не объявление указателя;
>   `c ? () => a : b` — условное выражение, а не лямбда с типом `c?`; `x is int.MaxValue`; `new byte[]?[n]`;
>   константы в паттернах вне `is` до уровня `??` (`case 'a' ^ 'b':`, `(A | B, 1)`); `nameof(...)` в паттерне — константа;
>   `switch ((a, b))` сохраняет скобки; `'\uD800'` (одиночный суррогат) больше не бросает исключение.
> - Новые файлы корпуса: `21-Declarations.cs`, `22-PreprocessorAndNullable.cs`. Юнит-тесты: `DeclarationTests.cs`.
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

> **Статус: выполнен** (`Parser/Preprocessor.cs`).
> - Директивы — часть trivia между токенами: `SyntaxParser` и `TriviaParser` сканируют trivia через `Preprocessor`.
> - Сканирование trivia в позиции не зависит от предыдущих сканов (токены сканируются лениво и не по порядку при просмотре вперёд):
>   `#define`/`#undef` допустимы только до первого токена, поэтому начальная trivia файла сканируется по порядку от символов
>   из `CSharpParseOptions`, а итоговые символы действуют дальше (и попадают в `CSharpParseContext.DefinedSymbols`).
>   `#elif`/`#else`, встреченные в активном коде, закрывают взятую ветку — скан пропускает всё до парного `#endif`.
> - `#if`/`#elif` вычисляются (`!`, `&&`, `||`, `==`, `!=`, скобки, `true`/`false`, комментарий в конце). Неактивные ветки
>   пропускаются по строкам с учётом вложенных `#if`. `#` — директива, только если перед ним на строке одни пробелы,
>   поэтому `#` в строках и комментариях не директива.
> - **Отклонение от плана:** `#nullable` сохраняется в AST. `SyntaxFactory.AreEquivalent` сравнивает `#nullable` в trivia токенов
>   (проверено: `#pragma`, `#region`, `#define`, `#if` не влияют), и директива меняет смысл кода.
>   `NullableDirective` хранится в `NullableDirectives` узлов, с которых может начинаться строка: объявления, `using`, операторы,
>   параметры, `where`, аксессоры, метки `case`, глобальные атрибуты. Для открывающих и закрывающих скобок есть
>   `OpenBraceNullableDirectives`/`CloseBraceNullableDirectives`, для конца файла — `CompilationUnit.EndNullableDirectives`.
>   Остальные директивы в AST не попадают.
> - `Oracle_BuiltInCorpus_Statements` больше не пропускает операторы с директивами.
>   `CSHARP_CORPUS_SYMBOLS` задаёт символы для внешнего корпуса. Юнит-тесты: `PreprocessorTests.cs`.
> - **Оракул по файлам:** встроенный корпус 179/179 (было 19/177).
>   Внешние корпуса: исходники Roslyn (`src/Compilers/{CSharp,Core}/Portable`, `src/Workspaces/CSharp/Portable`, 2176 файлов, 777 тыс. строк) — 2176/2176;
>   dotnet/runtime (`System.Private.CoreLib`, `System.Linq`, `System.Collections`, `System.Text.Json`, `System.Net.Http` и др., 2814 файлов, 875 тыс. строк) — 2812/2814,
>   то же с символами `NET;NETCOREAPP;DEBUG;TARGET_64BIT;TARGET_WINDOWS`.
>   Два расхождения — `#nullable` внутри списка аргументов типа (`IEnumerable<` `#nullable disable` `TResult` `#nullable restore` `>`):
>   к типам директивы не привязываются.
- Директивы допустимы только в начале строки после пробелов; позиция «начало строки» проверяется явно.
- Поддерживаемые директивы:
  - `#define`/`#undef`;
  - `#if`/`#elif`/`#else`/`#endif` с вычислением выражений (`!`, `&&`, `||`, `==`, `!=`, скобки, `true`/`false`) по символам из `CSharpParseOptions`;
  - `#region`/`#endregion`, `#pragma`, `#nullable`, `#line`, `#error`/`#warning`;
  - `#!` и `#:` (file-based apps).
- Неактивные ветки пропускаются как «disabled text» (по строкам, с учётом вложенных `#if`). Состояние хранится в `CSharpParseContext`.

### Этап 8. AST, позиции, Writer, документация

> **Статус: выполнен.**
> - **Позиции.** Базовый класс `CSharpNode` (реализует `ICSharpNode.Span`) и `TextSpan` (`Start`, `End`, `Length`, `Contains`, `GetText`).
>   Span — от первого токена узла до конца последнего, без trivia, как `SyntaxNode.Span` в Roslyn. Единица компиляции — весь вход,
>   `#nullable` — своя строка. Смещения в байтах UTF-8 входа без BOM; `CSharpParser.GetUtf8Source` возвращает эти байты.
> - Span заполняет `SyntaxParser.Finish(node, start)` при создании узла. **Отклонение от плана:** не `Then(ctx, start, end, value)`:
>   грамматика давно не комбинаторная.
> - **Проверка позиций по Roslyn.** Оракул после сравнения деревьев вызывает `SpanChecker`: span каждого узла лежит внутри родителя
>   и совпадает со span какого-либо узла или токена Roslyn (в байтах UTF-8). Исключения — узлы, которых у Roslyn нет:
>   имя внутри `NamedTypeReference` с алиасом или аргументами типа, вложенные `ArrayTypeReference` (`int[][]` у Roslyn один тип)
>   и цепочки после `?.` (Roslyn строит `a?.(b.c)`, мы — `(a?.b).c`). Новый статус оракула `SpanMismatch`.
> - Проверка позиций нашла ошибку дерева, которую сравнение текста не видело: `ref c ? ref a : ref b` строилось как `(ref c) ? …`.
>   Теперь, как в Roslyn, после `ref` разбирается целое выражение.
> - **Лоссовые узлы из списка ниже** были расширены на этапах 1–6 (`LiteralExpression` с текстом и типизированным значением,
>   `InterpolatedStringExpression`, инициализаторы через `InitializerExpression`, `DeclarationExpression` в аргументах, лямбды со `static`,
>   атрибутами и типом возврата, деконструкция в `foreach`, `UsingStatement` с выражением или объявлением, `ConstructorInitializer`,
>   тела аксессоров `MethodBody`); оракул подтверждает их на всех корпусах.
> - Хвост этапа 7 закрыт: `#nullable` внутри списка аргументов типа (`TypeReference.NullableDirectives`, `CloseAngleNullableDirectives`
>   у `NamedTypeReference`, `NameExpression`, `MemberAccessExpression`).
> - `TryParse` при неудаче возвращает `ParseError` с позицией токена после самого дальнего разобранного (`Unexpected ';'`, строка и столбец);
>   раньше `error` был `null`.
> - Документация: `src/PaspanParsers/CSharp/README.md` переписан (проверка, использование с `CSharpParseOptions`, позиции,
>   честная таблица поддержки и ограничения, производительность), корневой `README.md` и `CSharpWriter.README.md` обновлены.
>   Устаревшие `CSharpExamples.cs`, `Paspan.Tests.CSharp`, `dotnet test` убраны.
> - Тесты: `SpanTests.cs`, `PreprocessorTests.Nullable_InsideTypeArgumentLists`, `CSharpParserEntryPointTests.TryParse_ReportsWhereTheInputStopsParsing`.
>   Старые 68 тестов `CSharpParserTests.cs` не менялись: новые свойства необязательные, API не сломан.
> - **Оракул по файлам (с проверкой позиций):** встроенный корпус 182/182, операторы 8587/8587; исходники Roslyn 2176/2176;
>   dotnet/runtime 2488/2488 (было 2486: `#nullable` в аргументах типа), с символами `NET;NETCOREAPP;DEBUG;TARGET_64BIT;TARGET_WINDOWS` — 2490/2490;
>   новый корпус ASP.NET Core (`src/Http`, `Mvc.Core`, `Kestrel/Core`, `Components`, `SignalR/server/Core`) — 2015/2015.
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

> **Статус: выполнен.**
> - **Бенчмарк:** `CSharpPerformanceTests.Benchmark_Corpus` (при заданном `CSHARP_CORPUS_DIR`, запуск с `-c Release`) измеряет время и
>   аллокации нашего парсера и Roslyn (`ParseText(...).GetRoot()`) на одних и тех же файлах, отчёт — `benchmark-report.txt`.
>   Итог (один поток, лучший из трёх прогонов):
>
>   | Корпус | PaspanParsers | Roslyn |
>   |---|---|---|
>   | исходники Roslyn, 34 МБ | 23,8 МБ/с, 14,3 байта аллокаций на байт | 20,1 МБ/с, 4,8 байта на байт |
>   | dotnet/runtime, 37 МБ | 31,6 МБ/с, 10,9 | 16,9 МБ/с, 7,3 |
>   | ASP.NET Core, 15 МБ | 18,6 МБ/с, 19,9 | 19,5 МБ/с, 6,8 |
>
>   До оптимизаций: 15,0 МБ/с и 29 байт на байт на исходниках Roslyn.
> - **Линейность.** Найдены и устранены квадратичные места:
>   - `SkipBalanced` (поиск парной скобки при распознавании лямбд) кэшировал результат только для начальной скобки — теперь
>     один проход записывает концы всех вложенных скобок;
>   - попытка приведения типа на каждом `(` заново разбирала вложенный кортежный тип до дна — теперь результат разбора
>     кортежного типа (и неудача) кэшируется по позиции (кортежный тип не зависит от контекста).
>   Тесты: 1000 и 5000 вложенных скобок, 5000 и 50000 элементов `a + a + …`, 5000 вложенных вызовов, приведений и кортежей,
>   1000 вложенных блоков с лямбдами — каждый за десятки миллисекунд (8000 скобок — около 40 мс).
> - **Стек.** Рекурсия без проверки (`ParseTupleType → ParseType`, вложенные инициализаторы, `not not …`, `into … into`,
>   деконструкция, `#if ((…))`) роняла процесс со `StackOverflow` на 1000 скобок. Теперь все рекурсивные пути проверяют стек,
>   и нехватка стека — исключение `InsufficientExecutionStackException`, а не `null`: так парсер не может выбрать другую, неверную
>   трактовку входа. `CSharpParser.TryParse` ловит его и повторяет разбор в потоке со стеком 256 МБ; `CSharpWriter.WriteCompilationUnit`
>   делает так же. На стеке 1 МБ без повтора: около 580 вложенных скобок, 500 уровней блоков с лямбдами, 1000 вложенных `if`.
>   (Сам Roslyn на стеке 1 МБ падает со `StackOverflow` на таких входах.)
> - **Аллокации и время:** кэш токенов размечается заранее (около токена на 10 байт) и переиспользуется следующим разбором в том же
>   потоке (до 64K записей, около 4 МБ); строки идентификаторов интернируются в пределах разбора; `ScanLambda` и разбор оператора
>   не строят тип, если после простого имени не может идти `(` или имя переменной (`a.b.c(x);`).
> - **Отклонение от плана:** диспетчер по первому токену вместо цепочек `Or` и цикл-детекция `Deferred` неактуальны — грамматика
>   рукописная с этапов 2–6. Отдельный неаллоцирующий `ScanType` (перенесён сюда с этапа 2) не понадобился: после точечных проверок
>   спекулятивные типы заметной доли аллокаций не дают, а дублирование грамматики типов — источник расхождений.
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
  После этапа 7 встроенный корпус проходит полностью; дальнейшая метрика — внешние корпуса.
  После этапа 8 оракул проверяет и позиции узлов; после этапа 9 все внешние корпуса (Roslyn, dotnet/runtime, ASP.NET Core) проходят полностью.

## Проверка

1. `dotnet build PaspanParsers.slnx` (после установки .NET 10 SDK в окружении).
2. `dotnet run --project src/PaspanParsers.Tests`. Не `dotnet test`: так требует `CLAUDE.md`. Прогоняются все старые тесты, новые юнит-тесты и тесты оракула на встроенных корпусах.
3. Оракул на внешнем корпусе: `CSHARP_CORPUS_DIR=… dotnet run --project src/PaspanParsers.Tests -- --filter "FullyQualifiedName~Oracle_ExternalCorpus"`. Отчёт: процент успеха и первые расхождения.
   Бенчмарк: `CSHARP_CORPUS_DIR=… dotnet run -c Release --project src/PaspanParsers.Tests -- --filter "FullyQualifiedName~Benchmark_Corpus"`.
4. Регрессии ядра Paspan (`CoreRegressionTests.cs`, `FluentTests.cs`) остаются зелёными после правок `Keyword` и `ReadQuotedString`.
