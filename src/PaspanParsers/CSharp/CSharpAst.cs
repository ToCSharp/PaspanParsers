namespace PaspanParsers.CSharp;

// ========================================
// Base Interfaces
// ========================================

/// <summary>
/// Base interface for all C# AST nodes
/// </summary>
public interface ICSharpNode
{
}

// ========================================
// Compilation Unit
// ========================================

public sealed class CompilationUnit(
    IReadOnlyList<ExternAliasDirective> externAliases = null,
    IReadOnlyList<UsingDirective> usings = null,
    IReadOnlyList<AttributeSection> globalAttributes = null,
    IReadOnlyList<MemberDeclaration> members = null) : ICSharpNode
{
    public IReadOnlyList<ExternAliasDirective> ExternAliases { get; } = externAliases;
    public IReadOnlyList<UsingDirective> Usings { get; } = usings;
    public IReadOnlyList<AttributeSection> GlobalAttributes { get; } = globalAttributes;
    public IReadOnlyList<MemberDeclaration> Members { get; } = members;
}

// ========================================
// Using Directives
// ========================================

public abstract class UsingDirective : ICSharpNode
{
}

public sealed class UsingNamespaceDirective(NameExpression namespaceName) : UsingDirective
{
    public NameExpression Namespace { get; } = namespaceName;
}

public sealed class UsingAliasDirective(string alias, NameExpression target) : UsingDirective
{
    public string Alias { get; } = alias;
    public NameExpression Target { get; } = target;
}

public sealed class UsingStaticDirective(NameExpression type) : UsingDirective
{
    public NameExpression Type { get; } = type;
}

public sealed class ExternAliasDirective(string identifier) : ICSharpNode
{
    public string Identifier { get; } = identifier;
}

// ========================================
// Member Declarations
// ========================================

public abstract class MemberDeclaration(IReadOnlyList<AttributeSection> attributes, Modifiers modifiers) : ICSharpNode
{
    public IReadOnlyList<AttributeSection> Attributes { get; } = attributes;
    public Modifiers Modifiers { get; } = modifiers;
}

[Flags]
public enum Modifiers
{
    None = 0,
    New = 1 << 0,
    Public = 1 << 1,
    Protected = 1 << 2,
    Internal = 1 << 3,
    Private = 1 << 4,
    Abstract = 1 << 5,
    Sealed = 1 << 6,
    Static = 1 << 7,
    Readonly = 1 << 8,
    Virtual = 1 << 9,
    Override = 1 << 10,
    Extern = 1 << 11,
    Unsafe = 1 << 12,
    Volatile = 1 << 13,
    Async = 1 << 14,
    Partial = 1 << 15,
    Const = 1 << 16,
    Required = 1 << 17,
    File = 1 << 18,
    Ref = 1 << 19,
}

// ========================================
// Namespace Declaration
// ========================================

public sealed class NamespaceDeclaration(
    NameExpression name,
    IReadOnlyList<MemberDeclaration> members = null,
    IReadOnlyList<UsingDirective> usings = null,
    IReadOnlyList<ExternAliasDirective> externAliases = null,
    bool isFileScopedNamespace = false) : MemberDeclaration(null, Modifiers.None)
{
    public NameExpression Name { get; } = name;
    public IReadOnlyList<ExternAliasDirective> ExternAliases { get; } = externAliases;
    public IReadOnlyList<UsingDirective> Usings { get; } = usings;
    public IReadOnlyList<MemberDeclaration> Members { get; } = members;
    public bool IsFileScopedNamespace { get; } = isFileScopedNamespace;
}

// ========================================
// Type Declarations
// ========================================

public sealed class ClassDeclaration(
    string name,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None,
    IReadOnlyList<TypeParameter> typeParameters = null,
    IReadOnlyList<TypeReference> baseTypes = null,
    IReadOnlyList<TypeParameterConstraint> constraints = null,
    IReadOnlyList<MemberDeclaration> members = null) : MemberDeclaration(attributes, modifiers)
{
    public string Name { get; } = name;
    public IReadOnlyList<TypeParameter> TypeParameters { get; } = typeParameters;
    public IReadOnlyList<TypeReference> BaseTypes { get; } = baseTypes;
    public IReadOnlyList<TypeParameterConstraint> Constraints { get; } = constraints;
    public IReadOnlyList<MemberDeclaration> Members { get; } = members;
}

