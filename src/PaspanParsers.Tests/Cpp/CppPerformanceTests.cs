using System.Diagnostics;
using System.Text;
using Paspan;
using PaspanParsers.Cpp;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// Performance of the C++ parser (stage 9): deeply nested and long input must parse in linear time without
/// stack overflow, and <see cref="Benchmark_Corpus"/> measures time and allocations on a corpus, with
/// clang's <c>-fsyntax-only</c> as a reference.
/// </summary>
[TestClass]
public class CppPerformanceTests
{
    private static readonly TimeSpan DeepInputTimeout = TimeSpan.FromSeconds(10);

    public TestContext TestContext { get; set; }

    [TestMethod]
    [DataRow(1000)]
    [DataRow(5000)]
    public void NestedParentheses_Parse(int depth)
    {
        var expression = new string('(', depth) + "a" + new string(')', depth);
        var unit = ParseDeep($"int a;\nint f() {{ return {expression}; }}");

        var nested = ((ReturnStatement)Body(unit, 1).Statements[0]).Expression;
        for (var i = 0; i < depth; i++)
        {
            nested = ((ParenthesizedExpression)nested).Expression;
        }

        Assert.IsInstanceOfType<NameExpression>(nested);
        AssertWritesBack(unit, expression);
    }

    [TestMethod]
    [DataRow(1000)]
    [DataRow(5000)]
    public void NestedParenthesesOfUnknownNames_Parse(int depth)
    {
        // Each '(' might start a cast of an unknown type name: (a)(b)
        var expression = new string('(', depth) + "a" + new string(')', depth) + "(b)";
        var unit = ParseDeep($"int f() {{ return {expression}; }}");
        AssertWritesBack(unit, expression);
    }

    [TestMethod]
    [DataRow(5000)]
    [DataRow(50000)]
    public void LongBinaryChain_Parses(int count)
    {
        var expression = string.Join(" + ", Enumerable.Repeat("a", count));
        var source = $"int a;\nint f() {{ return {expression}; }}";
        var unit = ParseDeep(source);

        var returned = ((ReturnStatement)Body(unit, 1).Statements[0]).Expression;
        Assert.AreEqual(expression, returned.Span.GetText(source));
    }

    [TestMethod]
    [DataRow(5000)]
    [DataRow(50000)]
    public void LongComparisonChainOfUnknownNames_Parses(int count)
    {
        // Each '<' after a name might start template arguments
        var expression = string.Join(" < ", Enumerable.Repeat("a", count));
        var unit = ParseDeep($"bool f() {{ return {expression}; }}");
        AssertWritesBack(unit, expression);
    }

    [TestMethod]
    [DataRow(200)]
    public void ComparisonChainClosedByGreater_Parses(int count)
    {
        // Every '<' may start template arguments that the last '>' closes: exponential without the cache of
        // template arguments by position
        var expression = string.Join(" < ", Enumerable.Repeat("a", count)) + " > 0";
        var unit = ParseDeep($"bool f() {{ return {expression}; }}");
        AssertWritesBack(unit, expression);
    }

    [TestMethod]
    [DataRow(5000)]
    public void LongRightAssociativeChains_Parse(int count)
    {
        ParseDeep("int a;\nvoid f() { " + string.Join(" = ", Enumerable.Repeat("a", count)) + "; }");
        ParseDeep("int a;\nint f() { return " + string.Concat(Enumerable.Repeat("a ? a : ", count)) + "a; }");
        ParseDeep("int a;\nint f() { return " + string.Concat(Enumerable.Repeat("-!~", count)) + "a; }");
        ParseDeep("int *a;\nint f() { return " + new string('*', count) + "a; }");
    }

    [TestMethod]
    [DataRow(5000)]
    public void NestedCallsCastsAndInitializers_Parse(int depth)
    {
        ParseDeep("int f(int);\nint g() { return " + string.Concat(Enumerable.Repeat("f(", depth)) + "1" + new string(')', depth) + "; }");
        ParseDeep("int a;\nint g() { return " + string.Concat(Enumerable.Repeat("(int)", depth)) + "a; }");
        ParseDeep("typedef int T;\nint a;\nint g() { return " + string.Concat(Enumerable.Repeat("(T)", depth)) + "a; }");
        ParseDeep("struct S { S(int); };\nS g() { return " + string.Concat(Enumerable.Repeat("S(", depth)) + "1" + new string(')', depth) + "; }");
        ParseDeep("int a[] = " + new string('{', depth) + "1" + new string('}', depth) + ";");
        ParseDeep("int a;\nint g() { return " + string.Concat(Enumerable.Repeat("sizeof(", depth)) + "a" + new string(')', depth) + "; }");
    }

