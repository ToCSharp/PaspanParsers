using PaspanParsers.CSharp;
using static PaspanParsers.Tests.CSharp.SyntaxTestHelper;

namespace PaspanParsers.Tests.CSharp;

/// <summary>
/// Names and types (stage 2), and the '&lt;' disambiguation between type arguments and less-than.
/// </summary>
[TestClass]
public class TypeTests
{
    [TestMethod]
    public void Type_QualifiedGeneric_KeepsTypeArgumentsOfEveryPart()
    {
        var type = (NamedTypeReference)Type("A<B>.C<D>");

        CollectionAssert.AreEqual(new[] { "C" }, type.Name.Parts.ToArray());
        Assert.HasCount(1, type.TypeArguments);

        var qualifier = (NamedTypeReference)type.Qualifier;
        CollectionAssert.AreEqual(new[] { "A" }, qualifier.Name.Parts.ToArray());
        Assert.AreEqual("B", ((NamedTypeReference)qualifier.TypeArguments[0]).Name.Parts[0]);
    }

    [TestMethod]
    public void Type_DottedName_WithoutGenericPartsHasNoQualifier()
    {
        var type = (NamedTypeReference)Type("System.Collections.Generic.List<int>");

        CollectionAssert.AreEqual(new[] { "System", "Collections", "Generic", "List" }, type.Name.Parts.ToArray());
        Assert.IsNull(type.Qualifier);
        Assert.IsInstanceOfType<PredefinedTypeReference>(type.TypeArguments[0]);
    }

    [TestMethod]
    public void Type_AliasQualified()
    {
        var type = (NamedTypeReference)Type("global::System.String");

        Assert.AreEqual("global", type.Alias);
        CollectionAssert.AreEqual(new[] { "System", "String" }, type.Name.Parts.ToArray());
    }

    [TestMethod]
    public void Type_NestedGenericsCloseWithAdjacentGreaterThan()
    {
        var type = (NamedTypeReference)Type("List<List<Dictionary<string, int[]>>>");

        var inner = (NamedTypeReference)((NamedTypeReference)type.TypeArguments[0]).TypeArguments[0];
        Assert.IsInstanceOfType<ArrayTypeReference>(inner.TypeArguments[1]);
    }

    [TestMethod]
    public void Type_NullableAndArrays()
    {
        var nullableElements = (ArrayTypeReference)Type("int?[]");
        Assert.IsTrue(((PredefinedTypeReference)nullableElements.ElementType).IsNullable);

        var nullableArray = (NullableTypeReference)Type("T[]?");
        Assert.IsInstanceOfType<ArrayTypeReference>(nullableArray.ElementType);

        var jagged = (ArrayTypeReference)Type("int[][,]");
        Assert.AreEqual(2, jagged.Rank);
        Assert.AreEqual(1, ((ArrayTypeReference)jagged.ElementType).Rank);
    }

    [TestMethod]
    public void Type_Pointers()
    {
        var pointer = (PointerTypeReference)Type("void**");
        Assert.IsInstanceOfType<PointerTypeReference>(pointer.ElementType);
    }

    [TestMethod]
    public void Type_FunctionPointer()
    {
        var type = (FunctionPointerTypeReference)Type("delegate* unmanaged[Cdecl, SuppressGCTransition]<ref int, void>");

        Assert.AreEqual("unmanaged", type.CallingConvention);
        CollectionAssert.AreEqual(new[] { "Cdecl", "SuppressGCTransition" }, type.UnmanagedCallingConventions.ToArray());
        Assert.HasCount(2, type.Parameters);
        CollectionAssert.AreEqual(new[] { ParameterModifier.Ref }, type.Parameters[0].Modifiers.ToArray());
    }

    [TestMethod]
    public void Type_Tuple()
    {
        var tuple = (TupleTypeReference)Type("(int Id, string)");

        Assert.AreEqual("Id", tuple.Elements[0].Name);
        Assert.IsNull(tuple.Elements[1].Name);
    }

    [TestMethod]
    public void Type_RefAndScopedLocals()
    {
        var reference = (RefTypeReference)((LocalDeclarationStatement)Statement("ref readonly int x = ref y;")).Type;
        Assert.IsTrue(reference.IsReadOnly);

        var scoped = (ScopedTypeReference)((LocalDeclarationStatement)Statement("scoped Span<int> s = stackalloc int[1];")).Type;
        Assert.IsInstanceOfType<NamedTypeReference>(scoped.Type);
    }