public sealed class StructDeclaration(
    string name,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None,
    IReadOnlyList<TypeParameter> typeParameters = null,
    IReadOnlyList<TypeReference> interfaces = null,
    IReadOnlyList<TypeParameterConstraint> constraints = null,
    IReadOnlyList<MemberDeclaration> members = null) : MemberDeclaration(attributes, modifiers)
{
    public string Name { get; } = name;
    public IReadOnlyList<TypeParameter> TypeParameters { get; } = typeParameters;
    public IReadOnlyList<TypeReference> Interfaces { get; } = interfaces;
    public IReadOnlyList<TypeParameterConstraint> Constraints { get; } = constraints;
    public IReadOnlyList<MemberDeclaration> Members { get; } = members;
}

public sealed class InterfaceDeclaration(
    string name,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None,
    IReadOnlyList<TypeParameter> typeParameters = null,
    IReadOnlyList<TypeReference> baseInterfaces = null,
    IReadOnlyList<TypeParameterConstraint> constraints = null,
    IReadOnlyList<MemberDeclaration> members = null) : MemberDeclaration(attributes, modifiers)
{
    public string Name { get; } = name;
    public IReadOnlyList<TypeParameter> TypeParameters { get; } = typeParameters;
    public IReadOnlyList<TypeReference> BaseInterfaces { get; } = baseInterfaces;
    public IReadOnlyList<TypeParameterConstraint> Constraints { get; } = constraints;
    public IReadOnlyList<MemberDeclaration> Members { get; } = members;
}

public sealed class EnumDeclaration(
    string name,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None,
    TypeReference baseType = null,
    IReadOnlyList<EnumMember> members = null) : MemberDeclaration(attributes, modifiers)
{
    public string Name { get; } = name;
    public TypeReference BaseType { get; } = baseType;
    public IReadOnlyList<EnumMember> Members { get; } = members;
}

public sealed class EnumMember(string name, Expression value = null, IReadOnlyList<AttributeSection> attributes = null) : ICSharpNode
{
    public IReadOnlyList<AttributeSection> Attributes { get; } = attributes;
    public string Name { get; } = name;
    public Expression Value { get; } = value;
}

public sealed class DelegateDeclaration(
    TypeReference returnType,
    string name,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None,
    IReadOnlyList<TypeParameter> typeParameters = null,
    IReadOnlyList<Parameter> parameters = null,
    IReadOnlyList<TypeParameterConstraint> constraints = null) : MemberDeclaration(attributes, modifiers)
{
    public TypeReference ReturnType { get; } = returnType;
    public string Name { get; } = name;
    public IReadOnlyList<TypeParameter> TypeParameters { get; } = typeParameters;
    public IReadOnlyList<Parameter> Parameters { get; } = parameters;
    public IReadOnlyList<TypeParameterConstraint> Constraints { get; } = constraints;
}

public sealed class RecordDeclaration(
    string name,
    bool isRecordStruct = false,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None,
    IReadOnlyList<TypeParameter> typeParameters = null,
    IReadOnlyList<Parameter> primaryConstructorParameters = null,
    IReadOnlyList<TypeReference> baseTypes = null,
    IReadOnlyList<TypeParameterConstraint> constraints = null,
    IReadOnlyList<MemberDeclaration> members = null) : MemberDeclaration(attributes, modifiers)
{
    public string Name { get; } = name;
    public bool IsRecordStruct { get; } = isRecordStruct;
    public IReadOnlyList<TypeParameter> TypeParameters { get; } = typeParameters;
    public IReadOnlyList<Parameter> PrimaryConstructorParameters { get; } = primaryConstructorParameters;
    public IReadOnlyList<TypeReference> BaseTypes { get; } = baseTypes;
    public IReadOnlyList<TypeParameterConstraint> Constraints { get; } = constraints;
    public IReadOnlyList<MemberDeclaration> Members { get; } = members;
}

// ========================================
// Type Parameters
// ========================================

public sealed class TypeParameter(string name, VarianceKind? variance = null, IReadOnlyList<AttributeSection> attributes = null) : ICSharpNode
{
    public IReadOnlyList<AttributeSection> Attributes { get; } = attributes;
    public string Name { get; } = name;
    public VarianceKind? Variance { get; } = variance;
}

public enum VarianceKind
{
    In,
    Out
}

