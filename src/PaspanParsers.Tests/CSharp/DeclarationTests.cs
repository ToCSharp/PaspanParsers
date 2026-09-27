using PaspanParsers.CSharp;

namespace PaspanParsers.Tests.CSharp;

/// <summary>
/// Declarations and the compilation unit (stage 6): types, members, namespaces, using directives
/// and top-level statements.
/// </summary>
[TestClass]
public class DeclarationTests
{
    private static CompilationUnit Unit(string code)
    {
        var unit = CSharpParser.Parse(code);
        Assert.IsNotNull(unit, "failed to parse: " + code);
        return unit;
    }

    private static MemberDeclaration Member(string member)
    {
        var type = (ClassDeclaration)Unit($"class C {{ {member} }}").Members[0];
        Assert.HasCount(1, type.Members, member);
        return type.Members[0];
    }

    private static T Member<T>(string member) where T : MemberDeclaration
    {
        var result = Member(member);
        Assert.IsInstanceOfType<T>(result, member);
        return (T)result;
    }

    private static void AssertOracle(string code)
    {
        var result = RoslynOracle.Check(code);
        Assert.AreEqual(OracleStatus.Passed, result.Status, $"{code}\n{result.Detail}");
    }

    // ========================================
    // Compilation unit
    // ========================================

    [TestMethod]
    public void CompilationUnit_ExternsUsingsAndGlobalAttributes()
    {
        var unit = Unit("""
            extern alias Legacy;
            global using System;
            global using static System.Math;
            using unsafe Pointer = int*;
            using Pair = (int First, string Second);
            using IntList = System.Collections.Generic.List<int>;
            using Legacy::Old;
            [assembly: Version("1.0")]
            class A { }
            """);

        Assert.AreEqual("Legacy", unit.ExternAliases[0].Identifier);
        Assert.IsTrue(unit.Usings[0].IsGlobal);
        Assert.IsInstanceOfType<UsingStaticDirective>(unit.Usings[1]);
        Assert.IsTrue(unit.Usings[1].IsGlobal);

        var pointer = (UsingAliasDirective)unit.Usings[2];
        Assert.IsTrue(pointer.IsUnsafe);
        Assert.IsNull(pointer.Target);
        Assert.IsInstanceOfType<PointerTypeReference>(pointer.TargetType);

        Assert.IsInstanceOfType<TupleTypeReference>(((UsingAliasDirective)unit.Usings[3]).TargetType);
        Assert.HasCount(1, ((UsingAliasDirective)unit.Usings[4]).Target.TypeArguments);
        Assert.AreEqual("Legacy", ((UsingNamespaceDirective)unit.Usings[5]).Namespace.Alias);

        Assert.AreEqual(AttributeTarget.Assembly, unit.GlobalAttributes[0].Target);
        Assert.IsInstanceOfType<ClassDeclaration>(unit.Members[0]);
    }

    [TestMethod]
    public void Namespace_BlockNestedAndFileScoped()
    {
        var block = (NamespaceDeclaration)Unit("namespace A.B { using System; namespace C { class D { } } };").Members[0];
        CollectionAssert.AreEqual(new[] { "A", "B" }, block.Name.Parts.ToArray());
        Assert.HasCount(1, block.Usings);
        Assert.IsInstanceOfType<NamespaceDeclaration>(block.Members[0]);
        Assert.IsTrue(block.HasTrailingSemicolon);

        var fileScoped = (NamespaceDeclaration)Unit("namespace N; using System; class A { } class B { }").Members[0];
        Assert.IsTrue(fileScoped.IsFileScopedNamespace);
        Assert.HasCount(1, fileScoped.Usings);
        Assert.HasCount(2, fileScoped.Members);
    }

