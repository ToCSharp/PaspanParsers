namespace PaspanParsers.Java;

// ========================================
// Base Interfaces
// ========================================

/// <summary>
/// Base interface for all Java AST nodes
/// </summary>
public interface IJavaNode
{
}

// ========================================
// Compilation Unit
// ========================================

public sealed class CompilationUnit(
    PackageDeclaration packageDeclaration = null,
    IReadOnlyList<ImportDeclaration> importDeclarations = null,
    IReadOnlyList<TypeDeclaration> typeDeclarations = null) : IJavaNode
{
    public PackageDeclaration PackageDeclaration { get; } = packageDeclaration;
    public IReadOnlyList<ImportDeclaration> ImportDeclarations { get; } = importDeclarations;
    public IReadOnlyList<TypeDeclaration> TypeDeclarations { get; } = typeDeclarations;
}

// ========================================
// Package Declaration
// ========================================

public sealed class PackageDeclaration(QualifiedName name, IReadOnlyList<Annotation> annotations = null) : IJavaNode
{
    public IReadOnlyList<Annotation> Annotations { get; } = annotations;
    public QualifiedName Name { get; } = name;
}

public sealed class QualifiedName(IReadOnlyList<string> parts) : IJavaNode
{
    public IReadOnlyList<string> Parts { get; } = parts;

    public override string ToString() => string.Join(".", Parts);
}

// ========================================
// Import Declarations
// ========================================

public abstract class ImportDeclaration(bool isStatic) : IJavaNode
{
    public bool IsStatic { get; } = isStatic;
}

public sealed class SingleTypeImportDeclaration(QualifiedName typeName, bool isStatic = false) : ImportDeclaration(isStatic)
{
    public QualifiedName TypeName { get; } = typeName;
}

public sealed class TypeImportOnDemandDeclaration(QualifiedName packageOrTypeName, bool isStatic = false) : ImportDeclaration(isStatic)
{
    public QualifiedName PackageOrTypeName { get; } = packageOrTypeName;
}

// ========================================
// Modifiers
// ========================================

[Flags]
public enum Modifiers
{
    None = 0,
    Public = 1 << 0,
    Protected = 1 << 1,
    Private = 1 << 2,
    Static = 1 << 3,
    Final = 1 << 4,
    Abstract = 1 << 5,
    Synchronized = 1 << 6,
    Volatile = 1 << 7,
    Transient = 1 << 8,
    Native = 1 << 9,
    Strictfp = 1 << 10,
    Default = 1 << 11,      // Interface default methods (Java 8)
    Sealed = 1 << 12,       // Sealed classes (Java 17)
    NonSealed = 1 << 13,    // Non-sealed classes (Java 17)
}

// ========================================
// Type Declarations
// ========================================

public abstract class TypeDeclaration(
    string name,
    IReadOnlyList<Annotation> annotations,
    Modifiers modifiers) : IJavaNode
{
    public IReadOnlyList<Annotation> Annotations { get; } = annotations;
    public Modifiers Modifiers { get; } = modifiers;
    public string Name { get; } = name;
}

// ========================================
// Class Declaration
// ========================================

public sealed class ClassDeclaration(
    string name,
    IReadOnlyList<Annotation> annotations = null,
    Modifiers modifiers = Modifiers.None,
    IReadOnlyList<TypeParameter> typeParameters = null,
    TypeReference superClass = null,
    IReadOnlyList<TypeReference> superInterfaces = null,
    IReadOnlyList<TypeReference> permittedSubclasses = null,
    IReadOnlyList<MemberDeclaration> members = null) : TypeDeclaration(name, annotations, modifiers)
{
    public IReadOnlyList<TypeParameter> TypeParameters { get; } = typeParameters;
    public TypeReference SuperClass { get; } = superClass;
    public IReadOnlyList<TypeReference> SuperInterfaces { get; } = superInterfaces;
    public IReadOnlyList<TypeReference> PermittedSubclasses { get; } = permittedSubclasses;
    public IReadOnlyList<MemberDeclaration> Members { get; } = members;
}

// ========================================
// Interface Declaration
// ========================================

