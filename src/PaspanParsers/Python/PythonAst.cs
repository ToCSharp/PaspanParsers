namespace PaspanParsers.Python;

/// <summary>
/// Abstract Syntax Tree (AST) nodes for Python language (Python 3.6-3.12)
/// Based on Python's official AST structure
/// </summary>

// ========================================
// Base Interfaces
// ========================================

public interface IPythonNode
{
    // Marker interface for all Python AST nodes
}

// ========================================
// Module (Root Node)
// ========================================

public sealed class Module(IReadOnlyList<Statement> body, IReadOnlyList<TypeIgnore> typeIgnores = null) : IPythonNode
{
    public IReadOnlyList<Statement> Body { get; } = body ?? [];
    public IReadOnlyList<TypeIgnore> TypeIgnores { get; } = typeIgnores;
}

public sealed class TypeIgnore(int lineNumber, string tag = null) : IPythonNode
{
    public int LineNumber { get; } = lineNumber;
    public string Tag { get; } = tag;
}

// ========================================
// Statements
// ========================================

public abstract class Statement : IPythonNode
{
    public int LineNumber { get; set; }
    public int ColumnOffset { get; set; }
}

// Simple Statements

public sealed class ExpressionStatement(Expression value) : Statement
{
    public Expression Value { get; } = value;
}

public sealed class AssignmentStatement(IReadOnlyList<Expression> targets, Expression value, string typeComment = null) : Statement
{
    public IReadOnlyList<Expression> Targets { get; } = targets;
    public Expression Value { get; } = value;
    public string TypeComment { get; } = typeComment;
}

public sealed class AugmentedAssignment(Expression target, AugmentOperator op, Expression value) : Statement
{
    public Expression Target { get; } = target;
    public AugmentOperator Op { get; } = op;
    public Expression Value { get; } = value;
}

public enum AugmentOperator
{
    Add,        // +=
    Sub,        // -=
    Mult,       // *=
    MatMult,    // @=
    Div,        // /=
    Mod,        // %=
    Pow,        // **=
    LShift,     // <<=
    RShift,     // >>=
    BitOr,      // |=
    BitXor,     // ^=
    BitAnd,     // &=
    FloorDiv    // //=
}

public sealed class AnnotatedAssignment(Expression target, Expression annotation, Expression value = null, bool simple = true) : Statement
{
    public Expression Target { get; } = target;
    public Expression Annotation { get; } = annotation;
    public Expression Value { get; } = value;
    public bool Simple { get; } = simple;
}

public sealed class PassStatement : Statement
{
}

public sealed class DeleteStatement(IReadOnlyList<Expression> targets) : Statement
{
    public IReadOnlyList<Expression> Targets { get; } = targets;
}

public sealed class ReturnStatement(Expression value = null) : Statement
{
    public Expression Value { get; } = value;
}

public sealed class YieldStatement(Expression value) : Statement
{
    public Expression Value { get; } = value;
}

public sealed class RaiseStatement(Expression exc = null, Expression cause = null) : Statement
{
    public Expression Exc { get; } = exc;
    public Expression Cause { get; } = cause;
}

public sealed class BreakStatement : Statement
{
}

public sealed class ContinueStatement : Statement
{
}

public sealed class GlobalStatement(IReadOnlyList<string> names) : Statement
{
    public IReadOnlyList<string> Names { get; } = names;
}

public sealed class NonlocalStatement(IReadOnlyList<string> names) : Statement
{
    public IReadOnlyList<string> Names { get; } = names;
}

public sealed class AssertStatement(Expression test, Expression msg = null) : Statement
{
    public Expression Test { get; } = test;
    public Expression Msg { get; } = msg;
}

// Import Statements

public abstract class ImportBase : Statement
{
}

public sealed class ImportStatement(IReadOnlyList<Alias> names) : ImportBase
{
    public IReadOnlyList<Alias> Names { get; } = names;
}

public sealed class ImportFromStatement(IReadOnlyList<Alias> names, string module = null, int level = 0) : ImportBase
{
    public string Module { get; } = module;
    public IReadOnlyList<Alias> Names { get; } = names;
    public int Level { get; } = level;
}

