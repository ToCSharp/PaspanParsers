using Paspan;
using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.Python;

/// <summary>
/// Parser result wrapper for compatibility with tests.
/// </summary>
public class ParseResult<T>(T value, bool success, ParseError error)
{
    public T Value { get; } = value;
    public bool Success { get; } = success;
    public ParseError Error { get; } = error;
}

/// <summary>
/// Python parser implementation using Paspan Fluent API.
/// Supports Python 3.6-3.12 syntax (simplified for educational purposes).
/// This is an educational implementation - for production use, consider the official Python parser.
/// </summary>
public class PythonParser
{
    public static readonly Parser<Module> ModuleParser;

    static PythonParser()
    {
        // ========================================
        // Keywords
        // ========================================

        var keywords = new HashSet<string>
        {
            "False", "None", "True", "and", "as", "assert", "async", "await", "break",
            "class", "continue", "def", "del", "elif", "else", "except", "finally",
            "for", "from", "global", "if", "import", "in", "is", "lambda", "nonlocal",
            "not", "or", "pass", "raise", "return", "try", "while", "with", "yield",
            "match", "case", "_"  // Python 3.10+
        };

        // ========================================
        // Helper Functions
        // ========================================

        static CompareOperator ParseCompareOp(string op) => op switch
        {
            "==" => CompareOperator.Eq,
            "!=" => CompareOperator.NotEq,
            "<" => CompareOperator.Lt,
            "<=" => CompareOperator.LtE,
            ">" => CompareOperator.Gt,
            ">=" => CompareOperator.GtE,
            "is" => CompareOperator.Is,
            "in" => CompareOperator.In,
            _ => CompareOperator.Eq
        };

        // ========================================
        // Basic Terminals
        // ========================================

        var NEWLINE = Terms.Char('\n').Or(Terms.Char('\r').AndSkip(Terms.Char('\n').Optional()));
        
        var LPAREN = Terms.Char('(');
        var RPAREN = Terms.Char(')');
        var LBRACKET = Terms.Char('[');
        var RBRACKET = Terms.Char(']');
        var LBRACE = Terms.Char('{');
        var RBRACE = Terms.Char('}');
        var COMMA = Terms.Char(',');
        var COLON = Terms.Char(':');
        var DOT = Terms.Char('.');
        var EQ = Terms.Char('=');

        // ========================================
        // Keywords
        // ========================================

        var TRUE = Terms.Keyword("True", caseInsensitive: false);
        var FALSE = Terms.Keyword("False", caseInsensitive: false);
        var NONE = Terms.Keyword("None", caseInsensitive: false);
        var AND = Terms.Keyword("and", caseInsensitive: false);
        var OR = Terms.Keyword("or", caseInsensitive: false);
        var NOT = Terms.Keyword("not", caseInsensitive: false);
        var IF = Terms.Keyword("if", caseInsensitive: false);
        var ELSE = Terms.Keyword("else", caseInsensitive: false);
        var ELIF = Terms.Keyword("elif", caseInsensitive: false);
        var WHILE = Terms.Keyword("while", caseInsensitive: false);
        var FOR = Terms.Keyword("for", caseInsensitive: false);
        var IN = Terms.Keyword("in", caseInsensitive: false);
        var IS = Terms.Keyword("is", caseInsensitive: false);
        var DEF = Terms.Keyword("def", caseInsensitive: false);
        var CLASS = Terms.Keyword("class", caseInsensitive: false);
        var RETURN = Terms.Keyword("return", caseInsensitive: false);
        var PASS = Terms.Keyword("pass", caseInsensitive: false);
        var BREAK = Terms.Keyword("break", caseInsensitive: false);
        var CONTINUE = Terms.Keyword("continue", caseInsensitive: false);
        var RAISE = Terms.Keyword("raise", caseInsensitive: false);
        var IMPORT = Terms.Keyword("import", caseInsensitive: false);
        var FROM = Terms.Keyword("from", caseInsensitive: false);
        var AS = Terms.Keyword("as", caseInsensitive: false);

        // ========================================
        // Identifiers
        // ========================================

        var identifier = Terms.Identifier()
            .When((ctx, id) => !keywords.Contains(id.ToString()))
            .Then(id => id.ToString());

        // ========================================
        // Literals
        // ========================================

        // Number literal: try decimal first (handles both int and float)
        var numberLiteral = Terms.Decimal()
            .Then<Expression>(d => 
            {
                // If the number is a whole number, treat it as integer
                if (d == Math.Floor(d) && d >= int.MinValue && d <= int.MaxValue)
                {
                    return new LiteralExpression((int)d, LiteralKind.Integer);
                }
                return new LiteralExpression((double)d, LiteralKind.Float);
            });

        var stringLiteral = Terms.String(StringLiteralQuotes.SingleOrDouble)
            .Then<Expression>(s => new LiteralExpression(s.ToString(), LiteralKind.String));

        var boolLiteral = TRUE.Then<Expression>(new LiteralExpression(true, LiteralKind.Boolean))
            .Or(FALSE.Then<Expression>(new LiteralExpression(false, LiteralKind.Boolean)));

        var noneLiteral = NONE.Then<Expression>(new LiteralExpression(null, LiteralKind.None));

        var literal = numberLiteral.Or(stringLiteral).Or(boolLiteral).Or(noneLiteral);

        // ========================================
        // Deferred Parsers
        // ========================================

        var expression = Deferred<Expression>();
        var statement = Deferred<Statement>();

        // ========================================
        // Expressions
        // ========================================

        // Name
        var nameExpr = identifier.Then<Expression>(id => new NameExpression(id));

        // Parenthesized expression
        var parenExpr = Between(LPAREN, expression, RPAREN);

        // List expression: [expr, expr, ...]
        var listExpr = Between(LBRACKET, Separated(COMMA, expression).Else([]), RBRACKET)
            .Then<Expression>(elements => new ListExpression(elements));

        // Dict pair: key: value
        var dictPair = expression.AndSkip(COLON).And(expression)
            .Then(result =>
            {
                var (key, value) = result;
                return (Key: key, Value: value);
            });

        var dictPairList = Separated(COMMA, dictPair);

        // Dict expression: {key: value, ...}
        var dictExpr = Between(LBRACE, dictPairList, RBRACE)
            .Then<Expression>(pairs =>
            {
                var keys = pairs.Select(p => p.Key).ToList();
                var values = pairs.Select(p => p.Value).ToList();
                return new DictExpression(keys, values);
            });

        // Set expression: {expr, expr, ...}
        var setExpr = Between(LBRACE, Separated(COMMA, expression), RBRACE)
            .When((ctx, elements) => elements.Count != 0)
            .Then<Expression>(elements => new SetExpression(elements));

        // Primary expression
        var primary = literal
            .Or(parenExpr)
            .Or(listExpr)
            .Or(dictExpr.Or(setExpr))
            .Or(nameExpr);

        // Call expression: func(arg, arg, ...)
        var callSuffix = Between(LPAREN, Separated(COMMA, expression).Else([]), RPAREN);
        
        // Subscript: [index]
        var subscriptSuffix = Between(LBRACKET, expression, RBRACKET);
        
        // Attribute: .attr
        var attributeSuffix = DOT.SkipAnd(identifier);

        // Postfix expression (call, subscript, attribute)
        var postfix = primary.And(
            callSuffix.Then<object>(args => (object)args)
            .Or(subscriptSuffix.Then<object>(index => (object)index))
            .Or(attributeSuffix.Then<object>(attr => (object)attr))
            .ZeroOrMany()
        ).Then<Expression>(result =>
        {
            var (prim, suffixes) = result;
            var current = prim;
            foreach (var suffix in suffixes)
            {
                if (suffix is List<Expression> args)
                {
                    current = new CallExpression(current, args);
                }
                else if (suffix is Expression index)
                {
                    current = new SubscriptExpression(current, index);
                }
                else if (suffix is string attr)
                {
                    current = new AttributeExpression(current, attr);
                }
            }
            return current;
        });

        // Unary expression
        var unaryOp = NOT.Then("not")
            .Or(Terms.Char('-').Then("-"))
            .Or(Terms.Char('+').Then("+"))
            .Or(Terms.Char('~').Then("~"));

        var unary = unaryOp.And(postfix).Then<Expression>(result =>
        {
            var (op, operand) = result;
            var unaryOperator = op switch
            {
                "not" => UnaryOperator.Not,
                "-" => UnaryOperator.USub,
                "+" => UnaryOperator.UAdd,
                "~" => UnaryOperator.Invert,
                _ => UnaryOperator.Not
            };
            return new UnaryOperation(unaryOperator, operand);
        }).Or(postfix);

        // Power: **
        var powerOp = Terms.Text("**");
        var power = unary.And(powerOp.SkipAnd(unary).ZeroOrMany())
            .Then<Expression>(result =>
            {
                var (left, rights) = result;
                // Right-associative for power
                if (rights.Count == 0) return left;
                
                var current = rights.Last();
                for (int i = rights.Count - 2; i >= 0; i--)
                {
                    current = new BinaryOperation(rights.ElementAt(i), BinaryOperator.Pow, current);
                }
                return new BinaryOperation(left, BinaryOperator.Pow, current);
            });

        // Multiplicative: *, /, //, %
        // FloorDiv first (longer operator)
        var floorDiv = power.LeftAssociative(
            (Terms.Text("//"), (a, b) => new BinaryOperation(a, BinaryOperator.FloorDiv, b))
        );
        
        var multiplicative = floorDiv.LeftAssociative(
            (Terms.Char('*'), (a, b) => new BinaryOperation(a, BinaryOperator.Mult, b)),
            (Terms.Char('/'), (a, b) => new BinaryOperation(a, BinaryOperator.Div, b)),
            (Terms.Char('%'), (a, b) => new BinaryOperation(a, BinaryOperator.Mod, b))
        );

        // Additive: +, -
        var additive = multiplicative.LeftAssociative(
            (Terms.Char('+'), (a, b) => new BinaryOperation(a, BinaryOperator.Add, b)),
            (Terms.Char('-'), (a, b) => new BinaryOperation(a, BinaryOperator.Sub, b))
        );

        // Comparison: ==, !=, <, <=, >, >=, is, in
        var compOp = Terms.Text("==").Then("==")
            .Or(Terms.Text("!=").Then("!="))
            .Or(Terms.Text("<=").Then("<="))
            .Or(Terms.Text(">=").Then(">="))
            .Or(Terms.Char('<').Then("<"))
            .Or(Terms.Char('>').Then(">"))
            .Or(IS.Then("is"))
            .Or(IN.Then("in"));

        var comparison = additive.And(compOp.And(additive).Optional())
            .Then<Expression>(result =>
            {
                var (left, comp) = result;
                if (comp.HasValue)
                {
                    var (op, right) = comp.Value;
                    return new CompareOperation(
                        left,
                        [ParseCompareOp(op)],
                        [right]
                    );
                }
                return left;
            });

        // Logical AND
        var andExpr = comparison.And(AND.SkipAnd(comparison).ZeroOrMany())
            .Then<Expression>(result =>
            {
                var (left, rights) = result;
                if (rights.Count != 0)
                {
                    var values = new[] { left }.Concat(rights).ToList();
                    return new BooleanOperation(BoolOperator.And, values);
                }
                return left;
            });

        // Logical OR
        var orExpr = andExpr.And(OR.SkipAnd(andExpr).ZeroOrMany())
            .Then<Expression>(result =>
            {
                var (left, rights) = result;
                if (rights.Count != 0)
                {
                    var values = new[] { left }.Concat(rights).ToList();
                    return new BooleanOperation(BoolOperator.Or, values);
                }
                return left;
            });

        // Conditional expression: body if test else orelse
        var conditional = orExpr.And(IF.SkipAnd(orExpr).AndSkip(ELSE).And(orExpr).Optional())
            .Then<Expression>(result =>
            {
                var (body, cond) = result;
                if (cond.HasValue)
                {
                    var (test, orElse) = cond.Value;
                    return new ConditionalExpression(test, body, orElse);
                }
                return body;
            });

        expression.Parser = conditional;

        // ========================================
        // Statements
        // ========================================

        // Pass statement
        var passStmt = PASS.Then<Statement>(new PassStatement());

        // Break statement
        var breakStmt = BREAK.Then<Statement>(new BreakStatement());

        // Continue statement
        var continueStmt = CONTINUE.Then<Statement>(new ContinueStatement());

        // Return statement
        var returnStmt = RETURN.SkipAnd(expression.Optional())
            .Then<Statement>(expr => new ReturnStatement(expr.OrSome(null)));

        // Raise statement
        var raiseStmt = RAISE.SkipAnd(expression.Optional())
            .Then<Statement>(expr => new RaiseStatement(expr.OrSome(null)));

        // Assignment: name = value
        var assignmentStmt = nameExpr.AndSkip(EQ).And(expression)
            .Then<Statement>(result =>
            {
                var (target, value) = result;
                return new AssignmentStatement([target], value);
            });

        // Annotated assignment: name: type = value
        var annotatedAssignment = nameExpr.AndSkip(COLON).And(expression)
            .And(EQ.SkipAnd(expression).Optional())
            .Then<Statement>(result =>
            {
                var (target, annotation, value) = result;
                return new AnnotatedAssignment(target, annotation, value.OrSome(null));
            });

        // Expression statement
        var exprStmt = expression.Then<Statement>(expr => new ExpressionStatement(expr));

        // Simple statement
        var simpleStmt = passStmt
            .Or(breakStmt)
            .Or(continueStmt)
            .Or(returnStmt)
            .Or(raiseStmt)
            .Or(annotatedAssignment)
            .Or(assignmentStmt)
            .Or(exprStmt);

        // ========================================
        // Import Statements
        // ========================================

        var importAlias = identifier.And(AS.SkipAnd(identifier).Optional())
            .Then(result =>
            {
                var (name, asName) = result;
                return new Alias(name, asName.OrSome(null));
            });

        var importStmt = IMPORT.SkipAnd(Separated(COMMA, importAlias))
            .Then<Statement>(names => new ImportStatement(names));

        var importFromStmt = FROM.SkipAnd(identifier).AndSkip(IMPORT).And(Separated(COMMA, importAlias))
            .Then<Statement>(result =>
            {
                var (module, names) = result;
                return new ImportFromStatement(names, module);
            });

        // ========================================
        // Compound Statements
        // ========================================

        // Suite: : statement (simplified version without strict indentation)
        // Accepts: ":\n stmt" or ": stmt" 
        var suite = COLON.SkipAnd(NEWLINE.Optional()).SkipAnd(statement)
            .Then(stmt => (IReadOnlyList<Statement>)[stmt]);

        // If statement
        var ifStmt = IF.SkipAnd(expression).And(suite)
            .And(ELSE.SkipAnd(suite).Optional())
            .Then<Statement>(result =>
            {
                var (test, body, orElse) = result;
                return new IfStatement(test, body, orElse.OrSome(null));
            });

        // While statement
        var whileStmt = WHILE.SkipAnd(expression).And(suite)
            .Then<Statement>(result =>
            {
                var (test, body) = result;
                return new WhileStatement(test, body);
            });

        // For statement
        var forStmt = FOR.SkipAnd(nameExpr).AndSkip(IN).And(expression).And(suite)
            .Then<Statement>(result =>
            {
                var (target, iter, body) = result;
                return new ForStatement(target, iter, body);
            });

        // Function parameters
        var parameter = identifier.And(COLON.SkipAnd(expression).Optional())
            .Then(result =>
            {
                var (name, annotation) = result;
                return new Arg(name, annotation.OrSome(null));
            });

        var parameters = Between(LPAREN, Separated(COMMA, parameter).Else([]), RPAREN)
            .Then(args => new Arguments(args: args));

        // Function definition
        var functionDef = DEF.SkipAnd(identifier).And(parameters)
            .And(Terms.Text("->").SkipAnd(expression).Optional())
            .And(suite)
            .Then<Statement>(result =>
            {
                var (name, args, returns, body) = result;
                return new FunctionDef(name, args, body, returns: returns.OrSome(null));
            });

        // Class definition
        var classDef = CLASS.SkipAnd(identifier)
            .And(Between(LPAREN, Separated(COMMA, expression).Else([]), RPAREN).Optional())
            .And(suite)
            .Then<Statement>(result =>
            {
                var (name, bases, body) = result;
                return new ClassDef(name, body, bases: bases.OrSome(null));
            });

        // Compound statement
        var compoundStmt = ifStmt
            .Or(whileStmt)
            .Or(forStmt)
            .Or(functionDef)
            .Or(classDef);

        statement.Parser = compoundStmt
            .Or(importStmt)
            .Or(importFromStmt)
            .Or(simpleStmt);

        // ========================================
        // Module
        // ========================================

        var module = ZeroOrMany(statement.AndSkip(NEWLINE.ZeroOrMany()))
            .Then(stmts => new Module(stmts));

        ModuleParser = module.WithComments(comments =>
        {
            comments
                .WithWhiteSpaceOrNewLine()
                .WithSingleLine("#");
        });
    }