public sealed class TypeParameterConstraint(string typeParameterName, IReadOnlyList<TypeConstraint> constraints) : ICSharpNode
{
    public string TypeParameterName { get; } = typeParameterName;
    public IReadOnlyList<TypeConstraint> Constraints { get; } = constraints;
}

public abstract class TypeConstraint : ICSharpNode
{
}

public sealed class ClassConstraint(bool isNullable = false) : TypeConstraint
{
    public bool IsNullable { get; } = isNullable;
}

public sealed class StructConstraint : TypeConstraint
{
}

public sealed class UnmanagedConstraint : TypeConstraint
{
}

public sealed class NotNullConstraint : TypeConstraint
{
}

public sealed class TypeReferenceConstraint(TypeReference type) : TypeConstraint
{
    public TypeReference Type { get; } = type;
}

public sealed class ConstructorConstraint : TypeConstraint
{
}

// ========================================
// Field Declaration
// ========================================

public sealed class FieldDeclaration(
    TypeReference type,
    IReadOnlyList<VariableDeclarator> variables,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None) : MemberDeclaration(attributes, modifiers)
{
    public TypeReference Type { get; } = type;
    public IReadOnlyList<VariableDeclarator> Variables { get; } = variables;
}

public sealed class VariableDeclarator(string name, Expression initializer = null) : ICSharpNode
{
    public string Name { get; } = name;
    public Expression Initializer { get; } = initializer;
}

// ========================================
// Method Declaration
// ========================================

public sealed class MethodDeclaration(
    TypeReference returnType,
    string name,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None,
    IReadOnlyList<TypeParameter> typeParameters = null,
    IReadOnlyList<Parameter> parameters = null,
    IReadOnlyList<TypeParameterConstraint> constraints = null,
    MethodBody body = null) : MemberDeclaration(attributes, modifiers)
{
    public TypeReference ReturnType { get; } = returnType;
    public string Name { get; } = name;
    public IReadOnlyList<TypeParameter> TypeParameters { get; } = typeParameters;
    public IReadOnlyList<Parameter> Parameters { get; } = parameters;
    public IReadOnlyList<TypeParameterConstraint> Constraints { get; } = constraints;
    public MethodBody Body { get; } = body;
}

public abstract class MethodBody : ICSharpNode
{
}

public sealed class BlockMethodBody(BlockStatement block) : MethodBody
{
    public BlockStatement Block { get; } = block;
}

public sealed class ExpressionMethodBody(Expression expression) : MethodBody
{
    public Expression Expression { get; } = expression;
}

public sealed class Parameter(
    TypeReference type,
    string name,
    ParameterModifier modifier = ParameterModifier.None,
    Expression defaultValue = null,
    IReadOnlyList<AttributeSection> attributes = null) : ICSharpNode
{
    public IReadOnlyList<AttributeSection> Attributes { get; } = attributes;
    public ParameterModifier Modifier { get; } = modifier;
    public TypeReference Type { get; } = type;
    public string Name { get; } = name;
    public Expression DefaultValue { get; } = defaultValue;
}

public enum ParameterModifier
{
    None,
    This,
    Ref,
    Out,
    In,
    Params
}

// ========================================
// Property Declaration
// ========================================

public sealed class PropertyDeclaration(
    TypeReference type,
    string name,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None,
    IReadOnlyList<Accessor> accessors = null,
    Expression expressionBody = null,
    Expression initializer = null) : MemberDeclaration(attributes, modifiers)
{
    public TypeReference Type { get; } = type;
    public string Name { get; } = name;
    public IReadOnlyList<Accessor> Accessors { get; } = accessors;
    public Expression ExpressionBody { get; } = expressionBody;
    public Expression Initializer { get; } = initializer;
}

public sealed class Accessor(
    AccessorKind kind,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None,
    MethodBody body = null) : ICSharpNode
{
    public AccessorKind Kind { get; } = kind;
    public IReadOnlyList<AttributeSection> Attributes { get; } = attributes;
    public Modifiers Modifiers { get; } = modifiers;
    public MethodBody Body { get; } = body;
}

public enum AccessorKind
{
    Get,
    Set,
    Init
}

// ========================================
// Indexer Declaration
// ========================================

public sealed class IndexerDeclaration(
    TypeReference type,
    IReadOnlyList<Parameter> parameters,
    IReadOnlyList<Accessor> accessors,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None) : MemberDeclaration(attributes, modifiers)
{
    public TypeReference Type { get; } = type;
    public IReadOnlyList<Parameter> Parameters { get; } = parameters;
    public IReadOnlyList<Accessor> Accessors { get; } = accessors;
}

