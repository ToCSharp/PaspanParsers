using PaspanParsers.Cpp;

namespace PaspanParsers.Tests.Cpp;

/// <summary>
/// Declarations: namespaces, using declarations and directives, alias declarations, linkage specifications,
/// classes and their members, enumerations, templates, concepts, modules, attributes and GNU extensions;
/// and the scopes that make names declared in them known.
/// </summary>
[TestClass]
public class CppDeclarationTests
{
    private static string Write(TranslationUnit unit)
    {
        var writer = new CppWriter();
        writer.WriteTranslationUnit(unit);
        return writer.GetResult();
    }

    private static IReadOnlyList<Declaration> Declarations(string source) => CppTestHelper.Parse(source).Declarations;

    private static T Single<T>(string source) where T : Declaration
    {
        var declarations = Declarations(source);
        Assert.HasCount(1, declarations, source);
        Assert.IsInstanceOfType<T>(declarations[0], source);
        return (T)declarations[0];
    }

    /// <summary>
    /// The class defined by a declaration without declarators: <c>struct S { … };</c>.
    /// </summary>
    private static ClassSpecifier Class(Declaration declaration)
    {
        return (ClassSpecifier)((SimpleDeclaration)declaration).Specifiers.Specifiers.Single(s => s is ClassSpecifier);
    }

    /// <summary>
    /// "declaration" or "expression" for each statement of the last function of <paramref name="source"/>.
    /// </summary>
    private static List<string> StatementKinds(string source)
    {
        var function = (FunctionDefinition)Declarations(source).Last();
        return function.Body.Statements.Select(s => s switch
        {
            DeclarationStatement => "declaration",
            ExpressionStatement => "expression",
            _ => s.GetType().Name,
        }).ToList();
    }

    // ========================================
    // Namespaces, using and linkage
    // ========================================

    [TestMethod]
    public void Namespaces_AreNestedInlineUnnamedOrAliases()
    {
        var declarations = Declarations("""
            namespace a::b::inline c { int x; }
            inline namespace v1 {}
            namespace [[deprecated]] {}
            namespace alias = a::b;
            """);

        var nested = (NamespaceDefinition)declarations[0];
        CollectionAssert.AreEqual(new[] { new NamespaceName("a"), new NamespaceName("b"), new NamespaceName("c", IsInline: true) }, nested.Names.ToArray());
        Assert.IsInstanceOfType<SimpleDeclaration>(nested.Declarations[0]);

        Assert.IsTrue(((NamespaceDefinition)declarations[1]).IsInline);
        var unnamed = (NamespaceDefinition)declarations[2];
        Assert.IsEmpty(unnamed.Names);
        Assert.HasCount(1, unnamed.Attributes);

        var alias = (NamespaceAliasDefinition)declarations[3];
        Assert.AreEqual("alias", alias.Alias);
        Assert.IsInstanceOfType<QualifiedName>(alias.Target);
    }

    [TestMethod]
    public void Using_DeclaresDirectivesNamesEnumsAndAliases()
    {
        var declarations = Declarations("""
            using namespace std;
            using std::swap, ::std::move;
            using enum Color;
            using Integer [[deprecated]] = int;
            template <class... Bases> struct All : Bases... { using typename Bases::type...; };
            """);

        Assert.IsInstanceOfType<UsingDirective>(declarations[0]);
        var usings = (UsingDeclaration)declarations[1];
        Assert.HasCount(2, usings.Declarators);
        Assert.IsInstanceOfType<QualifiedName>(usings.Declarators[1].Name);
        Assert.IsInstanceOfType<UsingEnumDeclaration>(declarations[2]);

        var alias = (AliasDeclaration)declarations[3];
        Assert.AreEqual("Integer", alias.Identifier);
        Assert.HasCount(1, alias.Attributes);

        var member = (UsingDeclaration)Class(((TemplateDeclaration)declarations[4]).Declaration).Members[0];
        Assert.IsTrue(member.Declarators[0].IsTypename);
        Assert.IsTrue(member.Declarators[0].IsPackExpansion);
    }

