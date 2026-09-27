using PaspanParsers.Cpp;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// Expressions: precedence, casts or parentheses, sizeof, template arguments, functional casts, new and
/// delete, lambdas, fold and requires-expressions, braced lists and initializers.
/// </summary>
[TestClass]
public class CppExpressionTests
{
    /// <summary>
    /// The expressions of the statements after the declarations in <paramref name="declarations"/>, written
    /// with their structure: binary and conditional expressions in parentheses, the others as in the source.
    /// </summary>
    private static List<string> Shapes(string declarations, params string[] expressions)
    {
        var source = CppTestHelper.InFunction(declarations + "\n" + string.Join("\n", expressions.Select(e => e + ";")));
        var body = ((FunctionDefinition)CppTestHelper.Parse(source).Declarations[0]).Body;
        return body.Statements.Skip(body.Statements.Count - expressions.Length)
            .Select(s => Shape(((ExpressionStatement)s).Expression, source))
            .ToList();
    }

    private static string Shape(Expression expression, string source) => expression switch
    {
        BinaryExpression binary => $"({Shape(binary.Left, source)} {binary.Operator} {Shape(binary.Right, source)})",
        ConditionalExpression { WhenTrue: null } conditional => $"({Shape(conditional.Condition, source)} ?: {Shape(conditional.WhenFalse, source)})",
        ConditionalExpression conditional =>
            $"({Shape(conditional.Condition, source)} ? {Shape(conditional.WhenTrue, source)} : {Shape(conditional.WhenFalse, source)})",
        UnaryExpression { IsPostfix: true } unary => $"({Shape(unary.Operand, source)}{unary.Operator})",
        UnaryExpression unary => $"({unary.Operator} {Shape(unary.Operand, source)})",
        CastExpression cast => $"cast<{cast.Type.Span.GetText(source)}>({Shape(cast.Operand, source)})",
        CallExpression call => $"call {Shape(call.Callee, source)}({string.Join(", ", call.Arguments.Select(a => Shape(a, source)))})",
        ParenthesizedExpression parenthesized => $"[{Shape(parenthesized.Expression, source)}]",
        ThrowExpression { Operand: null } => "throw",
        ThrowExpression @throw => $"throw {Shape(@throw.Operand, source)}",
        _ => expression.Span.GetText(source),
    };

    private static Expression Initializer(string source, int statement = 0)
    {
        var declaration = (DeclarationStatement)CppTestHelper.Statements(source)[statement];
        return ((EqualsInitializer)((SimpleDeclaration)declaration.Declaration).Declarators[0].Initializer).Value;
    }

    /// <summary>
    /// Compares lists of strings line by line, which shows the differing strings.
    /// </summary>
    private static void AssertLines(IEnumerable<string> expected, IEnumerable<string> actual)
    {
        Assert.AreEqual(string.Join("\n", expected), string.Join("\n", actual));
    }

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
    // Operators
    // ========================================

    [TestMethod]
    public void BinaryOperators_HaveTheirPrecedence()
    {
        var shapes = Shapes(
            "int a, b, c, d, e, f, g, h, i, j, k, l, m;",
            "a = b || c && d | e ^ f & g == h < i <=> j << k + l * m",
            "a * b + c << d <=> e < f == g & h ^ i | j && k || l",
            "a >> b >= c > d",
            "a, b = c, d");
        AssertLines(
            new[]
            {
                "(a = (b || (c && (d | (e ^ (f & (g == (h < (i <=> (j << (k + (l * m))))))))))))",
                "(((((((((((a * b) + c) << d) <=> e) < f) == g) & h) ^ i) | j) && k) || l)",
                "(((a >> b) >= c) > d)",
                "((a , (b = c)) , d)",
            },
            shapes);
    }