    [TestMethod]
    public void TopLevelStatements_AreGlobalStatements()
    {
        var unit = Unit("""
            using System;
            Console.WriteLine("Hello");
            var total = Add(1, 2);
            using var stream = Open();
            static int Add(int a, int b) => a + b;
            record Message(string Text);
            """);

        Assert.HasCount(1, unit.Usings);
        Assert.IsInstanceOfType<ExpressionStatement>(((GlobalStatement)unit.Members[0]).Statement);
        Assert.IsInstanceOfType<LocalDeclarationStatement>(((GlobalStatement)unit.Members[1]).Statement);
        Assert.IsTrue(((LocalDeclarationStatement)((GlobalStatement)unit.Members[2]).Statement).IsUsing);
        Assert.IsInstanceOfType<LocalFunctionStatement>(((GlobalStatement)unit.Members[3]).Statement);
        Assert.IsInstanceOfType<RecordDeclaration>(unit.Members[4]);
    }

    // ========================================
    // Types
    // ========================================

    [TestMethod]
    public void Class_PrimaryConstructorBaseArgumentsAndModifierOrder()
    {
        var type = (ClassDeclaration)Unit("public sealed partial class Circle(double radius) : Shape(radius), IDisposable where T : new() { }").Members[0];

        CollectionAssert.AreEqual(new[] { Modifiers.Public, Modifiers.Sealed, Modifiers.Partial }, type.ModifierList.ToArray());
        Assert.HasCount(1, type.PrimaryConstructorParameters);
        Assert.HasCount(2, type.BaseTypes);
        Assert.HasCount(1, type.BaseArguments);
        Assert.HasCount(1, type.Constraints);
        Assert.IsTrue(type.HasBody);
    }

    [TestMethod]
    public void Types_WithoutBody()
    {
        Assert.IsFalse(((ClassDeclaration)Unit("class C;").Members[0]).HasBody);
        Assert.IsFalse(((StructDeclaration)Unit("struct S(int X);").Members[0]).HasBody);
        Assert.IsTrue(((InterfaceDeclaration)Unit("interface I { };").Members[0]).HasTrailingSemicolon);
    }

    [TestMethod]
    public void Record_ClassAndStruct()
    {
        var record = (RecordDeclaration)Unit("public record Person(string Name) : Base(Name);").Members[0];
        Assert.IsFalse(record.IsRecordStruct);
        Assert.IsFalse(record.HasClassKeyword);
        Assert.IsFalse(record.HasBody);
        Assert.HasCount(1, record.BaseArguments);

        Assert.IsTrue(((RecordDeclaration)Unit("record class R { }").Members[0]).HasClassKeyword);

        var structRecord = (RecordDeclaration)Unit("readonly record struct Point(int X, int Y);").Members[0];
        Assert.IsTrue(structRecord.IsRecordStruct);
        Assert.AreEqual(Modifiers.Readonly, structRecord.Modifiers);
    }

    [TestMethod]
    public void RefStruct_AndNestedTypes()
    {
        var type = (StructDeclaration)Unit("public readonly ref partial struct S { class Nested { } enum E { A } delegate void D(); }").Members[0];

        CollectionAssert.AreEqual(new[] { Modifiers.Public, Modifiers.Readonly, Modifiers.Ref, Modifiers.Partial }, type.ModifierList.ToArray());
        Assert.IsInstanceOfType<ClassDeclaration>(type.Members[0]);
        Assert.IsInstanceOfType<EnumDeclaration>(type.Members[1]);
        Assert.IsInstanceOfType<DelegateDeclaration>(type.Members[2]);
    }

    [TestMethod]
    public void Enum_TrailingComma()
    {
        var type = (EnumDeclaration)Unit("[Flags] enum Access : byte { None = 0, Read = 1 << 0, }").Members[0];

        Assert.IsInstanceOfType<PredefinedTypeReference>(type.BaseType);
        Assert.HasCount(2, type.Members);
        Assert.IsTrue(type.HasTrailingComma);
    }

    [TestMethod]
    public void Delegate_GenericWithVariance()
    {
        var type = (DelegateDeclaration)Unit("public delegate ref TResult Transformer<in T, out TResult>(T input) where T : class;").Members[0];

        Assert.IsInstanceOfType<RefTypeReference>(type.ReturnType);
        Assert.AreEqual(VarianceKind.In, type.TypeParameters[0].Variance);
        Assert.HasCount(1, type.Constraints);
    }

