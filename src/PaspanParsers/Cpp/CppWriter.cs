using System.Runtime.CompilerServices;
using System.Text;

namespace PaspanParsers.Cpp;

/// <summary>
/// Writes a C++ AST back as source code. The output is literal: parentheses, the forms of initializers and
/// the order of specifiers are those of the tree, so parsing the output again builds an equivalent tree.
/// Comments and formatting are not kept. Directives are written as they are in the source, on their own lines,
/// before the nodes they lead (<see cref="CppNode.LeadingDirectives"/>), before closing braces and at the end;
/// conditional directives and inactive branches are not in the tree.
/// </summary>
public sealed class CppWriter
{
    private readonly StringBuilder _builder = new();
    private readonly string _indentString;
    private int _indentLevel;
    private bool _needsIndent = true;
    private readonly HashSet<PreprocessorDirective> _writtenDirectives = new(ReferenceEqualityComparer.Instance);

    public CppWriter(string indentString = "    ")
    {
        _indentString = indentString;
    }

    public string GetResult() => _builder.ToString();

    // ========================================
    // Output
    // ========================================

    /// <summary>
    /// Writes a token, separated from the previous one by a space when they would otherwise read as one
    /// token (<c>a b</c>, <c>- -a</c>, <c>L "x"</c>).
    /// </summary>
    private void Token(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (_needsIndent)
        {
            for (var i = 0; i < _indentLevel; i++)
            {
                _builder.Append(_indentString);
            }

            _needsIndent = false;
        }
        else if (_builder.Length > 0 && NeedsSpace(_builder[^1], text[0]))
        {
            _builder.Append(' ');
        }

        _builder.Append(text);
    }

    private void Space()
    {
        if (!_needsIndent && _builder.Length > 0 && _builder[^1] != ' ')
        {
            _builder.Append(' ');
        }
    }

    private void NewLine()
    {
        _builder.AppendLine();
        _needsIndent = true;
    }

    private static bool IsWordCharacter(char c) => char.IsLetterOrDigit(c) || c is '_' or '$' || c >= 0x80;

    private static bool IsOperatorCharacter(char c) => c is '+' or '-' or '*' or '/' or '%' or '^' or '&' or '|' or '~' or '!' or '=' or '<' or '>' or '?' or ':' or '.' or '#';

    private static bool NeedsSpace(char previous, char next)
    {
        if (IsWordCharacter(previous))
        {
            // Words run together; a prefix like L or u8 would join a literal after it
            return IsWordCharacter(next) || next is '"' or '\'' || (next == '.' && char.IsDigit(previous));
        }

        if (previous is '"' or '\'')
        {
            // A word after a literal would become its user-defined suffix
            return IsWordCharacter(next);
        }

        return IsOperatorCharacter(previous) && IsOperatorCharacter(next);
    }

    private static void EnsureSufficientStack() => RuntimeHelpers.EnsureSufficientExecutionStack();

    /// <summary>
    /// Writes the leading directives of <paramref name="node"/> that are not written yet: nodes that start at
    /// the same token share them.
    /// </summary>
    private void Directives(CppNode node)
    {
        Directives(node?.LeadingDirectives);
    }

    private void Directives(IReadOnlyList<PreprocessorDirective> directives)
    {
        if (directives == null)
        {
            return;
        }

        foreach (var directive in directives)
        {
            if (!_writtenDirectives.Add(directive))
            {
                continue;
            }

            if (!_needsIndent)
            {
                while (_builder.Length > 0 && _builder[^1] == ' ')
                {
                    _builder.Length--;
                }

                NewLine();
            }

            _builder.Append(directive.Text ?? $"#{directive.Name} {directive.Arguments}");
            NewLine();
        }
    }

    // ========================================
    // Translation Unit
    // ========================================

    public void WriteTranslationUnit(TranslationUnit unit)
    {
        var length = _builder.Length;
        var indentLevel = _indentLevel;
        var needsIndent = _needsIndent;
        try
        {
            WriteTranslationUnitCore(unit);
            return;
        }
        catch (InsufficientExecutionStackException)
        {
            // A deeply nested tree: write it again on a thread with a large stack, like CppParser parses it
            _builder.Length = length;
            _indentLevel = indentLevel;
            _needsIndent = needsIndent;
        }

        try
        {
            LargeStack.Run(() => WriteTranslationUnitCore(unit));
        }
        catch (InsufficientExecutionStackException e)
        {
            throw new InsufficientExecutionStackException("The syntax tree is nested too deeply.", e);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("Writing the syntax tree failed.", e);
        }
    }