    [TestMethod]
    public void AssignmentsAndConditionals_AreRightAssociative()
    {
        var shapes = Shapes(
            "int a, b, c, d, e;",
            "a = b += c",
            "a ? b : c ? d : e",
            "a ? b, c : d = e",
            "a ?: b",
            "a ? throw 1 : throw",
            "a = b ? c : d");
        AssertLines(
            new[] { "(a = (b += c))", "(a ? b : (c ? d : e))", "(a ? (b , c) : (d = e))", "(a ?: b)", "(a ? throw 1 : throw)", "(a = (b ? c : d))" },
            shapes);
    }

    [TestMethod]
    public void UnaryAndPointerToMemberOperators_BindTighter()
    {
        var shapes = Shapes("int a, b, c, *p;", "-a .* b * c", "*p++", "- -a", "!~a", "++*p", "a->*b->*c", "co_await a + b");
        AssertLines(
            new[] { "(((- a) .* b) * c)", "(* (p++))", "(- (- a))", "(! (~ a))", "(++ (* p))", "((a ->* b) ->* c)", "((co_await a) + b)" },
            shapes);
    }

    // ========================================
    // Casts, sizeof and template arguments
    // ========================================

    [TestMethod]
    public void Parentheses_HoldACastWhenTheyHoldAType()
    {
        var shapes = Shapes(
            "typedef int T;\nint a, n;",
            "(T)-n",
            "(a) - n",
            "(T)(n)",
            "(a)(n)",
            "(int)+n",
            "(const T *)&n",
            "(unknown)n",
            "(unknown)(n)",
            "(unknown) - n",
            "(unknown *)n",
            "(f()) + n",
            "(T())");
        AssertLines(
            new[]
            {
                "cast<T>((- n))", "([a] - n)", "cast<T>([n])", "call [a](n)", "cast<int>((+ n))", "cast<const T *>((& n))",
                "cast<unknown>(n)", "call [unknown](n)", "([unknown] - n)", "cast<unknown *>(n)", "([call f()] + n)", "[T()]",
            },
            shapes);
    }

    [TestMethod]
    public void SizeOf_TakesATypeOrAnExpression()
    {
        var statements = CppTestHelper.Statements("int a;\nsizeof(int) * 2;\nsizeof(a) * 2;\nsizeof a;\nsizeof(unknown);\nsizeof -a;\nalignof(int);");
        var operands = statements.Skip(1).Select(s => ((ExpressionStatement)s).Expression)
            .Select(e => e is BinaryExpression binary ? binary.Left : e)
            .Select(e => ((SizeOfExpression)e).Operand.GetType().Name)
            .ToList();
        AssertLines(
            new[] { "TypeId", "ParenthesizedExpression", "NameExpression", "TypeId", "UnaryExpression", "TypeId" },
            operands);

        var lambda = CppTestHelper.Expression<LambdaExpression>("[](auto... xs) { return sizeof...(xs); }");
        var pack = (SizeOfPackExpression)((ReturnStatement)lambda.Body.Statements[0]).Expression;
        Assert.AreEqual("xs", pack.Pack.Identifier);
    }

    [TestMethod]
    public void UnknownNames_TakeTemplateArgumentsBeforeTokensThatCannotFollowAComparison()
    {
        var shapes = Shapes(
            "int a, b, c;",
            "get<0>(a)",
            "(x < a > b)",
            "a < b > c",
            "std::array<int, 3>{}",
            "is_same_v<int, long> && a",
            "a.template get<1>()",
            "s.get<1>()");
        AssertLines(
            new[] { "call get<0>(a)", "[((x < a) > b)]", "((a < b) > c)", "std::array<int, 3>{}", "(is_same_v<int, long> && a)", "call a.template get<1>()", "call s.get<1>()" },
            shapes);

        var cast = (FunctionalCastExpression)CppTestHelper.Expression("std::array<int, 3>{}");
        Assert.IsInstanceOfType<TemplateIdName>(((QualifiedName)((NamedTypeSpecifier)cast.Type).Name).Name);
    }

