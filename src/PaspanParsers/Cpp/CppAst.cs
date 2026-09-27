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
/// A literal as written in the source: <see cref="Text"/> is its text, including prefixes and suffixes.
/// </summary>
public sealed class LiteralExpression(LiteralKind kind, string text) : Expression
{
    public LiteralKind Kind { get; } = kind;
    public string Text { get; } = text;
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
