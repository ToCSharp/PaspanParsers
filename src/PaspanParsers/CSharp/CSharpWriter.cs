using System.Runtime.CompilerServices;
using System.Text;

namespace PaspanParsers.CSharp;

/// <summary>
/// Writes C# code from AST nodes
/// </summary>
public class CSharpWriter(string indentString = "    ")
{
    private readonly StringBuilder _builder = new StringBuilder();
    private int _indentLevel = 0;
    private bool _needsIndent = true;
    private readonly string _indentString = indentString;

    public string GetResult() => _builder.ToString();

    private void Write(string text)
    {
        if (_needsIndent && !string.IsNullOrEmpty(text))
        {
            for (int i = 0; i < _indentLevel; i++)
            {
                _builder.Append(_indentString);
            }
            _needsIndent = false;
        }
        _builder.Append(text);
    }

    private void WriteLine()
    {
        _builder.AppendLine();
        _needsIndent = true;
    }

    private void WriteLine(string text)
    {
        Write(text);
        WriteLine();
    }

    private void Indent()
    {
        _indentLevel++;
    }

    private void Unindent()
    {
        _indentLevel--;
    }

    /// <summary>
    /// Writes <c>#nullable</c> directives, each on its own line.
    /// </summary>
    private void WriteNullableDirectives(IReadOnlyList<NullableDirective> directives)
    {
        if (directives == null || directives.Count == 0)
            return;

        if (!_needsIndent)
            WriteLine();

        foreach (var directive in directives)
        {
            _builder.Append("#nullable ");
            _builder.Append(directive.Setting switch
            {
                NullableSetting.Enable => "enable",
                NullableSetting.Disable => "disable",
                NullableSetting.Restore => "restore",
                _ => throw new ArgumentException($"Unknown nullable setting: {directive.Setting}")
            });
            if (directive.Target.HasValue)
            {
                _builder.Append(directive.Target.Value == NullableTarget.Warnings ? " warnings" : " annotations");
            }
            WriteLine();
        }
    }

