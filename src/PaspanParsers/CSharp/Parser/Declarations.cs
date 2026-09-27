using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.CSharp;

// Attributes, modifiers, parameters, members and type declarations.
public partial class CSharpParser
{
    private static Parser<List<AttributeNode>> attributeList;
    private static Parser<List<AttributeSection>> attributes;
    private static Parser<Modifiers> modifiers;
    private static Parser<List<Parameter>> parameterList;
    private static Parser<List<VariableDeclarator>> variableDeclarators;
    private static Parser<MemberDeclaration> typeDeclaration;

    private static void InitializeAttributesAndModifiers()
    {
        attributes = new SyntaxRuleParser<List<AttributeSection>>(SyntaxParser.ParseAttributesRule);
        attributeList = new SyntaxRuleParser<List<AttributeNode>>(SyntaxParser.ParseAttributeListRule);

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
            // 'ref' is a modifier of ref structs; before a type it starts a by-reference return type
            .Or(REF.WhenFollowedBy(STRUCT.Or(PARTIAL)).Then(Modifiers.Ref));

        modifiers = ZeroOrMany(modifierKeyword)
            .Then(mods =>
            {
                var result = Modifiers.None;
                foreach (var mod in mods)
                {
                    result |= mod;
                }
                return result;
            });
    }

    private static void InitializeParameters()
    {
        parameterList = new SyntaxRuleParser<List<Parameter>>(SyntaxParser.ParseParametersRule);
        variableDeclarators = new SyntaxRuleParser<List<VariableDeclarator>>(SyntaxParser.ParseVariableDeclaratorsRule);
    }

    private static void InitializeDeclarations()
    {
        // Method body
        var blockMethodBody = block.Then<MethodBody>(b => new BlockMethodBody(b));
        var expressionMethodBody = ARROW.SkipAnd(expression).AndSkip(SEMICOLON)
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
        Parser<Accessor> AccessorOf(Parser<string> keyword, AccessorKind kind) =>
            keyword.And(block.Optional().AndSkip(SEMICOLON.Optional()))
                .Then(result =>
                {
                    var (_, body) = result;
                    return new Accessor(
                        kind,
                        null,
                        Modifiers.None,
                        body.HasValue ? new BlockMethodBody(body.Value) : null
                    );
                });

        var accessor = AccessorOf(GET, AccessorKind.Get)
            .Or(AccessorOf(SET, AccessorKind.Set))
            .Or(AccessorOf(INIT, AccessorKind.Init));
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
        var methodDeclaration = attributes.And(modifiers).And(returnType).And(anyIdentifier)
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

        // Class, struct and interface declarations share the same shape
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

        typeDeclaration = classDeclaration
            .Or(structDeclaration)
            .Or(interfaceDeclaration)
            .Or(enumDeclaration);
    }
}
