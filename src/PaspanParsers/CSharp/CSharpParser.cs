using Paspan;
using Paspan.Fluent;
using static Paspan.Fluent.Parsers;

namespace PaspanParsers.CSharp;

/// <summary>
/// C# parser built from Paspan combinators. The grammar is split into partial files
/// under <c>CSharp/Parser/</c>; this file wires them together and exposes the entry points.
/// </summary>
public partial class CSharpParser
{
    public static readonly Parser<CompilationUnit> CompilationUnitParser;

    // Forward references shared between the grammar parts
    private static readonly Deferred<Expression> expression = Deferred<Expression>();
    private static readonly Deferred<Statement> statement = Deferred<Statement>();
    private static readonly Deferred<BlockStatement> block = Deferred<BlockStatement>();
    private static readonly Deferred<TypeReference> typeReference = Deferred<TypeReference>();
    private static readonly Deferred<TypeConstraint> typeConstraint = Deferred<TypeConstraint>();
    private static readonly Deferred<MemberDeclaration> memberDeclaration = Deferred<MemberDeclaration>();
    private static readonly Deferred<Pattern> pattern = Deferred<Pattern>();
    private static readonly Deferred<SwitchExpressionArm> switchExpressionArm = Deferred<SwitchExpressionArm>();
    private static readonly Deferred<Expression> isExpression = Deferred<Expression>();
    private static readonly Deferred<LambdaBody> lambdaBody = Deferred<LambdaBody>();

    static CSharpParser()
    {
        InitializeLexical();
        InitializeNames();
        InitializeTypes();
        InitializeAttributesAndModifiers();
        InitializeExpressions();
        InitializePatterns();
        InitializeStatements();
        InitializeParameters();
        InitializeLambdasAndQueries();
        InitializeDeclarations();

        CompilationUnitParser = WithTrivia(InitializeCompilationUnit());
    }

    public static CompilationUnit Parse(string input, CSharpParseOptions options = null)
    {
        return TryParse(input, options, out var result, out _) ? result : null;
    }

    public static bool TryParse(string input, out CompilationUnit result, out ParseError error)
    {
        return TryParse(input, null, out result, out error);
    }

    public static bool TryParse(string input, CSharpParseOptions options, out CompilationUnit result, out ParseError error)
    {
        input ??= string.Empty;

        // A byte order mark is not part of the source text
        if (input.Length > 0 && input[0] == '﻿')
        {
            input = input[1..];
        }

        var reader = new SpanReader(input);
        return CompilationUnitParser.TryParse(ref reader, new CSharpParseContext(options), out result, out error);
    }
}