    [TestMethod]
    public void LinkageSpecifications_TakeADeclarationOrBraces()
    {
        var declarations = Declarations("""
            extern "C" int f(int);
            extern "C++" { int g(); struct S; }
            extern "C" {}
            """);

        var single = (LinkageSpecification)declarations[0];
        Assert.AreEqual("C", single.Language);
        Assert.IsFalse(single.HasBraces);
        Assert.HasCount(1, single.Declarations);

        var block = (LinkageSpecification)declarations[1];
        Assert.AreEqual("C++", block.Language);
        Assert.IsTrue(block.HasBraces);
        Assert.HasCount(2, block.Declarations);
        Assert.IsEmpty(((LinkageSpecification)declarations[2]).Declarations);
    }

    [TestMethod]
    public void BlockDeclarations_IncludeUsingAliasesAndLocalClasses()
    {
        var statements = CppTestHelper.Statements("""
            using namespace ns;
            using ns::value;
            namespace n = ns;
            using T = int;
            struct Local { int x; } local{ 1 };
            for (using U = int; U i : values) {}
            asm("nop");
            """);

        var declarations = statements.Take(5).Cast<DeclarationStatement>().Select(s => s.Declaration.GetType()).ToList();
        CollectionAssert.AreEqual(
            new[] { typeof(UsingDirective), typeof(UsingDeclaration), typeof(NamespaceAliasDefinition), typeof(AliasDeclaration), typeof(SimpleDeclaration) },
            declarations);
        Assert.IsInstanceOfType<AliasDeclaration>(((DeclarationStatement)((RangeForStatement)statements[5]).InitStatement).Declaration);
        Assert.IsInstanceOfType<AsmDeclaration>(((DeclarationStatement)statements[6]).Declaration);
    }

    // ========================================
    // Classes
    // ========================================

    [TestMethod]
    public void Classes_HaveBasesAndMembers()
    {
        var @class = Class(Single<SimpleDeclaration>("""
            struct [[nodiscard]] Leaf final : public Base, virtual protected Other, private virtual Third
            {
            public:
                int a = 1, b{ 2 };
                unsigned flag : 1 = 1, : 0;
                static constexpr int c = 3;
                virtual void draw() const = 0;
                void update() override final;
                friend class Friend;
                friend bool operator==(const Leaf &, const Leaf &) = default;
            private:
                ;
            };
            """));

        Assert.AreEqual("struct", @class.Key);
        Assert.IsTrue(@class.IsFinal);
        Assert.HasCount(1, @class.Attributes);
        Assert.AreEqual("public", @class.Bases[0].Access);
        Assert.IsTrue(@class.Bases[1].IsVirtual);
        Assert.IsTrue(@class.Bases[1].IsVirtualFirst);
        Assert.IsTrue(@class.Bases[2].IsVirtual);
        Assert.IsFalse(@class.Bases[2].IsVirtualFirst);

        var members = @class.Members;
        Assert.AreEqual("public", ((AccessSpecifier)members[0]).Access);
        var bitFields = ((SimpleDeclaration)members[2]).Declarators;
        Assert.IsNotNull(bitFields[0].BitFieldWidth);
        Assert.IsInstanceOfType<EqualsInitializer>(bitFields[0].Initializer);
        Assert.IsNull(bitFields[1].Declarator);
        Assert.IsTrue(((SimpleDeclaration)members[4]).Declarators[0].IsPure);
        CollectionAssert.AreEqual(new[] { "override", "final" }, ((SimpleDeclaration)members[5]).Declarators[0].VirtSpecifiers.ToArray());
        Assert.IsInstanceOfType<ElaboratedTypeSpecifier>(((SimpleDeclaration)members[6]).Specifiers.Specifiers[1]);
        Assert.IsTrue(((FunctionDefinition)members[7]).IsDefaulted);
        Assert.IsInstanceOfType<EmptyDeclaration>(members[9]);
    }

    [TestMethod]
    public void Constructors_HaveInitializersAndTryBlocks()
    {
        var @class = Class(Single<SimpleDeclaration>("""
            struct S : Base
            {
                int a, b;
                S() : S(0) {}
                explicit S(int x) : Base{ x }, a(x), b() {}
                S(const S &) = delete;
                S(int x, int y) try : a(x), b(y) {} catch (...) {}
                explicit(false) S(S &&) noexcept;
                ~S();
                S *next;
            };
            """));

        var members = @class.Members;
        var delegating = (FunctionDefinition)members[1];
        Assert.IsNull(delegating.Specifiers);
        Assert.HasCount(1, delegating.Initializers);

        var initializers = ((FunctionDefinition)members[2]).Initializers;
        Assert.HasCount(3, initializers);
        Assert.IsInstanceOfType<BracedInitializer>(initializers[0].Initializer);

        Assert.IsTrue(((FunctionDefinition)members[3]).IsDeleted);
        var tryBlock = (FunctionDefinition)members[4];
        Assert.HasCount(2, tryBlock.Initializers);
        Assert.HasCount(1, tryBlock.Handlers);

        Assert.IsInstanceOfType<ExplicitSpecifier>(((SimpleDeclaration)members[5]).Specifiers.Specifiers[0]);

        // The constructors do not hide the class: S *next; declares a pointer
        var next = (SimpleDeclaration)members[7];
        Assert.IsInstanceOfType<NamedTypeSpecifier>(next.Specifiers.Specifiers[0]);
        Assert.IsInstanceOfType<PointerDeclarator>(next.Declarators[0].Declarator);
    }

