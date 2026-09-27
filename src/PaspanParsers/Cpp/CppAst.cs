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
/// <c>typedef int Integer;</c> or, without declarators, <c>struct Point;</c>. The span includes the ';'.
/// The specifiers are null for a constructor, destructor or conversion function, which have none.
/// </summary>
public sealed class SimpleDeclaration(DeclSpecifierSequence specifiers, IReadOnlyList<InitDeclarator> declarators) : Declaration
{
    public DeclSpecifierSequence Specifiers { get; } = specifiers;
    public IReadOnlyList<InitDeclarator> Declarators { get; } = declarators ?? [];
}

/// <summary>
/// A function with its body: <c>int main() { return 0; }</c>. The specifiers are null for a constructor,
/// destructor or conversion function, which have none: <c>S::~S() { }</c>.
/// </summary>
public sealed class FunctionDefinition(DeclSpecifierSequence specifiers, Declarator declarator, CompoundStatement body) : Declaration
{
    public DeclSpecifierSequence Specifiers { get; } = specifiers;
    public Declarator Declarator { get; } = declarator;

    /// <summary>The constraint after the declarator: <c>requires C&lt;T&gt;</c>, or null.</summary>
    public Expression RequiresClause { get; init; }

    public CompoundStatement Body { get; } = body;
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
/// <c>= expression</c>.
/// </summary>
public sealed class EqualsInitializer(Expression value) : Initializer
{
    public Expression Value { get; } = value;
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
/// A declaration in a block: <c>int a = 1;</c>.
/// </summary>
public sealed class DeclarationStatement(SimpleDeclaration declaration) : Statement
{
    public SimpleDeclaration Declaration { get; } = declaration;
}

/// <summary>
/// <c>expression;</c>, or the null statement <c>;</c> when <see cref="Expression"/> is null.
/// </summary>
public sealed class ExpressionStatement(Expression expression) : Statement
{
    public Expression Expression { get; } = expression;
}

/// <summary>
/// <c>if (condition) then else otherwise</c>.
/// </summary>
public sealed class IfStatement(Expression condition, Statement then, Statement @else = null) : Statement
{
    public Expression Condition { get; } = condition;
    public Statement Then { get; } = then;
    public Statement Else { get; } = @else;
}

/// <summary>
/// <c>while (condition) body</c>.
/// </summary>
public sealed class WhileStatement(Expression condition, Statement body) : Statement
{
    public Expression Condition { get; } = condition;
    public Statement Body { get; } = body;
}

/// <summary>
/// <c>return expression;</c>; the expression is null in <c>return;</c>.
/// </summary>
public sealed class ReturnStatement(Expression expression = null) : Statement
{
    public Expression Expression { get; } = expression;
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
/// A prefix or postfix operator: <c>-a</c>, <c>*p</c>, <c>i++</c>.
/// </summary>
public sealed class UnaryExpression(string @operator, Expression operand, bool isPostfix = false) : Expression
{
    public string Operator { get; } = @operator;
    public Expression Operand { get; } = operand;
    public bool IsPostfix { get; } = isPostfix;
}

/// <summary>
/// A binary operator, including assignments and the comma operator: <c>a + b</c>, <c>a = b</c>, <c>a, b</c>.
/// </summary>
public sealed class BinaryExpression(Expression left, string @operator, Expression right) : Expression
{
    public Expression Left { get; } = left;
    public string Operator { get; } = @operator;
    public Expression Right { get; } = right;
}

/// <summary>
/// <c>condition ? whenTrue : whenFalse</c>.
/// </summary>
public sealed class ConditionalExpression(Expression condition, Expression whenTrue, Expression whenFalse) : Expression
{
    public Expression Condition { get; } = condition;
    public Expression WhenTrue { get; } = whenTrue;
    public Expression WhenFalse { get; } = whenFalse;
}

/// <summary>
/// A function call: <c>f(a, b)</c>.
/// </summary>
public sealed class CallExpression(Expression callee, IReadOnlyList<Expression> arguments) : Expression
{
    public Expression Callee { get; } = callee;
    public IReadOnlyList<Expression> Arguments { get; } = arguments ?? [];
}
