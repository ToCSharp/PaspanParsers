namespace PaspanParsers.CSharp;

// ========================================
// Base Interfaces
// ========================================

/// <summary>
/// Base interface for all C# AST nodes
/// </summary>
public interface ICSharpNode
{
    /// <summary>
    /// The position of the node in the parsed input; see <see cref="TextSpan"/>.
    /// </summary>
    TextSpan Span { get; }
}

/// <summary>
/// Base class of the C# AST nodes.
/// </summary>
public abstract class CSharpNode : ICSharpNode
{
    /// <summary>
    /// The position of the node in the parsed input, from its first token to the end of its last token,
    /// without the trivia around them. The parser sets it; nodes built in code have an empty span at 0.
    /// </summary>
    public TextSpan Span { get; set; }
}

/// <summary>
/// A range of the parsed input in UTF-8 bytes: <see cref="Start"/> is the first byte, <see cref="End"/>
/// the byte after the last one. Offsets count from the start of the input without its byte order mark.
/// </summary>
public readonly record struct TextSpan(int Start, int End)
{
    public int Length => End - Start;

    public bool IsEmpty => Start == End;

    /// <summary>True when <paramref name="span"/> lies inside this span.</summary>
    public bool Contains(TextSpan span) => Start <= span.Start && span.End <= End;

    /// <summary>
    /// The text of the span in <paramref name="utf8Source"/>, the input as UTF-8 bytes without the byte order mark.
    /// </summary>
    public string GetText(ReadOnlySpan<byte> utf8Source) => System.Text.Encoding.UTF8.GetString(utf8Source[Start..End]);

    /// <summary>
    /// The text of the span in <paramref name="source"/>, the string that was parsed. The string is encoded
    /// to UTF-8 on each call; use <see cref="GetText(ReadOnlySpan{byte})"/> for many spans of one input.
    /// </summary>
    public string GetText(string source) => GetText(CSharpParser.GetUtf8Source(source));

    public override string ToString() => $"[{Start}..{End})";
}

// ========================================
// Compilation Unit
// ========================================

public sealed class CompilationUnit(
    IReadOnlyList<ExternAliasDirective> externAliases = null,
    IReadOnlyList<UsingDirective> usings = null,
    IReadOnlyList<AttributeSection> globalAttributes = null,
    IReadOnlyList<MemberDeclaration> members = null) : CSharpNode
{
    public IReadOnlyList<ExternAliasDirective> ExternAliases { get; } = externAliases;
    public IReadOnlyList<UsingDirective> Usings { get; } = usings;
    public IReadOnlyList<AttributeSection> GlobalAttributes { get; } = globalAttributes;
    public IReadOnlyList<MemberDeclaration> Members { get; } = members;

    /// <summary><c>#nullable</c> directives after the last member.</summary>
    public IReadOnlyList<NullableDirective> EndNullableDirectives { get; init; }

    /// <summary>
    /// The syntax errors of the input in source order, or null when there are none. Only a parse with
    /// <see cref="CSharpParseOptions.ErrorRecovery"/> returns a tree for invalid input.
    /// </summary>
    public IReadOnlyList<SyntaxError> Errors { get; internal set; }
}

/// <summary>
/// A syntax error found by a parse with <see cref="CSharpParseOptions.ErrorRecovery"/>:
/// <see cref="Span"/> is the unexpected token (empty at the end of the input).
/// </summary>
public sealed record SyntaxError(TextSpan Span, string Message);

// ========================================
// Preprocessor directives
// ========================================

/// <summary>
/// <c>#nullable enable|disable|restore [warnings|annotations]</c>.
/// </summary>
/// <remarks>
/// Directives are trivia: the parser evaluates <c>#if</c> and skips the others. <c>#nullable</c> is kept
/// because it changes the meaning of the code. Nodes that can start a line hold the directives in the
/// trivia before their first token (<c>NullableDirectives</c>, set by the parser after the node is built);
/// nodes with braces also hold the directives before the closing brace (<c>CloseBraceNullableDirectives</c>).
/// </remarks>
public sealed class NullableDirective(NullableSetting setting, NullableTarget? target = null) : CSharpNode
{
    public NullableSetting Setting { get; } = setting;
    public NullableTarget? Target { get; } = target;
}

public enum NullableSetting
{
    Enable,
    Disable,
    Restore
}

public enum NullableTarget
{
    Warnings,
    Annotations
}

// ========================================
// Using Directives
// ========================================

public abstract class UsingDirective : CSharpNode
{
    /// <summary><c>global using</c> (C# 10).</summary>
    public bool IsGlobal { get; init; }

    /// <summary><c>using unsafe X = int*;</c> (C# 12).</summary>
    public bool IsUnsafe { get; init; }

    /// <summary><c>#nullable</c> directives before the directive.</summary>
    public IReadOnlyList<NullableDirective> NullableDirectives { get; set; }
}

public sealed class UsingNamespaceDirective(NameExpression namespaceName) : UsingDirective
{
    public NameExpression Namespace { get; } = namespaceName;
}

/// <summary>
/// <c>using Alias = Target;</c>. Since C# 12 the target may be any type; <see cref="Target"/> is set when
/// the type is a name, otherwise it is null and <see cref="TargetType"/> holds the type.
/// </summary>
public sealed class UsingAliasDirective(string alias, NameExpression target, TypeReference targetType = null) : UsingDirective
{
    public string Alias { get; } = alias;
    public NameExpression Target { get; } = target;
    public TypeReference TargetType { get; } = targetType;
}

/// <summary>
/// <c>using static Type;</c>. <see cref="Type"/> is set when the type is a name with type arguments only on
/// its last part, otherwise it is null and <see cref="TargetType"/> holds the type.
/// </summary>
public sealed class UsingStaticDirective(NameExpression type, TypeReference targetType = null) : UsingDirective
{
    public NameExpression Type { get; } = type;
    public TypeReference TargetType { get; } = targetType;
}

public sealed class ExternAliasDirective(string identifier) : CSharpNode
{
    public string Identifier { get; } = identifier;

    /// <summary><c>#nullable</c> directives before the directive.</summary>
    public IReadOnlyList<NullableDirective> NullableDirectives { get; set; }
}

// ========================================
// Member Declarations
// ========================================

public abstract class MemberDeclaration(IReadOnlyList<AttributeSection> attributes, Modifiers modifiers) : CSharpNode
{
    public IReadOnlyList<AttributeSection> Attributes { get; } = attributes;
    public Modifiers Modifiers { get; } = modifiers;

    /// <summary>
    /// The modifiers in source order. The parser sets it; when it is null, <see cref="CSharpWriter"/>
    /// writes <see cref="Modifiers"/> in a canonical order.
    /// </summary>
    public IReadOnlyList<Modifiers> ModifierList { get; init; }