    [TestMethod]
    public void AnonymousMembersAndClassesInDeclarations_AreClassSpecifiers()
    {
        var declarations = Declarations("""
            struct Point { int x, y; } origin{ 0, 0 }, *pointer = &origin;
            typedef struct { int a; } Unnamed;
            struct Holder { union { int i; float f; }; struct { int x; } nested; };
            """);

        var point = (SimpleDeclaration)declarations[0];
        Assert.IsInstanceOfType<ClassSpecifier>(point.Specifiers.Specifiers[0]);
        Assert.HasCount(2, point.Declarators);

        Assert.IsNull(((ClassSpecifier)((SimpleDeclaration)declarations[1]).Specifiers.Specifiers[1]).Name);

        var members = Class(declarations[2]).Members.Cast<SimpleDeclaration>().ToList();
        Assert.IsEmpty(members[0].Declarators);
        Assert.AreEqual("union", ((ClassSpecifier)members[0].Specifiers.Specifiers[0]).Key);
    }

    [TestMethod]
    public void ClassMembers_AreKnownInAndOutsideTheClass()
    {
        // Inside the definition of S::f, Inner is a type and value a variable, as they are in S
        var kinds = StatementKinds("""
            struct S { struct Inner {}; static int value; void f(); };
            void S::f()
            {
                Inner * a;
                value * 2;
                S::Inner * b;
            }
            """);

        CollectionAssert.AreEqual(new[] { "declaration", "expression", "declaration" }, kinds);
    }

    // ========================================
    // Enumerations
    // ========================================

    [TestMethod]
    public void Enumerations_AreScopedOpaqueOrUnscoped()
    {
        var declarations = Declarations("""
            enum class Color : unsigned char { Red, Green = 2, Blue [[deprecated]], };
            enum struct Opaque : int;
            enum class Forward;
            enum { First, Second } value;
            enum Color color;
            """);

        var color = (EnumSpecifier)((SimpleDeclaration)declarations[0]).Specifiers.Specifiers[0];
        Assert.AreEqual("class", color.ScopedKey);
        Assert.IsNotNull(color.UnderlyingType);
        Assert.HasCount(3, color.Enumerators);
        Assert.IsNotNull(color.Enumerators[1].Value);
        Assert.HasCount(1, color.Enumerators[2].Attributes);
        Assert.IsTrue(color.HasTrailingComma);

        var opaque = (EnumSpecifier)((SimpleDeclaration)declarations[1]).Specifiers.Specifiers[0];
        Assert.AreEqual("struct", opaque.ScopedKey);
        Assert.IsNull(opaque.Enumerators);
        Assert.IsNull(((EnumSpecifier)((SimpleDeclaration)declarations[2]).Specifiers.Specifiers[0]).UnderlyingType);

        var unnamed = (SimpleDeclaration)declarations[3];
        Assert.IsNull(((EnumSpecifier)unnamed.Specifiers.Specifiers[0]).Name);
        Assert.HasCount(1, unnamed.Declarators);
        Assert.IsInstanceOfType<ElaboratedTypeSpecifier>(((SimpleDeclaration)declarations[4]).Specifiers.Specifiers[0]);
    }

    [TestMethod]
    public void Enumerators_AreValues()
    {
        // The enumerators of an unscoped enumeration are known in the enclosing scope, those of a scoped one
        // through the enumeration and after using enum
        var kinds = StatementKinds("""
            enum Unscoped { A };
            enum class Scoped { B };
            void f(int x)
            {
                A * x;
                Scoped::B * x;
                using enum Scoped;
                B * x;
            }
            """);

        CollectionAssert.AreEqual(new[] { "expression", "expression", "declaration", "expression" }, kinds);
    }

