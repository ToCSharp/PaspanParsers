using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.CSharp;

// Identifiers and names.
public partial class CSharpParser
{
    private static Parser<string> identifier, anyIdentifier;
    private static Parser<List<string>> qualifiedName;
    private static Parser<Expression> nameExpr;

    private static void InitializeNames()
    {
        // Any identifier except reserved keywords; contextual keywords are identifiers here
        // and are recognized by position where the grammar needs them.
        identifier = SkipWhiteSpace(new IdentifierToken());

        anyIdentifier = identifier;

        // Qualified name (for namespaces and types)
        qualifiedName = Separated(DOT, anyIdentifier);

        nameExpr = qualifiedName.Then<Expression>(parts => new NameExpression(parts));
    }
}
