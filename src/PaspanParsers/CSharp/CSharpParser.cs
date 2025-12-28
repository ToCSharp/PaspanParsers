using Paspan;
using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.CSharp;

/// <summary>
/// A simplified C# parser demonstrating parsing of basic C# constructs.
/// This is an educational implementation - for production use, consider Roslyn.
/// </summary>
public class CSharpParser
{
    public static readonly Parser<CompilationUnit> CompilationUnitParser;

    static CSharpParser()
    {
        // Basic terminals
        var COMMA = Terms.Char(',');
        var DOT = Terms.Char('.');
        var SEMICOLON = Terms.Char(';');
        var COLON = Terms.Char(':');
        var LPAREN = Terms.Char('(');
        var RPAREN = Terms.Char(')');
        var LBRACE = Terms.Char('{');
        var RBRACE = Terms.Char('}');
        var LBRACKET = Terms.Char('[');
        var RBRACKET = Terms.Char(']');
        var LT = Terms.Char('<');
        var GT = Terms.Char('>');
        var EQ = Terms.Char('=');
        var QUESTION = Terms.Char('?');
        var AT = Terms.Char('@');

        // Keywords
        var NAMESPACE = Terms.Keyword("namespace", caseInsensitive: false);
        var USING = Terms.Keyword("using", caseInsensitive: false);
        var CLASS = Terms.Keyword("class", caseInsensitive: false);
        var STRUCT = Terms.Keyword("struct", caseInsensitive: false);
        var INTERFACE = Terms.Keyword("interface", caseInsensitive: false);
        var ENUM = Terms.Keyword("enum", caseInsensitive: false);
        var RECORD = Terms.Keyword("record", caseInsensitive: false);
        var DELEGATE = Terms.Keyword("delegate", caseInsensitive: false);
        
        // Modifiers
        var PUBLIC = Terms.Keyword("public", caseInsensitive: false);
        var PRIVATE = Terms.Keyword("private", caseInsensitive: false);
        var PROTECTED = Terms.Keyword("protected", caseInsensitive: false);
        var INTERNAL = Terms.Keyword("internal", caseInsensitive: false);
        var STATIC = Terms.Keyword("static", caseInsensitive: false);
        var READONLY = Terms.Keyword("readonly", caseInsensitive: false);
        var CONST = Terms.Keyword("const", caseInsensitive: false);
        var VIRTUAL = Terms.Keyword("virtual", caseInsensitive: false);
        var OVERRIDE = Terms.Keyword("override", caseInsensitive: false);
        var ABSTRACT = Terms.Keyword("abstract", caseInsensitive: false);
        var SEALED = Terms.Keyword("sealed", caseInsensitive: false);
        var PARTIAL = Terms.Keyword("partial", caseInsensitive: false);
        var ASYNC = Terms.Keyword("async", caseInsensitive: false);
        var EXTERN = Terms.Keyword("extern", caseInsensitive: false);
        var UNSAFE = Terms.Keyword("unsafe", caseInsensitive: false);
        var VOLATILE = Terms.Keyword("volatile", caseInsensitive: false);
        var NEW = Terms.Keyword("new", caseInsensitive: false);
        var REQUIRED = Terms.Keyword("required", caseInsensitive: false);
        var REF = Terms.Keyword("ref", caseInsensitive: false);
        var OUT = Terms.Keyword("out", caseInsensitive: false);
        var IN = Terms.Keyword("in", caseInsensitive: false);
        var PARAMS = Terms.Keyword("params", caseInsensitive: false);
        
        // Statement keywords
        var IF = Terms.Keyword("if", caseInsensitive: false);
        var ELSE = Terms.Keyword("else", caseInsensitive: false);
        var WHILE = Terms.Keyword("while", caseInsensitive: false);
        var DO = Terms.Keyword("do", caseInsensitive: false);
        var FOR = Terms.Keyword("for", caseInsensitive: false);
        var FOREACH = Terms.Keyword("foreach", caseInsensitive: false);
        var SWITCH = Terms.Keyword("switch", caseInsensitive: false);
        var CASE = Terms.Keyword("case", caseInsensitive: false);
        var DEFAULT = Terms.Keyword("default", caseInsensitive: false);
        var BREAK = Terms.Keyword("break", caseInsensitive: false);
        var CONTINUE = Terms.Keyword("continue", caseInsensitive: false);
        var RETURN = Terms.Keyword("return", caseInsensitive: false);
        var THROW = Terms.Keyword("throw", caseInsensitive: false);
        var TRY = Terms.Keyword("try", caseInsensitive: false);
        var CATCH = Terms.Keyword("catch", caseInsensitive: false);
        var FINALLY = Terms.Keyword("finally", caseInsensitive: false);
        var LOCK = Terms.Keyword("lock", caseInsensitive: false);
        var YIELD = Terms.Keyword("yield", caseInsensitive: false);
        var AWAIT = Terms.Keyword("await", caseInsensitive: false);
        
        // Other keywords
        var VAR = Terms.Keyword("var", caseInsensitive: false);
        var VOID = Terms.Keyword("void", caseInsensitive: false);
        var GET = Terms.Keyword("get", caseInsensitive: false);
        var SET = Terms.Keyword("set", caseInsensitive: false);
        var INIT = Terms.Keyword("init", caseInsensitive: false);
        var ADD = Terms.Keyword("add", caseInsensitive: false);
        var REMOVE = Terms.Keyword("remove", caseInsensitive: false);
        var WHERE = Terms.Keyword("where", caseInsensitive: false);
        var THIS = Terms.Keyword("this", caseInsensitive: false);
        var BASE = Terms.Keyword("base", caseInsensitive: false);
        var OPERATOR = Terms.Keyword("operator", caseInsensitive: false);
        var IMPLICIT = Terms.Keyword("implicit", caseInsensitive: false);
        var EXPLICIT = Terms.Keyword("explicit", caseInsensitive: false);
        var AS = Terms.Keyword("as", caseInsensitive: false);
        var IS = Terms.Keyword("is", caseInsensitive: false);
        var TYPEOF = Terms.Keyword("typeof", caseInsensitive: false);
        var SIZEOF = Terms.Keyword("sizeof", caseInsensitive: false);
        var NAMEOF = Terms.Keyword("nameof", caseInsensitive: false);
        var WHEN = Terms.Keyword("when", caseInsensitive: false);
        var AND = Terms.Keyword("and", caseInsensitive: false);
        var OR = Terms.Keyword("or", caseInsensitive: false);
        var NOT = Terms.Keyword("not", caseInsensitive: false);
        
        // Literals
        var TRUE = Terms.Keyword("true", caseInsensitive: false);
        var FALSE = Terms.Keyword("false", caseInsensitive: false);
        var NULL = Terms.Keyword("null", caseInsensitive: false);

        // Predefined types
        var OBJECT = Terms.Keyword("object", caseInsensitive: false);
        var STRING = Terms.Keyword("string", caseInsensitive: false);
        var BOOL = Terms.Keyword("bool", caseInsensitive: false);
        var BYTE = Terms.Keyword("byte", caseInsensitive: false);
        var SBYTE = Terms.Keyword("sbyte", caseInsensitive: false);
        var SHORT = Terms.Keyword("short", caseInsensitive: false);
        var USHORT = Terms.Keyword("ushort", caseInsensitive: false);
        var INT = Terms.Keyword("int", caseInsensitive: false);
        var UINT = Terms.Keyword("uint", caseInsensitive: false);
        var LONG = Terms.Keyword("long", caseInsensitive: false);
        var ULONG = Terms.Keyword("ulong", caseInsensitive: false);
        var FLOAT = Terms.Keyword("float", caseInsensitive: false);
        var DOUBLE = Terms.Keyword("double", caseInsensitive: false);
        var DECIMAL = Terms.Keyword("decimal", caseInsensitive: false);
        var CHAR = Terms.Keyword("char", caseInsensitive: false);
        var DYNAMIC = Terms.Keyword("dynamic", caseInsensitive: false);

        // Keywords set - includes LINQ contextual keywords to prevent them being parsed as identifiers
        var keywords = new HashSet<string>
        {
            "namespace", "using", "class", "struct", "interface", "enum", "record", "delegate",
            "public", "private", "protected", "internal", "static", "readonly", "const",
            "virtual", "override", "abstract", "sealed", "partial", "async", "extern", "unsafe",
            "volatile", "new", "required", "ref", "out", "in", "params",
            "if", "else", "while", "do", "for", "foreach", "switch", "case", "default",
            "break", "continue", "return", "throw", "try", "catch", "finally", "lock",
            "yield", "await", "var", "void", "get", "set", "init", "add", "remove",
            "where", "this", "base", "operator", "implicit", "explicit", "as", "is",
            "typeof", "sizeof", "nameof", "when", "true", "false", "null",
            "object", "string", "bool", "byte", "sbyte", "short", "ushort",
            "int", "uint", "long", "ulong", "float", "double", "decimal", "char", "dynamic",
            // LINQ contextual keywords - treated as keywords to ensure proper parsing
            "from", "select", "let", "join", "on", "equals", "into",
            "orderby", "ascending", "descending", "group", "by"
        };

        // Identifiers
        var identifier = Terms.Identifier()
            .When((ctx, id) => !keywords.Contains(id.ToString()))
            .Then(id => id.ToString());

        var verbatimIdentifier = AT.SkipAnd(Terms.Identifier()).Then(id => id.ToString());
        
        var anyIdentifier = verbatimIdentifier.Or(identifier);

        // Qualified name (for namespaces and types)
        var qualifiedName = Separated(DOT, anyIdentifier);

        // ========================================
        // Type Parameters and Type Arguments
        // ========================================

        // Type parameter (for declarations: <T, U>)
        var typeParameter = anyIdentifier.Then(name => new TypeParameter(name));
        var typeParameterList = Separated(COMMA, typeParameter);
        var typeParameters = Between(LT, typeParameterList, GT).Optional();

        // Type constraints
        var typeConstraint = Deferred<TypeConstraint>();
        
        var classConstraint = CLASS.And(QUESTION.Optional())
            .Then<TypeConstraint>(result =>
            {
                var (_, nullable) = result;
                return new ClassConstraint(nullable.HasValue);
            });

        var structConstraint = STRUCT.Then<TypeConstraint>(new StructConstraint());
        
        var unmanagedConstraint = Terms.Keyword("unmanaged", caseInsensitive: false)
            .Then<TypeConstraint>(new UnmanagedConstraint());
        
        var notnullConstraint = Terms.Keyword("notnull", caseInsensitive: false)
            .Then<TypeConstraint>(new NotNullConstraint());

        var constructorConstraint = NEW.SkipAnd(LPAREN).AndSkip(RPAREN)
            .Then<TypeConstraint>(new ConstructorConstraint());

        // Literals
        var integerLiteral = Terms.Integer()
            .Then<Expression>(i => new LiteralExpression((int)i, LiteralKind.Integer));

        var decimalLiteral = Terms.Decimal()
            .Then<Expression>(d => new LiteralExpression((decimal)d, LiteralKind.Real));

        var stringLiteral = Terms.String(StringLiteralQuotes.Double)
            .Then<Expression>(s => new LiteralExpression(s.ToString(), LiteralKind.String));

        var charLiteral = Between(Terms.Char('\''), Literals.NoneOf("'"), Terms.Char('\''))
            .Then<Expression>(c => new LiteralExpression(c.ToString()[0], LiteralKind.Character));

        var boolLiteral = TRUE.Then<Expression>(new LiteralExpression(true, LiteralKind.Boolean))
            .Or(FALSE.Then<Expression>(new LiteralExpression(false, LiteralKind.Boolean)));

        var nullLiteral = NULL.Then<Expression>(new LiteralExpression(null, LiteralKind.Null));

        // Deferred parsers
        var expression = Deferred<Expression>();
        var statement = Deferred<Statement>();
        var typeReference = Deferred<TypeReference>();
        var memberDeclaration = Deferred<MemberDeclaration>();

        // Predefined types
        var predefinedType = 
            OBJECT.Then(PredefinedType.Object)
            .Or(STRING.Then(PredefinedType.String))
            .Or(BOOL.Then(PredefinedType.Bool))
            .Or(BYTE.Then(PredefinedType.Byte))
            .Or(SBYTE.Then(PredefinedType.SByte))
            .Or(SHORT.Then(PredefinedType.Short))
            .Or(USHORT.Then(PredefinedType.UShort))
            .Or(INT.Then(PredefinedType.Int))
            .Or(UINT.Then(PredefinedType.UInt))
            .Or(LONG.Then(PredefinedType.Long))
            .Or(ULONG.Then(PredefinedType.ULong))
            .Or(FLOAT.Then(PredefinedType.Float))
            .Or(DOUBLE.Then(PredefinedType.Double))
            .Or(DECIMAL.Then(PredefinedType.Decimal))
            .Or(CHAR.Then(PredefinedType.Char))
            .Or(VOID.Then(PredefinedType.Void))
            .Or(DYNAMIC.Then(PredefinedType.Dynamic));

        // Type constraint (for where clauses)
        typeConstraint.Parser = classConstraint
            .Or(structConstraint)
            .Or(unmanagedConstraint)
            .Or(notnullConstraint)
            .Or(constructorConstraint)
            .Or(typeReference.Then<TypeConstraint>(t => new TypeReferenceConstraint(t)));

        var typeConstraints = Separated(COMMA, typeConstraint);

        // Type parameter constraint (where T : class, new())
        var typeParameterConstraintClause = WHERE.SkipAnd(anyIdentifier).AndSkip(COLON).And(typeConstraints)
            .Then(result =>
            {
                var (paramName, constraints) = result;
                return new TypeParameterConstraint(paramName, constraints);
            });

        var typeParameterConstraintClauses = ZeroOrMany(typeParameterConstraintClause);

        // ========================================
        // Attributes
        // ========================================

        // Attribute target keywords
        var ATTR_ASSEMBLY = Terms.Keyword("assembly", caseInsensitive: false);
        var ATTR_MODULE = Terms.Keyword("module", caseInsensitive: false);
        var ATTR_FIELD = Terms.Keyword("field", caseInsensitive: false);
        var ATTR_EVENT = Terms.Keyword("event", caseInsensitive: false);
        var ATTR_METHOD = Terms.Keyword("method", caseInsensitive: false);
        var ATTR_PARAM = Terms.Keyword("param", caseInsensitive: false);
        var ATTR_PROPERTY = Terms.Keyword("property", caseInsensitive: false);
        var ATTR_RETURN = Terms.Keyword("return", caseInsensitive: false);
        var ATTR_TYPE = Terms.Keyword("type", caseInsensitive: false);

        // Attribute target
        var attributeTarget = 
            ATTR_ASSEMBLY.Then(AttributeTarget.Assembly)
            .Or(ATTR_MODULE.Then(AttributeTarget.Module))
            .Or(ATTR_FIELD.Then(AttributeTarget.Field))
            .Or(ATTR_EVENT.Then(AttributeTarget.Event))
            .Or(ATTR_METHOD.Then(AttributeTarget.Method))
            .Or(ATTR_PARAM.Then(AttributeTarget.Param))
            .Or(ATTR_PROPERTY.Then(AttributeTarget.Property))
            .Or(ATTR_RETURN.Then(AttributeTarget.Return))
            .Or(ATTR_TYPE.Then(AttributeTarget.Type));

        var attributeTargetSpecifier = attributeTarget.AndSkip(COLON);

        // Attribute argument (either positional or named)
        var attributeArgument = Deferred<Argument>();
        
        var namedArgument = anyIdentifier.AndSkip(EQ).And(expression)
            .Then<Argument>(result =>
            {
                var (name, expr) = result;
                return new Argument(expr, name);
            });

        var positionalArgument = expression
            .Then<Argument>(expr => new Argument(expr, null));

        attributeArgument.Parser = namedArgument.Or(positionalArgument);

        var attributeArgumentList = Separated(COMMA, attributeArgument);

        // Attribute
        var attributeArguments = Between(LPAREN, attributeArgumentList, RPAREN).Optional();
        
        var attribute = qualifiedName.And(attributeArguments)
            .Then(result =>
            {
                var (name, args) = result;
                return new AttributeNode(
                    new NameExpression(name),
                    args.HasValue && args.Value.Count != 0 ? args.Value : null
                );
            });

        var attributeList = Separated(COMMA, attribute);

        // Attribute section: [AttributeTarget: Attr1, Attr2]
        var attributeSection = Between(
                LBRACKET,
                attributeTargetSpecifier.Optional().And(attributeList),
                RBRACKET
            )
            .Then(result =>
            {
                var (target, attrs) = result;
                AttributeTarget? targetValue = target.HasValue ? target.Value : null;
                return new AttributeSection(attrs, targetValue);
            });

        var attributes = ZeroOrMany(attributeSection);

        // ========================================
        // Type references
        // ========================================

        var predefinedTypeRef = predefinedType.And(QUESTION.Optional())
            .Then<TypeReference>(result =>
            {
                var (type, nullable) = result;
                return new PredefinedTypeReference(type, nullable.HasValue);
            });

        // Named type with optional type arguments
        var typeArgs = Between(LT, Separated(COMMA, typeReference), GT).Optional();
        
        var namedTypeRef = qualifiedName.And(typeArgs).And(QUESTION.Optional())
            .Then<TypeReference>(result =>
            {
                var (parts, typeArgsList, nullable) = result;
                return new NamedTypeReference(
                    new NameExpression(parts),
                    typeArgsList.HasValue ? (IReadOnlyList<TypeReference>)typeArgsList.Value : null,
                    nullable.HasValue
                );
            });

        // Base type reference (predefined or named, optionally nullable)
        var baseTypeRef = predefinedTypeRef.Or(namedTypeRef);

        // Array type is a base type followed by one or more array rank specifiers
        var arrayTypeRef = baseTypeRef.And(OneOrMany(Between(LBRACKET, ZeroOrMany(COMMA), RBRACKET)))
            .Then<TypeReference>(result =>
            {
                var (elementType, ranks) = result;
                var current = elementType;
                foreach (var commas in ranks)
                {
                    current = new ArrayTypeReference(current, commas.Count + 1);
                }
                return current;
            });

        typeReference.Parser = arrayTypeRef.Or(baseTypeRef);

        // Modifiers
        var modifierKeyword = 
            PUBLIC.Then(Modifiers.Public)
            .Or(PRIVATE.Then(Modifiers.Private))
            .Or(PROTECTED.Then(Modifiers.Protected))
            .Or(INTERNAL.Then(Modifiers.Internal))
            .Or(STATIC.Then(Modifiers.Static))
            .Or(READONLY.Then(Modifiers.Readonly))
            .Or(CONST.Then(Modifiers.Const))
            .Or(VIRTUAL.Then(Modifiers.Virtual))
            .Or(OVERRIDE.Then(Modifiers.Override))
            .Or(ABSTRACT.Then(Modifiers.Abstract))
            .Or(SEALED.Then(Modifiers.Sealed))
            .Or(PARTIAL.Then(Modifiers.Partial))
            .Or(ASYNC.Then(Modifiers.Async))
            .Or(EXTERN.Then(Modifiers.Extern))
            .Or(UNSAFE.Then(Modifiers.Unsafe))
            .Or(VOLATILE.Then(Modifiers.Volatile))
            .Or(NEW.Then(Modifiers.New))
            .Or(REQUIRED.Then(Modifiers.Required))
            .Or(REF.Then(Modifiers.Ref));

        var modifiers = ZeroOrMany(modifierKeyword)
            .Then(mods =>
            {
                var result = Modifiers.None;
                foreach (var mod in mods)
                {
                    result |= mod;
                }
                return result;
            });

        // Expressions
        var nameExpr = qualifiedName.Then<Expression>(parts => new NameExpression(parts));

        var literal = decimalLiteral.Or(integerLiteral).Or(stringLiteral).Or(charLiteral).Or(boolLiteral).Or(nullLiteral);

        var parenExpr = Between(LPAREN, expression, RPAREN)
            .Then<Expression>(expr => new ParenthesizedExpression(expr));

        // Arguments
        var argument = expression.Then(expr => new Argument(expr));
        var argumentList = Separated(COMMA, argument);

        // Invocation
        var invocation = nameExpr.And(Between(LPAREN, argumentList.Else([]), RPAREN))
            .Then<Expression>(result =>
            {
                var (expr, args) = result;
                return new InvocationExpression(expr, args);
            });

        // Member access
        var memberAccess = expression.AndSkip(DOT).And(anyIdentifier)
            .Then<Expression>(result =>
            {
                var (target, member) = result;
                return new MemberAccessExpression(member, target);
            });

        // Element access
        var elementAccess = expression.And(Between(LBRACKET, argumentList, RBRACKET))
            .Then<Expression>(result =>
            {
                var (target, args) = result;
                return new ElementAccessExpression(target, args);
            });

        // Primary expressions
        var primary = invocation
            .Or(parenExpr)
            .Or(literal)
            .Or(nameExpr);

        // Unary expressions
        var unaryPlus = Terms.Char('+').SkipAnd(primary)
            .Then<Expression>(expr => new UnaryExpression(UnaryOperator.Plus, expr));
        var unaryMinus = Terms.Char('-').SkipAnd(primary)
            .Then<Expression>(expr => new UnaryExpression(UnaryOperator.Minus, expr));
        var logicalNot = Terms.Char('!').SkipAnd(primary)
            .Then<Expression>(expr => new UnaryExpression(UnaryOperator.Not, expr));
        var bitwiseNot = Terms.Char('~').SkipAnd(primary)
            .Then<Expression>(expr => new UnaryExpression(UnaryOperator.BitwiseNot, expr));

        var unary = unaryPlus.Or(unaryMinus).Or(logicalNot).Or(bitwiseNot).Or(primary);

        // Declare deferred parsers for forward references
        var pattern = Deferred<Pattern>();
        var switchExpressionArm = Deferred<SwitchExpressionArm>();

        // Switch expression: expr switch { pattern => expr, ... }
        // Must be defined here to be used by multiplicative, but parser set later
        var switchExpressionArms = Separated(COMMA, switchExpressionArm);

        var switchExpression = unary.And(SWITCH.SkipAnd(Between(LBRACE, switchExpressionArms, RBRACE)).Optional())
            .Then<Expression>(result =>
            {
                var (expr, arms) = result;
                if (arms.HasValue && arms.Value.Count != 0)
                {
                    return new SwitchExpression(expr, (IReadOnlyList<SwitchExpressionArm>)arms.Value);
                }
                return expr;
            });

        // Binary expressions with precedence
        var multiplicative = switchExpression.LeftAssociative(
            (Terms.Char('*'), (a, b) => new BinaryExpression(a, BinaryOperator.Multiply, b)),
            (Terms.Char('/'), (a, b) => new BinaryExpression(a, BinaryOperator.Divide, b)),
            (Terms.Char('%'), (a, b) => new BinaryExpression(a, BinaryOperator.Modulo, b))
        );

        var additive = multiplicative.LeftAssociative(
            (Terms.Char('+'), (a, b) => new BinaryExpression(a, BinaryOperator.Add, b)),
            (Terms.Char('-'), (a, b) => new BinaryExpression(a, BinaryOperator.Subtract, b))
        );

        var shift = additive.LeftAssociative(
            (Terms.Text("<<"), (a, b) => new BinaryExpression(a, BinaryOperator.LeftShift, b)),
            (Terms.Text(">>"), (a, b) => new BinaryExpression(a, BinaryOperator.RightShift, b))
        );

        var relational = shift.LeftAssociative(
            (Terms.Text("<="), (a, b) => new BinaryExpression(a, BinaryOperator.LessThanOrEqual, b)),
            (Terms.Text(">="), (a, b) => new BinaryExpression(a, BinaryOperator.GreaterThanOrEqual, b)),
            (Terms.Text("<"), (a, b) => new BinaryExpression(a, BinaryOperator.LessThan, b)),
            (Terms.Text(">"), (a, b) => new BinaryExpression(a, BinaryOperator.GreaterThan, b))
        );

        // Is expression: expr is pattern (defined later in pattern matching section)
        var isExpression = Deferred<Expression>();

        var equality = isExpression.LeftAssociative(
            (Terms.Text("=="), (a, b) => new BinaryExpression(a, BinaryOperator.Equal, b)),
            (Terms.Text("!="), (a, b) => new BinaryExpression(a, BinaryOperator.NotEqual, b))
        );

        var bitwiseAnd = equality.LeftAssociative(
            (Terms.Char('&'), (a, b) => new BinaryExpression(a, BinaryOperator.BitwiseAnd, b))
        );

        var bitwiseXor = bitwiseAnd.LeftAssociative(
            (Terms.Char('^'), (a, b) => new BinaryExpression(a, BinaryOperator.BitwiseXor, b))
        );

        var bitwiseOr = bitwiseXor.LeftAssociative(
            (Terms.Char('|'), (a, b) => new BinaryExpression(a, BinaryOperator.BitwiseOr, b))
        );

        var logicalAnd = bitwiseOr.LeftAssociative(
            (Terms.Text("&&"), (a, b) => new BinaryExpression(a, BinaryOperator.And, b))
        );

        var logicalOr = logicalAnd.LeftAssociative(
            (Terms.Text("||"), (a, b) => new BinaryExpression(a, BinaryOperator.Or, b))
        );

        // Conditional expression
        var conditional = logicalOr.And(QUESTION.SkipAnd(expression).AndSkip(COLON).And(expression).Optional())
            .Then<Expression>(result =>
            {
                var (condition, rest) = result;
                if (rest.HasValue)
                {
                    var (trueExpr, falseExpr) = rest.Value;
                    return new ConditionalExpression(condition, trueExpr, falseExpr);
                }
                return condition;
            });

        // Assignment
        var assignment = conditional.And(EQ.SkipAnd(expression).Optional())
            .Then<Expression>(result =>
            {
                var (left, right) = result;
                if (right.HasValue)
                {
                    return new BinaryExpression(left, BinaryOperator.Assign, right.Value);
                }
                return left;
            });

        // ========================================
        // Pattern Matching
        // ========================================

        // Set switchExpressionArm parser now that assignment is defined
        switchExpressionArm.Parser = pattern.And(WHEN.SkipAnd(assignment).Optional()).AndSkip(Terms.Text("=>")).And(assignment)
            .Then(result =>
            {
                var (pat, guard, expr) = result;
                return new SwitchExpressionArm(pat, expr, guard.OrSome(null));
            });

        // Pattern was already declared above for switch expressions

        // Discard pattern: _
        var discardPattern = Terms.Char('_')
            .Then<Pattern>(_ => new DiscardPattern());

        // Constant pattern: literal or constant expression
        var constantPattern = primary
            .Then<Pattern>(expr => new ConstantPattern(expr));

        // Var pattern: var x
        var varPattern = VAR.SkipAnd(anyIdentifier)
            .Then<Pattern>(id => new VarPattern(id));

        // Type pattern: just a type
        var typePattern = typeReference
            .Then<Pattern>(type => new TypePattern(type));

        // Declaration pattern: Type identifier
        var declarationPattern = typeReference.And(anyIdentifier)
            .Then<Pattern>(result =>
            {
                var (type, identifier) = result;
                return new DeclarationPattern(type, identifier);
            });

        // Relational pattern: < expr, <= expr, > expr, >= expr
        var relationalPattern =
            Terms.Text("<=").SkipAnd(primary).Then<Pattern>(expr => new RelationalPattern(RelationalOperator.LessThanOrEqual, expr))
            .Or(Terms.Text(">=").SkipAnd(primary).Then<Pattern>(expr => new RelationalPattern(RelationalOperator.GreaterThanOrEqual, expr)))
            .Or(Terms.Text("<").SkipAnd(primary).Then<Pattern>(expr => new RelationalPattern(RelationalOperator.LessThan, expr)))
            .Or(Terms.Text(">").SkipAnd(primary).Then<Pattern>(expr => new RelationalPattern(RelationalOperator.GreaterThan, expr)));

        // Property subpattern: PropertyName: pattern
        var propertySubPattern = anyIdentifier.AndSkip(COLON).And(pattern)
            .Then(result =>
            {
                var (propName, pat) = result;
                return new PropertySubPattern(propName, pat);
            });

        var propertySubPatternList = Separated(COMMA, propertySubPattern);

        // Recursive pattern (property pattern): Type { Prop1: pattern1, Prop2: pattern2 } designation
        // or just { Prop1: pattern1 } designation
        var recursivePattern = typeReference.Optional()
            .And(Between(LBRACE, propertySubPatternList.Else([]), RBRACE))
            .And(anyIdentifier.Optional())
            .Then<Pattern>(result =>
            {
                var (type, props, designation) = result;
                return new RecursivePattern(
                    type.OrSome(null),
                    null,
                    props.Count != 0 ? (IReadOnlyList<PropertySubPattern>)props : null,
                    designation.OrSome(null)
                );
            });

        // Logical patterns: pattern and pattern, pattern or pattern, not pattern
        var notPattern = Deferred<Pattern>();
        var andPattern = Deferred<Pattern>();
        var orPattern = Deferred<Pattern>();

        // Basic pattern (without logical operators)
        // Order matters: try more specific patterns first
        var basicPattern = discardPattern
            .Or(varPattern)
            .Or(relationalPattern)
            .Or(recursivePattern)
            .Or(declarationPattern)  // Try declaration pattern first (Type identifier)
            .Or(typePattern)          // Then type pattern (just Type)
            .Or(constantPattern);

        // not pattern
        notPattern.Parser = NOT.SkipAnd(pattern)
            .Then<Pattern>(p => new LogicalPattern(LogicalPatternKind.Not, p));

        // and pattern
        andPattern.Parser = basicPattern.And(AND.SkipAnd(pattern))
            .Then<Pattern>(result =>
            {
                var (left, right) = result;
                return new LogicalPattern(LogicalPatternKind.And, left, right);
            });

        // or pattern
        orPattern.Parser = basicPattern.And(OR.SkipAnd(pattern))
            .Then<Pattern>(result =>
            {
                var (left, right) = result;
                return new LogicalPattern(LogicalPatternKind.Or, left, right);
            });

        // Pattern combines all pattern types
        pattern.Parser = notPattern.Or(andPattern).Or(orPattern).Or(basicPattern);

        // Set isExpression parser now that pattern is defined
        isExpression.Parser = relational.And(IS.SkipAnd(pattern).Optional())
            .Then<Expression>(result =>
            {
                var (expr, pat) = result;
                if (pat.HasValue)
                {
                    return new IsExpression(expr, pat.Value);
                }
                return expr;
            });

        // Note: expression.Parser will be set later after lambda and LINQ support is defined

        // Statements
        var block = Deferred<BlockStatement>();

        // Expression statement
        var expressionStatement = expression.AndSkip(SEMICOLON)
            .Then<Statement>(expr => new ExpressionStatement(expr));

        // Variable declarator
        var variableDeclarator = anyIdentifier.And(EQ.SkipAnd(expression).Optional())
            .Then(result =>
            {
                var (name, init) = result;
                return new VariableDeclarator(name, init.OrSome(null));
            });

        var variableDeclarators = Separated(COMMA, variableDeclarator);

        // Local variable declaration
        var localVarDecl = typeReference.And(variableDeclarators).AndSkip(SEMICOLON)
            .Then<Statement>(result =>
            {
                var (type, vars) = result;
                return new LocalDeclarationStatement(type, vars);
            });

        // If statement
        var ifStatement = IF.SkipAnd(Between(LPAREN, expression, RPAREN))
            .And(statement)
            .And(ELSE.SkipAnd(statement).Optional())
            .Then<Statement>(result =>
            {
                var (condition, thenStmt, elseStmt) = result;
                return new IfStatement(condition, thenStmt, elseStmt.OrSome(null));
            });

        // While statement
        var whileStatement = WHILE.SkipAnd(Between(LPAREN, expression, RPAREN))
            .And(statement)
            .Then<Statement>(result =>
            {
                var (condition, body) = result;
                return new WhileStatement(condition, body);
            });

        // Do-while statement
        var doStatement = DO.SkipAnd(statement).AndSkip(WHILE)
            .And(Between(LPAREN, expression, RPAREN)).AndSkip(SEMICOLON)
            .Then<Statement>(result =>
            {
                var (body, condition) = result;
                return new DoStatement(body, condition);
            });

        // For statement
        var forInit = localVarDecl.Or(expressionStatement).Or(SEMICOLON.Then<Statement>(_ => null));
        var forCondition = expression.Optional().AndSkip(SEMICOLON);
        var forIterator = Separated(COMMA, expression);

        var forStatement = FOR.SkipAnd(LPAREN)
            .SkipAnd(forInit.Else(new List<Statement>().Cast<Statement>().FirstOrDefault()))
            .And(forCondition)
            .And(forIterator.Else([]))
            .AndSkip(RPAREN)
            .And(statement)
            .Then<Statement>(result =>
            {
                var (init, condition, iterators, body) = result;
                return new ForStatement(
                    body,
                    init != null ? new[] { init } : null,
                    condition.OrSome(null),
                    iterators.Count != 0 ? iterators : null
                );
            });

        // Return statement
        var returnStatement = RETURN.SkipAnd(expression.Optional()).AndSkip(SEMICOLON)
            .Then<Statement>(expr => new ReturnStatement(expr.OrSome(null)));

        // Break/Continue
        var breakStatement = BREAK.AndSkip(SEMICOLON).Then<Statement>(new BreakStatement());
        var continueStatement = CONTINUE.AndSkip(SEMICOLON).Then<Statement>(new ContinueStatement());

        // Throw statement
        var throwStatement = THROW.SkipAnd(expression.Optional()).AndSkip(SEMICOLON)
            .Then<Statement>(expr => new ThrowStatement(expr.OrSome(null)));

        // Block statement
        var statementList = ZeroOrMany(statement);
        block.Parser = Between(LBRACE, statementList, RBRACE)
            .Then<BlockStatement>(stmts => new BlockStatement(stmts.Count != 0 ? stmts : null));

        statement.Parser = block.Then<Statement>(b => b)
            .Or(ifStatement)
            .Or(whileStatement)
            .Or(doStatement)
            .Or(forStatement)
            .Or(returnStatement)
            .Or(breakStatement)
            .Or(continueStatement)
            .Or(throwStatement)
            .Or(localVarDecl)
            .Or(expressionStatement);

        // Parameters
        var parameterModifier = 
            REF.Then(ParameterModifier.Ref)
            .Or(OUT.Then(ParameterModifier.Out))
            .Or(IN.Then(ParameterModifier.In))
            .Or(PARAMS.Then(ParameterModifier.Params))
            .Or(THIS.Then(ParameterModifier.This));

        var parameter = parameterModifier.Else(ParameterModifier.None)
            .And(typeReference)
            .And(anyIdentifier)
            .And(EQ.SkipAnd(expression).Optional())
            .Then(result =>
            {
                var (modifier, type, name, defaultValue) = result;
                return new Parameter(type, name, modifier, defaultValue.OrSome(null));
            });

        var parameterList = Separated(COMMA, parameter);

        // Method body
        var blockMethodBody = block.Then<MethodBody>(b => new BlockMethodBody(b));
        var expressionMethodBody = Terms.Text("=>").SkipAnd(expression).AndSkip(SEMICOLON)
            .Then<MethodBody>(expr => new ExpressionMethodBody(expr));
        var abstractMethodBody = SEMICOLON.Then<MethodBody>(_ => null);

        var methodBody = blockMethodBody.Or(expressionMethodBody).Or(abstractMethodBody);

        // Field declaration
        var fieldDeclaration = attributes.And(modifiers).And(typeReference).And(variableDeclarators).AndSkip(SEMICOLON)
            .Then<MemberDeclaration>(result =>
            {
                var (attrs, mods, type, vars) = result;
                return new FieldDeclaration(type, vars, attrs.Count != 0 ? (IReadOnlyList<AttributeSection>)attrs : null, mods);
            });

        // Property accessors
        var getAccessor = GET.And(block.Optional().AndSkip(SEMICOLON.Optional()))
            .Then(result =>
            {
                var (_, body) = result;
                return new Accessor(
                    AccessorKind.Get,
                    null,
                    Modifiers.None,
                    body.HasValue ? new BlockMethodBody(body.Value) : null
                );
            });

        var setAccessor = SET.And(block.Optional().AndSkip(SEMICOLON.Optional()))
            .Then(result =>
            {
                var (_, body) = result;
                return new Accessor(
                    AccessorKind.Set,
                    null,
                    Modifiers.None,
                    body.HasValue ? new BlockMethodBody(body.Value) : null
                );
            });

        var initAccessor = INIT.And(block.Optional().AndSkip(SEMICOLON.Optional()))
            .Then(result =>
            {
                var (_, body) = result;
                return new Accessor(
                    AccessorKind.Init,
                    null,
                    Modifiers.None,
                    body.HasValue ? new BlockMethodBody(body.Value) : null
                );
            });

        var accessor = getAccessor.Or(setAccessor).Or(initAccessor);
        var accessorList = OneOrMany(accessor);

        // Property declaration
        var propertyDeclaration = attributes.And(modifiers).And(typeReference).And(anyIdentifier)
            .And(Between(LBRACE, accessorList, RBRACE))
            .Then<MemberDeclaration>(result =>
            {
                var (attrs, mods, type, name, accessors) = result;
                return new PropertyDeclaration(type, name, attrs.Count != 0 ? (IReadOnlyList<AttributeSection>)attrs : null, mods, accessors);
            });

        // Method declaration
        var methodDeclaration = attributes.And(modifiers).And(typeReference).And(anyIdentifier)
            .And(typeParameters)
            .And(Between(LPAREN, parameterList.Else([]), RPAREN))
            .And(typeParameterConstraintClauses)
            .And(methodBody)
            .Then<MemberDeclaration>(result =>
            {
                var (attrs, mods, returnType, name, typeParams, parameters, constraints, body) = result;
                return new MethodDeclaration(
                    returnType,
                    name,
                    attrs.Count != 0 ? (IReadOnlyList<AttributeSection>)attrs : null,
                    mods,
                    typeParams.HasValue ? typeParams.Value : null,
                    parameters,
                    constraints.Count != 0 ? constraints : null,
                    body
                );
            });

        // Constructor declaration
        var constructorDeclaration = attributes.And(modifiers).And(anyIdentifier)
            .And(Between(LPAREN, parameterList.Else([]), RPAREN))
            .And(methodBody)
            .Then<MemberDeclaration>(result =>
            {
                var (attrs, mods, name, parameters, body) = result;
                return new ConstructorDeclaration(name, attrs.Count != 0 ? (IReadOnlyList<AttributeSection>)attrs : null, mods, parameters, null, body);
            });

        // Member declaration
        memberDeclaration.Parser = methodDeclaration
            .Or(propertyDeclaration)
            .Or(constructorDeclaration)
            .Or(fieldDeclaration);

        var memberList = ZeroOrMany(memberDeclaration);

        // Class declaration
        var baseTypeList = Separated(COMMA, typeReference);
        var baseClause = COLON.SkipAnd(baseTypeList);

        var classDeclaration = attributes.And(modifiers).AndSkip(PARTIAL.Optional()).AndSkip(CLASS).And(anyIdentifier)
            .And(typeParameters)
            .And(baseClause.Optional())
            .And(typeParameterConstraintClauses)
            .And(Between(LBRACE, memberList, RBRACE))
            .Then<MemberDeclaration>(result =>
            {
                var (attrs, mods, name, typeParams, baseTypes, constraints, members) = result;
                return new ClassDeclaration(
                    name,
                    attrs.Count != 0 ? (IReadOnlyList<AttributeSection>)attrs : null,
                    mods,
                    typeParams.HasValue ? typeParams.Value : null,
                    baseTypes.OrSome(null),
                    constraints.Count != 0 ? constraints : null,
                    members.Count != 0 ? members : null
                );
            });

        // Struct declaration
        var structDeclaration = attributes.And(modifiers).AndSkip(PARTIAL.Optional()).AndSkip(STRUCT).And(anyIdentifier)
            .And(typeParameters)
            .And(baseClause.Optional())
            .And(typeParameterConstraintClauses)
            .And(Between(LBRACE, memberList, RBRACE))
            .Then<MemberDeclaration>(result =>
            {
                var (attrs, mods, name, typeParams, interfaces, constraints, members) = result;
                return new StructDeclaration(
                    name,
                    attrs.Count != 0 ? (IReadOnlyList<AttributeSection>)attrs : null,
                    mods,
                    typeParams.HasValue ? typeParams.Value : null,
                    interfaces.OrSome(null),
                    constraints.Count != 0 ? constraints : null,
                    members.Count != 0 ? members : null
                );
            });

        // Interface declaration
        var interfaceDeclaration = attributes.And(modifiers).AndSkip(PARTIAL.Optional()).AndSkip(INTERFACE).And(anyIdentifier)
            .And(typeParameters)
            .And(baseClause.Optional())
            .And(typeParameterConstraintClauses)
            .And(Between(LBRACE, memberList, RBRACE))
            .Then<MemberDeclaration>(result =>
            {
                var (attrs, mods, name, typeParams, baseInterfaces, constraints, members) = result;
                return new InterfaceDeclaration(
                    name,
                    attrs.Count != 0 ? (IReadOnlyList<AttributeSection>)attrs : null,
                    mods,
                    typeParams.HasValue ? typeParams.Value : null,
                    baseInterfaces.OrSome(null),
                    constraints.Count != 0 ? constraints : null,
                    members.Count != 0 ? members : null
                );
            });

        // Enum member
        var enumMember = attributes.And(anyIdentifier).And(EQ.SkipAnd(expression).Optional())
            .Then(result =>
            {
                var (attrs, name, value) = result;
                return new EnumMember(name, value.OrSome(null), attrs.Count != 0 ? (IReadOnlyList<AttributeSection>)attrs : null);
            });

        var enumMemberList = Separated(COMMA, enumMember);

        // Enum declaration
        var enumDeclaration = attributes.And(modifiers).AndSkip(ENUM).And(anyIdentifier)
            .And(baseClause.Optional())
            .And(Between(LBRACE, enumMemberList.Else([]), RBRACE))
            .Then<MemberDeclaration>(result =>
            {
                var (attrs, mods, name, baseType, members) = result;
                return new EnumDeclaration(
                    name,
                    attrs.Count != 0 ? (IReadOnlyList<AttributeSection>)attrs : null,
                    mods,
                    baseType.HasValue ? baseType.Value.FirstOrDefault() : null,
                    members.Count != 0 ? (IReadOnlyList<EnumMember>)members : null
                );
            });

        // Type declaration
        var typeDeclaration = classDeclaration
            .Or(structDeclaration)
            .Or(interfaceDeclaration)
            .Or(enumDeclaration);

        // ========================================
        // Lambda Expressions
        // ========================================

        var ARROW = Terms.Text("=>");
        var FROM = Terms.Keyword("from", caseInsensitive: false);
        var SELECT = Terms.Keyword("select", caseInsensitive: false);
        var WHERE_KW = Terms.Keyword("where", caseInsensitive: false);
        var LET = Terms.Keyword("let", caseInsensitive: false);
        var JOIN = Terms.Keyword("join", caseInsensitive: false);
        var ON = Terms.Keyword("on", caseInsensitive: false);
        var EQUALS = Terms.Keyword("equals", caseInsensitive: false);
        var INTO = Terms.Keyword("into", caseInsensitive: false);
        var ORDERBY = Terms.Keyword("orderby", caseInsensitive: false);
        var ASCENDING = Terms.Keyword("ascending", caseInsensitive: false);
        var DESCENDING = Terms.Keyword("descending", caseInsensitive: false);
        var GROUP = Terms.Keyword("group", caseInsensitive: false);
        var BY = Terms.Keyword("by", caseInsensitive: false);

        // Lambda parameter (explicit with type or implicit without type)
        var explicitLambdaParam = parameterModifier.Else(ParameterModifier.None)
            .And(typeReference)
            .And(anyIdentifier)
            .Then(result =>
            {
                var (modifier, type, name) = result;
                return new Parameter(type, name, modifier, null);
            });

        var implicitLambdaParam = anyIdentifier
            .Then(name => new Parameter(null, name, ParameterModifier.None, null));

        // Single parameter without parentheses: x => x * x
        var singleImplicitParam = implicitLambdaParam;

        // Multiple parameters with parentheses: (x, y) => x + y  or  (int x, int y) => x + y
        // Try implicit param first (simpler pattern) to avoid consuming input with explicit param
        var lambdaParamList = Separated(COMMA, implicitLambdaParam.Or(explicitLambdaParam));
        var multipleParams = Between(LPAREN, lambdaParamList.Else([]), RPAREN);

        // Lambda body: will be defined after expression is set up
        var lambdaBody = Deferred<LambdaBody>();

        // Lambda with parenthesized parameters: (x, y) => ...  or  () => ...
        var lambdaWithParens = ASYNC.Optional()
            .And(multipleParams)
            .AndSkip(ARROW)
            .And(lambdaBody)
            .Then<Expression>(result =>
            {
                var (isAsync, parameters, body) = result;
                return new LambdaExpression(body, parameters, isAsync.HasValue);
            });

        // Lambda with single parameter: x => ...  or  async x => ...
        var lambdaWithSingleParam = ASYNC.Optional()
            .And(singleImplicitParam)
            .AndSkip(ARROW)
            .And(lambdaBody)
            .Then<Expression>(result =>
            {
                var (isAsync, parameter, body) = result;
                return new LambdaExpression(body, [parameter], isAsync.HasValue);
            });

        // Try parenthesized first (more specific), then single parameter
        var lambda = lambdaWithParens.Or(lambdaWithSingleParam);

        // ========================================
        // LINQ Query Expressions
        // ========================================

        // From clause: from x in collection  or  from int x in collection
        var fromClause = FROM.SkipAnd(typeReference.Optional())
            .And(anyIdentifier)
            .AndSkip(IN)
            .And(assignment)
            .Then(result =>
            {
                var (type, identifier, expr) = result;
                return new FromClause(identifier, expr, type.OrSome(null));
            });

        // Where clause: where condition
        var whereClause = WHERE_KW.SkipAnd(assignment)
            .Then<QueryClause>(expr => new WhereClause(expr));

        // Let clause: let x = expression
        var letClause = LET.SkipAnd(anyIdentifier).AndSkip(EQ).And(assignment)
            .Then<QueryClause>(result =>
            {
                var (identifier, expr) = result;
                return new LetClause(identifier, expr);
            });

        // Ordering: expression [ascending | descending]
        var ordering = assignment.And(DESCENDING.Then(OrderDirection.Descending)
                .Or(ASCENDING.Then(OrderDirection.Ascending))
                .Optional())
            .Then(result =>
            {
                var (expr, direction) = result;
                return new Ordering(expr, direction.HasValue ? direction.Value : OrderDirection.Ascending);
            });

        var orderings = Separated(COMMA, ordering);

        // OrderBy clause: orderby expression [, expression]
        var orderByClause = ORDERBY.SkipAnd(orderings)
            .Then<QueryClause>(orders => new OrderByClause(orders));

        // Join clause: join x in collection on expr1 equals expr2 [into identifier]
        var joinClause = JOIN.SkipAnd(typeReference.Optional())
            .And(anyIdentifier)
            .AndSkip(IN)
            .And(assignment)
            .AndSkip(ON)
            .And(assignment)
            .AndSkip(EQUALS)
            .And(assignment)
            .And(INTO.SkipAnd(anyIdentifier).Optional())
            .Then<QueryClause>(result =>
            {
                var (type, identifier, collection, leftExpr, rightExpr, into) = result;
                return new JoinClause(
                    identifier,
                    collection,
                    leftExpr,
                    rightExpr,
                    type.OrSome(null),
                    into.OrSome(null)
                );
            });

        // Additional from clause in body
        var fromBodyClause = fromClause.Then<QueryClause>(f => f);

        // Query body clauses
        var queryBodyClause = fromBodyClause
            .Or(letClause)
            .Or(whereClause)
            .Or(joinClause)
            .Or(orderByClause);

        var queryBodyClauses = ZeroOrMany(queryBodyClause);

        // Select clause: select expression
        var selectClause = SELECT.SkipAnd(assignment)
            .Then<SelectOrGroupClause>(expr => new SelectClause(expr));

        // Group clause: group expression by expression
        var groupClause = GROUP.SkipAnd(assignment).AndSkip(BY).And(assignment)
            .Then<SelectOrGroupClause>(result =>
            {
                var (groupExpr, byExpr) = result;
                return new GroupClause(groupExpr, byExpr);
            });

        var selectOrGroupClause = selectClause.Or(groupClause);

        // Query continuation: into identifier queryBody
        var queryContinuation = Deferred<QueryContinuation>();
        
        var queryBodyWithContinuation = queryBodyClauses.And(selectOrGroupClause).And(queryContinuation.Optional())
            .Then(result =>
            {
                var (clauses, selectOrGroup, continuation) = result;
                return (clauses, selectOrGroup, continuation.OrSome(null));
            });

        queryContinuation.Parser = INTO.SkipAnd(anyIdentifier).And(queryBodyClauses).And(selectOrGroupClause)
            .Then(result =>
            {
                var (identifier, clauses, selectOrGroup) = result;
                return new QueryContinuation(identifier, clauses, selectOrGroup);
            });

        // Query expression: from ... [where/let/orderby/join] ... select/group [into]
        var queryExpression = fromClause.And(queryBodyWithContinuation)
            .Then<Expression>(result =>
            {
                var (from, body) = result;
                var (bodyClauses, selectOrGroup, continuation) = body;
                
                if (continuation != null)
                {
                    // Add continuation as additional body clauses
                    var allClauses = bodyClauses.ToList();
                    allClauses.AddRange(continuation.BodyClauses);
                    return new QueryExpression(from, allClauses, continuation.SelectOrGroupClause);
                }
                
                return new QueryExpression(from, bodyClauses, selectOrGroup);
            });

        // Expression with lambda and query support
        expression.Parser = queryExpression.Or(lambda).Or(assignment);

        // Set lambda body now that expression is defined
        // Use assignment for lambda body to avoid infinite recursion
        var lambdaExprBody = assignment
            .Then<LambdaBody>(expr => new ExpressionLambdaBody(expr));
        
        var lambdaBlockBody = block
            .Then<LambdaBody>(b => new BlockLambdaBody(b));

        lambdaBody.Parser = lambdaBlockBody.Or(lambdaExprBody);

        // Namespace declaration
        var namespaceBody = Between(LBRACE, ZeroOrMany(typeDeclaration), RBRACE);

        var namespaceDeclaration = NAMESPACE.SkipAnd(qualifiedName)
            .And(namespaceBody)
            .Then<MemberDeclaration>(result =>
            {
                var (name, members) = result;
                return new NamespaceDeclaration(
                    new NameExpression(name),
                    members.Count != 0 ? members : null
                );
            });

        // Using directives
        var usingNamespace = USING.SkipAnd(qualifiedName).AndSkip(SEMICOLON)
            .Then<UsingDirective>(parts => new UsingNamespaceDirective(new NameExpression(parts)));

        var usingAlias = USING.SkipAnd(anyIdentifier).AndSkip(EQ).And(qualifiedName).AndSkip(SEMICOLON)
            .Then<UsingDirective>(result =>
            {
                var (alias, target) = result;
                return new UsingAliasDirective(alias, new NameExpression(target));
            });

        var usingStatic = USING.AndSkip(STATIC).And(qualifiedName).AndSkip(SEMICOLON)
            .Then<UsingDirective>(parts => new UsingStaticDirective(new NameExpression(parts.Item2)));

        var usingDirective = usingAlias.Or(usingStatic).Or(usingNamespace);
        var usingDirectives = ZeroOrMany(usingDirective);

        // Top-level members (namespaces and types)
        var topLevelMember = namespaceDeclaration.Or(typeDeclaration);
        var topLevelMembers = ZeroOrMany(topLevelMember);

        // Compilation unit
        // Global attributes (assembly: or module: target)
        var globalAttributeTarget = ATTR_ASSEMBLY.Or(ATTR_MODULE);
        var globalAttributeSection = Between(
                LBRACKET,
                globalAttributeTarget.AndSkip(COLON).And(attributeList),
                RBRACKET
            )
            .Then(result =>
            {
                var (target, attrs) = result;
                var attrTarget = target == "assembly" ? AttributeTarget.Assembly : AttributeTarget.Module;
                return new AttributeSection(attrs, attrTarget);
            });

        var globalAttributes = ZeroOrMany(globalAttributeSection);

        var compilationUnit = globalAttributes.And(usingDirectives).And(topLevelMembers)
            .Then(result =>
            {
                var (globalAttrs, usings, members) = result;
                return new CompilationUnit(
                    null,
                    usings.Count != 0 ? usings : null,
                    globalAttrs.Count != 0 ? (IReadOnlyList<AttributeSection>)globalAttrs : null,
                    members.Count != 0 ? members : null
                );
            })
            .Eof();

        CompilationUnitParser = compilationUnit.WithComments(comments =>
        {
            comments
                .WithWhiteSpaceOrNewLine()
                .WithSingleLine("//")
                .WithMultiLine("/*", "*/")
                ;
        });
    }

    public static CompilationUnit Parse(string input)
    {
        if (TryParse(input, out var result, out var _))
        {
            return result;
        }

        return null;
    }

    public static bool TryParse(string input, out CompilationUnit result, out ParseError error)
    {
        // Trim leading and trailing whitespace to handle indented test strings
        input = input?.Trim() ?? string.Empty;
        return CompilationUnitParser.TryParse(input, out result, out error);
    }
}