    // ========================================
    // Templates
    // ========================================

    [TestMethod]
    public void Templates_DeclareTemplatesSpecializationsAndInstantiations()
    {
        var declarations = Declarations("""
            template <typename T> requires true struct Box { T value; };
            template <> struct Box<void> {};
            template <typename T> struct Box<T *> {};
            template struct Box<int>;
            extern template struct Box<long>;
            template <typename T> Box(T) -> Box<T>;
            template <typename T> concept Small = sizeof(T) < 4;
            template <typename T> using Pointer = T *;
            template <typename T> constexpr T zero = T();
            template <typename T> template <typename U> void Box<T>::f(U) {}
            """);

        var box = (TemplateDeclaration)declarations[0];
        Assert.HasCount(1, box.Parameters);
        Assert.IsNotNull(box.RequiresClause);
        Assert.IsInstanceOfType<ClassSpecifier>(((SimpleDeclaration)box.Declaration).Specifiers.Specifiers[0]);

        var specialization = (TemplateDeclaration)declarations[1];
        Assert.IsEmpty(specialization.Parameters);
        Assert.IsInstanceOfType<TemplateIdName>(Class(specialization.Declaration).Name);

        Assert.IsFalse(((ExplicitInstantiation)declarations[3]).IsExtern);
        Assert.IsTrue(((ExplicitInstantiation)declarations[4]).IsExtern);

        var guide = (SimpleDeclaration)((TemplateDeclaration)declarations[5]).Declaration;
        Assert.IsNull(guide.Specifiers);
        Assert.IsNotNull(((FunctionDeclarator)guide.Declarators[0].Declarator).TrailingReturnType);

        var concept = (ConceptDefinition)((TemplateDeclaration)declarations[6]).Declaration;
        Assert.AreEqual("Small", concept.Name);
        Assert.IsInstanceOfType<BinaryExpression>(concept.Constraint);

        Assert.IsInstanceOfType<AliasDeclaration>(((TemplateDeclaration)declarations[7]).Declaration);
        Assert.IsInstanceOfType<TemplateDeclaration>(((TemplateDeclaration)declarations[9]).Declaration);
    }

    [TestMethod]
    public void DeclaredTemplates_TakeTemplateArguments()
    {
        // Known templates take arguments in expressions; a class template-id before '(' is a functional cast
        var unit = CppTestHelper.Parse("""
            template <typename T> struct Box { Box(T) {} };
            template <typename T> constexpr T zero = T();
            template <typename T> T maximum(T a, T b) { return a; }
            void f()
            {
                zero<int> + 1;
                maximum<long>(1, 2);
                Box<int>(1);
            }
            """);
        var body = ((FunctionDefinition)unit.Declarations[3]).Body.Statements.Cast<ExpressionStatement>().ToList();
        Assert.IsInstanceOfType<TemplateIdName>(((NameExpression)((BinaryExpression)body[0].Expression).Left).Name);
        Assert.IsInstanceOfType<CallExpression>(body[1].Expression);
        Assert.IsInstanceOfType<FunctionalCastExpression>(body[2].Expression);
    }

    [TestMethod]
    public void QualifiedTemplates_AreLookedUpInTheirNamespace()
    {
        var unit = CppTestHelper.Parse("""
            namespace ns { template <typename T> constexpr int size = sizeof(T); }
            int x = ns::size<int> + 1;
            """);

        var initializer = (EqualsInitializer)((SimpleDeclaration)unit.Declarations[1]).Declarators[0].Initializer;
        var sum = (BinaryExpression)initializer.Value;
        Assert.AreEqual("+", sum.Operator);
        Assert.IsInstanceOfType<TemplateIdName>(((QualifiedName)((NameExpression)sum.Left).Name).Name);
    }

    [TestMethod]
    public void AbstractParameterPacks_AreTheirTypesPacks()
    {
        var declarations = Declarations("""
            template <typename... Ts> void f(Ts...);
            template <typename... Ts> void g(Ts &&...);
            void h(int...);
            """);

        FunctionDeclarator Function(int index) =>
            (FunctionDeclarator)((SimpleDeclaration)(declarations[index] is TemplateDeclaration template ? template.Declaration : declarations[index])).Declarators[0].Declarator;

        Assert.IsFalse(Function(0).IsVariadic);
        Assert.IsInstanceOfType<PackDeclarator>(Function(0).Parameters[0].Declarator);
        Assert.IsFalse(Function(1).IsVariadic);
        Assert.IsInstanceOfType<PackDeclarator>(((ReferenceDeclarator)Function(1).Parameters[0].Declarator).Inner);
        Assert.IsTrue(Function(2).IsVariadic);
    }