    [TestMethod]
    public void Type_ContextualKeywordsAreNames()
    {
        foreach (var name in new[] { "var", "dynamic", "nint", "nuint", "record", "async" })
        {
            var type = (NamedTypeReference)Type(name);
            Assert.AreEqual(name, type.Name.Parts[0]);
        }
    }

    [TestMethod]
    public void Type_UnboundGenericInTypeOf()
    {
        var typeOf = Expression<TypeOfExpression>("typeof(Dictionary<,>)");

        var type = (NamedTypeReference)typeOf.Type;
        Assert.HasCount(2, type.TypeArguments);
        Assert.IsInstanceOfType<OmittedTypeReference>(type.TypeArguments[0]);
    }

    // ========================================
    // Type arguments or less-than
    // ========================================

    [TestMethod]
    public void LessThan_GenericInvocationWhenParenthesisFollows()
    {
        var call = Expression<InvocationExpression>("F(G<A, B>(7))");

        var inner = (InvocationExpression)call.Arguments[0].Expression;
        Assert.HasCount(2, ((NameExpression)inner.Expression).TypeArguments);
    }

    [TestMethod]
    public void LessThan_RelationalWhenIdentifierFollows()
    {
        var call = Expression<InvocationExpression>("F(a < b, c > d)");

        Assert.HasCount(2, call.Arguments);
        Assert.AreEqual(BinaryOperator.LessThan, ((BinaryExpression)call.Arguments[0].Expression).Operator);
        Assert.AreEqual(BinaryOperator.GreaterThan, ((BinaryExpression)call.Arguments[1].Expression).Operator);
    }

    [TestMethod]
    public void LessThan_GenericMemberAccess()
    {
        var access = Expression<MemberAccessExpression>("list.Select<int, string>");

        Assert.HasCount(2, access.TypeArguments);
    }

    [TestMethod]
    public void GreaterThan_ShiftIsComposedFromAdjacentTokens()
    {
        Assert.AreEqual(BinaryOperator.RightShift, Expression<BinaryExpression>("a >> b").Operator);
        Assert.AreEqual(BinaryOperator.UnsignedRightShift, Expression<BinaryExpression>("a >>> b").Operator);

        // Separated by white space these are two operators, which is not valid C#
        AssertParseFails("var x = a > > b;");
    }

    [TestMethod]
    [DataRow("List<int> list = new List<int>();")]
    [DataRow("Dictionary<string, List<int>>.KeyCollection keys = null;")]
    [DataRow("A<B>.C<D> x = null;")]
    [DataRow("global::System.String s = null;")]
    [DataRow("int?[] a = null; int[]?[] b = null; T?[]? c = null;")]
    [DataRow("int[][,] jagged = null;")]
    [DataRow("(int Id, string Name) tuple = default; (int, (string, bool)) nested = default;")]
    [DataRow("ref int r = ref x; ref readonly int ro = ref x;")]
    [DataRow("scoped ref int s = ref x; scoped Span<int> span = default;")]
    [DataRow("var t = typeof(Dictionary<,>); var u = typeof(List<>.Enumerator); var v = typeof(void);")]
    [DataRow("var n = nameof(List<>);")]
    [DataRow("F(G<A, B>(7)); F(a < b, c > d); F(a < b, c >= d);")]
    [DataRow("x = a >> b >>> c; a >>= 1; a >>>= 2; b = a > b; c = a >= b;")]
    [DataRow("var e = Array.Empty<List<int>>(); var f = x.Cast<int>().ToList();")]
    [DataRow("nint n = 0; nuint u = 0; dynamic d = null; var var = 1;")]
    [DataRow("unsafe { int* p = null; void** pp = null; delegate*<int, void> f = null; delegate* unmanaged[Cdecl]<int, int> g = null; }")]
    [DataRow("var w = x is List<int> list ? list.Count : 0;")]
    [DataRow("var c = (List<int>)o; var d = (int?)o; var e = (int[])o; var f = (global::A)o;")]
    public void Oracle_Types(string statements) => AssertOracle(statements);
}