    private void WriteList<T>(IReadOnlyList<T> items, Action<T> writeItem, string separator = ", ")
    {
        if (items == null || items.Count == 0)
            return;

        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0)
                Write(separator);
            writeItem(items[i]);
        }
    }

    // ========================================
    // Compilation Unit
    // ========================================

    public void WriteCompilationUnit(CompilationUnit unit)
    {
        var length = _builder.Length;
        var indentLevel = _indentLevel;
        var needsIndent = _needsIndent;
        try
        {
            WriteCompilationUnitCore(unit);
            return;
        }
        catch (InsufficientExecutionStackException)
        {
            // A deeply nested tree: write it again on a thread with a large stack, like CSharpParser parses it
            _builder.Length = length;
            _indentLevel = indentLevel;
            _needsIndent = needsIndent;
        }

        Exception failure = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    WriteCompilationUnitCore(unit);
                }
                catch (Exception e)
                {
                    failure = e;
                }
            },
            LargeStackSize);
        thread.Start();
        thread.Join();

        if (failure != null)
        {
            throw failure is InsufficientExecutionStackException
                ? new InsufficientExecutionStackException("The syntax tree is nested too deeply.", failure)
                : new InvalidOperationException("Writing the syntax tree failed.", failure);
        }
    }

    private const int LargeStackSize = 256 * 1024 * 1024;

    /// <summary>
    /// Guards the recursion over nested nodes: a tree too deep for the stack throws
    /// <see cref="InsufficientExecutionStackException"/> instead of overflowing the stack.
    /// </summary>
    private static void EnsureSufficientStack() => RuntimeHelpers.EnsureSufficientExecutionStack();

    private void WriteCompilationUnitCore(CompilationUnit unit)
    {
        if (unit.ExternAliases != null)
        {
            foreach (var externAlias in unit.ExternAliases)
            {
                WriteExternAliasDirective(externAlias);
            }
            if (unit.ExternAliases.Count > 0)
                WriteLine();
        }

        if (unit.Usings != null)
        {
            foreach (var usingDirective in unit.Usings)
            {
                WriteUsingDirective(usingDirective);
            }
            if (unit.Usings.Count > 0)
                WriteLine();
        }

        if (unit.GlobalAttributes != null)
        {
            foreach (var attr in unit.GlobalAttributes)
            {
                WriteAttributeSection(attr);
            }
            if (unit.GlobalAttributes.Count > 0)
                WriteLine();
        }

        if (unit.Members != null)
        {
            for (int i = 0; i < unit.Members.Count; i++)
            {
                if (i > 0)
                    WriteLine();
                WriteMemberDeclaration(unit.Members[i]);
            }
        }

        WriteNullableDirectives(unit.EndNullableDirectives);
    }

    // ========================================
    // Using Directives
    // ========================================

    private void WriteExternAliasDirective(ExternAliasDirective directive)
    {
        WriteNullableDirectives(directive.NullableDirectives);
        WriteLine($"extern alias {Id(directive.Identifier)};");
    }

    private void WriteUsingDirective(UsingDirective directive)
    {
        WriteNullableDirectives(directive.NullableDirectives);
        if (directive.IsGlobal)
            Write("global ");
        Write("using ");
        if (directive is UsingStaticDirective)
            Write("static ");
        if (directive.IsUnsafe)
            Write("unsafe ");

        switch (directive)
        {
            case UsingNamespaceDirective ns:
                WriteNameExpression(ns.Namespace);
                break;
            case UsingAliasDirective alias:
                Write($"{Id(alias.Alias)} = ");
                if (alias.Target != null)
                    WriteNameExpression(alias.Target);
                else
                    WriteTypeReference(alias.TargetType);
                break;
            case UsingStaticDirective staticDir:
                if (staticDir.Type != null)
                    WriteNameExpression(staticDir.Type);
                else
                    WriteTypeReference(staticDir.TargetType);
                break;
        }

        WriteLine(";");
    }

    // ========================================
    // Member Declarations
    // ========================================

    private void WriteMemberDeclaration(MemberDeclaration member)
    {
        EnsureSufficientStack();
        WriteNullableDirectives(member.NullableDirectives);

        switch (member)
        {
            case NamespaceDeclaration ns:
                WriteNamespaceDeclaration(ns);
                break;
            case ClassDeclaration cls:
                WriteClassDeclaration(cls);
                break;
            case StructDeclaration str:
                WriteStructDeclaration(str);
                break;
            case InterfaceDeclaration iface:
                WriteInterfaceDeclaration(iface);
                break;
            case EnumDeclaration enm:
                WriteEnumDeclaration(enm);
                break;
            case DelegateDeclaration del:
                WriteDelegateDeclaration(del);
                break;
            case RecordDeclaration rec:
                WriteRecordDeclaration(rec);
                break;
            case FieldDeclaration field:
                WriteFieldDeclaration(field);
                break;
            case MethodDeclaration method:
                WriteMethodDeclaration(method);
                break;
            case PropertyDeclaration prop:
                WritePropertyDeclaration(prop);
                break;
            case IndexerDeclaration indexer:
                WriteIndexerDeclaration(indexer);
                break;
            case EventDeclaration evt:
                WriteEventDeclaration(evt);
                break;
            case ConstructorDeclaration ctor:
                WriteConstructorDeclaration(ctor);
                break;
            case DestructorDeclaration dtor:
                WriteDestructorDeclaration(dtor);
                break;
            case OperatorDeclaration op:
                WriteOperatorDeclaration(op);
                break;
            case ConversionOperatorDeclaration conversion:
                WriteConversionOperatorDeclaration(conversion);
                break;
            case ExtensionBlockDeclaration extension:
                WriteExtensionBlockDeclaration(extension);
                break;
            case GlobalStatement global:
                WriteStatement(global.Statement);
                break;
            case IncompleteMemberDeclaration incomplete:
                WriteLine(incomplete.Text);
                break;
        }
    }

    private void WriteNamespaceDeclaration(NamespaceDeclaration ns)
    {
        Write("namespace ");
        WriteNameExpression(ns.Name);

        if (ns.IsFileScopedNamespace)
        {
            WriteLine(";");
            WriteLine();

            if (ns.ExternAliases != null)
            {
                foreach (var externAlias in ns.ExternAliases)
                {
                    WriteExternAliasDirective(externAlias);
                }
                if (ns.ExternAliases.Count > 0)
                    WriteLine();
            }

            if (ns.Usings != null)
            {
                foreach (var usingDirective in ns.Usings)
                {
                    WriteUsingDirective(usingDirective);
                }
                if (ns.Usings.Count > 0)
                    WriteLine();
            }

            if (ns.Members != null)
            {
                for (int i = 0; i < ns.Members.Count; i++)
                {
                    if (i > 0)
                        WriteLine();
                    WriteMemberDeclaration(ns.Members[i]);
                }
            }
        }
        else
        {
            WriteLine();
            WriteLine("{");
            Indent();

            if (ns.ExternAliases != null)
            {
                foreach (var externAlias in ns.ExternAliases)
                {
                    WriteExternAliasDirective(externAlias);
                }
                if (ns.ExternAliases.Count > 0)
                    WriteLine();
            }

            if (ns.Usings != null)
            {
                foreach (var usingDirective in ns.Usings)
                {
                    WriteUsingDirective(usingDirective);
                }
                if (ns.Usings.Count > 0)
                    WriteLine();
            }

            if (ns.Members != null)
            {
                for (int i = 0; i < ns.Members.Count; i++)
                {
                    if (i > 0)
                        WriteLine();
                    WriteMemberDeclaration(ns.Members[i]);
                }
            }

            WriteNullableDirectives(ns.CloseBraceNullableDirectives);
            Unindent();
            WriteLine(ns.HasTrailingSemicolon ? "};" : "}");
        }
    }

    private void WriteClassDeclaration(ClassDeclaration cls)
    {
        WriteTypeDeclaration(cls, "class", cls.Name, cls.TypeParameters, cls.BaseTypes, cls.Constraints, cls.Members);
    }

    private void WriteStructDeclaration(StructDeclaration str)
    {
        WriteTypeDeclaration(str, "struct", str.Name, str.TypeParameters, str.Interfaces, str.Constraints, str.Members);
    }

    private void WriteInterfaceDeclaration(InterfaceDeclaration iface)
    {
        WriteTypeDeclaration(iface, "interface", iface.Name, iface.TypeParameters, iface.BaseInterfaces, iface.Constraints, iface.Members);
    }

    private void WriteTypeDeclaration(
        TypeDeclaration type,
        string keyword,
        string name,
        IReadOnlyList<TypeParameter> typeParameters,
        IReadOnlyList<TypeReference> baseTypes,
        IReadOnlyList<TypeParameterConstraint> constraints,
        IReadOnlyList<MemberDeclaration> members)
    {
        WriteAttributes(type.Attributes);
        WriteModifiers(type);
        Write($"{keyword} {Id(name)}");

        if (typeParameters != null && typeParameters.Count > 0)
        {
            Write("<");
            WriteList(typeParameters, WriteTypeParameter);
            Write(">");
        }

        if (type.PrimaryConstructorParameters != null)
        {
            Write("(");
            WriteList(type.PrimaryConstructorParameters, WriteParameter);
            Write(")");
        }

        if (baseTypes != null && baseTypes.Count > 0)
        {
            Write(" : ");
            for (int i = 0; i < baseTypes.Count; i++)
            {
                if (i > 0)
                    Write(", ");
                WriteTypeReference(baseTypes[i]);
                if (i == 0 && type.BaseArguments != null)
                {
                    Write("(");
                    WriteList(type.BaseArguments, WriteArgument);
                    Write(")");
                }
            }
        }

        WriteConstraints(constraints);

        var hasBody = type.HasBody ?? !(type is RecordDeclaration && (members == null || members.Count == 0));
        if (!hasBody)
        {
            WriteLine(";");
            return;
        }

        WriteLine();
        WriteNullableDirectives(type.OpenBraceNullableDirectives);
        WriteLine("{");
        Indent();

        if (members != null)
        {
            for (int i = 0; i < members.Count; i++)
            {
                if (i > 0)
                    WriteLine();
                WriteMemberDeclaration(members[i]);
            }
        }

        WriteNullableDirectives(type.CloseBraceNullableDirectives);
        Unindent();
        WriteLine(type.HasTrailingSemicolon ? "};" : "}");
    }

    private void WriteConstraints(IReadOnlyList<TypeParameterConstraint> constraints)
    {
        if (constraints == null)
            return;

        foreach (var constraint in constraints)
        {
            WriteLine();
            Indent();
            WriteTypeParameterConstraint(constraint);
            Unindent();
        }
    }

    private void WriteExplicitInterface(TypeReference explicitInterface)
    {
        if (explicitInterface != null)
        {
            WriteTypeReference(explicitInterface);
            Write(".");
        }
    }

    private void WriteEnumDeclaration(EnumDeclaration enm)
    {
        WriteAttributes(enm.Attributes);
        WriteModifiers(enm);
        Write($"enum {Id(enm.Name)}");

        if (enm.BaseType != null)
        {
            Write(" : ");
            WriteTypeReference(enm.BaseType);
        }

        WriteLine();
        WriteLine("{");
        Indent();

        if (enm.Members != null)
        {
            for (int i = 0; i < enm.Members.Count; i++)
            {
                if (i > 0)
                {
                    WriteLine(",");
                }
                WriteEnumMember(enm.Members[i]);
            }
            WriteLine(enm.HasTrailingComma ? "," : "");
        }

        Unindent();
        WriteLine(enm.HasTrailingSemicolon ? "};" : "}");
    }

    private void WriteEnumMember(EnumMember member)
    {
        WriteAttributes(member.Attributes);
        Write(Id(member.Name));
        if (member.Value != null)
        {
            Write(" = ");
            WriteExpression(member.Value);
        }
    }

    private void WriteDelegateDeclaration(DelegateDeclaration del)
    {
        WriteAttributes(del.Attributes);
        WriteModifiers(del);
        Write("delegate ");
        WriteTypeReference(del.ReturnType);
        Write($" {Id(del.Name)}");

        if (del.TypeParameters != null && del.TypeParameters.Count > 0)
        {
            Write("<");
            WriteList(del.TypeParameters, WriteTypeParameter);
            Write(">");
        }

        Write("(");
        WriteList(del.Parameters, WriteParameter);
        Write(")");

        WriteConstraints(del.Constraints);

        WriteLine(";");
    }

    private void WriteRecordDeclaration(RecordDeclaration rec)
    {
        var keyword = rec.IsRecordStruct ? "record struct" : rec.HasClassKeyword ? "record class" : "record";
        WriteTypeDeclaration(rec, keyword, rec.Name, rec.TypeParameters, rec.BaseTypes, rec.Constraints, rec.Members);
    }

    private void WriteFieldDeclaration(FieldDeclaration field)
    {
        WriteAttributes(field.Attributes);
        WriteModifiers(field);
        WriteTypeReference(field.Type);
        Write(" ");
        WriteList(field.Variables, WriteVariableDeclarator);
        WriteLine(";");
    }

    private void WriteVariableDeclarator(VariableDeclarator variable)
    {
        Write(Id(variable.Name));
        if (variable.BracketedArguments != null)
        {
            Write("[");
            WriteList(variable.BracketedArguments, WriteArgument);
            Write("]");
        }
        if (variable.Initializer != null)
        {
            Write(" = ");
            WriteExpression(variable.Initializer);
        }
    }

    private void WriteMethodDeclaration(MethodDeclaration method)
    {
        WriteAttributes(method.Attributes);
        WriteModifiers(method);
        WriteTypeReference(method.ReturnType);
        Write(" ");
        WriteExplicitInterface(method.ExplicitInterface);
        Write(Id(method.Name));

        if (method.TypeParameters != null && method.TypeParameters.Count > 0)
        {
            Write("<");
            WriteList(method.TypeParameters, WriteTypeParameter);
            Write(">");
        }

        Write("(");
        WriteList(method.Parameters, WriteParameter);
        Write(")");

        WriteConstraints(method.Constraints);

        if (method.Body != null)
        {
            WriteMethodBody(method.Body);
        }
        else
        {
            WriteLine(";");
        }
    }

    private void WriteMethodBody(MethodBody body)
    {
        switch (body)
        {
            case BlockMethodBody block:
                WriteLine();
                WriteBlockStatement(block.Block);
                break;
            case ExpressionMethodBody expr:
                Write(" => ");
                WriteExpression(expr.Expression);
                WriteLine(";");
                break;
        }
    }

    private void WritePropertyDeclaration(PropertyDeclaration prop)
    {
        WriteAttributes(prop.Attributes);
        WriteModifiers(prop);
        WriteTypeReference(prop.Type);
        Write(" ");
        WriteExplicitInterface(prop.ExplicitInterface);
        Write(Id(prop.Name));

        if (prop.ExpressionBody != null)
        {
            Write(" => ");
            WriteExpression(prop.ExpressionBody);
            WriteLine(";");
        }
        else if (prop.Accessors != null && prop.Accessors.Count > 0)
        {
            WriteLine();
            WriteNullableDirectives(prop.OpenBraceNullableDirectives);
            WriteLine("{");
            Indent();

            foreach (var accessor in prop.Accessors)
            {
                WriteAccessor(accessor);
            }

            Unindent();
            Write("}");

            if (prop.Initializer != null)
            {
                Write(" = ");
                WriteExpression(prop.Initializer);
                WriteLine(";");
            }
            else
            {
                WriteLine();
            }
        }
        else
        {
            WriteLine(";");
        }
    }

    private void WriteAccessor(Accessor accessor)
    {
        WriteNullableDirectives(accessor.NullableDirectives);
        WriteAttributes(accessor.Attributes);
        if (accessor.ModifierList != null)
            WriteModifierList(accessor.ModifierList);
        else
            WriteModifiers(accessor.Modifiers);
        Write(accessor.Kind switch
        {
            AccessorKind.Get => "get",
            AccessorKind.Set => "set",
            AccessorKind.Init => "init",
            _ => throw new ArgumentException($"Unknown accessor kind: {accessor.Kind}")
        });

        if (accessor.Body != null)
        {
            WriteMethodBody(accessor.Body);
        }
        else
        {
            WriteLine(";");
        }
    }

    private void WriteIndexerDeclaration(IndexerDeclaration indexer)
    {
        WriteAttributes(indexer.Attributes);
        WriteModifiers(indexer);
        WriteTypeReference(indexer.Type);
        Write(" ");
        WriteExplicitInterface(indexer.ExplicitInterface);
        Write("this[");
        WriteList(indexer.Parameters, WriteParameter);
        Write("]");

        if (indexer.ExpressionBody != null)
        {
            Write(" => ");
            WriteExpression(indexer.ExpressionBody);
            WriteLine(";");
            return;
        }

        WriteLine();
        WriteNullableDirectives(indexer.OpenBraceNullableDirectives);
        WriteLine("{");
        Indent();

        foreach (var accessor in indexer.Accessors)
        {
            WriteAccessor(accessor);
        }

        Unindent();
        WriteLine("}");
    }

    private void WriteEventDeclaration(EventDeclaration evt)
    {
        WriteAttributes(evt.Attributes);
        WriteModifiers(evt);
        Write("event ");
        WriteTypeReference(evt.Type);
        Write(" ");

        if (evt.Accessors != null && evt.Accessors.Count > 0)
        {
            // Event with accessors
            WriteExplicitInterface(evt.ExplicitInterface);
            WriteList(evt.Variables, WriteVariableDeclarator);
            WriteLine();
            WriteLine("{");
            Indent();

            foreach (var accessor in evt.Accessors)
            {
                WriteEventAccessor(accessor);
            }

            Unindent();
            WriteLine("}");
        }
        else
        {
            // Field-like event
            WriteList(evt.Variables, WriteVariableDeclarator);
            WriteLine(";");
        }
    }

    private void WriteEventAccessor(EventAccessor accessor)
    {
        WriteAttributes(accessor.Attributes);
        Write(accessor.Kind switch
        {
            EventAccessorKind.Add => "add",
            EventAccessorKind.Remove => "remove",
            _ => throw new ArgumentException($"Unknown event accessor kind: {accessor.Kind}")
        });

        if (accessor.ExpressionBody != null)
        {
            Write(" => ");
            WriteExpression(accessor.ExpressionBody);
            WriteLine(";");
            return;
        }

        WriteLine();
        WriteBlockStatement(accessor.Block);
    }

    private void WriteConstructorDeclaration(ConstructorDeclaration ctor)
    {
        WriteAttributes(ctor.Attributes);
        WriteModifiers(ctor);
        Write($"{Id(ctor.Name)}(");
        WriteList(ctor.Parameters, WriteParameter);
        Write(")");

        if (ctor.Initializer != null)
        {
            WriteLine();
            Indent();
            Write(": ");
            Write(ctor.Initializer.IsBase ? "base(" : "this(");
            WriteList(ctor.Initializer.Arguments, WriteArgument);
            Write(")");
            Unindent();
        }

        if (ctor.Body != null)
        {
            WriteLine();
            WriteMethodBody(ctor.Body);
        }
        else
        {
            WriteLine(";");
        }
    }

    private void WriteDestructorDeclaration(DestructorDeclaration dtor)
    {
        WriteAttributes(dtor.Attributes);
        WriteModifiers(dtor);
        Write($"~{Id(dtor.Name)}()");
        WriteMemberBody(dtor.Body);
    }

    private void WriteOperatorDeclaration(OperatorDeclaration op)
    {
        WriteAttributes(op.Attributes);
        WriteModifiers(op);
        WriteTypeReference(op.ReturnType);
        Write(" ");
        WriteExplicitInterface(op.ExplicitInterface);
        Write("operator ");
        if (op.IsChecked)
            Write("checked ");
        Write(op.Operator);
        Write("(");
        WriteList(op.Parameters, WriteParameter);
        Write(")");
        WriteMemberBody(op.Body);
    }

    private void WriteConversionOperatorDeclaration(ConversionOperatorDeclaration conversion)
    {
        WriteAttributes(conversion.Attributes);
        WriteModifiers(conversion);
        Write(conversion.IsImplicit ? "implicit " : "explicit ");
        WriteExplicitInterface(conversion.ExplicitInterface);
        Write("operator ");
        if (conversion.IsChecked)
            Write("checked ");
        WriteTypeReference(conversion.Type);
        Write("(");
        WriteList(conversion.Parameters, WriteParameter);
        Write(")");
        WriteMemberBody(conversion.Body);
    }

    private void WriteExtensionBlockDeclaration(ExtensionBlockDeclaration extension)
    {
        WriteAttributes(extension.Attributes);
        WriteModifiers(extension);
        Write("extension");

        if (extension.TypeParameters != null && extension.TypeParameters.Count > 0)
        {
            Write("<");
            WriteList(extension.TypeParameters, WriteTypeParameter);
            Write(">");
        }

        Write("(");
        WriteParameter(extension.Receiver);
        Write(")");

        WriteConstraints(extension.Constraints);

        WriteLine();
        WriteLine("{");
        Indent();

        if (extension.Members != null)
        {
            for (int i = 0; i < extension.Members.Count; i++)
            {
                if (i > 0)
                    WriteLine();
                WriteMemberDeclaration(extension.Members[i]);
            }
        }

        WriteNullableDirectives(extension.CloseBraceNullableDirectives);
        Unindent();
        WriteLine("}");
    }

    /// <summary>
    /// The body of a member, or ';' when it has none.
    /// </summary>
    private void WriteMemberBody(MethodBody body)
    {
        if (body != null)
            WriteMethodBody(body);
        else
            WriteLine(";");
    }

    // ========================================
    // Type Parameters and Constraints
    // ========================================

    private void WriteTypeParameter(TypeParameter param)
    {
        WriteAttributes(param.Attributes);
        if (param.Variance.HasValue)
        {
            Write(param.Variance.Value switch
            {
                VarianceKind.In => "in ",
                VarianceKind.Out => "out ",
                _ => throw new ArgumentException($"Unknown variance: {param.Variance}")
            });
        }
        Write(Id(param.Name));
    }

    private void WriteTypeParameterConstraint(TypeParameterConstraint constraint)
    {
        WriteNullableDirectives(constraint.NullableDirectives);
        Write($"where {Id(constraint.TypeParameterName)} : ");
        WriteList(constraint.Constraints, WriteTypeConstraint);
    }

    private void WriteTypeConstraint(TypeConstraint constraint)
    {
        switch (constraint)
        {
            case ClassConstraint cls:
                Write(cls.IsNullable ? "class?" : "class");
                break;
            case StructConstraint:
                Write("struct");
                break;
            case UnmanagedConstraint:
                Write("unmanaged");
                break;
            case NotNullConstraint:
                Write("notnull");
                break;
            case TypeReferenceConstraint typeRef:
                WriteTypeReference(typeRef.Type);
                break;
            case ConstructorConstraint:
                Write("new()");
                break;
            case DefaultConstraint:
                Write("default");
                break;
            case AllowsRefStructConstraint:
                Write("allows ref struct");
                break;
        }
    }

    private void WriteParameter(Parameter param)
    {
        WriteNullableDirectives(param.NullableDirectives);
        WriteAttributes(param.Attributes);

        foreach (var modifier in param.Modifiers)
        {
            Write(GetParameterModifierString(modifier));
            Write(" ");
        }

        if (param.Type == null)
        {
            // Implicitly typed lambda parameter, or __arglist
            Write(param.Name == "__arglist" ? "__arglist" : Id(param.Name));
        }
        else
        {
            WriteTypeReference(param.Type);
            if (param.Name != null)
                Write($" {Id(param.Name)}");
        }

        if (param.DefaultValue != null)
        {
            Write(" = ");
            WriteExpression(param.DefaultValue);
        }
    }

    private static string GetParameterModifierString(ParameterModifier modifier) => modifier switch
    {
        ParameterModifier.This => "this",
        ParameterModifier.Ref => "ref",
        ParameterModifier.Out => "out",
        ParameterModifier.In => "in",
        ParameterModifier.Params => "params",
        ParameterModifier.Scoped => "scoped",
        ParameterModifier.Readonly => "readonly",
        _ => throw new ArgumentException($"Unknown parameter modifier: {modifier}")
    };

    // ========================================
    // Type References
    // ========================================

    private void WriteTypeReference(TypeReference type)
    {
        EnsureSufficientStack();
        WriteNullableDirectives(type.NullableDirectives);
        switch (type)
        {
            case NamedTypeReference named:
                if (named.Qualifier != null)
                {
                    WriteTypeReference(named.Qualifier);
                    Write(".");
                }
                if (named.Alias != null)
                {
                    Write($"{Id(named.Alias)}::");
                }
                WriteNameExpression(named.Name);
                if (named.TypeArguments != null && named.TypeArguments.Count > 0)
                {
                    Write("<");
                    WriteList(named.TypeArguments, WriteTypeReference);
                    WriteNullableDirectives(named.CloseAngleNullableDirectives);
                    Write(">");
                }
                if (named.IsNullable)
                    Write("?");
                break;

            case PredefinedTypeReference predefined:
                Write(predefined.Type switch
                {
                    PredefinedType.Object => "object",
                    PredefinedType.String => "string",
                    PredefinedType.Bool => "bool",
                    PredefinedType.Byte => "byte",
                    PredefinedType.SByte => "sbyte",
                    PredefinedType.Short => "short",
                    PredefinedType.UShort => "ushort",
                    PredefinedType.Int => "int",
                    PredefinedType.UInt => "uint",
                    PredefinedType.Long => "long",
                    PredefinedType.ULong => "ulong",
                    PredefinedType.Float => "float",
                    PredefinedType.Double => "double",
                    PredefinedType.Decimal => "decimal",
                    PredefinedType.Char => "char",
                    PredefinedType.Void => "void",
                    PredefinedType.Dynamic => "dynamic",
                    _ => throw new ArgumentException($"Unknown predefined type: {predefined.Type}")
                });
                if (predefined.IsNullable)
                    Write("?");
                break;

            case ArrayTypeReference array:
                WriteTypeReference(array.ElementType);
                Write("[");
                for (int i = 1; i < array.Rank; i++)
                    Write(",");
                Write("]");
                break;

            case TupleTypeReference tuple:
                Write("(");
                WriteList(tuple.Elements, WriteTupleElement);
                Write(")");
                break;

            case NullableTypeReference nullable:
                WriteTypeReference(nullable.ElementType);
                Write("?");
                break;

            case PointerTypeReference pointer:
                WriteTypeReference(pointer.ElementType);
                Write("*");
                break;

            case FunctionPointerTypeReference functionPointer:
                Write("delegate*");
                if (functionPointer.CallingConvention != null)
                {
                    Write($" {functionPointer.CallingConvention}");
                    if (functionPointer.UnmanagedCallingConventions != null)
                    {
                        Write($"[{string.Join(", ", functionPointer.UnmanagedCallingConventions.Select(Id))}]");
                    }
                }
                Write("<");
                WriteList(functionPointer.Parameters, parameter =>
                {
                    if (parameter.Modifiers != null)
                    {
                        foreach (var modifier in parameter.Modifiers)
                        {
                            Write(GetParameterModifierString(modifier));
                            Write(" ");
                        }
                    }
                    WriteTypeReference(parameter.Type);
                });
                Write(">");
                break;

            case RefTypeReference reference:
                Write(reference.IsReadOnly ? "ref readonly " : "ref ");
                WriteTypeReference(reference.Type);
                break;

            case ScopedTypeReference scoped:
                Write("scoped ");
                WriteTypeReference(scoped.Type);
                break;

            case OmittedTypeReference:
                break;
        }
    }

    private void WriteTupleElement(TupleElement element)
    {
        WriteTypeReference(element.Type);
        if (!string.IsNullOrEmpty(element.Name))
        {
            Write($" {Id(element.Name)}");
        }
    }

    // ========================================
    // Statements
    // ========================================

    private void WriteStatement(Statement stmt)
    {
        EnsureSufficientStack();

        // A block writes its directives itself, also as the body of a member
        if (stmt is not BlockStatement)
            WriteNullableDirectives(stmt.NullableDirectives);

        switch (stmt)
        {
            case BlockStatement block:
                WriteBlockStatement(block);
                break;
            case IncompleteStatement incomplete:
                WriteLine(incomplete.Text);
                break;
            case ExpressionStatement expr:
                WriteExpression(expr.Expression);
                WriteLine(";");
                break;
            case LocalDeclarationStatement local:
                WriteLocalDeclarationStatement(local);
                break;
            case IfStatement ifStmt:
                WriteIfStatement(ifStmt);
                break;
            case SwitchStatement switchStmt:
                WriteSwitchStatement(switchStmt);
                break;
            case WhileStatement whileStmt:
                WriteWhileStatement(whileStmt);
                break;
            case DoStatement doStmt:
                WriteDoStatement(doStmt);
                break;
            case ForStatement forStmt:
                WriteForStatement(forStmt);
                break;
            case ForEachStatement forEachStmt:
                WriteForEachStatement(forEachStmt);
                break;
            case BreakStatement:
                WriteLine("break;");
                break;
            case ContinueStatement:
                WriteLine("continue;");
                break;
            case ReturnStatement returnStmt:
                WriteReturnStatement(returnStmt);
                break;
            case ThrowStatement throwStmt:
                WriteThrowStatement(throwStmt);
                break;
            case TryStatement tryStmt:
                WriteTryStatement(tryStmt);
                break;
            case UsingStatement usingStmt:
                WriteUsingStatement(usingStmt);
                break;
            case LockStatement lockStmt:
                WriteLockStatement(lockStmt);
                break;
            case YieldReturnStatement yieldReturn:
                Write("yield return ");
                WriteExpression(yieldReturn.Expression);
                WriteLine(";");
                break;
            case YieldBreakStatement:
                WriteLine("yield break;");
                break;
            case LabeledStatement labeled:
                WriteLabeledStatement(labeled);
                break;
            case GotoStatement gotoStmt:
                switch (gotoStmt.Kind)
                {
                    case GotoKind.Case:
                        Write("goto case ");
                        WriteExpression(gotoStmt.CaseExpression);
                        WriteLine(";");
                        break;
                    case GotoKind.Default:
                        WriteLine("goto default;");
                        break;
                    default:
                        WriteLine($"goto {Id(gotoStmt.Label)};");
                        break;
                }
                break;
            case EmptyStatement:
                WriteLine(";");
                break;
            case CheckedStatement checkedStmt:
                WriteLine(checkedStmt.IsChecked ? "checked" : "unchecked");
                WriteBlockStatement(checkedStmt.Block);
                break;
            case UnsafeStatement unsafeStmt:
                WriteLine("unsafe");
                WriteBlockStatement(unsafeStmt.Block);
                break;
            case FixedStatement fixedStmt:
                Write("fixed (");
                WriteTypeReference(fixedStmt.Type);
                Write(" ");
                WriteList(fixedStmt.Variables, WriteVariableDeclarator);
                WriteLine(")");
                WriteStatement(fixedStmt.Body);
                break;
            case LocalFunctionStatement localFunction:
                WriteLocalFunctionStatement(localFunction);
                break;
        }
    }

    private void WriteLocalFunctionStatement(LocalFunctionStatement function)
    {
        WriteAttributes(function.Attributes);
        WriteModifierList(function.Modifiers);
        WriteTypeReference(function.ReturnType);
        Write($" {Id(function.Name)}");

        if (function.TypeParameters != null && function.TypeParameters.Count > 0)
        {
            Write("<");
            WriteList(function.TypeParameters, WriteTypeParameter);
            Write(">");
        }

        Write("(");
        WriteList(function.Parameters, WriteParameter);
        Write(")");

        if (function.Constraints != null)
        {
            foreach (var constraint in function.Constraints)
            {
                Write(" ");
                WriteTypeParameterConstraint(constraint);
            }
        }

        if (function.Body != null)
        {
            WriteLine();
            WriteBlockStatement(function.Body);
        }
        else if (function.ExpressionBody != null)
        {
            Write(" => ");
            WriteExpression(function.ExpressionBody);
            WriteLine(";");
        }
        else
        {
            WriteLine(";");
        }
    }

    private void WriteBlockStatement(BlockStatement block)
    {
        WriteNullableDirectives(block.NullableDirectives);
        WriteLine("{");
        Indent();

        if (block.Statements != null)
        {
            foreach (var stmt in block.Statements)
            {
                WriteStatement(stmt);
            }
        }

        WriteNullableDirectives(block.CloseBraceNullableDirectives);
        Unindent();
        WriteLine("}");
    }

    private void WriteLocalDeclarationStatement(LocalDeclarationStatement local)
    {
        if (local.IsAwait)
            Write("await ");
        if (local.IsUsing)
            Write("using ");
        if (local.IsConst)
            Write("const ");

        WriteTypeReference(local.Type);
        Write(" ");
        WriteList(local.Variables, WriteVariableDeclarator);
        WriteLine(";");
    }

    private void WriteIfStatement(IfStatement ifStmt)
    {
        Write("if (");
        WriteExpression(ifStmt.Condition);
        WriteLine(")");

        if (ifStmt.ThenStatement is BlockStatement)
        {
            WriteStatement(ifStmt.ThenStatement);
        }
        else
        {
            Indent();
            WriteStatement(ifStmt.ThenStatement);
            Unindent();
        }

        if (ifStmt.ElseStatement != null)
        {
            Write("else");

            if (ifStmt.ElseStatement is IfStatement)
            {
                Write(" ");
                WriteIfStatement((IfStatement)ifStmt.ElseStatement);
            }
            else
            {
                WriteLine();
                if (ifStmt.ElseStatement is BlockStatement)
                {
                    WriteStatement(ifStmt.ElseStatement);
                }
                else
                {
                    Indent();
                    WriteStatement(ifStmt.ElseStatement);
                    Unindent();
                }
            }
        }
    }

    private void WriteSwitchStatement(SwitchStatement switchStmt)
    {
        if (switchStmt.HasParentheses == false || (switchStmt.HasParentheses == null && switchStmt.Expression is TupleExpression))
        {
            // switch (a, b): the tuple's parentheses are the statement's
            Write("switch ");
            WriteExpression(switchStmt.Expression);
            WriteLine();
        }
        else
        {
            Write("switch (");
            WriteExpression(switchStmt.Expression);
            WriteLine(")");
        }
        WriteLine("{");
        Indent();

        if (switchStmt.Sections != null)
        {
            foreach (var section in switchStmt.Sections)
            {
                WriteSwitchSection(section);
            }
        }

        Unindent();
        WriteLine("}");
    }

    private void WriteSwitchSection(SwitchSection section)
    {
        foreach (var label in section.Labels)
        {
            WriteSwitchLabel(label);
        }

        Indent();
        foreach (var stmt in section.Statements)
        {
            WriteStatement(stmt);
        }
        Unindent();
    }

    private void WriteSwitchLabel(SwitchLabel label)
    {
        WriteNullableDirectives(label.NullableDirectives);
        switch (label)
        {
            case CaseSwitchLabel caseLabel:
                Write("case ");
                WritePattern(caseLabel.Pattern);
                if (caseLabel.Guard != null)
                {
                    Write(" when ");
                    WriteExpression(caseLabel.Guard);
                }
                WriteLine(":");
                break;
            case DefaultSwitchLabel:
                WriteLine("default:");
                break;
        }
    }

    private void WriteWhileStatement(WhileStatement whileStmt)
    {
        Write("while (");
        WriteExpression(whileStmt.Condition);
        WriteLine(")");
        WriteStatement(whileStmt.Body);
    }

    private void WriteDoStatement(DoStatement doStmt)
    {
        WriteLine("do");
        WriteStatement(doStmt.Body);
        Write("while (");
        WriteExpression(doStmt.Condition);
        WriteLine(");");
    }

    private void WriteForStatement(ForStatement forStmt)
    {
        Write("for (");

        if (forStmt.Initializers != null && forStmt.Initializers.Count > 0)
        {
            // For initializers, we need special handling to avoid multiple statements
            bool first = true;
            foreach (var init in forStmt.Initializers)
            {
                if (!first)
                    Write(", ");
                first = false;

                if (init is LocalDeclarationStatement local)
                {
                    WriteTypeReference(local.Type);
                    Write(" ");
                    WriteList(local.Variables, WriteVariableDeclarator);
                }
                else if (init is ExpressionStatement expr)
                {
                    WriteExpression(expr.Expression);
                }
            }
        }

        Write("; ");

        if (forStmt.Condition != null)
        {
            WriteExpression(forStmt.Condition);
        }

        Write("; ");

        if (forStmt.Iterators != null)
        {
            WriteList(forStmt.Iterators, WriteExpression);
        }

        WriteLine(")");
        WriteStatement(forStmt.Body);
    }

    private void WriteForEachStatement(ForEachStatement forEachStmt)
    {
        if (forEachStmt.IsAwait)
            Write("await ");
        Write("foreach (");
        if (forEachStmt.Variable != null)
        {
            WriteExpression(forEachStmt.Variable);
            Write(" in ");
        }
        else
        {
            WriteTypeReference(forEachStmt.Type);
            Write($" {Id(forEachStmt.Identifier)} in ");
        }
        WriteExpression(forEachStmt.Collection);
        WriteLine(")");
        WriteStatement(forEachStmt.Body);
    }

    private void WriteReturnStatement(ReturnStatement returnStmt)
    {
        Write("return");
        if (returnStmt.Expression != null)
        {
            Write(" ");
            WriteExpression(returnStmt.Expression);
        }
        WriteLine(";");
    }

    private void WriteThrowStatement(ThrowStatement throwStmt)
    {
        Write("throw");
        if (throwStmt.Expression != null)
        {
            Write(" ");
            WriteExpression(throwStmt.Expression);
        }
        WriteLine(";");
    }

    private void WriteTryStatement(TryStatement tryStmt)
    {
        WriteLine("try");
        WriteBlockStatement(tryStmt.Block);

        if (tryStmt.CatchClauses != null)
        {
            foreach (var catchClause in tryStmt.CatchClauses)
            {
                WriteCatchClause(catchClause);
            }
        }

        if (tryStmt.FinallyBlock != null)
        {
            WriteLine("finally");
            WriteBlockStatement(tryStmt.FinallyBlock);
        }
    }

    private void WriteCatchClause(CatchClause catchClause)
    {
        Write("catch");

        if (catchClause.ExceptionType != null)
        {
            Write(" (");
            WriteTypeReference(catchClause.ExceptionType);
            if (!string.IsNullOrEmpty(catchClause.Identifier))
            {
                Write($" {Id(catchClause.Identifier)}");
            }
            Write(")");
        }

        if (catchClause.Filter != null)
        {
            Write(" when (");
            WriteExpression(catchClause.Filter);
            Write(")");
        }

        WriteLine();
        WriteBlockStatement(catchClause.Block);
    }

    private void WriteUsingStatement(UsingStatement usingStmt)
    {
        if (usingStmt.IsAwait)
            Write("await ");
        Write("using (");

        if (usingStmt.ResourceAcquisition is LocalDeclarationStatement local)
        {
            WriteTypeReference(local.Type);
            Write(" ");
            WriteList(local.Variables, WriteVariableDeclarator);
        }
        else if (usingStmt.ResourceAcquisition is ExpressionStatement expr)
        {
            WriteExpression(expr.Expression);
        }

        WriteLine(")");
        WriteStatement(usingStmt.Body);
    }

    private void WriteLockStatement(LockStatement lockStmt)
    {
        Write("lock (");
        WriteExpression(lockStmt.Expression);
        WriteLine(")");
        WriteStatement(lockStmt.Body);
    }

    private void WriteLabeledStatement(LabeledStatement labeled)
    {
        WriteLine($"{Id(labeled.Label)}:");
        WriteStatement(labeled.Statement);
    }

    // ========================================
    // Expressions
    // ========================================

    private void WriteExpression(Expression expr)
    {
        EnsureSufficientStack();
        switch (expr)
        {
            case LiteralExpression lit:
                WriteLiteralExpression(lit);
                break;
            case InterpolatedStringExpression interpolated:
                WriteInterpolatedString(interpolated);
                break;
            case NameExpression name:
                WriteNameExpression(name);
                break;
            case BinaryExpression binary:
                WriteBinaryExpression(binary);
                break;
            case UnaryExpression unary:
                WriteUnaryExpression(unary);
                break;
            case ConditionalExpression cond:
                WriteConditionalExpression(cond);
                break;
            case InvocationExpression invocation:
                WriteInvocationExpression(invocation);
                break;
            case MemberAccessExpression memberAccess:
                WriteMemberAccessExpression(memberAccess);
                break;
            case ElementAccessExpression elementAccess:
                WriteElementAccessExpression(elementAccess);
                break;
            case ObjectCreationExpression objCreation:
                WriteObjectCreationExpression(objCreation);
                break;
            case ArrayCreationExpression arrayCreation:
                WriteArrayCreationExpression(arrayCreation);
                break;
            case CastExpression cast:
                WriteCastExpression(cast);
                break;
            case IsExpression isExpr:
                WriteIsExpression(isExpr);
                break;
            case AsExpression asExpr:
                WriteAsExpression(asExpr);
                break;
            case LambdaExpression lambda:
                WriteLambdaExpression(lambda);
                break;
            case QueryExpression query:
                WriteQueryExpression(query);
                break;
            case SwitchExpression switchExpr:
                WriteSwitchExpression(switchExpr);
                break;
            case ThrowExpression throwExpr:
                Write("throw ");
                WriteExpression(throwExpr.Expression);
                break;
            case DefaultExpression defaultExpr:
                WriteDefaultExpression(defaultExpr);
                break;
            case TypeOfExpression typeOfExpr:
                Write("typeof(");
                WriteTypeReference(typeOfExpr.Type);
                Write(")");
                break;
            case SizeOfExpression sizeOfExpr:
                Write("sizeof(");
                WriteTypeReference(sizeOfExpr.Type);
                Write(")");
                break;
            case NameOfExpression nameOfExpr:
                Write("nameof(");
                WriteExpression(nameOfExpr.Expression);
                Write(")");
                break;
            case AwaitExpression awaitExpr:
                Write("await ");
                WriteExpression(awaitExpr.Expression);
                break;
            case ParenthesizedExpression paren:
                Write("(");
                WriteExpression(paren.Expression);
                Write(")");
                break;
            case TupleExpression tuple:
                WriteTupleExpression(tuple);
                break;
            case RangeExpression range:
                WriteRangeExpression(range);
                break;
            case WithExpression withExpr:
                WriteWithExpression(withExpr);
                break;
            case AliasQualifiedNameExpression aliasQualified:
                Write($"{Id(aliasQualified.Alias)}::");
                WriteNameExpression(aliasQualified.Name);
                break;
            case ThisExpression:
                Write("this");
                break;
            case BaseExpression:
                Write("base");
                break;
            case PredefinedTypeExpression predefinedType:
                WriteTypeReference(new PredefinedTypeReference(predefinedType.Type));
                break;
            case ImplicitElementAccessExpression implicitElementAccess:
                Write("[");
                WriteList(implicitElementAccess.Arguments, WriteArgument);
                Write("]");
                break;
            case InitializerExpression initializer:
                WriteInitializerExpression(initializer);
                break;
            case AnonymousObjectCreationExpression anonymousObject:
                WriteAnonymousObjectCreationExpression(anonymousObject);
                break;
            case ImplicitArrayCreationExpression implicitArray:
                Write("new[");
                Write(new string(',', implicitArray.Rank - 1));
                Write("] ");
                WriteInitializerExpression(implicitArray.Initializer);
                break;
            case StackAllocExpression stackAlloc:
                WriteStackAllocExpression(stackAlloc);
                break;
            case CollectionExpression collection:
                Write("[");
                WriteList(collection.Elements, WriteExpression);
                if (collection.HasTrailingComma)
                    Write(",");
                Write("]");
                break;
            case SpreadElement spread:
                Write("..");
                WriteExpression(spread.Expression);
                break;
            case AnonymousMethodExpression anonymousMethod:
                WriteAnonymousMethodExpression(anonymousMethod);
                break;
            case CheckedExpression checkedExpr:
                Write(checkedExpr.IsChecked ? "checked(" : "unchecked(");
                WriteExpression(checkedExpr.Expression);
                Write(")");
                break;
            case RefExpression refExpr:
                Write("ref ");
                WriteExpression(refExpr.Expression);
                break;
            case DeclarationExpression declaration:
                WriteTypeReference(declaration.Type);
                Write(" ");
                WriteVariableDesignation(declaration.Designation);
                break;
            case ArgListExpression argList:
                Write("__arglist");
                if (argList.Arguments != null)
                {
                    Write("(");
                    WriteList(argList.Arguments, WriteArgument);
                    Write(")");
                }
                break;
            case MakeRefExpression makeRef:
                Write("__makeref(");
                WriteExpression(makeRef.Expression);
                Write(")");
                break;
            case RefTypeExpression refType:
                Write("__reftype(");
                WriteExpression(refType.Expression);
                Write(")");
                break;
            case RefValueExpression refValue:
                Write("__refvalue(");
                WriteExpression(refValue.Expression);
                Write(", ");
                WriteTypeReference(refValue.Type);
                Write(")");
                break;
        }
    }

    private void WriteVariableDesignation(VariableDesignation designation)
    {
        switch (designation)
        {
            case SingleVariableDesignation single:
                Write(Id(single.Identifier));
                break;
            case DiscardDesignation:
                Write("_");
                break;
            case ParenthesizedVariableDesignation parenthesized:
                Write("(");
                WriteList(parenthesized.Variables, WriteVariableDesignation);
                Write(")");
                break;
        }
    }

    private void WriteInitializerExpression(InitializerExpression initializer)
    {
        Write("{ ");
        WriteList(initializer.Expressions, WriteExpression);
        if (initializer.HasTrailingComma)
            Write(",");
        Write(" }");
    }

    private void WriteAnonymousObjectCreationExpression(AnonymousObjectCreationExpression anonymousObject)
    {
        Write("new { ");
        WriteList(anonymousObject.Members, member =>
        {
            if (member.Name != null)
            {
                Write($"{Id(member.Name)} = ");
            }
            WriteExpression(member.Expression);
        });
        if (anonymousObject.HasTrailingComma)
            Write(",");
        Write(" }");
    }

    private void WriteStackAllocExpression(StackAllocExpression stackAlloc)
    {
        Write("stackalloc");
        if (stackAlloc.ElementType != null)
        {
            Write(" ");
            WriteTypeReference(stackAlloc.ElementType);
        }
        Write("[");
        if (stackAlloc.Size != null)
        {
            WriteExpression(stackAlloc.Size);
        }
        Write("]");
        if (stackAlloc.Initializer != null)
        {
            Write(" ");
            WriteInitializerExpression(stackAlloc.Initializer);
        }
    }

    private void WriteAnonymousMethodExpression(AnonymousMethodExpression anonymousMethod)
    {
        WriteModifierList(anonymousMethod.Modifiers);
        Write("delegate");
        if (anonymousMethod.Parameters != null)
        {
            Write(" (");
            WriteList(anonymousMethod.Parameters, WriteParameter);
            Write(")");
        }
        WriteLine();
        WriteBlockStatement(anonymousMethod.Block);
    }

    /// <summary>
    /// An identifier as written in source: reserved keywords need the '@' prefix.
    /// </summary>
    private static string Id(string name) =>
        name != null && Lexer.ReservedKeywords.Contains(name) ? "@" + name : name;

    private void WriteInterpolatedString(InterpolatedStringExpression interpolated)
    {
        Write(interpolated.StartToken);
        foreach (var content in interpolated.Contents)
        {
            switch (content)
            {
                case InterpolatedStringText text:
                    Write(text.Text);
                    break;
                case Interpolation interpolation:
                    Write(new string('{', interpolated.BraceCount));
                    WriteExpression(interpolation.Expression);
                    if (interpolation.Alignment != null)
                    {
                        Write(",");
                        WriteExpression(interpolation.Alignment);
                    }
                    if (interpolation.Format != null)
                    {
                        Write(":");
                        Write(interpolation.Format);
                    }
                    Write(new string('}', interpolated.BraceCount));
                    break;
            }
        }
        Write(interpolated.EndToken);
    }

    private void WriteLiteralExpression(LiteralExpression lit)
    {
        // Literals parsed from source are written exactly as they were
        if (lit.Text != null)
        {
            Write(lit.Text);
            return;
        }

        switch (lit.Kind)
        {
            case LiteralKind.Null:
                Write("null");
                break;
            case LiteralKind.Boolean:
                Write((bool)lit.Value ? "true" : "false");
                break;
            case LiteralKind.Integer:
            case LiteralKind.Real:
                Write(FormatNumber(lit.Value));
                break;
            case LiteralKind.Character:
                Write($"'{EscapeChar((char)lit.Value)}'");
                break;
            case LiteralKind.String:
                Write($"\"{EscapeString((string)lit.Value)}\"");
                break;
            case LiteralKind.Utf8String:
                var utf8 = lit.Value is byte[] bytes ? Encoding.UTF8.GetString(bytes) : (string)lit.Value;
                Write($"\"{EscapeString(utf8)}\"u8");
                break;
        }
    }

    /// <summary>
    /// A numeric literal whose C# type matches the value's type.
    /// </summary>
    private static string FormatNumber(object value)
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        return value switch
        {
            uint u => u.ToString(invariant) + "U",
            long l => l.ToString(invariant) + "L",
            ulong ul => ul.ToString(invariant) + "UL",
            float f => f.ToString("R", invariant) + "F",
            double d => d.ToString("R", invariant) + "D",
            decimal m => m.ToString(invariant) + "M",
            IFormattable formattable => formattable.ToString(null, invariant),
            _ => value.ToString(),
        };
    }

    private string EscapeChar(char c)
    {
        return c switch
        {
            '\'' => "\\'",
            '\\' => "\\\\",
            '\0' => "\\0",
            '\a' => "\\a",
            '\b' => "\\b",
            '\f' => "\\f",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            '\v' => "\\v",
            _ => c.ToString()
        };
    }

    private string EscapeString(string s)
    {
        var result = new StringBuilder();
        foreach (char c in s)
        {
            result.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\0' => "\\0",
                '\a' => "\\a",
                '\b' => "\\b",
                '\f' => "\\f",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\v' => "\\v",
                _ => c.ToString()
            });
        }
        return result.ToString();
    }

    private void WriteNameExpression(NameExpression name)
    {
        if (name.Alias != null)
        {
            Write($"{Id(name.Alias)}::");
        }
        Write(string.Join(".", name.Parts.Select(Id)));
        if (name.TypeArguments != null && name.TypeArguments.Count > 0)
        {
            Write("<");
            WriteList(name.TypeArguments, WriteTypeReference);
            WriteNullableDirectives(name.CloseAngleNullableDirectives);
            Write(">");
        }
    }

    private void WriteBinaryExpression(BinaryExpression binary)
    {
        WriteExpression(binary.Left);
        Write($" {GetBinaryOperatorString(binary.Operator)} ");
        WriteExpression(binary.Right);
    }

    private string GetBinaryOperatorString(BinaryOperator op)
    {
        return op switch
        {
            BinaryOperator.Add => "+",
            BinaryOperator.Subtract => "-",
            BinaryOperator.Multiply => "*",
            BinaryOperator.Divide => "/",
            BinaryOperator.Modulo => "%",
            BinaryOperator.And => "&&",
            BinaryOperator.Or => "||",
            BinaryOperator.BitwiseAnd => "&",
            BinaryOperator.BitwiseOr => "|",
            BinaryOperator.BitwiseXor => "^",
            BinaryOperator.LeftShift => "<<",
            BinaryOperator.RightShift => ">>",
            BinaryOperator.UnsignedRightShift => ">>>",
            BinaryOperator.Equal => "==",
            BinaryOperator.NotEqual => "!=",
            BinaryOperator.LessThan => "<",
            BinaryOperator.LessThanOrEqual => "<=",
            BinaryOperator.GreaterThan => ">",
            BinaryOperator.GreaterThanOrEqual => ">=",
            BinaryOperator.Assign => "=",
            BinaryOperator.AddAssign => "+=",
            BinaryOperator.SubtractAssign => "-=",
            BinaryOperator.MultiplyAssign => "*=",
            BinaryOperator.DivideAssign => "/=",
            BinaryOperator.ModuloAssign => "%=",
            BinaryOperator.BitwiseAndAssign => "&=",
            BinaryOperator.BitwiseOrAssign => "|=",
            BinaryOperator.BitwiseXorAssign => "^=",
            BinaryOperator.LeftShiftAssign => "<<=",
            BinaryOperator.RightShiftAssign => ">>=",
            BinaryOperator.UnsignedRightShiftAssign => ">>>=",
            BinaryOperator.NullCoalescing => "??",
            BinaryOperator.NullCoalescingAssign => "??=",
            _ => throw new ArgumentException($"Unknown binary operator: {op}")
        };
    }

    private void WriteUnaryExpression(UnaryExpression unary)
    {
        if (unary.IsPrefix)
        {
            var op = GetUnaryOperatorString(unary.Operator);
            Write(op);

            // Keep "- -x" and "- --x" apart: "--x" and "---x" would lex differently
            if (unary.Operand is UnaryExpression { IsPrefix: true } inner
                && GetUnaryOperatorString(inner.Operator)[0] == op[^1]
                && op[^1] is '+' or '-' or '&')
            {
                Write(" ");
            }

            WriteExpression(unary.Operand);
        }
        else
        {
            WriteExpression(unary.Operand);
            Write(GetUnaryOperatorString(unary.Operator));
        }
    }

    private string GetUnaryOperatorString(UnaryOperator op)
    {
        return op switch
        {
            UnaryOperator.Plus => "+",
            UnaryOperator.Minus => "-",
            UnaryOperator.Not => "!",
            UnaryOperator.BitwiseNot => "~",
            UnaryOperator.Increment => "++",
            UnaryOperator.Decrement => "--",
            UnaryOperator.AddressOf => "&",
            UnaryOperator.Dereference => "*",
            UnaryOperator.Index => "^",
            UnaryOperator.NullForgiving => "!",
            _ => throw new ArgumentException($"Unknown unary operator: {op}")
        };
    }

    private void WriteConditionalExpression(ConditionalExpression cond)
    {
        WriteExpression(cond.Condition);
        Write(" ? ");
        WriteExpression(cond.TrueExpression);
        Write(" : ");
        WriteExpression(cond.FalseExpression);
    }

    private void WriteInvocationExpression(InvocationExpression invocation)
    {
        WriteExpression(invocation.Expression);
        Write("(");
        WriteList(invocation.Arguments, WriteArgument);
        Write(")");
    }

    private void WriteArgument(Argument arg)
    {
        if (!string.IsNullOrEmpty(arg.Name))
        {
            Write(arg.IsNameEquals ? $"{Id(arg.Name)} = " : $"{Id(arg.Name)}: ");
        }

        switch (arg.RefKind)
        {
            case RefKind.Ref:
                Write("ref ");
                break;
            case RefKind.Out:
                Write("out ");
                break;
            case RefKind.In:
                Write("in ");
                break;
        }

        WriteExpression(arg.Expression);
    }

    private void WriteMemberAccessExpression(MemberAccessExpression memberAccess)
    {
        if (memberAccess.Target != null)
        {
            WriteExpression(memberAccess.Target);
            Write(memberAccess.IsPointerAccess ? "->" : memberAccess.IsConditional ? "?." : ".");
        }
        Write(Id(memberAccess.MemberName));
        if (memberAccess.TypeArguments != null && memberAccess.TypeArguments.Count > 0)
        {
            Write("<");
            WriteList(memberAccess.TypeArguments, WriteTypeReference);
            WriteNullableDirectives(memberAccess.CloseAngleNullableDirectives);
            Write(">");
        }
    }

    private void WriteElementAccessExpression(ElementAccessExpression elementAccess)
    {
        WriteExpression(elementAccess.Target);
        Write(elementAccess.IsConditional ? "?[" : "[");
        WriteList(elementAccess.Arguments, WriteArgument);
        Write("]");
    }

    private void WriteObjectCreationExpression(ObjectCreationExpression objCreation)
    {
        Write("new");
        if (objCreation.Type != null)
        {
            Write(" ");
            WriteTypeReference(objCreation.Type);
        }

        // Without an initializer the argument list is required
        if (objCreation.Arguments != null || objCreation.Initializer == null)
        {
            Write("(");
            WriteList(objCreation.Arguments, WriteArgument);
            Write(")");
        }

        if (objCreation.Initializer != null)
        {
            Write(" ");
            WriteInitializerExpression(objCreation.Initializer);
        }
    }

    private void WriteArrayCreationExpression(ArrayCreationExpression arrayCreation)
    {
        Write("new ");
        WriteTypeReference(arrayCreation.ElementType);
        Write("[");
        if (arrayCreation.Sizes != null)
        {
            for (int i = 0; i < arrayCreation.Sizes.Count; i++)
            {
                if (i > 0)
                    Write(",");
                if (arrayCreation.Sizes[i] != null)
                    WriteExpression(arrayCreation.Sizes[i]);
            }
        }
        Write("]");

        if (arrayCreation.AdditionalRanks != null)
        {
            foreach (var rank in arrayCreation.AdditionalRanks)
            {
                Write("[" + new string(',', rank - 1) + "]");
            }
        }

        if (arrayCreation.Initializer != null)
        {
            Write(" ");
            WriteInitializerExpression(arrayCreation.Initializer);
        }
    }

    private void WriteCastExpression(CastExpression cast)
    {
        Write("(");
        WriteTypeReference(cast.Type);
        Write(")");
        WriteExpression(cast.Expression);
    }

    private void WriteIsExpression(IsExpression isExpr)
    {
        WriteExpression(isExpr.Expression);
        Write(" is ");
        WritePattern(isExpr.Pattern);
    }

    private void WriteAsExpression(AsExpression asExpr)
    {
        WriteExpression(asExpr.Expression);
        Write(" as ");
        WriteTypeReference(asExpr.Type);
    }

    private void WriteLambdaExpression(LambdaExpression lambda)
    {
        WriteAttributes(lambda.Attributes);
        WriteModifierList(lambda.Modifiers);

        if (lambda.ReturnType != null)
        {
            WriteTypeReference(lambda.ReturnType);
            Write(" ");
        }

        var isSimple = lambda.HasParenthesizedParameters == false
            || (lambda.HasParenthesizedParameters == null && lambda.ReturnType == null
                && lambda.Parameters is [{ Type: null, DefaultValue: null, Attributes: null, Modifiers.Count: 0 }]);

        if (lambda.Parameters == null || lambda.Parameters.Count == 0)
        {
            Write("()");
        }
        else if (isSimple)
        {
            Write(Id(lambda.Parameters[0].Name));
        }
        else
        {
            Write("(");
            WriteList(lambda.Parameters, WriteParameter);
            Write(")");
        }

        Write(" => ");

        switch (lambda.Body)
        {
            case ExpressionLambdaBody exprBody:
                WriteExpression(exprBody.Expression);
                break;
            case BlockLambdaBody blockBody:
                WriteBlockStatement(blockBody.Block);
                break;
        }
    }

    private void WriteQueryExpression(QueryExpression query)
    {
        WriteFromClause(query.FromClause);
        WriteQueryBody(query.BodyClauses, query.SelectOrGroupClause, query.Continuation);
    }

    private void WriteQueryBody(IReadOnlyList<QueryClause> clauses, SelectOrGroupClause selectOrGroup, QueryContinuation continuation)
    {
        if (clauses != null)
        {
            foreach (var clause in clauses)
            {
                WriteLine();
                WriteQueryClause(clause);
            }
        }

        WriteLine();
        WriteSelectOrGroupClause(selectOrGroup);

        if (continuation != null)
        {
            Write($" into {Id(continuation.Identifier)}");
            WriteQueryBody(continuation.BodyClauses, continuation.SelectOrGroupClause, continuation.Continuation);
        }
    }

    private void WriteFromClause(FromClause fromClause)
    {
        Write("from ");
        if (fromClause.Type != null)
        {
            WriteTypeReference(fromClause.Type);
            Write(" ");
        }
        Write($"{Id(fromClause.Identifier)} in ");
        WriteExpression(fromClause.Expression);
    }

    private void WriteQueryClause(QueryClause clause)
    {
        switch (clause)
        {
            case FromClause from:
                WriteFromClause(from);
                break;
            case JoinClause join:
                WriteJoinClause(join);
                break;
            case LetClause let:
                Write($"let {Id(let.Identifier)} = ");
                WriteExpression(let.Expression);
                break;
            case WhereClause where:
                Write("where ");
                WriteExpression(where.Condition);
                break;
            case OrderByClause orderBy:
                Write("orderby ");
                WriteList(orderBy.Orderings, WriteOrdering);
                break;
        }
    }

    private void WriteJoinClause(JoinClause join)
    {
        Write("join ");
        if (join.Type != null)
        {
            WriteTypeReference(join.Type);
            Write(" ");
        }
        Write($"{Id(join.Identifier)} in ");
        WriteExpression(join.InExpression);
        Write(" on ");
        WriteExpression(join.LeftExpression);
        Write(" equals ");
        WriteExpression(join.RightExpression);

        if (!string.IsNullOrEmpty(join.IntoIdentifier))
        {
            Write($" into {Id(join.IntoIdentifier)}");
        }
    }

    private void WriteOrdering(Ordering ordering)
    {
        WriteExpression(ordering.Expression);
        if (ordering.Direction == OrderDirection.Descending)
        {
            Write(" descending");
        }
        else if (ordering.HasExplicitDirection)
        {
            Write(" ascending");
        }
    }

    private void WriteSelectOrGroupClause(SelectOrGroupClause clause)
    {
        switch (clause)
        {
            case SelectClause select:
                Write("select ");
                WriteExpression(select.Expression);
                break;
            case GroupClause group:
                Write("group ");
                WriteExpression(group.GroupExpression);
                Write(" by ");
                WriteExpression(group.ByExpression);
                break;
        }
    }

    private void WriteSwitchExpression(SwitchExpression switchExpr)
    {
        WriteExpression(switchExpr.GoverningExpression);
        Write(" switch { ");
        WriteList(switchExpr.Arms, WriteSwitchExpressionArm);
        if (switchExpr.HasTrailingComma)
            Write(",");
        Write(" }");
    }

    private void WriteSwitchExpressionArm(SwitchExpressionArm arm)
    {
        WritePattern(arm.Pattern);
        if (arm.Guard != null)
        {
            Write(" when ");
            WriteExpression(arm.Guard);
        }
        Write(" => ");
        WriteExpression(arm.Expression);
    }

    private void WriteDefaultExpression(DefaultExpression defaultExpr)
    {
        if (defaultExpr.Type != null)
        {
            Write("default(");
            WriteTypeReference(defaultExpr.Type);
            Write(")");
        }
        else
        {
            Write("default");
        }
    }

    private void WriteTupleExpression(TupleExpression tuple)
    {
        Write("(");
        WriteList(tuple.Elements, WriteTupleExpressionElement);
        Write(")");
    }

    private void WriteTupleExpressionElement(TupleExpressionElement element)
    {
        if (!string.IsNullOrEmpty(element.Name))
        {
            Write($"{Id(element.Name)}: ");
        }
        WriteExpression(element.Expression);
    }

    private void WriteRangeExpression(RangeExpression range)
    {
        if (range.Start != null)
        {
            WriteExpression(range.Start);
        }
        Write("..");
        if (range.End != null)
        {
            WriteExpression(range.End);
        }
    }

    private void WriteWithExpression(WithExpression withExpr)
    {
        WriteExpression(withExpr.Expression);
        Write(" with ");
        WriteInitializerExpression(withExpr.Initializer);
    }

    // ========================================
    // Patterns
    // ========================================

    private void WritePattern(Pattern pattern)
    {
        EnsureSufficientStack();
        switch (pattern)
        {
            case TypePattern type:
                WriteTypeReference(type.Type);
                break;
            case ConstantPattern constant:
                WriteExpression(constant.Expression);
                break;
            case VarPattern varPattern:
                Write("var ");
                WriteVariableDesignation(varPattern.Designation);
                break;
            case ParenthesizedPattern parenthesized:
                Write("(");
                WritePattern(parenthesized.Pattern);
                Write(")");
                break;
            case ListPattern list:
                Write("[");
                WriteList(list.Patterns, WritePattern);
                if (list.HasTrailingComma)
                    Write(",");
                Write("]");
                if (!string.IsNullOrEmpty(list.Designation))
                {
                    Write($" {Id(list.Designation)}");
                }
                break;
            case SlicePattern slice:
                Write("..");
                if (slice.Pattern != null)
                {
                    Write(" ");
                    WritePattern(slice.Pattern);
                }
                break;
            case DiscardPattern:
                Write("_");
                break;
            case DeclarationPattern declaration:
                WriteTypeReference(declaration.Type);
                if (!string.IsNullOrEmpty(declaration.Identifier))
                {
                    Write($" {Id(declaration.Identifier)}");
                }
                break;
            case RecursivePattern recursive:
                WriteRecursivePattern(recursive);
                break;
            case RelationalPattern relational:
                Write(relational.Operator switch
                {
                    RelationalOperator.LessThan => "< ",
                    RelationalOperator.LessThanOrEqual => "<= ",
                    RelationalOperator.GreaterThan => "> ",
                    RelationalOperator.GreaterThanOrEqual => ">= ",
                    _ => throw new ArgumentException($"Unknown relational operator: {relational.Operator}")
                });
                WriteExpression(relational.Expression);
                break;
            case LogicalPattern logical:
                WriteLogicalPattern(logical);
                break;
        }
    }

    private void WriteRecursivePattern(RecursivePattern pattern)
    {
        if (pattern.Type != null)
        {
            WriteTypeReference(pattern.Type);
            Write(" ");
        }

        if (pattern.PositionalPatterns != null)
        {
            Write("(");
            WriteList(pattern.PositionalPatterns, p =>
            {
                if (p.Name != null)
                {
                    Write($"{Id(p.Name)}: ");
                }
                WritePattern(p.Pattern);
            });
            Write(")");
        }

        if (pattern.PropertyPatterns != null)
        {
            Write("{ ");
            WriteList(pattern.PropertyPatterns, WritePropertySubPattern);
            if (pattern.PropertyPatternsHaveTrailingComma)
                Write(",");
            Write(" }");
        }

        if (!string.IsNullOrEmpty(pattern.Designation))
        {
            Write($" {Id(pattern.Designation)}");
        }
    }

    private void WritePropertySubPattern(PropertySubPattern subPattern)
    {
        if (subPattern.PropertyName != null)
        {
            // An extended property pattern names a path: A.B.C
            Write(string.Join(".", subPattern.PropertyName.Split('.').Select(Id)));
            Write(": ");
        }
        WritePattern(subPattern.Pattern);
    }

    private void WriteLogicalPattern(LogicalPattern pattern)
    {
        switch (pattern.Kind)
        {
            case LogicalPatternKind.And:
                WritePattern(pattern.Left);
                Write(" and ");
                WritePattern(pattern.Right);
                break;
            case LogicalPatternKind.Or:
                WritePattern(pattern.Left);
                Write(" or ");
                WritePattern(pattern.Right);
                break;
            case LogicalPatternKind.Not:
                Write("not ");
                WritePattern(pattern.Left);
                break;
        }
    }

    // ========================================
    // Attributes
    // ========================================

    private void WriteAttributes(IReadOnlyList<AttributeSection> attributes)
    {
        if (attributes == null || attributes.Count == 0)
            return;

        foreach (var section in attributes)
        {
            WriteAttributeSection(section);
        }
    }

    private void WriteAttributeSection(AttributeSection section)
    {
        WriteNullableDirectives(section.NullableDirectives);
        Write("[");

        if (section.Target.HasValue)
        {
            Write(section.Target.Value switch
            {
                AttributeTarget.Assembly => "assembly",
                AttributeTarget.Module => "module",
                AttributeTarget.Field => "field",
                AttributeTarget.Event => "event",
                AttributeTarget.Method => "method",
                AttributeTarget.Param => "param",
                AttributeTarget.Property => "property",
                AttributeTarget.Return => "return",
                AttributeTarget.Type => "type",
                AttributeTarget.TypeVar => "typevar",
                _ => throw new ArgumentException($"Unknown attribute target: {section.Target}")
            });
            Write(": ");
        }

        WriteList(section.Attributes, WriteAttributeNode);
        WriteLine("]");
    }

    private void WriteAttributeNode(AttributeNode attr)
    {
        WriteNameExpression(attr.Name);
        if (attr.Arguments != null)
        {
            Write("(");
            WriteList(attr.Arguments, WriteArgument);
            Write(")");
        }
    }

    // ========================================
    // Modifiers
    // ========================================

    /// <summary>
    /// Modifiers in the given order, for nodes that keep the source order.
    /// </summary>
    private void WriteModifierList(IReadOnlyList<Modifiers> modifiers)
    {
        if (modifiers == null)
            return;

        foreach (var modifier in modifiers)
        {
            WriteModifiers(modifier);
        }
    }

    private void WriteModifiers(MemberDeclaration member)
    {
        if (member.ModifierList != null)
            WriteModifierList(member.ModifierList);
        else
            WriteModifiers(member.Modifiers);
    }

    private void WriteModifiers(Modifiers modifiers)
    {
        if (modifiers == Modifiers.None)
            return;

        if ((modifiers & Modifiers.New) != 0) Write("new ");
        if ((modifiers & Modifiers.Public) != 0) Write("public ");
        if ((modifiers & Modifiers.Protected) != 0) Write("protected ");
        if ((modifiers & Modifiers.Internal) != 0) Write("internal ");
        if ((modifiers & Modifiers.Private) != 0) Write("private ");
        if ((modifiers & Modifiers.File) != 0) Write("file ");
        if ((modifiers & Modifiers.Abstract) != 0) Write("abstract ");
        if ((modifiers & Modifiers.Sealed) != 0) Write("sealed ");
        if ((modifiers & Modifiers.Static) != 0) Write("static ");
        if ((modifiers & Modifiers.Readonly) != 0) Write("readonly ");
        if ((modifiers & Modifiers.Virtual) != 0) Write("virtual ");
        if ((modifiers & Modifiers.Override) != 0) Write("override ");
        if ((modifiers & Modifiers.Extern) != 0) Write("extern ");
        if ((modifiers & Modifiers.Unsafe) != 0) Write("unsafe ");
        if ((modifiers & Modifiers.Volatile) != 0) Write("volatile ");
        if ((modifiers & Modifiers.Async) != 0) Write("async ");
        if ((modifiers & Modifiers.Partial) != 0) Write("partial ");
        if ((modifiers & Modifiers.Const) != 0) Write("const ");
        if ((modifiers & Modifiers.Required) != 0) Write("required ");
        if ((modifiers & Modifiers.Ref) != 0) Write("ref ");
        if ((modifiers & Modifiers.Fixed) != 0) Write("fixed ");
        if ((modifiers & Modifiers.Safe) != 0) Write("safe ");
    }
}

