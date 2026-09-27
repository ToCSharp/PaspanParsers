namespace PaspanParsers.Cpp;

// ========================================
// Base Types
// ========================================

/// <summary>
/// Base interface for all C++ AST nodes.
/// </summary>
public interface ICppNode
{
    /// <summary>
    /// The position of the node in the parsed input; see <see cref="TextSpan"/>.
    /// </summary>
    TextSpan Span { get; }
}

/// <summary>
/// Base class of the C++ AST nodes.
/// </summary>
public abstract class CppNode : ICppNode
{
    /// <summary>
    /// The position of the node in the parsed input, from its first token to the end of its last token,
    /// without the trivia around them. The parser sets it; nodes built in code have an empty span at 0.
    /// </summary>
    public TextSpan Span { get; set; }

    /// <summary>
    /// The directives in the trivia before the first token of the node that <see cref="CppWriter"/> writes
    /// back: all but the conditional ones (see <see cref="PreprocessorDirective.IsConditional"/>), whose
    /// inactive branches are not in the tree. Every node that starts at the same token has the same list;
    /// the writer writes each directive once. Null when there are none.
    /// </summary>
    public IReadOnlyList<PreprocessorDirective> LeadingDirectives { get; set; }
}

// ========================================
// Translation Unit
// ========================================

/// <summary>
/// A source file: its declarations in source order.
/// </summary>
public sealed class TranslationUnit(IReadOnlyList<Declaration> declarations) : CppNode
{
    public IReadOnlyList<Declaration> Declarations { get; } = declarations ?? [];

    /// <summary>
    /// Every directive the preprocessor processed, in source order: the directives of active code and the
    /// conditional directives that end inactive branches. Directives inside inactive branches are not included.
    /// </summary>
    public IReadOnlyList<PreprocessorDirective> Directives { get; init; } = [];

    /// <summary>
    /// The directives after the last declaration that the writer writes back, or null.
    /// </summary>
    public IReadOnlyList<PreprocessorDirective> EndDirectives { get; init; }
}

// ========================================
// Preprocessor
// ========================================

public enum PreprocessorDirectiveKind
{
    /// <summary>The null directive: a line with only <c>#</c>.</summary>
    Null,
    If,
    Ifdef,
    Ifndef,
    Elif,
    Elifdef,
    Elifndef,
    Else,
    Endif,
    Define,
    Undef,
    Include,
    IncludeNext,
    Import,
    Embed,
    /// <summary><c>#line 10 "file"</c>, or the line marker <c># 10 "file"</c>.</summary>
    Line,
    Pragma,
    Error,
    Warning,
    Ident,
    /// <summary>Any other directive, such as <c>#assert</c>.</summary>
    Other,
}

/// <summary>
/// A preprocessing directive: from the '#' to the end of its last token, without the comment after it.
/// The parser does not expand macros: directives are trivia between the tokens, <c>#define</c> and
/// <c>#undef</c> only change the macros used by conditional directives, and <c>#include</c> does not
/// read the file.
/// </summary>
public sealed class PreprocessorDirective(PreprocessorDirectiveKind kind, string name, string arguments, string text) : CppNode
{
    public PreprocessorDirectiveKind Kind { get; } = kind;

    /// <summary>The name after '#', like <c>include</c>; empty for the null directive and a line marker.</summary>
    public string Name { get; } = name;

    /// <summary>
    /// The tokens after the name, separated by single spaces where the source has white space, comments or
    /// line splices between them: <c>&lt;cstddef&gt;</c>, <c>VERSION &gt;= 3</c>.
    /// </summary>
    public string Arguments { get; } = arguments;

    /// <summary>The directive as written in the source, which <see cref="CppWriter"/> writes back.</summary>
    public string Text { get; } = text;

    /// <summary>
    /// For conditional directives other than <c>#endif</c>: the group after the directive is compiled.
    /// </summary>
    public bool IsBranchTaken { get; init; }

    /// <summary>The macro of a <c>#define</c>, otherwise null.</summary>
    public MacroDefinition Macro { get; init; }

    /// <summary>
    /// <c>#if</c>, <c>#ifdef</c>, <c>#ifndef</c>, <c>#elif</c>, <c>#elifdef</c>, <c>#elifndef</c>, <c>#else</c> or <c>#endif</c>.
    /// </summary>
    public bool IsConditional => Kind is >= PreprocessorDirectiveKind.If and <= PreprocessorDirectiveKind.Endif;
}

/// <summary>
/// The macro defined by <c>#define</c>. <see cref="Parameters"/> is null for an object-like macro; a
/// variadic macro has <see cref="IsVariadic"/> and its parameters do not include <c>...</c> (a named
/// variadic parameter, <c>args...</c>, is the last one). <see cref="Replacement"/> is the replacement list,
/// written like <see cref="PreprocessorDirective.Arguments"/>.
/// </summary>
public sealed class MacroDefinition(string name, IReadOnlyList<string> parameters, bool isVariadic, string replacement)
{
    public string Name { get; } = name;
    public IReadOnlyList<string> Parameters { get; } = parameters;
    public bool IsVariadic { get; } = isVariadic;
    public string Replacement { get; } = replacement;

    public bool IsFunctionLike => Parameters != null;
}

// ========================================
// Declarations
// ========================================

public abstract class Declaration : CppNode
{
}

/// <summary>
/// A declaration of variables, functions or types: <c>static int a = 1, *b;</c>, <c>int f(int);</c>,
/// <c>typedef int Integer;</c> or, without declarators, <c>struct Point;</c>. The span includes the
/// attributes before it and the ';'. The specifiers are null for a constructor, destructor or conversion
/// function, which have none.
/// </summary>
public sealed class SimpleDeclaration(DeclSpecifierSequence specifiers, IReadOnlyList<InitDeclarator> declarators) : Declaration
{
    /// <summary>The attributes before the specifiers: <c>[[maybe_unused]] int a;</c>.</summary>
    public IReadOnlyList<AttributeSpecifier> Attributes { get; init; } = [];

    public DeclSpecifierSequence Specifiers { get; } = specifiers;
    public IReadOnlyList<InitDeclarator> Declarators { get; } = declarators ?? [];
}

/// <summary>
/// A function with its body: <c>int main() { return 0; }</c>. The specifiers are null for a constructor,
/// destructor or conversion function, which have none: <c>S::~S() { }</c>.
/// </summary>
public sealed class FunctionDefinition(DeclSpecifierSequence specifiers, Declarator declarator, CompoundStatement body) : Declaration
{
    /// <summary>The attributes before the specifiers: <c>[[nodiscard]] int f() { … }</c>.</summary>
    public IReadOnlyList<AttributeSpecifier> Attributes { get; init; } = [];