public sealed class Alias(string name, string asName = null) : IPythonNode
{
    public string Name { get; } = name;
    public string AsName { get; } = asName;
}

// Compound Statements

public sealed class IfStatement(Expression test, IReadOnlyList<Statement> body, IReadOnlyList<Statement> orElse = null) : Statement
{
    public Expression Test { get; } = test;
    public IReadOnlyList<Statement> Body { get; } = body;
    public IReadOnlyList<Statement> OrElse { get; } = orElse ?? [];
}

public sealed class WhileStatement(Expression test, IReadOnlyList<Statement> body, IReadOnlyList<Statement> orElse = null) : Statement
{
    public Expression Test { get; } = test;
    public IReadOnlyList<Statement> Body { get; } = body;
    public IReadOnlyList<Statement> OrElse { get; } = orElse ?? [];
}

public sealed class ForStatement(Expression target, Expression iter, IReadOnlyList<Statement> body,
                   IReadOnlyList<Statement> orElse = null, string typeComment = null) : Statement
{
    public Expression Target { get; } = target;
    public Expression Iter { get; } = iter;
    public IReadOnlyList<Statement> Body { get; } = body;
    public IReadOnlyList<Statement> OrElse { get; } = orElse ?? [];
    public string TypeComment { get; } = typeComment;
}

public sealed class WithStatement(IReadOnlyList<WithItem> items, IReadOnlyList<Statement> body, string typeComment = null) : Statement
{
    public IReadOnlyList<WithItem> Items { get; } = items;
    public IReadOnlyList<Statement> Body { get; } = body;
    public string TypeComment { get; } = typeComment;
}

public sealed class WithItem(Expression contextExpr, Expression optionalVars = null) : IPythonNode
{
    public Expression ContextExpr { get; } = contextExpr;
    public Expression OptionalVars { get; } = optionalVars;
}

public sealed class TryStatement(IReadOnlyList<Statement> body, IReadOnlyList<ExceptHandler> handlers,
                   IReadOnlyList<Statement> orElse = null, IReadOnlyList<Statement> finalBody = null) : Statement
{
    public IReadOnlyList<Statement> Body { get; } = body;
    public IReadOnlyList<ExceptHandler> Handlers { get; } = handlers ?? [];
    public IReadOnlyList<Statement> OrElse { get; } = orElse ?? [];
    public IReadOnlyList<Statement> FinalBody { get; } = finalBody ?? [];
}

public sealed class ExceptHandler(IReadOnlyList<Statement> body, Expression type = null, string name = null) : IPythonNode
{
    public Expression Type { get; } = type;
    public string Name { get; } = name;
    public IReadOnlyList<Statement> Body { get; } = body;
}

// Function Definition

public sealed class FunctionDef(string name, Arguments args, IReadOnlyList<Statement> body,
                  IReadOnlyList<Expression> decoratorList = null, Expression returns = null,
                  string typeComment = null) : Statement
{
    public string Name { get; } = name;
    public Arguments Args { get; } = args;
    public IReadOnlyList<Statement> Body { get; } = body;
    public IReadOnlyList<Expression> DecoratorList { get; } = decoratorList ?? [];
    public Expression Returns { get; } = returns;
    public string TypeComment { get; } = typeComment;
}

public sealed class AsyncFunctionDef(string name, Arguments args, IReadOnlyList<Statement> body,
                       IReadOnlyList<Expression> decoratorList = null, Expression returns = null,
                       string typeComment = null) : Statement
{
    public string Name { get; } = name;
    public Arguments Args { get; } = args;
    public IReadOnlyList<Statement> Body { get; } = body;
    public IReadOnlyList<Expression> DecoratorList { get; } = decoratorList ?? [];
    public Expression Returns { get; } = returns;
    public string TypeComment { get; } = typeComment;
}

