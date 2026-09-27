using PaspanParsers.Cpp;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// Names, declaration specifiers, declarators and the choice between declarations and expressions.
/// </summary>
[TestClass]
public class CppDeclaratorTests
{
    private static SimpleDeclaration Declaration(string source, int index = 0)
    {
        return (SimpleDeclaration)CppTestHelper.Parse(source).Declarations[index];
    }

    private static Declarator Declarator(string source) => Declaration(source).Declarators[0].Declarator;

    private static string Write(TranslationUnit unit)
    {
        var writer = new CppWriter();
        writer.WriteTranslationUnit(unit);
        return writer.GetResult();
    }

    /// <summary>
    /// The source parses, and so does the written code, which is written the same again.
    /// </summary>
    private static void AssertRoundTrip(string source)
    {
        var written = Write(CppTestHelper.Parse(source));
        Assert.AreEqual(written, Write(CppTestHelper.Parse(written)), source);
    }

    // ========================================
    // Names
    // ========================================

    [TestMethod]
    public void QualifiedNames_NestFromTheLeft()
    {
        var specifier = (NamedTypeSpecifier)Declaration("::a::b<int>::c x;").Specifiers.Specifiers[0];
        var name = (QualifiedName)specifier.Name;
        Assert.AreEqual("::a::b<int>::c", specifier.Name.Span.GetText("::a::b<int>::c x;"));
        Assert.AreEqual("c", name.Name.ToString());

        var qualifier = (QualifiedName)name.Qualifier;
        Assert.IsInstanceOfType<TemplateIdName>(qualifier.Name);
        var global = (QualifiedName)qualifier.Qualifier;
        Assert.IsNull(global.Qualifier);
        Assert.AreEqual("a", global.Name.ToString());
    }

    [TestMethod]
    public void TemplateArguments_AreTypesOrExpressions()
    {
        var source = "int N = 3;\nA<int, N, 1 + 2, B<C<int>>, int (*)(int), (4 > 5), D<E>::F> x;";
        var templateId = (TemplateIdName)((NamedTypeSpecifier)Declaration(source, 1).Specifiers.Specifiers[0]).Name;
        var kinds = templateId.Arguments.Select(a => a is TypeId ? "type" : "expression").ToList();
        CollectionAssert.AreEqual(new[] { "type", "expression", "expression", "type", "type", "expression", "type" }, kinds);

        var nested = (TemplateIdName)((NamedTypeSpecifier)((TypeId)templateId.Arguments[3]).Specifiers.Specifiers[0]).Name;
        Assert.AreEqual("B<C<int>>", nested.Span.GetText(source));
        AssertRoundTrip(source);
    }

    [TestMethod]
    public void TemplateId_InAnExpressionNeedsAKnownTemplate()
    {
        // a is a value: '<' compares
        var statements = CppTestHelper.Statements("int a = 1, b = 2, c = 3;\nbool d = a < b > c;");
        var initializer = (EqualsInitializer)((SimpleDeclaration)((DeclarationStatement)statements[1]).Declaration).Declarators[0].Initializer;
        Assert.AreEqual(">", ((BinaryExpression)initializer.Value).Operator);

        var options = new CppParseOptions(functionTemplateNames: ["make"]);
        Assert.IsTrue(CppParser.TryParse("int x = make<int>(1);", options, out var unit, out _));
        var call = (CallExpression)((EqualsInitializer)((SimpleDeclaration)unit.Declarations[0]).Declarators[0].Initializer).Value;
        Assert.IsInstanceOfType<TemplateIdName>(((NameExpression)call.Callee).Name);

        // The template-id of a class template is a type
        options = new CppParseOptions(templateNames: ["box"]);
        Assert.IsTrue(CppParser.TryParse("int x = box<int>(1);", options, out unit, out _));
        Assert.IsInstanceOfType<FunctionalCastExpression>(((EqualsInitializer)((SimpleDeclaration)unit.Declarations[0]).Declarators[0].Initializer).Value);
    }

    [TestMethod]
    public void OperatorNames_AreDeclaratorIds()
    {
        foreach (var (source, expected) in new[]
        {
            ("int operator+(int, int);", "+"),
            ("int operator()(int);", "()"),
            ("void *operator new[](unsigned long);", "new[]"),
            ("void operator delete(void *);", "delete"),
            ("int operator>>=(int, int);", ">>="),
            ("int operator>=(int, int);", ">="),
            ("int operator<=>(int, int);", "<=>"),
            ("int operator->*(int, int);", "->*"),
            ("int operator co_await(int);", "co_await"),
        })
        {
            var declarator = Declarator(source);
            var function = (FunctionDeclarator)(declarator is PointerDeclarator pointer ? pointer.Inner : declarator);
            Assert.AreEqual(expected, ((OperatorFunctionName)((NameDeclarator)function.Inner).Name).Operator, source);
            AssertRoundTrip(source);
        }
    }