    public DeclSpecifierSequence Specifiers { get; } = specifiers;
    public Declarator Declarator { get; } = declarator;

    /// <summary>The constraint after the declarator: <c>requires C&lt;T&gt;</c>, or null.</summary>
    public Expression RequiresClause { get; init; }

    public CompoundStatement Body { get; } = body;
}

/// <summary>
/// <c>static_assert(condition, message);</c>; the message is null in <c>static_assert(condition);</c>.
/// </summary>
public sealed class StaticAssertDeclaration(Expression condition, Expression message = null) : Declaration
{
    public Expression Condition { get; } = condition;
    public Expression Message { get; } = message;
}

// ========================================
// Declaration Specifiers
// ========================================

/// <summary>
/// The declaration specifiers before the declarators, in source order: <c>static const unsigned int</c>.
/// </summary>
public sealed class DeclSpecifierSequence(IReadOnlyList<DeclSpecifier> specifiers) : CppNode
{
    public IReadOnlyList<DeclSpecifier> Specifiers { get; } = specifiers ?? [];
}

public abstract class DeclSpecifier : CppNode
{
}

/// <summary>
/// A declaration specifier that is a keyword: a fundamental type (<c>int</c>, <c>unsigned</c>, <c>auto</c>),
/// a cv-qualifier, a storage class, a function specifier or <c>typedef</c>. The GNU types <c>__int128</c>,
/// <c>__float128</c>, <c>_Float16</c> and <c>__bf16</c> are keyword specifiers too.
/// </summary>
public sealed class KeywordSpecifier(string keyword) : DeclSpecifier
{
    public string Keyword { get; } = keyword;
}

/// <summary>
/// A type named by a name: <c>Integer</c>, <c>std::size_t</c>, <c>vector&lt;int&gt;</c>, or with
/// <see cref="IsTypename"/>, <c>typename T::value_type</c>.
/// </summary>
public sealed class NamedTypeSpecifier(Name name) : DeclSpecifier
{
    public Name Name { get; } = name;
    public bool IsTypename { get; init; }
}

/// <summary>
/// An elaborated type specifier: <c>struct Point</c>; <see cref="Key"/> is <c>class</c>, <c>struct</c>,
/// <c>union</c> or <c>enum</c>.
/// </summary>
public sealed class ElaboratedTypeSpecifier(string key, Name name) : DeclSpecifier
{
    public string Key { get; } = key;
    public Name Name { get; } = name;
}

/// <summary>
/// <c>decltype(expression)</c>, or <c>decltype(auto)</c> when <see cref="Expression"/> is null.
/// </summary>
public sealed class DecltypeSpecifier(Expression expression) : DeclSpecifier
{
    public Expression Expression { get; } = expression;
}

/// <summary>
/// A placeholder constrained by a concept: <c>std::integral auto</c>, or <c>C decltype(auto)</c> with
/// <see cref="IsDecltypeAuto"/>. An unconstrained <c>auto</c> is a <see cref="KeywordSpecifier"/>.
/// </summary>
public sealed class PlaceholderTypeSpecifier(Name concept, bool isDecltypeAuto = false) : DeclSpecifier
{
    public Name Concept { get; } = concept;
    public bool IsDecltypeAuto { get; } = isDecltypeAuto;
}

// ========================================
// Names
// ========================================

/// <summary>
/// A name, qualified or not, as in id-expressions, type names and declarator ids. <see cref="ToString"/>
/// spells it without white space.
/// </summary>
public abstract class Name : CppNode
{
}

/// <summary>
/// An identifier: <c>a</c>.
/// </summary>
public sealed class IdentifierName(string identifier) : Name
{
    public string Identifier { get; } = identifier;

    public override string ToString() => Identifier;
}

/// <summary>
/// A template-id: <c>vector&lt;int&gt;</c>. Each argument is a <see cref="TypeId"/> or an <see cref="Expression"/>;
/// the template is an <see cref="IdentifierName"/>, <see cref="OperatorFunctionName"/> or <see cref="LiteralOperatorName"/>.
/// </summary>
public sealed class TemplateIdName(Name template, IReadOnlyList<CppNode> arguments) : Name
{
    public Name Template { get; } = template;
    public IReadOnlyList<CppNode> Arguments { get; } = arguments ?? [];

    public override string ToString() => $"{Template}<{string.Join(",", Arguments)}>";
}

/// <summary>
/// An operator function: <c>operator+</c>, <c>operator()</c>, <c>operator new[]</c>; <see cref="Operator"/> is
/// the text after <c>operator</c>, like <c>+</c>, <c>()</c> or <c>new[]</c>.
/// </summary>
public sealed class OperatorFunctionName(string @operator) : Name
{
    public string Operator { get; } = @operator;

    public override string ToString() => "operator" + (char.IsLetter(Operator[0]) ? " " : "") + Operator;
}

/// <summary>
/// A conversion function: <c>operator int*</c>.
/// </summary>
public sealed class ConversionFunctionName(TypeId type) : Name
{
    public TypeId Type { get; } = type;

    public override string ToString() => $"operator {Type}";
}

/// <summary>
/// A literal operator: <c>operator""_km</c>; <see cref="Suffix"/> is <c>_km</c>.
/// </summary>
public sealed class LiteralOperatorName(string suffix) : Name
{
    public string Suffix { get; } = suffix;

    public override string ToString() => "operator\"\"" + Suffix;
}

/// <summary>
/// A destructor: <c>~T</c>; the type is an <see cref="IdentifierName"/>, a <see cref="TemplateIdName"/> or a
/// <see cref="DecltypeName"/>.
/// </summary>
public sealed class DestructorName(Name type) : Name
{
    public Name Type { get; } = type;

    public override string ToString() => "~" + Type;
}

/// <summary>
/// <c>decltype(expression)</c> as the qualifier of a qualified name: <c>decltype(x)::type</c>.
/// </summary>
public sealed class DecltypeName(Expression expression) : Name
{
    public Expression Expression { get; } = expression;

    public override string ToString() => "decltype(...)";
}