// ========================================
// Event Declaration
// ========================================

public sealed class EventDeclaration(
    TypeReference type,
    IReadOnlyList<VariableDeclarator> variables,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None,
    IReadOnlyList<EventAccessor> accessors = null) : MemberDeclaration(attributes, modifiers)
{
    public TypeReference Type { get; } = type;
    public IReadOnlyList<VariableDeclarator> Variables { get; } = variables;
    public IReadOnlyList<EventAccessor> Accessors { get; } = accessors;
}

public sealed class EventAccessor(EventAccessorKind kind, BlockStatement block, IReadOnlyList<AttributeSection> attributes = null) : ICSharpNode
{
    public EventAccessorKind Kind { get; } = kind;
    public IReadOnlyList<AttributeSection> Attributes { get; } = attributes;
    public BlockStatement Block { get; } = block;
}

public enum EventAccessorKind
{
    Add,
    Remove
}

// ========================================
// Constructor Declaration
// ========================================

public sealed class ConstructorDeclaration(
    string name,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None,
    IReadOnlyList<Parameter> parameters = null,
    ConstructorInitializer initializer = null,
    MethodBody body = null) : MemberDeclaration(attributes, modifiers)
{
    public string Name { get; } = name;
    public IReadOnlyList<Parameter> Parameters { get; } = parameters;
    public ConstructorInitializer Initializer { get; } = initializer;
    public MethodBody Body { get; } = body;
}

public sealed class ConstructorInitializer(bool isBase, IReadOnlyList<Argument> arguments = null) : ICSharpNode
{
    public bool IsBase { get; } = isBase;
    public IReadOnlyList<Argument> Arguments { get; } = arguments;
}

// ========================================
// Type References
// ========================================

public abstract class TypeReference : ICSharpNode
{
}

public sealed class NamedTypeReference(NameExpression name, IReadOnlyList<TypeReference> typeArguments = null, bool isNullable = false) : TypeReference
{
    public NameExpression Name { get; } = name;
    public IReadOnlyList<TypeReference> TypeArguments { get; } = typeArguments;
    public bool IsNullable { get; } = isNullable;
}

public sealed class PredefinedTypeReference(PredefinedType type, bool isNullable = false) : TypeReference
{
    public PredefinedType Type { get; } = type;
    public bool IsNullable { get; } = isNullable;
}

public enum PredefinedType
{
    Object,
    String,
    Bool,
    Byte,
    SByte,
    Short,
    UShort,
    Int,
    UInt,
    Long,
    ULong,
    Float,
    Double,
    Decimal,
    Char,
    Void,
    Dynamic
}

public sealed class ArrayTypeReference(TypeReference elementType, int rank = 1) : TypeReference
{
    public TypeReference ElementType { get; } = elementType;
    public int Rank { get; } = rank;
}

public sealed class TupleTypeReference(IReadOnlyList<TupleElement> elements) : TypeReference
{
    public IReadOnlyList<TupleElement> Elements { get; } = elements;
}

public sealed class TupleElement(TypeReference type, string name = null) : ICSharpNode
{
    public TypeReference Type { get; } = type;
    public string Name { get; } = name;
}

// ========================================
// Statements
// ========================================

public abstract class Statement : ICSharpNode
{
}

public sealed class BlockStatement(IReadOnlyList<Statement> statements = null) : Statement
{
    public IReadOnlyList<Statement> Statements { get; } = statements;
}

public sealed class ExpressionStatement(Expression expression) : Statement
{
    public Expression Expression { get; } = expression;
}

public sealed class LocalDeclarationStatement(
    TypeReference type,
    IReadOnlyList<VariableDeclarator> variables,
    bool isConst = false,
    bool isUsing = false) : Statement
{
    public bool IsConst { get; } = isConst;
    public bool IsUsing { get; } = isUsing;
    public TypeReference Type { get; } = type;
    public IReadOnlyList<VariableDeclarator> Variables { get; } = variables;
}

public sealed class IfStatement(Expression condition, Statement thenStatement, Statement elseStatement = null) : Statement
{
    public Expression Condition { get; } = condition;
    public Statement ThenStatement { get; } = thenStatement;
    public Statement ElseStatement { get; } = elseStatement;
}

