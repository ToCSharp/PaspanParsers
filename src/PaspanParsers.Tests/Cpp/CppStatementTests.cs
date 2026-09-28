using PaspanParsers.Cpp;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// Statements: selection with init-statements and condition declarations, loops and range-based for,
/// jumps and labels, exceptions, structured bindings, attributes, and declarations or expressions ([stmt.ambig]).
/// </summary>
[TestClass]
public class CppStatementTests
{
    private static string Text(CppNode node, string source) => node.Span.GetText(CppTestHelper.InFunction(source));

    private static string Write(TranslationUnit unit)
    {
        var writer = new CppWriter();
        writer.WriteTranslationUnit(unit);
        return writer.GetResult();
    }

    /// <summary>
    /// "declaration" or "expression" for each statement.
    /// </summary>
    private static List<string> Kinds(IEnumerable<Statement> statements)
    {
        return statements.Select(s => s switch
        {
            DeclarationStatement => "declaration",
            ExpressionStatement => "expression",
            _ => s.GetType().Name,
        }).ToList();
    }

    private static void AssertLines(IEnumerable<string> expected, IEnumerable<string> actual)
    {
        Assert.AreEqual(string.Join("\n", expected), string.Join("\n", actual));
    }

    // ========================================
    // Selection statements
    // ========================================

    [TestMethod]
    public void If_TakesInitStatementsAndConditionDeclarations()
    {
        var source = """
            if (int m = n * 2; m > 10) n++;
            if (int k = next()) n += k;
            if (T k{ 1 }) {}
            if (n++; n) {} else if (n) {} else {}
            if (; n) {}
            if (auto f = [] { return 1; }; f()) {}
            """;
        var statements = CppTestHelper.Statements(source).Cast<IfStatement>().ToList();

        Assert.AreEqual("int m = n * 2;", Text(statements[0].InitStatement, source));
        Assert.IsInstanceOfType<BinaryExpression>(statements[0].Condition);

        var declaration = (ConditionDeclaration)statements[1].Condition;
        Assert.AreEqual("int k = next()", Text(declaration, source));
        Assert.IsInstanceOfType<EqualsInitializer>(declaration.Initializer);
        Assert.IsNull(statements[1].InitStatement);

        Assert.IsInstanceOfType<BracedInitializer>(((ConditionDeclaration)statements[2].Condition).Initializer);

        Assert.IsInstanceOfType<ExpressionStatement>(statements[3].InitStatement);
        Assert.IsInstanceOfType<IfStatement>(statements[3].Else);
        Assert.IsNotNull(((IfStatement)statements[3].Else).Else);

        Assert.IsNull(((ExpressionStatement)statements[4].InitStatement).Expression);

        // The ';' in the lambda does not end an init-statement
        Assert.AreEqual("auto f = [] { return 1; };", Text(statements[5].InitStatement, source));
        Assert.IsInstanceOfType<CallExpression>(statements[5].Condition);
    }

    [TestMethod]
    public void If_TakesConstexprAndConsteval()
    {
        var statements = CppTestHelper.Statements("""
            if constexpr (sizeof(int) == 4) {} else {}
            if consteval { } else { }
            if !consteval { }
            if not consteval { }
            """).Cast<IfStatement>().ToList();

        Assert.IsTrue(statements[0].IsConstexpr);
        Assert.IsFalse(statements[0].IsConsteval);

        Assert.IsTrue(statements[1].IsConsteval);
        Assert.IsFalse(statements[1].IsNegated);
        Assert.IsNull(statements[1].Condition);
        Assert.IsNotNull(statements[1].Else);

        Assert.IsTrue(statements[2].IsConsteval && statements[2].IsNegated);
        Assert.IsTrue(statements[3].IsConsteval && statements[3].IsNegated);
    }

    [TestMethod]
    public void Switch_HasCasesAndLabels()
    {
        var source = """
            switch (int v = next(); v)
            {
            case 1:
            case 2 ... 3:
                break;
            [[likely]] case 4:
                n++;
            default:
            }
            """;
        var @switch = (SwitchStatement)CppTestHelper.Statement(source);
        Assert.IsNotNull(@switch.InitStatement);
        Assert.IsInstanceOfType<NameExpression>(@switch.Condition);

        var body = ((CompoundStatement)@switch.Body).Statements;
        Assert.HasCount(3, body);

        var @case = (CaseStatement)body[0];
        Assert.AreEqual("1", Text(@case.Value, source));
        var range = (CaseStatement)@case.Statement;
        Assert.AreEqual("3", Text(range.RangeEnd, source));
        Assert.IsInstanceOfType<BreakStatement>(range.Statement);

        var attributed = (AttributedStatement)body[1];
        Assert.AreEqual("likely", attributed.Attributes[0].Attributes[0].Name);
        Assert.IsInstanceOfType<CaseStatement>(attributed.Statement);

        // A label at the end of a block (C++23)
        Assert.IsNull(((DefaultStatement)body[2]).Statement);
    }