    [TestMethod]
    [DataRow(5000)]
    public void LongPostfixChains_Parse(int count)
    {
        ParseDeep("struct S { S &f(); int a[1]; };\nS s;\nvoid g() { s" + string.Concat(Enumerable.Repeat(".f()", count)) + "; }");
        ParseDeep("int ***a;\nvoid g() { a" + string.Concat(Enumerable.Repeat("[0]", count)) + "; }");
        ParseDeep("void g() { u" + string.Concat(Enumerable.Repeat("->v", count)) + "; }");
    }

    [TestMethod]
    [DataRow(1000)]
    public void NestedTemplateArguments_Parse(int depth)
    {
        var type = string.Concat(Enumerable.Repeat("A<", depth)) + "int" + new string('>', depth);
        var unit = ParseDeep($"template <class T> struct A {{}};\n{type} x;");
        AssertWritesBack(unit, type);

        // In expressions, and with names the parser does not know
        ParseDeep($"template <class T> struct A {{}};\ntemplate <class T> int f();\nint g() {{ return f<{type}>(); }}");
        ParseDeep("void g() { " + string.Concat(Enumerable.Repeat("U<", depth)) + "int" + new string('>', depth) + " x; }");
        ParseDeep("int g() { return f<" + string.Concat(Enumerable.Repeat("U<", depth)) + "int" + new string('>', depth) + ">(); }");
    }

    [TestMethod]
    [DataRow(1000)]
    public void NestedDecltype_Parses(int depth)
    {
        // Each decltype was parsed as a name and again as a type specifier: exponential
        var type = string.Concat(Enumerable.Repeat("decltype(", depth)) + "a" + string.Concat(Enumerable.Repeat(")()", depth - 1)) + ")";
        var unit = ParseDeep($"int a;\n{type} b;");
        AssertWritesBack(unit, type);
    }

    [TestMethod]
    [DataRow(5000)]
    public void LongQualifiedNames_Parse(int count)
    {
        var qualifier = string.Concat(Enumerable.Repeat("n::", count));
        ParseDeep($"int g() {{ return {qualifier}x; }}");
        ParseDeep($"{qualifier}T x;");
        ParseDeep($"int {qualifier}x = 1;");
    }

    [TestMethod]
    [DataRow(1000)]
    public void NestedDeclarators_Parse(int depth)
    {
        ParseDeep("int " + new string('(', depth) + "x" + new string(')', depth) + ";");
        ParseDeep("int " + new string('*', depth) + "x;");
        ParseDeep("int " + string.Concat(Enumerable.Repeat("(*", depth)) + "f" + string.Concat(Enumerable.Repeat(")(int)", depth)) + ";");
        ParseDeep("int x" + string.Concat(Enumerable.Repeat("[1]", depth)) + ";");
        ParseDeep("void f(" + string.Concat(Enumerable.Repeat("void (*)(", depth)) + "int" + new string(')', depth) + ");");
        ParseDeep("int x" + string.Concat(Enumerable.Repeat("[1]", depth * 5)) + ";");

        var parameters = "template <" + string.Concat(Enumerable.Repeat("template <", depth)) + "class" + string.Concat(Enumerable.Repeat("> class", depth)) + " T> struct X;";
        AssertWritesBack(ParseDeep(parameters), parameters);
    }

    [TestMethod]
    [DataRow(1000)]
    public void NestedBlocksAndLambdas_Parse(int depth)
    {
        var blocks = new StringBuilder();
        for (var i = 0; i < depth; i++)
        {
            blocks.Append(i % 2 == 0 ? "{ " : "f([&] { ");
        }

        for (var i = depth - 1; i >= 0; i--)
        {
            blocks.Append(i % 2 == 0 ? "} " : "}); ");
        }

        var unit = ParseDeep($"template <class F> void f(F);\nvoid g() {blocks}");
        AssertWritesBack(unit, "f([&]{");
    }