public sealed class SwitchStatement(Expression expression, IReadOnlyList<SwitchSection> sections = null) : Statement
{
    public Expression Expression { get; } = expression;
    public IReadOnlyList<SwitchSection> Sections { get; } = sections;
}

public sealed class SwitchSection(IReadOnlyList<SwitchLabel> labels, IReadOnlyList<Statement> statements) : ICSharpNode
{
    public IReadOnlyList<SwitchLabel> Labels { get; } = labels;
    public IReadOnlyList<Statement> Statements { get; } = statements;
}

public abstract class SwitchLabel : ICSharpNode
{
}

public sealed class CaseSwitchLabel(Pattern pattern, Expression guard = null) : SwitchLabel
{
    public Pattern Pattern { get; } = pattern;
    public Expression Guard { get; } = guard;
}

public sealed class DefaultSwitchLabel : SwitchLabel
{
}

public sealed class WhileStatement(Expression condition, Statement body) : Statement
{
    public Expression Condition { get; } = condition;
    public Statement Body { get; } = body;
}

public sealed class DoStatement(Statement body, Expression condition) : Statement
{
    public Statement Body { get; } = body;
    public Expression Condition { get; } = condition;
}

public sealed class ForStatement(
    Statement body,
    IReadOnlyList<Statement> initializers = null,
    Expression condition = null,
    IReadOnlyList<Expression> iterators = null) : Statement
{
    public IReadOnlyList<Statement> Initializers { get; } = initializers;
    public Expression Condition { get; } = condition;
    public IReadOnlyList<Expression> Iterators { get; } = iterators;
    public Statement Body { get; } = body;
}

public sealed class ForEachStatement(
    TypeReference type,
    string identifier,
    Expression collection,
    Statement body,
    bool isAwait = false) : Statement
{
    public TypeReference Type { get; } = type;
    public string Identifier { get; } = identifier;
    public Expression Collection { get; } = collection;
    public Statement Body { get; } = body;
    public bool IsAwait { get; } = isAwait;
}

public sealed class BreakStatement : Statement
{
}

public sealed class ContinueStatement : Statement
{
}

public sealed class ReturnStatement(Expression expression = null) : Statement
{
    public Expression Expression { get; } = expression;
}

public sealed class ThrowStatement(Expression expression = null) : Statement
{
    public Expression Expression { get; } = expression;
}

public sealed class TryStatement(
    BlockStatement block,
    IReadOnlyList<CatchClause> catchClauses = null,
    BlockStatement finallyBlock = null) : Statement
{
    public BlockStatement Block { get; } = block;
    public IReadOnlyList<CatchClause> CatchClauses { get; } = catchClauses;
    public BlockStatement FinallyBlock { get; } = finallyBlock;
}

public sealed class CatchClause(
    BlockStatement block,
    TypeReference exceptionType = null,
    string identifier = null,
    Expression filter = null) : ICSharpNode
{
    public TypeReference ExceptionType { get; } = exceptionType;
    public string Identifier { get; } = identifier;
    public Expression Filter { get; } = filter;
    public BlockStatement Block { get; } = block;
}

public sealed class UsingStatement(Statement resourceAcquisition, Statement body, bool isAwait = false) : Statement
{
    public Statement ResourceAcquisition { get; } = resourceAcquisition;
    public Statement Body { get; } = body;
    public bool IsAwait { get; } = isAwait;
}

public sealed class LockStatement(Expression expression, Statement body) : Statement
{
    public Expression Expression { get; } = expression;
    public Statement Body { get; } = body;
}

public sealed class YieldReturnStatement(Expression expression) : Statement
{
    public Expression Expression { get; } = expression;
}

public sealed class YieldBreakStatement : Statement
{
}

public sealed class LabeledStatement(string label, Statement statement) : Statement
{
    public string Label { get; } = label;
    public Statement Statement { get; } = statement;
}

public sealed class GotoStatement(string label) : Statement
{
    public string Label { get; } = label;
}

// ========================================
// Expressions
// ========================================

public abstract class Expression : ICSharpNode
{
}

public sealed class LiteralExpression(object value, LiteralKind kind) : Expression
{
    public object Value { get; } = value;
    public LiteralKind Kind { get; } = kind;
}

public enum LiteralKind
{
    Null,
    Boolean,
    Integer,
    Real,
    Character,
    String
}