/// <summary>
/// A qualified name: <c>std::size_t</c>. The qualifier is the nested name specifier without its final
/// <c>::</c>; it is null for a name in the global namespace, <c>::size_t</c>. <c>a::b::c</c> is
/// <c>(a::b)::c</c>. <see cref="IsTemplate"/> is set when <c>template</c> comes before the name:
/// <c>T::template apply&lt;int&gt;</c>.
/// </summary>
public sealed class QualifiedName(Name qualifier, Name name) : Name
{
    public Name Qualifier { get; } = qualifier;
    public Name Name { get; } = name;
    public bool IsTemplate { get; init; }

    public override string ToString() => $"{Qualifier}::{(IsTemplate ? "template " : "")}{Name}";
}

// ========================================
// Types
// ========================================

/// <summary>
/// A type-id: the type specifiers and an optional abstract declarator, as in <c>int (*)(int)</c>.
/// <see cref="IsPackExpansion"/> is set for a template argument followed by <c>...</c>.
/// </summary>
public sealed class TypeId(DeclSpecifierSequence specifiers, Declarator declarator = null) : CppNode
{
    public DeclSpecifierSequence Specifiers { get; } = specifiers;
    public Declarator Declarator { get; } = declarator;
    public bool IsPackExpansion { get; init; }
}

// ========================================
// Declarators
// ========================================

/// <summary>
/// A declarator and its optional initializer: <c>a = 1</c> in <c>int a = 1;</c>.
/// </summary>
public sealed class InitDeclarator(Declarator declarator, Initializer initializer = null) : CppNode
{
    public Declarator Declarator { get; } = declarator;

    /// <summary>The constraint after the declarator: <c>requires C&lt;T&gt;</c>, or null.</summary>
    public Expression RequiresClause { get; init; }

    public Initializer Initializer { get; } = initializer;
}

/// <summary>
/// A declarator, nested as in the grammar ([dcl.decl]): <c>*a[3]</c> is a <see cref="PointerDeclarator"/>
/// around the <see cref="ArrayDeclarator"/> <c>a[3]</c>, and <c>(*f)(int)</c> is a
/// <see cref="FunctionDeclarator"/> around the <see cref="ParenthesizedDeclarator"/> <c>(*f)</c>. In an abstract
/// declarator (<c>int (*)(int)</c>) the innermost <c>Inner</c> is null.
/// </summary>
public abstract class Declarator : CppNode
{
}

/// <summary>
/// The declared name: <c>a</c> in <c>int a;</c>, <c>S::method</c>, <c>operator+</c>.
/// </summary>
public sealed class NameDeclarator(Name name) : Declarator
{
    public Name Name { get; } = name;
}

/// <summary>
/// The names of a structured binding declaration: <c>[a, b]</c> in <c>auto &amp;[a, b] = pair;</c>.
/// </summary>
public sealed class StructuredBindingDeclarator(IReadOnlyList<IdentifierName> names) : Declarator
{
    public IReadOnlyList<IdentifierName> Names { get; } = names ?? [];
}

/// <summary>
/// A parameter pack: <c>... args</c>; the inner declarator is null in an abstract declarator.
/// </summary>
public sealed class PackDeclarator(Declarator inner) : Declarator
{
    public Declarator Inner { get; } = inner;
}

/// <summary>
/// <c>* const inner</c>.
/// </summary>
public sealed class PointerDeclarator(Declarator inner, IReadOnlyList<string> qualifiers = null) : Declarator
{
    public Declarator Inner { get; } = inner;

    /// <summary>The cv-qualifiers after '*', in source order.</summary>
    public IReadOnlyList<string> Qualifiers { get; } = qualifiers ?? [];
}

/// <summary>
/// <c>&amp; inner</c>, or <c>&amp;&amp; inner</c> when <see cref="IsRvalue"/>.
/// </summary>
public sealed class ReferenceDeclarator(Declarator inner, bool isRvalue = false) : Declarator
{
    public Declarator Inner { get; } = inner;
    public bool IsRvalue { get; } = isRvalue;
}

/// <summary>
/// A pointer to member: <c>S::* const inner</c>; <see cref="Class"/> is the name before <c>::*</c>.
/// </summary>
public sealed class MemberPointerDeclarator(Name @class, Declarator inner, IReadOnlyList<string> qualifiers = null) : Declarator
{
    public Name Class { get; } = @class;
    public Declarator Inner { get; } = inner;
    public IReadOnlyList<string> Qualifiers { get; } = qualifiers ?? [];
}

/// <summary>
/// <c>inner[size]</c>; the size is null in <c>inner[]</c>.
/// </summary>
public sealed class ArrayDeclarator(Declarator inner, Expression size = null) : Declarator
{
    public Declarator Inner { get; } = inner;
    public Expression Size { get; } = size;
}

/// <summary>
/// A function declarator: <c>f(int a, char b) const &amp; noexcept -> int</c>.
/// </summary>
public sealed class FunctionDeclarator(Declarator inner, IReadOnlyList<ParameterDeclaration> parameters) : Declarator
{
    public Declarator Inner { get; } = inner;

    /// <summary>
    /// The parameters; <c>(void)</c> is one parameter with the specifier <c>void</c>, as written.
    /// </summary>
    public IReadOnlyList<ParameterDeclaration> Parameters { get; } = parameters ?? [];

    /// <summary>The parameters end with <c>...</c>: <c>(int, ...)</c>, <c>(int...)</c> or <c>(...)</c>.</summary>
    public bool IsVariadic { get; init; }

    /// <summary>The cv-qualifiers after the parameters, in source order.</summary>
    public IReadOnlyList<string> Qualifiers { get; init; } = [];

    /// <summary><c>&amp;</c>, <c>&amp;&amp;</c> or null.</summary>
    public string RefQualifier { get; init; }

    public NoexceptSpecifier Noexcept { get; init; }

    /// <summary>The type after <c>-&gt;</c>, or null.</summary>
    public TypeId TrailingReturnType { get; init; }
}

/// <summary>
/// <c>( inner )</c>.
/// </summary>
public sealed class ParenthesizedDeclarator(Declarator inner) : Declarator
{
    public Declarator Inner { get; } = inner;
}

/// <summary>
/// <c>noexcept</c>, or <c>noexcept(condition)</c>.
/// </summary>
public sealed class NoexceptSpecifier(Expression condition = null) : CppNode
{
    public Expression Condition { get; } = condition;
}

/// <summary>
/// A function parameter: <c>int a = 0</c>; the declarator is null for an unnamed parameter.
/// </summary>
public sealed class ParameterDeclaration(DeclSpecifierSequence specifiers, Declarator declarator = null, Expression defaultValue = null) : CppNode
{
    public DeclSpecifierSequence Specifiers { get; } = specifiers;
    public Declarator Declarator { get; } = declarator;
    public Expression DefaultValue { get; } = defaultValue;
}

