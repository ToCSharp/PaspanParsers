using PaspanParsers.CSharp;
using static PaspanParsers.Tests.CSharp.SyntaxTestHelper;

namespace PaspanParsers.Tests.CSharp;

/// <summary>
/// Expressions (stage 3): precedence, primary and postfix forms, casts, lambdas and queries.
/// </summary>
[TestClass]
public class ExpressionTests
{
    // ========================================
    // Precedence and associativity
    // ========================================

    [TestMethod]
    public void Precedence_MultiplicativeBindsTighterThanAdditive()
    {
        var add = Expression<BinaryExpression>("a + b * c");

        Assert.AreEqual(BinaryOperator.Add, add.Operator);
        Assert.AreEqual(BinaryOperator.Multiply, ((BinaryExpression)add.Right).Operator);
    }

    [TestMethod]
    public void Precedence_BinaryOperatorsAreLeftAssociative()
    {
        var subtract = Expression<BinaryExpression>("a - b - c");

        Assert.IsInstanceOfType<BinaryExpression>(subtract.Left);
        Assert.IsInstanceOfType<NameExpression>(subtract.Right);
    }

    [TestMethod]
    public void Precedence_AssignmentAndCoalescingAreRightAssociative()
    {
        var assign = (BinaryExpression)((ExpressionStatement)Statement("a = b += c;")).Expression;
        Assert.AreEqual(BinaryOperator.Assign, assign.Operator);
        Assert.AreEqual(BinaryOperator.AddAssign, ((BinaryExpression)assign.Right).Operator);

        var coalesce = Expression<BinaryExpression>("a ?? b ?? c");
        Assert.IsInstanceOfType<NameExpression>(coalesce.Left);
        Assert.AreEqual(BinaryOperator.NullCoalescing, ((BinaryExpression)coalesce.Right).Operator);
    }

    [TestMethod]
    public void Precedence_ConditionalIsRightAssociative()
    {
        var conditional = Expression<ConditionalExpression>("a ? b : c ? d : e");

        Assert.IsInstanceOfType<ConditionalExpression>(conditional.FalseExpression);
    }

    [TestMethod]
    public void Precedence_LogicalOperators()
    {
        var or = Expression<BinaryExpression>("a || b && c | d ^ e & f == g");

        Assert.AreEqual(BinaryOperator.Or, or.Operator);
        var and = (BinaryExpression)or.Right;
        Assert.AreEqual(BinaryOperator.And, and.Operator);
        var bitwiseOr = (BinaryExpression)and.Right;
        Assert.AreEqual(BinaryOperator.BitwiseOr, bitwiseOr.Operator);
        var xor = (BinaryExpression)bitwiseOr.Right;
        Assert.AreEqual(BinaryOperator.BitwiseXor, xor.Operator);
        Assert.AreEqual(BinaryOperator.BitwiseAnd, ((BinaryExpression)xor.Right).Operator);
    }

    [TestMethod]
    public void Precedence_SwitchBindsTighterThanAdditive()
    {
        var add = Expression<BinaryExpression>("a + b switch { _ => 1 }");

        Assert.IsInstanceOfType<SwitchExpression>(add.Right);
    }

    [TestMethod]
    public void Precedence_RangeAndIndex()
    {
        var access = Expression<ElementAccessExpression>("a[1..^1]");

        var range = (RangeExpression)access.Arguments[0].Expression;
        Assert.AreEqual(UnaryOperator.Index, ((UnaryExpression)range.End).Operator);

        var open = Expression<RangeExpression>("..");
        Assert.IsNull(open.Start);
        Assert.IsNull(open.End);
    }

    [TestMethod]
    public void Precedence_IsAndAsAreRelational()
    {
        var and = Expression<BinaryExpression>("a is B && c as D != null");

        Assert.IsInstanceOfType<IsExpression>(and.Left);
        var notEqual = (BinaryExpression)and.Right;
        Assert.IsInstanceOfType<AsExpression>(notEqual.Left);
    }

    [TestMethod]
    public void Conditional_NullableTypeAfterIsIsNotConsumed()
    {
        var conditional = Expression<ConditionalExpression>("o is int ? 1 : 2");

        var test = (IsExpression)conditional.Condition;
        Assert.IsFalse(((PredefinedTypeReference)((TypePattern)test.Pattern).Type).IsNullable);
    }

    // ========================================
    // Primary and postfix expressions
    // ========================================

