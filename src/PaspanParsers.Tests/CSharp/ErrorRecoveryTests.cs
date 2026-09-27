using System.Text;
using PaspanParsers.CSharp;

namespace PaspanParsers.Tests.CSharp;

/// <summary>
/// <see cref="CSharpParseOptions.ErrorRecovery"/>: invalid members and statements are skipped and the rest
/// of the input still parses; valid input parses to the same tree as without recovery.
/// </summary>
[TestClass]
public class ErrorRecoveryTests
{
    private static readonly CSharpParseOptions Recovery = new(errorRecovery: true);

    public TestContext TestContext { get; set; }

    private static CompilationUnit Parse(string source)
    {
        Assert.IsTrue(CSharpParser.TryParse(source, Recovery, out var unit, out var error), error?.Message);
        return unit;
    }

    private static string Text(string source, TextSpan span) => span.GetText(source);

    private static IReadOnlyList<MemberDeclaration> ClassMembers(CompilationUnit unit) => ((ClassDeclaration)unit.Members[0]).Members;

    private static IReadOnlyList<Statement> MethodStatements(MemberDeclaration member) =>
        ((BlockMethodBody)((MethodDeclaration)member).Body).Block.Statements;

    [TestMethod]
    public void WithoutRecovery_InvalidInputStillFails()
    {
        Assert.IsFalse(CSharpParser.TryParse("class C { void M( { } }", out _, out var error));
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void InvalidMember_IsSkipped_AndTheOtherMembersParse()
    {
        const string source = "class C\n{\n    void A() { }\n    void B( { x(); }\n    int f;\n    void D() { }\n}\n";
        var unit = Parse(source);

        var members = ClassMembers(unit);
        Assert.HasCount(4, members);
        Assert.AreEqual("A", ((MethodDeclaration)members[0]).Name);
        var incomplete = Assert.IsInstanceOfType<IncompleteMemberDeclaration>(members[1]);
        Assert.AreEqual("void B( { x(); }", incomplete.Text);
        Assert.AreEqual("void B( { x(); }", Text(source, incomplete.Span));
        Assert.IsInstanceOfType<FieldDeclaration>(members[2]);
        Assert.AreEqual("D", ((MethodDeclaration)members[3]).Name);

        Assert.HasCount(1, unit.Errors);
    }

    [TestMethod]
    public void InvalidStatement_IsSkipped_AndTheOtherStatementsParse()
    {
        const string source = "class C\n{\n    void M()\n    {\n        a();\n        b(;\n        c();\n    }\n}\n";
        var unit = Parse(source);

        var statements = MethodStatements(ClassMembers(unit)[0]);
        Assert.HasCount(3, statements);
        Assert.AreEqual("a();", Text(source, statements[0].Span));
        Assert.AreEqual("b(;", Assert.IsInstanceOfType<IncompleteStatement>(statements[1]).Text);
        Assert.AreEqual("c();", Text(source, statements[2].Span));
    }

    [TestMethod]
    public void Error_IsAtTheUnexpectedToken()
    {
        const string source = "class C\n{\n    int x = ;\n}\n";
        var unit = Parse(source);

        var error = unit.Errors.Single();
        Assert.AreEqual("Unexpected ';'", error.Message);
        Assert.AreEqual(source.IndexOf(';'), error.Span.Start);
        Assert.AreEqual(1, error.Span.Length);
        Assert.AreEqual("int x = ;", ((IncompleteMemberDeclaration)ClassMembers(unit)[0]).Text);
    }

    [TestMethod]
    public void BodiesLeftOpen_AreClosedAtTheEndOfTheInput()
    {
        const string source = "namespace N\n{\n    class C\n    {\n        void M()\n        {\n            a();\n";
        var unit = Parse(source);

        var ns = (NamespaceDeclaration)unit.Members[0];
        var type = (ClassDeclaration)ns.Members[0];
        var statements = MethodStatements(type.Members[0]);
        Assert.AreEqual("a();", Text(source, statements.Single().Span));

        // One error for the three bodies: they all end at the same place
        var error = unit.Errors.Single();
        Assert.AreEqual("'}' expected", error.Message);
        Assert.AreEqual(source.Length, error.Span.Start);
    }

    [TestMethod]
    public void StrayCloseBrace_AtTheTopLevel_IsSkipped()
    {
        var unit = Parse("class A { }\n}\nclass B { }\n");

        Assert.HasCount(3, unit.Members);
        Assert.AreEqual("A", ((ClassDeclaration)unit.Members[0]).Name);
        Assert.AreEqual("}", ((IncompleteMemberDeclaration)unit.Members[1]).Text);
        Assert.AreEqual("B", ((ClassDeclaration)unit.Members[2]).Name);
    }

    [TestMethod]
    public void InvalidMember_InNestedType_DoesNotAffectTheOuterType()
    {
        var unit = Parse("class Outer\n{\n    class Inner\n    {\n        public int = 5;\n    }\n    void M() { }\n}\n");

        var members = ClassMembers(unit);
        Assert.HasCount(2, members);
        var inner = (ClassDeclaration)members[0];
        Assert.IsInstanceOfType<IncompleteMemberDeclaration>(inner.Members.Single());
        Assert.AreEqual("M", ((MethodDeclaration)members[1]).Name);
    }

    [TestMethod]
    public void InvalidStatement_InNestedBlock_RecoversInThatBlock()
    {
        var unit = Parse("class C\n{\n    void A() { if (x) { y(); } else { z( } }\n    void B() { }\n}\n");

        var members = ClassMembers(unit);
        Assert.HasCount(2, members);

        var ifStatement = (IfStatement)MethodStatements(members[0]).Single();
        var elseBlock = (BlockStatement)ifStatement.ElseStatement;
        Assert.AreEqual("z(", ((IncompleteStatement)elseBlock.Statements.Single()).Text);
        Assert.AreEqual("B", ((MethodDeclaration)members[1]).Name);
    }

    [TestMethod]
    public void Skipping_StopsAtTheNextDeclaration()
    {
        var unit = Parse("class C\n{\n    public int X { get; set;\n    public void M() { }\n}\nclass D { }\n");

        var members = ClassMembers(unit);
        Assert.AreEqual("M", members.OfType<MethodDeclaration>().Single().Name);
        Assert.AreEqual("D", ((ClassDeclaration)unit.Members[1]).Name);
    }

    [TestMethod]
    public void Skipping_StopsAtTheNextStatementKeyword()
    {
        const string source = "class C\n{\n    void M()\n    {\n        a(\n        return b;\n    }\n}\n";
        var statements = MethodStatements(ClassMembers(Parse(source))[0]);

        Assert.HasCount(2, statements);
        Assert.AreEqual("a(", ((IncompleteStatement)statements[0]).Text);
        Assert.IsInstanceOfType<ReturnStatement>(statements[1]);
    }

    [TestMethod]
    public void InvalidUsingAndAttribute_AtTheTop_AreSkipped()
    {
        var unit = Parse("using System.;\n[assembly: A(]\nclass C { }\n");

        Assert.AreEqual("C", unit.Members.OfType<ClassDeclaration>().Single().Name);
        Assert.IsNotEmpty(unit.Errors);
    }

    [TestMethod]
    public void TopLevelStatements_Recover()
    {
        var unit = Parse("Console.WriteLine(1);\nfoo(;\nConsole.WriteLine(2);\n");

        Assert.HasCount(3, unit.Members);
        Assert.IsInstanceOfType<GlobalStatement>(unit.Members[0]);
        Assert.IsInstanceOfType<IncompleteMemberDeclaration>(unit.Members[1]);
        Assert.IsInstanceOfType<GlobalStatement>(unit.Members[2]);
    }

    [TestMethod]
    public void Writer_WritesSkippedSourceAsIs()
    {
        var unit = Parse("class C\n{\n    int x = ;\n}\n");
        var writer = new CSharpWriter();
        writer.WriteCompilationUnit(unit);

        StringAssert.Contains(writer.GetResult(), "int x = ;");
    }

    [TestMethod]
    public void ValidInput_HasNoErrors()
    {
        var unit = Parse("class C { void M() { a(); } }");
        Assert.IsNull(unit.Errors);
    }

    /// <summary>
    /// Every file of the built-in corpus (and of <c>CSHARP_CORPUS_DIR</c> when set) that parses without
    /// recovery must parse to the same tree with it: same written code, same node types and spans, no errors.
    /// </summary>
    [TestMethod]
    public void ValidCorpus_ParsesTheSameWithRecovery()
    {
        var root = FindRepositoryRoot();
        var files = CorpusFiles(Path.Combine(root, "src"));
        var external = Environment.GetEnvironmentVariable("CSHARP_CORPUS_DIR");
        if (!string.IsNullOrEmpty(external))
        {
            files = files.Concat(CorpusFiles(external));
        }

        var symbols = (Environment.GetEnvironmentVariable("CSHARP_CORPUS_SYMBOLS") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var strict = new CSharpParseOptions(preprocessorSymbols: symbols);
        var recovering = new CSharpParseOptions(preprocessorSymbols: symbols, errorRecovery: true);

        var compared = 0;
        var differences = new List<string>();
        foreach (var path in files)
        {
            var source = File.ReadAllText(path);
            if (!CSharpParser.TryParse(source, strict, out var expected, out _))
            {
                continue;
            }

            compared++;
            if (!CSharpParser.TryParse(source, recovering, out var actual, out var error))
            {
                differences.Add($"{path}: fails with recovery: {error?.Message}");
                continue;
            }

            if (actual.Errors != null)
            {
                differences.Add($"{path}: {actual.Errors[0].Message} at {actual.Errors[0].Span}");
                continue;
            }

            if (Shape(expected) != Shape(actual) || Write(expected) != Write(actual))
            {
                differences.Add($"{path}: the tree differs");
            }
        }

        TestContext.WriteLine($"{compared} files compared");
        Assert.IsGreaterThan(100, compared);
        Assert.IsEmpty(differences, string.Join("\n", differences.Take(20)));
    }

    /// <summary>
    /// Damaged copies of the corpus files (truncated, or with a few characters removed or inserted) always
    /// parse with recovery, and the errors lie inside the input.
    /// </summary>
    [TestMethod]
    public void DamagedCorpus_AlwaysParses()
    {
        var random = new Random(12345);
        var failures = new List<string>();
        var parsed = 0;
        foreach (var path in CorpusFiles(Path.Combine(FindRepositoryRoot(), "src")))
        {
            var source = File.ReadAllText(path);
            if (source.Length < 20)
            {
                continue;
            }

            for (var i = 0; i < 6; i++)
            {
                var at = random.Next(source.Length);
                var damaged = (i % 3) switch
                {
                    0 => source[..at],
                    1 => source.Remove(at, Math.Min(source.Length - at, 1 + random.Next(20))),
                    _ => source.Insert(at, Damage[random.Next(Damage.Length)].ToString()),
                };

                var parse = Task.Run(() => (CSharpParser.TryParse(damaged, Recovery, out var unit, out var error), unit, error));
                if (!parse.Wait(TimeSpan.FromSeconds(10)))
                {
                    failures.Add($"{path} damage {i} at {at}: timed out");
                    continue;
                }

                var (success, unit, error) = parse.Result;
                parsed++;
                var length = CSharpParser.GetUtf8Source(damaged).Length;
                if (!success)
                {
                    failures.Add($"{path} damage {i} at {at}: {error?.Message}");
                }
                else if (unit.Errors?.Any(e => e.Span.Start < 0 || e.Span.End > length) == true)
                {
                    failures.Add($"{path} damage {i} at {at}: error outside the input");
                }
            }
        }

        TestContext.WriteLine($"{parsed} damaged files parsed");
        Assert.IsEmpty(failures, string.Join("\n", failures.Take(20)));
    }

    private const string Damage = "{}()[]<>;,.=\"'$@#/";

    private static IEnumerable<string> CorpusFiles(string directory)
    {
        var separator = Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".xcs", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{separator}bin{separator}") && !path.Contains($"{separator}obj{separator}"))
            .Order(StringComparer.Ordinal);
    }

    private static string Write(CompilationUnit unit)
    {
        var writer = new CSharpWriter();
        writer.WriteCompilationUnit(unit);
        return writer.GetResult();
    }

    /// <summary>
    /// The node types and spans of the tree in preorder.
    /// </summary>
    private static string Shape(ICSharpNode node)
    {
        var builder = new StringBuilder();
        var stack = new Stack<ICSharpNode>();
        stack.Push(node);
        while (stack.Count != 0)
        {
            var current = stack.Pop();
            builder.Append(current.GetType().Name).Append(current.Span).Append(' ');
            foreach (var child in SpanChecker.Children(current).Reverse())
            {
                stack.Push(child);
            }
        }

        return builder.ToString();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PaspanParsers.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("PaspanParsers.slnx not found");
    }
}