    /// <summary><c>#nullable</c> directives before the declaration.</summary>
    public IReadOnlyList<NullableDirective> NullableDirectives { get; set; }

    /// <summary>
    /// The whitespace, comments and directives before the declaration, from the end of the previous token
    /// (empty for a declaration the parser did not read from source); <see cref="DocumentationComment"/>
    /// reads the documentation comment in it.
    /// </summary>
    public TextSpan LeadingTrivia { get; set; }
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
    /// <summary>A fixed-size buffer: <c>fixed int buffer[16];</c></summary>
    Fixed = 1 << 20,
    /// <summary>The <c>safe</c> modifier of the unsafe code evolution (C# 15 preview).</summary>
    Safe = 1 << 21,
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

    /// <summary>A ';' follows the closing brace: <c>namespace N { };</c></summary>
    public bool HasTrailingSemicolon { get; init; }

    /// <summary><c>#nullable</c> directives before the closing brace.</summary>
    public IReadOnlyList<NullableDirective> CloseBraceNullableDirectives { get; init; }
}

// ========================================
// Type Declarations
// ========================================

/// <summary>
/// A class, struct, interface, enum or record declaration.
/// </summary>
public abstract class TypeDeclaration(
    IReadOnlyList<AttributeSection> attributes,
    Modifiers modifiers,
    IReadOnlyList<Parameter> primaryConstructorParameters = null) : MemberDeclaration(attributes, modifiers)
{
    /// <summary>
    /// The parameters of a primary constructor (<c>class C(int x)</c>, C# 12 for classes and structs);
    /// null without a parameter list, empty for <c>()</c>.
    /// </summary>
    public IReadOnlyList<Parameter> PrimaryConstructorParameters { get; } = primaryConstructorParameters;

    /// <summary>
    /// The arguments passed to the base class of a primary constructor: <c>record B(int X) : A(X);</c>
    /// </summary>
    public IReadOnlyList<Argument> BaseArguments { get; init; }

    /// <summary>
    /// True for a body in braces, false for a ';' instead of the body (<c>class C;</c>, <c>record R(int X);</c>).
    /// Null lets <see cref="CSharpWriter"/> choose: braces, or ';' for a record without members.
    /// </summary>
    public bool? HasBody { get; init; }

    /// <summary>A ';' follows the closing brace: <c>class C { };</c></summary>
    public bool HasTrailingSemicolon { get; init; }

    /// <summary><c>#nullable</c> directives before the opening brace.</summary>
    public IReadOnlyList<NullableDirective> OpenBraceNullableDirectives { get; init; }

    /// <summary><c>#nullable</c> directives before the closing brace.</summary>
    public IReadOnlyList<NullableDirective> CloseBraceNullableDirectives { get; init; }
}

public sealed class ClassDeclaration(
    string name,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None,
    IReadOnlyList<TypeParameter> typeParameters = null,
    IReadOnlyList<TypeReference> baseTypes = null,
    IReadOnlyList<TypeParameterConstraint> constraints = null,
    IReadOnlyList<MemberDeclaration> members = null,
    IReadOnlyList<Parameter> primaryConstructorParameters = null) : TypeDeclaration(attributes, modifiers, primaryConstructorParameters)
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
    IReadOnlyList<MemberDeclaration> members = null,
    IReadOnlyList<Parameter> primaryConstructorParameters = null) : TypeDeclaration(attributes, modifiers, primaryConstructorParameters)
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
    IReadOnlyList<MemberDeclaration> members = null) : TypeDeclaration(attributes, modifiers)
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
    IReadOnlyList<EnumMember> members = null) : TypeDeclaration(attributes, modifiers)
{
    public string Name { get; } = name;
    public TypeReference BaseType { get; } = baseType;
    public IReadOnlyList<EnumMember> Members { get; } = members;

    /// <summary>A ',' follows the last member.</summary>
    public bool HasTrailingComma { get; init; }
}

public sealed class EnumMember(string name, Expression value = null, IReadOnlyList<AttributeSection> attributes = null) : CSharpNode
{
    public IReadOnlyList<AttributeSection> Attributes { get; } = attributes;
    public string Name { get; } = name;
    public Expression Value { get; } = value;

    /// <summary>
    /// The whitespace, comments and directives before the member, from the end of the previous token;
    /// <see cref="DocumentationComment"/> reads the documentation comment in it.
    /// </summary>
    public TextSpan LeadingTrivia { get; init; }
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
    IReadOnlyList<MemberDeclaration> members = null) : TypeDeclaration(attributes, modifiers, primaryConstructorParameters)
{
    public string Name { get; } = name;
    public bool IsRecordStruct { get; } = isRecordStruct;

    /// <summary>Written as <c>record class</c>.</summary>
    public bool HasClassKeyword { get; init; }

    public IReadOnlyList<TypeParameter> TypeParameters { get; } = typeParameters;
    public IReadOnlyList<TypeReference> BaseTypes { get; } = baseTypes;
    public IReadOnlyList<TypeParameterConstraint> Constraints { get; } = constraints;
    public IReadOnlyList<MemberDeclaration> Members { get; } = members;
}

// ========================================
// Type Parameters
// ========================================

public sealed class TypeParameter(string name, VarianceKind? variance = null, IReadOnlyList<AttributeSection> attributes = null) : CSharpNode
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

public sealed class TypeParameterConstraint(string typeParameterName, IReadOnlyList<TypeConstraint> constraints) : CSharpNode
{
    public string TypeParameterName { get; } = typeParameterName;
    public IReadOnlyList<TypeConstraint> Constraints { get; } = constraints;

    /// <summary><c>#nullable</c> directives before the clause.</summary>
    public IReadOnlyList<NullableDirective> NullableDirectives { get; set; }
}

public abstract class TypeConstraint : CSharpNode
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

/// <summary>
/// The <c>default</c> constraint of overrides and explicit implementations.
/// </summary>
public sealed class DefaultConstraint : TypeConstraint
{
}

/// <summary>
/// The anti-constraint <c>allows ref struct</c>.
/// </summary>
public sealed class AllowsRefStructConstraint : TypeConstraint
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

public sealed class VariableDeclarator(string name, Expression initializer = null) : CSharpNode
{
    public string Name { get; } = name;
    public Expression Initializer { get; } = initializer;

    /// <summary>The size of a fixed-size buffer: <c>buffer[16]</c>; null otherwise.</summary>
    public IReadOnlyList<Argument> BracketedArguments { get; init; }
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

    /// <summary>The interface of an explicit implementation: <c>IEquatable&lt;T&gt;</c> in <c>bool IEquatable&lt;T&gt;.Equals(T other)</c>.</summary>
    public TypeReference ExplicitInterface { get; init; }
}