    [TestMethod]
    public void Postfix_MemberAccessInvocationAndElementAccessChain()
    {
        var length = Expression<MemberAccessExpression>("this.data[0].ToString().Length");

        var call = (InvocationExpression)length.Target;
        var toString = (MemberAccessExpression)call.Expression;
        var element = (ElementAccessExpression)toString.Target;
        var data = (MemberAccessExpression)element.Target;
        Assert.IsInstanceOfType<ThisExpression>(data.Target);
    }

    [TestMethod]
    public void Postfix_NullConditionalAndNullForgiving()
    {
        var member = Expression<MemberAccessExpression>("a?.b!.c");

        var forgiving = (UnaryExpression)member.Target;
        Assert.AreEqual(UnaryOperator.NullForgiving, forgiving.Operator);
        Assert.IsTrue(((MemberAccessExpression)forgiving.Operand).IsConditional);

        Assert.IsTrue(Expression<ElementAccessExpression>("list?[0]").IsConditional);
    }

    [TestMethod]
    public void Postfix_PointerMemberAccess()
    {
        var statement = (ExpressionStatement)Statement("p->x = 1;");

        var member = (MemberAccessExpression)((BinaryExpression)statement.Expression).Left;
        Assert.IsTrue(member.IsPointerAccess);
    }

    [TestMethod]
    public void Primary_PredefinedTypeMemberAccess()
    {
        var call = Expression<InvocationExpression>("int.Parse(s)");

        var parse = (MemberAccessExpression)call.Expression;
        Assert.AreEqual(PredefinedType.Int, ((PredefinedTypeExpression)parse.Target).Type);
    }

    [TestMethod]
    public void Primary_AliasQualifiedName()
    {
        var access = Expression<MemberAccessExpression>("global::System.Console");

        var alias = (AliasQualifiedNameExpression)access.Target;
        Assert.AreEqual("global", alias.Alias);
        Assert.AreEqual("System", alias.Name.Parts[0]);
    }

    [TestMethod]
    public void Primary_KeywordExpressions()
    {
        Assert.IsNotNull(Expression<TypeOfExpression>("typeof(int)").Type);
        Assert.IsNotNull(Expression<SizeOfExpression>("sizeof(int)").Type);
        Assert.IsNotNull(Expression<DefaultExpression>("default(int)").Type);
        Assert.IsNull(Expression<DefaultExpression>("default").Type);
        Assert.IsFalse(Expression<CheckedExpression>("unchecked(a * b)").IsChecked);
        Assert.IsInstanceOfType<NameExpression>(Expression<NameOfExpression>("nameof(field)").Expression);
        Assert.IsInstanceOfType<ThrowExpression>(Expression<BinaryExpression>("a ?? throw new E()").Right);
    }

    [TestMethod]
    public void Arguments_NamedRefOutAndDeclarations()
    {
        var call = Expression<InvocationExpression>("F(name: 1, ref a, out var b, out int c, out _, in d)");

        Assert.AreEqual("name", call.Arguments[0].Name);
        Assert.AreEqual(RefKind.Ref, call.Arguments[1].RefKind);

        var declaration = (DeclarationExpression)call.Arguments[2].Expression;
        Assert.AreEqual("b", ((SingleVariableDesignation)declaration.Designation).Identifier);
        Assert.IsInstanceOfType<PredefinedTypeReference>(((DeclarationExpression)call.Arguments[3].Expression).Type);

        // 'out _' is a discard name, not a declaration
        Assert.IsInstanceOfType<NameExpression>(call.Arguments[4].Expression);
        Assert.AreEqual(RefKind.In, call.Arguments[5].RefKind);
    }

    // ========================================
    // Casts, parentheses and tuples
    // ========================================

    [TestMethod]
    public void Cast_PredefinedTypeBeforeUnaryOperator()
    {
        var cast = Expression<CastExpression>("(int)-b");

        Assert.IsInstanceOfType<UnaryExpression>(cast.Expression);
    }

    [TestMethod]
    public void Cast_NameBeforeMinusIsSubtraction()
    {
        var subtract = Expression<BinaryExpression>("(A)-b");

        Assert.AreEqual(BinaryOperator.Subtract, subtract.Operator);
        Assert.IsInstanceOfType<ParenthesizedExpression>(subtract.Left);
    }

    [TestMethod]
    public void Cast_NameBeforeIdentifierOrParenthesis()
    {
        Assert.IsInstanceOfType<NameExpression>(Expression<CastExpression>("(A)b").Expression);
        Assert.IsInstanceOfType<ParenthesizedExpression>(Expression<CastExpression>("(Func<int>)(() => 1)").Expression);
    }