    [TestMethod]
    public void TypesFollowedByParenthesesOrBraces_AreFunctionalCasts()
    {
        var statements = CppTestHelper.Statements("""
            typedef int T;
            int a;
            T(a) + 1;
            int{a} + unsigned(a) + auto(a);
            unknown{a} + unknown(a);
            decltype(a)(1) + decltype(a){2};
            """);
        var kinds = new List<string>();
        void Collect(Expression expression)
        {
            if (expression is BinaryExpression binary)
            {
                Collect(binary.Left);
                Collect(binary.Right);
            }
            else
            {
                kinds.Add(expression.GetType().Name);
            }
        }

        foreach (var statement in statements.Skip(2))
        {
            Collect(((ExpressionStatement)statement).Expression);
        }

        AssertLines(
            new[]
            {
                "FunctionalCastExpression", "LiteralExpression", "FunctionalCastExpression", "FunctionalCastExpression", "FunctionalCastExpression",
                "FunctionalCastExpression", "CallExpression", "FunctionalCastExpression", "FunctionalCastExpression",
            },
            kinds);

        var typename = (FunctionalCastExpression)CppTestHelper.Expression("typename T::type(1)");
        Assert.IsTrue(((NamedTypeSpecifier)typename.Type).IsTypename);
        Assert.IsInstanceOfType<ParenthesizedInitializer>(typename.Initializer);
    }

    [TestMethod]
    public void NamedCastsAndTypeid()
    {
        var cast = CppTestHelper.Expression<NamedCastExpression>("static_cast<A<B<int>>>(x)");
        Assert.AreEqual("static_cast", cast.Keyword);
        var type = (TemplateIdName)((NamedTypeSpecifier)cast.Type.Specifiers.Specifiers[0]).Name;
        Assert.IsInstanceOfType<TypeId>(type.Arguments[0]);

        Assert.IsInstanceOfType<TypeId>(CppTestHelper.Expression<TypeidExpression>("typeid(int)").Operand);
        Assert.IsInstanceOfType<BinaryExpression>(CppTestHelper.Expression<TypeidExpression>("typeid(1 + 2)").Operand);
        Assert.IsInstanceOfType<ParenthesizedExpression>(CppTestHelper.Expression<NoexceptExpression>("noexcept((1))").Operand);
    }

    // ========================================
    // new, delete, throw
    // ========================================

    [TestMethod]
    public void NewExpressions_HavePlacementOrParenthesizedTypes()
    {
        var statements = CppTestHelper.Statements("""
            void *buffer;
            new int;
            new (buffer) int(1);
            new (int *)(nullptr);
            ::new (std::nothrow) int[3]{ 1, 2, 3 };
            new int *[2][3];
            new (buffer) (int);
            """);
        var news = statements.Skip(1).Select(s => (NewExpression)((ExpressionStatement)s).Expression).ToList();

        Assert.IsNull(news[0].Placement);
        Assert.HasCount(1, news[1].Placement);
        Assert.IsInstanceOfType<ParenthesizedInitializer>(news[1].Initializer);
        Assert.IsTrue(news[2].IsParenthesizedType);
        Assert.IsNull(news[2].Placement);
        Assert.IsTrue(news[3].IsGlobal);
        Assert.IsInstanceOfType<BracedInitializer>(news[3].Initializer);
        Assert.IsInstanceOfType<ArrayDeclarator>(news[3].Type.Declarator);

        var type = (PointerDeclarator)news[4].Type.Declarator;
        Assert.IsInstanceOfType<ArrayDeclarator>(type.Inner);
        Assert.IsTrue(news[5].IsParenthesizedType);
        Assert.HasCount(1, news[5].Placement);
    }

    [TestMethod]
    public void DeleteAndThrow()
    {
        var delete = CppTestHelper.Expression<DeleteExpression>("::delete[] p");
        Assert.IsTrue(delete.IsGlobal);
        Assert.IsTrue(delete.IsArray);
        Assert.IsNull(CppTestHelper.Expression<ThrowExpression>("throw").Operand);
        Assert.IsInstanceOfType<YieldExpression>(CppTestHelper.Expression("co_yield { 1, 2 }"));
    }

