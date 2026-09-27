using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.CSharp;

// Patterns, switch expression arms and the 'is' expression.
public partial class CSharpParser
{
    private static void InitializePatterns()
    {
        switchExpressionArm.Parser = pattern.And(WHEN.SkipAnd(assignment).Optional()).AndSkip(ARROW).And(assignment)
            .Then(result =>
            {
                var (pat, guard, expr) = result;
                return new SwitchExpressionArm(pat, expr, guard.OrSome(null));
            });

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

        // Basic pattern (without logical operators)
        // Order matters: try more specific patterns first
        var basicPattern = discardPattern
            .Or(varPattern)
            .Or(relationalPattern)
            .Or(recursivePattern)
            .Or(declarationPattern)  // Try declaration pattern first (Type identifier)
            .Or(typePattern)          // Then type pattern (just Type)
            .Or(constantPattern);

        // Logical patterns: not pattern, pattern and pattern, pattern or pattern
        var notPattern = Deferred<Pattern>();
        var andPattern = Deferred<Pattern>();
        var orPattern = Deferred<Pattern>();

        notPattern.Parser = NOT.SkipAnd(pattern)
            .Then<Pattern>(p => new LogicalPattern(LogicalPatternKind.Not, p));

        andPattern.Parser = basicPattern.And(AND.SkipAnd(pattern))
            .Then<Pattern>(result =>
            {
                var (left, right) = result;
                return new LogicalPattern(LogicalPatternKind.And, left, right);
            });

        orPattern.Parser = basicPattern.And(OR.SkipAnd(pattern))
            .Then<Pattern>(result =>
            {
                var (left, right) = result;
                return new LogicalPattern(LogicalPatternKind.Or, left, right);
            });

        pattern.Parser = notPattern.Or(andPattern).Or(orPattern).Or(basicPattern);

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
    }
}