// ========================================
// Initializers
// ========================================

public abstract class Initializer : CppNode
{
}

/// <summary>
/// <c>= expression</c>, or <c>= { list }</c> when <see cref="Value"/> is an <see cref="InitializerListExpression"/>.
/// </summary>
public sealed class EqualsInitializer(Expression value) : Initializer
{
    public Expression Value { get; } = value;
}

/// <summary>
/// Direct initialization with parentheses: <c>(1, 2)</c> in <c>int a(1);</c>, <c>new int(5)</c>, <c>T(x)</c>.
/// An argument is an expression, an <see cref="InitializerListExpression"/> or a <see cref="PackExpansionExpression"/>.
/// </summary>
public sealed class ParenthesizedInitializer(IReadOnlyList<Expression> arguments) : Initializer
{
    public IReadOnlyList<Expression> Arguments { get; } = arguments ?? [];
}

/// <summary>
/// List initialization: <c>{ 1, 2 }</c> in <c>int a{ 1 };</c>, <c>new int[2]{ 1, 2 }</c>, <c>T{ x }</c>. The
/// initializer has the span of its list.
/// </summary>
public sealed class BracedInitializer(InitializerListExpression list) : Initializer
{
    public InitializerListExpression List { get; } = list;
}

// ========================================
// Statements
// ========================================

public abstract class Statement : CppNode
{
}

/// <summary>
/// <c>{ statements }</c>.
/// </summary>
public sealed class CompoundStatement(IReadOnlyList<Statement> statements) : Statement
{
    public IReadOnlyList<Statement> Statements { get; } = statements ?? [];

    /// <summary>
    /// The directives before the closing brace that the writer writes back, or null.
    /// </summary>
    public IReadOnlyList<PreprocessorDirective> CloseBraceDirectives { get; init; }
}

/// <summary>
/// A declaration in a block: <c>int a = 1;</c>, <c>static_assert(sizeof(int) == 4);</c>.
/// </summary>
public sealed class DeclarationStatement(Declaration declaration) : Statement
{
    public Declaration Declaration { get; } = declaration;
}

/// <summary>
/// <c>expression;</c>, or the null statement <c>;</c> when <see cref="Expression"/> is null.
/// </summary>
public sealed class ExpressionStatement(Expression expression) : Statement
{
    public Expression Expression { get; } = expression;
}

/// <summary>
/// The declaration of a condition: <c>int k = next()</c> in <c>if (int k = next())</c>. The initializer is an
/// <see cref="EqualsInitializer"/> or a <see cref="BracedInitializer"/>.
/// </summary>
public sealed class ConditionDeclaration(DeclSpecifierSequence specifiers, Declarator declarator, Initializer initializer) : CppNode
{
    public IReadOnlyList<AttributeSpecifier> Attributes { get; init; } = [];
    public DeclSpecifierSequence Specifiers { get; } = specifiers;
    public Declarator Declarator { get; } = declarator;
    public Initializer Initializer { get; } = initializer;
}

/// <summary>
/// <c>if (init; condition) then else otherwise</c>, <c>if constexpr (condition) …</c> and
/// <c>if consteval { … }</c>, <c>if !consteval { … }</c>. The condition is an <see cref="Expression"/> or a
/// <see cref="ConditionDeclaration"/>, and null in <c>if consteval</c>. The else branch is null when there is none.
/// </summary>
public sealed class IfStatement(CppNode condition, Statement then, Statement @else = null) : Statement
{
    public bool IsConstexpr { get; init; }

    /// <summary><c>if consteval</c>, or <c>if !consteval</c> with <see cref="IsNegated"/>.</summary>
    public bool IsConsteval { get; init; }

    public bool IsNegated { get; init; }

    /// <summary>The init-statement before the condition: <c>int m = n * 2;</c> in <c>if (int m = n * 2; m &gt; 10)</c>, or null.</summary>
    public Statement InitStatement { get; init; }

    public CppNode Condition { get; } = condition;
    public Statement Then { get; } = then;
    public Statement Else { get; } = @else;
}

/// <summary>
/// <c>switch (init; condition) body</c>. The condition is an <see cref="Expression"/> or a <see cref="ConditionDeclaration"/>.
/// </summary>
public sealed class SwitchStatement(CppNode condition, Statement body) : Statement
{
    /// <summary>The init-statement before the condition, or null.</summary>
    public Statement InitStatement { get; init; }

    public CppNode Condition { get; } = condition;
    public Statement Body { get; } = body;
}

/// <summary>
/// <c>case value: statement</c>; <see cref="RangeEnd"/> is set in the GNU range <c>case 1 ... 3:</c>. The
/// statement is null for a label at the end of a block (C++23): <c>case 1: }</c>.
/// </summary>
public sealed class CaseStatement(Expression value, Statement statement) : Statement
{
    public Expression Value { get; } = value;
    public Expression RangeEnd { get; init; }
    public Statement Statement { get; } = statement;
}

/// <summary>
/// <c>default: statement</c>; the statement is null for a label at the end of a block (C++23).
/// </summary>
public sealed class DefaultStatement(Statement statement) : Statement
{
    public Statement Statement { get; } = statement;
}

/// <summary>
/// <c>label: statement</c>; the statement is null for a label at the end of a block (C++23): <c>end: }</c>.
/// Attributes of the label are in an <see cref="AttributedStatement"/> around it.
/// </summary>
public sealed class LabeledStatement(string label, Statement statement) : Statement
{
    public string Label { get; } = label;
    public Statement Statement { get; } = statement;
}

/// <summary>
/// <c>while (condition) body</c>. The condition is an <see cref="Expression"/> or a <see cref="ConditionDeclaration"/>.
/// </summary>
public sealed class WhileStatement(CppNode condition, Statement body) : Statement
{
    public CppNode Condition { get; } = condition;
    public Statement Body { get; } = body;
}

/// <summary>
/// <c>do body while (condition);</c>.
/// </summary>
public sealed class DoStatement(Statement body, Expression condition) : Statement
{
    public Statement Body { get; } = body;
    public Expression Condition { get; } = condition;
}