public sealed class NameExpression(IReadOnlyList<string> parts, IReadOnlyList<TypeReference> typeArguments = null) : Expression
{
    public IReadOnlyList<string> Parts { get; } = parts;
    public IReadOnlyList<TypeReference> TypeArguments { get; } = typeArguments;
}

public sealed class BinaryExpression(Expression left, BinaryOperator op, Expression right) : Expression
{
    public Expression Left { get; } = left;
    public BinaryOperator Operator { get; } = op;
    public Expression Right { get; } = right;
}

public enum BinaryOperator
{
    // Arithmetic
    Add, Subtract, Multiply, Divide, Modulo,
    // Logical
    And, Or,
    // Bitwise
    BitwiseAnd, BitwiseOr, BitwiseXor, LeftShift, RightShift, UnsignedRightShift,
    // Comparison
    Equal, NotEqual, LessThan, LessThanOrEqual, GreaterThan, GreaterThanOrEqual,
    // Assignment
    Assign, AddAssign, SubtractAssign, MultiplyAssign, DivideAssign, ModuloAssign,
    BitwiseAndAssign, BitwiseOrAssign, BitwiseXorAssign, LeftShiftAssign, RightShiftAssign, UnsignedRightShiftAssign,
    // Other
    NullCoalescing, NullCoalescingAssign
}

public sealed class UnaryExpression(UnaryOperator op, Expression operand, bool isPrefix = true) : Expression
{
    public UnaryOperator Operator { get; } = op;
    public Expression Operand { get; } = operand;
    public bool IsPrefix { get; } = isPrefix;
}

public enum UnaryOperator
{
    Plus, Minus, Not, BitwiseNot,
    Increment, Decrement,
    AddressOf, Dereference, Index
}

public sealed class ConditionalExpression(Expression condition, Expression trueExpr, Expression falseExpr) : Expression
{
    public Expression Condition { get; } = condition;
    public Expression TrueExpression { get; } = trueExpr;
    public Expression FalseExpression { get; } = falseExpr;
}

public sealed class InvocationExpression(Expression expression, IReadOnlyList<Argument> arguments = null) : Expression
{
    public Expression Expression { get; } = expression;
    public IReadOnlyList<Argument> Arguments { get; } = arguments;
}

public sealed class Argument(Expression expression, string name = null, RefKind refKind = RefKind.None) : ICSharpNode
{
    public string Name { get; } = name;
    public RefKind RefKind { get; } = refKind;
    public Expression Expression { get; } = expression;
}

public enum RefKind
{
    None,
    Ref,
    Out,
    In
}

public sealed class MemberAccessExpression(string memberName, Expression target = null, bool isConditional = false) : Expression
{
    public Expression Target { get; } = target;
    public string MemberName { get; } = memberName;
    public bool IsConditional { get; } = isConditional;
}

public sealed class ElementAccessExpression(Expression target, IReadOnlyList<Argument> arguments, bool isConditional = false) : Expression
{
    public Expression Target { get; } = target;
    public IReadOnlyList<Argument> Arguments { get; } = arguments;
    public bool IsConditional { get; } = isConditional;
}

public sealed class ObjectCreationExpression(
    TypeReference type,
    IReadOnlyList<Argument> arguments = null,
    ObjectInitializer initializer = null) : Expression
{
    public TypeReference Type { get; } = type;
    public IReadOnlyList<Argument> Arguments { get; } = arguments;
    public ObjectInitializer Initializer { get; } = initializer;
}

public sealed class ObjectInitializer(IReadOnlyList<MemberInitializer> members) : ICSharpNode
{
    public IReadOnlyList<MemberInitializer> Members { get; } = members;
}

public sealed class MemberInitializer(string name, Expression value) : ICSharpNode
{
    public string Name { get; } = name;
    public Expression Value { get; } = value;
}

public sealed class ArrayCreationExpression(
    TypeReference elementType,
    IReadOnlyList<Expression> sizes = null,
    ArrayInitializer initializer = null) : Expression
{
    public TypeReference ElementType { get; } = elementType;
    public IReadOnlyList<Expression> Sizes { get; } = sizes;
    public ArrayInitializer Initializer { get; } = initializer;
}

public sealed class ArrayInitializer(IReadOnlyList<Expression> elements) : ICSharpNode
{
    public IReadOnlyList<Expression> Elements { get; } = elements;
}

public sealed class CastExpression(TypeReference type, Expression expression) : Expression
{
    public TypeReference Type { get; } = type;
    public Expression Expression { get; } = expression;
}

