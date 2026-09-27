// Preprocessor directives: guards, conditionals, macros used as names, pragmas.

#pragma once
#ifndef CORPUS_PREPROCESSOR_H
#define CORPUS_PREPROCESSOR_H

#include <cstddef>
#include "02-Preprocessor.inc"

#define VERSION 3
#define SQUARE(x) ((x) * (x))
#define STRINGIFY(x) #x
#define EMPTY

#if VERSION >= 3 && defined(__cplusplus) && __cplusplus >= 202002L
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
#elif (1 + 2) * 3 == 9 && (0x10 >> 2) == 4 && 'a' == 97 && -1 < 0 && (1 ? 2 : 3) == 2
int arithmetic = 1;
#endif

#if __has_include(<cstddef>)
std::size_t size = sizeof(int);
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
int from_include = included_value;

#pragma pack(push, 1)
struct Packed { char c; int i; };
#pragma pack(pop)

#line 100 "renamed.cpp"
int after_line = __LINE__;

#endif // CORPUS_PREPROCESSOR_H