    // ========================================
    // Primary expressions
    // ========================================

    [TestMethod]
    public void Lambdas_HaveAllTheirParts()
    {
        var lambda = CppTestHelper.Expression<LambdaExpression>(
            "[=, &a, b = 1, ...c = d, e..., this, *this]<typename T, int N = 1> requires C<T> [[nodiscard]] (T x, int y = 0) mutable constexpr noexcept -> int requires D<T> { return x; }");
        Assert.AreEqual("=", lambda.CaptureDefault);
        AssertLines(
            new[] { "&a", "b=", "...c=", "e...", "this", "*this" },
            lambda.Captures.Select(c => (c.IsStarThis ? "*" : "") + (c.IsByReference ? "&" : "") + (c.IsPack && c.Initializer != null ? "..." : "")
                + (c.IsThis ? "this" : c.Identifier) + (c.IsPack && c.Initializer == null ? "..." : "") + (c.Initializer != null ? "=" : "")).ToList());
        Assert.HasCount(2, lambda.TemplateParameters);
        Assert.IsInstanceOfType<TypeTemplateParameter>(lambda.TemplateParameters[0]);
        Assert.IsInstanceOfType<NonTypeTemplateParameter>(lambda.TemplateParameters[1]);
        Assert.IsNotNull(lambda.TemplateRequiresClause);
        Assert.AreEqual("nodiscard", lambda.Attributes[0].Attributes[0].Name);
        Assert.HasCount(2, lambda.Parameters);
        AssertLines(new[] { "mutable", "constexpr" }, lambda.Specifiers.ToList());
        Assert.IsNotNull(lambda.Noexcept);
        Assert.IsNotNull(lambda.TrailingReturnType);
        Assert.IsNotNull(lambda.RequiresClause);

        var empty = CppTestHelper.Expression<LambdaExpression>("[] {}");
        Assert.IsNull(empty.Parameters);
        Assert.IsEmpty(empty.Captures);
    }

    [TestMethod]
    public void Lambdas_DeclareTheirParametersAndTemplateParameters()
    {
        // Inside the lambda, T is a type and x a value: T * p declares p, x * y multiplies
        var lambda = CppTestHelper.Expression<LambdaExpression>("[]<typename T>(T x, T y) { T * p; x * y; }");
        Assert.IsInstanceOfType<DeclarationStatement>(lambda.Body.Statements[0]);
        Assert.IsInstanceOfType<ExpressionStatement>(lambda.Body.Statements[1]);

        // After the lambda, T is not known
        var statements = CppTestHelper.Statements("int T = 1, p = 2;\n[]<typename T>(T x) { return x; };\nT * p;");
        Assert.IsInstanceOfType<ExpressionStatement>(statements[2]);
    }

    [TestMethod]
    public void TemplateParameters_OfEveryKind()
    {
        var lambda = CppTestHelper.Expression<LambdaExpression>(
            "[]<typename A, class... B, typename C = int, int D, auto... E, std::integral F, Sortable<int> G, std::size_t H, template <typename> class I = X, typename T::type J>() {}");
        AssertLines(
            new[]
            {
                "TypeTemplateParameter", "TypeTemplateParameter", "TypeTemplateParameter", "NonTypeTemplateParameter", "NonTypeTemplateParameter",
                "TypeTemplateParameter", "TypeTemplateParameter", "NonTypeTemplateParameter", "TemplateTemplateParameter", "NonTypeTemplateParameter",
            },
            lambda.TemplateParameters.Select(p => p.GetType().Name).ToList());
        Assert.IsTrue(((TypeTemplateParameter)lambda.TemplateParameters[1]).IsPack);
        Assert.IsNotNull(((TypeTemplateParameter)lambda.TemplateParameters[5]).Constraint);
    }