public sealed class InterfaceDeclaration(
    string name,
    IReadOnlyList<Annotation> annotations = null,
    Modifiers modifiers = Modifiers.None,
    IReadOnlyList<TypeParameter> typeParameters = null,
    IReadOnlyList<TypeReference> extendsInterfaces = null,
    IReadOnlyList<TypeReference> permittedSubtypes = null,
    IReadOnlyList<MemberDeclaration> members = null) : TypeDeclaration(name, annotations, modifiers)
{
    public IReadOnlyList<TypeParameter> TypeParameters { get; } = typeParameters;
    public IReadOnlyList<TypeReference> ExtendsInterfaces { get; } = extendsInterfaces;
    public IReadOnlyList<TypeReference> PermittedSubtypes { get; } = permittedSubtypes;
    public IReadOnlyList<MemberDeclaration> Members { get; } = members;
}

// ========================================
// Enum Declaration
// ========================================

public sealed class EnumDeclaration(
    string name,
    IReadOnlyList<Annotation> annotations = null,
    Modifiers modifiers = Modifiers.None,
    IReadOnlyList<TypeReference> superInterfaces = null,
    IReadOnlyList<EnumConstant> constants = null,
    IReadOnlyList<MemberDeclaration> members = null) : TypeDeclaration(name, annotations, modifiers)
{
    public IReadOnlyList<TypeReference> SuperInterfaces { get; } = superInterfaces;
    public IReadOnlyList<EnumConstant> Constants { get; } = constants;
    public IReadOnlyList<MemberDeclaration> Members { get; } = members;
}

public sealed class EnumConstant(
    string name,
    IReadOnlyList<Annotation> annotations = null,
    IReadOnlyList<Expression> arguments = null,
    IReadOnlyList<MemberDeclaration> body = null) : IJavaNode
{
    public IReadOnlyList<Annotation> Annotations { get; } = annotations;
    public string Name { get; } = name;
    public IReadOnlyList<Expression> Arguments { get; } = arguments;
    public IReadOnlyList<MemberDeclaration> Body { get; } = body;
}

// ========================================
// Annotation Type Declaration
// ========================================

public sealed class AnnotationDeclaration(
    string name,
    IReadOnlyList<Annotation> annotations = null,
    Modifiers modifiers = Modifiers.None,
    IReadOnlyList<AnnotationMemberDeclaration> members = null) : TypeDeclaration(name, annotations, modifiers)
{
    public IReadOnlyList<AnnotationMemberDeclaration> Members { get; } = members;
}

public sealed class AnnotationMemberDeclaration(
    TypeReference type,
    string name,
    IReadOnlyList<Annotation> annotations = null,
    Modifiers modifiers = Modifiers.None,
    Expression defaultValue = null) : IJavaNode
{
    public IReadOnlyList<Annotation> Annotations { get; } = annotations;
    public Modifiers Modifiers { get; } = modifiers;
    public TypeReference Type { get; } = type;
    public string Name { get; } = name;
    public Expression DefaultValue { get; } = defaultValue;
}

// ========================================
// Record Declaration (Java 16+)
// ========================================

public sealed class RecordDeclaration(
    string name,
    IReadOnlyList<RecordComponent> components,
    IReadOnlyList<Annotation> annotations = null,
    Modifiers modifiers = Modifiers.None,
    IReadOnlyList<TypeParameter> typeParameters = null,
    IReadOnlyList<TypeReference> superInterfaces = null,
    IReadOnlyList<MemberDeclaration> members = null) : TypeDeclaration(name, annotations, modifiers)
{
    public IReadOnlyList<TypeParameter> TypeParameters { get; } = typeParameters;
    public IReadOnlyList<RecordComponent> Components { get; } = components;
    public IReadOnlyList<TypeReference> SuperInterfaces { get; } = superInterfaces;
    public IReadOnlyList<MemberDeclaration> Members { get; } = members;
}

public sealed class RecordComponent(TypeReference type, string name, IReadOnlyList<Annotation> annotations = null) : IJavaNode
{
    public IReadOnlyList<Annotation> Annotations { get; } = annotations;
    public TypeReference Type { get; } = type;
    public string Name { get; } = name;
}

// ========================================
// Type Parameters
// ========================================

public sealed class TypeParameter(string name, IReadOnlyList<TypeReference> bounds = null, IReadOnlyList<Annotation> annotations = null) : IJavaNode
{
    public IReadOnlyList<Annotation> Annotations { get; } = annotations;
    public string Name { get; } = name;
    public IReadOnlyList<TypeReference> Bounds { get; } = bounds;
}

// ========================================
// Member Declarations
// ========================================