/// <summary>
/// <c>for (init condition; increment) body</c>. The init-statement is a <see cref="DeclarationStatement"/> or
/// an <see cref="ExpressionStatement"/> and includes its ';'; it is null in <c>for (;;)</c>. The condition is an
/// <see cref="Expression"/>, a <see cref="ConditionDeclaration"/> or null; the increment may be null.
/// </summary>
public sealed class ForStatement(Statement initStatement, CppNode condition, Expression increment, Statement body) : Statement
{
    public Statement InitStatement { get; } = initStatement;
    public CppNode Condition { get; } = condition;
    public Expression Increment { get; } = increment;
    public Statement Body { get; } = body;
}

/// <summary>
/// The range-based for statement: <c>for (init; declaration : range) body</c>. The range is an expression or
/// a braced-init-list.
/// </summary>
public sealed class RangeForStatement(ForRangeDeclaration declaration, Expression range, Statement body) : Statement
{
    /// <summary>The init-statement before the declaration, or null.</summary>
    public Statement InitStatement { get; init; }

    public ForRangeDeclaration Declaration { get; } = declaration;
    public Expression Range { get; } = range;
    public Statement Body { get; } = body;
}

/// <summary>
/// The loop variable of a range-based for statement: <c>const auto &amp;value</c>, <c>auto [key, value]</c>.
/// </summary>
public sealed class ForRangeDeclaration(DeclSpecifierSequence specifiers, Declarator declarator) : CppNode
{
    public IReadOnlyList<AttributeSpecifier> Attributes { get; init; } = [];
    public DeclSpecifierSequence Specifiers { get; } = specifiers;
    public Declarator Declarator { get; } = declarator;
}

/// <summary>
/// <c>break;</c>.
/// </summary>
public sealed class BreakStatement : Statement
{
}

/// <summary>
/// <c>continue;</c>.
/// </summary>
public sealed class ContinueStatement : Statement
{
}

/// <summary>
/// <c>return expression;</c>; the expression is null in <c>return;</c> and may be a braced-init-list.
/// </summary>
public sealed class ReturnStatement(Expression expression = null) : Statement
{
    public Expression Expression { get; } = expression;
}

/// <summary>
/// <c>co_return expression;</c>; the expression is null in <c>co_return;</c> and may be a braced-init-list.
/// </summary>
public sealed class CoReturnStatement(Expression expression = null) : Statement
{
    public Expression Expression { get; } = expression;
}

/// <summary>
/// <c>goto label;</c>.
/// </summary>
public sealed class GotoStatement(string label) : Statement
{
    public string Label { get; } = label;
}

/// <summary>
/// A statement with attributes: <c>[[likely]] return 1;</c>, <c>[[fallthrough]];</c>. The attributes of a
/// declaration statement belong to its declaration (<see cref="SimpleDeclaration.Attributes"/>).
/// </summary>
public sealed class AttributedStatement(IReadOnlyList<AttributeSpecifier> attributes, Statement statement) : Statement
{
    public IReadOnlyList<AttributeSpecifier> Attributes { get; } = attributes ?? [];
    public Statement Statement { get; } = statement;
}

/// <summary>
/// <c>try { … } catch (…) { … }</c>.
/// </summary>
public sealed class TryStatement(CompoundStatement block, IReadOnlyList<CatchClause> handlers) : Statement
{
    public CompoundStatement Block { get; } = block;
    public IReadOnlyList<CatchClause> Handlers { get; } = handlers ?? [];
}

/// <summary>
/// A handler: <c>catch (const std::exception &amp;e) { … }</c>; the declaration is null in <c>catch (...)</c>.
/// </summary>
public sealed class CatchClause(ParameterDeclaration declaration, CompoundStatement body) : CppNode
{
    public ParameterDeclaration Declaration { get; } = declaration;
    public CompoundStatement Body { get; } = body;
}

// ========================================
// Expressions
// ========================================

public abstract class Expression : CppNode
{
}

public enum LiteralKind
{
    Integer,
    Floating,
    Character,
    String,
    Boolean,
    Nullptr,
}

/// <summary>
/// The encoding prefix of a character or string literal: none, <c>u8</c>, <c>u</c>, <c>U</c> or <c>L</c>.
/// </summary>
public enum CharacterEncoding
{
    Ordinary,
    Utf8,
    Utf16,
    Utf32,
    Wide,
}

/// <summary>
/// A literal as written in the source: <see cref="Text"/> is its text, including prefixes and suffixes
/// (without line splices, except in raw strings).
/// </summary>
/// <remarks>
/// <see cref="Value"/> is the value of the literal without its user-defined suffix, or null when it cannot
/// be computed:
/// <list type="bullet">
/// <item>integers: <see cref="ulong"/>, null when the value does not fit in 64 bits;</item>
/// <item>floating literals: <see cref="double"/>;</item>
/// <item>characters: the value of the code unit as a <see cref="long"/>, without sign extension
/// (<c>'\xFF'</c> is 255); a multicharacter literal (<c>'ab'</c>) is the <c>int</c> value
/// <c>('a' &lt;&lt; 8) | 'b'</c>, like GCC and clang compute it;</item>
/// <item>ordinary and <c>u8</c> strings: the UTF-8 code units as a <see cref="byte"/> array, without the
/// terminating zero; <c>u</c>, <c>U</c> and <c>L</c> strings: a <see cref="string"/>;</item>
/// <item><c>true</c> and <c>false</c>: <see cref="bool"/>; <c>nullptr</c>: null.</item>
/// </list>
/// Characters named with <c>\N{...}</c> are not decoded: .NET has no table of Unicode character names, so
/// literals with them have no value.
/// </remarks>
public sealed class LiteralExpression(LiteralKind kind, string text, object value = null) : Expression
{
    public LiteralKind Kind { get; } = kind;
    public string Text { get; } = text;
    public object Value { get; } = value;

    /// <summary>The encoding prefix of a character or string literal.</summary>
    public CharacterEncoding Encoding { get; init; }

    /// <summary>A raw string literal: <c>R"delimiter(...)delimiter"</c>.</summary>
    public bool IsRaw { get; init; }

    /// <summary>The suffix of a number that is part of the language, like <c>ull</c> or <c>f</c>, or null.</summary>
    public string Suffix { get; init; }

    /// <summary>The suffix of a user-defined literal, like <c>_km</c>, or null.</summary>
    public string UserDefinedSuffix { get; init; }
}

/// <summary>
/// Adjacent string literals, which are one literal: <c>"a" "b"</c>. The parts are decoded in the encoding
/// of the concatenation, the prefix of the parts that have one. A single string literal is a
/// <see cref="LiteralExpression"/>.
/// </summary>
public sealed class ConcatenatedStringExpression(IReadOnlyList<LiteralExpression> parts, object value = null) : Expression
{
    public IReadOnlyList<LiteralExpression> Parts { get; } = parts ?? [];