    private void WriteTranslationUnitCore(TranslationUnit unit)
    {
        foreach (var declaration in unit.Declarations)
        {
            WriteDeclaration(declaration);
        }

        Directives(unit.EndDirectives);
    }

    // ========================================
    // Declarations
    // ========================================

    public void WriteDeclaration(Declaration declaration)
    {
        EnsureSufficientStack();
        Directives(declaration);
        switch (declaration)
        {
            case SimpleDeclaration simple:
                WriteSimpleDeclaration(simple);
                NewLine();
                break;
            case FunctionDefinition function:
                WriteSpecifiersAndDeclarator(function.Specifiers, function.Declarator);
                WriteRequiresClause(function.RequiresClause);
                NewLine();
                WriteCompoundStatement(function.Body);
                NewLine();
                break;
            default:
                throw new NotSupportedException($"Unknown declaration {declaration.GetType().Name}");
        }
    }

    private void WriteSimpleDeclaration(SimpleDeclaration declaration)
    {
        Directives(declaration);
        if (declaration.Declarators.Count == 0)
        {
            WriteDeclSpecifiers(declaration.Specifiers);
        }

        for (var i = 0; i < declaration.Declarators.Count; i++)
        {
            if (i > 0)
            {
                Token(",");
                Space();
            }

            WriteInitDeclarator(i == 0 ? declaration.Specifiers : null, declaration.Declarators[i]);
        }

        Token(";");
    }

    /// <summary>
    /// The specifiers, if any, and the declarator after them.
    /// </summary>
    private void WriteSpecifiersAndDeclarator(DeclSpecifierSequence specifiers, Declarator declarator)
    {
        if (specifiers != null)
        {
            WriteDeclSpecifiers(specifiers);
            if (declarator != null)
            {
                Space();
            }
        }

        WriteDeclarator(declarator);
    }

    private void WriteDeclSpecifiers(DeclSpecifierSequence specifiers)
    {
        if (specifiers == null)
        {
            return;
        }

        Directives(specifiers);
        for (var i = 0; i < specifiers.Specifiers.Count; i++)
        {
            var specifier = specifiers.Specifiers[i];
            if (i > 0)
            {
                Space();
            }

            Directives(specifier);
            switch (specifier)
            {
                case KeywordSpecifier keyword:
                    Token(keyword.Keyword);
                    break;
                case NamedTypeSpecifier named:
                    if (named.IsTypename)
                    {
                        Token("typename");
                        Space();
                    }

                    WriteName(named.Name);
                    break;
                case ElaboratedTypeSpecifier elaborated:
                    Token(elaborated.Key);
                    Space();
                    WriteName(elaborated.Name);
                    break;
                case DecltypeSpecifier decltype:
                    Token("decltype");
                    Token("(");
                    if (decltype.Expression == null)
                    {
                        Token("auto");
                    }
                    else
                    {
                        WriteExpression(decltype.Expression);
                    }

                    Token(")");
                    break;
                case PlaceholderTypeSpecifier placeholder:
                    WriteName(placeholder.Concept);
                    Space();
                    if (placeholder.IsDecltypeAuto)
                    {
                        Token("decltype");
                        Token("(");
                        Token("auto");
                        Token(")");
                    }
                    else
                    {
                        Token("auto");
                    }

                    break;
                default:
                    throw new NotSupportedException($"Unknown declaration specifier {specifier.GetType().Name}");
            }
        }
    }

    private void WriteInitDeclarator(DeclSpecifierSequence specifiers, InitDeclarator declarator)
    {
        Directives(declarator);
        WriteSpecifiersAndDeclarator(specifiers, declarator.Declarator);
        WriteRequiresClause(declarator.RequiresClause);
        WriteInitializer(declarator.Initializer);
    }

    private void WriteRequiresClause(Expression constraint)
    {
        if (constraint != null)
        {
            Space();
            Token("requires");
            Space();
            WriteExpression(constraint);
        }
    }

    private void WriteInitializer(Initializer initializer)
    {
        switch (initializer)
        {
            case null:
                break;
            case EqualsInitializer equals:
                Directives(equals);
                Space();
                Token("=");
                Space();
                WriteExpression(equals.Value);
                break;
            default:
                throw new NotSupportedException($"Unknown initializer {initializer.GetType().Name}");
        }
    }

    // ========================================
    // Declarators
    // ========================================