    // ========================================
    // Modules
    // ========================================

    [TestMethod]
    public void Modules_DeclareFragmentsImportsAndExports()
    {
        var declarations = Declarations("""
            module;
            export module a.b:part [[deprecated]];
            import std.core;
            export import :other;
            import <vector>;
            import "local.h";
            export int f();
            export { int g(); }
            module :private;
            """);

        var global = (ModuleDeclaration)declarations[0];
        Assert.IsNull(global.Name);
        Assert.IsNull(global.Partition);

        var named = (ModuleDeclaration)declarations[1];
        Assert.IsTrue(named.IsExport);
        Assert.AreEqual("a.b", named.Name);
        Assert.AreEqual("part", named.Partition);
        Assert.HasCount(1, named.Attributes);

        Assert.AreEqual("std.core", ((ImportDeclaration)declarations[2]).Module);
        var partition = (ImportDeclaration)declarations[3];
        Assert.IsTrue(partition.IsExport);
        Assert.AreEqual("other", partition.Partition);
        Assert.AreEqual("<vector>", ((ImportDeclaration)declarations[4]).Header);
        Assert.AreEqual("\"local.h\"", ((ImportDeclaration)declarations[5]).Header);

        Assert.IsFalse(((ExportDeclaration)declarations[6]).HasBraces);
        Assert.IsTrue(((ExportDeclaration)declarations[7]).HasBraces);
        Assert.AreEqual("private", ((ModuleDeclaration)declarations[8]).Partition);
    }

    [TestMethod]
    public void ModuleAndImport_AreIdentifiersElsewhere()
    {
        var declarations = Declarations("""
            int module, import;
            void f() { module = import; }
            """);

        Assert.IsInstanceOfType<SimpleDeclaration>(declarations[0]);
        Assert.IsInstanceOfType<FunctionDefinition>(declarations[1]);
    }

    // ========================================
    // Attributes and GNU extensions
    // ========================================

    [TestMethod]
    public void Attributes_AppearOnNamesParametersAndClasses()
    {
        var declarations = Declarations("""
            alignas(8) int aligned;
            struct alignas(16) Aligned {};
            template <typename... T> struct alignas(T...) Packed {};
            int f([[maybe_unused]] int a, int b [[maybe_unused]]);
            """);

        var alignas = (AlignasSpecifier)((SimpleDeclaration)declarations[0]).Attributes[0];
        Assert.IsInstanceOfType<Expression>(alignas.Operand);
        Assert.IsInstanceOfType<AlignasSpecifier>(Class(declarations[1]).Attributes[0]);
        var pack = (AlignasSpecifier)Class(((TemplateDeclaration)declarations[2]).Declaration).Attributes[0];
        Assert.IsInstanceOfType<TypeId>(pack.Operand);
        Assert.IsTrue(pack.IsPackExpansion);

        var parameters = ((FunctionDeclarator)((SimpleDeclaration)declarations[3]).Declarators[0].Declarator).Parameters;
        Assert.HasCount(1, parameters[0].Attributes);
        Assert.HasCount(1, ((NameDeclarator)parameters[1].Declarator).Attributes);
    }

    [TestMethod]
    public void GnuExtensions_AreKept()
    {
        var declarations = Declarations("""
            __attribute__((noinline)) void f();
            static __attribute__((unused)) int a, __attribute__((unused)) b __attribute__((aligned(4)));
            int renamed asm("symbol");
            int *__restrict p;
            __asm__ volatile("" ::: "memory");
            void g() throw();
            """);

        var gnu = (GnuAttributeSpecifier)((SimpleDeclaration)declarations[0]).Attributes[0];
        Assert.AreEqual("__attribute__", gnu.Keyword);
        Assert.AreEqual("noinline", gnu.Arguments);

        var variables = (SimpleDeclaration)declarations[1];
        Assert.IsInstanceOfType<AttributeDeclSpecifier>(variables.Specifiers.Specifiers[1]);
        Assert.HasCount(1, variables.Declarators[1].LeadingAttributes);
        Assert.AreEqual("aligned(4)", ((GnuAttributeSpecifier)variables.Declarators[1].Attributes[0]).Arguments);

        Assert.AreEqual("\"symbol\"", ((SimpleDeclaration)declarations[2]).Declarators[0].AsmLabel);
        CollectionAssert.AreEqual(new[] { "__restrict" }, ((PointerDeclarator)((SimpleDeclaration)declarations[3]).Declarators[0].Declarator).Qualifiers.ToArray());

        var asm = (AsmDeclaration)declarations[4];
        Assert.AreEqual("__asm__", asm.Keyword);
        CollectionAssert.AreEqual(new[] { "volatile" }, asm.Qualifiers.ToArray());
        Assert.AreEqual("\"\" ::: \"memory\"", asm.Text);

        Assert.IsTrue(((FunctionDeclarator)((SimpleDeclaration)declarations[5]).Declarators[0].Declarator).Noexcept.IsThrow);
    }