    /// <summary>The value of the whole string, like <see cref="LiteralExpression.Value"/> of a string.</summary>
    public object Value { get; } = value;

    public CharacterEncoding Encoding { get; init; }

    /// <summary>The suffix of a user-defined literal, which the parts share, or null.</summary>
    public string UserDefinedSuffix { get; init; }
}

/// <summary>
/// A name used as an expression: <c>a</c>, <c>S::member</c>, <c>operator+</c>.
/// </summary>
public sealed class NameExpression(Name name) : Expression
{
    public Name Name { get; } = name;
}

/// <summary>
/// <c>(expression)</c>.
/// </summary>
public sealed class ParenthesizedExpression(Expression expression) : Expression
{
    public Expression Expression { get; } = expression;
}

/// <summary>
/// A prefix or postfix operator: <c>-a</c>, <c>*p</c>, <c>i++</c>, or <c>co_await a</c>.
/// </summary>
public sealed class UnaryExpression(string @operator, Expression operand, bool isPostfix = false) : Expression
{
    public string Operator { get; } = @operator;
    public Expression Operand { get; } = operand;
    public bool IsPostfix { get; } = isPostfix;
}

/// <summary>
/// A binary operator, including assignments, the comma operator and the pointer-to-member operators:
/// <c>a + b</c>, <c>a = b</c>, <c>a, b</c>, <c>a.*m</c>. The right operand of an assignment may be an
/// <see cref="InitializerListExpression"/>: <c>a = { 1, 2 }</c>.
/// </summary>
public sealed class BinaryExpression(Expression left, string @operator, Expression right) : Expression
{
    public Expression Left { get; } = left;
    public string Operator { get; } = @operator;
    public Expression Right { get; } = right;
}

/// <summary>
/// <c>condition ? whenTrue : whenFalse</c>. The GNU conditional without a middle operand,
/// <c>condition ?: whenFalse</c>, has a null <see cref="WhenTrue"/>.
/// </summary>
public sealed class ConditionalExpression(Expression condition, Expression whenTrue, Expression whenFalse) : Expression
{
    public Expression Condition { get; } = condition;
    public Expression WhenTrue { get; } = whenTrue;
    public Expression WhenFalse { get; } = whenFalse;
}

/// <summary>
/// A function call: <c>f(a, b)</c>. An argument is an expression, an <see cref="InitializerListExpression"/>
/// or a <see cref="PackExpansionExpression"/>. A call of a name that is a type is a <see cref="FunctionalCastExpression"/>;
/// names declared outside the file (in headers) are not known as types, so <c>std::string("a")</c> is a call.
/// </summary>
public sealed class CallExpression(Expression callee, IReadOnlyList<Expression> arguments) : Expression
{
    public Expression Callee { get; } = callee;
    public IReadOnlyList<Expression> Arguments { get; } = arguments ?? [];
}


/// <summary>
/// <c>this</c>.
/// </summary>
public sealed class ThisExpression : Expression
{
}

/// <summary>
/// Member access: <c>a.b</c>, <c>p-&gt;b</c>, <c>a.template f&lt;int&gt;</c>, <c>a.Base::b</c>, or a destructor
/// name <c>p-&gt;~T</c>. <see cref="Operator"/> is <c>.</c> or <c>-&gt;</c>.
/// </summary>
public sealed class MemberAccessExpression(Expression @object, string @operator, Name member) : Expression
{
    public Expression Object { get; } = @object;
    public string Operator { get; } = @operator;

    /// <summary><c>template</c> comes before the member: <c>a.template f&lt;int&gt;</c>.</summary>
    public bool IsTemplate { get; init; }

    public Name Member { get; } = member;
}

/// <summary>
/// A subscript: <c>a[i]</c>. Since C++23 it takes any number of arguments: <c>m[1, 2]</c>, <c>a[]</c>;
/// an argument may be an <see cref="InitializerListExpression"/>.
/// </summary>
public sealed class SubscriptExpression(Expression @object, IReadOnlyList<Expression> arguments) : Expression
{
    public Expression Object { get; } = @object;
    public IReadOnlyList<Expression> Arguments { get; } = arguments ?? [];
}

/// <summary>
/// A C-style cast: <c>(int)x</c>.
/// </summary>
public sealed class CastExpression(TypeId type, Expression operand) : Expression
{
    public TypeId Type { get; } = type;
    public Expression Operand { get; } = operand;
}

/// <summary>
/// <c>static_cast&lt;T&gt;(x)</c>; <see cref="Keyword"/> is <c>static_cast</c>, <c>dynamic_cast</c>,
/// <c>const_cast</c> or <c>reinterpret_cast</c>.
/// </summary>
public sealed class NamedCastExpression(string keyword, TypeId type, Expression operand) : Expression
{
    public string Keyword { get; } = keyword;
    public TypeId Type { get; } = type;
    public Expression Operand { get; } = operand;
}

/// <summary>
/// A type used as a function: <c>int(x)</c>, <c>T{ 1, 2 }</c>, <c>auto(x)</c>, <c>typename T::type()</c>. The type
/// is one simple type specifier (<see cref="KeywordSpecifier"/>, <see cref="NamedTypeSpecifier"/> or
/// <see cref="DecltypeSpecifier"/>); the initializer is a <see cref="ParenthesizedInitializer"/> or a
/// <see cref="BracedInitializer"/>.
/// </summary>
public sealed class FunctionalCastExpression(DeclSpecifier type, Initializer initializer) : Expression
{
    public DeclSpecifier Type { get; } = type;
    public Initializer Initializer { get; } = initializer;
}

/// <summary>
/// <c>sizeof</c> or <c>alignof</c> of an expression (<c>sizeof x</c>) or of a type (<c>sizeof(int)</c>, the
/// operand is then a <see cref="TypeId"/> and the parentheses belong to the expression). <see cref="Keyword"/>
/// is <c>sizeof</c>, <c>alignof</c>, or one of the extensions <c>_Alignof</c>, <c>__alignof</c> and <c>__alignof__</c>.
/// </summary>
public sealed class SizeOfExpression(string keyword, CppNode operand) : Expression
{
    public string Keyword { get; } = keyword;

    /// <summary>A <see cref="TypeId"/> or an <see cref="Expression"/>.</summary>
    public CppNode Operand { get; } = operand;
}

