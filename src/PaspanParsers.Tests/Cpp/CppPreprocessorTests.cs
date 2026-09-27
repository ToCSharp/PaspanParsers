using PaspanParsers.Cpp;

namespace PaspanParsers.Tests.Cpp;

[TestClass]
public class CppPreprocessorTests
{
    private static TranslationUnit Parse(string source, CppParseOptions options = null)
    {
        var success = CppParser.TryParse(source, options, out var unit, out var error);
        Assert.IsTrue(success, $"failed to parse: {source}\n({error?.Line},{error?.Column}): {error?.Message}");
        return unit;
    }

    /// <summary>
    /// The names of the variables the source declares, in order.
    /// </summary>
    private static List<string> DeclaredNames(string source, CppParseOptions options = null)
    {
        return Parse(source, options).Declarations
            .OfType<SimpleDeclaration>()
            .SelectMany(d => d.Declarators)
            .Select(d => ((NameDeclarator)d.Declarator).Name)
            .ToList();
    }

    private static string Write(TranslationUnit unit)
    {
        var writer = new CppWriter();
        writer.WriteTranslationUnit(unit);
        return writer.GetResult();
    }

    // ========================================
    // Directives as trivia
    // ========================================

    [TestMethod]
    public void Directives_AreTriviaKeptInTheTree()
    {
        var source = "#pragma once\n#include <cstddef> // comment\n  #  define X 1 /* comment */\nint a = X;\n";
        var unit = Parse(source);

        Assert.HasCount(1, unit.Declarations);
        var kinds = unit.Directives.Select(d => d.Kind).ToList();
        CollectionAssert.AreEqual(new[] { PreprocessorDirectiveKind.Pragma, PreprocessorDirectiveKind.Include, PreprocessorDirectiveKind.Define }, kinds);

        var include = unit.Directives[1];
        Assert.AreEqual("include", include.Name);
        Assert.AreEqual("<cstddef>", include.Arguments);
        Assert.AreEqual("#include <cstddef>", include.Text);
        Assert.AreEqual("#include <cstddef>", include.Span.GetText(System.Text.Encoding.UTF8.GetBytes(source)));

        var define = unit.Directives[2];
        Assert.AreEqual("#  define X 1", define.Text);
        Assert.AreEqual("X", define.Macro.Name);
        Assert.IsNull(define.Macro.Parameters);
        Assert.AreEqual("1", define.Macro.Replacement);

        // All directives lead the declaration and every node that starts with 'int'
        var declaration = (SimpleDeclaration)unit.Declarations[0];
        CollectionAssert.AreEqual(unit.Directives.ToList(), declaration.LeadingDirectives.ToList());
        Assert.AreSame(declaration.LeadingDirectives, declaration.Specifiers.LeadingDirectives);
        Assert.IsNull(declaration.Declarators[0].LeadingDirectives);
    }

    [TestMethod]
    public void Directive_StartsOnlyAtTheStartOfALine()
    {
        // After a block comment that starts on an earlier line, as in clang
        var unit = Parse("int a;\n/* comment\n */ #define X 1\nint b;\n");
        Assert.HasCount(1, unit.Directives);
        Assert.HasCount(2, unit.Declarations);

        // Digraph and comments before the name
        unit = Parse("%: /**/ define X 1\nint a;\n");
        Assert.AreEqual(PreprocessorDirectiveKind.Define, unit.Directives[0].Kind);

        // A '#' after a token on the same line is no directive
        CppTestHelper.AssertParseFails("int a; /* comment\n */ #define X 1\n");
    }

    [TestMethod]
    public void Directive_EndsAtTheEndOfTheLogicalLine()
    {
        var source = "#define LONG 1 + \\\n  2 /* a comment\n spanning lines */\nint a;\n#def\\\nine X 3\nint b;\n";
        var unit = Parse(source);

        Assert.HasCount(2, unit.Declarations);
        Assert.AreEqual("1 + 2", unit.Directives[0].Macro.Replacement);
        Assert.AreEqual("#define LONG 1 + \\\n  2", unit.Directives[0].Text);
        Assert.AreEqual(PreprocessorDirectiveKind.Define, unit.Directives[1].Kind);
        Assert.AreEqual("X", unit.Directives[1].Macro.Name);
    }

    [TestMethod]
    public void HashInLiteralsAndComments_IsNoDirective()
    {
        var unit = Parse("auto s = R\"(\n#if 0\n)\";\n// #if 0\n/*\n#if 0\n*/\nint a;\n");
        Assert.IsEmpty(unit.Directives);
        Assert.HasCount(2, unit.Declarations);
    }

