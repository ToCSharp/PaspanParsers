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
            case FunctionDefinition function:
                WriteFunctionDefinition(function);
                break;
            case NamespaceDefinition @namespace:
                WriteNamespaceDefinition(@namespace);
                break;
            case LinkageSpecification linkage:
                Token("extern");
                Space();
                Token("\"" + linkage.Language + "\"");
                WriteDeclarationBlockOrDeclaration(linkage.Declarations, linkage.HasBraces, linkage.CloseBraceDirectives);
                break;
            case ExportDeclaration export:
                Token("export");
                WriteDeclarationBlockOrDeclaration(export.Declarations, export.HasBraces, export.CloseBraceDirectives);
                break;
            case TemplateDeclaration template:
                Token("template");
                Space();
                WriteTemplateParameterList(template.Parameters);
                WriteRequiresClause(template.RequiresClause);
                NewLine();
                WriteDeclaration(template.Declaration);
                break;
            case ExplicitInstantiation instantiation:
                if (instantiation.IsExtern)
                {
                    Token("extern");
                    Space();
                }

                Token("template");
                Space();
                WriteDeclaration(instantiation.Declaration);
                break;
            case AccessSpecifier access:
                // Access specifiers stand out of the members
                _indentLevel--;
                Token(access.Access);
                Token(":");
                _indentLevel++;
                NewLine();
                break;
            case ModuleDeclaration module:
                WriteExport(module.IsExport);
                Token("module");
                WriteModuleName(module.Name, module.Partition);
                WriteAttributeSpecifiers(module.Attributes);
                Token(";");
                NewLine();
                break;
            case ImportDeclaration import:
                WriteExport(import.IsExport);
                Token("import");
                Space();
                if (import.Header != null)
                {
                    _builder.Append(import.Header);
                }
                else
                {
                    WriteModuleName(import.Module, import.Partition);
                }

                WriteAttributeSpecifiers(import.Attributes);
                Token(";");
                NewLine();
                break;
            default:
                WriteBlockDeclaration(declaration);
                NewLine();
                break;
        }
    }

    /// <summary>
    /// A declaration in a block or at namespace scope, without the line break after it.
    /// </summary>
    private void WriteBlockDeclaration(Declaration declaration)
    {
        Directives(declaration);
        switch (declaration)
        {
            case SimpleDeclaration simple:
                WriteSimpleDeclaration(simple);
                break;
            case StaticAssertDeclaration staticAssert:
                WriteStaticAssertDeclaration(staticAssert);
                break;
            case EmptyDeclaration:
                Token(";");
                break;
            case NamespaceAliasDefinition alias:
                Token("namespace");
                Space();
                Token(alias.Alias);
                Space();
                Token("=");
                Space();
                WriteName(alias.Target);
                Token(";");
                break;
            case UsingDirective directive:
                WriteLeadingAttributes(directive.Attributes);
                Token("using");
                Space();
                Token("namespace");
                Space();
                WriteName(directive.Namespace);
                Token(";");
                break;
            case UsingEnumDeclaration usingEnum:
                Token("using");
                Space();
                Token("enum");
                Space();
                WriteName(usingEnum.Enum);
                Token(";");
                break;
            case UsingDeclaration usingDeclaration:
                Token("using");
                for (var i = 0; i < usingDeclaration.Declarators.Count; i++)
                {
                    var declarator = usingDeclaration.Declarators[i];
                    Token(i > 0 ? "," : "");
                    Space();
                    Directives(declarator);
                    if (declarator.IsTypename)
                    {
                        Token("typename");
                        Space();
                    }

                    WriteName(declarator.Name);
                    if (declarator.IsPackExpansion)
                    {
                        Token("...");
                    }
                }

                Token(";");
                break;
            case AliasDeclaration alias:
                Token("using");
                Space();
                Token(alias.Identifier);
                WriteAttributeSpecifiers(alias.Attributes);
                Space();
                Token("=");
                Space();
                WriteTypeId(alias.Type);
                Token(";");
                break;
            case ConceptDefinition concept:
                Token("concept");
                Space();
                Token(concept.Name);
                Space();
                Token("=");
                Space();
                WriteExpression(concept.Constraint);
                Token(";");
                break;
            case AsmDeclaration asm:
                Token(asm.Keyword);
                foreach (var qualifier in asm.Qualifiers)
                {
                    Space();
                    Token(qualifier);
                }

                Token("(");
                _builder.Append(asm.Text);
                Token(")");
                Token(";");
                break;
            default:
                throw new NotSupportedException($"Unknown declaration {declaration.GetType().Name}");
        }
    }

    private void WriteExport(bool isExport)
    {
        if (isExport)
        {
            Token("export");
            Space();
        }
    }

    private void WriteModuleName(string name, string partition)
    {
        if (name != null)
        {
            Space();
            Token(name);
        }

        if (partition != null)
        {
            Space();
            Token(":");
            Token(partition);
        }
    }

    private void WriteFunctionDefinition(FunctionDefinition function)
    {
        WriteLeadingAttributes(function.Attributes);
        WriteSpecifiersAndDeclarator(function.Specifiers, function.Declarator);
        WriteVirtSpecifiers(function.VirtSpecifiers);
        WriteRequiresClause(function.RequiresClause);
        WriteAttributeSpecifiers(function.DeclaratorAttributes);
        if (function.IsDefaulted || function.IsDeleted)
        {
            Space();
            Token("=");
            Space();
            Token(function.IsDefaulted ? "default" : "delete");
            Token(";");
            NewLine();
            return;
        }

        if (function.Handlers != null)
        {
            NewLine();
            Token("try");
        }

        if (function.Initializers != null)
        {
            NewLine();
            _indentLevel++;
            Token(":");
            for (var i = 0; i < function.Initializers.Count; i++)
            {
                var initializer = function.Initializers[i];
                Token(i > 0 ? "," : "");
                Space();
                Directives(initializer);
                WriteName(initializer.Member);
                WriteInitializer(initializer.Initializer);
                if (initializer.IsPackExpansion)
                {
                    Token("...");
                }
            }

            _indentLevel--;
        }

        NewLine();
        WriteCompoundStatement(function.Body);
        if (function.Handlers != null)
        {
            WriteHandlers(function.Handlers);
        }

        NewLine();
    }

    private void WriteVirtSpecifiers(IReadOnlyList<string> specifiers)
    {
        foreach (var specifier in specifiers)
        {
            Space();
            Token(specifier);
        }
    }

    private void WriteNamespaceDefinition(NamespaceDefinition @namespace)
    {
        if (@namespace.IsInline)
        {
            Token("inline");
            Space();
        }

        Token("namespace");
        WriteAttributeSpecifiers(@namespace.Attributes);
        for (var i = 0; i < @namespace.Names.Count; i++)
        {
            var name = @namespace.Names[i];
            if (i > 0)
            {
                Token("::");
            }
            else
            {
                Space();
            }

            if (name.IsInline)
            {
                Token("inline");
                Space();
            }

            Token(name.Identifier);
        }

        WriteDeclarationBlock(@namespace.Declarations, @namespace.CloseBraceDirectives);
        NewLine();
    }

    /// <summary>
    /// The declarations of a linkage specification or an export declaration: in braces, or the single declaration.
    /// </summary>
    private void WriteDeclarationBlockOrDeclaration(IReadOnlyList<Declaration> declarations, bool hasBraces, IReadOnlyList<PreprocessorDirective> closeBraceDirectives)
    {
        if (hasBraces)
        {
            WriteDeclarationBlock(declarations, closeBraceDirectives);
            NewLine();
            return;
        }

        Space();
        WriteDeclaration(declarations[0]);
    }

    /// <summary>
    /// <c>{ declarations }</c> on their own lines, the closing brace not followed by a line break.
    /// </summary>
    private void WriteDeclarationBlock(IReadOnlyList<Declaration> declarations, IReadOnlyList<PreprocessorDirective> closeBraceDirectives)
    {
        NewLine();
        Token("{");
        NewLine();
        _indentLevel++;
        foreach (var declaration in declarations)
        {
            WriteDeclaration(declaration);
        }

        Directives(closeBraceDirectives);
        _indentLevel--;
        Token("}");
    }

    private void WriteStaticAssertDeclaration(StaticAssertDeclaration declaration)
    {
        Directives(declaration);
        Token("static_assert");
        Token("(");
        WriteExpression(declaration.Condition);
        if (declaration.Message != null)
        {
            Token(",");
            Space();
            WriteExpression(declaration.Message);
        }

        Token(")");
        Token(";");
    }

    /// <summary>
    /// Attributes at the start of a declaration or statement, followed by a space.
    /// </summary>
    private void WriteLeadingAttributes(IReadOnlyList<AttributeSpecifier> attributes)
    {
        if (attributes.Count != 0)
        {
            WriteAttributeSpecifiers(attributes);
            Space();
        }
    }

    private void WriteSimpleDeclaration(SimpleDeclaration declaration)
    {
        Directives(declaration);
        WriteLeadingAttributes(declaration.Attributes);
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

            WriteDeclSpecifier(specifier);
        }
    }

    private void WriteDeclSpecifier(DeclSpecifier specifier)
    {
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
                WriteAttributeSpecifiers(elaborated.Attributes);
                Space();
                WriteName(elaborated.Name);
                break;
            case ClassSpecifier @class:
                WriteClassSpecifier(@class);
                break;
            case EnumSpecifier @enum:
                WriteEnumSpecifier(@enum);
                break;
            case ExplicitSpecifier @explicit:
                Token("explicit");
                Token("(");
                WriteExpression(@explicit.Condition);
                Token(")");
                break;
            case AttributeDeclSpecifier attribute:
                WriteAttributeSpecifiers([attribute.Attribute]);
                break;
            case BitIntSpecifier bitInt:
                Token("_BitInt");
                Token("(");
                WriteExpression(bitInt.Width);
                Token(")");
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

    private void WriteInitDeclarator(DeclSpecifierSequence specifiers, InitDeclarator declarator)
    {
        Directives(declarator);
        WriteLeadingAttributes(declarator.LeadingAttributes);
        WriteSpecifiersAndDeclarator(specifiers, declarator.Declarator);
        WriteVirtSpecifiers(declarator.VirtSpecifiers);
        WriteRequiresClause(declarator.RequiresClause);
        if (declarator.AsmLabel != null)
        {
            Space();
            Token("asm");
            Token("(");
            _builder.Append(declarator.AsmLabel);
            Token(")");
        }

        WriteAttributeSpecifiers(declarator.Attributes);
        if (declarator.BitFieldWidth != null)
        {
            Space();
            Token(":");
            Space();
            WriteExpression(declarator.BitFieldWidth);
        }

        if (declarator.IsPure)
        {
            Space();
            Token("=");
            Space();
            Token("0");
        }

        WriteInitializer(declarator.Initializer);
    }

    private void WriteClassSpecifier(ClassSpecifier @class)
    {
        Token(@class.Key);
        WriteAttributeSpecifiers(@class.Attributes);
        if (@class.Name != null)
        {
            Space();
            WriteName(@class.Name);
        }

        if (@class.IsFinal)
        {
            Space();
            Token("final");
        }

        for (var i = 0; i < @class.Bases.Count; i++)
        {
            var @base = @class.Bases[i];
            Space();
            Token(i == 0 ? ":" : ",");
            Space();
            Directives(@base);
            WriteLeadingAttributes(@base.Attributes);
            if (@base.IsVirtual && @base.IsVirtualFirst)
            {
                Token("virtual");
                Space();
            }

            if (@base.Access != null)
            {
                Token(@base.Access);
                Space();
            }

            if (@base.IsVirtual && !@base.IsVirtualFirst)
            {
                Token("virtual");
                Space();
            }

            WriteName(@base.Name);
            if (@base.IsPackExpansion)
            {
                Token("...");
            }
        }

        WriteDeclarationBlock(@class.Members, @class.CloseBraceDirectives);
    }

    private void WriteEnumSpecifier(EnumSpecifier @enum)
    {
        Token("enum");
        if (@enum.ScopedKey != null)
        {
            Space();
            Token(@enum.ScopedKey);
        }

        WriteAttributeSpecifiers(@enum.Attributes);
        if (@enum.Name != null)
        {
            Space();
            WriteName(@enum.Name);
        }

        if (@enum.UnderlyingType != null)
        {
            Space();
            Token(":");
            Space();
            WriteTypeId(@enum.UnderlyingType);
        }

        if (@enum.Enumerators == null)
        {
            return;
        }

        NewLine();
        Token("{");
        NewLine();
        _indentLevel++;
        for (var i = 0; i < @enum.Enumerators.Count; i++)
        {
            var enumerator = @enum.Enumerators[i];
            Directives(enumerator);
            Token(enumerator.Identifier);
            WriteAttributeSpecifiers(enumerator.Attributes);
            if (enumerator.Value != null)
            {
                Space();
                Token("=");
                Space();
                WriteExpression(enumerator.Value);
            }

            if (i < @enum.Enumerators.Count - 1 || @enum.HasTrailingComma)
            {
                Token(",");
            }

            NewLine();
        }

        Directives(@enum.CloseBraceDirectives);
        _indentLevel--;
        Token("}");
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
            case ParenthesizedInitializer parenthesized:
                Directives(parenthesized);
                Token("(");
                WriteExpressionList(parenthesized.Arguments);
                Token(")");
                break;
            case BracedInitializer braced:
                Directives(braced);
                WriteExpression(braced.List);
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
                WriteAttributeSpecifiers(name.Attributes);
                break;
            case StructuredBindingDeclarator binding:
                Token("[");
                for (var i = 0; i < binding.Names.Count; i++)
                {
                    if (i > 0)
                    {
                        Token(",");
                        Space();
                    }

                    WriteName(binding.Names[i]);
                }

                Token("]");
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

    private void WriteParameters(IReadOnlyList<ParameterDeclaration> parameters, bool isVariadic)
    {
        for (var i = 0; i < parameters.Count; i++)
        {
            if (i > 0)
            {
                Token(",");
                Space();
            }

            WriteParameter(parameters[i]);
        }

        if (isVariadic)
        {
            if (parameters.Count > 0)
            {
                Token(",");
                Space();
            }

            Token("...");
        }
    }

    private void WriteNoexceptSpecifier(NoexceptSpecifier noexcept)
    {
        if (noexcept == null)
        {
            return;
        }

        Directives(noexcept);
        Space();
        if (noexcept.IsThrow)
        {
            Token("throw");
            Token("(");
            Token(")");
            return;
        }

        Token("noexcept");
        if (noexcept.Condition != null)
        {
            Token("(");
            WriteExpression(noexcept.Condition);
            Token(")");
        }
    }

    private void WriteFunctionDeclarator(FunctionDeclarator function)
    {
        WriteDeclarator(function.Inner);
        Token("(");
        WriteParameters(function.Parameters, function.IsVariadic);
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

        WriteNoexceptSpecifier(function.Noexcept);

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
        WriteLeadingAttributes(parameter.Attributes);
        if (parameter.IsExplicitObject)
        {
            Token("this");
            Space();
        }

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
                WriteBlockDeclaration(declaration.Declaration);
                break;
            case ExpressionStatement expression:
                if (expression.Expression != null)
                {
                    WriteExpression(expression.Expression);
                }

                Token(";");
                break;
            case IfStatement @if:
                WriteIfStatement(@if);
                break;
            case SwitchStatement @switch:
                Token("switch");
                WriteConditionClause(@switch.InitStatement, @switch.Condition);
                WriteEmbeddedStatement(@switch.Body);
                break;
            case CaseStatement @case:
                Token("case");
                Space();
                WriteExpression(@case.Value);
                if (@case.RangeEnd != null)
                {
                    Space();
                    Token("...");
                    Space();
                    WriteExpression(@case.RangeEnd);
                }

                Token(":");
                WriteLabeledStatement(@case.Statement);
                break;
            case DefaultStatement @default:
                Token("default");
                Token(":");
                WriteLabeledStatement(@default.Statement);
                break;
            case LabeledStatement labeled:
                Token(labeled.Label);
                Token(":");
                WriteLabeledStatement(labeled.Statement);
                break;
            case WhileStatement @while:
                Token("while");
                WriteConditionClause(null, @while.Condition);
                WriteEmbeddedStatement(@while.Body);
                break;
            case DoStatement @do:
                Token("do");
                WriteEmbeddedStatement(@do.Body);
                NewLine();
                Token("while");
                Space();
                Token("(");
                WriteExpression(@do.Condition);
                Token(")");
                Token(";");
                break;
            case ForStatement @for:
                WriteForStatement(@for);
                break;
            case RangeForStatement rangeFor:
                Token("for");
                Space();
                Token("(");
                WriteInitStatement(rangeFor.InitStatement);
                Directives(rangeFor.Declaration);
                WriteLeadingAttributes(rangeFor.Declaration.Attributes);
                WriteSpecifiersAndDeclarator(rangeFor.Declaration.Specifiers, rangeFor.Declaration.Declarator);
                Space();
                Token(":");
                Space();
                WriteExpression(rangeFor.Range);
                Token(")");
                WriteEmbeddedStatement(rangeFor.Body);
                break;
            case BreakStatement:
                Token("break");
                Token(";");
                break;
            case ContinueStatement:
                Token("continue");
                Token(";");
                break;
            case ReturnStatement @return:
                WriteReturnStatement("return", @return.Expression);
                break;
            case CoReturnStatement coReturn:
                WriteReturnStatement("co_return", coReturn.Expression);
                break;
            case GotoStatement @goto:
                Token("goto");
                Space();
                Token(@goto.Label);
                Token(";");
                break;
            case AttributedStatement attributed:
                WriteLeadingAttributes(attributed.Attributes);
                WriteStatement(attributed.Statement);
                break;
            case TryStatement @try:
                Token("try");
                NewLine();
                WriteCompoundStatement(@try.Block);
                WriteHandlers(@try.Handlers);
                break;
            default:
                throw new NotSupportedException($"Unknown statement {statement.GetType().Name}");
        }
    }

    private void WriteIfStatement(IfStatement @if)
    {
        Token("if");
        Space();
        if (@if.IsConsteval)
        {
            if (@if.IsNegated)
            {
                Token("!");
            }

            Token("consteval");
        }
        else
        {
            if (@if.IsConstexpr)
            {
                Token("constexpr");
            }

            WriteConditionClause(@if.InitStatement, @if.Condition);
        }

        WriteEmbeddedStatement(@if.Then);
        if (@if.Else != null)
        {
            NewLine();
            Token("else");
            if (@if.Else is IfStatement elseIf && elseIf.LeadingDirectives == null)
            {
                // else if on one line
                Space();
                WriteStatement(elseIf);
            }
            else
            {
                WriteEmbeddedStatement(@if.Else);
            }
        }
    }

    private void WriteForStatement(ForStatement @for)
    {
        Token("for");
        Space();
        Token("(");
        if (@for.InitStatement == null)
        {
            Token(";");
            Space();
        }

        WriteInitStatement(@for.InitStatement);
        if (@for.Condition != null)
        {
            WriteCondition(@for.Condition);
        }

        Token(";");
        if (@for.Increment != null)
        {
            Space();
            WriteExpression(@for.Increment);
        }

        Token(")");
        WriteEmbeddedStatement(@for.Body);
    }

    /// <summary>
    /// <c>( init-statement condition )</c> after if, switch or while.
    /// </summary>
    private void WriteConditionClause(Statement initStatement, CppNode condition)
    {
        Space();
        Token("(");
        WriteInitStatement(initStatement);
        WriteCondition(condition);
        Token(")");
    }

    /// <summary>
    /// The init-statement of an if, switch or for statement, with its ';', followed by a space.
    /// </summary>
    private void WriteInitStatement(Statement initStatement)
    {
        if (initStatement != null)
        {
            WriteStatement(initStatement);
            Space();
        }
    }

    private void WriteCondition(CppNode condition)
    {
        switch (condition)
        {
            case Expression expression:
                WriteExpression(expression);
                break;
            case ConditionDeclaration declaration:
                Directives(declaration);
                WriteLeadingAttributes(declaration.Attributes);
                WriteSpecifiersAndDeclarator(declaration.Specifiers, declaration.Declarator);
                WriteInitializer(declaration.Initializer);
                break;
            default:
                throw new NotSupportedException($"Unknown condition {condition?.GetType().Name}");
        }
    }

    private void WriteReturnStatement(string keyword, Expression expression)
    {
        Token(keyword);
        if (expression != null)
        {
            Space();
            WriteExpression(expression);
        }

        Token(";");
    }

    /// <summary>
    /// The statement after a label, on the next line; nothing for a label at the end of a block.
    /// </summary>
    private void WriteLabeledStatement(Statement statement)
    {
        if (statement == null)
        {
            return;
        }

        if (statement is CaseStatement or DefaultStatement or LabeledStatement)
        {
            NewLine();
            WriteStatement(statement);
            return;
        }

        WriteEmbeddedStatement(statement);
    }

    private void WriteHandlers(IReadOnlyList<CatchClause> handlers)
    {
        foreach (var handler in handlers)
        {
            NewLine();
            Directives(handler);
            Token("catch");
            Space();
            Token("(");
            if (handler.Declaration == null)
            {
                Token("...");
            }
            else
            {
                WriteParameter(handler.Declaration);
            }

            Token(")");
            NewLine();
            WriteCompoundStatement(handler.Body);
        }
    }

    /// <summary>
    /// The body of an if statement or a loop: a block on its own lines, other statements indented.
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
                if (conditional.WhenTrue != null)
                {
                    Space();
                    WriteExpression(conditional.WhenTrue);
                    Space();
                }

                Token(":");
                Space();
                WriteExpression(conditional.WhenFalse);
                break;
            case CallExpression call:
                WriteExpression(call.Callee);
                Token("(");
                WriteExpressionList(call.Arguments);
                Token(")");
                break;
            case ThisExpression:
                Token("this");
                break;
            case MemberAccessExpression member:
                WriteExpression(member.Object);
                Token(member.Operator);
                if (member.IsTemplate)
                {
                    Token("template");
                    Space();
                }

                WriteName(member.Member);
                break;
            case SubscriptExpression subscript:
                WriteExpression(subscript.Object);
                Token("[");
                WriteExpressionList(subscript.Arguments);
                Token("]");
                break;
            case CastExpression cast:
                Token("(");
                WriteTypeId(cast.Type);
                Token(")");
                WriteExpression(cast.Operand);
                break;
            case NamedCastExpression cast:
                Token(cast.Keyword);
                Token("<");
                WriteTypeId(cast.Type);
                Token(">");
                Token("(");
                WriteExpression(cast.Operand);
                Token(")");
                break;
            case FunctionalCastExpression cast:
                WriteDeclSpecifier(cast.Type);
                WriteInitializer(cast.Initializer);
                break;
            case BuiltinCallExpression builtin:
                Token(builtin.Name);
                Token("(");
                for (var i = 0; i < builtin.Arguments.Count; i++)
                {
                    if (i > 0)
                    {
                        Token(",");
                        Space();
                    }

                    if (builtin.Arguments[i] is TypeId type)
                    {
                        WriteTypeId(type);
                    }
                    else
                    {
                        WriteExpression((Expression)builtin.Arguments[i]);
                    }
                }

                Token(")");
                break;
            case SizeOfExpression size:
                Token(size.Keyword);
                WriteTypeOrExpressionOperand(size.Operand);
                break;
            case SizeOfPackExpression pack:
                Token("sizeof");
                Token("...");
                Token("(");
                WriteName(pack.Pack);
                Token(")");
                break;
            case NoexceptExpression noexcept:
                Token("noexcept");
                Token("(");
                WriteExpression(noexcept.Operand);
                Token(")");
                break;
            case TypeidExpression typeid:
                Token("typeid");
                Token("(");
                WriteTypeOrExpression(typeid.Operand);
                Token(")");
                break;
            case NewExpression @new:
                WriteNewExpression(@new);
                break;
            case DeleteExpression delete:
                if (delete.IsGlobal)
                {
                    Token("::");
                }

                Token("delete");
                if (delete.IsArray)
                {
                    Token("[");
                    Token("]");
                }

                Space();
                WriteExpression(delete.Operand);
                break;
            case ThrowExpression @throw:
                Token("throw");
                if (@throw.Operand != null)
                {
                    Space();
                    WriteExpression(@throw.Operand);
                }

                break;
            case YieldExpression yield:
                Token("co_yield");
                Space();
                WriteExpression(yield.Operand);
                break;
            case InitializerListExpression list:
                Token("{");
                WriteExpressionList(list.Elements);
                if (list.HasTrailingComma)
                {
                    Token(",");
                }

                Token("}");
                break;
            case DesignatedInitializerExpression designated:
                foreach (var designator in designated.Designators)
                {
                    Directives(designator);
                    if (designator.Index != null)
                    {
                        Token("[");
                        WriteExpression(designator.Index);
                        Token("]");
                    }
                    else
                    {
                        Token(".");
                        Token(designator.Member);
                    }
                }

                if (designated.HasEquals)
                {
                    Space();
                    Token("=");
                    Space();
                }

                WriteExpression(designated.Value);
                break;
            case PackExpansionExpression pack:
                WriteExpression(pack.Pattern);
                Token("...");
                break;
            case FoldExpression fold:
                Token("(");
                if (fold.Left != null)
                {
                    WriteExpression(fold.Left);
                    Space();
                    Token(fold.Operator);
                    Space();
                }

                Token("...");
                if (fold.Right != null)
                {
                    Space();
                    Token(fold.Operator);
                    Space();
                    WriteExpression(fold.Right);
                }

                Token(")");
                break;
            case LambdaExpression lambda:
                WriteLambdaExpression(lambda);
                break;
            case RequiresExpression requires:
                WriteRequiresExpression(requires);
                break;
            default:
                throw new NotSupportedException($"Unknown expression {expression.GetType().Name}");
        }
    }

    /// <summary>
    /// Expressions separated by commas: arguments, subscripts and the elements of lists.
    /// </summary>
    private void WriteExpressionList(IReadOnlyList<Expression> expressions)
    {
        for (var i = 0; i < expressions.Count; i++)
        {
            if (i > 0)
            {
                Token(",");
                Space();
            }

            WriteExpression(expressions[i]);
        }
    }

    /// <summary>
    /// A type in parentheses or an expression: the operand of <c>typeid</c>.
    /// </summary>
    private void WriteTypeOrExpression(CppNode operand)
    {
        if (operand is TypeId type)
        {
            WriteTypeId(type);
        }
        else
        {
            WriteExpression((Expression)operand);
        }
    }

    /// <summary>
    /// The operand of <c>sizeof</c> and <c>alignof</c>: a type in parentheses, or an expression.
    /// </summary>
    private void WriteTypeOrExpressionOperand(CppNode operand)
    {
        if (operand is TypeId type)
        {
            Token("(");
            WriteTypeId(type);
            Token(")");
        }
        else
        {
            Space();
            WriteExpression((Expression)operand);
        }
    }

    private void WriteNewExpression(NewExpression @new)
    {
        if (@new.IsGlobal)
        {
            Token("::");
        }

        Token("new");
        Space();
        if (@new.Placement != null)
        {
            Token("(");
            WriteExpressionList(@new.Placement);
            Token(")");
            Space();
        }

        if (@new.IsParenthesizedType)
        {
            Token("(");
            WriteTypeId(@new.Type);
            Token(")");
        }
        else
        {
            WriteTypeId(@new.Type);
        }

        WriteInitializer(@new.Initializer);
    }

    private void WriteLambdaExpression(LambdaExpression lambda)
    {
        Token("[");
        if (lambda.CaptureDefault != null)
        {
            Token(lambda.CaptureDefault);
            if (lambda.Captures.Count > 0)
            {
                Token(",");
                Space();
            }
        }

        for (var i = 0; i < lambda.Captures.Count; i++)
        {
            if (i > 0)
            {
                Token(",");
                Space();
            }

            WriteLambdaCapture(lambda.Captures[i]);
        }

        Token("]");
        if (lambda.TemplateParameters != null)
        {
            WriteTemplateParameterList(lambda.TemplateParameters);
            WriteRequiresClause(lambda.TemplateRequiresClause);
        }

        WriteAttributeSpecifiers(lambda.Attributes);
        if (lambda.Parameters != null)
        {
            Token("(");
            WriteParameters(lambda.Parameters, lambda.IsVariadic);
            Token(")");
        }

        foreach (var specifier in lambda.Specifiers)
        {
            Space();
            Token(specifier);
        }

        WriteNoexceptSpecifier(lambda.Noexcept);
        WriteAttributeSpecifiers(lambda.TypeAttributes);
        if (lambda.TrailingReturnType != null)
        {
            Space();
            Token("->");
            Space();
            WriteTypeId(lambda.TrailingReturnType);
        }

        WriteRequiresClause(lambda.RequiresClause);
        Space();
        WriteCompoundStatement(lambda.Body);
    }

    private void WriteLambdaCapture(LambdaCapture capture)
    {
        Directives(capture);
        if (capture.IsThis)
        {
            if (capture.IsStarThis)
            {
                Token("*");
            }

            Token("this");
            return;
        }

        if (capture.IsByReference)
        {
            Token("&");
        }

        if (capture.IsPack && capture.Initializer != null)
        {
            Token("...");
        }

        Token(capture.Identifier);
        if (capture.IsPack && capture.Initializer == null)
        {
            Token("...");
        }

        WriteInitializer(capture.Initializer);
    }

    private void WriteRequiresExpression(RequiresExpression requires)
    {
        Token("requires");
        if (requires.Parameters != null)
        {
            Space();
            Token("(");
            WriteParameters(requires.Parameters, isVariadic: false);
            Token(")");
        }

        Space();
        Token("{");
        foreach (var requirement in requires.Requirements)
        {
            Space();
            Directives(requirement);
            switch (requirement)
            {
                case SimpleRequirement simple:
                    WriteExpression(simple.Expression);
                    break;
                case TypeRequirement type:
                    Token("typename");
                    Space();
                    WriteName(type.Type);
                    break;
                case CompoundRequirement compound:
                    Token("{");
                    Space();
                    WriteExpression(compound.Expression);
                    Space();
                    Token("}");
                    if (compound.IsNoexcept)
                    {
                        Space();
                        Token("noexcept");
                    }

                    if (compound.TypeConstraint != null)
                    {
                        Space();
                        Token("->");
                        Space();
                        WriteName(compound.TypeConstraint);
                    }

                    break;
                case NestedRequirement nested:
                    Token("requires");
                    Space();
                    WriteExpression(nested.Constraint);
                    break;
                default:
                    throw new NotSupportedException($"Unknown requirement {requirement.GetType().Name}");
            }

            Token(";");
        }

        Space();
        Token("}");
    }

    // ========================================
    // Templates and attributes
    // ========================================

    private void WriteTemplateParameterList(IReadOnlyList<TemplateParameter> parameters)
    {
        Token("<");
        for (var i = 0; i < parameters.Count; i++)
        {
            if (i > 0)
            {
                Token(",");
                Space();
            }

            WriteTemplateParameter(parameters[i]);
        }

        Token(">");
    }

    private void WriteTemplateParameter(TemplateParameter parameter)
    {
        Directives(parameter);
        switch (parameter)
        {
            case TypeTemplateParameter type:
                if (type.Key != null)
                {
                    Token(type.Key);
                }
                else
                {
                    WriteName(type.Constraint);
                }

                if (type.IsPack)
                {
                    Token("...");
                }

                if (type.Identifier != null)
                {
                    Space();
                    Token(type.Identifier);
                }

                if (type.Default != null)
                {
                    Space();
                    Token("=");
                    Space();
                    WriteTypeId(type.Default);
                }

                break;
            case NonTypeTemplateParameter nonType:
                WriteParameter(nonType.Parameter);
                break;
            case TemplateTemplateParameter template:
                Token("template");
                Space();
                WriteTemplateParameterList(template.Parameters);
                Space();
                Token(template.Key);
                if (template.IsPack)
                {
                    Token("...");
                }

                if (template.Identifier != null)
                {
                    Space();
                    Token(template.Identifier);
                }

                if (template.Default != null)
                {
                    Space();
                    Token("=");
                    Space();
                    WriteName(template.Default);
                }

                break;
            default:
                throw new NotSupportedException($"Unknown template parameter {parameter.GetType().Name}");
        }
    }

    private void WriteAttributeSpecifiers(IReadOnlyList<AttributeSpecifier> specifiers)
    {
        foreach (var specifier in specifiers)
        {
            Space();
            Directives(specifier);
            switch (specifier)
            {
                case AlignasSpecifier alignas:
                    Token("alignas");
                    Token("(");
                    WriteTypeOrExpression(alignas.Operand);
                    if (alignas.IsPackExpansion)
                    {
                        Token("...");
                    }

                    Token(")");
                    continue;
                case GnuAttributeSpecifier gnu:
                    Token(gnu.Keyword);
                    Token("(");
                    Token("(");
                    _builder.Append(gnu.Arguments);
                    Token(")");
                    Token(")");
                    continue;
            }

            Token("[");
            Token("[");
            if (specifier.UsingNamespace != null)
            {
                Token("using");
                Space();
                Token(specifier.UsingNamespace);
                Token(":");
                Space();
            }

            for (var i = 0; i < specifier.Attributes.Count; i++)
            {
                if (i > 0)
                {
                    Token(",");
                    Space();
                }

                var attribute = specifier.Attributes[i];
                Directives(attribute);
                if (attribute.Namespace != null)
                {
                    Token(attribute.Namespace);
                    Token("::");
                }

                Token(attribute.Name);
                if (attribute.Arguments != null)
                {
                    Token("(");
                    _builder.Append(attribute.Arguments);
                    Token(")");
                }

                if (attribute.IsPackExpansion)
                {
                    Token("...");
                }
            }

            Token("]");
            Token("]");
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