    // ========================================
    // Iteration statements
    // ========================================

    [TestMethod]
    public void Loops_HaveTheirParts()
    {
        var source = """
            while (int k = next()) {}
            do n--; while (n > 0);
            for (int i = 0, j = n; i < j; ++i, --j) {}
            for (;;) {}
            for (n = 0; n < 3;) n++;
            for (; int k = next();) {}
            """;
        var statements = CppTestHelper.Statements(source);

        Assert.IsInstanceOfType<ConditionDeclaration>(((WhileStatement)statements[0]).Condition);

        var @do = (DoStatement)statements[1];
        Assert.AreEqual("n--;", Text(@do.Body, source));
        Assert.AreEqual("n > 0", Text(@do.Condition, source));

        var @for = (ForStatement)statements[2];
        Assert.AreEqual("int i = 0, j = n;", Text(@for.InitStatement, source));
        Assert.AreEqual("i < j", Text(@for.Condition, source));
        Assert.AreEqual("++i, --j", Text(@for.Increment, source));

        var forever = (ForStatement)statements[3];
        Assert.IsNull(forever.InitStatement);
        Assert.IsNull(forever.Condition);
        Assert.IsNull(forever.Increment);

        var withExpression = (ForStatement)statements[4];
        Assert.IsInstanceOfType<ExpressionStatement>(withExpression.InitStatement);
        Assert.IsNull(withExpression.Increment);

        Assert.IsInstanceOfType<ConditionDeclaration>(((ForStatement)statements[5]).Condition);
    }

    [TestMethod]
    public void RangeFor_TakesDeclarationsAndRanges()
    {
        var source = """
            for (int value : values) {}
            for (const auto &value : { 1, 2 }) {}
            for (auto [key, value] : map) {}
            for (int copy[2] = { 1, 2 }; auto value : copy) {}
            for (Unknown &item : items) {}
            for ([[maybe_unused]] X x : xs) {}
            """;
        var statements = CppTestHelper.Statements(source).Cast<RangeForStatement>().ToList();

        Assert.AreEqual("int value", Text(statements[0].Declaration, source));
        Assert.AreEqual("values", Text(statements[0].Range, source));
        Assert.IsNull(statements[0].InitStatement);

        Assert.IsInstanceOfType<ReferenceDeclarator>(statements[1].Declaration.Declarator);
        Assert.IsInstanceOfType<InitializerListExpression>(statements[1].Range);

        var binding = (StructuredBindingDeclarator)statements[2].Declaration.Declarator;
        AssertLines(new[] { "key", "value" }, binding.Names.Select(n => n.Identifier));

        Assert.AreEqual("int copy[2] = { 1, 2 };", Text(statements[3].InitStatement, source));
        Assert.AreEqual("Unknown &item", Text(statements[4].Declaration, source));
        Assert.HasCount(1, statements[5].Declaration.Attributes);
    }

    // ========================================
    // Jumps, labels, exceptions
    // ========================================

    [TestMethod]
    public void JumpsAndLabels_Parse()
    {
        var source = """
            goto end;
            again: n++;
            break;
            continue;
            return;
            return { 1, 2 };
            co_return;
            co_return n;
            end:
            """;
        var statements = CppTestHelper.Statements(source);
        AssertLines(
            new[]
            {
                "GotoStatement", "LabeledStatement", "BreakStatement", "ContinueStatement", "ReturnStatement", "ReturnStatement",
                "CoReturnStatement", "CoReturnStatement", "LabeledStatement",
            },
            Kinds(statements));

        Assert.AreEqual("end", ((GotoStatement)statements[0]).Label);
        var label = (LabeledStatement)statements[1];
        Assert.AreEqual("again", label.Label);
        Assert.AreEqual("again: n++;", Text(label, source));
        Assert.IsInstanceOfType<InitializerListExpression>(((ReturnStatement)statements[5]).Expression);
        Assert.IsNull(((CoReturnStatement)statements[6]).Expression);
        Assert.IsNull(((LabeledStatement)statements[8]).Statement);
    }

    [TestMethod]
    public void Try_HasHandlers()
    {
        var source = """
            try { f(); }
            catch (const char *message) {}
            catch (int) { throw; }
            catch (...) {}
            """;
        var @try = (TryStatement)CppTestHelper.Statement(source);
        Assert.AreEqual("{ f(); }", Text(@try.Block, source));
        Assert.HasCount(3, @try.Handlers);
        Assert.AreEqual("const char *message", Text(@try.Handlers[0].Declaration, source));
        Assert.IsNull(@try.Handlers[1].Declaration.Declarator);
        Assert.IsNull(@try.Handlers[2].Declaration);

        CppTestHelper.AssertParseFails(CppTestHelper.InFunction("try {}"));
        CppTestHelper.AssertParseFails(CppTestHelper.InFunction("try {} catch (int x = 1) {}"));
    }

