using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Paspan;
using Paspan.Fluent;
using PaspanParsers.CSharp;

namespace PaspanParsers.Tests.CSharp;

/// <summary>
/// The building blocks of the hybrid parser (<see cref="CSharpHybridParser"/>): token parsers, the choice by
/// the next token, the bridges between the combinator grammar and the hand-written parser, declarations, and the
/// check that the hybrid parser gives the same ASTs as the recursive descent parser on the whole built-in corpus.
/// </summary>
[TestClass]
public class HybridParserTests
{
    public TestContext TestContext { get; set; }

    // ========================================
    // Token parsers
    // ========================================

    [TestMethod]
    public void TokenParser_SkipsTriviaBeforeItsToken()
    {
        var (success, token, start, end) = Run(TokenParsers.Punctuator("{"), "  /* comment */\n { x");

        Assert.IsTrue(success);
        Assert.AreEqual("{", token.Text);
        Assert.AreEqual(17, start);
        Assert.AreEqual(18, end);
    }

    [TestMethod]
    public void TokenParser_FailsWithoutMoving()
    {
        var (success, _, _, end) = Run(TokenParsers.Punctuator("{"), "  ( x");

        Assert.IsFalse(success);
        Assert.AreEqual(0, end);
    }

    [TestMethod]
    [DataRow("if", true)]
    [DataRow("@if", false)]
    [DataRow("iff", false)]
    public void Keyword_MatchesReservedKeywordsOnly(string input, bool matches)
    {
        Assert.AreEqual(matches, Run(TokenParsers.Keyword("if"), input).Success);
    }

    [TestMethod]
    [DataRow("where", true, true)]
    [DataRow("@where", false, true)]
    [DataRow("wh\\u0065re", false, true)]
    [DataRow("class", false, false)]
    public void Contextual_IsAnIdentifierWithoutEscapes(string input, bool contextual, bool identifier)
    {
        Assert.AreEqual(contextual, Run(TokenParsers.Contextual("where"), input).Success);
        Assert.AreEqual(identifier, Run(TokenParsers.Identifier, input).Success);
    }

    [TestMethod]
    [DataRow(">>", ">>", true)]
    [DataRow("> >", ">>", false)]
    [DataRow(">>=", ">>=", true)]
    [DataRow(">>>", ">>>", true)]
    [DataRow(">>>=", ">>>=", true)]
    [DataRow(">>>", ">>", true)]
    [DataRow(">", ">>", false)]
    [DataRow(">=", ">>=", false)]
    public void ComposedPunctuators_AreAdjacentTokens(string input, string punctuator, bool matches)
    {
        var (success, token, start, end) = Run(TokenParsers.Punctuator(punctuator), input);

        Assert.AreEqual(matches, success);
        if (matches)
        {
            Assert.AreEqual(punctuator, token.Text);
            Assert.AreEqual(0, start);
            Assert.AreEqual(punctuator.Length, end);
        }
    }

    [TestMethod]
    public void TokenParser_CarriesNullableDirectives()
    {
        var (success, token, _, _) = Run(TokenParsers.Punctuator("{"), "#nullable enable\n{");

        Assert.IsTrue(success);
        Assert.HasCount(1, token.NullableDirectives);
    }

    // ========================================
    // Choice by the next token
    // ========================================