/// <summary>
/// <c>sizeof...(pack)</c>.
/// </summary>
public sealed class SizeOfPackExpression(IdentifierName pack) : Expression
{
    public IdentifierName Pack { get; } = pack;
}

/// <summary>
/// <c>noexcept(expression)</c>.
/// </summary>
public sealed class NoexceptExpression(Expression operand) : Expression
{
    public Expression Operand { get; } = operand;
}

/// <summary>
/// <c>typeid(type)</c> or <c>typeid(expression)</c>.
/// </summary>
public sealed class TypeidExpression(CppNode operand) : Expression
{
    /// <summary>A <see cref="TypeId"/> or an <see cref="Expression"/>.</summary>
    public CppNode Operand { get; } = operand;
}

/// <summary>
/// A new-expression: <c>new int</c>, <c>::new (buffer) T(1)</c>, <c>new int[n]{ 1, 2 }</c>, <c>new (int *)(p)</c>.
/// The type of <c>new int[n]</c> has an <see cref="ArrayDeclarator"/> whose size is any expression.
/// </summary>
public sealed class NewExpression(IReadOnlyList<Expression> placement, TypeId type, Initializer initializer = null) : Expression
{
    /// <summary><c>::new</c>.</summary>
    public bool IsGlobal { get; init; }

    /// <summary>The placement arguments: <c>(buffer)</c>, or null when there are none.</summary>
    public IReadOnlyList<Expression> Placement { get; } = placement;

    public TypeId Type { get; } = type;

    /// <summary>The type is in parentheses: <c>new (int *)</c>.</summary>
    public bool IsParenthesizedType { get; init; }

    /// <summary>A <see cref="ParenthesizedInitializer"/>, a <see cref="BracedInitializer"/> or null.</summary>
    public Initializer Initializer { get; } = initializer;
}

/// <summary>
/// <c>delete p</c>, <c>delete[] p</c>, <c>::delete p</c>.
/// </summary>
public sealed class DeleteExpression(Expression operand) : Expression
{
    public bool IsGlobal { get; init; }
    public bool IsArray { get; init; }
    public Expression Operand { get; } = operand;
}

/// <summary>
/// <c>throw expression</c>, or <c>throw</c> alone when <see cref="Operand"/> is null.
/// </summary>
public sealed class ThrowExpression(Expression operand = null) : Expression
{
    public Expression Operand { get; } = operand;
}

/// <summary>
/// <c>co_yield expression</c>; the operand may be an <see cref="InitializerListExpression"/>.
/// </summary>
public sealed class YieldExpression(Expression operand) : Expression
{
    public Expression Operand { get; } = operand;
}

/// <summary>
/// A braced-init-list: <c>{ 1, 2 }</c>, <c>{ .x = 1 }</c>. It appears where the grammar allows an
/// initializer-clause: in initializers, arguments, subscripts, other lists and on the right of an assignment.
/// An element is an expression, a nested list, a <see cref="DesignatedInitializerExpression"/> or a
/// <see cref="PackExpansionExpression"/>.
/// </summary>
public sealed class InitializerListExpression(IReadOnlyList<Expression> elements) : Expression
{
    public IReadOnlyList<Expression> Elements { get; } = elements ?? [];

    /// <summary>The list ends with a comma: <c>{ 1, 2, }</c>.</summary>
    public bool HasTrailingComma { get; init; }
}

/// <summary>
/// A designated initializer: <c>.x = 1</c>, <c>.to{ 2, 3 }</c>, or with the extensions of C99 that clang
/// accepts, <c>.a.b = 1</c> and <c>[2] = 1</c>. Without <see cref="HasEquals"/> the value is an
/// <see cref="InitializerListExpression"/>.
/// </summary>
public sealed class DesignatedInitializerExpression(IReadOnlyList<Designator> designators, Expression value) : Expression
{
    public IReadOnlyList<Designator> Designators { get; } = designators ?? [];
    public bool HasEquals { get; init; }
    public Expression Value { get; } = value;
}

/// <summary>
/// A designator: <c>.member</c>, or <c>[index]</c> when <see cref="Index"/> is set.
/// </summary>
public sealed class Designator(string member, Expression index = null) : CppNode
{
    public string Member { get; } = member;
    public Expression Index { get; } = index;
}

/// <summary>
/// A pack expansion in a list: <c>args...</c> in <c>f(args...)</c>, <c>{ xs... }</c> or template arguments.
/// </summary>
public sealed class PackExpansionExpression(Expression pattern) : Expression
{
    public Expression Pattern { get; } = pattern;
}

/// <summary>
/// A fold expression, with its parentheses: <c>(xs + ...)</c> has a null <see cref="Right"/>, <c>(... + xs)</c>
/// a null <see cref="Left"/>, and <c>(xs + ... + 0)</c> both operands.
/// </summary>
public sealed class FoldExpression(Expression left, string @operator, Expression right) : Expression
{
    public Expression Left { get; } = left;
    public string Operator { get; } = @operator;
    public Expression Right { get; } = right;
}

/// <summary>
/// A lambda: <c>[captures] &lt;template parameters&gt; requires C (parameters) specifiers -&gt; type { body }</c>.
/// </summary>
public sealed class LambdaExpression(IReadOnlyList<LambdaCapture> captures, CompoundStatement body) : Expression
{
    /// <summary><c>=</c> or <c>&amp;</c> as the first capture, or null.</summary>
    public string CaptureDefault { get; init; }

    public IReadOnlyList<LambdaCapture> Captures { get; } = captures ?? [];

    /// <summary>The template parameters: <c>&lt;typename T&gt;</c>, or null when there are none.</summary>
    public IReadOnlyList<TemplateParameter> TemplateParameters { get; init; }

    /// <summary>The requires-clause after the template parameters, or null.</summary>
    public Expression TemplateRequiresClause { get; init; }

    /// <summary>The attributes before the parameters: <c>[] [[nodiscard]] (int x)</c>.</summary>
    public IReadOnlyList<AttributeSpecifier> Attributes { get; init; } = [];

    /// <summary>The parameters, or null when the lambda has no parentheses: <c>[] { }</c>.</summary>
    public IReadOnlyList<ParameterDeclaration> Parameters { get; init; }

    /// <summary>The parameters end with <c>...</c>, as in <see cref="FunctionDeclarator.IsVariadic"/>.</summary>
    public bool IsVariadic { get; init; }

    /// <summary><c>mutable</c>, <c>constexpr</c>, <c>consteval</c> and <c>static</c>, in source order.</summary>
    public IReadOnlyList<string> Specifiers { get; init; } = [];