    // ========================================
    // Real code: complete classes, friends and aliases
    // ========================================

    [TestMethod]
    public void MemberFunctionBodies_SeeTheMembersDeclaredAfterThem()
    {
        // buffer is a template of a header, and a member declared below the function: buffer[i] = c; assigns
        var options = new CppParseOptions(templateNames: ["buffer"]);
        Assert.IsTrue(CppParser.TryParse("""
            struct Escape
            {
                void set(int i, char c) { buffer[i] = c; }
                struct Inner { int g() { return size * 2; } };
                char buffer[8];
                static const int size = 8;
            };
            """, options, out var unit, out var error), error?.Message);

        var escape = Class(unit.Declarations[0]);
        var set = (FunctionDefinition)escape.Members[0];
        Assert.IsInstanceOfType<ExpressionStatement>(set.Body.Statements[0]);
        var inner = Class(escape.Members[1]);
        Assert.IsNotNull(((FunctionDefinition)inner.Members[0]).Body);
    }

    [TestMethod]
    public void MemberFunctionBodies_SeeTheTemplateParametersOfMemberTemplates()
    {
        var escape = Class(Declarations("""
            struct S
            {
                template <class T> T get() { T *p = nullptr; return *p; }
                S() : value(0) { value = 1; }
                int value;
            };
            """)[0]);

        var get = (FunctionDefinition)((TemplateDeclaration)escape.Members[0]).Declaration;
        Assert.IsInstanceOfType<DeclarationStatement>(get.Body.Statements[0]);
        var constructor = (FunctionDefinition)escape.Members[1];
        Assert.HasCount(1, constructor.Initializers);
        Assert.IsInstanceOfType<ExpressionStatement>(constructor.Body.Statements[0]);
    }

    [TestMethod]
    public void AnErrorInAMemberFunctionBody_IsReportedWhereItIs()
    {
        Assert.IsFalse(CppParser.TryParse("struct S {\n  void f() { return +; }\n  int x;\n};\n", out _, out var error));
        Assert.AreEqual((2, 22), (error.Line, error.Column), error.Message);
    }

    [TestMethod]
    public void FriendTypes_NeedNoClassKey()
    {
        var template = (TemplateDeclaration)Declarations("""
            class Other;
            template <class T> struct Traits { using base = int; };
            template <class T> class A { friend Other; friend typename Traits<T>::base; friend A<int>; };
            """)[2];

        var members = Class(template.Declaration).Members;
        Assert.HasCount(3, members);
        Assert.IsTrue(members.All(m => m is SimpleDeclaration { Declarators.Count: 0 }));
    }

    [TestMethod]
    public void TypeAliases_OfClasses_QualifyNames()
    {
        // json::pointer is a type: the declaration declares a function
        var declarations = Declarations("""
            template <class T = void> class basic;
            using json = basic<>;
            template <class T> class basic { public: using pointer = int *; };
            void f() { json::pointer p(json::pointer(0)); }
            """);

        var function = (FunctionDefinition)declarations[3];
        var declaration = (SimpleDeclaration)((DeclarationStatement)function.Body.Statements[0]).Declaration;
        Assert.IsInstanceOfType<ParenthesizedInitializer>(declaration.Declarators[0].Initializer);
        var cast = ((ParenthesizedInitializer)declaration.Declarators[0].Initializer).Arguments[0];
        Assert.IsInstanceOfType<FunctionalCastExpression>(cast);
    }