public sealed class Arguments(IReadOnlyList<Arg> args = null, Arg varArg = null, IReadOnlyList<Arg> kwOnlyArgs = null,
                IReadOnlyList<Expression> kwDefaults = null, Arg kwArg = null,
                IReadOnlyList<Expression> defaults = null, IReadOnlyList<Arg> posOnlyArgs = null) : IPythonNode
{
    public IReadOnlyList<Arg> PosOnlyArgs { get; } = posOnlyArgs ?? [];
    public IReadOnlyList<Arg> Args { get; } = args ?? [];
    public Arg VarArg { get; } = varArg;
    public IReadOnlyList<Arg> KwOnlyArgs { get; } = kwOnlyArgs ?? [];
    public IReadOnlyList<Expression> KwDefaults { get; } = kwDefaults ?? [];
    public Arg KwArg { get; } = kwArg;
    public IReadOnlyList<Expression> Defaults { get; } = defaults ?? [];
}

public sealed class Arg(string arg, Expression annotation = null, string typeComment = null) : IPythonNode
{
    public string Arg_ { get; } = arg;
    public Expression Annotation { get; } = annotation;
    public string TypeComment { get; } = typeComment;
}

// Class Definition

public sealed class ClassDef(string name, IReadOnlyList<Statement> body, IReadOnlyList<Expression> bases = null,
               IReadOnlyList<Keyword> keywords = null, IReadOnlyList<Expression> decoratorList = null) : Statement
{
    public string Name { get; } = name;
    public IReadOnlyList<Expression> Bases { get; } = bases ?? [];
    public IReadOnlyList<Keyword> Keywords { get; } = keywords ?? [];
    public IReadOnlyList<Statement> Body { get; } = body;
    public IReadOnlyList<Expression> DecoratorList { get; } = decoratorList ?? [];
}

// Match Statement (Python 3.10+)

public sealed class MatchStatement(Expression subject, IReadOnlyList<MatchCase> cases) : Statement
{
    public Expression Subject { get; } = subject;
    public IReadOnlyList<MatchCase> Cases { get; } = cases;
}

public sealed class MatchCase(Pattern pattern, IReadOnlyList<Statement> body, Expression guard = null) : IPythonNode
{
    public Pattern Pattern { get; } = pattern;
    public Expression Guard { get; } = guard;
    public IReadOnlyList<Statement> Body { get; } = body;
}

// ========================================
// Patterns (for match statement)
// ========================================

public abstract class Pattern : IPythonNode
{
}

public sealed class MatchValue(Expression value) : Pattern
{
    public Expression Value { get; } = value;
}

public sealed class MatchSingleton(object value) : Pattern
{
    public object Value { get; } = value;
}

public sealed class MatchSequence(IReadOnlyList<Pattern> patterns) : Pattern
{
    public IReadOnlyList<Pattern> Patterns { get; } = patterns;
}

public sealed class MatchMapping(IReadOnlyList<Expression> keys, IReadOnlyList<Pattern> patterns, string rest = null) : Pattern
{
    public IReadOnlyList<Expression> Keys { get; } = keys;
    public IReadOnlyList<Pattern> Patterns { get; } = patterns;
    public string Rest { get; } = rest;
}

public sealed class MatchClass(Expression cls, IReadOnlyList<Pattern> patterns,
                 IReadOnlyList<string> kwdAttrs = null, IReadOnlyList<Pattern> kwdPatterns = null) : Pattern
{
    public Expression Cls { get; } = cls;
    public IReadOnlyList<Pattern> Patterns { get; } = patterns;
    public IReadOnlyList<string> KwdAttrs { get; } = kwdAttrs ?? [];
    public IReadOnlyList<Pattern> KwdPatterns { get; } = kwdPatterns ?? [];
}

public sealed class MatchStar(string name = null) : Pattern
{
    public string Name { get; } = name;
}

public sealed class MatchAs(string name, Pattern pattern = null) : Pattern
{
    public Pattern Pattern { get; } = pattern;
    public string Name { get; } = name;
}

public sealed class MatchOr(IReadOnlyList<Pattern> patterns) : Pattern
{
    public IReadOnlyList<Pattern> Patterns { get; } = patterns;
}

// ========================================
// Expressions
// ========================================

public abstract class Expression : IPythonNode
{
}

// Literals

public sealed class LiteralExpression(object value, LiteralKind kind) : Expression
{
    public object Value { get; } = value;
    public LiteralKind Kind { get; } = kind;
}