    [TestMethod]
    public void OtherDirectives_HaveTheirKinds()
    {
        var unit = Parse("#\n# 10 \"file.cpp\"\n#line 20\n#ident \"x\"\n#warning careful\n#include_next <a.h>\n#assert x(y)\nint a;\n");
        var kinds = unit.Directives.Select(d => d.Kind).ToList();
        CollectionAssert.AreEqual(
            new[]
            {
                PreprocessorDirectiveKind.Null, PreprocessorDirectiveKind.Line, PreprocessorDirectiveKind.Line, PreprocessorDirectiveKind.Ident,
                PreprocessorDirectiveKind.Warning, PreprocessorDirectiveKind.IncludeNext, PreprocessorDirectiveKind.Other,
            },
            kinds);
        Assert.AreEqual("10 \"file.cpp\"", unit.Directives[1].Arguments);
    }

    [TestMethod]
    public void FunctionLikeMacro_HasItsParameters()
    {
        var unit = Parse("#define F(a, b) a ## b\n#define G(x, ...) x __VA_ARGS__\n#define H(args...) args\n#define O (x)\nint a;\n");
        var macros = unit.Directives.Select(d => d.Macro).ToList();

        CollectionAssert.AreEqual(new[] { "a", "b" }, macros[0].Parameters.ToList());
        Assert.AreEqual("a ## b", macros[0].Replacement);
        Assert.IsFalse(macros[0].IsVariadic);
        CollectionAssert.AreEqual(new[] { "x" }, macros[1].Parameters.ToList());
        Assert.IsTrue(macros[1].IsVariadic);
        CollectionAssert.AreEqual(new[] { "args" }, macros[2].Parameters.ToList());
        Assert.IsTrue(macros[2].IsVariadic);
        Assert.IsFalse(macros[3].IsFunctionLike);
        Assert.AreEqual("(x)", macros[3].Replacement);
    }

    // ========================================
    // Conditional directives
    // ========================================

    [TestMethod]
    public void InactiveBranches_AreSkipped()
    {
        var source = """
            #if 0
            this is not C++ #if
            #if 1
            neither is this
            #else
            nor this
            #endif
            #elif 1
            int taken;
            #elif 1
            int later;
            #else
            int other;
            #endif
            int after;
            """;

        CollectionAssert.AreEqual(new[] { "taken", "after" }, DeclaredNames(source));

        // The nested conditional of the inactive branch is not processed
        var unit = Parse(source);
        var kinds = unit.Directives.Select(d => d.Kind).ToList();
        CollectionAssert.AreEqual(
            new[] { PreprocessorDirectiveKind.If, PreprocessorDirectiveKind.Elif, PreprocessorDirectiveKind.Elif, PreprocessorDirectiveKind.Else, PreprocessorDirectiveKind.Endif },
            kinds);
        CollectionAssert.AreEqual(new[] { false, true, false, false, false }, unit.Directives.Select(d => d.IsBranchTaken).ToList());
    }

    [TestMethod]
    public void DefinedMacros_DecideLaterConditions()
    {
        var source = """
            #ifndef GUARD
            #define GUARD
            #define VERSION 3
            #ifdef GUARD
            int guarded;
            #endif
            #if VERSION >= 3 && defined(GUARD) && defined VERSION
            int version3;
            #endif
            #undef VERSION
            #ifdef VERSION
            int defined_again;
            #elifndef VERSION
            int undefined;
            #endif
            #if VERSION == 0
            int zero;
            #endif
            #endif
            """;

        CollectionAssert.AreEqual(new[] { "guarded", "version3", "undefined", "zero" }, DeclaredNames(source));
    }

    [TestMethod]
    public void ConditionsWithMacros_AreExpanded()
    {
        var source = """
            #define ADD(a, b) ((a) + (b))
            #define TWICE(x) ADD(x, x)
            #define SELF SELF + 1
            #define CAT(a, b) a ## b
            #define ONE_TWO 12
            #define COUNT(...) COUNT_(__VA_ARGS__, 3, 2, 1, 0)
            #define COUNT_(a, b, c, n, ...) n
            #define OPT(x, ...) x __VA_OPT__(+ 1)
            #define EMPTY
            #if TWICE(3) == 6
            int twice;
            #endif
            #if SELF == 1
            int self;
            #endif
            #if CAT(ONE_, TWO) == 12 && CAT(1, 2) == 12
            int pasted;
            #endif
            #if COUNT(a, b) == 2 && COUNT(a) == 1
            int variadic;
            #endif
            #if OPT(1) == 1 && OPT(1, x) == 2
            int va_opt;
            #endif
            #if EMPTY 1 EMPTY
            int empty;
            #endif
            #if ADD
            int not_called;
            #endif
            """;

        CollectionAssert.AreEqual(new[] { "twice", "self", "pasted", "variadic", "va_opt", "empty" }, DeclaredNames(source));
    }