    [TestMethod]
    public void ExtensionBlock()
    {
        var type = (ClassDeclaration)Unit("static class E { extension<T>(List<T> list) where T : class { public bool IsEmpty => list.Count == 0; } extension(string) { } }").Members[0];

        var generic = (ExtensionBlockDeclaration)type.Members[0];
        Assert.HasCount(1, generic.TypeParameters);
        Assert.AreEqual("list", generic.Receiver.Name);
        Assert.HasCount(1, generic.Constraints);
        Assert.IsInstanceOfType<PropertyDeclaration>(generic.Members[0]);

        Assert.IsNull(((ExtensionBlockDeclaration)type.Members[1]).Receiver.Name);
    }

    // ========================================
    // Members
    // ========================================

    [TestMethod]
    public void Field_ConstVolatileAndFixedBuffer()
    {
        var constant = Member<FieldDeclaration>("public const int Max = 10, Min = -10;");
        Assert.IsTrue(constant.Modifiers.HasFlag(Modifiers.Const));
        Assert.HasCount(2, constant.Variables);

        var buffer = Member<FieldDeclaration>("public fixed byte Buffer[16];");
        Assert.IsTrue(buffer.Modifiers.HasFlag(Modifiers.Fixed));
        Assert.HasCount(1, buffer.Variables[0].BracketedArguments);
    }

    [TestMethod]
    public void Method_ExplicitInterfaceAndGeneric()
    {
        var method = Member<MethodDeclaration>("bool IEquatable<T>.Equals<U>(U other) where U : T => true;");

        var explicitInterface = (NamedTypeReference)method.ExplicitInterface;
        Assert.AreEqual("IEquatable", explicitInterface.Name.Parts[0]);
        Assert.HasCount(1, explicitInterface.TypeArguments);
        Assert.AreEqual("Equals", method.Name);
        Assert.HasCount(1, method.TypeParameters);
        Assert.IsInstanceOfType<ExpressionMethodBody>(method.Body);
    }

    [TestMethod]
    public void Method_WithoutBody()
    {
        Assert.IsNull(Member<MethodDeclaration>("public abstract void Run();").Body);
        Assert.IsNull(Member<MethodDeclaration>("public static extern void Native();").Body);
    }

    [TestMethod]
    public void Property_AccessorsInitializerAndExpressionBody()
    {
        var auto = Member<PropertyDeclaration>("public int Auto { get; protected internal set; } = 42;");
        Assert.HasCount(2, auto.Accessors);
        CollectionAssert.AreEqual(new[] { Modifiers.Protected, Modifiers.Internal }, auto.Accessors[1].ModifierList.ToArray());
        Assert.IsNotNull(auto.Initializer);

        var computed = Member<PropertyDeclaration>("public int Computed => Auto * 2;");
        Assert.IsNotNull(computed.ExpressionBody);

        var field = Member<PropertyDeclaration>("public string Name { get => field; init => field = value; }");
        Assert.IsInstanceOfType<ExpressionMethodBody>(field.Accessors[0].Body);
        Assert.AreEqual(AccessorKind.Init, field.Accessors[1].Kind);
    }

    [TestMethod]
    public void Indexer_AccessorsAndExpressionBody()
    {
        var indexer = Member<IndexerDeclaration>("public int this[int index] { get => values[index]; set => values[index] = value; }");
        Assert.HasCount(2, indexer.Accessors);

        var expression = Member<IndexerDeclaration>("string IList.this[string key, int n] => key + n;");
        Assert.IsNull(expression.Accessors);
        Assert.IsNotNull(expression.ExpressionBody);
        Assert.IsNotNull(expression.ExplicitInterface);
        Assert.HasCount(2, expression.Parameters);
    }

    [TestMethod]
    public void Event_FieldLikeAndAccessors()
    {
        var fieldLike = Member<EventDeclaration>("public event EventHandler A, B;");
        Assert.HasCount(2, fieldLike.Variables);
        Assert.IsNull(fieldLike.Accessors);

        var accessors = Member<EventDeclaration>("event EventHandler INotify.Changed { add { } remove => handlers -= value; }");
        Assert.IsNotNull(accessors.ExplicitInterface);
        Assert.IsNotNull(accessors.Accessors[0].Block);
        Assert.IsNotNull(accessors.Accessors[1].ExpressionBody);
    }