    public static Module Parse(string input)
    {
        if (TryParse(input, out var result, out var _))
        {
            return result;
        }
        return null;
    }

    public static bool TryParse(string input, out Module result, out ParseError error)
    {
        input = input?.Trim() ?? string.Empty;
        return ModuleParser.TryParse(input, out result, out error);
    }

    // ========================================
    // Instance Methods (for backward compatibility with tests)
    // ========================================

    public static ParseResult<Module> ParseModule(string input)
    {
        if (ModuleParser.TryParse(input, out var result, out var error))
        {
            return new ParseResult<Module>(result, true, error);
        }
        return new ParseResult<Module>(default, false, error);
    }

    public static ParseResult<Expression> ParseExpression(string input)
    {
        // For expression parsing, we need to extract the expression parser from the static constructor
        // Since it's not exposed, we'll parse as a module with single expression statement
        var moduleResult = ParseModule(input);
        if (moduleResult.Success && moduleResult.Value.Body.Count > 0)
        {
            if (moduleResult.Value.Body[0] is ExpressionStatement exprStmt)
            {
                return new ParseResult<Expression>(exprStmt.Value, true, null);
            }
        }
        return new ParseResult<Expression>(default, false, null);
    }

    public static ParseResult<Statement> ParseStatement(string input)
    {
        var moduleResult = ParseModule(input + "\n");
        if (moduleResult.Success && moduleResult.Value.Body.Count > 0)
        {
            return new ParseResult<Statement>(moduleResult.Value.Body[0], true, null);
        }
        return new ParseResult<Statement>(default, false, null);
    }
}