    [TestMethod]
    public void Conditions_AreIntegerExpressions()
    {
        foreach (var (condition, expected) in new[]
        {
            ("(1 + 2) * 3 == 9 && 10 / 3 == 3 && 10 % 3 == 1", true),
            ("(0x10 >> 2) == 4 && (1 << 4) == 0b1'0000 && 010 == 8", true),
            ("-1 < 0 && !(-1 < 0u) && 0xFFFFFFFFFFFFFFFF > 0", true),
            ("(1 ? 2 : 3) == 2 && (0 ? 2 : 3) == 3", true),
            ("'a' == 97 && '\\n' == 10 && '\\xff' < 0 && u'\\xff' > 0", true),
            ("(~0 & 0xF) == 15 && (5 ^ 3) == 6 && (4 | 1) == 5", true),
            ("true && !false && not 0 and 1", true),
            ("undefined_name == 0 && !undefined_name", true),
            ("0 && 1 / 0", false),
            ("1 || 1 / 0", true),
            ("1 / 0", false),
            ("1.5", false),
            ("", false),
            ("(1", false),
        })
        {
            var names = DeclaredNames($"#if {condition}\nint yes;\n#else\nint no;\n#endif\n");
            CollectionAssert.AreEqual(new[] { expected ? "yes" : "no" }, names, condition);
        }
    }

    [TestMethod]
    public void Options_DefineMacros()
    {
        var options = new CppParseOptions(macros: new Dictionary<string, string>
        {
            ["__cplusplus"] = "202302L",
            ["DEBUG"] = "",
            ["F(x)"] = "x + 1",
        });

        var names = DeclaredNames("#if __cplusplus >= 202002L && defined(DEBUG) && F(1) == 2\nint yes;\n#endif\n", options);
        CollectionAssert.AreEqual(new[] { "yes" }, names);
    }

    [TestMethod]
    public void HasInclude_LooksInTheIncludeDirectories()
    {
        var directory = Directory.CreateTempSubdirectory("paspan-cpp-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, "present.h"), "");
            Directory.CreateDirectory(Path.Combine(directory.FullName, "sub"));
            File.WriteAllText(Path.Combine(directory.FullName, "sub", "nested.h"), "");
            var options = new CppParseOptions(includeDirectories: [directory.FullName]);

            var source = """
                #define HEADER <sub/nested.h>
                #if __has_include(<present.h>) && __has_include("present.h") && __has_include(HEADER)
                int present;
                #endif
                #if __has_include(<absent.h>) || defined(__has_include) == 0
                int absent;
                #endif
                """;

            CollectionAssert.AreEqual(new[] { "present" }, DeclaredNames(source, options));

            // Quoted names are looked up in the directory of the source first
            options = new CppParseOptions(sourceDirectory: directory.FullName);
            CollectionAssert.AreEqual(new[] { "yes" }, DeclaredNames("#if __has_include(\"present.h\") && !__has_include(<present.h>)\nint yes;\n#endif\n", options));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void FeatureTests_UseTables()
    {
        var source = """
            #if __has_cpp_attribute(nodiscard) >= 201907L && __has_cpp_attribute(gnu::always_inline) && !__has_cpp_attribute(unknown)
            int attributes;
            #endif
            #if __has_builtin(__builtin_expect) && __has_builtin(__is_same) && !__has_builtin(unknown)
            int builtins;
            #endif
            #if __has_feature(cxx_rtti) && !__has_feature(address_sanitizer) && __has_attribute(always_inline)
            int features;
            #endif
            #if __is_identifier(abc) && !__is_identifier(int) && __LINE__ == 10
            int identifiers;
            #endif
            """;

        CollectionAssert.AreEqual(new[] { "attributes", "builtins", "features", "identifiers" }, DeclaredNames(source));
    }

    // ========================================
    // Writer
    // ========================================

    [TestMethod]
    public void Writer_WritesDirectivesOnTheirOwnLines()
    {
        var source = "#include <cstddef>\n#if 1\n#define ONE 1\n#else\n#define ONE 2\n#endif\nint a = 1 +\n#define TWO 2\n  TWO;\nvoid f()\n{\n  int b;\n#pragma inside\n}\n#pragma end\n";
        var written = Write(Parse(source));

        Assert.AreEqual(
            "#include <cstddef>\n#define ONE 1\nint a = 1 +\n#define TWO 2\nTWO;\nvoid f()\n{\n    int b;\n#pragma inside\n}\n#pragma end\n",
            written.ReplaceLineEndings("\n"));
    }

    // ========================================
    // Oracle
    // ========================================

    [TestMethod]
    public void Oracle_DirectivesAndConditionals()
    {
        CppTestHelper.AssertOracle("""
            #pragma once
            #ifndef GUARD
            #define GUARD
            #define VALUE 40
            #define ADD(a, b) ((a) + (b))
            #if defined(__clang__) && __cplusplus >= 202302L
            int modern = ADD(VALUE, 2);
            #elif 1
            int old = 1;
            #else
            #error "unsupported"
            #endif
            int f(int x)
            {
            #ifdef VALUE
                return x + VALUE;
            #else
                return x;
            #endif
            #pragma unused
            }
            #undef VALUE
            #endif // GUARD
            """);
    }

    [TestMethod]
    public void Oracle_DirectiveInsideAnExpression()
    {
        CppTestHelper.AssertOracle("int a = 1 +\n#define TWO 2\n    TWO;\n");
    }
}