public abstract class MemberDeclaration(IReadOnlyList<Annotation> annotations, Modifiers modifiers) : IJavaNode
{
    public IReadOnlyList<Annotation> Annotations { get; } = annotations;
    public Modifiers Modifiers { get; } = modifiers;
}

// ========================================
// Field Declaration
// ========================================

public sealed class FieldDeclaration(
    TypeReference type,
    IReadOnlyList<VariableDeclarator> variables,
    IReadOnlyList<Annotation> annotations = null,
    Modifiers modifiers = Modifiers.None) : MemberDeclaration(annotations, modifiers)
{
    public TypeReference Type { get; } = type;
    public IReadOnlyList<VariableDeclarator> Variables { get; } = variables;
}

public sealed class VariableDeclarator(string name, int arrayDimensions = 0, Expression initializer = null) : IJavaNode
{
    public string Name { get; } = name;
    public int ArrayDimensions { get; } = arrayDimensions;
    public Expression Initializer { get; } = initializer;
}

// ========================================
// Method Declaration
// ========================================

public sealed class MethodDeclaration(
    TypeReference returnType,
    string name,
    IReadOnlyList<Annotation> annotations = null,
    Modifiers modifiers = Modifiers.None,
    IReadOnlyList<TypeParameter> typeParameters = null,
    IReadOnlyList<Parameter> parameters = null,
    int arrayDimensions = 0,
    IReadOnlyList<TypeReference> throwsClause = null,
    BlockStatement body = null) : MemberDeclaration(annotations, modifiers)
{
    public IReadOnlyList<TypeParameter> TypeParameters { get; } = typeParameters;
    public TypeReference ReturnType { get; } = returnType;
    public string Name { get; } = name;
    public IReadOnlyList<Parameter> Parameters { get; } = parameters;
    public int ArrayDimensions { get; } = arrayDimensions;
    public IReadOnlyList<TypeReference> ThrowsClause { get; } = throwsClause;
    public BlockStatement Body { get; } = body;
}

public sealed class Parameter(
    TypeReference type,
    string name,
    IReadOnlyList<Annotation> annotations = null,
    bool isFinal = false,
    bool isVarArgs = false,
    int arrayDimensions = 0) : IJavaNode
{
    public IReadOnlyList<Annotation> Annotations { get; } = annotations;
    public bool IsFinal { get; } = isFinal;
    public TypeReference Type { get; } = type;
    public bool IsVarArgs { get; } = isVarArgs;
    public string Name { get; } = name;
    public int ArrayDimensions { get; } = arrayDimensions;
}

// ========================================
// Constructor Declaration
// ========================================

public sealed class ConstructorDeclaration(
    string name,
    IReadOnlyList<Annotation> annotations = null,
    Modifiers modifiers = Modifiers.None,
    IReadOnlyList<TypeParameter> typeParameters = null,
    IReadOnlyList<Parameter> parameters = null,
    IReadOnlyList<TypeReference> throwsClause = null,
    BlockStatement body = null,
    ConstructorInvocation explicitConstructorInvocation = null) : MemberDeclaration(annotations, modifiers)
{
    public IReadOnlyList<TypeParameter> TypeParameters { get; } = typeParameters;
    public string Name { get; } = name;
    public IReadOnlyList<Parameter> Parameters { get; } = parameters;
    public IReadOnlyList<TypeReference> ThrowsClause { get; } = throwsClause;
    public BlockStatement Body { get; } = body;
    public ConstructorInvocation ExplicitConstructorInvocation { get; } = explicitConstructorInvocation;
}

public sealed class ConstructorInvocation(
    bool isSuper,
    IReadOnlyList<Expression> arguments,
    IReadOnlyList<TypeReference> typeArguments = null) : IJavaNode
{
    public bool IsSuper { get; } = isSuper;
    public IReadOnlyList<TypeReference> TypeArguments { get; } = typeArguments;
    public IReadOnlyList<Expression> Arguments { get; } = arguments;
}

// ========================================
// Initializer Block
// ========================================

public sealed class InitializerBlock(BlockStatement block, bool isStatic = false) : MemberDeclaration(null, isStatic ? Modifiers.Static : Modifiers.None)
{
    public bool IsStatic { get; } = isStatic;
    public BlockStatement Block { get; } = block;
}

// ========================================
// Type References
// ========================================

public abstract class TypeReference : IJavaNode
{
}