    [TestMethod]
    public void Constructor_InitializerAndDestructor()
    {
        var constructor = Member<ConstructorDeclaration>("public C(int value) : this(value, 0) { }");
        Assert.IsFalse(constructor.Initializer.IsBase);
        Assert.HasCount(2, constructor.Initializer.Arguments);

        var staticConstructor = Member<ConstructorDeclaration>("static C() => Init();");
        Assert.AreEqual(Modifiers.Static, staticConstructor.Modifiers);

        Assert.AreEqual("C", Member<DestructorDeclaration>("~C() { }").Name);
    }

    [TestMethod]
    [DataRow("public static C operator +(C a, C b) => a;", "+", false)]
    [DataRow("public static C operator checked -(C a) => a;", "-", true)]
    [DataRow("public static bool operator true(C a) => true;", "true", false)]
    [DataRow("public static C operator >>(C a, int b) => a;", ">>", false)]
    [DataRow("public static C operator >>>(C a, int b) => a;", ">>>", false)]
    [DataRow("public void operator >>>=(int b) { }", ">>>=", false)]
    [DataRow("public void operator ++() { }", "++", false)]
    public void Operator(string code, string op, bool isChecked)
    {
        var declaration = Member<OperatorDeclaration>(code);

        Assert.AreEqual(op, declaration.Operator);
        Assert.AreEqual(isChecked, declaration.IsChecked);
        AssertOracle($"class C {{ {code} }}");
    }

    [TestMethod]
    public void ConversionOperator()
    {
        var conversion = Member<ConversionOperatorDeclaration>("public static explicit operator checked int(C c) => 0;");

        Assert.IsFalse(conversion.IsImplicit);
        Assert.IsTrue(conversion.IsChecked);
        Assert.IsInstanceOfType<PredefinedTypeReference>(conversion.Type);
    }

    [TestMethod]
    [DataRow("public partial int Size { get; set; }", Modifiers.Partial)]
    [DataRow("async Task Run() { }", Modifiers.Async)]
    [DataRow("public required string Name { get; init; }", Modifiers.Required)]
    public void ContextualModifiers(string code, Modifiers modifier)
    {
        Assert.IsTrue(Member(code).Modifiers.HasFlag(modifier), code);
    }

    [TestMethod]
    [DataRow("partial x;")]
    [DataRow("async x;")]
    public void ContextualKeywords_AsTypes(string code)
    {
        var field = Member<FieldDeclaration>(code);

        Assert.AreEqual(Modifiers.None, field.Modifiers);
        Assert.AreEqual("x", field.Variables[0].Name);
    }

    // ========================================
    // Oracle
    // ========================================

    [TestMethod]
    [DataRow("class C { public int this[int i] { get => i; } }")]
    [DataRow("class C { int I.P { get; } }")]
    [DataRow("class C<T> : B<T> where T : class? { }")]
    [DataRow("class C { public static implicit operator int(C c) => 0; }")]
    [DataRow("class C() : B() { }")]
    [DataRow("interface I { static abstract int Count { get; } void M() { } }")]
    [DataRow("enum E { A, B, }")]
    [DataRow("namespace N { class A { } };")]
    [DataRow("struct S { readonly int M() => 0; public readonly int P { get => 0; } }")]
    [DataRow("unsafe struct S { fixed char name[32]; }")]
    [DataRow("class C { static C() { } ~C() { } }")]
    [DataRow("file class C { }")]
    [DataRow("partial class C { public partial C(int x); }")]
    [DataRow("static class E { extension(string text) { public bool IsBlank => text.Length == 0; } }")]
    [DataRow("delegate void D<in T>(T x);")]
    [DataRow("Console.WriteLine(1); int F() => 1;")]
    [DataRow("using System; await Task.Delay(1); return 0;")]
    [DataRow("[assembly: A] [module: B]")]
    [DataRow("global using unsafe P = int*;")]
    [DataRow("class C { record R; async A; }")]
    public void Oracle(string code)
    {
        AssertOracle(code);
    }
}