    // ========================================
    // Declarations in blocks
    // ========================================

    [TestMethod]
    public void StructuredBindings_AreDeclarators()
    {
        var source = "auto [a, b] = pair;\nauto &[c] = array;\nconst auto &&[d, e] = f();";
        var declarators = CppTestHelper.Statements(source)
            .Select(s => ((SimpleDeclaration)((DeclarationStatement)s).Declaration).Declarators.Single())
            .ToList();

        Assert.AreEqual("[a, b]", Text(declarators[0].Declarator, source));
        Assert.IsInstanceOfType<StructuredBindingDeclarator>(((ReferenceDeclarator)declarators[1].Declarator).Inner);
        Assert.IsTrue(((ReferenceDeclarator)declarators[2].Declarator).IsRvalue);
    }

    [TestMethod]
    public void StaticAssert_IsADeclaration()
    {
        var unit = CppTestHelper.Parse("static_assert(sizeof(int) == 4, \"int\");\nvoid f() { static_assert(true); }");
        var declaration = (StaticAssertDeclaration)unit.Declarations[0];
        Assert.IsInstanceOfType<BinaryExpression>(declaration.Condition);
        Assert.IsInstanceOfType<LiteralExpression>(declaration.Message);

        var statement = (DeclarationStatement)((FunctionDefinition)unit.Declarations[1]).Body.Statements[0];
        Assert.IsNull(((StaticAssertDeclaration)statement.Declaration).Message);
    }

    [TestMethod]
    public void Attributes_BelongToDeclarationsOrStatements()
    {
        var source = """
            [[maybe_unused]] int a = 1;
            [[likely]] a++;
            [[fallthrough]];
            [[maybe_unused]] label: ;
            [[gnu::unused]] Unknown b;
            """;
        var statements = CppTestHelper.Statements(source);

        var declaration = (SimpleDeclaration)((DeclarationStatement)statements[0]).Declaration;
        Assert.AreEqual("[[maybe_unused]] int a = 1;", Text(declaration, source));
        Assert.HasCount(1, declaration.Attributes);

        Assert.IsInstanceOfType<ExpressionStatement>(((AttributedStatement)statements[1]).Statement);
        Assert.IsNull(((ExpressionStatement)((AttributedStatement)statements[2]).Statement).Expression);
        Assert.IsInstanceOfType<LabeledStatement>(((AttributedStatement)statements[3]).Statement);
        Assert.IsInstanceOfType<DeclarationStatement>(statements[4]);

        var unit = CppTestHelper.Parse("[[nodiscard]] int f() { return 1; }\n[[deprecated]] int v;");
        Assert.HasCount(1, ((FunctionDefinition)unit.Declarations[0]).Attributes);
        Assert.HasCount(1, ((SimpleDeclaration)unit.Declarations[1]).Attributes);
    }

    // ========================================
    // Declarations or expressions ([stmt.ambig])
    // ========================================

    [TestMethod]
    public void StatementsThatCanBeDeclarations_AreDeclarations()
    {
        // T is a type: what parses as a declaration is one; the others are functional casts
        var statements = CppTestHelper.Statements("""
            typedef int T;
            int a = 1;
            T(x);
            T(*p);
            T(c) = 7;
            T(e)[5];
            T(g)(int);
            T(f), h, i = 3;
            T(a) + 1;
            T(a)->m;
            T{a};
            T(a), 1;
            """);
        AssertLines(
            new[]
            {
                "declaration", "declaration", "declaration", "declaration", "declaration", "declaration", "declaration", "declaration",
                "expression", "expression", "expression", "expression",
            },
            Kinds(statements));
    }

    [TestMethod]
    public void UnknownNames_AreTypesOnlyBeforeWhatFollowsATypeInADeclaration()
    {
        var statements = CppTestHelper.Statements("""
            X const y = z;
            X volatile *v;
            X::Y w;
            X<int>::type *t = nullptr;
            V(y);
            V(y) + 1;
            x.y = 1;
            x ? y : z;
            """);
        AssertLines(
            new[] { "declaration", "declaration", "declaration", "declaration", "expression", "expression", "expression", "expression" },
            Kinds(statements));
    }