    [TestMethod]
    public void FoldExpressions_OfEveryForm()
    {
        var lambda = CppTestHelper.Expression<LambdaExpression>("[](auto... xs) { (xs + ...); (... * xs); (0 - ... - xs); (xs, ...); (xs = ...); ((xs + 1) && ...); }");
        var folds = lambda.Body.Statements.Select(s => (FoldExpression)((ExpressionStatement)s).Expression).ToList();
        AssertLines(new[] { "+", "*", "-", ",", "=", "&&" }, folds.Select(f => f.Operator).ToList());
        AssertLines(["True", "False", "True", "True", "True", "True"], folds.Select(f => (f.Left != null).ToString()));
        AssertLines(["False", "True", "True", "False", "False", "False"], folds.Select(f => (f.Right != null).ToString()));
    }

    [TestMethod]
    public void PackExpansions_InListsAndArguments()
    {
        var lambda = CppTestHelper.Expression<LambdaExpression>("[](auto... xs) { f(xs...); g({ xs... }, h(xs)...); a[xs...]; }");
        var call = (CallExpression)((ExpressionStatement)lambda.Body.Statements[0]).Expression;
        Assert.IsInstanceOfType<PackExpansionExpression>(call.Arguments[0]);
        var second = (CallExpression)((ExpressionStatement)lambda.Body.Statements[1]).Expression;
        Assert.IsInstanceOfType<PackExpansionExpression>(((InitializerListExpression)second.Arguments[0]).Elements[0]);
        Assert.IsInstanceOfType<PackExpansionExpression>(second.Arguments[1]);
    }

    [TestMethod]
    public void RequiresExpressions_HaveEveryKindOfRequirement()
    {
        var requires = CppTestHelper.Expression<RequiresExpression>(
            "requires (T a, T *b) { a + 1; typename T::type; { a } noexcept -> std::same_as<int>; { *b }; requires C<T> && sizeof(T) > 1; }");
        Assert.HasCount(2, requires.Parameters);
        AssertLines(
            new[] { "SimpleRequirement", "TypeRequirement", "CompoundRequirement", "CompoundRequirement", "NestedRequirement" },
            requires.Requirements.Select(r => r.GetType().Name).ToList());
        var compound = (CompoundRequirement)requires.Requirements[2];
        Assert.IsTrue(compound.IsNoexcept);
        Assert.IsInstanceOfType<QualifiedName>(compound.TypeConstraint);
        Assert.IsNull(CppTestHelper.Expression<RequiresExpression>("requires { 1; }").Parameters);
    }

    [TestMethod]
    public void BracedLists_HaveDesignatorsAndTrailingCommas()
    {
        var list = (InitializerListExpression)Initializer("S s = { .x = 1, .y{ 2 }, .a.b = 3, [2] = 4, { 5 }, 6, };");
        Assert.IsTrue(list.HasTrailingComma);
        Assert.HasCount(6, list.Elements);
        var first = (DesignatedInitializerExpression)list.Elements[0];
        Assert.IsTrue(first.HasEquals);
        Assert.AreEqual("x", first.Designators[0].Member);
        var second = (DesignatedInitializerExpression)list.Elements[1];
        Assert.IsFalse(second.HasEquals);
        Assert.IsInstanceOfType<InitializerListExpression>(second.Value);
        Assert.HasCount(2, ((DesignatedInitializerExpression)list.Elements[2]).Designators);
        Assert.IsNotNull(((DesignatedInitializerExpression)list.Elements[3]).Designators[0].Index);
        Assert.IsInstanceOfType<InitializerListExpression>(list.Elements[4]);

        // '[' in a list starts a lambda unless '=' follows the bracket
        var lambdas = (InitializerListExpression)Initializer("F f[] = { [] { return 1; }, [x] { return x; } };");
        Assert.IsInstanceOfType<LambdaExpression>(lambdas.Elements[1]);
    }