public sealed class PrimitiveTypeReference(PrimitiveType type, IReadOnlyList<Annotation> annotations = null) : TypeReference
{
    public IReadOnlyList<Annotation> Annotations { get; } = annotations;
    public PrimitiveType Type { get; } = type;
}

public enum PrimitiveType
{
    Boolean,
    Byte,
    Short,
    Int,
    Long,
    Char,
    Float,
    Double
}

public sealed class ReferenceTypeReference(
    QualifiedName name,
    IReadOnlyList<TypeArgument> typeArguments = null,
    IReadOnlyList<Annotation> annotations = null) : TypeReference
{
    public IReadOnlyList<Annotation> Annotations { get; } = annotations;
    public QualifiedName Name { get; } = name;
    public IReadOnlyList<TypeArgument> TypeArguments { get; } = typeArguments;

    // Convenience constructor for simple names
    public ReferenceTypeReference(string name, IReadOnlyList<TypeArgument> typeArguments = null)
        : this(new QualifiedName([name]), typeArguments)
    {
    }
}

public sealed class ArrayTypeReference(TypeReference elementType, IReadOnlyList<Annotation> annotations = null) : TypeReference
{
    public TypeReference ElementType { get; } = elementType;
    public IReadOnlyList<Annotation> Annotations { get; } = annotations;
}

public sealed class VarTypeReference : TypeReference
{
    // Local variable type inference (Java 10+)
}

public sealed class VoidTypeReference : TypeReference
{
    // void return type for methods
}

// ========================================
// Type Arguments (Generics)
// ========================================

public abstract class TypeArgument : IJavaNode
{
}

public sealed class ReferenceTypeArgument(TypeReference type) : TypeArgument
{
    public TypeReference Type { get; } = type;
}

public sealed class WildcardTypeArgument(WildcardBounds bounds = null, IReadOnlyList<Annotation> annotations = null) : TypeArgument
{
    public IReadOnlyList<Annotation> Annotations { get; } = annotations;
    public WildcardBounds Bounds { get; } = bounds;
}

public abstract class WildcardBounds : IJavaNode
{
}

public sealed class ExtendsWildcardBounds(TypeReference type) : WildcardBounds
{
    public TypeReference Type { get; } = type;
}

public sealed class SuperWildcardBounds(TypeReference type) : WildcardBounds
{
    public TypeReference Type { get; } = type;
}

// ========================================
// Annotations
// ========================================

public sealed class Annotation(QualifiedName name, IReadOnlyList<AnnotationElement> elements = null) : IJavaNode
{
    public QualifiedName Name { get; } = name;
    public IReadOnlyList<AnnotationElement> Elements { get; } = elements;

    // Convenience constructor for simple names
    public Annotation(string name, IReadOnlyList<AnnotationElement> elements = null)
        : this(new QualifiedName([name]), elements)
    {
    }
}

public sealed class AnnotationElement(Expression value, string name = null) : IJavaNode
{
    public string Name { get; } = name;
    public Expression Value { get; } = value;
}

// ========================================
// Statements
// ========================================

public abstract class Statement : IJavaNode
{
}

public sealed class BlockStatement(IReadOnlyList<Statement> statements = null) : Statement
{
    public IReadOnlyList<Statement> Statements { get; } = statements;
}

public sealed class EmptyStatement : Statement
{
}

public sealed class ExpressionStatement(Expression expression) : Statement
{
    public Expression Expression { get; } = expression;
}

// ========================================
// Local Variable Declaration
// ========================================

public sealed class LocalVariableDeclarationStatement(
    TypeReference type,
    IReadOnlyList<VariableDeclarator> variables,
    IReadOnlyList<Annotation> annotations = null,
    bool isFinal = false) : Statement
{
    public IReadOnlyList<Annotation> Annotations { get; } = annotations;
    public bool IsFinal { get; } = isFinal;
    public TypeReference Type { get; } = type;
    public IReadOnlyList<VariableDeclarator> Variables { get; } = variables;
}

// ========================================
// Control Flow Statements
// ========================================

public sealed class IfStatement(Expression condition, Statement thenStatement, Statement elseStatement = null) : Statement
{
    public Expression Condition { get; } = condition;
    public Statement ThenStatement { get; } = thenStatement;
    public Statement ElseStatement { get; } = elseStatement;
}

public sealed class AssertStatement(Expression condition, Expression message = null) : Statement
{
    public Expression Condition { get; } = condition;
    public Expression Message { get; } = message;
}