    [TestMethod]
    public void NamesDeclaredInStatements_AreScopedToThem()
    {
        // Inside the statements T is a variable: T * y multiplies; after them T is the type again
        var statements = CppTestHelper.Statements("""
            typedef int T;
            int y;
            if (int T = 1) T * y;
            for (int T = 0; T < 1;) { T * y; }
            for (int T : values) T * y;
            while (int T = 0) T * y;
            switch (int T = 0) { default: T * y; }
            try {} catch (int T) { T * y; }
            { auto [T, U] = pair; T * y; }
            T * z;
            """);

        Statement Inner(Statement statement) => statement switch
        {
            IfStatement @if => @if.Then,
            ForStatement @for => @for.Body,
            RangeForStatement rangeFor => rangeFor.Body,
            WhileStatement @while => @while.Body,
            SwitchStatement @switch => ((DefaultStatement)((CompoundStatement)@switch.Body).Statements[0]).Statement,
            TryStatement @try => @try.Handlers[0].Body,
            CompoundStatement compound => compound.Statements[^1],
            _ => statement,
        };

        var kinds = Kinds(statements.Skip(2).Select(s => Inner(s) is CompoundStatement { Statements: [.., var last] } ? last : Inner(s)));
        AssertLines(
            new[] { "expression", "expression", "expression", "expression", "expression", "expression", "expression", "declaration" },
            kinds);
    }

    [TestMethod]
    public void LabelsAndConditionalExpressions_AreTold()
    {
        var statements = CppTestHelper.Statements("a ? b : c;\nstd::x = 1;\nlabel: a;\ndefault_value: ;");
        AssertLines(new[] { "expression", "expression", "LabeledStatement", "LabeledStatement" }, Kinds(statements));
    }

    // ========================================
    // Writer and oracle
    // ========================================

    [TestMethod]
    public void WrittenStatements_ParseTheSame()
    {
        const string source = """
            [[nodiscard]] int f(int n)
            {
                if (int m = n; m > 0) n++; else if (n) n--; else {}
                if constexpr (true) {} if consteval {} else {} if !consteval {}
                switch (int v = n; v) { case 1: case 2 ... 3: break; [[likely]] default: n++; }
                while (int k = n) { break; } do n--; while (n);
                for (int i = 0; i < n; ++i) continue; for (;;) {} for (; n;) {} for (n = 0; int k = n;) {}
                for (int copy[1] = {}; [[maybe_unused]] auto &[a] : copy) {} for (auto x : { 1 }) {}
                try { throw 1; } catch (const int &e) {} catch (...) { throw; }
                [[maybe_unused]] auto [b, c] = pair; static_assert(sizeof(int) == 4, "int");
                goto end; end: ; again: [[fallthrough]]; return { 1 };
                last:
            }
            """;
        var written = Write(CppTestHelper.Parse(source));
        Assert.AreEqual(written, Write(CppTestHelper.Parse(written)), written);
    }

    [TestMethod]
    public void Oracle_Statements()
    {
        CppTestHelper.AssertOracle("""
            #include <initializer_list>
            typedef int T;
            int next();
            int f(int n)
            {
                T(x);
                T(*p) = &x;
                T(c) = 7, d[2];
                T(n) + 1;
                if (T k{ next() }; k > 0) x += k; else if (int j = k) x -= j;
                int pairs[2][1] = { { 1 }, { 2 } };
                for (int values[2] = { 1, 2 }; auto v : values) x += v;
                for (auto &[v] : pairs) x += v;
                for (auto v : { 1, 2 }) x += v;
                switch (n) { case 0: [[fallthrough]]; case 1 ... 2: break; default: }
                try { throw n; } catch (T (&array)[2]) { x = array[0]; } catch (T) {} catch (...) { throw; }
                [[maybe_unused]] ready: while (x > 0) x--;
                do { if (x) goto ready; } while (false);
                (void)p; (void)c; (void)d;
                return x;
            }
            """);
    }

    [TestMethod]
    [DataRow("void f(int n) { for (;;) { if (n) break; else continue; } for (; n;) {} for (; int k = n;) { n = k - 1; } }")]
    [DataRow("void f(int n) { if consteval { n++; } if !consteval { n--; } else { n++; } if constexpr (sizeof(n) > 1) {} }")]
    [DataRow("static_assert(sizeof(int) == 4);\n[[nodiscard]] int f() { static_assert(true, \"x\"); return 1; }\n[[deprecated]] int v = 1, w;")]
    [DataRow("int a[2];\nvoid f() { auto [x, y] = a; auto &[r, s] = a; const auto &&[t, u] = static_cast<int (&&)[2]>(a); (void)(x + y + r + s + t + u); }")]
    [DataRow("void f(int n) { switch (n) { case 1: { int local = n; (void)local; } [[likely]] case 2: n++; } lab: }")]
    [DataRow("void f(int n) { [[unlikely]] if (n) {} [[likely]] { n++; } [[unknown::attribute]] n++; }")]
    public void Oracle_Snippets(string source)
    {
        CppTestHelper.AssertOracle(source);
    }
}
