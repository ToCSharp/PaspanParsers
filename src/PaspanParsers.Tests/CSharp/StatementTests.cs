using PaspanParsers.CSharp;
using static PaspanParsers.Tests.CSharp.SyntaxTestHelper;

namespace PaspanParsers.Tests.CSharp;

/// <summary>
/// Statements (stage 5): declarations or expressions, local functions and every statement form.
/// </summary>
[TestClass]
public class StatementTests
{
    [TestMethod]
    public void Declaration_MultipleDeclaratorsAndArrayInitializer()
    {
        var declaration = (LocalDeclarationStatement)Statement("int[] a = { 1, 2 }, b;");

        Assert.HasCount(2, declaration.Variables);
        Assert.AreEqual(InitializerKind.Array, ((InitializerExpression)declaration.Variables[0].Initializer).Kind);
        Assert.IsNull(declaration.Variables[1].Initializer);
    }

    [TestMethod]
    public void Declaration_ConstUsingAndAwaitUsing()
    {
        Assert.IsTrue(((LocalDeclarationStatement)Statement("const int limit = 10;")).IsConst);

        var awaitUsing = (LocalDeclarationStatement)Statement("await using var s = Open();");
        Assert.IsTrue(awaitUsing.IsUsing);
        Assert.IsTrue(awaitUsing.IsAwait);
    }

    [TestMethod]
    public void Declaration_OrExpression()
    {
        Assert.IsInstanceOfType<LocalDeclarationStatement>(Statement("A<B> c = d;"));
        Assert.IsInstanceOfType<ExpressionStatement>(Statement("F(a < b);"));
        Assert.IsInstanceOfType<LocalDeclarationStatement>(Statement("T? x = null;"));
        Assert.IsInstanceOfType<ExpressionStatement>(Statement("x = a ? b : c;"));
        Assert.IsInstanceOfType<ExpressionStatement>(Statement("await t;"));
        Assert.IsInstanceOfType<LocalDeclarationStatement>(Statement("a * b;"));
    }

    [TestMethod]
    public void LocalFunction_ModifiersInSourceOrder()
    {
        var function = (LocalFunctionStatement)Statement("static async Task<T> F<T>(T value) where T : struct { await Task.Yield(); return value; }");

        CollectionAssert.AreEqual(new[] { Modifiers.Static, Modifiers.Async }, function.Modifiers.ToArray());
        Assert.AreEqual("F", function.Name);
        Assert.HasCount(1, function.TypeParameters);
        Assert.IsInstanceOfType<StructConstraint>(function.Constraints[0].Constraints[0]);
        Assert.IsNotNull(function.Body);
    }

    [TestMethod]
    public void LocalFunction_ExpressionBodyAndAttributes()
    {
        var function = (LocalFunctionStatement)Statement("[Pure] int Twice(int x) => x * 2;");

        Assert.HasCount(1, function.Attributes);
        Assert.IsInstanceOfType<BinaryExpression>(function.ExpressionBody);
    }

    [TestMethod]
    public void Labeled_AndGoto()
    {
        var statements = Statements("label: a++; goto label; goto case 1; goto default;");

        Assert.AreEqual("label", ((LabeledStatement)statements[0]).Label);
        Assert.AreEqual(GotoKind.Label, ((GotoStatement)statements[1]).Kind);
        Assert.AreEqual(GotoKind.Case, ((GotoStatement)statements[2]).Kind);
        Assert.IsNotNull(((GotoStatement)statements[2]).CaseExpression);
        Assert.AreEqual(GotoKind.Default, ((GotoStatement)statements[3]).Kind);
    }

    [TestMethod]
    public void ForEach_TypedAndDeconstruction()
    {
        var typed = (ForEachStatement)Statement("foreach (var item in items) { }");
        Assert.AreEqual("item", typed.Identifier);

        var deconstruction = (ForEachStatement)Statement("await foreach (var (k, v) in pairs) { }");
        Assert.IsTrue(deconstruction.IsAwait);
        Assert.IsNull(deconstruction.Type);
        Assert.IsInstanceOfType<DeclarationExpression>(deconstruction.Variable);
    }

    [TestMethod]
    public void For_DeclarationOrExpressionInitializers()
    {
        var declaration = (ForStatement)Statement("for (int i = 0, j = 10; i < j; i++, j--) { }");
        Assert.HasCount(1, declaration.Initializers);
        Assert.HasCount(2, ((LocalDeclarationStatement)declaration.Initializers[0]).Variables);
        Assert.HasCount(2, declaration.Iterators);

        var expressions = (ForStatement)Statement("for (i = 0, j = 1; ; ) { }");
        Assert.HasCount(2, expressions.Initializers);
        Assert.IsNull(expressions.Condition);
    }

    [TestMethod]
    public void Switch_SectionsAndTupleGoverningExpression()
    {
        var statement = (SwitchStatement)Statement("switch (a, b) { case (0, 0): case (1, 1) when c: x(); break; default: break; }");

        Assert.IsInstanceOfType<TupleExpression>(statement.Expression);
        Assert.HasCount(2, statement.Sections);
        Assert.HasCount(2, statement.Sections[0].Labels);
        Assert.IsNotNull(((CaseSwitchLabel)statement.Sections[0].Labels[1]).Guard);
        Assert.HasCount(2, statement.Sections[0].Statements);
        Assert.IsInstanceOfType<DefaultSwitchLabel>(statement.Sections[1].Labels[0]);
    }

