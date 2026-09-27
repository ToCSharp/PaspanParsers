using Paspan;
using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.CSharp;

// Type parameters, constraints and type references.
public partial class CSharpParser
{
    private static Parser<Option<List<TypeParameter>>> typeParameters;
    private static Parser<List<TypeParameterConstraint>> typeParameterConstraintClauses;

    private static void InitializeTypes()
    {
        // Type parameter (for declarations: <T, U>)
        var typeParameter = anyIdentifier.Then(name => new TypeParameter(name));
        var typeParameterList = Separated(COMMA, typeParameter);
        typeParameters = Between(LT, typeParameterList, GT).Optional();

        // Type constraints
        var classConstraint = CLASS.And(QUESTION.Optional())
            .Then<TypeConstraint>(result =>
            {
                var (_, nullable) = result;
                return new ClassConstraint(nullable.HasValue);
            });

        var structConstraint = STRUCT.Then<TypeConstraint>(new StructConstraint());

        var unmanagedConstraint = Keyword("unmanaged")
            .Then<TypeConstraint>(new UnmanagedConstraint());

        var notnullConstraint = Keyword("notnull")
            .Then<TypeConstraint>(new NotNullConstraint());

        var constructorConstraint = NEW.SkipAnd(LPAREN).AndSkip(RPAREN)
            .Then<TypeConstraint>(new ConstructorConstraint());

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

        typeParameterConstraintClauses = ZeroOrMany(typeParameterConstraintClause);

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
    }
}
