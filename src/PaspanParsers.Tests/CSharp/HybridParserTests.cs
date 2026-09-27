using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Paspan;
using Paspan.Fluent;
using PaspanParsers.CSharp;

namespace PaspanParsers.Tests.CSharp;

/// <summary>
/// The building blocks of the hybrid parser (<see cref="CSharpHybridParser"/>): token parsers, the choice by
/// the next token, the bridges between the combinator grammar and the hand-written parser, and the check that
/// the hybrid parser gives the same ASTs as the recursive descent parser on the whole built-in corpus.
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
    /// The hand-written parser runs the block parser of the context for the bodies of lambdas and
    /// anonymous methods, and the rules the block runs continue at the position after its '{'.
    /// </summary>
    [TestMethod]
    public void LambdaAndAnonymousMethodBodies_UseTheBlockParserOfTheContext()
    {
        const string source = "class C { void M() { F(() => { G(x => { return; }); }); H(delegate { }); } }";
        var counting = new CountingParser<BlockStatement>(CSharpHybridParser.Block);

        Assert.IsTrue(CSharpParser.TryParse(source, null, CSharpHybridParser.CompilationUnitParser, counting, out var unit, out _));

        Assert.AreEqual(3, counting.Successes);
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

    private sealed class CountingParser<T>(Parser<T> parser) : Parser<T>
    {
        public int Successes { get; private set; }

        public override bool Parse(ref SpanReader reader, ParseContext context, ref ParseResult<T> result)
        {
            if (!parser.Parse(ref reader, context, ref result))
            {
                return false;
            }

            Successes++;
            return true;
        }
    }
}
