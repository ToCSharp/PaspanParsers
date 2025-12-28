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
    }

    // ========================================
    // Using Directives
    // ========================================

    private void WriteExternAliasDirective(ExternAliasDirective directive)
    {
        WriteLine($"extern alias {directive.Identifier};");
    }

    private void WriteUsingDirective(UsingDirective directive)
    {
        switch (directive)
        {
            case UsingNamespaceDirective ns:
                Write("using ");
                WriteNameExpression(ns.Namespace);
                WriteLine(";");
                break;
            case UsingAliasDirective alias:
                Write($"using {alias.Alias} = ");
                WriteNameExpression(alias.Target);
                WriteLine(";");
                break;
            case UsingStaticDirective staticDir:
                Write("using static ");
                WriteNameExpression(staticDir.Type);
                WriteLine(";");
                break;
        }
    }

    // ========================================
    // Member Declarations
    // ========================================

    private void WriteMemberDeclaration(MemberDeclaration member)
    {
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

            Unindent();
            WriteLine("}");
        }
    }

    private void WriteClassDeclaration(ClassDeclaration cls)
    {
        WriteAttributes(cls.Attributes);
        WriteModifiers(cls.Modifiers);
        Write($"class {cls.Name}");

        if (cls.TypeParameters != null && cls.TypeParameters.Count > 0)
        {
            Write("<");
            WriteList(cls.TypeParameters, WriteTypeParameter);
            Write(">");
        }

        if (cls.BaseTypes != null && cls.BaseTypes.Count > 0)
        {
            Write(" : ");
            WriteList(cls.BaseTypes, WriteTypeReference);
        }

        if (cls.Constraints != null && cls.Constraints.Count > 0)
        {
            foreach (var constraint in cls.Constraints)
            {
                WriteLine();
                Indent();
                WriteTypeParameterConstraint(constraint);
                Unindent();
            }
        }

        WriteLine();
        WriteLine("{");
        Indent();

        if (cls.Members != null)
        {
            for (int i = 0; i < cls.Members.Count; i++)
            {
                if (i > 0)
                    WriteLine();
                WriteMemberDeclaration(cls.Members[i]);
            }
        }

        Unindent();
        WriteLine("}");
    }

    private void WriteStructDeclaration(StructDeclaration str)
    {
        WriteAttributes(str.Attributes);
        WriteModifiers(str.Modifiers);
        Write($"struct {str.Name}");

        if (str.TypeParameters != null && str.TypeParameters.Count > 0)
        {
            Write("<");
            WriteList(str.TypeParameters, WriteTypeParameter);
            Write(">");
        }

        if (str.Interfaces != null && str.Interfaces.Count > 0)
        {
            Write(" : ");
            WriteList(str.Interfaces, WriteTypeReference);
        }

        if (str.Constraints != null && str.Constraints.Count > 0)
        {
            foreach (var constraint in str.Constraints)
            {
                WriteLine();
                Indent();
                WriteTypeParameterConstraint(constraint);
                Unindent();
            }
        }

        WriteLine();
        WriteLine("{");
        Indent();

        if (str.Members != null)
        {
            for (int i = 0; i < str.Members.Count; i++)
            {
                if (i > 0)
                    WriteLine();
                WriteMemberDeclaration(str.Members[i]);
            }
        }

        Unindent();
        WriteLine("}");
    }

    private void WriteInterfaceDeclaration(InterfaceDeclaration iface)
    {
        WriteAttributes(iface.Attributes);
        WriteModifiers(iface.Modifiers);
        Write($"interface {iface.Name}");

        if (iface.TypeParameters != null && iface.TypeParameters.Count > 0)
        {
            Write("<");
            WriteList(iface.TypeParameters, WriteTypeParameter);
            Write(">");
        }

        if (iface.BaseInterfaces != null && iface.BaseInterfaces.Count > 0)
        {
            Write(" : ");
            WriteList(iface.BaseInterfaces, WriteTypeReference);
        }

        if (iface.Constraints != null && iface.Constraints.Count > 0)
        {
            foreach (var constraint in iface.Constraints)
            {
                WriteLine();
                Indent();
                WriteTypeParameterConstraint(constraint);
                Unindent();
            }
        }

        WriteLine();
        WriteLine("{");
        Indent();

        if (iface.Members != null)
        {
            for (int i = 0; i < iface.Members.Count; i++)
            {
                if (i > 0)
                    WriteLine();
                WriteMemberDeclaration(iface.Members[i]);
            }
        }

        Unindent();
        WriteLine("}");
    }

    private void WriteEnumDeclaration(EnumDeclaration enm)
    {
        WriteAttributes(enm.Attributes);
        WriteModifiers(enm.Modifiers);
        Write($"enum {enm.Name}");

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
            WriteLine();
        }

        Unindent();
        WriteLine("}");
    }

    private void WriteEnumMember(EnumMember member)
    {
        WriteAttributes(member.Attributes);
        Write(member.Name);
        if (member.Value != null)
        {
            Write(" = ");
            WriteExpression(member.Value);
        }
    }

    private void WriteDelegateDeclaration(DelegateDeclaration del)
    {
        WriteAttributes(del.Attributes);
        WriteModifiers(del.Modifiers);
        Write("delegate ");
        WriteTypeReference(del.ReturnType);
        Write($" {del.Name}");

        if (del.TypeParameters != null && del.TypeParameters.Count > 0)
        {
            Write("<");
            WriteList(del.TypeParameters, WriteTypeParameter);
            Write(">");
        }

        Write("(");
        WriteList(del.Parameters, WriteParameter);
        Write(")");

        if (del.Constraints != null && del.Constraints.Count > 0)
        {
            foreach (var constraint in del.Constraints)
            {
                WriteLine();
                Indent();
                WriteTypeParameterConstraint(constraint);
                Unindent();
            }
        }

        WriteLine(";");
    }

    private void WriteRecordDeclaration(RecordDeclaration rec)
    {
        WriteAttributes(rec.Attributes);
        WriteModifiers(rec.Modifiers);
        Write(rec.IsRecordStruct ? "record struct " : "record ");
        Write(rec.Name);

        if (rec.TypeParameters != null && rec.TypeParameters.Count > 0)
        {
            Write("<");
            WriteList(rec.TypeParameters, WriteTypeParameter);
            Write(">");
        }

        if (rec.PrimaryConstructorParameters != null && rec.PrimaryConstructorParameters.Count > 0)
        {
            Write("(");
            WriteList(rec.PrimaryConstructorParameters, WriteParameter);
            Write(")");
        }

        if (rec.BaseTypes != null && rec.BaseTypes.Count > 0)
        {
            Write(" : ");
            WriteList(rec.BaseTypes, WriteTypeReference);
        }

        if (rec.Constraints != null && rec.Constraints.Count > 0)
        {
            foreach (var constraint in rec.Constraints)
            {
                WriteLine();
                Indent();
                WriteTypeParameterConstraint(constraint);
                Unindent();
            }
        }

        if (rec.Members == null || rec.Members.Count == 0)
        {
            WriteLine(";");
        }
        else
        {
            WriteLine();
            WriteLine("{");
            Indent();

            for (int i = 0; i < rec.Members.Count; i++)
            {
                if (i > 0)
                    WriteLine();
                WriteMemberDeclaration(rec.Members[i]);
            }

            Unindent();
            WriteLine("}");
        }
    }

    private void WriteFieldDeclaration(FieldDeclaration field)
    {
        WriteAttributes(field.Attributes);
        WriteModifiers(field.Modifiers);
        WriteTypeReference(field.Type);
        Write(" ");
        WriteList(field.Variables, WriteVariableDeclarator);
        WriteLine(";");
    }

    private void WriteVariableDeclarator(VariableDeclarator variable)
    {
        Write(variable.Name);
        if (variable.Initializer != null)
        {
            Write(" = ");
            WriteExpression(variable.Initializer);
        }
    }

    private void WriteMethodDeclaration(MethodDeclaration method)
    {
        WriteAttributes(method.Attributes);
        WriteModifiers(method.Modifiers);
        WriteTypeReference(method.ReturnType);
        Write($" {method.Name}");

        if (method.TypeParameters != null && method.TypeParameters.Count > 0)
        {
            Write("<");
            WriteList(method.TypeParameters, WriteTypeParameter);
            Write(">");
        }

        Write("(");
        WriteList(method.Parameters, WriteParameter);
        Write(")");

        if (method.Constraints != null && method.Constraints.Count > 0)
        {
            foreach (var constraint in method.Constraints)
            {
                WriteLine();
                Indent();
                WriteTypeParameterConstraint(constraint);
                Unindent();
            }
        }

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
        WriteModifiers(prop.Modifiers);
        WriteTypeReference(prop.Type);
        Write($" {prop.Name}");

        if (prop.ExpressionBody != null)
        {
            Write(" => ");
            WriteExpression(prop.ExpressionBody);
            WriteLine(";");
        }
        else if (prop.Accessors != null && prop.Accessors.Count > 0)
        {
            WriteLine();
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
        WriteAttributes(accessor.Attributes);
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
        WriteModifiers(indexer.Modifiers);
        WriteTypeReference(indexer.Type);
        Write(" this[");
        WriteList(indexer.Parameters, WriteParameter);
        Write("]");

        WriteLine();
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
        WriteModifiers(evt.Modifiers);
        Write("event ");
        WriteTypeReference(evt.Type);
        Write(" ");

        if (evt.Accessors != null && evt.Accessors.Count > 0)
        {
            // Event with accessors
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

        WriteLine();
        WriteBlockStatement(accessor.Block);
    }

    private void WriteConstructorDeclaration(ConstructorDeclaration ctor)
    {
        WriteAttributes(ctor.Attributes);
        WriteModifiers(ctor.Modifiers);
        Write($"{ctor.Name}(");
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
        Write(param.Name);
    }

    private void WriteTypeParameterConstraint(TypeParameterConstraint constraint)
    {
        Write($"where {constraint.TypeParameterName} : ");
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
        }
    }

    private void WriteParameter(Parameter param)
    {
        WriteAttributes(param.Attributes);

        switch (param.Modifier)
        {
            case ParameterModifier.This:
                Write("this ");
                break;
            case ParameterModifier.Ref:
                Write("ref ");
                break;
            case ParameterModifier.Out:
                Write("out ");
                break;
            case ParameterModifier.In:
                Write("in ");
                break;
            case ParameterModifier.Params:
                Write("params ");
                break;
        }

        WriteTypeReference(param.Type);
        Write($" {param.Name}");

        if (param.DefaultValue != null)
        {
            Write(" = ");
            WriteExpression(param.DefaultValue);
        }
    }

    // ========================================
    // Type References
    // ========================================

    private void WriteTypeReference(TypeReference type)
    {
        switch (type)
        {
            case NamedTypeReference named:
                WriteNameExpression(named.Name);
                if (named.TypeArguments != null && named.TypeArguments.Count > 0)
                {
                    Write("<");
                    WriteList(named.TypeArguments, WriteTypeReference);
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
        }
    }

    private void WriteTupleElement(TupleElement element)
    {
        WriteTypeReference(element.Type);
        if (!string.IsNullOrEmpty(element.Name))
        {
            Write($" {element.Name}");
        }
    }

    // ========================================
    // Statements
    // ========================================

    private void WriteStatement(Statement stmt)
    {
        switch (stmt)
        {
            case BlockStatement block:
                WriteBlockStatement(block);
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
                WriteLine($"goto {gotoStmt.Label};");
                break;
        }
    }

    private void WriteBlockStatement(BlockStatement block)
    {
        WriteLine("{");
        Indent();

        if (block.Statements != null)
        {
            foreach (var stmt in block.Statements)
            {
                WriteStatement(stmt);
            }
        }

        Unindent();
        WriteLine("}");
    }

    private void WriteLocalDeclarationStatement(LocalDeclarationStatement local)
    {
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
        Write("switch (");
        WriteExpression(switchStmt.Expression);
        WriteLine(")");
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
        Write("foreach");
        if (forEachStmt.IsAwait)
            Write(" await");
        Write(" (");
        WriteTypeReference(forEachStmt.Type);
        Write($" {forEachStmt.Identifier} in ");
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
                Write($" {catchClause.Identifier}");
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
        Write("using");
        if (usingStmt.IsAwait)
            Write(" await");
        Write(" (");

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
        WriteLine($"{labeled.Label}:");
        WriteStatement(labeled.Statement);
    }

    // ========================================
    // Expressions
    // ========================================

    private void WriteExpression(Expression expr)
    {
        switch (expr)
        {
            case LiteralExpression lit:
                WriteLiteralExpression(lit);
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
        }
    }

    private void WriteLiteralExpression(LiteralExpression lit)
    {
        switch (lit.Kind)
        {
            case LiteralKind.Null:
                Write("null");
                break;
            case LiteralKind.Boolean:
                Write((bool)lit.Value ? "true" : "false");
                break;
            case LiteralKind.Integer:
                Write(lit.Value.ToString());
                break;
            case LiteralKind.Real:
                Write(lit.Value.ToString());
                break;
            case LiteralKind.Character:
                Write($"'{EscapeChar((char)lit.Value)}'");
                break;
            case LiteralKind.String:
                Write($"\"{EscapeString((string)lit.Value)}\"");
                break;
        }
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
        Write(string.Join(".", name.Parts));
        if (name.TypeArguments != null && name.TypeArguments.Count > 0)
        {
            Write("<");
            WriteList(name.TypeArguments, WriteTypeReference);
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
            Write(GetUnaryOperatorString(unary.Operator));
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
            Write($"{arg.Name}: ");
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
            Write(memberAccess.IsConditional ? "?." : ".");
        }
        Write(memberAccess.MemberName);
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
        Write("new ");
        WriteTypeReference(objCreation.Type);
        Write("(");
        WriteList(objCreation.Arguments, WriteArgument);
        Write(")");

        if (objCreation.Initializer != null)
        {
            Write(" ");
            WriteObjectInitializer(objCreation.Initializer);
        }
    }

    private void WriteObjectInitializer(ObjectInitializer initializer)
    {
        Write("{ ");
        WriteList(initializer.Members, WriteMemberInitializer);
        Write(" }");
    }

    private void WriteMemberInitializer(MemberInitializer member)
    {
        Write($"{member.Name} = ");
        WriteExpression(member.Value);
    }

    private void WriteArrayCreationExpression(ArrayCreationExpression arrayCreation)
    {
        Write("new ");
        WriteTypeReference(arrayCreation.ElementType);
        Write("[");
        WriteList(arrayCreation.Sizes, WriteExpression);
        Write("]");

        if (arrayCreation.Initializer != null)
        {
            Write(" ");
            WriteArrayInitializer(arrayCreation.Initializer);
        }
    }

    private void WriteArrayInitializer(ArrayInitializer initializer)
    {
        Write("{ ");
        WriteList(initializer.Elements, WriteExpression);
        Write(" }");
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
        if (lambda.IsAsync)
            Write("async ");

        if (lambda.Parameters == null || lambda.Parameters.Count == 0)
        {
            Write("()");
        }
        else if (lambda.Parameters.Count == 1 && lambda.Parameters[0].Type == null)
        {
            Write(lambda.Parameters[0].Name);
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

        if (query.BodyClauses != null)
        {
            foreach (var clause in query.BodyClauses)
            {
                WriteLine();
                WriteQueryClause(clause);
            }
        }

        WriteLine();
        WriteSelectOrGroupClause(query.SelectOrGroupClause);
    }

    private void WriteFromClause(FromClause fromClause)
    {
        Write("from ");
        if (fromClause.Type != null)
        {
            WriteTypeReference(fromClause.Type);
            Write(" ");
        }
        Write($"{fromClause.Identifier} in ");
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
                Write($"let {let.Identifier} = ");
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
        Write($"{join.Identifier} in ");
        WriteExpression(join.InExpression);
        Write(" on ");
        WriteExpression(join.LeftExpression);
        Write(" equals ");
        WriteExpression(join.RightExpression);

        if (!string.IsNullOrEmpty(join.IntoIdentifier))
        {
            Write($" into {join.IntoIdentifier}");
        }
    }

    private void WriteOrdering(Ordering ordering)
    {
        WriteExpression(ordering.Expression);
        if (ordering.Direction == OrderDirection.Descending)
        {
            Write(" descending");
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
            Write($"{element.Name}: ");
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
        WriteObjectInitializer(withExpr.Initializer);
    }

    // ========================================
    // Patterns
    // ========================================

    private void WritePattern(Pattern pattern)
    {
        switch (pattern)
        {
            case TypePattern type:
                WriteTypeReference(type.Type);
                break;
            case ConstantPattern constant:
                WriteExpression(constant.Expression);
                break;
            case VarPattern varPattern:
                Write($"var {varPattern.Identifier}");
                break;
            case DiscardPattern:
                Write("_");
                break;
            case DeclarationPattern declaration:
                WriteTypeReference(declaration.Type);
                if (!string.IsNullOrEmpty(declaration.Identifier))
                {
                    Write($" {declaration.Identifier}");
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

        if (pattern.PositionalPatterns != null && pattern.PositionalPatterns.Count > 0)
        {
            Write("(");
            WriteList(pattern.PositionalPatterns, p => WritePattern(p.Pattern));
            Write(")");
        }

        if (pattern.PropertyPatterns != null && pattern.PropertyPatterns.Count > 0)
        {
            Write("{ ");
            WriteList(pattern.PropertyPatterns, WritePropertySubPattern);
            Write(" }");
        }

        if (!string.IsNullOrEmpty(pattern.Designation))
        {
            Write($" {pattern.Designation}");
        }
    }

    private void WritePropertySubPattern(PropertySubPattern subPattern)
    {
        Write($"{subPattern.PropertyName}: ");
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
        if (attr.Arguments != null && attr.Arguments.Count > 0)
        {
            Write("(");
            WriteList(attr.Arguments, WriteArgument);
            Write(")");
        }
    }

    // ========================================
    // Modifiers
    // ========================================

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
    }
}