    [TestMethod]
    public void NamesOfHeaders_QualifiedAndMembers()
    {
        // std::system_error is a class, fmt's system_error() a function template; indent is a type and a member of
        // raw_ostream, whose member functions see it; the injected name of a base template takes arguments
        var options = new CppParseOptions(
            typeNames: ["std::system_error", "indent"],
            templateNames: ["formatter"],
            functionTemplateNames: ["system_error"],
            classMembers: new Dictionary<string, IReadOnlyCollection<string>> { ["raw_ostream"] = ["indent"], ["formatter"] = ["format"] });
        Assert.IsTrue(CppParser.TryParse("""
            void f(int e) { throw std::system_error(e); }
            void g(int e) { throw system_error(e); }
            void raw_ostream::write() { indent(2); }
            template <class T> struct F : formatter<T> { int format(int x) { return formatter<T>::format(x); } };
            """, options, out var unit, out var error), error?.Message);

        Expression Thrown(int function) => ((ThrowExpression)((ExpressionStatement)((FunctionDefinition)unit.Declarations[function]).Body.Statements[0]).Expression).Operand;
        Assert.IsInstanceOfType<FunctionalCastExpression>(Thrown(0));
        Assert.IsInstanceOfType<CallExpression>(Thrown(1));
        var write = (FunctionDefinition)unit.Declarations[2];
        Assert.IsInstanceOfType<CallExpression>(((ExpressionStatement)write.Body.Statements[0]).Expression);
        var format = (FunctionDefinition)Class(((TemplateDeclaration)unit.Declarations[3]).Declaration).Members[0];
        var call = (CallExpression)((ReturnStatement)format.Body.Statements[0]).Expression;
        Assert.IsInstanceOfType<QualifiedName>(((NameExpression)call.Callee).Name);
    }

    [TestMethod]
    public void LocalDeclarations_WithUnknownArgumentsInitializeVariables()
    {
        // lock(mutex) with an unknown mutex initializes a variable; with a type it declares a function
        var kinds = Declarations("""
            typedef int T;
            void f()
            {
                std::lock_guard<std::mutex> lock(mutex);
                std::string text(source());
                int g(T);
            }
            """);

        var body = ((FunctionDefinition)kinds[1]).Body.Statements;
        InitDeclarator Declarator(int i) => ((SimpleDeclaration)((DeclarationStatement)body[i]).Declaration).Declarators[0];
        Assert.IsInstanceOfType<ParenthesizedInitializer>(Declarator(0).Initializer);
        Assert.IsInstanceOfType<ParenthesizedInitializer>(Declarator(1).Initializer);
        Assert.IsInstanceOfType<FunctionDeclarator>(Declarator(2).Declarator);
    }

    [TestMethod]
    public void UnknownNamesThatDeclare_AreTypesInTheRestOfTheFile()
    {
        // StringRef declares s, so StringRef(s) is a functional cast; SmallVector<int> v makes SmallVector a template
        var function = (FunctionDefinition)Declarations("""
            void f(StringRef s)
            {
                auto t = StringRef(s);
                SmallVector<int> v;
                auto w = SmallVector<int>(v);
                g(h);
            }
            """)[0];

        Expression Initializer(int i) => ((EqualsInitializer)((SimpleDeclaration)((DeclarationStatement)function.Body.Statements[i]).Declaration).Declarators[0].Initializer).Value;
        Assert.IsInstanceOfType<FunctionalCastExpression>(Initializer(0));
        Assert.IsInstanceOfType<FunctionalCastExpression>(Initializer(2));
        Assert.IsInstanceOfType<ExpressionStatement>(function.Body.Statements[3]);
    }

    [TestMethod]
    public void GnuExtensions_OfRealCode()
    {
        var declarations = Declarations("""
            __extension__ typedef long long wide;
            struct S { S(const char *s) __attribute__((enable_if(s != 0, "null"))) : p(s) {} const char *p; };
            void f() __attribute__((cold)) {}
            template <int N> using bits = _BitInt(N);
            """);

        Assert.HasCount(1, Class(declarations[1]).Members.OfType<FunctionDefinition>().Single().DeclaratorAttributes);
        Assert.HasCount(1, ((FunctionDefinition)declarations[2]).DeclaratorAttributes);
        var alias = (AliasDeclaration)((TemplateDeclaration)declarations[3]).Declaration;
        Assert.IsInstanceOfType<BitIntSpecifier>(alias.Type.Specifiers.Specifiers[0]);
    }

    // ========================================
    // Writer and oracle
    // ========================================