public sealed class SwitchStatement(Expression expression, IReadOnlyList<SwitchBlockStatementGroup> blockGroups = null) : Statement
{
    public Expression Expression { get; } = expression;
    public IReadOnlyList<SwitchBlockStatementGroup> BlockGroups { get; } = blockGroups;
}

public sealed class SwitchBlockStatementGroup(IReadOnlyList<SwitchLabel> labels, IReadOnlyList<Statement> statements) : IJavaNode
{
    public IReadOnlyList<SwitchLabel> Labels { get; } = labels;
    public IReadOnlyList<Statement> Statements { get; } = statements;
}

public abstract class SwitchLabel : IJavaNode
{
}

public sealed class CaseSwitchLabel(Expression expression) : SwitchLabel
{
    public Expression Expression { get; } = expression;
}

public sealed class CasePatternSwitchLabel(Pattern pattern, Expression guard = null) : SwitchLabel
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
    IReadOnlyList<Expression> updates = null) : Statement
{
    public IReadOnlyList<Statement> Initializers { get; } = initializers;
    public Expression Condition { get; } = condition;
    public IReadOnlyList<Expression> Updates { get; } = updates;
    public Statement Body { get; } = body;
}

public sealed class EnhancedForStatement(
    TypeReference type,
    string identifier,
    Expression expression,
    Statement body,
    IReadOnlyList<Annotation> annotations = null,
    bool isFinal = false) : Statement
{
    public IReadOnlyList<Annotation> Annotations { get; } = annotations;
    public bool IsFinal { get; } = isFinal;
    public TypeReference Type { get; } = type;
    public string Identifier { get; } = identifier;
    public Expression Expression { get; } = expression;
    public Statement Body { get; } = body;
}

public sealed class BreakStatement(string label = null) : Statement
{
    public string Label { get; } = label;
}

public sealed class ContinueStatement(string label = null) : Statement
{
    public string Label { get; } = label;
}

public sealed class ReturnStatement(Expression expression = null) : Statement
{
    public Expression Expression { get; } = expression;
}

public sealed class ThrowStatement(Expression expression) : Statement
{
    public Expression Expression { get; } = expression;
}

public sealed class SynchronizedStatement(Expression expression, BlockStatement block) : Statement
{
    public Expression Expression { get; } = expression;
    public BlockStatement Block { get; } = block;
}

public sealed class TryStatement(
    BlockStatement block,
    IReadOnlyList<Resource> resources = null,
    IReadOnlyList<CatchClause> catchClauses = null,
    BlockStatement finallyBlock = null) : Statement
{
    public IReadOnlyList<Resource> Resources { get; } = resources;
    public BlockStatement Block { get; } = block;
    public IReadOnlyList<CatchClause> CatchClauses { get; } = catchClauses;
    public BlockStatement FinallyBlock { get; } = finallyBlock;
}

public sealed class Resource(
    TypeReference type,
    string name,
    Expression initializer,
    IReadOnlyList<Annotation> annotations = null,
    bool isFinal = false) : IJavaNode
{
    public IReadOnlyList<Annotation> Annotations { get; } = annotations;
    public bool IsFinal { get; } = isFinal;
    public TypeReference Type { get; } = type;
    public string Name { get; } = name;
    public Expression Initializer { get; } = initializer;
}

public sealed class CatchClause(
    IReadOnlyList<TypeReference> exceptionTypes,
    string identifier,
    BlockStatement block,
    IReadOnlyList<Annotation> annotations = null,
    bool isFinal = false) : IJavaNode
{
    public IReadOnlyList<Annotation> Annotations { get; } = annotations;
    public bool IsFinal { get; } = isFinal;
    public IReadOnlyList<TypeReference> ExceptionTypes { get; } = exceptionTypes;
    public string Identifier { get; } = identifier;
    public BlockStatement Block { get; } = block;
}

public sealed class LabeledStatement(string label, Statement statement) : Statement
{
    public string Label { get; } = label;
    public Statement Statement { get; } = statement;
}

public sealed class YieldStatement(Expression expression) : Statement
{
    public Expression Expression { get; } = expression;
}

// ========================================
// Switch Expression (Java 14+)
// ========================================

public sealed class SwitchExpression(Expression switchExpression, IReadOnlyList<SwitchRule> rules) : Expression
{
    public Expression SwitchExpression_ { get; } = switchExpression;
    public IReadOnlyList<SwitchRule> Rules { get; } = rules;
}

