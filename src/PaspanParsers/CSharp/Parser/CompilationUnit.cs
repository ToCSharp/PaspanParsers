using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.CSharp;

// Namespaces, using directives and the compilation unit.
public partial class CSharpParser
{
    private static Parser<CompilationUnit> InitializeCompilationUnit()
    {
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

        var usingStatic = USING.SkipAnd(STATIC).SkipAnd(qualifiedName).AndSkip(SEMICOLON)
            .Then<UsingDirective>(parts => new UsingStaticDirective(new NameExpression(parts)));

        var usingDirective = usingAlias.Or(usingStatic).Or(usingNamespace);
        var usingDirectives = ZeroOrMany(usingDirective);

        // Top-level members (namespaces and types)
        var topLevelMember = namespaceDeclaration.Or(typeDeclaration);
        var topLevelMembers = ZeroOrMany(topLevelMember);

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

        return globalAttributes.And(usingDirectives).And(topLevelMembers)
            .Then(result =>
            {
                var (globalAttrs, usings, members) = result;
                return new CompilationUnit(
                    null,
                    usings.Count != 0 ? usings : null,
                    globalAttrs.Count != 0 ? (IReadOnlyList<AttributeSection>)globalAttrs : null,
                    members.Count != 0 ? members : null
                );
            });
    }
}
