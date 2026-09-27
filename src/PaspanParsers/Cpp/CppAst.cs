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
/// A declaration of variables or functions: <c>static int a = 1, *b;</c> or <c>int f(int);</c>.
/// The span includes the ';'.
/// </summary>
public sealed class SimpleDeclaration(DeclSpecifierSequence specifiers, IReadOnlyList<InitDeclarator> declarators) : Declaration
{
    public DeclSpecifierSequence Specifiers { get; } = specifiers;
    public IReadOnlyList<InitDeclarator> Declarators { get; } = declarators ?? [];
}

/// <summary>
/// A function with its body: <c>int main() { return 0; }</c>.
/// </summary>
public sealed class FunctionDefinition(DeclSpecifierSequence specifiers, Declarator declarator, CompoundStatement body) : Declaration
{
    public DeclSpecifierSequence Specifiers { get; } = specifiers;
    public Declarator Declarator { get; } = declarator;
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
/// A declaration specifier that is a keyword: a fundamental type (<c>int</c>, <c>unsigned</c>), a cv-qualifier,
/// a storage class or a function specifier.
/// </summary>
public sealed class KeywordSpecifier(string keyword) : DeclSpecifier
{
    public string Keyword { get; } = keyword;
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
    public Initializer Initializer { get; } = initializer;
}

public abstract class Declarator : CppNode
{
}

/// <summary>
/// The declared name: <c>a</c> in <c>int a;</c>.
/// </summary>
public sealed class NameDeclarator(string name) : Declarator
{
    public string Name { get; } = name;
}

/// <summary>
/// A function declarator: <c>f(int a, char b)</c>.
/// </summary>
public sealed class FunctionDeclarator(Declarator inner, IReadOnlyList<ParameterDeclaration> parameters) : Declarator
{
    public Declarator Inner { get; } = inner;
    public IReadOnlyList<ParameterDeclaration> Parameters { get; } = parameters ?? [];
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
/// A name used as an expression: <c>a</c>.
/// </summary>
public sealed class NameExpression(string name) : Expression
{
    public string Name { get; } = name;
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