public sealed class SwitchRule(SwitchLabel label, SwitchRuleBody body) : IJavaNode
{
    public SwitchLabel Label { get; } = label;
    public SwitchRuleBody Body { get; } = body;
}

public abstract class SwitchRuleBody : IJavaNode
{
}

public sealed class ExpressionSwitchRuleBody(Expression expression) : SwitchRuleBody
{
    public Expression Expression { get; } = expression;
}

public sealed class BlockSwitchRuleBody(BlockStatement block) : SwitchRuleBody
{
    public BlockStatement Block { get; } = block;
}

public sealed class ThrowSwitchRuleBody(Expression expression) : SwitchRuleBody
{
    public Expression Expression { get; } = expression;
}

// ========================================
// Expressions
// ========================================

public abstract class Expression : IJavaNode
{
}

// ========================================
// Literals
// ========================================

public sealed class LiteralExpression(object value, LiteralKind kind) : Expression
{
    public object Value { get; } = value;
    public LiteralKind Kind { get; } = kind;
}

public enum LiteralKind
{
    Integer,
    FloatingPoint,
    Boolean,
    Character,
    String,
    TextBlock,      // Java 15+
    Null
}

// ========================================
// Primary Expressions
// ========================================

public sealed class ThisExpression(QualifiedName qualifier = null) : Expression
{
    public QualifiedName Qualifier { get; } = qualifier;
}

public sealed class ParenthesizedExpression(Expression expression) : Expression
{
    public Expression Expression { get; } = expression;
}

public sealed class ClassLiteralExpression(TypeReference type) : Expression
{
    public TypeReference Type { get; } = type;
}

public sealed class NameExpression(QualifiedName name) : Expression
{
    public QualifiedName Name { get; } = name;

    // Convenience constructor for simple names
    public NameExpression(string name)
        : this(new QualifiedName([name]))
    {
    }
}

// ========================================
// Object Creation
// ========================================

public sealed class NewObjectExpression(
    TypeReference type,
    IReadOnlyList<Expression> arguments = null,
    IReadOnlyList<TypeArgument> typeArguments = null,
    Expression qualifier = null,
    IReadOnlyList<MemberDeclaration> anonymousClassBody = null) : Expression
{
    public Expression Qualifier { get; } = qualifier;
    public IReadOnlyList<TypeArgument> TypeArguments { get; } = typeArguments;
    public TypeReference Type { get; } = type;
    public IReadOnlyList<Expression> Arguments { get; } = arguments;
    public IReadOnlyList<MemberDeclaration> AnonymousClassBody { get; } = anonymousClassBody;
}

public sealed class NewArrayExpression(
    TypeReference elementType,
    IReadOnlyList<Expression> dimensionExpressions = null,
    int emptyDimensions = 0,
    ArrayInitializerExpression initializer = null) : Expression
{
    public TypeReference ElementType { get; } = elementType;
    public IReadOnlyList<Expression> DimensionExpressions { get; } = dimensionExpressions;
    public int EmptyDimensions { get; } = emptyDimensions;
    public ArrayInitializerExpression Initializer { get; } = initializer;
}

public sealed class ArrayInitializerExpression(IReadOnlyList<Expression> elements = null) : Expression
{
    public IReadOnlyList<Expression> Elements { get; } = elements;
}

// ========================================
// Field and Array Access
// ========================================

public sealed class FieldAccessExpression(Expression target, string fieldName) : Expression
{
    public Expression Target { get; } = target;
    public string FieldName { get; } = fieldName;
}

public sealed class ArrayAccessExpression(Expression array, Expression index) : Expression
{
    public Expression Array { get; } = array;
    public Expression Index { get; } = index;
}

// ========================================
// Method Invocation
// ========================================

public sealed class MethodInvocationExpression(
    string methodName,
    IReadOnlyList<Expression> arguments = null,
    Expression target = null,
    IReadOnlyList<TypeArgument> typeArguments = null) : Expression
{
    public Expression Target { get; } = target;
    public IReadOnlyList<TypeArgument> TypeArguments { get; } = typeArguments;
    public string MethodName { get; } = methodName;
    public IReadOnlyList<Expression> Arguments { get; } = arguments;
}

// ========================================
// Method Reference (Java 8+)
// ========================================

