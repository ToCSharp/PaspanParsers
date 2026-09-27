using PaspanParsers.CSharp;

namespace PaspanParsers.Tests.CSharp;

/// <summary>
/// Preprocessor directives (stage 7): conditional compilation, #define/#undef and the directives
/// skipped as trivia; #nullable directives kept in the AST.
/// </summary>
[TestClass]
public class PreprocessorTests
{
    private static CompilationUnit Unit(string code, params string[] symbols)
    {
        var unit = CSharpParser.Parse(code, new CSharpParseOptions(preprocessorSymbols: symbols));
        Assert.IsNotNull(unit, "failed to parse: " + code);
        return unit;
    }

    private static string[] MemberNames(string code, params string[] symbols)
    {
        var type = (ClassDeclaration)Unit(code, symbols).Members[0];
        return type.Members?.Cast<FieldDeclaration>().Select(f => f.Variables[0].Name).ToArray() ?? [];
    }

    private static void AssertOracle(string code, params string[] symbols)
    {
        var result = RoslynOracle.Check(code, symbols);
        Assert.AreEqual(OracleStatus.Passed, result.Status, $"{code}\n{result.Detail}");
    }

    private const string Conditional = """
        class C
        {
        #if A
            int a;
        #elif B
            int b;
        #else
            int c;
        #endif
            int d;
        }
        """;

    [TestMethod]
    [DataRow(new string[0], new[] { "c", "d" })]
    [DataRow(new[] { "A" }, new[] { "a", "d" })]
    [DataRow(new[] { "B" }, new[] { "b", "d" })]
    [DataRow(new[] { "A", "B" }, new[] { "a", "d" })]
    public void If_ChoosesBranchBySymbols(string[] symbols, string[] members)
    {
        CollectionAssert.AreEqual(members, MemberNames(Conditional, symbols));
        AssertOracle(Conditional, symbols);
    }

    [TestMethod]
    [DataRow("A && !B", new[] { "A" }, true)]
    [DataRow("A && !B", new[] { "A", "B" }, false)]
    [DataRow("(A || B) && (true == true)", new[] { "B" }, true)]
    [DataRow("A == B", new string[0], true)]
    [DataRow("A != B", new[] { "A" }, true)]
    [DataRow("!(A || false) // comment", new string[0], true)]
    [DataRow("false", new string[0], false)]
    public void If_EvaluatesExpressions(string condition, string[] symbols, bool expected)
    {
        var code = $"class C\n{{\n#if {condition}\n    int x;\n#endif\n}}";

        Assert.AreEqual(expected ? 1 : 0, MemberNames(code, symbols).Length, condition);
    }

    [TestMethod]
    public void DefineAndUndef_ApplyToTheRestOfTheFile()
    {
        const string code = """
            #define A
            #undef B
            #if !C
            #define D
            #endif
            class C
            {
            #if A && !B && D
                int x;
            #endif
            }
            """;

        CollectionAssert.AreEqual(new[] { "x" }, MemberNames(code, "B"));
        AssertOracle(code, "B");
    }

    [TestMethod]
    public void DisabledText_IsNotParsed()
    {
        const string code = """
            class C
            {
            #if false
                garbage that is never parsed {
            #if NESTED
                more garbage
            #else
                "unterminated
            #endif
            #elif true
                int x;
            #else
                int y;
            #endif
            }
            """;

        CollectionAssert.AreEqual(new[] { "x" }, MemberNames(code));
        AssertOracle(code);
    }

    [TestMethod]
    public void Directives_InsideStatementsAndExpressions()
    {
        const string code = """
            class C
            {
                int M()
                {
                    var x =
            #if DEBUG
                        1
            #else
                        2
            #endif
                        ;
            #region locals
                    int y = x;
            #endregion
            #pragma warning disable CS0168
                    return y;
                }
            }
            """;

        AssertOracle(code);
        AssertOracle(code, "DEBUG");
    }

    [TestMethod]
    public void HashInStringsAndComments_IsNotADirective()
    {
        const string code = """"
            class C
            {
                string s = @"
            #if A
            ";
                string raw = """
            #endif
            """;
                /* #if B */
                int x; // #endif
            }
            """";

        AssertOracle(code);
    }

    [TestMethod]
    public void OtherDirectives_AreSkipped()
    {
        const string code = """
            #!/usr/bin/env dotnet
            #nullable enable
            #pragma warning disable CS0168 // comment
            #line 200 "generated.cs"
            #line default
            #line hidden
            #region Types
            class C { }
            #endregion
            #warning careful
            """;

        Assert.HasCount(1, Unit(code).Members);
    }

    [TestMethod]
    public void Nullable_KeptInTheAst()
    {
        const string code = """
            #nullable enable
            using System;
            class C
            #nullable disable
            {
            #nullable restore warnings
                string M()
                {
            #nullable enable annotations
                    return "";
            #nullable disable
                }
            }
            #nullable restore
            """;

        var unit = Unit(code);
        Assert.AreEqual(NullableSetting.Enable, unit.Usings[0].NullableDirectives[0].Setting);
        Assert.IsNull(unit.Usings[0].NullableDirectives[0].Target);

        var type = (ClassDeclaration)unit.Members[0];
        Assert.AreEqual(NullableSetting.Disable, type.OpenBraceNullableDirectives[0].Setting);

        var method = (MethodDeclaration)type.Members[0];
        Assert.AreEqual(NullableTarget.Warnings, method.NullableDirectives[0].Target);

        var block = ((BlockMethodBody)method.Body).Block;
        Assert.AreEqual(NullableTarget.Annotations, block.Statements[0].NullableDirectives[0].Target);
        Assert.AreEqual(NullableSetting.Disable, block.CloseBraceNullableDirectives[0].Setting);
        Assert.AreEqual(NullableSetting.Restore, unit.EndNullableDirectives[0].Setting);

        AssertOracle(code);
    }

    [TestMethod]
    public void Nullable_AroundParametersConstraintsAndLabels()
    {
        const string code = """
            class C
            {
                void M<T>(
            #nullable disable
                    T value,
            #nullable enable
                    int other)
            #nullable disable
                    where T : class
            #nullable restore
                {
                    switch (other)
                    {
            #nullable disable
                        case 1:
                            break;
                    }
                }

                object Current
            #nullable restore
                {
            #nullable disable
                    get;
                }
            }
            """;

        AssertOracle(code);
    }

    [TestMethod]
    public void ParseContext_DefinedSymbols_ReflectDefines()
    {
        var context = new CSharpParseContext(new CSharpParseOptions(preprocessorSymbols: ["A"]));
        var reader = new Paspan.SpanReader("#define B\n#undef A\nclass C { }");

        Assert.IsTrue(CSharpParser.CompilationUnitParser.TryParse(ref reader, context, out _, out _));
        CollectionAssert.AreEquivalent(new[] { "B" }, context.DefinedSymbols.ToArray());
    }
}