    [TestMethod]
    public void Tuple_NamedElementsAndDeconstruction()
    {
        var tuple = Expression<TupleExpression>("(Id: 1, Name: \"two\")");
        Assert.AreEqual("Id", tuple.Elements[0].Name);

        var assignment = (BinaryExpression)((ExpressionStatement)Statement("(int x, var y) = (1, 2);")).Expression;
        var left = (TupleExpression)assignment.Left;
        Assert.IsInstanceOfType<DeclarationExpression>(left.Elements[0].Expression);

        var deconstruction = (BinaryExpression)((ExpressionStatement)Statement("var (a, (b, _)) = t;")).Expression;
        var declaration = (DeclarationExpression)deconstruction.Left;
        var designation = (ParenthesizedVariableDesignation)declaration.Designation;
        Assert.IsInstanceOfType<DiscardDesignation>(((ParenthesizedVariableDesignation)designation.Variables[1]).Variables[1]);
    }

    // ========================================
    // Object, array and collection creation
    // ========================================

    [TestMethod]
    public void New_ObjectInitializerWithoutParentheses()
    {
        var creation = Expression<ObjectCreationExpression>("new C { A = 1, [0] = 2, B = { 3 } }");

        Assert.IsNull(creation.Arguments);
        Assert.AreEqual(InitializerKind.Object, creation.Initializer.Kind);
        Assert.IsInstanceOfType<ImplicitElementAccessExpression>(((BinaryExpression)creation.Initializer.Expressions[1]).Left);
        var nested = (InitializerExpression)((BinaryExpression)creation.Initializer.Expressions[2]).Right;
        Assert.AreEqual(InitializerKind.Collection, nested.Kind);
    }

    [TestMethod]
    public void New_CollectionInitializerWithComplexElements()
    {
        var creation = Expression<ObjectCreationExpression>("new Dictionary<string, int> { { \"a\", 1 }, { \"b\", 2 }, }");

        Assert.IsNull(creation.Arguments);
        Assert.AreEqual(InitializerKind.Collection, creation.Initializer.Kind);
        Assert.IsTrue(creation.Initializer.HasTrailingComma);
        Assert.AreEqual(InitializerKind.ComplexElement, ((InitializerExpression)creation.Initializer.Expressions[0]).Kind);
    }

    [TestMethod]
    public void New_TargetTypedAnonymousAndImplicitArray()
    {
        Assert.IsNull(Expression<ObjectCreationExpression>("new()").Type);

        var anonymous = Expression<AnonymousObjectCreationExpression>("new { Name = \"x\", a }");
        Assert.AreEqual("Name", anonymous.Members[0].Name);
        Assert.IsNull(anonymous.Members[1].Name);

        Assert.AreEqual(2, Expression<ImplicitArrayCreationExpression>("new[,] { { 1 } }").Rank);
    }

    [TestMethod]
    public void New_ArrayCreation()
    {
        var sized = Expression<ArrayCreationExpression>("new int[2, n][]");
        Assert.HasCount(2, sized.Sizes);
        CollectionAssert.AreEqual(new[] { 1 }, sized.AdditionalRanks.ToArray());

        var initialized = Expression<ArrayCreationExpression>("new int[,] { { 1, 2 } }");
        Assert.IsNull(initialized.Sizes[0]);
        Assert.AreEqual(InitializerKind.Array, initialized.Initializer.Kind);
    }

    [TestMethod]
    public void Collection_SpreadAndStackAlloc()
    {
        var collection = Expression<CollectionExpression>("[1, ..rest]");
        Assert.IsInstanceOfType<SpreadElement>(collection.Elements[1]);

        var stackAlloc = Expression<StackAllocExpression>("stackalloc int[4]");
        Assert.IsNotNull(stackAlloc.Size);
        Assert.IsNull(Expression<StackAllocExpression>("stackalloc[] { 1 }").ElementType);
    }

    // ========================================
    // Lambdas and anonymous methods
    // ========================================

    [TestMethod]
    public void Lambda_SimpleAndParenthesizedParameters()
    {
        var simple = Expression<LambdaExpression>("x => x");
        Assert.IsFalse(simple.HasParenthesizedParameters);

        var parenthesized = Expression<LambdaExpression>("(x) => x");
        Assert.IsTrue(parenthesized.HasParenthesizedParameters);
        Assert.IsNull(parenthesized.Parameters[0].Type);
    }

    [TestMethod]
    public void Lambda_ModifiersAttributesAndReturnType()
    {
        var lambda = Expression<LambdaExpression>("[A] static async int (ref int x, int y = 5) => x");

        Assert.HasCount(1, lambda.Attributes);
        CollectionAssert.AreEqual(new[] { Modifiers.Static, Modifiers.Async }, lambda.Modifiers.ToArray());
        Assert.IsTrue(lambda.IsAsync);
        Assert.IsTrue(lambda.IsStatic);
        Assert.IsInstanceOfType<PredefinedTypeReference>(lambda.ReturnType);
        Assert.AreEqual(ParameterModifier.Ref, lambda.Parameters[0].Modifier);
        Assert.IsNotNull(lambda.Parameters[1].DefaultValue);
    }

