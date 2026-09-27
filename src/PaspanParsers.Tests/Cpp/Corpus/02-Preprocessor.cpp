// Preprocessor directives as trivia: guards, conditionals, macros used as names, pragmas.

#pragma once
#ifndef CORPUS_PREPROCESSOR_H
#define CORPUS_PREPROCESSOR_H

#include <cstddef>
#include "02-Preprocessor.inc"

#define VERSION 3
#define SQUARE(x) ((x) * (x))
#define ADD(a, b) ((a) + (b))
#define CAT(a, b) a ## b
#define STRINGIFY(x) #x
#define COUNT(...) COUNT_(__VA_ARGS__, 3, 2, 1, 0)
#define COUNT_(a, b, c, n, ...) n
#define EMPTY
#define LONG_MACRO 1 + \
    2 + \
    3
#define COMMENTED /* a comment
    spanning lines */ 4
  #  define INDENTED 5
%:define DIGRAPH 6
#

#if VERSION >= 3 && defined(__cplusplus) && __cplusplus >= 202302L
int modern = 1;
#elif VERSION == 2
int old = 2;
#else
#error "unsupported"
#endif

#ifdef __clang__
int compiler = 1;
#endif

#if defined __GNUC__ || defined(_MSC_VER)
#  define HAS_COMPILER 1
#endif

#if !HAS_COMPILER
this is not C++
#elif (1 + 2) * 3 == 9 && (0x10 >> 2) == 4 && 'a' == 97 && -1 < 0 && (1 ? 2 : 3) == 2 && !(-1 < 0u)
int arithmetic = 1;
#endif

#if __has_include(<cstddef>) && __has_include("02-Preprocessor.inc") && !__has_include(<no/such/header.h>)
int has_include = 1;
#endif

#if __has_cpp_attribute(nodiscard) >= 201907L && __has_builtin(__builtin_expect) && !__has_cpp_attribute(unknown)
int features = 1;
#endif

#if SQUARE(VERSION) == 9 && ADD(1, 2) == 3 && CAT(VER, SION) == 3 && COUNT(a, b) == 2
int expanded = 1;
#endif

#if LONG_MACRO == 6 && COMMENTED == 4 && INDENTED + DIGRAPH == 11 && EMPTY 1 EMPTY
int line_structure = 1;
#endif

#ifdef UNDEFINED
#if nested
#else
#endif
int never;
#elifdef VERSION
int elifdef = 1;
#endif

#undef EMPTY
#ifndef EMPTY
int undefined_again = 1;
#endif

int squared = SQUARE(VERSION);
int sum = ADD(squared, VERSION) + ADD(1, 2);
int from_include = included_value;
auto name = STRINGIFY(VERSION);
auto raw = R"(
#if 0
not a directive
)";

int f(int x)
{
#if VERSION > 2
    int y = x * VERSION;
#else
    int y = x;
#endif
#pragma clang diagnostic push
    if (y > 0)
    {
        return y;
    }
#pragma clang diagnostic pop
#define LOCAL 5
    return LOCAL;
#undef LOCAL
}

int g(int a,
#define DEFAULT 7
      int b = DEFAULT)
{
    return a +
#ifdef DEFAULT
        b
#else
        0
#endif
        ;
}

#pragma pack(push, 1)
#pragma pack(pop)

#line 100 "renamed.cpp"
int after_line = 1;

#endif // CORPUS_PREPROCESSOR_H
