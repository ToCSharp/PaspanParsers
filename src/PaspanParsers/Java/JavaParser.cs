using Paspan;
using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.Java;

/// <summary>
/// A Java parser demonstrating parsing of Java language constructs.
/// This is an educational implementation - for production use, consider Eclipse JDT or JavaParser library.
/// </summary>
public class JavaParser
{
    public static readonly Parser<CompilationUnit> CompilationUnitParser;

    static JavaParser()
    {
        // ========================================
        // Basic terminals
        // ========================================
        
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
        var ELLIPSIS = Terms.Text("...");

        // ========================================
        // Keywords
        // ========================================
        
        // Modifiers
        var PUBLIC = Terms.Keyword("public", caseInsensitive: false);
        var PRIVATE = Terms.Keyword("private", caseInsensitive: false);
        var PROTECTED = Terms.Keyword("protected", caseInsensitive: false);
        var STATIC = Terms.Keyword("static", caseInsensitive: false);
        var FINAL = Terms.Keyword("final", caseInsensitive: false);
        var ABSTRACT = Terms.Keyword("abstract", caseInsensitive: false);
        var SYNCHRONIZED = Terms.Keyword("synchronized", caseInsensitive: false);
        var VOLATILE = Terms.Keyword("volatile", caseInsensitive: false);
        var TRANSIENT = Terms.Keyword("transient", caseInsensitive: false);
        var NATIVE = Terms.Keyword("native", caseInsensitive: false);
        var STRICTFP = Terms.Keyword("strictfp", caseInsensitive: false);
        var DEFAULT = Terms.Keyword("default", caseInsensitive: false);
        var SEALED = Terms.Keyword("sealed", caseInsensitive: false);
        var NON_SEALED = Terms.Keyword("non-sealed", caseInsensitive: false);

        // Type declarations
        var PACKAGE = Terms.Keyword("package", caseInsensitive: false);
        var IMPORT = Terms.Keyword("import", caseInsensitive: false);
        var CLASS = Terms.Keyword("class", caseInsensitive: false);
        var INTERFACE = Terms.Keyword("interface", caseInsensitive: false);
        var ENUM = Terms.Keyword("enum", caseInsensitive: false);
        var RECORD = Terms.Keyword("record", caseInsensitive: false);
        var EXTENDS = Terms.Keyword("extends", caseInsensitive: false);
        var IMPLEMENTS = Terms.Keyword("implements", caseInsensitive: false);
        var PERMITS = Terms.Keyword("permits", caseInsensitive: false);

        // Control flow
        var IF = Terms.Keyword("if", caseInsensitive: false);
        var ELSE = Terms.Keyword("else", caseInsensitive: false);
        var WHILE = Terms.Keyword("while", caseInsensitive: false);
        var DO = Terms.Keyword("do", caseInsensitive: false);
        var FOR = Terms.Keyword("for", caseInsensitive: false);
        var SWITCH = Terms.Keyword("switch", caseInsensitive: false);
        var CASE = Terms.Keyword("case", caseInsensitive: false);
        var BREAK = Terms.Keyword("break", caseInsensitive: false);
        var CONTINUE = Terms.Keyword("continue", caseInsensitive: false);
        var RETURN = Terms.Keyword("return", caseInsensitive: false);
        var THROW = Terms.Keyword("throw", caseInsensitive: false);
        var THROWS = Terms.Keyword("throws", caseInsensitive: false);
        var TRY = Terms.Keyword("try", caseInsensitive: false);
        var CATCH = Terms.Keyword("catch", caseInsensitive: false);
        var FINALLY = Terms.Keyword("finally", caseInsensitive: false);
        var ASSERT = Terms.Keyword("assert", caseInsensitive: false);
        var YIELD = Terms.Keyword("yield", caseInsensitive: false);

        // Other keywords
        var NEW = Terms.Keyword("new", caseInsensitive: false);
        var THIS = Terms.Keyword("this", caseInsensitive: false);
        var SUPER = Terms.Keyword("super", caseInsensitive: false);
        var VOID = Terms.Keyword("void", caseInsensitive: false);
        var VAR = Terms.Keyword("var", caseInsensitive: false);
        var INSTANCEOF = Terms.Keyword("instanceof", caseInsensitive: false);
        var WHEN = Terms.Keyword("when", caseInsensitive: false);

        // Literals
        var TRUE = Terms.Keyword("true", caseInsensitive: false);
        var FALSE = Terms.Keyword("false", caseInsensitive: false);
        var NULL = Terms.Keyword("null", caseInsensitive: false);

        // Primitive types
        var BOOLEAN = Terms.Keyword("boolean", caseInsensitive: false);
        var BYTE = Terms.Keyword("byte", caseInsensitive: false);
        var SHORT = Terms.Keyword("short", caseInsensitive: false);
        var INT = Terms.Keyword("int", caseInsensitive: false);
        var LONG = Terms.Keyword("long", caseInsensitive: false);
        var CHAR = Terms.Keyword("char", caseInsensitive: false);
        var FLOAT = Terms.Keyword("float", caseInsensitive: false);
        var DOUBLE = Terms.Keyword("double", caseInsensitive: false);

        // Keywords set
        var keywords = new HashSet<string>
        {
            "package", "import", "class", "interface", "enum", "record",
            "public", "private", "protected", "static", "final", "abstract",
            "synchronized", "volatile", "transient", "native", "strictfp", "default",
            "sealed", "non-sealed", "permits",
            "if", "else", "while", "do", "for", "switch", "case", "break", "continue",
            "return", "throw", "throws", "try", "catch", "finally", "assert", "yield",
            "new", "this", "super", "void", "var", "instanceof", "extends", "implements", "when",
            "true", "false", "null",
            "boolean", "byte", "short", "int", "long", "char", "float", "double"
        };

        // ========================================
        // Identifiers
        // ========================================
        
        var identifier = Terms.Identifier()
            .When((ctx, id) => !keywords.Contains(id.ToString()))
            .Then(id => id.ToString());

        var qualifiedNameParts = Separated(DOT, identifier);
        var qualifiedName = qualifiedNameParts.Then(parts => new QualifiedName(parts));

        // ========================================
        // Deferred parsers
        // ========================================
        
        var expression = Deferred<Expression>();
        var statement = Deferred<Statement>();
        var typeReference = Deferred<TypeReference>();
        var memberDeclaration = Deferred<MemberDeclaration>();
        var typeDeclaration = Deferred<TypeDeclaration>();
        var annotation = Deferred<Annotation>();

        // ========================================
        // Literals
        // ========================================
        
        var integerLiteral = Terms.Integer()
            .AndSkip(Not(Literals.Char('.')))
            .Then<Expression>(i => new LiteralExpression((int)i, LiteralKind.Integer));

        var floatingPointLiteral = Terms.Decimal()
            .Then<Expression>(d => new LiteralExpression((double)d, LiteralKind.FloatingPoint));

        var stringLiteral = Terms.String(StringLiteralQuotes.Double)
            .Then<Expression>(s => new LiteralExpression(s.ToString(), LiteralKind.String));

        var charLiteral = Terms.String(StringLiteralQuotes.Single)
            .Then<Expression>(s => {
                var str = s.ToString();
                if (str.Length != 1)
                    throw new InvalidOperationException($"Character literal must be exactly one character, but was: {str}");
                return new LiteralExpression(str[0], LiteralKind.Character);
            });

        var boolLiteral = TRUE.Then<Expression>(new LiteralExpression(true, LiteralKind.Boolean))
            .Or(FALSE.Then<Expression>(new LiteralExpression(false, LiteralKind.Boolean)));

        var nullLiteral = NULL.Then<Expression>(new LiteralExpression(null, LiteralKind.Null));

        var literal = integerLiteral.Or(floatingPointLiteral).Or(stringLiteral)
            .Or(charLiteral).Or(boolLiteral).Or(nullLiteral);

        // ========================================
        // Annotations
        // ========================================
        
        var annotationElement = Deferred<AnnotationElement>();
        
        var namedAnnotationElement = identifier.AndSkip(EQ).And(expression)
            .Then(result =>
            {
                var (name, value) = result;
                return new AnnotationElement(value, name);
            });

        var positionalAnnotationElement = expression
            .Then(value => new AnnotationElement(value));

        annotationElement.Parser = namedAnnotationElement.Or(positionalAnnotationElement);

        var annotationElements = Between(
            LPAREN,
            Separated(COMMA, annotationElement),
            RPAREN
        );

        annotation.Parser = AT.SkipAnd(qualifiedName).And(annotationElements.Optional())
            .Then(result =>
            {
                var (name, elements) = result;
                return new Annotation(name, elements.OrSome(null));
            });

        var annotations = ZeroOrMany(annotation);

        // ========================================
        // Type References
        // ========================================
        
        var primitiveType = 
            BOOLEAN.Then(PrimitiveType.Boolean)
            .Or(BYTE.Then(PrimitiveType.Byte))
            .Or(SHORT.Then(PrimitiveType.Short))
            .Or(INT.Then(PrimitiveType.Int))
            .Or(LONG.Then(PrimitiveType.Long))
            .Or(CHAR.Then(PrimitiveType.Char))
            .Or(FLOAT.Then(PrimitiveType.Float))
            .Or(DOUBLE.Then(PrimitiveType.Double));

        var primitiveTypeRef = annotations.And(primitiveType)
            .Then<TypeReference>(result =>
            {
                var (annots, type) = result;
                return new PrimitiveTypeReference(type, annots.Count != 0 ? annots : null);
            });

        // Type arguments (generics)
        var typeArgument = Deferred<TypeArgument>();
        
        var wildcardBounds = Deferred<WildcardBounds>();
        
        var extendsWildcard = EXTENDS.SkipAnd(typeReference)
            .Then<WildcardBounds>(type => new ExtendsWildcardBounds(type));

        var superWildcard = SUPER.SkipAnd(typeReference)
            .Then<WildcardBounds>(type => new SuperWildcardBounds(type));

        wildcardBounds.Parser = extendsWildcard.Or(superWildcard);

        var wildcardTypeArg = annotations.And(QUESTION).And(wildcardBounds.Optional())
            .Then<TypeArgument>(result =>
            {
                var (annots, _, bounds) = result;
                return new WildcardTypeArgument(bounds.OrSome(null), annots.Count != 0 ? annots : null);
            });

        var referenceTypeArg = typeReference.Then<TypeArgument>(type => new ReferenceTypeArgument(type));

        typeArgument.Parser = wildcardTypeArg.Or(referenceTypeArg);

        var typeArguments = Between(LT, Separated(COMMA, typeArgument), GT);

        // Reference type
        var referenceTypeRef = annotations.And(qualifiedName).And(typeArguments.Optional())
            .Then<TypeReference>(result =>
            {
                var (annots, name, typeArgs) = result;
                return new ReferenceTypeReference(
                    name,
                    typeArgs.HasValue ? typeArgs.Value : null,
                    annots.Count != 0 ? annots : null
                );
            });

        // Var type (Java 10+)
        var varTypeRef = VAR.Then<TypeReference>(new VarTypeReference());

        // Void type (for method return types)
        var voidTypeRef = VOID.Then<TypeReference>(new VoidTypeReference());

        // Base type reference (before array dimensions)
        var baseTypeRef = primitiveTypeRef.Or(voidTypeRef).Or(referenceTypeRef).Or(varTypeRef);

        // Array dimensions
        var arrayDimension = annotations.And(Between(LBRACKET, ZeroOrMany(Terms.Char(' ')), RBRACKET))
            .Then(result => result.Item1);

        // Complete type reference with array dimensions
        typeReference.Parser = baseTypeRef.And(ZeroOrMany(arrayDimension))
            .Then<TypeReference>(result =>
            {
                var (baseType, dimensions) = result;
                var current = baseType;
                foreach (var annots in dimensions)
                {
                    current = new ArrayTypeReference(current, annots.Count != 0 ? annots : null);
                }
                return current;
            });

        // ========================================
        // Modifiers
        // ========================================
        
        var modifierKeyword = 
            PUBLIC.Then(Modifiers.Public)
            .Or(PRIVATE.Then(Modifiers.Private))
            .Or(PROTECTED.Then(Modifiers.Protected))
            .Or(STATIC.Then(Modifiers.Static))
            .Or(FINAL.Then(Modifiers.Final))
            .Or(ABSTRACT.Then(Modifiers.Abstract))
            .Or(SYNCHRONIZED.Then(Modifiers.Synchronized))
            .Or(VOLATILE.Then(Modifiers.Volatile))
            .Or(TRANSIENT.Then(Modifiers.Transient))
            .Or(NATIVE.Then(Modifiers.Native))
            .Or(STRICTFP.Then(Modifiers.Strictfp))
            .Or(DEFAULT.Then(Modifiers.Default))
            .Or(SEALED.Then(Modifiers.Sealed))
            .Or(NON_SEALED.Then(Modifiers.NonSealed));

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

        // ========================================
        // Expressions
        // ========================================
        
        var nameExpr = qualifiedName.Then<Expression>(name => new NameExpression(name));

        var thisExpr = THIS.Then<Expression>(new ThisExpression());

        var parenExpr = Between(LPAREN, expression, RPAREN)
            .Then<Expression>(expr => new ParenthesizedExpression(expr));

        // Arguments
        var argumentList = Separated(COMMA, expression);
        var arguments = Between(LPAREN, argumentList.Else([]), RPAREN);

        // Object creation (new)
        var newObjectExpr = NEW.SkipAnd(typeReference).And(arguments)
            .Then<Expression>(result =>
            {
                var (type, args) = result;
                return new NewObjectExpression(type, args.Count != 0 ? args : null);
            });

        // Method invocation
        var methodInvocation = nameExpr.And(arguments)
            .Then<Expression>(result =>
            {
                var (nameExp, args) = result;
                var name = ((NameExpression)nameExp).Name;
                return new MethodInvocationExpression(
                    name.Parts[name.Parts.Count - 1],
                    args,
                    name.Parts.Count > 1 
                        ? new NameExpression(new QualifiedName([.. name.Parts.Take(name.Parts.Count - 1)]))
                        : null
                );
            });

        // Primary expressions
        var primary = methodInvocation
            .Or(newObjectExpr)
            .Or(parenExpr)
            .Or(literal)
            .Or(thisExpr)
            .Or(nameExpr);

        // Field access
        var fieldAccess = primary.And(OneOrMany(DOT.SkipAnd(identifier)))
            .Then<Expression>(result =>
            {
                var (target, fields) = result;
                var current = target;
                foreach (var field in fields)
                {
                    current = new FieldAccessExpression(current, field);
                }
                return current;
            });

        // Array access
        var arrayAccess = primary.And(OneOrMany(Between(LBRACKET, expression, RBRACKET)))
            .Then<Expression>(result =>
            {
                var (target, indices) = result;
                var current = target;
                foreach (var index in indices)
                {
                    current = new ArrayAccessExpression(current, index);
                }
                return current;
            });

        var postfix = arrayAccess.Or(fieldAccess).Or(primary);

        // Unary expressions
        var unaryPlus = Terms.Char('+').SkipAnd(postfix)
            .Then<Expression>(expr => new UnaryExpression(UnaryOperator.Plus, expr));
        var unaryMinus = Terms.Char('-').SkipAnd(postfix)
            .Then<Expression>(expr => new UnaryExpression(UnaryOperator.Minus, expr));
        var logicalComplement = Terms.Char('!').SkipAnd(postfix)
            .Then<Expression>(expr => new UnaryExpression(UnaryOperator.LogicalComplement, expr));
        var bitwiseComplement = Terms.Char('~').SkipAnd(postfix)
            .Then<Expression>(expr => new UnaryExpression(UnaryOperator.BitwiseComplement, expr));

        var unary = unaryPlus.Or(unaryMinus).Or(logicalComplement).Or(bitwiseComplement).Or(postfix);

        // Binary expressions with precedence
        var multiplicative = unary.LeftAssociative(
            (Terms.Char('*'), (a, b) => new BinaryExpression(a, BinaryOperator.Multiply, b)),
            (Terms.Char('/'), (a, b) => new BinaryExpression(a, BinaryOperator.Divide, b)),
            (Terms.Char('%'), (a, b) => new BinaryExpression(a, BinaryOperator.Modulo, b))
        );

        var additive = multiplicative.LeftAssociative(
            (Terms.Char('+'), (a, b) => new BinaryExpression(a, BinaryOperator.Add, b)),
            (Terms.Char('-'), (a, b) => new BinaryExpression(a, BinaryOperator.Subtract, b))
        );

        var shift = additive.LeftAssociative(
            (Terms.Text(">>>"), (a, b) => new BinaryExpression(a, BinaryOperator.UnsignedRightShift, b)),
            (Terms.Text("<<"), (a, b) => new BinaryExpression(a, BinaryOperator.LeftShift, b)),
            (Terms.Text(">>"), (a, b) => new BinaryExpression(a, BinaryOperator.RightShift, b))
        );

        var relational = shift.LeftAssociative(
            (Terms.Text("<="), (a, b) => new BinaryExpression(a, BinaryOperator.LessThanOrEqual, b)),
            (Terms.Text(">="), (a, b) => new BinaryExpression(a, BinaryOperator.GreaterThanOrEqual, b)),
            (Terms.Text("<"), (a, b) => new BinaryExpression(a, BinaryOperator.LessThan, b)),
            (Terms.Text(">"), (a, b) => new BinaryExpression(a, BinaryOperator.GreaterThan, b))
        );

        // instanceof expression
        var instanceofExpr = relational.And(INSTANCEOF.SkipAnd(typeReference).Optional())
            .Then<Expression>(result =>
            {
                var (expr, typeOpt) = result;
                if (typeOpt.HasValue)
                {
                    return new InstanceOfExpression(expr, typeOpt.Value);
                }
                return expr;
            });

        var equality = instanceofExpr.LeftAssociative(
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

        var conditionalAnd = bitwiseOr.LeftAssociative(
            (Terms.Text("&&"), (a, b) => new BinaryExpression(a, BinaryOperator.ConditionalAnd, b))
        );

        var conditionalOr = conditionalAnd.LeftAssociative(
            (Terms.Text("||"), (a, b) => new BinaryExpression(a, BinaryOperator.ConditionalOr, b))
        );

        // Ternary expression
        var ternary = conditionalOr.And(QUESTION.SkipAnd(expression).AndSkip(COLON).And(expression).Optional())
            .Then<Expression>(result =>
            {
                var (condition, rest) = result;
                if (rest.HasValue)
                {
                    var (trueExpr, falseExpr) = rest.Value;
                    return new TernaryExpression(condition, trueExpr, falseExpr);
                }
                return condition;
            });

        // Assignment
        var assignment = ternary.And(EQ.SkipAnd(expression).Optional())
            .Then<Expression>(result =>
            {
                var (left, right) = result;
                if (right.HasValue)
                {
                    return new BinaryExpression(left, BinaryOperator.Assign, right.Value);
                }
                return left;
            });

        expression.Parser = assignment;

        // ========================================
        // Statements
        // ========================================
        
        var block = Deferred<BlockStatement>();

        // Expression statement
        var expressionStatement = expression.AndSkip(SEMICOLON)
            .Then<Statement>(expr => new ExpressionStatement(expr));

        // Variable declarator
        var variableDeclarator = identifier.And(ZeroOrMany(Between(LBRACKET, ZeroOrMany(Terms.Char(' ')), RBRACKET)))
            .And(EQ.SkipAnd(expression).Optional())
            .Then(result =>
            {
                var (name, dims, init) = result;
                return new VariableDeclarator(name, dims.Count, init.OrSome(null));
            });

        var variableDeclarators = Separated(COMMA, variableDeclarator);

        // Local variable declaration
        var localVarDecl = annotations.And(FINAL.Optional()).And(typeReference).And(variableDeclarators).AndSkip(SEMICOLON)
            .Then<Statement>(result =>
            {
                var (annots, finalOpt, type, vars) = result;
                return new LocalVariableDeclarationStatement(
                    type,
                    vars,
                    annots.Count != 0 ? annots : null,
                    finalOpt.HasValue
                );
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
        var forUpdate = Separated(COMMA, expression);

        var forStatement = FOR.SkipAnd(LPAREN)
            .SkipAnd(forInit.Else(new List<Statement>().Cast<Statement>().FirstOrDefault()))
            .And(forCondition)
            .And(forUpdate.Else([]))
            .AndSkip(RPAREN)
            .And(statement)
            .Then<Statement>(result =>
            {
                var (init, condition, updates, body) = result;
                return new ForStatement(
                    body,
                    init != null ? new[] { init } : null,
                    condition.OrSome(null),
                    updates.Count != 0 ? updates : null
                );
            });

        // Return statement
        var returnStatement = RETURN.SkipAnd(expression.Optional()).AndSkip(SEMICOLON)
            .Then<Statement>(expr => new ReturnStatement(expr.OrSome(null)));

        // Break/Continue
        var breakStatement = BREAK.SkipAnd(identifier.Optional()).AndSkip(SEMICOLON)
            .Then<Statement>(label => new BreakStatement(label.OrSome(null)));
        
        var continueStatement = CONTINUE.SkipAnd(identifier.Optional()).AndSkip(SEMICOLON)
            .Then<Statement>(label => new ContinueStatement(label.OrSome(null)));

        // Throw statement
        var throwStatement = THROW.SkipAnd(expression).AndSkip(SEMICOLON)
            .Then<Statement>(expr => new ThrowStatement(expr));

        // Empty statement
        var emptyStatement = SEMICOLON.Then<Statement>(new EmptyStatement());

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
            .Or(expressionStatement)
            .Or(emptyStatement);

        // ========================================
        // Type Parameters
        // ========================================
        
        var typeParameter = annotations.And(identifier).And(EXTENDS.SkipAnd(Separated(Terms.Char('&'), typeReference)).Optional())
            .Then(result =>
            {
                var (annots, name, boundsOpt) = result;
                return new TypeParameter(name, boundsOpt.OrSome(null), annots.Count != 0 ? annots : null);
            });

        var typeParameters = Between(LT, Separated(COMMA, typeParameter), GT);

        // ========================================
        // Parameters
        // ========================================
        
        var parameter = annotations.And(FINAL.Optional()).And(typeReference).And(ELLIPSIS.Optional())
            .And(identifier).And(ZeroOrMany(Between(LBRACKET, ZeroOrMany(Terms.Char(' ')), RBRACKET)))
            .Then(result =>
            {
                var (annots, finalOpt, type, varArgsOpt, name, dims) = result;
                return new Parameter(
                    type,
                    name,
                    annots.Count != 0 ? annots : null,
                    finalOpt.HasValue,
                    varArgsOpt.HasValue,
                    dims.Count
                );
            });

        var parameters = Separated(COMMA, parameter);
        var parameterList = Between(LPAREN, parameters.Else([]), RPAREN);

        // ========================================
        // Members
        // ========================================
        
        // Field declaration
        var fieldDeclaration = annotations.And(modifiers).And(typeReference).And(variableDeclarators).AndSkip(SEMICOLON)
            .Then<MemberDeclaration>(result =>
            {
                var (annots, mods, type, vars) = result;
                return new FieldDeclaration(type, vars, annots.Count != 0 ? annots : null, mods);
            });

        // Method declaration
        var throwsClause = THROWS.SkipAnd(Separated(COMMA, typeReference));
        
        var methodDeclaration = annotations.And(modifiers).And(typeParameters.Optional())
            .And(typeReference).And(identifier).And(parameterList)
            .And(ZeroOrMany(Between(LBRACKET, ZeroOrMany(Terms.Char(' ')), RBRACKET)))
            .And(throwsClause.Optional())
            .And(block.Or(SEMICOLON.Then<BlockStatement>(_ => null)))
            .Then<MemberDeclaration>(result =>
            {
                var (annots, mods, typeParams, returnType, name, params_, dims, throws, body) = result;
                return new MethodDeclaration(
                    returnType,
                    name,
                    annots.Count != 0 ? annots : null,
                    mods,
                    typeParams.OrSome(null),
                    params_,
                    dims.Count,
                    throws.OrSome(null),
                    body
                );
            });

        // Constructor declaration
        var constructorDeclaration = annotations.And(modifiers).And(typeParameters.Optional())
            .And(identifier).And(parameterList).And(throwsClause.Optional()).And(block)
            .Then<MemberDeclaration>(result =>
            {
                var (annots, mods, typeParams, name, params_, throws, body) = result;
                return new ConstructorDeclaration(
                    name,
                    annots.Count != 0 ? annots : null,
                    mods,
                    typeParams.OrSome(null),
                    params_,
                    throws.OrSome(null),
                    body
                );
            });

        memberDeclaration.Parser = fieldDeclaration
            .Or(methodDeclaration)
            .Or(constructorDeclaration);

        var memberList = ZeroOrMany(memberDeclaration);

        // ========================================
        // Type Declarations
        // ========================================
        
        // Class declaration
        var extendsClause = EXTENDS.SkipAnd(typeReference);
        var implementsClause = IMPLEMENTS.SkipAnd(Separated(COMMA, typeReference));
        var permitsClause = PERMITS.SkipAnd(Separated(COMMA, typeReference));

        var classDeclaration = annotations.And(modifiers).AndSkip(CLASS).And(identifier)
            .And(typeParameters.Optional())
            .And(extendsClause.Optional())
            .And(implementsClause.Optional())
            .And(permitsClause.Optional())
            .And(Between(LBRACE, memberList, RBRACE))
            .Then<TypeDeclaration>(result =>
            {
                var (annots, mods, name, typeParams, superClass, interfaces, permits, members) = result;
                return new ClassDeclaration(
                    name,
                    annots.Count != 0 ? annots : null,
                    mods,
                    typeParams.OrSome(null),
                    superClass.OrSome(null),
                    interfaces.OrSome(null),
                    permits.OrSome(null),
                    members.Count != 0 ? members : null
                );
            });

        // Interface declaration
        var extendsInterfaces = EXTENDS.SkipAnd(Separated(COMMA, typeReference));

        var interfaceDeclaration = annotations.And(modifiers).AndSkip(INTERFACE).And(identifier)
            .And(typeParameters.Optional())
            .And(extendsInterfaces.Optional())
            .And(permitsClause.Optional())
            .And(Between(LBRACE, memberList, RBRACE))
            .Then<TypeDeclaration>(result =>
            {
                var (annots, mods, name, typeParams, extendsIfaces, permits, members) = result;
                return new InterfaceDeclaration(
                    name,
                    annots.Count != 0 ? annots : null,
                    mods,
                    typeParams.OrSome(null),
                    extendsIfaces.OrSome(null),
                    permits.OrSome(null),
                    members.Count != 0 ? members : null
                );
            });

        // Enum declaration
        var enumConstant = annotations.And(identifier).And(arguments.Optional())
            .Then(result =>
            {
                var (annots, name, args) = result;
                return new EnumConstant(name, annots.Count != 0 ? annots : null, args.OrSome(null));
            });

        var enumConstants = Separated(COMMA, enumConstant);

        var enumDeclaration = annotations.And(modifiers).AndSkip(ENUM).And(identifier)
            .And(implementsClause.Optional())
            .And(Between(
                LBRACE,
                enumConstants.Else([]).AndSkip(COMMA.Optional()).AndSkip(SEMICOLON.Optional())
                    .And(memberList),
                RBRACE
            ))
            .Then<TypeDeclaration>(result =>
            {
                var (annots, mods, name, interfaces, body) = result;
                var (constants, members) = body;
                return new EnumDeclaration(
                    name,
                    annots.Count != 0 ? annots : null,
                    mods,
                    interfaces.OrSome(null),
                    constants.Count != 0 ? constants : null,
                    members.Count != 0 ? members : null
                );
            });

        // Record declaration (Java 16+)
        var recordComponent = annotations.And(typeReference).And(identifier)
            .Then(result =>
            {
                var (annots, type, name) = result;
                return new RecordComponent(type, name, annots.Count != 0 ? annots : null);
            });

        var recordComponents = Between(LPAREN, Separated(COMMA, recordComponent).Else([]), RPAREN);

        var recordDeclaration = annotations.And(modifiers).AndSkip(RECORD).And(identifier)
            .And(typeParameters.Optional())
            .And(recordComponents)
            .And(implementsClause.Optional())
            .And(Between(LBRACE, memberList, RBRACE))
            .Then<TypeDeclaration>(result =>
            {
                var (annots, mods, name, typeParams, components, interfaces, members) = result;
                return new RecordDeclaration(
                    name,
                    components,
                    annots.Count != 0 ? annots : null,
                    mods,
                    typeParams.OrSome(null),
                    interfaces.OrSome(null),
                    members.Count != 0 ? members : null
                );
            });

        typeDeclaration.Parser = classDeclaration
            .Or(interfaceDeclaration)
            .Or(enumDeclaration)
            .Or(recordDeclaration);

        var typeDeclarations = ZeroOrMany(typeDeclaration);

        // ========================================
        // Package and Imports
        // ========================================
        
        var packageDeclaration = annotations.AndSkip(PACKAGE).And(qualifiedName).AndSkip(SEMICOLON)
            .Then(result =>
            {
                var (annots, name) = result;
                return new PackageDeclaration(name, annots.Count != 0 ? annots : null);
            });

        var importDeclaration = IMPORT.SkipAnd(STATIC.Optional()).And(qualifiedName)
            .And(DOT.SkipAnd(Terms.Char('*')).Optional()).AndSkip(SEMICOLON)
            .Then<ImportDeclaration>(result =>
            {
                var (isStatic, name, wildcard) = result;
                if (wildcard.HasValue)
                {
                    return new TypeImportOnDemandDeclaration(name, isStatic.HasValue);
                }
                return new SingleTypeImportDeclaration(name, isStatic.HasValue);
            });

        var importDeclarations = ZeroOrMany(importDeclaration);

        // ========================================
        // Compilation Unit
        // ========================================
        
        var compilationUnit = packageDeclaration.Optional()
            .And(importDeclarations)
            .And(typeDeclarations)
            .Then(result =>
            {
                var (pkg, imports, types) = result;
                return new CompilationUnit(
                    pkg.OrSome(null),
                    imports.Count != 0 ? imports : null,
                    types.Count != 0 ? types : null
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