    [TestMethod]
    public void Try_CatchClausesAndFinally()
    {
        var statement = (TryStatement)Statement("try { } catch (E e) when (e.X) { } catch (Exception) { throw; } catch { } finally { }");

        Assert.HasCount(3, statement.CatchClauses);
        Assert.AreEqual("e", statement.CatchClauses[0].Identifier);
        Assert.IsNotNull(statement.CatchClauses[0].Filter);
        Assert.IsNull(statement.CatchClauses[1].Identifier);
        Assert.IsNull(statement.CatchClauses[2].ExceptionType);
        Assert.IsNotNull(statement.FinallyBlock);
    }

    [TestMethod]
    public void Using_DeclarationOrExpression()
    {
        var declaration = (UsingStatement)Statement("using (var s = Open()) { }");
        Assert.IsInstanceOfType<LocalDeclarationStatement>(declaration.ResourceAcquisition);

        var expression = (UsingStatement)Statement("await using (Open()) { }");
        Assert.IsTrue(expression.IsAwait);
        Assert.IsInstanceOfType<ExpressionStatement>(expression.ResourceAcquisition);
    }

    [TestMethod]
    public void CheckedUnsafeFixedLockAndYield()
    {
        var statements = Statements("checked { } unchecked { } unsafe { } fixed (int* p = a, q = b) { } lock (gate) { } yield return 1; yield break;");

        Assert.IsTrue(((CheckedStatement)statements[0]).IsChecked);
        Assert.IsFalse(((CheckedStatement)statements[1]).IsChecked);
        Assert.IsInstanceOfType<UnsafeStatement>(statements[2]);
        Assert.HasCount(2, ((FixedStatement)statements[3]).Variables);
        Assert.IsInstanceOfType<LockStatement>(statements[4]);
        Assert.IsInstanceOfType<YieldReturnStatement>(statements[5]);
        Assert.IsInstanceOfType<YieldBreakStatement>(statements[6]);
    }

    [TestMethod]
    public void Empty_AndDanglingElse()
    {
        Assert.IsInstanceOfType<EmptyStatement>(Statement(";"));

        var outer = (IfStatement)Statement("if (a) if (b) x(); else y();");
        Assert.IsNull(outer.ElseStatement);
        Assert.IsNotNull(((IfStatement)outer.ThenStatement).ElseStatement);
    }

    [TestMethod]
    [DataRow("; int a = 0, b = 1; const int limit = 10; var list = new List<int>(); ref int first = ref items[0]; ref readonly int second = ref items[1];")]
    [DataRow("(int x, int y) = (1, 2); var (p, q) = (3, 4); (a, b) = (b, a);")]
    [DataRow("if (a > b) a++; else if (a < b) b--; else { } if (a) if (b) x(); else y();")]
    [DataRow("while (a < limit) { a++; if (a == 5) continue; if (a == 8) break; } do { b++; } while (b < limit); do b++; while (b < limit);")]
    [DataRow("for (int i = 0, j = 10; i < j; i++, j--) { } for (;;) { break; } for (i = 0, j = 1; i < j; ) { } for (var (k, l) = (0, 0); k < 1; k++) { }")]
    [DataRow("foreach (var item in items) { } foreach (var (k, v) in dict) { } foreach ((int k2, int v2) in pairs) { } foreach (ref var r in span) { }")]
    [DataRow("switch (a) { case 0: case 1 when b > 0: break; case int n and > 100: goto default; case > 50: goto case 0; default: { break; } }")]
    [DataRow("switch (a, b) { case (0, 0): break; } switch ((a)) { default: break; } switch (x) { }")]
    [DataRow("try { throw new InvalidOperationException(); } catch (InvalidOperationException ex) when (ex.Message != null) { } catch (Exception) { throw; } catch { } finally { }")]
    [DataRow("using (var s1 = new MemoryStream()) { } using (new MemoryStream()) { } using var s2 = new MemoryStream(); await using var s3 = new MemoryStream(); using (A a = x, b = y) { }")]
    [DataRow("lock (gate) { } checked { a = a * 2; } unchecked { b = b * 2; } unsafe { int* p = &a; } fixed (int* p = arr) { } fixed (char* c = s, d = t) { }")]
    [DataRow("label: a++; if (a < 3) goto label; yield return 1; yield break; return; return a; return ref a;")]
    [DataRow("int Local(int value) => value * 2; static async Task<T> LocalGeneric<T>(T value) where T : struct { await Task.Yield(); return value; }")]
    [DataRow("[Pure] int Attributed() => 1; unsafe void U() { } extern static void E(); void Params(params int[] xs, ref int r, out int o, in int i) { o = 0; }")]
    [DataRow("async Task Run() { } static int S<T, U>(T t) where T : class?, new() where U : notnull, allows ref struct => 0; ref int R(ref int x) => ref x;")]
    [DataRow("scoped ref int s = ref x; scoped Span<int> span = stackalloc int[1]; void Scoped(scoped ref int r, scoped Span<int> s) { }")]
    [DataRow("A<B> c = d; F(a < b); T? t = null; T[] arr = null; A.B c = d; A.B<C>.D e = f; a * b; x = a ? b : c;")]
    [DataRow("var var = 1; int async = 2; async = 3; var where = var; int await = 1; int yield = 2; yield++;")]
    [DataRow("await t; await foreach (var v in s) { } await using (x) { } var r = await t;")]
    [DataRow("{ } { { } } x(); new C(); new C().M(); this.x = 1; base.M(); typeof(int).ToString(); ((Action)null)(); default(C).M();")]
    public void Oracle_Statements(string statements) => AssertOracle(statements);
}