public sealed class IsExpression(Expression expression, Pattern pattern) : Expression
{
    public Expression Expression { get; } = expression;
    public Pattern Pattern { get; } = pattern;
}

public sealed class AsExpression(Expression expression, TypeReference type) : Expression
{
    public Expression Expression { get; } = expression;
    public TypeReference Type { get; } = type;
}

public sealed class LambdaExpression(LambdaBody body, IReadOnlyList<Parameter> parameters = null, bool isAsync = false) : Expression
{
    public IReadOnlyList<Parameter> Parameters { get; } = parameters;
    public LambdaBody Body { get; } = body;
    public bool IsAsync { get; } = isAsync;
}

public abstract class LambdaBody : ICSharpNode
{
}

public sealed class ExpressionLambdaBody(Expression expression) : LambdaBody
{
    public Expression Expression { get; } = expression;
}

public sealed class BlockLambdaBody(BlockStatement block) : LambdaBody
{
    public BlockStatement Block { get; } = block;
}

public sealed class QueryExpression(
    FromClause fromClause,
    IReadOnlyList<QueryClause> bodyClauses,
    SelectOrGroupClause selectOrGroupClause) : Expression
{
    public FromClause FromClause { get; } = fromClause;
    public IReadOnlyList<QueryClause> BodyClauses { get; } = bodyClauses;
    public SelectOrGroupClause SelectOrGroupClause { get; } = selectOrGroupClause;
}

public sealed class FromClause(string identifier, Expression expression, TypeReference type = null) : QueryClause
{
    public TypeReference Type { get; } = type;
    public string Identifier { get; } = identifier;
    public Expression Expression { get; } = expression;
}

public abstract class QueryClause : ICSharpNode
{
}

public sealed class JoinClause(
    string identifier,
    Expression inExpression,
    Expression leftExpression,
    Expression rightExpression,
    TypeReference type = null,
    string intoIdentifier = null) : QueryClause
{
    public TypeReference Type { get; } = type;
    public string Identifier { get; } = identifier;
    public Expression InExpression { get; } = inExpression;
    public Expression LeftExpression { get; } = leftExpression;
    public Expression RightExpression { get; } = rightExpression;
    public string IntoIdentifier { get; } = intoIdentifier;
}

public sealed class LetClause(string identifier, Expression expression) : QueryClause
{
    public string Identifier { get; } = identifier;
    public Expression Expression { get; } = expression;
}

public sealed class WhereClause(Expression condition) : QueryClause
{
    public Expression Condition { get; } = condition;
}

public sealed class OrderByClause(IReadOnlyList<Ordering> orderings) : QueryClause
{
    public IReadOnlyList<Ordering> Orderings { get; } = orderings;
}

public sealed class Ordering(Expression expression, OrderDirection direction = OrderDirection.Ascending) : ICSharpNode
{
    public Expression Expression { get; } = expression;
    public OrderDirection Direction { get; } = direction;
}

public abstract class SelectOrGroupClause : ICSharpNode
{
}

public sealed class SelectClause(Expression expression) : SelectOrGroupClause
{
    public Expression Expression { get; } = expression;
}

public sealed class GroupClause(Expression groupExpression, Expression byExpression) : SelectOrGroupClause
{
    public Expression GroupExpression { get; } = groupExpression;
    public Expression ByExpression { get; } = byExpression;
}

public sealed class QueryContinuation(
    string identifier,
    IReadOnlyList<QueryClause> bodyClauses,
    SelectOrGroupClause selectOrGroupClause) : ICSharpNode
{
    public string Identifier { get; } = identifier;
    public IReadOnlyList<QueryClause> BodyClauses { get; } = bodyClauses;
    public SelectOrGroupClause SelectOrGroupClause { get; } = selectOrGroupClause;
}

public enum OrderDirection
{
    Ascending,
    Descending
}

public sealed class SwitchExpression(Expression governingExpression, IReadOnlyList<SwitchExpressionArm> arms) : Expression
{
    public Expression GoverningExpression { get; } = governingExpression;
    public IReadOnlyList<SwitchExpressionArm> Arms { get; } = arms;
}

public sealed class SwitchExpressionArm(Pattern pattern, Expression expression, Expression guard = null) : ICSharpNode
{
    public Pattern Pattern { get; } = pattern;
    public Expression Guard { get; } = guard;
    public Expression Expression { get; } = expression;
}