    [TestMethod]
    public void PostfixExpressions()
    {
        var statements = CppTestHelper.Statements("p->~T();\nx.T::~T();\na[1, 2];\na[{ 1 }];\na[];\nx.operator()();\nthis->y;");
        var destructor = (MemberAccessExpression)((CallExpression)((ExpressionStatement)statements[0]).Expression).Callee;
        Assert.IsInstanceOfType<DestructorName>(destructor.Member);
        Assert.AreEqual("->", destructor.Operator);
        Assert.IsInstanceOfType<QualifiedName>(((MemberAccessExpression)((CallExpression)((ExpressionStatement)statements[1]).Expression).Callee).Member);
        Assert.HasCount(2, ((SubscriptExpression)((ExpressionStatement)statements[2]).Expression).Arguments);
        Assert.IsInstanceOfType<InitializerListExpression>(((SubscriptExpression)((ExpressionStatement)statements[3]).Expression).Arguments[0]);
        Assert.IsEmpty(((SubscriptExpression)((ExpressionStatement)statements[4]).Expression).Arguments);
        Assert.IsInstanceOfType<ThisExpression>(((MemberAccessExpression)((ExpressionStatement)statements[6]).Expression).Object);
    }

    [TestMethod]
    public void DoubleBracket_IsNeverASubscript()
    {
        CppTestHelper.AssertParseFails(CppTestHelper.InFunction("a[[] { return 0; }()];"));
    }

    // ========================================
    // Initializers
    // ========================================

    [TestMethod]
    public void Initializers_OfEveryForm()
    {
        var declaration = (SimpleDeclaration)CppTestHelper.Parse("int a = 1, b(2), c{ 3 }, d = { 4 }, e{}, f((5));").Declarations[0];
        AssertLines(
            new[] { "EqualsInitializer", "ParenthesizedInitializer", "BracedInitializer", "EqualsInitializer", "BracedInitializer", "ParenthesizedInitializer" },
            declaration.Declarators.Select(d => d.Initializer.GetType().Name).ToList());
        Assert.IsInstanceOfType<InitializerListExpression>(((EqualsInitializer)declaration.Declarators[3].Initializer).Value);
    }

    [TestMethod]
    public void ParenthesesAfterADeclarator_AreParametersUnlessTheyHoldValues()
    {
        var unit = CppTestHelper.Parse("typedef int T;\nint a = 1;\nint b(a), c(T), d(1), e(), f((a));");
        var declarators = ((SimpleDeclaration)unit.Declarations[2]).Declarators;
        AssertLines(
            new[] { "ParenthesizedInitializer", null, "ParenthesizedInitializer", null, "ParenthesizedInitializer" },
            declarators.Select(d => d.Initializer?.GetType().Name).ToList());
        Assert.IsInstanceOfType<FunctionDeclarator>(declarators[1].Declarator);
        Assert.IsInstanceOfType<FunctionDeclarator>(declarators[3].Declarator);
    }

    // ========================================
    // Writer and oracle
    // ========================================

    [TestMethod]
    public void WrittenExpressions_ParseTheSame()
    {
        AssertRoundTrip("""
            void f()
            {
                a = - -b + (int)-c + !~d + a.*b + p->*q + x ?: y;
                a = { 1, { 2, 3 }, .x = 4, .y{ 5 }, [6] = 7, };
                auto l = [=, &a, b = 1, ...c = d, e..., this, *this]<typename T, int N = 1, template <typename> class TT> requires C<T>
                    [[nodiscard, gnu::always_inline]] [[using gnu: hot]] (T x, int y = 0, ...) mutable static noexcept(true) -> int requires D<T> { return x; };
                auto r = requires (int a) { a + 1; typename T::type; { a } noexcept -> std::same_as<int>; requires C<T>; };
                new (buffer) int[3]{ 1, 2 }; ::new (int *)(nullptr); delete[] p; ::delete q;
                throw; throw 1; co_await x; co_yield { 1 };
                sizeof(int) + sizeof x + sizeof...(xs) + alignof(int) + noexcept(f()) + __alignof__(int);
                static_cast<A<B<int>>>(x); typeid(int); typeid(x); int(1); T{ 2 }; typename T::type(); decltype(x)(1);
                (xs + ...); (... + xs); (xs + ... + 0); f(xs...); a[1, 2];
                p->~T(); x.template f<int>(); this->x;
            }
            """);
    }