    [TestMethod]
    [DataRow("if x", "keyword")]
    [DataRow("where x", "contextual")]
    [DataRow("@where x", "identifier")]
    [DataRow("other x", "identifier")]
    [DataRow("{ x", "otherwise")]
    [DataRow("while x", "fell through")]
    public void TokenSwitch_ChoosesByTheNextToken(string input, string expected)
    {
        var parser = new TokenSwitch<string>()
            .OnKeyword("if", TokenParsers.Keyword("if").Then(_ => "keyword"))
            .OnContextual("where", TokenParsers.Contextual("where").Then(_ => "contextual"))
            .OnKind(TokenKind.Identifier, TokenParsers.Identifier.Then(_ => "identifier"))
            // Chosen for 'while', fails there, and the next alternatives are tried
            .OnKeyword("while", TokenParsers.Keyword("while").And(TokenParsers.Punctuator("(")).Then(_ => "while"))
            .Otherwise(TokenParsers.Punctuator("{").Then(_ => "otherwise"))
            .Otherwise(TokenParsers.Keyword("while").Then(_ => "fell through"));

        var (success, value, _, _) = Run(parser, input);

        Assert.IsTrue(success);
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    public void TokenSwitch_FailsWhenNoAlternativeMatches()
    {
        var parser = new TokenSwitch<string>().OnKeyword("if", TokenParsers.Keyword("if").Then(_ => "if"));

        Assert.IsFalse(Run(parser, "while").Success);
    }

    // ========================================
    // Bridges
    // ========================================

    /// <summary>
    /// The hand-written parser runs the combinator grammar for every block it meets: lambdas and anonymous
    /// methods. Member bodies and top-level statements are parts of the grammar, which uses its own rules
    /// for blocks and statements.
    /// </summary>
    [TestMethod]
    public void BlocksAndStatements_AreParsedByTheGrammar()
    {
        const string source = "F(); class C { void M() { F(() => { G(x => { return; }); }); H(delegate { }); } int P { get { return 0; } } }";
        var blocks = 0;
        var statements = 0;
        var grammar = HybridGrammar.Instance.WithEntryPoints(
            block => new CountingParser<BlockStatement>(block, () => blocks++),
            statement => new CountingParser<Statement>(statement, () => statements++));

        Assert.IsTrue(CSharpParser.TryParse(source, null, CSharpHybridParser.CompilationUnitParser, grammar, out var unit, out _));

        // The two lambdas and the anonymous method
        Assert.AreEqual(3, blocks);
        Assert.AreEqual(0, statements);
        Assert.IsNull(ParserVariants.Compare(CSharpParser.Parse(source), unit));
    }

    /// <summary>
    /// The state of the hand-written parser crosses the combinator block: omitted type arguments are
    /// allowed inside nameof(...), a block resets the query keywords and the lambda arrow of switch arms.
    /// Both parsers must agree on each input, valid or not.
    /// </summary>
    [TestMethod]
    [DataRow("var n = nameof(() => { List<> x; });")]
    [DataRow("var q = from x in xs select (Func<int>)(() => { var from = 1; return from; });")]
    [DataRow("var q = from x in xs select (Func<bool>)(() => { F(out var select); return o is int where; });")]
    [DataRow("var q = from x in xs where F(delegate { return o is string orderby && G(out int group); }) select x;")]
    [DataRow("var q = from x in xs select (Func<int>)(() => { var y = from z in zs select z; return 0; });")]
    [DataRow("var s = o switch { int i when F(() => { var f = x => x; return true; }) => 1, _ => 0 };")]
    [DataRow("var s = o switch { int i when F(delegate { return x => x; }) => 1, _ => 0 };")]
    [DataRow("F(async () => { await Task.Yield(); }, delegate (int a) { return a; });")]
    [DataRow("F(() => { #nullable enable\n });")]
    [DataRow("F(() => { F(() => { F(() => { }); }); });")]
    [DataRow("F(() => { if (x) { } else { } }")]
    public void BlocksInExpressions_ParseTheSameWithBothParsers(string statements)
    {
        ParserVariants.Parse(SyntaxTestHelper.InMethod(statements));
    }

    // ========================================
    // Declarations
    // ========================================

    /// <summary>
    /// Declarations and the compilation unit are parsed by the grammar. Both parsers must agree on each input,
    /// valid or not: the grammar commits to a declaration where the hand-written parser does.
    /// </summary>
    [TestMethod]
    [DataRow("partial class C { partial void M(); async Task F() { } required int P { get; init; } file class D { } }")]
    [DataRow("class C { async x; partial p; required r; }")]
    [DataRow("class C { partial C(int x); public partial C(int x) { } }")]
    [DataRow("record R(int X) : B(X), I; record struct S; record class K { } class C { record(int x) { } }")]
    [DataRow("static class E { extension(string s) { void F() { } } extension<T>(List<T>) where T : class { } }")]
    [DataRow("class C { public static C operator >>>(C a, int b) => a; public static C operator >>=(C a, int b) => a; public static C operator >>>=(C a, int b) => a; }")]
    [DataRow("class C { public static C operator checked +(C a, C b) => a; public static explicit operator checked int(C c) => 0; bool I.operator ==(C a, C b) => true; }")]
    [DataRow("class C { public static bool operator true(C c) => true; public void operator ++() { } public void operator +=(C c) { } }")]
    [DataRow("class C : I { event EventHandler I.E { add { } remove => F(); } event EventHandler A, B = null; }")]
    [DataRow("class C : I { event EventHandler I.E; }")]
    [DataRow("class C { event EventHandler E { add; } }")]
    [DataRow("global using static System.Math; using unsafe P = int*; using A = (int, int); extern alias X; using N = A::B.C<int>;")]
    [DataRow("using static; using X = ; global using; using List<int>;")]
    [DataRow("namespace A.B { namespace C { } }; namespace D { extern alias E; using F; }")]
    [DataRow("namespace A; class C { }")]
    [DataRow("namespace A; namespace B;")]
    [DataRow("namespace A { namespace B; }")]
    [DataRow("[assembly: X] [module: Y(1, Name = 2), Z] class C { }")]
    [DataRow("[assembly: ] class C { }")]
    [DataRow("[assembly: X(] class C { }")]
    [DataRow("[type: A, B,] [return: C] [foo: D] class C { }")]
    [DataRow("[A::B.C<int>(x: 1, Y = 2)] class C { }")]
    [DataRow("class C<[A] in T, out U> where T : class?, new() where U : unmanaged, notnull, allows ref struct, I<T> { }")]
    [DataRow("class C<T> where T : { }")]
    [DataRow("class C<T> where T : unmanaged.X, notnull<int>, allows { }")]
    [DataRow("class C { int this[int i, params int[] a] { get => 0; set { } } int this[] => 0; unsafe fixed int b[4], c[2]; }")]
    [DataRow("enum E : byte { A = 1, [X] B, C, } enum F { } enum G { A };")]
    [DataRow("enum E { , }")]
    [DataRow("enum E { A,, }")]
    [DataRow("delegate ref readonly int D<T>(scoped ref T x, this int y = 1, __arglist) where T : struct;")]
    [DataRow("class C { ~C() { } C() : base(1) { } C(int x) : this() => F(); C(string s) : { } }")]
    [DataRow("#nullable enable\nclass C {\n#nullable disable\n int x;\n#nullable restore\n}\n#nullable enable\n")]
    [DataRow("class C\n#nullable enable\n{\n int P\n#nullable disable\n { get; }\n}")]
    [DataRow("int x = 1; static void F() { } Console.WriteLine(x); class C { }")]
    [DataRow("delegate { }; delegate*<int, void> p; public int x;")]
    [DataRow("delegate (int a, int b) D(); namespace N { delegate (int a, int b) D(); }")]
    [DataRow("class C { public int P { get; } = 1; public int Q => 1; int I.R { get; } int I.S; }")]
    [DataRow("class C { void M<T>(T x) where T : new() { } int M2() => 0 }")]
    [DataRow("class C { int; }")]
    [DataRow("class C { void M() }")]
    [DataRow("class C { public }")]
    [DataRow("class { }")]
    [DataRow("interface I(int x) { }")]
    [DataRow("public namespace N { }")]
    [DataRow("class C { namespace N { } }")]
    [DataRow("class C { ref int F() => ref x; ref struct S { } readonly ref partial struct T { } }")]
    [DataRow("class C { void F(scoped ref int x, scoped Span<int> y, scoped) { } }")]
    public void Declarations_ParseTheSameWithBothParsers(string source)
    {
        ParserVariants.Parse(source);
    }

    // ========================================
    // The hybrid parser on the built-in corpus
    // ========================================

    /// <summary>
    /// Every file of the built-in corpus and every statement of its method bodies on its own give
    /// the same AST, spans included, with both parsers (or both fail).
    /// </summary>
    [TestMethod]
    public void BuiltInCorpus_SameAstAsRecursiveDescent()
    {
        var options = new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(LanguageVersion.CSharp14);
        var differences = new List<string>();
        var files = 0;
        var statements = 0;

        foreach (var (name, path) in CSharpCorpusTests.BuiltInCorpus())
        {
            var source = File.ReadAllText(path);
            files++;
            Compare(name, source, differences);

            var bodies = CSharpSyntaxTree.ParseText(source, options).GetRoot().DescendantNodes().OfType<BlockSyntax>()
                .Where(block => block.Parent is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax);
            foreach (var statement in bodies.SelectMany(body => body.Statements))
            {
                statements++;
                var line = statement.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                Compare($"{name}:{line}", SyntaxTestHelper.InMethod(statement.ToString()), differences);
            }
        }

        TestContext.WriteLine($"Compared {files} files and {statements} statements");
        Assert.IsEmpty(differences, "The hybrid parser differs from the recursive descent parser:\n" + string.Join("\n", differences));
    }

    /// <summary>
    /// Invalid code too: every statement of the built-in corpus with one token removed (the first, one in the
    /// middle, the last) is rejected by both parsers, or parsed by both to the same AST. This checks that the
    /// grammar commits to a statement where the hand-written parser does.
    /// </summary>
    [TestMethod]
    public void BuiltInCorpus_BrokenStatements_SameResult()
    {
        var options = new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(LanguageVersion.CSharp14);
        var differences = new List<string>();
        var variants = 0;

        foreach (var (name, path) in CSharpCorpusTests.BuiltInCorpus())
        {
            var bodies = CSharpSyntaxTree.ParseText(File.ReadAllText(path), options).GetRoot().DescendantNodes().OfType<BlockSyntax>()
                .Where(block => block.Parent is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax);
            foreach (var statement in bodies.SelectMany(body => body.Statements))
            {
                var text = statement.ToString();
                var tokens = statement.DescendantTokens().Where(t => t.Span.Length != 0).ToList();
                foreach (var index in new[] { 0, tokens.Count / 2, tokens.Count - 1 }.Distinct())
                {
                    var removed = tokens[index].Span.Start - statement.SpanStart;
                    var broken = text.Remove(removed, tokens[index].Span.Length);
                    variants++;
                    var line = statement.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    Compare($"{name}:{line} without '{tokens[index].Text}'", SyntaxTestHelper.InMethod(broken), differences);
                }
            }
        }

        TestContext.WriteLine($"Compared {variants} broken statements");
        Assert.IsEmpty(differences, "The hybrid parser differs from the recursive descent parser:\n" + string.Join("\n", differences.Take(50)));
    }

    /// <summary>
    /// Like <see cref="BuiltInCorpus_BrokenStatements_SameResult"/> for declarations: every type and member
    /// declaration of the built-in corpus with one token removed (the first, the last, and three in between)
    /// is rejected by both parsers, or parsed by both to the same AST. Members of types are parsed in a class.
    /// </summary>
    [TestMethod]
    public void BuiltInCorpus_BrokenDeclarations_SameResult()
    {
        var options = new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(LanguageVersion.CSharp14);
        var differences = new List<string>();
        var variants = 0;

        foreach (var (name, path) in CSharpCorpusTests.BuiltInCorpus())
        {
            var declarations = CSharpSyntaxTree.ParseText(File.ReadAllText(path), options).GetRoot().DescendantNodes()
                .OfType<MemberDeclarationSyntax>()
                .Where(member => member is not GlobalStatementSyntax);

            foreach (var declaration in declarations)
            {
                var text = declaration.ToString();
                var tokens = declaration.DescendantTokens().Where(t => t.Span.Length != 0).ToList();
                var inType = declaration.Parent is TypeDeclarationSyntax;
                var line = declaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                foreach (var index in new[] { 0, tokens.Count / 4, tokens.Count / 2, tokens.Count * 3 / 4, tokens.Count - 1 }.Distinct())
                {
                    var removed = tokens[index].Span.Start - declaration.SpanStart;
                    var broken = text.Remove(removed, tokens[index].Span.Length);
                    variants++;
                    Compare($"{name}:{line} without '{tokens[index].Text}'", inType ? $"class C\n{{\n{broken}\n}}\n" : broken, differences);
                }
            }
        }

        TestContext.WriteLine($"Compared {variants} broken declarations");
        Assert.IsEmpty(differences, "The hybrid parser differs from the recursive descent parser:\n" + string.Join("\n", differences.Take(50)));
    }

    private static void Compare(string name, string source, List<string> differences)
    {
        CSharpParser.TryParse(source, out var expected, out _);
        CSharpHybridParser.TryParse(source, out var actual, out _);
        var difference = ParserVariants.Compare(expected, actual);
        if (difference != null)
        {
            differences.Add($"{name}: {difference}");
        }
    }

    // ========================================
    // Helpers
    // ========================================

    private static (bool Success, T Value, int Start, int End) Run<T>(Parser<T> parser, string input)
    {
        var reader = new SpanReader(input);
        var context = new CSharpParseContext(null);
        var result = new ParseResult<T>();
        var success = parser.Parse(ref reader, context, ref result);
        return (success, result.Value, result.Start, reader.GetCurrentPosition());
    }

    private sealed class CountingParser<T>(Parser<T> parser, Action success) : Parser<T>
    {
        public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
        {
            if (!parser.Parse(ref reader, context, ref result))
            {
                return false;
            }

            success();
            return true;
        }
    }
}