public sealed class MethodReferenceExpression(
    Expression target,
    string methodName = null,
    IReadOnlyList<TypeArgument> typeArguments = null,
    bool isConstructorReference = false) : Expression
{
    public Expression Target { get; } = target;
    public IReadOnlyList<TypeArgument> TypeArguments { get; } = typeArguments;
    public string MethodName { get; } = methodName;
    public bool IsConstructorReference { get; } = isConstructorReference;
}

// ========================================
// Operators
// ========================================

public sealed class UnaryExpression(UnaryOperator op, Expression operand, bool isPrefix = true) : Expression
{
    public UnaryOperator Operator { get; } = op;
    public Expression Operand { get; } = operand;
    public bool IsPrefix { get; } = isPrefix;
}

public enum UnaryOperator
{
    Plus,
    Minus,
    BitwiseComplement,
    LogicalComplement,
    PreIncrement,
    PreDecrement,
    PostIncrement,
    PostDecrement
}

public sealed class BinaryExpression(Expression left, BinaryOperator op, Expression right) : Expression
{
    public Expression Left { get; } = left;
    public BinaryOperator Operator { get; } = op;
    public Expression Right { get; } = right;
}

public enum BinaryOperator
{
    // Multiplicative
    Multiply, Divide, Modulo,
    
    // Additive
    Add, Subtract,
    
    // Shift
    LeftShift, RightShift, UnsignedRightShift,
    
    // Relational
    LessThan, GreaterThan, LessThanOrEqual, GreaterThanOrEqual,
    
    // Equality
    Equal, NotEqual,
    
    // Bitwise
    BitwiseAnd, BitwiseXor, BitwiseOr,
    
    // Logical
    ConditionalAnd, ConditionalOr,
    
    // Assignment
    Assign,
    AddAssign, SubtractAssign, MultiplyAssign, DivideAssign, ModuloAssign,
    LeftShiftAssign, RightShiftAssign, UnsignedRightShiftAssign,
    BitwiseAndAssign, BitwiseXorAssign, BitwiseOrAssign
}

public sealed class TernaryExpression(Expression condition, Expression trueExpr, Expression falseExpr) : Expression
{
    public Expression Condition { get; } = condition;
    public Expression TrueExpression { get; } = trueExpr;
    public Expression FalseExpression { get; } = falseExpr;
}

// ========================================
// Type Operations
// ========================================

public sealed class CastExpression(TypeReference type, Expression expression, IReadOnlyList<TypeReference> additionalBounds = null) : Expression
{
    public TypeReference Type { get; } = type;
    public IReadOnlyList<TypeReference> AdditionalBounds { get; } = additionalBounds;
    public Expression Expression { get; } = expression;
}

public sealed class InstanceOfExpression(Expression expression, TypeReference type = null, Pattern pattern = null) : Expression
{
    public Expression Expression { get; } = expression;
    public TypeReference Type { get; } = type;
    public Pattern Pattern { get; } = pattern;
}

// ========================================
// Lambda Expression (Java 8+)
// ========================================

public sealed class LambdaExpression(IReadOnlyList<LambdaParameter> parameters, LambdaBody body) : Expression
{
    public IReadOnlyList<LambdaParameter> Parameters { get; } = parameters;
    public LambdaBody Body { get; } = body;
}

public sealed class LambdaParameter(string name, TypeReference type = null, IReadOnlyList<Annotation> annotations = null) : IJavaNode
{
    public IReadOnlyList<Annotation> Annotations { get; } = annotations;
    public TypeReference Type { get; } = type;
    public string Name { get; } = name;
}

public abstract class LambdaBody : IJavaNode
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

// ========================================
// Patterns (Java 16+)
// ========================================

public abstract class Pattern : IJavaNode
{
}

public sealed class TypePattern(TypeReference type, string identifier) : Pattern
{
    public TypeReference Type { get; } = type;
    public string Identifier { get; } = identifier;
}

public sealed class RecordPattern(TypeReference type, IReadOnlyList<Pattern> patterns = null) : Pattern
{
    public TypeReference Type { get; } = type;
    public IReadOnlyList<Pattern> Patterns { get; } = patterns;
}

// ========================================
// Array Initializer
// ========================================

public sealed class ArrayInitializer(IReadOnlyList<Expression> elements = null) : IJavaNode
{
    public IReadOnlyList<Expression> Elements { get; } = elements;
}