    [TestMethod]
    public void SpecialMemberNames_HaveNoSpecifiers()
    {
        var unit = CppTestHelper.Parse("S::S(int) { }\nS::~S() { }\nS::operator int *() { return 0; }\nN::S<int>::S() { }\nunsigned long long operator\"\"_km(unsigned long long);\n");
        var names = unit.Declarations.Take(4).Cast<FunctionDefinition>().Select(f =>
        {
            Assert.IsNull(f.Specifiers);
            return ((QualifiedName)((NameDeclarator)((FunctionDeclarator)f.Declarator).Inner).Name).Name;
        }).ToList();

        Assert.IsInstanceOfType<IdentifierName>(names[0]);
        Assert.IsInstanceOfType<DestructorName>(names[1]);
        var conversion = (ConversionFunctionName)names[2];
        Assert.IsInstanceOfType<PointerDeclarator>(conversion.Type.Declarator);
        Assert.IsInstanceOfType<IdentifierName>(names[3]);

        var literal = (FunctionDeclarator)((SimpleDeclaration)unit.Declarations[4]).Declarators[0].Declarator;
        Assert.AreEqual("_km", ((LiteralOperatorName)((NameDeclarator)literal.Inner).Name).Suffix);
        AssertRoundTrip("S::S(int) { }\nS::~S() { }\nS::operator int *() { return 0; }\n");
    }

    // ========================================
    // Declaration specifiers
    // ========================================

    [TestMethod]
    public void DeclSpecifiers_TakeOneNamedType()
    {
        // After a type specifier a name is the declarator
        var declaration = Declaration("typedef int Integer;\nunsigned Integer;", 1);
        Assert.AreEqual("Integer", ((NameDeclarator)declaration.Declarators[0].Declarator).Name.ToString());

        declaration = Declaration("typedef int Integer;\nconst Integer volatile x;", 1);
        var kinds = declaration.Specifiers.Specifiers.Select(s => s.GetType().Name).ToList();
        CollectionAssert.AreEqual(new[] { "KeywordSpecifier", "NamedTypeSpecifier", "KeywordSpecifier" }, kinds);
    }

    [TestMethod]
    public void TypeSpecifiers_OfEveryKind()
    {
        var source = """
            typename T::value_type a;
            struct Point *b;
            enum Color *c;
            decltype(a) d;
            decltype(auto) e = a;
            std::integral auto f = 1;
            C<int> decltype(auto) g = f;
            decltype(a)::type h;
            """;
        var specifiers = CppTestHelper.Parse(source).Declarations.Cast<SimpleDeclaration>().Select(d => d.Specifiers.Specifiers[0]).ToList();

        Assert.IsTrue(((NamedTypeSpecifier)specifiers[0]).IsTypename);
        Assert.AreEqual("struct", ((ElaboratedTypeSpecifier)specifiers[1]).Key);
        Assert.AreEqual("enum", ((ElaboratedTypeSpecifier)specifiers[2]).Key);
        Assert.IsNotNull(((DecltypeSpecifier)specifiers[3]).Expression);
        Assert.IsNull(((DecltypeSpecifier)specifiers[4]).Expression);
        Assert.AreEqual("std::integral", ((PlaceholderTypeSpecifier)specifiers[5]).Concept.ToString());
        Assert.IsTrue(((PlaceholderTypeSpecifier)specifiers[6]).IsDecltypeAuto);
        Assert.IsInstanceOfType<DecltypeName>(((QualifiedName)((NamedTypeSpecifier)specifiers[7]).Name).Qualifier);
        AssertRoundTrip(source);
    }

    // ========================================
    // Declarators
    // ========================================

    [TestMethod]
    public void Declarators_NestAsInTheGrammar()
    {
        // An array of pointers, a pointer to an array
        var arrayOfPointers = (PointerDeclarator)Declarator("int *a[3];");
        Assert.IsInstanceOfType<ArrayDeclarator>(arrayOfPointers.Inner);
        var pointerToArray = (ArrayDeclarator)Declarator("int (*a)[3];");
        Assert.IsInstanceOfType<PointerDeclarator>(((ParenthesizedDeclarator)pointerToArray.Inner).Inner);

        // signal is a function taking (int, void (*)(int)) and returning a pointer to a function
        var source = "void (*signal(int, void (*)(int)))(int);";
        var outer = (FunctionDeclarator)Declarator(source);
        var signal = (FunctionDeclarator)((PointerDeclarator)((ParenthesizedDeclarator)outer.Inner).Inner).Inner;
        Assert.AreEqual("signal(int, void (*)(int))", signal.Span.GetText(source));
        var handler = (FunctionDeclarator)signal.Parameters[1].Declarator;
        Assert.IsNull(((PointerDeclarator)((ParenthesizedDeclarator)handler.Inner).Inner).Inner);
    }

