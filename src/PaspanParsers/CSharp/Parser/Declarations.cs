using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.CSharp;

// Attributes, modifiers, parameters, members and type declarations.
public partial class CSharpParser
{
    private static Parser<List<AttributeNode>> attributeList;
    private static Parser<List<AttributeSection>> attributes;
    private static Parser<Modifiers> modifiers;
    private static Parser<ParameterModifier> parameterModifier;
    private static Parser<List<Parameter>> parameterList;
    private static Parser<MemberDeclaration> typeDeclaration;

    private static void InitializeAttributesAndModifiers()
    {
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

        attributeList = Separated(COMMA, attribute);

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

        attributes = ZeroOrMany(attributeSection);

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
        parameterModifier =
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

        parameterList = Separated(COMMA, parameter);
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
