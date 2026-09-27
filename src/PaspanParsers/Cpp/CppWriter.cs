using System.Runtime.CompilerServices;
using System.Text;

namespace PaspanParsers.Cpp;

/// <summary>
/// Writes a C++ AST back as source code. The output is literal: parentheses, the forms of initializers and
/// the order of specifiers are those of the tree, so parsing the output again builds an equivalent tree.
/// Comments and formatting are not kept.
/// </summary>
public sealed class CppWriter
{
    private readonly StringBuilder _builder = new();
    private readonly string _indentString;
    private int _indentLevel;
    private bool _needsIndent = true;

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
    }

    // ========================================
    // Declarations
    // ========================================

    public void WriteDeclaration(Declaration declaration)
    {
        EnsureSufficientStack();
        switch (declaration)
        {
            case SimpleDeclaration simple:
                WriteSimpleDeclaration(simple);
                NewLine();
                break;
            case FunctionDefinition function:
                WriteDeclSpecifiers(function.Specifiers);
                WriteDeclarator(function.Declarator);
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
        WriteDeclSpecifiers(declaration.Specifiers);
        for (var i = 0; i < declaration.Declarators.Count; i++)
        {
            if (i > 0)
            {
                Token(",");
                Space();
            }

            WriteInitDeclarator(declaration.Declarators[i]);
        }

        Token(";");
    }

    private void WriteDeclSpecifiers(DeclSpecifierSequence specifiers)
    {
        foreach (var specifier in specifiers.Specifiers)
        {
            switch (specifier)
            {
                case KeywordSpecifier keyword:
                    Token(keyword.Keyword);
                    break;
                default:
                    throw new NotSupportedException($"Unknown declaration specifier {specifier.GetType().Name}");
            }

            Space();
        }
    }

    private void WriteInitDeclarator(InitDeclarator declarator)
    {
        WriteDeclarator(declarator.Declarator);
        WriteInitializer(declarator.Initializer);
    }

    private void WriteInitializer(Initializer initializer)
    {
        switch (initializer)
        {
            case null:
                break;
            case EqualsInitializer equals:
                Space();
                Token("=");
                Space();
                WriteExpression(equals.Value);
                break;
            default:
                throw new NotSupportedException($"Unknown initializer {initializer.GetType().Name}");
        }
    }

    private void WriteDeclarator(Declarator declarator)
    {
        EnsureSufficientStack();
        switch (declarator)
        {
            case NameDeclarator name:
                Token(name.Name);
                break;
            case FunctionDeclarator function:
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

                Token(")");
                break;
            default:
                throw new NotSupportedException($"Unknown declarator {declarator.GetType().Name}");
        }
    }

    private void WriteParameter(ParameterDeclaration parameter)
    {
        WriteDeclSpecifiers(parameter.Specifiers);
        if (parameter.Declarator != null)
        {
            WriteDeclarator(parameter.Declarator);
        }

        if (parameter.DefaultValue != null)
        {
            Space();
            Token("=");
            Space();
            WriteExpression(parameter.DefaultValue);
        }
    }

    // ========================================
    // Statements
    // ========================================

    public void WriteStatement(Statement statement)
    {
        EnsureSufficientStack();
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
        Token("{");
        NewLine();
        _indentLevel++;
        foreach (var statement in compound.Statements)
        {
            WriteStatement(statement);
            NewLine();
        }

        _indentLevel--;
        Token("}");
    }

    // ========================================
    // Expressions
    // ========================================

    public void WriteExpression(Expression expression)
    {
        EnsureSufficientStack();
        switch (expression)
        {
            case LiteralExpression literal:
                Token(literal.Text);
                break;
            case NameExpression name:
                Token(name.Name);
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