public abstract class MethodBody : CSharpNode
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

/// <summary>
/// A parameter. <see cref="Type"/> is null for implicitly typed lambda parameters.
/// <see cref="Modifiers"/> lists the modifiers in source order (<c>scoped ref</c>, <c>ref readonly</c>, <c>this in</c>);
/// <see cref="Modifier"/> is the first one that is not <c>scoped</c> or <c>readonly</c>.
/// </summary>
public sealed class Parameter(
    TypeReference type,
    string name,
    ParameterModifier modifier = ParameterModifier.None,
    Expression defaultValue = null,
    IReadOnlyList<AttributeSection> attributes = null,
    IReadOnlyList<ParameterModifier> modifiers = null) : CSharpNode
{
    public IReadOnlyList<AttributeSection> Attributes { get; } = attributes;
    public ParameterModifier Modifier { get; } = modifier;
    public IReadOnlyList<ParameterModifier> Modifiers { get; } = modifiers ?? (modifier == ParameterModifier.None ? [] : [modifier]);
    public TypeReference Type { get; } = type;
    public string Name { get; } = name;
    public Expression DefaultValue { get; } = defaultValue;

    /// <summary><c>#nullable</c> directives before the parameter.</summary>
    public IReadOnlyList<NullableDirective> NullableDirectives { get; set; }
}

public enum ParameterModifier
{
    None,
    This,
    Ref,
    Out,
    In,
    Params,
    Scoped,
    Readonly
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

    /// <summary>The interface of an explicit implementation.</summary>
    public TypeReference ExplicitInterface { get; init; }

    /// <summary><c>#nullable</c> directives before the brace of the accessor list.</summary>
    public IReadOnlyList<NullableDirective> OpenBraceNullableDirectives { get; init; }
}

public sealed class Accessor(
    AccessorKind kind,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None,
    MethodBody body = null) : CSharpNode
{
    public AccessorKind Kind { get; } = kind;
    public IReadOnlyList<AttributeSection> Attributes { get; } = attributes;
    public Modifiers Modifiers { get; } = modifiers;

    /// <summary>The modifiers in source order; see <see cref="MemberDeclaration.ModifierList"/>.</summary>
    public IReadOnlyList<Modifiers> ModifierList { get; init; }

    /// <summary>The body, or null for <c>get;</c>.</summary>
    public MethodBody Body { get; } = body;

    /// <summary><c>#nullable</c> directives before the accessor.</summary>
    public IReadOnlyList<NullableDirective> NullableDirectives { get; set; }
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

    /// <summary>The accessors, or null for an expression-bodied indexer.</summary>
    public IReadOnlyList<Accessor> Accessors { get; } = accessors;

    /// <summary>The body of <c>this[int i] =&gt; expression;</c></summary>
    public Expression ExpressionBody { get; init; }

    /// <summary>The interface of an explicit implementation.</summary>
    public TypeReference ExplicitInterface { get; init; }

    /// <summary><c>#nullable</c> directives before the brace of the accessor list.</summary>
    public IReadOnlyList<NullableDirective> OpenBraceNullableDirectives { get; init; }
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

    /// <summary>The events of a field-like declaration, or the single event with <see cref="Accessors"/>.</summary>
    public IReadOnlyList<VariableDeclarator> Variables { get; } = variables;
    public IReadOnlyList<EventAccessor> Accessors { get; } = accessors;

    /// <summary>The interface of an explicit implementation.</summary>
    public TypeReference ExplicitInterface { get; init; }
}