    public NoexceptSpecifier Noexcept { get; init; }

    /// <summary>The attributes after the specifiers, which appertain to the type of the call operator.</summary>
    public IReadOnlyList<AttributeSpecifier> TypeAttributes { get; init; } = [];

    /// <summary>The type after <c>-&gt;</c>, or null.</summary>
    public TypeId TrailingReturnType { get; init; }

    /// <summary>The requires-clause after the parameters, or null.</summary>
    public Expression RequiresClause { get; init; }

    public CompoundStatement Body { get; } = body;
}

/// <summary>
/// A lambda capture: <c>x</c>, <c>&amp;x</c>, <c>x...</c>, <c>this</c>, <c>*this</c>, or an init-capture
/// <c>x = a + b</c>, <c>&amp;r = a</c>, <c>...xs = ys</c>, <c>x{ a }</c>.
/// </summary>
public sealed class LambdaCapture(string identifier, Initializer initializer = null) : CppNode
{
    /// <summary>The captured or declared variable; null for <c>this</c> and <c>*this</c>.</summary>
    public string Identifier { get; } = identifier;

    public bool IsByReference { get; init; }

    /// <summary><c>this</c>, or <c>*this</c> with <see cref="IsStarThis"/>.</summary>
    public bool IsThis { get; init; }

    public bool IsStarThis { get; init; }

    /// <summary>A pack: <c>xs...</c>, or <c>...xs = ys</c> for an init-capture.</summary>
    public bool IsPack { get; init; }

    /// <summary>The initializer of an init-capture, or null.</summary>
    public Initializer Initializer { get; } = initializer;
}

/// <summary>
/// A requires-expression: <c>requires (T a) { a + 1; typename T::type; { a } -&gt; C; requires D&lt;T&gt;; }</c>.
/// </summary>
public sealed class RequiresExpression(IReadOnlyList<ParameterDeclaration> parameters, IReadOnlyList<Requirement> requirements) : Expression
{
    /// <summary>The parameters, or null when there are no parentheses.</summary>
    public IReadOnlyList<ParameterDeclaration> Parameters { get; } = parameters;

    public IReadOnlyList<Requirement> Requirements { get; } = requirements ?? [];
}

public abstract class Requirement : CppNode
{
}

/// <summary>
/// <c>expression;</c> in a requires-expression.
/// </summary>
public sealed class SimpleRequirement(Expression expression) : Requirement
{
    public Expression Expression { get; } = expression;
}

/// <summary>
/// <c>typename T::type;</c> in a requires-expression.
/// </summary>
public sealed class TypeRequirement(Name type) : Requirement
{
    public Name Type { get; } = type;
}

/// <summary>
/// <c>{ expression } noexcept -&gt; C&lt;int&gt;;</c> in a requires-expression; <c>noexcept</c> and the type constraint
/// are optional.
/// </summary>
public sealed class CompoundRequirement(Expression expression) : Requirement
{
    public Expression Expression { get; } = expression;
    public bool IsNoexcept { get; init; }

    /// <summary>The type constraint after <c>-&gt;</c>: a concept name with its arguments but the first, or null.</summary>
    public Name TypeConstraint { get; init; }
}

/// <summary>
/// <c>requires constraint;</c> in a requires-expression.
/// </summary>
public sealed class NestedRequirement(Expression constraint) : Requirement
{
    public Expression Constraint { get; } = constraint;
}

// ========================================
// Templates
// ========================================

/// <summary>
/// A template parameter ([temp.param]).
/// </summary>
public abstract class TemplateParameter : CppNode
{
}

/// <summary>
/// A type parameter: <c>typename T</c>, <c>class... Ts</c>, <c>typename T = int</c>, or constrained by a concept,
/// <c>std::integral T</c>, when <see cref="Constraint"/> is set and <see cref="Key"/> is null. The identifier is
/// null for an unnamed parameter.
/// </summary>
public sealed class TypeTemplateParameter(string key, string identifier) : TemplateParameter
{
    /// <summary><c>typename</c> or <c>class</c>; null for a constrained parameter.</summary>
    public string Key { get; } = key;

    /// <summary>The concept of a constrained parameter: <c>std::integral</c>, <c>C&lt;int&gt;</c>, or null.</summary>
    public Name Constraint { get; init; }

    public bool IsPack { get; init; }
    public string Identifier { get; } = identifier;
    public TypeId Default { get; init; }
}

/// <summary>
/// A non-type parameter: <c>int N</c>, <c>auto V = 1</c>, <c>int... Ns</c>.
/// </summary>
public sealed class NonTypeTemplateParameter(ParameterDeclaration parameter) : TemplateParameter
{
    public ParameterDeclaration Parameter { get; } = parameter;
}

/// <summary>
/// A template template parameter: <c>template &lt;typename&gt; class TT = std::vector</c>.
/// </summary>
public sealed class TemplateTemplateParameter(IReadOnlyList<TemplateParameter> parameters, string key, string identifier) : TemplateParameter
{
    public IReadOnlyList<TemplateParameter> Parameters { get; } = parameters ?? [];

    /// <summary><c>class</c> or <c>typename</c>.</summary>
    public string Key { get; } = key;

    public bool IsPack { get; init; }
    public string Identifier { get; } = identifier;
    public Name Default { get; init; }
}

// ========================================
// Attributes
// ========================================

/// <summary>
/// <c>[[ attributes ]]</c>, or <c>[[using ns: attributes]]</c> with <see cref="UsingNamespace"/>.
/// </summary>
public sealed class AttributeSpecifier(IReadOnlyList<CppAttribute> attributes) : CppNode
{
    public string UsingNamespace { get; init; }
    public IReadOnlyList<CppAttribute> Attributes { get; } = attributes ?? [];
}

/// <summary>
/// An attribute: <c>nodiscard</c>, <c>gnu::always_inline</c>, <c>deprecated("reason")</c>. The arguments are
/// kept as written, without the parentheses: <c>"reason"</c>; they are null when there are no parentheses.
/// (The name avoids a clash with <see cref="System.Attribute"/>.)
/// </summary>
public sealed class CppAttribute(string @namespace, string name, string arguments = null) : CppNode
{
    public string Namespace { get; } = @namespace;
    public string Name { get; } = name;
    public string Arguments { get; } = arguments;

    /// <summary>The attribute is followed by <c>...</c>.</summary>
    public bool IsPackExpansion { get; init; }
}