public sealed class ThrowExpression(Expression expression) : Expression
{
    public Expression Expression { get; } = expression;
}

public sealed class DefaultExpression(TypeReference type = null) : Expression
{
    public TypeReference Type { get; } = type;
}

public sealed class TypeOfExpression(TypeReference type) : Expression
{
    public TypeReference Type { get; } = type;
}

public sealed class SizeOfExpression(TypeReference type) : Expression
{
    public TypeReference Type { get; } = type;
}

public sealed class NameOfExpression(Expression expression) : Expression
{
    public Expression Expression { get; } = expression;
}

public sealed class AwaitExpression(Expression expression) : Expression
{
    public Expression Expression { get; } = expression;
}

public sealed class ParenthesizedExpression(Expression expression) : Expression
{
    public Expression Expression { get; } = expression;
}

public sealed class TupleExpression(IReadOnlyList<TupleExpressionElement> elements) : Expression
{
    public IReadOnlyList<TupleExpressionElement> Elements { get; } = elements;
}

public sealed class TupleExpressionElement(Expression expression, string name = null) : ICSharpNode
{
    public string Name { get; } = name;
    public Expression Expression { get; } = expression;
}

public sealed class RangeExpression(Expression start = null, Expression end = null) : Expression
{
    public Expression Start { get; } = start;
    public Expression End { get; } = end;
}

public sealed class WithExpression(Expression expression, ObjectInitializer initializer) : Expression
{
    public Expression Expression { get; } = expression;
    public ObjectInitializer Initializer { get; } = initializer;
}

// ========================================
// Patterns
// ========================================

public abstract class Pattern : ICSharpNode
{
}

public sealed class TypePattern(TypeReference type) : Pattern
{
    public TypeReference Type { get; } = type;
}

public sealed class ConstantPattern(Expression expression) : Pattern
{
    public Expression Expression { get; } = expression;
}

public sealed class VarPattern(string identifier) : Pattern
{
    public string Identifier { get; } = identifier;
}

public sealed class DiscardPattern : Pattern
{
}

public sealed class DeclarationPattern(TypeReference type, string identifier = null) : Pattern
{
    public TypeReference Type { get; } = type;
    public string Identifier { get; } = identifier;
}

public sealed class RecursivePattern(
    TypeReference type = null,
    IReadOnlyList<SubPattern> positionalPatterns = null,
    IReadOnlyList<PropertySubPattern> propertyPatterns = null,
    string designation = null) : Pattern
{
    public TypeReference Type { get; } = type;
    public IReadOnlyList<SubPattern> PositionalPatterns { get; } = positionalPatterns;
    public IReadOnlyList<PropertySubPattern> PropertyPatterns { get; } = propertyPatterns;
    public string Designation { get; } = designation;
}

public sealed class SubPattern(Pattern pattern) : ICSharpNode
{
    public Pattern Pattern { get; } = pattern;
}

public sealed class PropertySubPattern(string propertyName, Pattern pattern) : ICSharpNode
{
    public string PropertyName { get; } = propertyName;
    public Pattern Pattern { get; } = pattern;
}

public sealed class RelationalPattern(RelationalOperator op, Expression expression) : Pattern
{
    public RelationalOperator Operator { get; } = op;
    public Expression Expression { get; } = expression;
}

public enum RelationalOperator
{
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual
}

public sealed class LogicalPattern(LogicalPatternKind kind, Pattern left, Pattern right = null) : Pattern
{
    public LogicalPatternKind Kind { get; } = kind;
    public Pattern Left { get; } = left;
    public Pattern Right { get; } = right;
}

public enum LogicalPatternKind
{
    And,
    Or,
    Not
}

// ========================================
// Attributes
// ========================================

public sealed class AttributeSection(IReadOnlyList<AttributeNode> attributes, AttributeTarget? target = null) : ICSharpNode
{
    public AttributeTarget? Target { get; } = target;
    public IReadOnlyList<AttributeNode> Attributes { get; } = attributes;
}

public enum AttributeTarget
{
    Assembly,
    Module,
    Field,
    Event,
    Method,
    Param,
    Property,
    Return,
    Type
}

public sealed class AttributeNode(NameExpression name, IReadOnlyList<Argument> arguments = null) : ICSharpNode
{
    public NameExpression Name { get; } = name;
    public IReadOnlyList<Argument> Arguments { get; } = arguments;
}