/// <summary>
/// An <c>add</c> or <c>remove</c> accessor with a <see cref="Block"/> or an <see cref="ExpressionBody"/>.
/// </summary>
public sealed class EventAccessor(EventAccessorKind kind, BlockStatement block, IReadOnlyList<AttributeSection> attributes = null) : CSharpNode
{
    public EventAccessorKind Kind { get; } = kind;
    public IReadOnlyList<AttributeSection> Attributes { get; } = attributes;
    public BlockStatement Block { get; } = block;
    public Expression ExpressionBody { get; init; }
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

public sealed class ConstructorInitializer(bool isBase, IReadOnlyList<Argument> arguments = null) : CSharpNode
{
    public bool IsBase { get; } = isBase;
    public IReadOnlyList<Argument> Arguments { get; } = arguments;
}

// ========================================
// Destructor, operators, extension blocks, global statements
// ========================================

/// <summary>
/// <c>~Name() { }</c>
/// </summary>
public sealed class DestructorDeclaration(
    string name,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None,
    MethodBody body = null) : MemberDeclaration(attributes, modifiers)
{
    public string Name { get; } = name;

    /// <summary>The body, or null for <c>extern ~C();</c>.</summary>
    public MethodBody Body { get; } = body;
}

/// <summary>
/// A user-defined operator: <c>public static C operator +(C a, C b)</c>, <c>operator checked -(C c)</c>,
/// <c>void operator +=(C other)</c> (C# 14). <see cref="Operator"/> is the operator token text
/// (<c>+</c>, <c>&gt;&gt;&gt;</c>, <c>true</c>).
/// </summary>
public sealed class OperatorDeclaration(
    TypeReference returnType,
    string op,
    IReadOnlyList<Parameter> parameters,
    MethodBody body = null,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None,
    bool isChecked = false) : MemberDeclaration(attributes, modifiers)
{
    public TypeReference ReturnType { get; } = returnType;
    public string Operator { get; } = op;
    public bool IsChecked { get; } = isChecked;
    public IReadOnlyList<Parameter> Parameters { get; } = parameters;
    public MethodBody Body { get; } = body;

    /// <summary>The interface of an explicit implementation.</summary>
    public TypeReference ExplicitInterface { get; init; }
}

/// <summary>
/// A conversion operator: <c>public static implicit operator int(C c)</c>, <c>explicit operator checked int(C c)</c>.
/// </summary>
public sealed class ConversionOperatorDeclaration(
    bool isImplicit,
    TypeReference type,
    IReadOnlyList<Parameter> parameters,
    MethodBody body = null,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None,
    bool isChecked = false) : MemberDeclaration(attributes, modifiers)
{
    public bool IsImplicit { get; } = isImplicit;
    public bool IsChecked { get; } = isChecked;
    public TypeReference Type { get; } = type;
    public IReadOnlyList<Parameter> Parameters { get; } = parameters;
    public MethodBody Body { get; } = body;

    /// <summary>The interface of an explicit implementation.</summary>
    public TypeReference ExplicitInterface { get; init; }
}

/// <summary>
/// A C# 14 extension block: <c>extension&lt;T&gt;(List&lt;T&gt; list) where T : class { members }</c>.
/// The receiver parameter may have no name (<see cref="Parameter.Name"/> is null).
/// </summary>
public sealed class ExtensionBlockDeclaration(
    Parameter receiver,
    IReadOnlyList<MemberDeclaration> members = null,
    IReadOnlyList<TypeParameter> typeParameters = null,
    IReadOnlyList<TypeParameterConstraint> constraints = null,
    IReadOnlyList<AttributeSection> attributes = null,
    Modifiers modifiers = Modifiers.None) : MemberDeclaration(attributes, modifiers)
{
    public IReadOnlyList<TypeParameter> TypeParameters { get; } = typeParameters;
    public Parameter Receiver { get; } = receiver;
    public IReadOnlyList<TypeParameterConstraint> Constraints { get; } = constraints;
    public IReadOnlyList<MemberDeclaration> Members { get; } = members;

    /// <summary><c>#nullable</c> directives before the closing brace.</summary>
    public IReadOnlyList<NullableDirective> CloseBraceNullableDirectives { get; init; }
}

/// <summary>
/// A top-level statement (C# 9) among the members of the compilation unit. Methods and variables at
/// the top level are local functions and local declarations, like in Roslyn.
/// </summary>
public sealed class GlobalStatement(Statement statement) : MemberDeclaration(null, Modifiers.None)
{
    public Statement Statement { get; } = statement;
}

/// <summary>
/// Source that could not be parsed as a member declaration, skipped by a parse with
/// <see cref="CSharpParseOptions.ErrorRecovery"/> up to the next ';', balanced '{...}' block or the '}'
/// that closes the enclosing body. The error is in <see cref="CompilationUnit.Errors"/>.
/// </summary>
public sealed class IncompleteMemberDeclaration(string text) : MemberDeclaration(null, Modifiers.None)
{
    /// <summary>The skipped source text.</summary>
    public string Text { get; } = text;
}

// ========================================
// Type References
// ========================================

public abstract class TypeReference : CSharpNode
{
    /// <summary><c>#nullable</c> directives before a type argument: <c>IEnumerable&lt;</c> <c>#nullable disable</c> <c>T&gt;</c>.</summary>
    public IReadOnlyList<NullableDirective> NullableDirectives { get; set; }
}

/// <summary>
/// A named type: <c>List&lt;int&gt;</c>, <c>System.String</c>, <c>global::System.String</c>, <c>A&lt;B&gt;.C&lt;D&gt;</c>.
/// <see cref="TypeArguments"/> belong to the last part of <see cref="Name"/>. When an earlier part has
/// type arguments, everything up to it is in <see cref="Qualifier"/> (<c>A&lt;B&gt;</c> in <c>A&lt;B&gt;.C&lt;D&gt;</c>).
/// <see cref="Alias"/> is the alias of an alias-qualified name (<c>global</c> in <c>global::System.String</c>).
/// </summary>
public sealed class NamedTypeReference(
    NameExpression name,
    IReadOnlyList<TypeReference> typeArguments = null,
    bool isNullable = false,
    TypeReference qualifier = null,
    string alias = null) : TypeReference
{
    public TypeReference Qualifier { get; } = qualifier;
    public string Alias { get; } = alias;
    public NameExpression Name { get; } = name;
    public IReadOnlyList<TypeReference> TypeArguments { get; } = typeArguments;
    public bool IsNullable { get; } = isNullable;

    /// <summary><c>#nullable</c> directives before the '&gt;' that closes <see cref="TypeArguments"/>.</summary>
    public IReadOnlyList<NullableDirective> CloseAngleNullableDirectives { get; init; }
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

/// <summary>
/// A nullable type other than a named or predefined type: <c>int[]?</c>, <c>(int, int)?</c>.
/// Named and predefined types use their <c>IsNullable</c> flag instead.
/// </summary>
public sealed class NullableTypeReference(TypeReference elementType) : TypeReference
{
    public TypeReference ElementType { get; } = elementType;
}

/// <summary>
/// A pointer type: <c>int*</c>, <c>void**</c>.
/// </summary>
public sealed class PointerTypeReference(TypeReference elementType) : TypeReference
{
    public TypeReference ElementType { get; } = elementType;
}

/// <summary>
/// A function pointer type: <c>delegate*&lt;int, void&gt;</c>, <c>delegate* unmanaged[Cdecl]&lt;int, void&gt;</c>.
/// The last parameter is the return type.
/// </summary>
public sealed class FunctionPointerTypeReference(
    IReadOnlyList<FunctionPointerParameter> parameters,
    string callingConvention = null,
    IReadOnlyList<string> unmanagedCallingConventions = null) : TypeReference
{
    /// <summary><c>managed</c>, <c>unmanaged</c> or null when not specified.</summary>
    public string CallingConvention { get; } = callingConvention;
    /// <summary>The conventions in brackets after <c>unmanaged</c>, or null without brackets.</summary>
    public IReadOnlyList<string> UnmanagedCallingConventions { get; } = unmanagedCallingConventions;
    public IReadOnlyList<FunctionPointerParameter> Parameters { get; } = parameters;
}

public sealed class FunctionPointerParameter(TypeReference type, IReadOnlyList<ParameterModifier> modifiers = null) : CSharpNode
{
    public IReadOnlyList<ParameterModifier> Modifiers { get; } = modifiers;
    public TypeReference Type { get; } = type;
}

/// <summary>
/// A by-reference type of a local or a return: <c>ref int</c>, <c>ref readonly int</c>.
/// </summary>
public sealed class RefTypeReference(TypeReference type, bool isReadOnly = false) : TypeReference
{
    public TypeReference Type { get; } = type;
    public bool IsReadOnly { get; } = isReadOnly;
}

/// <summary>
/// The type of a <c>scoped</c> local: <c>scoped ref int</c>, <c>scoped Span&lt;int&gt;</c>.
/// </summary>
public sealed class ScopedTypeReference(TypeReference type) : TypeReference
{
    public TypeReference Type { get; } = type;
}

/// <summary>
/// A missing type argument of an unbound generic type: <c>Dictionary&lt;,&gt;</c> in <c>typeof</c>.
/// </summary>
public sealed class OmittedTypeReference : TypeReference
{
}

public sealed class TupleElement(TypeReference type, string name = null) : CSharpNode
{
    public TypeReference Type { get; } = type;
    public string Name { get; } = name;
}

// ========================================
// Statements
// ========================================

public abstract class Statement : CSharpNode
{
    /// <summary><c>#nullable</c> directives before the statement.</summary>
    public IReadOnlyList<NullableDirective> NullableDirectives { get; set; }
}

/// <summary>
/// Source that could not be parsed as a statement, skipped by a parse with
/// <see cref="CSharpParseOptions.ErrorRecovery"/> like <see cref="IncompleteMemberDeclaration"/>.
/// </summary>
public sealed class IncompleteStatement(string text) : Statement
{
    /// <summary>The skipped source text.</summary>
    public string Text { get; } = text;
}

public sealed class BlockStatement(IReadOnlyList<Statement> statements = null) : Statement
{
    public IReadOnlyList<Statement> Statements { get; } = statements;

    /// <summary><c>#nullable</c> directives before the closing brace.</summary>
    public IReadOnlyList<NullableDirective> CloseBraceNullableDirectives { get; init; }
}

public sealed class ExpressionStatement(Expression expression) : Statement
{
    public Expression Expression { get; } = expression;
}

public sealed class EmptyStatement : Statement
{
}

/// <summary>
/// A local variable declaration. <see cref="Type"/> is a <see cref="RefTypeReference"/> for
/// <c>ref</c> locals and a <see cref="ScopedTypeReference"/> for <c>scoped</c> locals.
/// </summary>
public sealed class LocalDeclarationStatement(
    TypeReference type,
    IReadOnlyList<VariableDeclarator> variables,
    bool isConst = false,
    bool isUsing = false,
    bool isAwait = false) : Statement
{
    public bool IsConst { get; } = isConst;
    public bool IsUsing { get; } = isUsing;
    /// <summary><c>await using</c>.</summary>
    public bool IsAwait { get; } = isAwait;
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
    /// <summary>
    /// The expression is in the statement's own parentheses. False for a tuple written as <c>switch (a, b)</c>,
    /// true for <c>switch ((a, b))</c>; null lets <see cref="CSharpWriter"/> choose (no parentheses around a tuple).
    /// </summary>
    public bool? HasParentheses { get; init; }

    public Expression Expression { get; } = expression;
    public IReadOnlyList<SwitchSection> Sections { get; } = sections;
}

public sealed class SwitchSection(IReadOnlyList<SwitchLabel> labels, IReadOnlyList<Statement> statements) : CSharpNode
{
    public IReadOnlyList<SwitchLabel> Labels { get; } = labels;
    public IReadOnlyList<Statement> Statements { get; } = statements;
}

public abstract class SwitchLabel : CSharpNode
{
    /// <summary><c>#nullable</c> directives before the label.</summary>
    public IReadOnlyList<NullableDirective> NullableDirectives { get; set; }
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

/// <summary>
/// <c>foreach (Type identifier in collection)</c>, or with deconstruction <c>foreach (var (a, b) in collection)</c>,
/// where <see cref="Variable"/> holds the deconstruction and <see cref="Type"/> and <see cref="Identifier"/> are null.
/// </summary>
public sealed class ForEachStatement(
    TypeReference type,
    string identifier,
    Expression collection,
    Statement body,
    bool isAwait = false,
    Expression variable = null) : Statement
{
    public TypeReference Type { get; } = type;
    public string Identifier { get; } = identifier;
    public Expression Variable { get; } = variable;
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
    Expression filter = null) : CSharpNode
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

public sealed class GotoStatement(string label, GotoKind kind = GotoKind.Label, Expression caseExpression = null) : Statement
{
    public GotoKind Kind { get; } = kind;
    public string Label { get; } = label;
    /// <summary>The constant of <c>goto case</c>.</summary>
    public Expression CaseExpression { get; } = caseExpression;
}

public enum GotoKind
{
    Label,
    Case,
    Default
}

/// <summary>
/// A <c>checked</c> or <c>unchecked</c> block.
/// </summary>
public sealed class CheckedStatement(bool isChecked, BlockStatement block) : Statement
{
    public bool IsChecked { get; } = isChecked;
    public BlockStatement Block { get; } = block;
}

public sealed class UnsafeStatement(BlockStatement block) : Statement
{
    public BlockStatement Block { get; } = block;
}

public sealed class FixedStatement(TypeReference type, IReadOnlyList<VariableDeclarator> variables, Statement body) : Statement
{
    public TypeReference Type { get; } = type;
    public IReadOnlyList<VariableDeclarator> Variables { get; } = variables;
    public Statement Body { get; } = body;
}

/// <summary>
/// A local function. <see cref="Modifiers"/> lists the modifiers in source order.
/// The body is either <see cref="Body"/> or <see cref="ExpressionBody"/>.
/// </summary>
public sealed class LocalFunctionStatement(
    TypeReference returnType,
    string name,
    IReadOnlyList<Parameter> parameters,
    BlockStatement body = null,
    Expression expressionBody = null,
    IReadOnlyList<Modifiers> modifiers = null,
    IReadOnlyList<TypeParameter> typeParameters = null,
    IReadOnlyList<TypeParameterConstraint> constraints = null,
    IReadOnlyList<AttributeSection> attributes = null) : Statement
{
    public IReadOnlyList<AttributeSection> Attributes { get; } = attributes;
    public IReadOnlyList<Modifiers> Modifiers { get; } = modifiers ?? [];
    public TypeReference ReturnType { get; } = returnType;
    public string Name { get; } = name;
    public IReadOnlyList<TypeParameter> TypeParameters { get; } = typeParameters;
    public IReadOnlyList<Parameter> Parameters { get; } = parameters;
    public IReadOnlyList<TypeParameterConstraint> Constraints { get; } = constraints;
    public BlockStatement Body { get; } = body;
    public Expression ExpressionBody { get; } = expressionBody;
}

// ========================================
// Expressions
// ========================================

public abstract class Expression : CSharpNode
{
}

/// <summary>
/// A literal. <see cref="Value"/> has the type C# gives the literal (int, uint, long, ulong,
/// float, double, decimal, char, string, or byte[] for UTF-8 strings); <see cref="Text"/> is
/// the literal as written in the source, when it came from source.
/// </summary>
public sealed class LiteralExpression(object value, LiteralKind kind, string text = null) : Expression
{
    public object Value { get; } = value;
    public LiteralKind Kind { get; } = kind;
    public string Text { get; } = text;
}

public enum LiteralKind
{
    Null,
    Boolean,
    Integer,
    Real,
    Character,
    String,
    Utf8String
}

/// <summary>
/// An interpolated string: $"...", $@"...", or a raw $"""...""" string.
/// <see cref="StartToken"/> and <see cref="EndToken"/> are the delimiters as written
/// (for multi-line raw strings including the new lines next to the content);
/// <see cref="BraceCount"/> is the number of braces that open an interpolation.
/// </summary>
public sealed class InterpolatedStringExpression(
    string startToken,
    IReadOnlyList<InterpolatedStringContent> contents,
    string endToken,
    int braceCount = 1) : Expression
{
    public string StartToken { get; } = startToken;
    public IReadOnlyList<InterpolatedStringContent> Contents { get; } = contents;
    public string EndToken { get; } = endToken;
    public int BraceCount { get; } = braceCount;
}

public abstract class InterpolatedStringContent : CSharpNode
{
}

/// <summary>
/// Literal text of an interpolated string: <see cref="Text"/> as written, <see cref="Value"/> decoded.
/// </summary>
public sealed class InterpolatedStringText(string text, string value) : InterpolatedStringContent
{
    public string Text { get; } = text;
    public string Value { get; } = value;
}

/// <summary>
/// An interpolation hole: {expression,alignment:format}.
/// </summary>
public sealed class Interpolation(Expression expression, Expression alignment = null, string format = null) : InterpolatedStringContent
{
    public Expression Expression { get; } = expression;
    public Expression Alignment { get; } = alignment;
    public string Format { get; } = format;
}

/// <summary>
/// A name. In expressions it is a single identifier, optionally generic (<c>F&lt;int&gt;</c>);
/// qualified names in expressions are <see cref="MemberAccessExpression"/> chains.
/// Namespace, using and attribute names keep all their parts here.
/// </summary>
public sealed class NameExpression(IReadOnlyList<string> parts, IReadOnlyList<TypeReference> typeArguments = null, string alias = null) : Expression
{
    /// <summary>The alias of an alias-qualified name: <c>global</c> in <c>global::System.Obsolete</c>.</summary>
    public string Alias { get; } = alias;
    public IReadOnlyList<string> Parts { get; } = parts;
    public IReadOnlyList<TypeReference> TypeArguments { get; } = typeArguments;

    /// <summary><c>#nullable</c> directives before the '&gt;' that closes <see cref="TypeArguments"/>.</summary>
    public IReadOnlyList<NullableDirective> CloseAngleNullableDirectives { get; init; }
}

/// <summary>
/// An alias-qualified name: <c>global::System</c>.
/// </summary>
public sealed class AliasQualifiedNameExpression(string alias, NameExpression name) : Expression
{
    public string Alias { get; } = alias;
    public NameExpression Name { get; } = name;
}

public sealed class ThisExpression : Expression
{
}

public sealed class BaseExpression : Expression
{
}

/// <summary>
/// A predefined type used as an expression: <c>int</c> in <c>int.Parse(s)</c>.
/// </summary>
public sealed class PredefinedTypeExpression(PredefinedType type) : Expression
{
    public PredefinedType Type { get; } = type;
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
    AddressOf, Dereference, Index,
    /// <summary>The postfix null-forgiving operator <c>x!</c>.</summary>
    NullForgiving
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

/// <summary>
/// An argument: <c>x</c>, <c>name: x</c>, <c>ref x</c>, <c>out var x</c>. Attribute arguments can also
/// be written <c>Name = x</c>, which sets <see cref="IsNameEquals"/>.
/// </summary>
public sealed class Argument(Expression expression, string name = null, RefKind refKind = RefKind.None, bool isNameEquals = false) : CSharpNode
{
    public string Name { get; } = name;
    public bool IsNameEquals { get; } = isNameEquals;
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

/// <summary>
/// <c>target.Member</c>, <c>target?.Member</c> (<see cref="IsConditional"/>) or
/// <c>pointer-&gt;Member</c> (<see cref="IsPointerAccess"/>), optionally with type arguments.
/// </summary>
public sealed class MemberAccessExpression(
    string memberName,
    Expression target = null,
    bool isConditional = false,
    IReadOnlyList<TypeReference> typeArguments = null,
    bool isPointerAccess = false) : Expression
{
    public Expression Target { get; } = target;
    public string MemberName { get; } = memberName;
    public IReadOnlyList<TypeReference> TypeArguments { get; } = typeArguments;
    public bool IsConditional { get; } = isConditional;
    public bool IsPointerAccess { get; } = isPointerAccess;

    /// <summary><c>#nullable</c> directives before the '&gt;' that closes <see cref="TypeArguments"/>.</summary>
    public IReadOnlyList<NullableDirective> CloseAngleNullableDirectives { get; init; }
}

/// <summary>
/// <c>[arguments]</c> on the left of an assignment in an object initializer.
/// </summary>
public sealed class ImplicitElementAccessExpression(IReadOnlyList<Argument> arguments) : Expression
{
    public IReadOnlyList<Argument> Arguments { get; } = arguments;
}

public sealed class ElementAccessExpression(Expression target, IReadOnlyList<Argument> arguments, bool isConditional = false) : Expression
{
    public Expression Target { get; } = target;
    public IReadOnlyList<Argument> Arguments { get; } = arguments;
    public bool IsConditional { get; } = isConditional;
}

/// <summary>
/// <c>new T(arguments) { initializer }</c>. <see cref="Type"/> is null for target-typed <c>new()</c>;
/// <see cref="Arguments"/> is null when the parentheses are omitted (<c>new T { A = 1 }</c>).
/// </summary>
public sealed class ObjectCreationExpression(
    TypeReference type,
    IReadOnlyList<Argument> arguments = null,
    InitializerExpression initializer = null) : Expression
{
    public TypeReference Type { get; } = type;
    public IReadOnlyList<Argument> Arguments { get; } = arguments;
    public InitializerExpression Initializer { get; } = initializer;
}

/// <summary>
/// An initializer in braces. Object initializer members are assignments (<c>A = 1</c>, <c>[0] = 2</c>),
/// collection elements are expressions or <see cref="InitializerKind.ComplexElement"/> initializers
/// (<c>{ "a", 1 }</c>), and array initializers nest for multi-dimensional arrays.
/// </summary>
public sealed class InitializerExpression(InitializerKind kind, IReadOnlyList<Expression> expressions, bool hasTrailingComma = false) : Expression
{
    public InitializerKind Kind { get; } = kind;
    public IReadOnlyList<Expression> Expressions { get; } = expressions;
    public bool HasTrailingComma { get; } = hasTrailingComma;
}

public enum InitializerKind
{
    Object,
    Collection,
    ComplexElement,
    Array
}

/// <summary>
/// <c>new { Name = x, y }</c>.
/// </summary>
public sealed class AnonymousObjectCreationExpression(IReadOnlyList<AnonymousObjectMember> members, bool hasTrailingComma = false) : Expression
{
    public IReadOnlyList<AnonymousObjectMember> Members { get; } = members;
    public bool HasTrailingComma { get; } = hasTrailingComma;
}

public sealed class AnonymousObjectMember(Expression expression, string name = null) : CSharpNode
{
    public string Name { get; } = name;
    public Expression Expression { get; } = expression;
}

/// <summary>
/// <c>new T[size, size][]{ ... }</c>. <see cref="Sizes"/> holds the first rank specifier, with null for
/// omitted sizes (<c>new int[,] { ... }</c>); <see cref="AdditionalRanks"/> holds the ranks of the
/// following rank specifiers (<c>new int[2][]</c>).
/// </summary>
public sealed class ArrayCreationExpression(
    TypeReference elementType,
    IReadOnlyList<Expression> sizes = null,
    InitializerExpression initializer = null,
    IReadOnlyList<int> additionalRanks = null) : Expression
{
    public TypeReference ElementType { get; } = elementType;
    public IReadOnlyList<Expression> Sizes { get; } = sizes;
    public IReadOnlyList<int> AdditionalRanks { get; } = additionalRanks;
    public InitializerExpression Initializer { get; } = initializer;
}

/// <summary>
/// <c>new[] { ... }</c> or <c>new[,] { ... }</c>.
/// </summary>
public sealed class ImplicitArrayCreationExpression(InitializerExpression initializer, int rank = 1) : Expression
{
    public int Rank { get; } = rank;
    public InitializerExpression Initializer { get; } = initializer;
}

/// <summary>
/// <c>stackalloc T[size]</c>, <c>stackalloc T[] { ... }</c> or <c>stackalloc[] { ... }</c> (no <see cref="ElementType"/>).
/// </summary>
public sealed class StackAllocExpression(TypeReference elementType, Expression size = null, InitializerExpression initializer = null) : Expression
{
    public TypeReference ElementType { get; } = elementType;
    public Expression Size { get; } = size;
    public InitializerExpression Initializer { get; } = initializer;
}

/// <summary>
/// A collection expression: <c>[1, 2, ..rest]</c>.
/// </summary>
public sealed class CollectionExpression(IReadOnlyList<Expression> elements, bool hasTrailingComma = false) : Expression
{
    public IReadOnlyList<Expression> Elements { get; } = elements;
    public bool HasTrailingComma { get; } = hasTrailingComma;
}

/// <summary>
/// A spread element of a collection expression: <c>..rest</c>.
/// </summary>
public sealed class SpreadElement(Expression expression) : Expression
{
    public Expression Expression { get; } = expression;
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

/// <summary>
/// A lambda. <see cref="Modifiers"/> lists <c>async</c> and <c>static</c> in source order.
/// <see cref="HasParenthesizedParameters"/> is false for <c>x =&gt; ...</c>; when null, a single
/// untyped parameter is written without parentheses.
/// </summary>
public sealed class LambdaExpression(
    LambdaBody body,
    IReadOnlyList<Parameter> parameters = null,
    bool isAsync = false,
    IReadOnlyList<Modifiers> modifiers = null,
    TypeReference returnType = null,
    IReadOnlyList<AttributeSection> attributes = null,
    bool? hasParenthesizedParameters = null) : Expression
{
    public IReadOnlyList<AttributeSection> Attributes { get; } = attributes;
    public IReadOnlyList<Modifiers> Modifiers { get; } = modifiers ?? (isAsync ? [CSharp.Modifiers.Async] : []);
    public TypeReference ReturnType { get; } = returnType;
    public IReadOnlyList<Parameter> Parameters { get; } = parameters;
    public bool? HasParenthesizedParameters { get; } = hasParenthesizedParameters;
    public LambdaBody Body { get; } = body;
    public bool IsAsync { get; } = isAsync;
    public bool IsStatic => Modifiers.Contains(CSharp.Modifiers.Static);
}

/// <summary>
/// <c>delegate (parameters) { ... }</c>; <see cref="Parameters"/> is null when the parameter list is omitted.
/// </summary>
public sealed class AnonymousMethodExpression(
    BlockStatement block,
    IReadOnlyList<Parameter> parameters = null,
    IReadOnlyList<Modifiers> modifiers = null) : Expression
{
    public IReadOnlyList<Modifiers> Modifiers { get; } = modifiers ?? [];
    public IReadOnlyList<Parameter> Parameters { get; } = parameters;
    public BlockStatement Block { get; } = block;
}

public abstract class LambdaBody : CSharpNode
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
    SelectOrGroupClause selectOrGroupClause,
    QueryContinuation continuation = null) : Expression
{
    public FromClause FromClause { get; } = fromClause;
    public IReadOnlyList<QueryClause> BodyClauses { get; } = bodyClauses;
    public SelectOrGroupClause SelectOrGroupClause { get; } = selectOrGroupClause;
    /// <summary><c>into identifier</c> and the query body that follows it.</summary>
    public QueryContinuation Continuation { get; } = continuation;
}

public sealed class FromClause(string identifier, Expression expression, TypeReference type = null) : QueryClause
{
    public TypeReference Type { get; } = type;
    public string Identifier { get; } = identifier;
    public Expression Expression { get; } = expression;
}

public abstract class QueryClause : CSharpNode
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

/// <summary>
/// An ordering of an orderby clause. <see cref="HasExplicitDirection"/> is true when
/// <c>ascending</c> or <c>descending</c> is written.
/// </summary>
public sealed class Ordering(Expression expression, OrderDirection direction = OrderDirection.Ascending, bool hasExplicitDirection = false) : CSharpNode
{
    public Expression Expression { get; } = expression;
    public OrderDirection Direction { get; } = direction;
    public bool HasExplicitDirection { get; } = hasExplicitDirection || direction == OrderDirection.Descending;
}

public abstract class SelectOrGroupClause : CSharpNode
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
    SelectOrGroupClause selectOrGroupClause,
    QueryContinuation continuation = null) : CSharpNode
{
    public string Identifier { get; } = identifier;
    public IReadOnlyList<QueryClause> BodyClauses { get; } = bodyClauses;
    public SelectOrGroupClause SelectOrGroupClause { get; } = selectOrGroupClause;
    public QueryContinuation Continuation { get; } = continuation;
}

public enum OrderDirection
{
    Ascending,
    Descending
}

public sealed class SwitchExpression(Expression governingExpression, IReadOnlyList<SwitchExpressionArm> arms, bool hasTrailingComma = false) : Expression
{
    public Expression GoverningExpression { get; } = governingExpression;
    public IReadOnlyList<SwitchExpressionArm> Arms { get; } = arms;
    public bool HasTrailingComma { get; } = hasTrailingComma;
}

public sealed class SwitchExpressionArm(Pattern pattern, Expression expression, Expression guard = null) : CSharpNode
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

public sealed class TupleExpressionElement(Expression expression, string name = null) : CSharpNode
{
    public string Name { get; } = name;
    public Expression Expression { get; } = expression;
}

public sealed class RangeExpression(Expression start = null, Expression end = null) : Expression
{
    public Expression Start { get; } = start;
    public Expression End { get; } = end;
}

public sealed class WithExpression(Expression expression, InitializerExpression initializer) : Expression
{
    public Expression Expression { get; } = expression;
    public InitializerExpression Initializer { get; } = initializer;
}

/// <summary>
/// <c>checked(expression)</c> or <c>unchecked(expression)</c>.
/// </summary>
public sealed class CheckedExpression(bool isChecked, Expression expression) : Expression
{
    public bool IsChecked { get; } = isChecked;
    public Expression Expression { get; } = expression;
}

/// <summary>
/// <c>ref expression</c>: a by-reference initializer, return value, argument of a conditional or assignment.
/// </summary>
public sealed class RefExpression(Expression expression) : Expression
{
    public Expression Expression { get; } = expression;
}

/// <summary>
/// A declaration in an expression: <c>out var x</c>, <c>out int x</c>, <c>var (a, b)</c> in deconstruction,
/// <c>int x</c> inside a deconstructing tuple.
/// </summary>
public sealed class DeclarationExpression(TypeReference type, VariableDesignation designation) : Expression
{
    public TypeReference Type { get; } = type;
    public VariableDesignation Designation { get; } = designation;
}

public abstract class VariableDesignation : CSharpNode
{
}

public sealed class SingleVariableDesignation(string identifier) : VariableDesignation
{
    public string Identifier { get; } = identifier;
}

public sealed class DiscardDesignation : VariableDesignation
{
}

public sealed class ParenthesizedVariableDesignation(IReadOnlyList<VariableDesignation> variables) : VariableDesignation
{
    public IReadOnlyList<VariableDesignation> Variables { get; } = variables;
}

/// <summary>
/// <c>__arglist</c>, or <c>__arglist(arguments)</c> in an argument list.
/// </summary>
public sealed class ArgListExpression(IReadOnlyList<Argument> arguments = null) : Expression
{
    public IReadOnlyList<Argument> Arguments { get; } = arguments;
}

/// <summary>
/// <c>__makeref(expression)</c>.
/// </summary>
public sealed class MakeRefExpression(Expression expression) : Expression
{
    public Expression Expression { get; } = expression;
}

/// <summary>
/// <c>__reftype(expression)</c>.
/// </summary>
public sealed class RefTypeExpression(Expression expression) : Expression
{
    public Expression Expression { get; } = expression;
}

/// <summary>
/// <c>__refvalue(expression, Type)</c>.
/// </summary>
public sealed class RefValueExpression(Expression expression, TypeReference type) : Expression
{
    public Expression Expression { get; } = expression;
    public TypeReference Type { get; } = type;
}

// ========================================
// Patterns
// ========================================

public abstract class Pattern : CSharpNode
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

/// <summary>
/// <c>var x</c>, <c>var _</c> or <c>var (a, b)</c>. <see cref="Identifier"/> is the name of a single variable.
/// </summary>
public sealed class VarPattern : Pattern
{
    public VarPattern(string identifier)
        : this(identifier == "_" ? new DiscardDesignation() : new SingleVariableDesignation(identifier))
    {
    }

    public VarPattern(VariableDesignation designation)
    {
        Designation = designation;
        Identifier = designation is SingleVariableDesignation single ? single.Identifier : null;
    }

    public string Identifier { get; }
    public VariableDesignation Designation { get; }
}

public sealed class DiscardPattern : Pattern
{
}

public sealed class DeclarationPattern(TypeReference type, string identifier = null) : Pattern
{
    public TypeReference Type { get; } = type;
    public string Identifier { get; } = identifier;
}

/// <summary>
/// <c>Type (positional) { properties } designation</c> with each part optional.
/// A present but empty clause is an empty list (<c>{ }</c>), an absent clause is null.
/// </summary>
public sealed class RecursivePattern(
    TypeReference type = null,
    IReadOnlyList<SubPattern> positionalPatterns = null,
    IReadOnlyList<PropertySubPattern> propertyPatterns = null,
    string designation = null,
    bool propertyPatternsHaveTrailingComma = false) : Pattern
{
    public bool PropertyPatternsHaveTrailingComma { get; } = propertyPatternsHaveTrailingComma;
    public TypeReference Type { get; } = type;
    public IReadOnlyList<SubPattern> PositionalPatterns { get; } = positionalPatterns;
    public IReadOnlyList<PropertySubPattern> PropertyPatterns { get; } = propertyPatterns;
    public string Designation { get; } = designation;
}

/// <summary>
/// A positional subpattern, optionally named: <c>X: 0</c>.
/// </summary>
public sealed class SubPattern(Pattern pattern, string name = null) : CSharpNode
{
    public string Name { get; } = name;
    public Pattern Pattern { get; } = pattern;
}

/// <summary>
/// A property subpattern. <see cref="PropertyName"/> may be an extended property path: <c>A.B</c>.
/// </summary>
public sealed class PropertySubPattern(string propertyName, Pattern pattern) : CSharpNode
{
    public string PropertyName { get; } = propertyName;
    public Pattern Pattern { get; } = pattern;
}

public sealed class ParenthesizedPattern(Pattern pattern) : Pattern
{
    public Pattern Pattern { get; } = pattern;
}

/// <summary>
/// A list pattern: <c>[1, .., var last] designation</c>.
/// </summary>
public sealed class ListPattern(IReadOnlyList<Pattern> patterns, string designation = null, bool hasTrailingComma = false) : Pattern
{
    public IReadOnlyList<Pattern> Patterns { get; } = patterns;
    public bool HasTrailingComma { get; } = hasTrailingComma;
    public string Designation { get; } = designation;
}

/// <summary>
/// A slice pattern in a list pattern: <c>..</c> or <c>.. var rest</c>.
/// </summary>
public sealed class SlicePattern(Pattern pattern = null) : Pattern
{
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

public sealed class AttributeSection(IReadOnlyList<AttributeNode> attributes, AttributeTarget? target = null) : CSharpNode
{
    public AttributeTarget? Target { get; } = target;
    public IReadOnlyList<AttributeNode> Attributes { get; } = attributes;

    /// <summary>
    /// <c>#nullable</c> directives before a global attribute section. Directives before the attributes of
    /// a declaration belong to the declaration.
    /// </summary>
    public IReadOnlyList<NullableDirective> NullableDirectives { get; set; }
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
    Type,
    TypeVar
}

/// <summary>
/// An attribute; <see cref="Arguments"/> is null without parentheses and empty for <c>Name()</c>.
/// </summary>
public sealed class AttributeNode(NameExpression name, IReadOnlyList<Argument> arguments = null) : CSharpNode
{
    public NameExpression Name { get; } = name;
    public IReadOnlyList<Argument> Arguments { get; } = arguments;
}