    [TestMethod]
    public void FunctionDeclarators_HaveQualifiersAndTrailingTypes()
    {
        var function = (FunctionDeclarator)Declarator("int (S::*m)(int, ...) const volatile && noexcept(true);");
        Assert.IsInstanceOfType<MemberPointerDeclarator>(((ParenthesizedDeclarator)function.Inner).Inner);

        Assert.IsTrue(function.IsVariadic);
        CollectionAssert.AreEqual(new[] { "const", "volatile" }, function.Qualifiers.ToList());
        Assert.AreEqual("&&", function.RefQualifier);
        Assert.IsNotNull(function.Noexcept.Condition);

        var trailing = (FunctionDeclarator)Declarator("auto f() -> int (*)[3];");
        Assert.IsInstanceOfType<ArrayDeclarator>(trailing.TrailingReturnType.Declarator);

        var variadic = (FunctionDeclarator)Declarator("void f(int count...);");
        Assert.IsTrue(variadic.IsVariadic);
        Assert.HasCount(1, variadic.Parameters);

        var pack = (FunctionDeclarator)Declarator("void f(Ts &&... args);");
        Assert.IsInstanceOfType<PackDeclarator>(((ReferenceDeclarator)pack.Parameters[0].Declarator).Inner);
    }

    [TestMethod]
    public void ParenthesizedParameter_IsANameOrAFunctionType()
    {
        // [dcl.ambig.res]: (T) with a type T is a function type, (x) a parenthesized name
        var function = (FunctionDeclarator)((SimpleDeclaration)CppTestHelper.Parse("typedef int T;\nvoid f(int (T), int (x));").Declarations[1]).Declarators[0].Declarator;
        Assert.IsInstanceOfType<FunctionDeclarator>(function.Parameters[0].Declarator);
        Assert.IsInstanceOfType<ParenthesizedDeclarator>(function.Parameters[1].Declarator);
    }

    [TestMethod]
    public void RequiresClause_FollowsTheDeclarator()
    {
        var unit = CppTestHelper.Parse("void f() requires C<int> && D { }\nint g() requires true;");
        Assert.IsInstanceOfType<BinaryExpression>(((FunctionDefinition)unit.Declarations[0]).RequiresClause);
        Assert.IsNotNull(((SimpleDeclaration)unit.Declarations[1]).Declarators[0].RequiresClause);
        AssertRoundTrip("void f() requires C<int> && D { }\nint g() requires true;");
    }

    [TestMethod]
    public void WrittenDeclarators_ParseTheSame()
    {
        AssertRoundTrip("""
            int *const *volatile p, &r = *p, a[2][3], (*f)(int), (*g(int))[3];
            int S::*m, (S::*n)(int) const &;
            void h(int (*)(int), int[], int (&)[3], ...);
            auto t(int x) -> int (*)(int) noexcept;
            """);
    }

    // ========================================
    // Declarations or expressions
    // ========================================

    [TestMethod]
    public void Statement_IsADeclarationWhenItStartsWithAType()
    {
        var statements = CppTestHelper.Statements("""
            typedef int T;
            int v = 1, w = 2;
            T * a;
            v * w;
            Unknown * b;
            Unknown c;
            Unknown & d = c;
            unknown * e + w;
            vector<int> f;
            unknown(v);
            v < w;
            """);

        var kinds = statements.Select(s => s is DeclarationStatement ? "declaration" : "expression").ToList();
        CollectionAssert.AreEqual(
            new[] { "declaration", "declaration", "declaration", "expression", "declaration", "declaration", "declaration", "expression", "declaration", "expression", "expression" },
            kinds);
    }

    [TestMethod]
    public void Symbols_FollowScopes()
    {
        // T is a type inside the block that declares it, and a value after it
        var statements = CppTestHelper.Statements("int T = 1, x = 2;\n{ typedef int T; T * y; }\nT * x;");
        var block = (CompoundStatement)statements[1];
        Assert.IsInstanceOfType<DeclarationStatement>(block.Statements[1]);
        Assert.IsInstanceOfType<ExpressionStatement>(statements[2]);

        // A function hides a class of the same name
        statements = CppTestHelper.Statements("struct stat *s;\nint stat(struct stat *);\nstat(s);");
        Assert.IsInstanceOfType<ExpressionStatement>(statements[2]);
    }

    [TestMethod]
    [Timeout(10_000, CooperativeCancellation = true)]
    public void LessThanChains_AreNotTriedExponentially()
    {
        // Each '<' may start template arguments; failed attempts are remembered
        var chain = string.Concat(Enumerable.Repeat("u < ", 100));
        CppTestHelper.Parse($"int a = 1;\nint x = {chain.Replace("u", "a")}1;\nint y = {chain}1;\nvoid f() {{ {chain}1; }}");
    }

    [TestMethod]
    public void Oracle_DeclaratorsAndTypes()
    {
        CppTestHelper.AssertOracle("""
            struct S;
            typedef int T;
            T value = 1, *pointer = &value, &reference = value;
            int (*function_pointer)(int), (*array_pointer)[3];
            int S::*member_pointer = nullptr;
            void (*signal(int, void (*)(int)))(int);
            int f(int a, int (b), int (T), int c = 1);
            auto g(T x) -> decltype(x + 1) { T * y = &x; return *y; }
            int main()
            {
                T * p = &value;
                value * 2;
                return *p;
            }
            """);
    }
}
