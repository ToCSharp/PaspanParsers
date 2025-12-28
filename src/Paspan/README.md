# Paspan

[![NuGet](https://img.shields.io/nuget/v/Paspan.svg)](https://nuget.org/packages/Paspan)
[![BSD 3-Clause](https://img.shields.io/github/license/sebastienros/parlot)](https://github.com/sebastienros/parlot/blob/main/LICENSE) [![Join the chat at https://gitter.im/sebastienros/parlot](https://badges.gitter.im/sebastienros/parlot.svg)](https://gitter.im/sebastienros/parlot?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

Paspan is [Parlot](https://github.com/sebastienros/parlot) fork. It is a __fast__, __lightweight__ and simple to use .NET parser combinator.
Unlike Parlot, Paspan based on Spans and ReadOnlySequences, and because of it, has small changes in API.
Its SpanReader is rewritten version of [Utf8JsonReader](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Text.Json/src/System/Text/Json/Reader/Utf8JsonReader.cs).
So, Paspan is best for parsing huge Utf8 or binary files. Literals.Text("foo") will create parser with utf8 bytes representation and will search bytes without converting source to string.

Paspan provides a fluent API based on parser combinators that provide a more readable grammar definition.

## Fluent API

The Fluent API provides simple parser combinators that are assembled to express more complex expressions.
The main goal of this API is to provide and easy-to-read grammar. Another advantage is that grammars are built at runtime, and they can be extended dynamically.

```c#
using Paspan.Common;
using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

Parser<ulong> hex = Terms.Pattern(c => Character.IsHexDigit(c)).AsHex();
Parser<(ulong Start, ulong End)> AddressRangeParser = '['.And(hex).Skip(" - ").And(hex).Skip(']');

Console.WriteLine((0x7FF55CC77440, 0x7FFBBCC77440) == AddressRangeParser.Parse("[7FF55CC77440 - 7FFBBCC77440]"));
```

The following example is a complete parser that create a mathematical expression tree (AST).
The source is available [here](./test/Paspan.Tests/Calc/FluentParser.cs).

```c#
public static readonly Parser<Expression> Expression;

static FluentParser()
{
    /*
     * Grammar:
     * expression     => factor ( ( "-" | "+" ) factor )* ;
     * factor         => unary ( ( "/" | "*" ) unary )* ;
     * unary          => ( "-" ) unary
     *                 | primary ;
     * primary        => NUMBER
     *                  | "(" expression ")" ;
    */

    // The Deferred helper creates a parser that can be referenced by others before it is defined
    var expression = Deferred<Expression>();

    var number = Terms.Decimal()
        .Then<Expression>(static d => new Number(d));

    var divided = Terms.Char('/').AsChar();
    var times = Terms.Char('*').AsChar();
    var minus = Terms.Char('-').AsChar();
    var plus = Terms.Char('+').AsChar();
    var openParen = Terms.Char('(').AsChar();
    var closeParen = Terms.Char(')').AsChar();

    // "(" expression ")"
    var groupExpression = Between(openParen, expression, closeParen);

    // primary => NUMBER | "(" expression ")";
    var primary = number.Or(groupExpression);

    // The Recursive helper allows to create parsers that depend on themselves.
    // ( "-" ) unary | primary;
    var unary = Recursive<Expression>((u) => 
        minus.And(u)
            .Then<Expression>(static x => new NegateExpression(x.Item2))
            .Or(primary));

    // factor => unary ( ( "/" | "*" ) unary )* ;
    var factor = unary.And(ZeroOrMany(divided.Or(times).And(unary)))
        .Then(static x =>
        {
            // unary
            var result = x.Item1;

            // (("/" | "*") unary ) *
            foreach (var op in x.Item2)
            {
                result = op.Item1 switch
                {
                    '/' => new Division(result, op.Item2),
                    '*' => new Multiplication(result, op.Item2),
                    _ => null
                };
            }

            return result;
        });

    // expression => factor ( ( "-" | "+" ) factor )* ;
    expression.Parser = factor.And(ZeroOrMany(plus.Or(minus).And(factor)))
        .Then(static x =>
        {
            // factor
            var result = x.Item1;

            // (("-" | "+") factor ) *
            foreach (var op in x.Item2)
            {
                result = op.Item1 switch
                {
                    '+' => new Addition(result, op.Item2),
                    '-' => new Subtraction(result, op.Item2),
                    _ => null
                };
            }

            return result;
        });            

    Expression = expression;
}
```

## Documentation

- [Existing parsers and usage examples](docs/parsers.md)
- [Best practices for custom parsers](docs/writing.md)

## Compilation

Parlot Grammar trees built using the Fluent API can optionally be compiled with the `Compile()` method. At that point instead of evaluating recursively all the parsers in the grammar tree, these 
are converted to a more linear and optimized and equivalent compiled IL. This can improve the performance by 20% (see benchmarks results).
But `Compile()` option was removed from Paspan. .Net 5 introduced Source Generators and .Net 6 improved it.
__Source Generators__ can optimize whole Parser in more simple way. It is what __TODO__ in future: __"Parser Generator from Parser Combinator with Source Generators"__ )))

## Performance

Paspan was originally made to provide a more efficient alternative to Parlot and projects like
- [Pidgin](https://github.com/benjamin-hodgson/Pidgin)
- [Sprache](https://github.com/sprache/Sprache)
- [Irony](https://github.com/IronyProject/Irony)

TODO: Paspan has performance issues when parser creates Region. And general performance can be improved, I believe.

### Expression Benchmarks

This benchmark creates an expression tree (AST) representing mathematical expressions with operator precedence and grouping. It exercises two expressions:
- Small: `3 - 1 / 2 + 1`
- Big: `1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2`

These benchmarks don't evaluate the expressions but only parse them to create the same AST.

```
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19043.1348 (21H1/May2021Update)
Intel Core i5-9600K CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK=6.0.100
  [Host]   : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT
  ShortRun : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

|              Method |        Mean |     Error |   StdDev | Ratio | RatioSD |  Gen 0 |  Gen 1 | Allocated |
|-------------------- |------------:|----------:|---------:|------:|--------:|-------:|-------:|----------:|
|      PaspanRawSmall |    209.3 ns |   3.01 ns |  0.16 ns |  1.00 |    0.00 | 0.0560 |      - |     264 B |
|   PaspanFluentSmall |    833.2 ns |  37.18 ns |  2.04 ns |  3.98 |    0.01 | 0.1440 |      - |     680 B |
|      ParlotRawSmall |    346.2 ns |  20.21 ns |  1.11 ns |  1.65 |    0.01 | 0.0644 |      - |     304 B |
| ParlotCompiledSmall |    573.0 ns | 355.44 ns | 19.48 ns |  2.74 |    0.09 | 0.1392 |      - |     656 B |
|   ParlotFluentSmall |    863.0 ns |   9.64 ns |  0.53 ns |  4.12 |    0.00 | 0.1392 |      - |     656 B |
|         PidginSmall |  8,773.3 ns | 590.26 ns | 32.35 ns | 41.91 |    0.15 | 0.1678 |      - |     832 B |
|                     |             |           |          |       |         |        |        |           |
|        PaspanRawBig |  1,038.6 ns | 327.78 ns | 17.97 ns |  1.00 |    0.00 | 0.2613 |      - |   1,232 B |
|     PaspanFluentBig |  4,490.1 ns |  50.25 ns |  2.75 ns |  4.32 |    0.08 | 0.7324 |      - |   3,464 B |
|        ParlotRawBig |  1,689.9 ns |  34.60 ns |  1.90 ns |  1.63 |    0.03 | 0.2537 |      - |   1,200 B |
|   ParlotCompiledBig |  3,247.5 ns |  89.53 ns |  4.91 ns |  3.13 |    0.06 | 0.6104 | 0.0038 |   2,888 B |
|     ParlotFluentBig |  4,567.0 ns |  96.86 ns |  5.31 ns |  4.40 |    0.07 | 0.6104 |      - |   2,888 B |
|           PidginBig | 45,716.8 ns | 698.94 ns | 38.31 ns | 44.03 |    0.78 | 0.8545 |      - |   4,152 B |
```

### JSON Benchmarks

This benchmark was taken from the Pidgin repository and demonstrates how to perform simple JSON document parsing. It exercises the parsers with different kinds of documents. Pidgin, Parlot, Paspan and System.Text.Json are compared. The programming models are all except System.Text.Json based on parser combinator.

```
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19043.1348 (21H1/May2021Update)
Intel Core i5-9600K CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK=6.0.100
  [Host]   : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT
  ShortRun : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

|                      Method |         Mean |       Error |    StdDev | Ratio | RatioSD |     Gen 0 |    Gen 1 | Allocated |
|---------------------------- |-------------:|------------:|----------:|------:|--------:|----------:|---------:|----------:|
|              BigJson_Paspan |   148.170 us |   2.8040 us | 0.1537 us |  1.00 |    0.00 |   23.4375 |   6.8359 |    109 KB |
|          BigJson_PaspanUtf8 |   150.457 us |   3.3655 us | 0.1845 us |  1.02 |    0.00 |   21.9727 |   6.3477 |    102 KB |
|    BigJson_PaspanUtf8Region |   151.438 us |   2.3425 us | 0.1284 us |  1.02 |    0.00 |   22.9492 |   5.6152 |    106 KB |
|  BigJson_DotNetJsonDocument |    28.999 us |   0.4563 us | 0.0250 us |  0.20 |    0.00 |    3.4790 |   0.3662 |     16 KB |
|              BigJson_Parlot |   170.500 us |   2.7210 us | 0.1491 us |  1.15 |    0.00 |   21.9727 |   6.3477 |    102 KB |
|      BigJson_ParlotCompiled |   151.327 us |   4.2170 us | 0.2311 us |  1.02 |    0.00 |   21.9727 |   6.3477 |    102 KB |
|              BigJson_Pidgin |   282.659 us |  12.8734 us | 0.7056 us |  1.91 |    0.00 |   21.9727 |   6.3477 |    102 KB |
|             BigJson_Sprache | 2,067.098 us | 134.6442 us | 7.3803 us | 13.95 |    0.06 | 1144.5313 |   7.8125 |  5,274 KB |
|                             |              |             |           |       |         |           |          |           |
|             LongJson_Paspan |   124.743 us |  10.2408 us | 0.5613 us |  1.00 |    0.00 |   23.6816 |   5.8594 |    109 KB |
|         LongJson_PaspanUtf8 |   116.325 us |   0.6299 us | 0.0345 us |  0.93 |    0.00 |   22.5830 |   5.6152 |    104 KB |
|   LongJson_PaspanUtf8Region |   118.263 us |   0.1529 us | 0.0084 us |  0.95 |    0.00 |   23.5596 |   5.1270 |    108 KB |
| LongJson_DotNetJsonDocument |    21.725 us |   0.2259 us | 0.0124 us |  0.17 |    0.00 |    3.4790 |   0.3662 |     16 KB |
|             LongJson_Parlot |   128.829 us |   0.7932 us | 0.0435 us |  1.03 |    0.00 |   22.7051 |   5.6152 |    104 KB |
|     LongJson_ParlotCompiled |   111.977 us |   1.2224 us | 0.0670 us |  0.90 |    0.00 |   22.7051 |   5.6152 |    104 KB |
|             LongJson_Pidgin |   250.967 us |  20.5418 us | 1.1260 us |  2.01 |    0.01 |   22.4609 |   5.3711 |    104 KB |
|            LongJson_Sprache | 1,777.950 us |  27.6450 us | 1.5153 us | 14.25 |    0.08 |  923.8281 |   3.9063 |  4,245 KB |
|                             |              |             |           |       |         |           |          |           |
|             DeepJson_Paspan |    21.690 us |   0.2236 us | 0.0123 us |  1.00 |    0.00 |    4.3640 |   0.3357 |     20 KB |
|         DeepJson_PaspanUtf8 |    22.038 us |   0.4483 us | 0.0246 us |  1.02 |    0.00 |    4.2114 |   0.3052 |     19 KB |
|   DeepJson_PaspanUtf8Region |    22.664 us |   0.9363 us | 0.0513 us |  1.04 |    0.00 |    4.8218 |   0.3357 |     22 KB |
| DeepJson_DotNetJsonDocument |     8.142 us |   0.1774 us | 0.0097 us |  0.38 |    0.00 |    0.8850 |   0.0153 |      4 KB |
|             DeepJson_Parlot |    23.958 us |   0.2446 us | 0.0134 us |  1.10 |    0.00 |    4.2419 |   0.2136 |     20 KB |
|     DeepJson_ParlotCompiled |    18.670 us |   1.1347 us | 0.0622 us |  0.86 |    0.00 |    4.2419 |   0.3052 |     20 KB |
|             DeepJson_Pidgin |    59.390 us |   0.5491 us | 0.0301 us |  2.74 |    0.00 |    8.6060 |   1.0376 |     40 KB |
|            DeepJson_Sprache |   268.485 us |  15.1502 us | 0.8304 us | 12.38 |    0.04 |  148.9258 |  29.2969 |    686 KB |
|                             |              |             |           |       |         |           |          |           |
|             WideJson_Paspan |    91.331 us |   0.8223 us | 0.0451 us |  1.00 |    0.00 |   11.3525 |   2.1973 |     53 KB |
|         WideJson_PaspanUtf8 |    90.080 us |   0.8997 us | 0.0493 us |  0.99 |    0.00 |   10.4980 |   2.0752 |     48 KB |
|   WideJson_PaspanUtf8Region |   362.736 us |  15.2640 us | 0.8367 us |  3.97 |    0.01 |   66.8945 |  10.7422 |    308 KB |
| WideJson_DotNetJsonDocument |    12.245 us |   0.1036 us | 0.0057 us |  0.13 |    0.00 |    1.7548 |   0.0916 |      8 KB |
|             WideJson_Parlot |   105.022 us |   2.6111 us | 0.1431 us |  1.15 |    0.00 |   10.4980 |   2.0752 |     49 KB |
|     WideJson_ParlotCompiled |   100.364 us |   6.8092 us | 0.3732 us |  1.10 |    0.00 |   10.4980 |   2.0752 |     49 KB |
|             WideJson_Pidgin |   157.780 us |  15.9412 us | 0.8738 us |  1.73 |    0.01 |   10.4980 |   1.9531 |     48 KB |
|            WideJson_Sprache |   915.958 us |  75.2069 us | 4.1223 us | 10.03 |    0.05 |  600.5859 | 120.1172 |  2,761 KB |
```

TODO: As we see Paspan has performance issues when parser creates Region.

### Regex Benchmarks

```
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19043.1348 (21H1/May2021Update)
Intel Core i5-9600K CPU 3.70GHz (Coffee Lake), 1 CPU, 6 logical and 6 physical cores
.NET SDK=6.0.100
  [Host]   : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT
  ShortRun : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

|              Method |      Mean |     Error |   StdDev |  Gen 0 | Allocated |
|-------------------- |----------:|----------:|---------:|-------:|----------:|
|          RegexEmail | 197.28 ns |  3.365 ns | 0.184 ns | 0.0441 |     208 B |
|  RegexEmailCompiled |  86.77 ns |  4.406 ns | 0.242 ns | 0.0441 |     208 B |
|         PaspanEmail | 327.03 ns |  4.814 ns | 0.264 ns | 0.0882 |     416 B |
|    PaspanEmailBytes | 295.21 ns |  8.025 ns | 0.440 ns | 0.0782 |     368 B |
|   PaspanEmailRegion | 279.53 ns |  2.304 ns | 0.126 ns | 0.0644 |     304 B |
|      PaspanEmailRaw |  49.27 ns |  0.070 ns | 0.004 ns |      - |         - |
|         ParlotEmail | 308.66 ns | 44.932 ns | 2.463 ns | 0.0677 |     320 B |
| ParlotEmailCompiled | 131.05 ns |  3.307 ns | 0.181 ns | 0.0272 |     128 B |
```


### Usages

Parlot, the parent of Paspan is already used in these projects:
- [Shortcodes](https://github.com/sebastienros/shortcodes)
- [Fluid](https://github.com/sebastienros/fluid)
- [OrchardCore](https://github.com/OrchardCMS/OrchardCore)