    private void WriteDeclarator(Declarator declarator)
    {
        EnsureSufficientStack();
        if (declarator == null)
        {
            return;
        }

        Directives(declarator);
        switch (declarator)
        {
            case NameDeclarator name:
                WriteName(name.Name);
                break;
            case PackDeclarator pack:
                Token("...");
                WriteDeclarator(pack.Inner);
                break;
            case PointerDeclarator pointer:
                Token("*");
                WriteQualifiers(pointer.Qualifiers);
                WriteDeclarator(pointer.Inner);
                break;
            case ReferenceDeclarator reference:
                Token(reference.IsRvalue ? "&&" : "&");
                WriteDeclarator(reference.Inner);
                break;
            case MemberPointerDeclarator member:
                WriteName(member.Class);
                Token("::");
                Token("*");
                WriteQualifiers(member.Qualifiers);
                WriteDeclarator(member.Inner);
                break;
            case ArrayDeclarator array:
                WriteDeclarator(array.Inner);
                Token("[");
                if (array.Size != null)
                {
                    WriteExpression(array.Size);
                }

                Token("]");
                break;
            case FunctionDeclarator function:
                WriteFunctionDeclarator(function);
                break;
            case ParenthesizedDeclarator parenthesized:
                Token("(");
                WriteDeclarator(parenthesized.Inner);
                Token(")");
                break;
            default:
                throw new NotSupportedException($"Unknown declarator {declarator.GetType().Name}");
        }
    }

    private void WriteQualifiers(IReadOnlyList<string> qualifiers)
    {
        foreach (var qualifier in qualifiers)
        {
            Token(qualifier);
            Space();
        }
    }

    private void WriteFunctionDeclarator(FunctionDeclarator function)
    {
        WriteDeclarator(function.Inner);
        Token("(");
        for (var i = 0; i < function.Parameters.Count; i++)
        {
            if (i > 0)
            {
                Token(",");
                Space();
            }

            WriteParameter(function.Parameters[i]);
        }

        if (function.IsVariadic)
        {
            if (function.Parameters.Count > 0)
            {
                Token(",");
                Space();
            }

            Token("...");
        }

        Token(")");
        foreach (var qualifier in function.Qualifiers)
        {
            Space();
            Token(qualifier);
        }

        if (function.RefQualifier != null)
        {
            Space();
            Token(function.RefQualifier);
        }

        if (function.Noexcept != null)
        {
            Directives(function.Noexcept);
            Space();
            Token("noexcept");
            if (function.Noexcept.Condition != null)
            {
                Token("(");
                WriteExpression(function.Noexcept.Condition);
                Token(")");
            }
        }

        if (function.TrailingReturnType != null)
        {
            Space();
            Token("->");
            Space();
            WriteTypeId(function.TrailingReturnType);
        }
    }

    private void WriteParameter(ParameterDeclaration parameter)
    {
        Directives(parameter);
        WriteSpecifiersAndDeclarator(parameter.Specifiers, parameter.Declarator);
        if (parameter.DefaultValue != null)
        {
            Space();
            Token("=");
            Space();
            WriteExpression(parameter.DefaultValue);
        }
    }

    public void WriteTypeId(TypeId type)
    {
        Directives(type);
        WriteSpecifiersAndDeclarator(type.Specifiers, type.Declarator);
        if (type.IsPackExpansion)
        {
            Token("...");
        }
    }

    // ========================================
    // Names
    // ========================================

    public void WriteName(Name name)
    {
        EnsureSufficientStack();
        Directives(name);
        switch (name)
        {
            case IdentifierName identifier:
                Token(identifier.Identifier);
                break;
            case TemplateIdName templateId:
                WriteName(templateId.Template);
                Token("<");
                for (var i = 0; i < templateId.Arguments.Count; i++)
                {
                    if (i > 0)
                    {
                        Token(",");
                        Space();
                    }

                    switch (templateId.Arguments[i])
                    {
                        case TypeId type:
                            WriteTypeId(type);
                            break;
                        case Expression expression:
                            WriteExpression(expression);
                            break;
                        default:
                            throw new NotSupportedException($"Unknown template argument {templateId.Arguments[i].GetType().Name}");
                    }
                }

                Token(">");
                break;
            case OperatorFunctionName @operator:
                Token("operator");
                Token(@operator.Operator);
                break;
            case ConversionFunctionName conversion:
                Token("operator");
                Space();
                WriteTypeId(conversion.Type);
                break;
            case LiteralOperatorName literal:
                Token("operator");
                Token("\"\"" + literal.Suffix);
                break;
            case DestructorName destructor:
                Token("~");
                WriteName(destructor.Type);
                break;
            case DecltypeName decltype:
                Token("decltype");
                Token("(");
                WriteExpression(decltype.Expression);
                Token(")");
                break;
            case QualifiedName qualified:
                if (qualified.Qualifier != null)
                {
                    WriteName(qualified.Qualifier);
                }

                Token("::");
                if (qualified.IsTemplate)
                {
                    Token("template");
                    Space();
                }

                WriteName(qualified.Name);
                break;
            default:
                throw new NotSupportedException($"Unknown name {name.GetType().Name}");
        }
    }