    [TestMethod]
    public void Oracle_Expressions()
    {
        CppTestHelper.AssertOracle("""
            typedef int T;
            int g(int, int);
            int designated[4] = { [1] = 2, [3] = 4 };
            int f(int a, int b, int *p)
            {
                auto expand = [](auto... xs) { int list[] = { xs..., 0 }; return g(xs...) + list[0]; };
                int r = expand(1, 2) + (T)-a + (a) - b + sizeof(T) * 2 + sizeof (a) * 2;
                r = a < b ? a : b;
                r = static_cast<int>(1.5) < 2 > (3);
                r += (p[0] = 1, p[1]);
                auto parenthesized = new (T *)(p);
                delete parenthesized;
                return r;
            }
            """);
    }

    [TestMethod]
    public void Oracle_LambdasAndRequirements()
    {
        CppTestHelper.AssertOracle("""
            int f(int a)
            {
                auto l = [a, &r = a]<typename T, int N = 2>(T x) mutable noexcept -> T { r++; return x * N + a; };
                auto m = []<typename T> requires (sizeof(T) >= 4) (T x) { return x; };
                bool b = requires (int x) { x + 1; { x } noexcept; requires sizeof(int) == 4; };
                return l(1) + m(2) + b;
            }
            """);
    }

    [TestMethod]
    [DataRow("#include <new>\nauto a = std::nothrow_t{};\nint *b = new int[3]();\nauto c = alignof(int[3]) + sizeof(int (*)[3]);")]
    [DataRow("#include <compare>\nbool a = std::strong_ordering::less < 0;\nvoid f(int x) { x ? throw 1 : throw 2; }")]
    [DataRow("void f(){ (void)0, (void)1; int a[2][3]; a[1][2] = 3; int *p = &a[0][0]; p = &(*p); bool b = !(1 < 2); static_cast<void>(0); }")]
    [DataRow("int g(...);\nvoid f(){ auto l = []<class... Ts>(Ts... ts) { return (sizeof(ts) + ...) + g(sizeof(ts)...); }; l(1, 2); }")]
    [DataRow("auto l = []() -> decltype(auto) { return 1; };\nauto m = []() consteval { return 2; };\nint (*p)() = +[] { return 3; };\nint x = (int)(unsigned char)'a';\nint (*q)(int) = (int (*)(int))nullptr;")]
    [DataRow("#include <typeinfo>\nchar c = typeid(int).name()[0];\nbool n = noexcept(noexcept(c));\nbool r = requires { typename std::type_info; };")]
    [DataRow("void f(int n) { auto outer = []<typename T>(T x) { return [x]<typename U>(U y) -> T { return x + T(y); }; }; outer(1)(2L); int *m = new int[n]{}; delete[] m; }")]
    [DataRow("int a = 1, b = 2;\nint c = a < b ? a : b;\nbool d = a < b && b > a;\nint e = (a < b) + (b > a);\nint g = a < (b > 1);")]
    [DataRow("void f(int *p) { if (p) p[0] = 1; else { *p = 2; } while (*p) --*p; }")]
    [DataRow("typedef int T;\nint f(T a) { T b = T(a) + T{a} + T(); return (T)b + sizeof(T) + (T)(a); }")]
    [DataRow("int x = [](auto&&... a) { return (a + ... + 1); }(1, 2.0, 3L);\nauto y = [x = 1]<typename T = int>(T v = T{}) mutable -> T { return v + ++x; };")]
    public void Oracle_Snippets(string source)
    {
        CppTestHelper.AssertOracle(source);
    }
}