    [TestMethod]
    public void WrittenDeclarations_ParseTheSame()
    {
        const string source = """
            module;
            export module m;
            namespace a::inline b { int x; }
            namespace alias = a;
            using namespace a;
            using a::x;
            extern "C" { int c(int); }
            template <typename T, typename... Ts> requires true
            struct [[nodiscard]] S final : public virtual T, Ts...
            {
            public:
                enum class E : int { A, B = 2, };
                using Alias [[deprecated]] = T;
                int bits : 3 = 1, : 0;
                S() try : T(), Ts()... {} catch (...) {}
                virtual void f() const override = 0;
                explicit(true) operator bool() const = delete;
                template <typename U> friend struct Other;
            };
            template <> struct S<int> {};
            template struct S<long>;
            template <typename T> concept C = sizeof(T) > 1;
            export { alignas(8) int aligned [[maybe_unused]]; }
            static __attribute__((unused)) int g, __attribute__((unused)) h asm("h") __attribute__((aligned(4)));
            asm("nop");
            void t() throw();
            module :private;
            """;
        var written = Write(CppTestHelper.Parse(source));
        Assert.AreEqual(written, Write(CppTestHelper.Parse(written)), written);
    }

    [TestMethod]
    public void Oracle_ClassesAndTemplates()
    {
        CppTestHelper.AssertOracle("""
            namespace shapes
            {
                struct Shape { virtual ~Shape() = default; virtual int area() const = 0; };

                template <typename T>
                struct Square final : Shape
                {
                    T side;
                    explicit Square(T s) : side(s) {}
                    int area() const override { return static_cast<int>(side * side); }
                    template <typename U> U as() const;
                };

                template <typename T> template <typename U> U Square<T>::as() const { return U(side); }
                template <typename T> Square(T) -> Square<T>;
            }

            using shapes::Square;
            int use() { Square square(2); return square.area() + square.as<int>(); }
            """);
    }

    [TestMethod]
    [DataRow("namespace a::b::inline c { int x; }\nnamespace n = a::b;\nint y = n::x + a::b::c::x;")]
    [DataRow("struct S { int a : 3 = 1; unsigned : 0; int b[2], d; static const int c = 1; };\nint S::* member = &S::d;")]
    [DataRow("enum class E : char { A = 'a', B [[deprecated]], };\nenum class E : char;\nenum U { X };\nint f(E e) { using enum E; return e == A ? X : 0; }")]
    [DataRow("template <typename... Ts> void f(Ts...);\ntemplate <typename... Ts> void g(Ts &&...) {}\nvoid h(int, ...);\nvoid use() { f(1, 2); g(3); h(4, 5); }")]
    [DataRow("extern \"C\" int c(int);\nextern \"C\" { int d; }\nextern \"C++\" typedef int (*F)(int);")]
    [DataRow("struct B { B(int) {} virtual void f() = 0; };\nstruct D : B { D() try : B(1) {} catch (...) {} void f() final {} };")]
    [DataRow("static __attribute__((unused)) int a, __attribute__((unused)) b;\nint c __attribute__((aligned(8))) = 1;\nint d asm(\"d_symbol\");\nasm(\"nop\");\nvoid f() { __asm__ volatile(\"\" ::: \"memory\"); }")]
    [DataRow("template <class T> struct A { struct B; static int x; void f(); };\ntemplate <class T> struct A<T>::B { T y; };\ntemplate <class T> int A<T>::x = 1;\ntemplate <class T> void A<T>::f() {}\ntemplate <> void A<int>::f() {}")]
    [DataRow("struct E { void set(int i) { buffer[i] = 'a'; } template <class T> T get() const { return T(buffer[0]); } char buffer[4]; };")]
    [DataRow("class F;\ntemplate <class T> class G { friend F; friend G<int>; };\nstruct R { R &operator=(int) = delete; template <class T> R &operator=(T) = delete; };\ntemplate <class T> void h(T) = delete;")]
    [DataRow("template <int N> using bits = _BitInt(N);\n__extension__ typedef long long wide;\nstruct S { S(const char *s) __attribute__((enable_if(s != 0, \"null\"))) : p(s) {} const char *p; };")]
    [DataRow("template <class T = void> class basic;\nusing json = basic<>;\ntemplate <class T> class basic { public: using pointer = int; basic() = default; };\njson::pointer f() { return json::pointer(1); }")]
    public void Oracle_Snippets(string source)
    {
        CppTestHelper.AssertOracle(source);
    }
}