    [TestMethod]
    public void Lambda_AsyncAndAwaitAsIdentifiers()
    {
        Assert.IsInstanceOfType<InvocationExpression>(Expression("async(x)"));
        Assert.IsInstanceOfType<LambdaExpression>(Expression("async => async"));
        Assert.IsInstanceOfType<NameExpression>(Expression("await"));
        Assert.IsInstanceOfType<AwaitExpression>(Expression("await t"));
    }

    [TestMethod]
    public void AnonymousMethod_WithAndWithoutParameters()
    {
        Assert.IsNull(Expression<AnonymousMethodExpression>("delegate { }").Parameters);
        Assert.HasCount(1, Expression<AnonymousMethodExpression>("delegate (int x) { }").Parameters);
    }

    // ========================================
    // Queries
    // ========================================

    [TestMethod]
    public void Query_ClausesAndContinuation()
    {
        var query = Expression<QueryExpression>(
            "from int n in numbers join w in words on n equals w.Length into g let d = n * 2 where d > 0 orderby d descending, n ascending group n by d into byD select byD.Key");

        Assert.IsNotNull(query.FromClause.Type);
        Assert.IsInstanceOfType<JoinClause>(query.BodyClauses[0]);
        Assert.AreEqual("g", ((JoinClause)query.BodyClauses[0]).IntoIdentifier);

        var orderBy = (OrderByClause)query.BodyClauses[3];
        Assert.AreEqual(OrderDirection.Descending, orderBy.Orderings[0].Direction);
        Assert.IsTrue(orderBy.Orderings[1].HasExplicitDirection);

        Assert.IsInstanceOfType<GroupClause>(query.SelectOrGroupClause);
        Assert.AreEqual("byD", query.Continuation.Identifier);
        Assert.IsInstanceOfType<SelectClause>(query.Continuation.SelectOrGroupClause);
    }

    [TestMethod]
    public void Query_KeywordsAreIdentifiersOutsideQueries()
    {
        var add = Expression<BinaryExpression>("from + select");

        Assert.AreEqual("from", ((NameExpression)add.Left).Parts[0]);
    }

    // ========================================
    // Round trip through Roslyn
    // ========================================