    [TestMethod]
    [DataRow(1000)]
    public void NestedStatements_Parse(int depth)
    {
        ParseDeep("int a;\nvoid f() { " + string.Concat(Enumerable.Repeat("if (a) ", depth)) + "; }");
        ParseDeep("int a;\nvoid f() { " + string.Concat(Enumerable.Repeat("if (a) {} else ", depth)) + "; }");
        ParseDeep("int a;\nvoid f() { " + string.Concat(Enumerable.Repeat("while (a) for (;;) ", depth)) + "; }");
        ParseDeep("int a;\nvoid f() { " + string.Concat(Enumerable.Repeat("l: ", depth)) + "; }");
        ParseDeep("int a;\nvoid f() { " + string.Concat(Enumerable.Repeat("switch (a) case 1: ", depth)) + "; }");
        ParseDeep("void f() { " + string.Concat(Enumerable.Repeat("try { ", depth)) + string.Concat(Enumerable.Repeat("} catch (...) {} ", depth)) + "}");
    }

    [TestMethod]
    [DataRow(1000)]
    public void NestedDeclarations_Parse(int depth)
    {
        ParseDeep(string.Concat(Enumerable.Repeat("namespace n { ", depth)) + "int x;" + new string('}', depth));
        ParseDeep(string.Concat(Enumerable.Repeat("extern \"C++\" { ", depth)) + "int x;" + new string('}', depth));

        // Member function bodies are parsed when the outermost class is complete
        var classes = new StringBuilder();
        for (var i = 0; i < depth; i++)
        {
            classes.Append($"struct S{i} {{ int f() {{ return g(); }} int g(); ");
        }

        for (var i = depth - 1; i >= 0; i--)
        {
            classes.Append("}; ");
        }

        ParseDeep(classes.ToString());
        ParseDeep(string.Concat(Enumerable.Repeat("template <class T> struct A { ", depth)) + "T x;" + string.Concat(Enumerable.Repeat("};", depth)));
    }

    [TestMethod]
    [DataRow(1000)]
    public void NestedConditionalDirectives_Parse(int depth)
    {
        var source = new StringBuilder();
        for (var i = 0; i < depth; i++)
        {
            source.Append(i % 2 == 0 ? "#if 1\n" : "#ifdef X\nint y;\n#else\n");
        }

        source.Append("int x;\n");
        for (var i = 0; i < depth; i++)
        {
            source.Append("#endif\n");
        }

        var unit = ParseDeep(source.ToString());
        Assert.HasCount(1, unit.Declarations);

        ParseDeep("#if " + new string('(', depth) + "1" + new string(')', depth) + "\nint x;\n#endif\n");
        ParseDeep("#if " + string.Join(" + ", Enumerable.Repeat("1", depth * 10)) + "\nint x;\n#endif\n");
        ParseDeep("#if " + string.Concat(Enumerable.Repeat("1 ? ", depth)) + "1" + string.Concat(Enumerable.Repeat(" : 0", depth)) + "\nint x;\n#endif\n");
        ParseDeep("#if " + new string('!', depth) + "0\nint x;\n#endif\n");

        // Macros that expand to macros, and nested calls of a function-like macro
        ParseDeep(string.Concat(Enumerable.Range(0, depth).Select(i => $"#define M{i + 1} M{i}\n")) + $"#define M0 1\n#if M{depth}\nint x;\n#endif\n");
        ParseDeep("#define F(x) x\n#if " + string.Concat(Enumerable.Repeat("F(", depth)) + "1" + new string(')', depth) + "\nint x;\n#endif\n");
    }

