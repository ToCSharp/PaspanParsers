using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.CSharp;

// Identifiers and names.
public partial class CSharpParser
{
    private static Parser<string> identifier, verbatimIdentifier, anyIdentifier;
    private static Parser<List<string>> qualifiedName;
    private static Parser<Expression> nameExpr;

    private static void InitializeNames()
    {
        identifier = Terms.Identifier()
            .When((ctx, id) => !keywords.Contains(id.ToString()))
            .Then(id => id.ToString());

        verbatimIdentifier = AT.SkipAnd(Terms.Identifier()).Then(id => id.ToString());

        anyIdentifier = verbatimIdentifier.Or(identifier);

        // Qualified name (for namespaces and types)
        qualifiedName = Separated(DOT, anyIdentifier);

        nameExpr = qualifiedName.Then<Expression>(parts => new NameExpression(parts));
    }
}