public enum LiteralKind
{
    Integer,
    Float,
    Complex,
    String,
    Bytes,
    Boolean,
    None,
    Ellipsis
}

// F-String (Python 3.6+)

public sealed class JoinedStr(IReadOnlyList<Expression> values) : Expression
{
    public IReadOnlyList<Expression> Values { get; } = values;
}

public sealed class FormattedValue(Expression value, int conversion = -1, Expression formatSpec = null) : Expression
{
    public Expression Value { get; } = value;
    public int Conversion { get; } = conversion;
    public Expression FormatSpec { get; } = formatSpec;
}

// Names and Attributes

public sealed class NameExpression(string id, ExprContext context = ExprContext.Load) : Expression
{
    public string Id { get; } = id;
    public ExprContext Context { get; } = context;
}

public enum ExprContext
{
    Load,
    Store,
    Del
}

public sealed class AttributeExpression(Expression value, string attr, ExprContext context = ExprContext.Load) : Expression
{
    public Expression Value { get; } = value;
    public string Attr { get; } = attr;
    public ExprContext Context { get; } = context;
}

// Subscripting

public sealed class SubscriptExpression(Expression value, Expression slice, ExprContext context = ExprContext.Load) : Expression
{
    public Expression Value { get; } = value;
    public Expression Slice { get; } = slice;
    public ExprContext Context { get; } = context;
}

public sealed class SliceExpression(Expression lower = null, Expression upper = null, Expression step = null) : Expression
{
    public Expression Lower { get; } = lower;
    public Expression Upper { get; } = upper;
    public Expression Step { get; } = step;
}

// Collections

public sealed class ListExpression(IReadOnlyList<Expression> elements, ExprContext context = ExprContext.Load) : Expression
{
    public IReadOnlyList<Expression> Elements { get; } = elements ?? [];
    public ExprContext Context { get; } = context;
}

public sealed class TupleExpression(IReadOnlyList<Expression> elements, ExprContext context = ExprContext.Load) : Expression
{
    public IReadOnlyList<Expression> Elements { get; } = elements ?? [];
    public ExprContext Context { get; } = context;
}

public sealed class SetExpression(IReadOnlyList<Expression> elements) : Expression
{
    public IReadOnlyList<Expression> Elements { get; } = elements ?? [];
}

public sealed class DictExpression(IReadOnlyList<Expression> keys, IReadOnlyList<Expression> values) : Expression
{
    public IReadOnlyList<Expression> Keys { get; } = keys ?? [];
    public IReadOnlyList<Expression> Values { get; } = values ?? [];
}

// Operators

public sealed class BinaryOperation(Expression left, BinaryOperator op, Expression right) : Expression
{
    public Expression Left { get; } = left;
    public BinaryOperator Op { get; } = op;
    public Expression Right { get; } = right;
}

public enum BinaryOperator
{
    Add,        // +
    Sub,        // -
    Mult,       // *
    MatMult,    // @
    Div,        // /
    Mod,        // %
    Pow,        // **
    LShift,     // <<
    RShift,     // >>
    BitOr,      // |
    BitXor,     // ^
    BitAnd,     // &
    FloorDiv    // //
}

public sealed class UnaryOperation(UnaryOperator op, Expression operand) : Expression
{
    public UnaryOperator Op { get; } = op;
    public Expression Operand { get; } = operand;
}

public enum UnaryOperator
{
    Invert,     // ~
    Not,        // not
    UAdd,       // +
    USub        // -
}

public sealed class BooleanOperation(BoolOperator op, IReadOnlyList<Expression> values) : Expression
{
    public BoolOperator Op { get; } = op;
    public IReadOnlyList<Expression> Values { get; } = values;
}

public enum BoolOperator
{
    And,
    Or
}

public sealed class CompareOperation(Expression left, IReadOnlyList<CompareOperator> ops,
                       IReadOnlyList<Expression> comparators) : Expression
{
    public Expression Left { get; } = left;
    public IReadOnlyList<CompareOperator> Ops { get; } = ops;
    public IReadOnlyList<Expression> Comparators { get; } = comparators;
}