    [TestMethod]
    [DataRow(20000)]
    public void LongLists_Parse(int count)
    {
        ParseDeep(string.Concat(Enumerable.Range(0, count).Select(i => $"int f{i}(int a) {{ return a + {i}; }}\n")));
        ParseDeep("int a;\nvoid f() {\n" + string.Concat(Enumerable.Repeat("a = a * 2 + 1;\n", count)) + "}");
        ParseDeep("int a[] = {" + string.Join(", ", Enumerable.Range(0, count)) + "};");
        ParseDeep("const char *s = " + string.Join(" ", Enumerable.Repeat("\"a\"", count)) + ";");
        ParseDeep("enum E {" + string.Join(", ", Enumerable.Range(0, count).Select(i => $"e{i}")) + "};");
        ParseDeep("struct S {\n" + string.Concat(Enumerable.Range(0, count).Select(i => $"int f{i}() {{ return m{count - 1 - i}; }} int m{i};\n")) + "};");
        ParseDeep(string.Concat(Enumerable.Range(0, count).Select(i => $"#define M{i} {i}\n#if M{i}\nint x{i};\n#endif\n")));
    }

    private static CompoundStatement Body(TranslationUnit unit, int index) => ((FunctionDefinition)unit.Declarations[index]).Body;

    /// <summary>
    /// Parses on a thread with a 1 MB stack, like thread pool threads, within a time limit. Input nested
    /// deeper than the stack allows is parsed again on a larger stack by <see cref="CppParser"/>.
    /// </summary>
    private static TranslationUnit ParseDeep(string source)
    {
        TranslationUnit unit = null;
        ParseError error = null;
        var thread = new Thread(() => CppParser.TryParse(source, out unit, out error), 1024 * 1024) { IsBackground = true };
        var stopwatch = Stopwatch.StartNew();
        thread.Start();
        Assert.IsTrue(thread.Join(DeepInputTimeout), $"did not finish in {DeepInputTimeout.TotalSeconds}s: {Shorten(source)}");
        Assert.IsNotNull(unit, $"failed to parse: {Shorten(source)}\n({error?.Line},{error?.Column}): {error?.Message}");
        Console.WriteLine($"{source.Length} chars in {stopwatch.ElapsedMilliseconds} ms: {Shorten(source)}");
        return unit;
    }

    private static string Shorten(string source) => source.Length <= 100 ? source : source[..100] + "...";

    /// <summary>
    /// Writes the tree back on a thread with a 1 MB stack (a tree nested deeper than the stack allows is
    /// written on a larger stack by <see cref="CppWriter"/>); the text without white space must contain
    /// <paramref name="expected"/> without white space.
    /// </summary>
    private static void AssertWritesBack(TranslationUnit unit, string expected)
    {
        string written = null;
        var thread = new Thread(
            () =>
            {
                var writer = new CppWriter();
                writer.WriteTranslationUnit(unit);
                written = writer.GetResult();
            },
            1024 * 1024) { IsBackground = true };
        thread.Start();
        Assert.IsTrue(thread.Join(DeepInputTimeout), $"did not finish in {DeepInputTimeout.TotalSeconds}s");
        Assert.IsNotNull(written, "failed to write");
        Assert.Contains(WithoutWhiteSpace(expected), WithoutWhiteSpace(written));
    }

