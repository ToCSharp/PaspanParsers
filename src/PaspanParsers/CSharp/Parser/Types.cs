using Paspan;
using Paspan.Fluent;

namespace PaspanParsers.CSharp;

// Type parameters and constraints of declarations; types themselves are parsed by SyntaxParser.
public partial class CSharpParser
{
    private static Parser<Option<List<TypeParameter>>> typeParameters;
    private static Parser<List<TypeParameterConstraint>> typeParameterConstraintClauses;

    private static void InitializeTypes()
    {
        typeParameters = new SyntaxRuleParser<List<TypeParameter>>(SyntaxParser.ParseTypeParameterListRule).Optional();
        typeParameterConstraintClauses = new SyntaxRuleParser<List<TypeParameterConstraint>>(SyntaxParser.ParseConstraintClausesRule);
    }
}