public enum CompareOperator
{
    Eq,         // ==
    NotEq,      // !=
    Lt,         // <
    LtE,        // <=
    Gt,         // >
    GtE,        // >=
    Is,         // is
    IsNot,      // is not
    In,         // in
    NotIn       // not in
}

// Function Calls

public sealed class CallExpression(Expression func, IReadOnlyList<Expression> args = null,
                     IReadOnlyList<Keyword> keywords = null) : Expression
{
    public Expression Func { get; } = func;
    public IReadOnlyList<Expression> Args { get; } = args ?? [];
    public IReadOnlyList<Keyword> Keywords { get; } = keywords ?? [];
}

public sealed class Keyword(Expression value, string arg = null) : IPythonNode
{
    public string Arg { get; } = arg;
    public Expression Value { get; } = value;
}

// Conditionals

public sealed class ConditionalExpression(Expression test, Expression body, Expression orElse) : Expression
{
    public Expression Test { get; } = test;
    public Expression Body { get; } = body;
    public Expression OrElse { get; } = orElse;
}

// Lambda

public sealed class LambdaExpression(Arguments args, Expression body) : Expression
{
    public Arguments Args { get; } = args;
    public Expression Body { get; } = body;
}

// Comprehensions

public sealed class ListComprehension(Expression element, IReadOnlyList<Comprehension> generators) : Expression
{
    public Expression Element { get; } = element;
    public IReadOnlyList<Comprehension> Generators { get; } = generators;
}

public sealed class SetComprehension(Expression element, IReadOnlyList<Comprehension> generators) : Expression
{
    public Expression Element { get; } = element;
    public IReadOnlyList<Comprehension> Generators { get; } = generators;
}

public sealed class DictComprehension(Expression key, Expression value, IReadOnlyList<Comprehension> generators) : Expression
{
    public Expression Key { get; } = key;
    public Expression Value { get; } = value;
    public IReadOnlyList<Comprehension> Generators { get; } = generators;
}

public sealed class GeneratorExpression(Expression element, IReadOnlyList<Comprehension> generators) : Expression
{
    public Expression Element { get; } = element;
    public IReadOnlyList<Comprehension> Generators { get; } = generators;
}

public sealed class Comprehension(Expression target, Expression iter, IReadOnlyList<Expression> ifs = null,
                    bool isAsync = false) : IPythonNode
{
    public Expression Target { get; } = target;
    public Expression Iter { get; } = iter;
    public IReadOnlyList<Expression> Ifs { get; } = ifs ?? [];
    public bool IsAsync { get; } = isAsync;
}

// Yield and Await

public sealed class YieldExpression(Expression value = null) : Expression
{
    public Expression Value { get; } = value;
}

public sealed class YieldFromExpression(Expression value) : Expression
{
    public Expression Value { get; } = value;
}

public sealed class AwaitExpression(Expression value) : Expression
{
    public Expression Value { get; } = value;
}

// Starred and Named Expressions

public sealed class StarredExpression(Expression value, ExprContext context = ExprContext.Load) : Expression
{
    public Expression Value { get; } = value;
    public ExprContext Context { get; } = context;
}

public sealed class NamedExpression(Expression target, Expression value) : Expression
{
    public Expression Target { get; } = target;
    public Expression Value { get; } = value;
}

// ========================================
// Async Statements
// ========================================

public sealed class AsyncForStatement(Expression target, Expression iter, IReadOnlyList<Statement> body,
                        IReadOnlyList<Statement> orElse = null, string typeComment = null) : Statement
{
    public Expression Target { get; } = target;
    public Expression Iter { get; } = iter;
    public IReadOnlyList<Statement> Body { get; } = body;
    public IReadOnlyList<Statement> OrElse { get; } = orElse ?? [];
    public string TypeComment { get; } = typeComment;
}

public sealed class AsyncWithStatement(IReadOnlyList<WithItem> items, IReadOnlyList<Statement> body,
                         string typeComment = null) : Statement
{
    public IReadOnlyList<WithItem> Items { get; } = items;
    public IReadOnlyList<Statement> Body { get; } = body;
    public string TypeComment { get; } = typeComment;
}