    [TestMethod]
    [DataRow("var a = 1 + 2 * 3 - 4 / 5 % 6; var b = a << 2 >> 1 >>> 3; var c = a < b && b <= a || a > b ^ a >= b;")]
    [DataRow("a++; a--; ++a; --a; a += 1; a -= 1; a *= 2; a /= 2; a %= 3; a &= 1; a |= 2; a ^= 3; a <<= 1; a >>= 1; a >>>= 1; s ??= \"x\";")]
    [DataRow("var d = - -a; var e = + +a; var f = - --a; var g = !!b; var h = ~~a; var i = -(-a);")]
    [DataRow("var j = s?.Length; var k = list?[0]; var l = s!.Length; a?.b?.c(); a?.b = c; x = a?[0]?.b;")]
    [DataRow("var m = (int)3.5; var p = (object)s; var q = o as string; var r = o is string; var t = (A)-b; var u = (int)-b; var v = (A)(b);")]
    [DataRow("var w = (Func<int>)(() => 1); var y = (A.B<C>)x; var z = ((A)b).c; var aa = (a)?.b;")]
    [DataRow("var t = typeof(int); var u = sizeof(int); var v = default(int); int w = default; var x = nameof(field); var y = checked(a * b); var z = unchecked(a);")]
    [DataRow("var o = new object(); var arr = new int[] { 1, 2, 3 }; var arr2 = new int[3, 4]; var jag = new int[2][]; var im = new[] { 1, 2 }; var im2 = new[,] { { 1 } };")]
    [DataRow("var anon = new { Name = \"x\", a, b.c, }; var init = new List<int> { 1, 2, 3, }; var dict = new Dictionary<string, int> { [\"a\"] = 1, [\"b\"] = 2 };")]
    [DataRow("var d2 = new Dictionary<string, int> { { \"a\", 1 } }; var w = new C { F = 1, G = { 1, 2 }, H = { X = 1 } }; C t = new(); C t2 = new() { F = 2 }; var e = new C { };")]
    [DataRow("int[] c1 = [1, 2, .. arr]; List<int> c2 = []; int[] c3 = [1,]; int[][] c4 = [[1], [2, 3]];")]
    [DataRow("var i1 = arr[^1]; var r1 = arr[1..^1]; var r2 = arr[..]; Range r3 = ..; var r4 = arr[..2]; var r5 = arr[1..]; var r6 = a..b;")]
    [DataRow("var t1 = (1, \"two\"); var t2 = (Id: 1, Name: \"two\"); (int x, int y) = (1, 2); var (p, q) = (3, 4); (a, b) = (b, a); (var s, _) = t;")]
    [DataRow("var chained = this.data[0].ToString().Length; var call = Math.Max(a, b); base.ToString(); x = int.MaxValue; x = string.Empty; x = global::System.Math.PI;")]
    [DataRow("var gen = list.Select(item => item * 2).Where(item => item > 2).ToList(); var sel = list.Select<int, string>(i => i.ToString());")]
    [DataRow("var s1 = $\"a = {a}, b = {b,5:F2}, c = {(c ? 1 : 2)}\"; var s2 = $@\"{a}\\\"; var s3 = $$\"\"\"{{a}} {b}\"\"\";")]
    [DataRow("var th = s ?? throw new ArgumentNullException(nameof(s)); Func<int> f = () => throw new E(); var c = b ? 1 : throw new E();")]
    [DataRow("var sw = a switch { 0 => \"zero\", 1 or 2 => \"small\", > 10 when a % 2 == 0 => \"even\", _ => \"many\", };")]
    [DataRow("var rec = p with { X = 1, Y = 2 }; var rec2 = p with { }; var rec3 = a + p with { X = 1 };")]
    [DataRow("Span<int> st = stackalloc int[4]; var st2 = stackalloc int[] { 1, 2 }; Span<int> st3 = stackalloc[] { 1, 2 };")]
    [DataRow("var outCall = int.TryParse(s, out var parsed); var outDiscard = int.TryParse(s, out _); F(out int x1, out var (y1, z1)); F(ref a, in b, name: c);")]
    [DataRow("ref int r = ref (flag ? ref a : ref b); ref var e = ref arr[0]; x = ref y;")]
    [DataRow("await Task.Delay(1); var aw = await t; var aw2 = await (t); var y = await;")]
    [DataRow("var l1 = x => x; var l2 = (x) => x; var l3 = (int x, int y) => x + y; var l4 = () => { }; var l5 = async () => await t; var l6 = static x => x;")]
    [DataRow("var l7 = int (string s) => s.Length; var l8 = [Obsolete] (int x) => x; var l9 = (int x = 5) => x; var l10 = (_, _) => 0; var l11 = (ref int x) => x++;")]
    [DataRow("var l12 = async static x => x; var l13 = static async (a, b) => a; var l14 = (params int[] xs) => xs; var l15 = List<int> () => null; var l16 = ref int (ref int x) => ref x;")]
    [DataRow("Action<int> a1 = delegate (int x) { }; Action a2 = delegate { }; var a3 = static delegate (int x) { }; var a4 = async delegate { await t; };")]
    [DataRow("var q1 = from n in numbers where n > 0 let d = n * 2 orderby d descending, n select new { n, d };")]
    [DataRow("var q2 = from n in numbers join w in words on n equals w.Length into g from x in g select x; var q3 = from int n in numbers select n;")]
    [DataRow("var q4 = from w in words group w by w.Length into byLength where byLength.Count() > 1 select byLength.Key;")]
    [DataRow("var q5 = from a in xs from b in ys where a < b orderby a ascending select (a, b) into p select p;")]
    [DataRow("var q6 = from x in (items) select x; var q7 = from x in xs select (x) into y select y;")]
    [DataRow("var al = __arglist(1, 2); var mr = __makeref(x); var rt = __reftype(mr); var rv = __refvalue(mr, int);")]
    [DataRow("var select = 1; var from = select + 1; var where = from; var group = where; int await = 1; var async = 2; var nameof = 3;")]
    [DataRow("var pa = p->x; (*p).x = 1; var addr = &x; var der = *p; int* q = &arr[0];")]
    [DataRow("var cond = a ? b : c ? d : e; var cond2 = a ? [1] : [2]; var cond3 = a is int ? 1 : 2; var cond4 = a as int? ?? 0;")]
    [DataRow("var neg = -1; var pos = +1; var cmp = a == b != c; var bits = a & b | a ^ ~b; var not = !c; var idx = ^1;")]
    [DataRow("var arr3 = new (int, string)[3]; var arr4 = new int?[2]; var arr5 = new List<int>[] { null };")]
    public void Oracle_Expressions(string statements) => AssertOracle(statements);
}