    private static string WithoutWhiteSpace(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));

    /// <summary>
    /// Time and allocations of <see cref="CppParser"/> on <c>CPP_CORPUS_DIR</c> (directories separated by the
    /// path separator), and for reference the time of <c>clang++ -fsyntax-only</c> on the same files, which
    /// also reads the included headers and analyzes the code. Conditional directives are evaluated like in
    /// <see cref="CppCorpusTests.Oracle_ExternalCorpus"/>: with clang's predefined macros,
    /// <c>CPP_CORPUS_DEFINES</c> and, for <c>__has_include</c>, <c>CPP_CORPUS_INCLUDE</c>.
    /// Set <c>CPP_BENCHMARK_CLANG=0</c> to skip clang. The report is written to <c>cpp-benchmark-report.txt</c>.
    /// </summary>
    [TestMethod]
    public void Benchmark_Corpus()
    {
        var directories = (Environment.GetEnvironmentVariable("CPP_CORPUS_DIR") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Path.GetFullPath)
            .ToList();
        if (directories.Count == 0)
        {
            Assert.Inconclusive("Set CPP_CORPUS_DIR to a directory of C++ files to measure the parser on it.");
            return;
        }

        var paths = directories
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            .Where(CppCorpusTests.IsSourceFile)
            .Order(StringComparer.Ordinal)
            .ToList();
        var sources = paths.Select(path => (ReadOnlyMemory<byte>)File.ReadAllBytes(path)).ToList();
        var bytes = sources.Sum(s => (long)s.Length);

        var defines = (Environment.GetEnvironmentVariable("CPP_CORPUS_DEFINES") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var includes = (Environment.GetEnvironmentVariable("CPP_CORPUS_INCLUDE") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Path.GetFullPath)
            .ToArray();
        var oracleOptions = new ClangOracleOptions(defines, includes);
        var options = Clang.IsAvailable ? oracleOptions.ParseOptions() : CppParseOptions.Default;

        var report = new StringBuilder();
        report.AppendLine($"Corpus {string.Join(Path.PathSeparator, directories)}: {sources.Count} files, {bytes / 1024.0 / 1024.0:F1} MB");

        var failures = 0;
        Measure(report, "PaspanParsers", bytes, () =>
        {
            failures = 0;
            foreach (var source in sources)
            {
                if (!CppParser.TryParse(source, options, out _, out _))
                {
                    failures++;
                }
            }
        });

        report.AppendLine($"  files that did not parse (including files that use macros in syntactic positions): {failures}");

        // A file that does not parse stops early: the speed on the files that parse is the speed on whole files
        var parsed = sources.Where(source => CppParser.TryParse(source, options, out _, out _)).ToList();
        if (parsed.Count != 0 && failures != 0)
        {
            var parsedBytes = parsed.Sum(s => (long)s.Length);
            Measure(report, $"PaspanParsers on the {parsed.Count} files that parse, {parsedBytes / 1024.0 / 1024.0:F1} MB:", parsedBytes, () =>
            {
                foreach (var source in parsed)
                {
                    CppParser.TryParse(source, options, out _, out _);
                }
            });
        }

        if (Clang.IsAvailable && Environment.GetEnvironmentVariable("CPP_BENCHMARK_CLANG") != "0")
        {
            // One run per file, as a compiler runs; the start of the process is measured on an empty file
            var startup = Stopwatch.StartNew();
            Clang.Run([], ["-fsyntax-only"]);
            startup.Stop();

            var stopwatch = Stopwatch.StartNew();
            var clangFailures = 0;
            for (var i = 0; i < paths.Count; i++)
            {
                if (!Clang.Run(sources[i].ToArray(), ["-fsyntax-only", .. oracleOptions.ClangArguments()], Path.GetDirectoryName(paths[i])).Succeeded)
                {
                    clangFailures++;
                }
            }

            stopwatch.Stop();
            report.AppendLine($"  {"clang++",-14} {stopwatch.Elapsed.TotalMilliseconds,8:F0} ms  {stopwatch.Elapsed.TotalMilliseconds / paths.Count:F0} ms per file"
                + $" (-fsyntax-only: with the headers and semantic analysis, one process per file; an empty file takes {startup.Elapsed.TotalMilliseconds:F0} ms)");
            report.AppendLine($"  files clang rejected: {clangFailures}");
        }

        TestContext.WriteLine(report.ToString());
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "cpp-benchmark-report.txt"), report.ToString());
    }

    private static void Measure(StringBuilder report, string name, long bytes, Action run)
    {
        // Warm up until the JIT has optimized the parser (tiered compilation recompiles hot methods after
        // many calls, which a small corpus needs several runs for), then take the best of five runs
        var warmUp = Stopwatch.StartNew();
        for (var i = 0; i < 3 || warmUp.Elapsed < TimeSpan.FromSeconds(3); i++)
        {
            run();
        }

        var best = TimeSpan.MaxValue;
        long allocated = 0;
        for (var i = 0; i < 5; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var before = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            run();
            stopwatch.Stop();
            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (stopwatch.Elapsed < best)
            {
                best = stopwatch.Elapsed;
            }
        }

        var megabytes = bytes / 1024.0 / 1024.0;
        if (name.Length > 14)
        {
            report.AppendLine($"  {name}");
            name = "";
        }

        report.AppendLine($"  {name,-14} {best.TotalMilliseconds,8:F0} ms  {megabytes / best.TotalSeconds,6:F1} MB/s  allocated {allocated / 1024.0 / 1024.0,8:F0} MB ({(double)allocated / bytes:F1} bytes per source byte)");
    }
}