    // ========================================
    // Statements
    // ========================================

    public void WriteStatement(Statement statement)
    {
        EnsureSufficientStack();
        Directives(statement);
        switch (statement)
        {
            case CompoundStatement compound:
                WriteCompoundStatement(compound);
                break;
            case DeclarationStatement declaration:
                WriteSimpleDeclaration(declaration.Declaration);
                break;
            case ExpressionStatement expression:
                if (expression.Expression != null)
                {
                    WriteExpression(expression.Expression);
                }

                Token(";");
                break;
            case IfStatement @if:
                Token("if");
                Space();
                Token("(");
                WriteExpression(@if.Condition);
                Token(")");
                WriteEmbeddedStatement(@if.Then);
                if (@if.Else != null)
                {
                    NewLine();
                    Token("else");
                    WriteEmbeddedStatement(@if.Else);
                }

                break;
            case WhileStatement @while:
                Token("while");
                Space();
                Token("(");
                WriteExpression(@while.Condition);
                Token(")");
                WriteEmbeddedStatement(@while.Body);
                break;
            case ReturnStatement @return:
                Token("return");
                if (@return.Expression != null)
                {
                    Space();
                    WriteExpression(@return.Expression);
                }

                Token(";");
                break;
            default:
                throw new NotSupportedException($"Unknown statement {statement.GetType().Name}");
        }
    }

    /// <summary>
    /// The body of an if or while statement: a block on its own lines, other statements indented.
    /// </summary>
    private void WriteEmbeddedStatement(Statement statement)
    {
        NewLine();
        if (statement is CompoundStatement)
        {
            WriteStatement(statement);
            return;
        }

        _indentLevel++;
        WriteStatement(statement);
        _indentLevel--;
    }

    private void WriteCompoundStatement(CompoundStatement compound)
    {
        Directives(compound);
        Token("{");
        NewLine();
        _indentLevel++;
        foreach (var statement in compound.Statements)
        {
            WriteStatement(statement);
            NewLine();
        }

        Directives(compound.CloseBraceDirectives);
        _indentLevel--;
        Token("}");
    }

    // ========================================
    // Expressions
    // ========================================

    public void WriteExpression(Expression expression)
    {
        EnsureSufficientStack();
        Directives(expression);
        switch (expression)
        {
            case LiteralExpression literal:
                Token(literal.Text);
                break;
            case ConcatenatedStringExpression concatenation:
                for (var i = 0; i < concatenation.Parts.Count; i++)
                {
                    if (i > 0)
                    {
                        Space();
                    }

                    Directives(concatenation.Parts[i]);
                    Token(concatenation.Parts[i].Text);
                }

                break;
            case NameExpression name:
                WriteName(name.Name);
                break;
            case ParenthesizedExpression parenthesized:
                Token("(");
                WriteExpression(parenthesized.Expression);
                Token(")");
                break;
            case UnaryExpression { IsPostfix: true } postfix:
                WriteExpression(postfix.Operand);
                Token(postfix.Operator);
                break;
            case UnaryExpression prefix:
                Token(prefix.Operator);
                WriteExpression(prefix.Operand);
                break;
            case BinaryExpression binary:
                WriteBinaryExpression(binary);
                break;
            case ConditionalExpression conditional:
                WriteExpression(conditional.Condition);
                Space();
                Token("?");
                Space();
                WriteExpression(conditional.WhenTrue);
                Space();
                Token(":");
                Space();
                WriteExpression(conditional.WhenFalse);
                break;
            case CallExpression call:
                WriteExpression(call.Callee);
                Token("(");
                for (var i = 0; i < call.Arguments.Count; i++)
                {
                    if (i > 0)
                    {
                        Token(",");
                        Space();
                    }

                    WriteExpression(call.Arguments[i]);
                }

                Token(")");
                break;
            default:
                throw new NotSupportedException($"Unknown expression {expression.GetType().Name}");
        }
    }

    private void WriteBinaryExpression(BinaryExpression binary)
    {
        // Chains of left-associative operators are nested on the left: write them without deep recursion
        var chain = new Stack<BinaryExpression>();
        Expression left = binary;
        while (left is BinaryExpression nested)
        {
            chain.Push(nested);
            left = nested.Left;
        }

        WriteExpression(left);
        while (chain.Count != 0)
        {
            var current = chain.Pop();
            if (current.Operator != ",")
            {
                Space();
            }

            Token(current.Operator);
            Space();
            WriteExpression(current.Right);
        }
    }
}
