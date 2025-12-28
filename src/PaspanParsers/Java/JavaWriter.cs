using System.Text;

namespace PaspanParsers.Java;

/// <summary>
/// Writes Java code from AST nodes
/// </summary>
public class JavaWriter(string indentString = "    ")
{
    private readonly StringBuilder _builder = new();
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
        if (unit.PackageDeclaration != null)
        {
            WritePackageDeclaration(unit.PackageDeclaration);
            WriteLine();
        }

        if (unit.ImportDeclarations != null && unit.ImportDeclarations.Count > 0)
        {
            foreach (var import in unit.ImportDeclarations)
            {
                WriteImportDeclaration(import);
            }
            WriteLine();
        }

        if (unit.TypeDeclarations != null)
        {
            for (int i = 0; i < unit.TypeDeclarations.Count; i++)
            {
                if (i > 0)
                    WriteLine();
                WriteTypeDeclaration(unit.TypeDeclarations[i]);
            }
        }
    }

    // ========================================
    // Package and Imports
    // ========================================

    private void WritePackageDeclaration(PackageDeclaration pkg)
    {
        WriteAnnotations(pkg.Annotations);
        Write("package ");
        WriteQualifiedName(pkg.Name);
        WriteLine(";");
    }

    private void WriteImportDeclaration(ImportDeclaration import)
    {
        Write("import ");
        if (import.IsStatic)
            Write("static ");

        switch (import)
        {
            case SingleTypeImportDeclaration single:
                WriteQualifiedName(single.TypeName);
                break;
            case TypeImportOnDemandDeclaration onDemand:
                WriteQualifiedName(onDemand.PackageOrTypeName);
                Write(".*");
                break;
        }

        WriteLine(";");
    }

    private void WriteQualifiedName(QualifiedName name)
    {
        if (name == null || name.Parts == null) return;
        Write(string.Join(".", name.Parts));
    }

    // ========================================
    // Type Declarations
    // ========================================

    private void WriteTypeDeclaration(TypeDeclaration type)
    {
        switch (type)
        {
            case ClassDeclaration cls:
                WriteClassDeclaration(cls);
                break;
            case InterfaceDeclaration iface:
                WriteInterfaceDeclaration(iface);
                break;
            case EnumDeclaration enm:
                WriteEnumDeclaration(enm);
                break;
            case RecordDeclaration rec:
                WriteRecordDeclaration(rec);
                break;
            case AnnotationDeclaration ann:
                WriteAnnotationDeclaration(ann);
                break;
        }
    }

    private void WriteClassDeclaration(ClassDeclaration cls)
    {
        WriteAnnotations(cls.Annotations);
        WriteModifiers(cls.Modifiers);
        Write($"class {cls.Name}");

        if (cls.TypeParameters != null && cls.TypeParameters.Count > 0)
        {
            Write("<");
            WriteList(cls.TypeParameters, WriteTypeParameter);
            Write(">");
        }

        if (cls.SuperClass != null)
        {
            Write(" extends ");
            WriteTypeReference(cls.SuperClass);
        }

        if (cls.SuperInterfaces != null && cls.SuperInterfaces.Count > 0)
        {
            Write(" implements ");
            WriteList(cls.SuperInterfaces, WriteTypeReference);
        }

        if (cls.PermittedSubclasses != null && cls.PermittedSubclasses.Count > 0)
        {
            Write(" permits ");
            WriteList(cls.PermittedSubclasses, WriteTypeReference);
        }

        WriteLine(" {");
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

    private void WriteInterfaceDeclaration(InterfaceDeclaration iface)
    {
        WriteAnnotations(iface.Annotations);
        WriteModifiers(iface.Modifiers);
        Write($"interface {iface.Name}");

        if (iface.TypeParameters != null && iface.TypeParameters.Count > 0)
        {
            Write("<");
            WriteList(iface.TypeParameters, WriteTypeParameter);
            Write(">");
        }

        if (iface.ExtendsInterfaces != null && iface.ExtendsInterfaces.Count > 0)
        {
            Write(" extends ");
            WriteList(iface.ExtendsInterfaces, WriteTypeReference);
        }

        if (iface.PermittedSubtypes != null && iface.PermittedSubtypes.Count > 0)
        {
            Write(" permits ");
            WriteList(iface.PermittedSubtypes, WriteTypeReference);
        }

        WriteLine(" {");
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
        WriteAnnotations(enm.Annotations);
        WriteModifiers(enm.Modifiers);
        Write($"enum {enm.Name}");

        if (enm.SuperInterfaces != null && enm.SuperInterfaces.Count > 0)
        {
            Write(" implements ");
            WriteList(enm.SuperInterfaces, WriteTypeReference);
        }

        WriteLine(" {");
        Indent();

        if (enm.Constants != null && enm.Constants.Count > 0)
        {
            for (int i = 0; i < enm.Constants.Count; i++)
            {
                if (i > 0)
                    WriteLine(",");
                WriteEnumConstant(enm.Constants[i], i == enm.Constants.Count - 1);
            }

            if (enm.Members != null && enm.Members.Count > 0)
            {
                WriteLine(";");
                WriteLine();

                for (int i = 0; i < enm.Members.Count; i++)
                {
                    if (i > 0)
                        WriteLine();
                    WriteMemberDeclaration(enm.Members[i]);
                }
            }
            else
            {
                WriteLine();
            }
        }

        Unindent();
        WriteLine("}");
    }

    private void WriteEnumConstant(EnumConstant constant, bool isLast)
    {
        WriteAnnotations(constant.Annotations);
        Write(constant.Name);

        if (constant.Arguments != null && constant.Arguments.Count > 0)
        {
            Write("(");
            WriteList(constant.Arguments, WriteExpression);
            Write(")");
        }

        if (constant.Body != null && constant.Body.Count > 0)
        {
            WriteLine(" {");
            Indent();

            for (int i = 0; i < constant.Body.Count; i++)
            {
                if (i > 0)
                    WriteLine();
                WriteMemberDeclaration(constant.Body[i]);
            }

            Unindent();
            Write("}");
        }
    }

    private void WriteRecordDeclaration(RecordDeclaration rec)
    {
        WriteAnnotations(rec.Annotations);
        WriteModifiers(rec.Modifiers);
        Write($"record {rec.Name}");

        if (rec.TypeParameters != null && rec.TypeParameters.Count > 0)
        {
            Write("<");
            WriteList(rec.TypeParameters, WriteTypeParameter);
            Write(">");
        }

        Write("(");
        if (rec.Components != null && rec.Components.Count > 0)
        {
            WriteList(rec.Components, WriteRecordComponent);
        }
        Write(")");

        if (rec.SuperInterfaces != null && rec.SuperInterfaces.Count > 0)
        {
            Write(" implements ");
            WriteList(rec.SuperInterfaces, WriteTypeReference);
        }

        WriteLine(" {");
        Indent();

        if (rec.Members != null)
        {
            for (int i = 0; i < rec.Members.Count; i++)
            {
                if (i > 0)
                    WriteLine();
                WriteMemberDeclaration(rec.Members[i]);
            }
        }

        Unindent();
        WriteLine("}");
    }

    private void WriteRecordComponent(RecordComponent component)
    {
        WriteAnnotations(component.Annotations, inline: true);
        WriteTypeReference(component.Type);
        Write($" {component.Name}");
    }

    private void WriteAnnotationDeclaration(AnnotationDeclaration ann)
    {
        WriteAnnotations(ann.Annotations);
        WriteModifiers(ann.Modifiers);
        Write($"@interface {ann.Name}");

        WriteLine(" {");
        Indent();

        if (ann.Members != null)
        {
            for (int i = 0; i < ann.Members.Count; i++)
            {
                if (i > 0)
                    WriteLine();
                WriteAnnotationMemberDeclaration(ann.Members[i]);
            }
        }

        Unindent();
        WriteLine("}");
    }

    private void WriteAnnotationMemberDeclaration(AnnotationMemberDeclaration member)
    {
        WriteAnnotations(member.Annotations);
        WriteModifiers(member.Modifiers);
        WriteTypeReference(member.Type);
        Write($" {member.Name}()");

        if (member.DefaultValue != null)
        {
            Write(" default ");
            WriteExpression(member.DefaultValue);
        }

        WriteLine(";");
    }

    // ========================================
    // Type Parameters
    // ========================================

    private void WriteTypeParameter(TypeParameter param)
    {
        WriteAnnotations(param.Annotations, inline: true);
        Write(param.Name);

        if (param.Bounds != null && param.Bounds.Count > 0)
        {
            Write(" extends ");
            WriteList(param.Bounds, WriteTypeReference, " & ");
        }
    }

    // ========================================
    // Members
    // ========================================

    private void WriteMemberDeclaration(MemberDeclaration member)
    {
        switch (member)
        {
            case FieldDeclaration field:
                WriteFieldDeclaration(field);
                break;
            case MethodDeclaration method:
                WriteMethodDeclaration(method);
                break;
            case ConstructorDeclaration ctor:
                WriteConstructorDeclaration(ctor);
                break;
            case InitializerBlock init:
                WriteInitializerBlock(init);
                break;
        }
    }

    private void WriteFieldDeclaration(FieldDeclaration field)
    {
        WriteAnnotations(field.Annotations);
        WriteModifiers(field.Modifiers);
        WriteTypeReference(field.Type);
        Write(" ");
        WriteList(field.Variables, WriteVariableDeclarator);
        WriteLine(";");
    }

    private void WriteVariableDeclarator(VariableDeclarator var)
    {
        Write(var.Name);

        for (int i = 0; i < var.ArrayDimensions; i++)
        {
            Write("[]");
        }

        if (var.Initializer != null)
        {
            Write(" = ");
            WriteExpression(var.Initializer);
        }
    }

    private void WriteMethodDeclaration(MethodDeclaration method)
    {
        WriteAnnotations(method.Annotations);
        WriteModifiers(method.Modifiers);

        if (method.TypeParameters != null && method.TypeParameters.Count > 0)
        {
            Write("<");
            WriteList(method.TypeParameters, WriteTypeParameter);
            Write("> ");
        }

        WriteTypeReference(method.ReturnType);
        Write($" {method.Name}(");

        if (method.Parameters != null && method.Parameters.Count > 0)
        {
            WriteList(method.Parameters, WriteParameter);
        }

        Write(")");

        for (int i = 0; i < method.ArrayDimensions; i++)
        {
            Write("[]");
        }

        if (method.ThrowsClause != null && method.ThrowsClause.Count > 0)
        {
            Write(" throws ");
            WriteList(method.ThrowsClause, WriteTypeReference);
        }

        if (method.Body != null)
        {
            WriteLine(" {");
            Indent();
            WriteBlockContents(method.Body);
            Unindent();
            WriteLine("}");
        }
        else
        {
            WriteLine(";");
        }
    }

    private void WriteConstructorDeclaration(ConstructorDeclaration ctor)
    {
        WriteAnnotations(ctor.Annotations);
        WriteModifiers(ctor.Modifiers);

        if (ctor.TypeParameters != null && ctor.TypeParameters.Count > 0)
        {
            Write("<");
            WriteList(ctor.TypeParameters, WriteTypeParameter);
            Write("> ");
        }

        Write($"{ctor.Name}(");

        if (ctor.Parameters != null && ctor.Parameters.Count > 0)
        {
            WriteList(ctor.Parameters, WriteParameter);
        }

        Write(")");

        if (ctor.ThrowsClause != null && ctor.ThrowsClause.Count > 0)
        {
            Write(" throws ");
            WriteList(ctor.ThrowsClause, WriteTypeReference);
        }

        if (ctor.Body != null)
        {
            WriteLine(" {");
            Indent();

            if (ctor.ExplicitConstructorInvocation != null)
            {
                WriteConstructorInvocation(ctor.ExplicitConstructorInvocation);
            }

            WriteBlockContents(ctor.Body);
            Unindent();
            WriteLine("}");
        }
        else
        {
            WriteLine(";");
        }
    }

    private void WriteConstructorInvocation(ConstructorInvocation inv)
    {
        if (inv.TypeArguments != null && inv.TypeArguments.Count > 0)
        {
            Write("<");
            WriteList(inv.TypeArguments, WriteTypeReference);
            Write(">");
        }

        Write(inv.IsSuper ? "super" : "this");
        Write("(");

        if (inv.Arguments != null && inv.Arguments.Count > 0)
        {
            WriteList(inv.Arguments, WriteExpression);
        }

        WriteLine(");");
    }

    private void WriteInitializerBlock(InitializerBlock init)
    {
        if (init.IsStatic)
            Write("static ");

        WriteLine("{");
        Indent();
        WriteBlockContents(init.Block);
        Unindent();
        WriteLine("}");
    }

    private void WriteParameter(Parameter param)
    {
        WriteAnnotations(param.Annotations, inline: true);

        if (param.IsFinal)
            Write("final ");

        WriteTypeReference(param.Type);

        if (param.IsVarArgs)
        {
            Write("...");
        }
        else
        {
            for (int i = 0; i < param.ArrayDimensions; i++)
            {
                Write("[]");
            }
        }

        Write($" {param.Name}");
    }

    // ========================================
    // Type References
    // ========================================

    private void WriteTypeReference(TypeReference type)
    {
        switch (type)
        {
            case PrimitiveTypeReference primitive:
                WritePrimitiveType(primitive);
                break;
            case ReferenceTypeReference reference:
                WriteReferenceType(reference);
                break;
            case ArrayTypeReference array:
                WriteArrayType(array);
                break;
            case VarTypeReference:
                Write("var");
                break;
        }
    }

    private void WritePrimitiveType(PrimitiveTypeReference primitive)
    {
        WriteAnnotations(primitive.Annotations, inline: true);

        Write(primitive.Type switch
        {
            PrimitiveType.Boolean => "boolean",
            PrimitiveType.Byte => "byte",
            PrimitiveType.Short => "short",
            PrimitiveType.Int => "int",
            PrimitiveType.Long => "long",
            PrimitiveType.Char => "char",
            PrimitiveType.Float => "float",
            PrimitiveType.Double => "double",
            _ => "unknown"
        });
    }

    private void WriteReferenceType(ReferenceTypeReference reference)
    {
        WriteAnnotations(reference.Annotations, inline: true);
        WriteQualifiedName(reference.Name);

        if (reference.TypeArguments != null && reference.TypeArguments.Count > 0)
        {
            Write("<");
            WriteList(reference.TypeArguments, WriteTypeArgument);
            Write(">");
        }
    }

    private void WriteArrayType(ArrayTypeReference array)
    {
        WriteTypeReference(array.ElementType);
        Write("[]");
    }

    private void WriteTypeArgument(TypeArgument arg)
    {
        switch (arg)
        {
            case ReferenceTypeArgument refArg:
                WriteTypeReference(refArg.Type);
                break;
            case WildcardTypeArgument wildcard:
                WriteWildcard(wildcard);
                break;
        }
    }

    private void WriteWildcard(WildcardTypeArgument wildcard)
    {
        WriteAnnotations(wildcard.Annotations, inline: true);
        Write("?");

        if (wildcard.Bounds != null)
        {
            switch (wildcard.Bounds)
            {
                case ExtendsWildcardBounds extends:
                    Write(" extends ");
                    WriteTypeReference(extends.Type);
                    break;
                case SuperWildcardBounds super:
                    Write(" super ");
                    WriteTypeReference(super.Type);
                    break;
            }
        }
    }

    // ========================================
    // Modifiers
    // ========================================

    private void WriteModifiers(Modifiers mods)
    {
        if (mods == Modifiers.None) return;

        if (mods.HasFlag(Modifiers.Public)) Write("public ");
        if (mods.HasFlag(Modifiers.Protected)) Write("protected ");
        if (mods.HasFlag(Modifiers.Private)) Write("private ");
        if (mods.HasFlag(Modifiers.Static)) Write("static ");
        if (mods.HasFlag(Modifiers.Final)) Write("final ");
        if (mods.HasFlag(Modifiers.Abstract)) Write("abstract ");
        if (mods.HasFlag(Modifiers.Synchronized)) Write("synchronized ");
        if (mods.HasFlag(Modifiers.Volatile)) Write("volatile ");
        if (mods.HasFlag(Modifiers.Transient)) Write("transient ");
        if (mods.HasFlag(Modifiers.Native)) Write("native ");
        if (mods.HasFlag(Modifiers.Strictfp)) Write("strictfp ");
        if (mods.HasFlag(Modifiers.Default)) Write("default ");
        if (mods.HasFlag(Modifiers.Sealed)) Write("sealed ");
        if (mods.HasFlag(Modifiers.NonSealed)) Write("non-sealed ");
    }

    // ========================================
    // Annotations
    // ========================================

    private void WriteAnnotations(IReadOnlyList<Annotation> annotations, bool inline = false)
    {
        if (annotations == null || annotations.Count == 0) return;

        foreach (var annotation in annotations)
        {
            WriteAnnotation(annotation);
            if (inline)
                Write(" ");
            else
                WriteLine();
        }
    }

    private void WriteAnnotation(Annotation annotation)
    {
        Write("@");
        WriteQualifiedName(annotation.Name);

        if (annotation.Elements != null && annotation.Elements.Count > 0)
        {
            Write("(");

            if (annotation.Elements.Count == 1 && annotation.Elements[0].Name == null)
            {
                // Single-element annotation
                WriteExpression(annotation.Elements[0].Value);
            }
            else
            {
                // Normal annotation
                WriteList(annotation.Elements, WriteAnnotationElement);
            }

            Write(")");
        }
    }

    private void WriteAnnotationElement(AnnotationElement element)
    {
        if (element.Name != null)
        {
            Write($"{element.Name} = ");
        }
        WriteExpression(element.Value);
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
            case EmptyStatement:
                WriteLine(";");
                break;
            case ExpressionStatement expr:
                WriteExpression(expr.Expression);
                WriteLine(";");
                break;
            case LocalVariableDeclarationStatement localVar:
                WriteLocalVariableDeclaration(localVar);
                break;
            case IfStatement ifStmt:
                WriteIfStatement(ifStmt);
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
            case EnhancedForStatement enhancedFor:
                WriteEnhancedForStatement(enhancedFor);
                break;
            case BreakStatement breakStmt:
                WriteBreakStatement(breakStmt);
                break;
            case ContinueStatement continueStmt:
                WriteContinueStatement(continueStmt);
                break;
            case ReturnStatement returnStmt:
                WriteReturnStatement(returnStmt);
                break;
            case ThrowStatement throwStmt:
                WriteThrowStatement(throwStmt);
                break;
            case SynchronizedStatement syncStmt:
                WriteSynchronizedStatement(syncStmt);
                break;
            case TryStatement tryStmt:
                WriteTryStatement(tryStmt);
                break;
            case LabeledStatement labeled:
                WriteLabeledStatement(labeled);
                break;
            case YieldStatement yieldStmt:
                WriteYieldStatement(yieldStmt);
                break;
            case AssertStatement assertStmt:
                WriteAssertStatement(assertStmt);
                break;
            case SwitchStatement switchStmt:
                WriteSwitchStatement(switchStmt);
                break;
        }
    }

    private void WriteBlockStatement(BlockStatement block)
    {
        WriteLine("{");
        Indent();
        WriteBlockContents(block);
        Unindent();
        WriteLine("}");
    }

    private void WriteBlockContents(BlockStatement block)
    {
        if (block.Statements != null)
        {
            foreach (var stmt in block.Statements)
            {
                WriteStatement(stmt);
            }
        }
    }

    private void WriteLocalVariableDeclaration(LocalVariableDeclarationStatement localVar)
    {
        WriteAnnotations(localVar.Annotations, inline: true);

        if (localVar.IsFinal)
            Write("final ");

        WriteTypeReference(localVar.Type);
        Write(" ");
        WriteList(localVar.Variables, WriteVariableDeclarator);
        WriteLine(";");
    }

    private void WriteIfStatement(IfStatement ifStmt)
    {
        Write("if (");
        WriteExpression(ifStmt.Condition);
        Write(") ");

        if (ifStmt.ThenStatement is BlockStatement)
        {
            WriteStatement(ifStmt.ThenStatement);
        }
        else
        {
            WriteLine();
            Indent();
            WriteStatement(ifStmt.ThenStatement);
            Unindent();
        }

        if (ifStmt.ElseStatement != null)
        {
            Write("else ");

            if (ifStmt.ElseStatement is BlockStatement || ifStmt.ElseStatement is IfStatement)
            {
                WriteStatement(ifStmt.ElseStatement);
            }
            else
            {
                WriteLine();
                Indent();
                WriteStatement(ifStmt.ElseStatement);
                Unindent();
            }
        }
    }

    private void WriteWhileStatement(WhileStatement whileStmt)
    {
        Write("while (");
        WriteExpression(whileStmt.Condition);
        Write(") ");
        WriteStatement(whileStmt.Body);
    }

    private void WriteDoStatement(DoStatement doStmt)
    {
        Write("do ");
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
            // Handle initializer (could be local variable declaration or expressions)
            foreach (var init in forStmt.Initializers)
            {
                if (init is LocalVariableDeclarationStatement localVar)
                {
                    if (localVar.IsFinal)
                        Write("final ");
                    WriteTypeReference(localVar.Type);
                    Write(" ");
                    WriteList(localVar.Variables, WriteVariableDeclarator);
                }
                else if (init is ExpressionStatement exprStmt)
                {
                    WriteExpression(exprStmt.Expression);
                }
            }
        }

        Write("; ");

        if (forStmt.Condition != null)
        {
            WriteExpression(forStmt.Condition);
        }

        Write("; ");

        if (forStmt.Updates != null && forStmt.Updates.Count > 0)
        {
            WriteList(forStmt.Updates, WriteExpression);
        }

        Write(") ");
        WriteStatement(forStmt.Body);
    }

    private void WriteEnhancedForStatement(EnhancedForStatement enhancedFor)
    {
        Write("for (");

        WriteAnnotations(enhancedFor.Annotations, inline: true);

        if (enhancedFor.IsFinal)
            Write("final ");

        WriteTypeReference(enhancedFor.Type);
        Write($" {enhancedFor.Identifier} : ");
        WriteExpression(enhancedFor.Expression);
        Write(") ");
        WriteStatement(enhancedFor.Body);
    }

    private void WriteBreakStatement(BreakStatement breakStmt)
    {
        Write("break");
        if (breakStmt.Label != null)
            Write($" {breakStmt.Label}");
        WriteLine(";");
    }

    private void WriteContinueStatement(ContinueStatement continueStmt)
    {
        Write("continue");
        if (continueStmt.Label != null)
            Write($" {continueStmt.Label}");
        WriteLine(";");
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
        Write("throw ");
        WriteExpression(throwStmt.Expression);
        WriteLine(";");
    }

    private void WriteSynchronizedStatement(SynchronizedStatement syncStmt)
    {
        Write("synchronized (");
        WriteExpression(syncStmt.Expression);
        Write(") ");
        WriteBlockStatement(syncStmt.Block);
    }

    private void WriteTryStatement(TryStatement tryStmt)
    {
        Write("try");

        if (tryStmt.Resources != null && tryStmt.Resources.Count > 0)
        {
            Write(" (");
            WriteList(tryStmt.Resources, WriteResource, "; ");
            Write(") ");
        }
        else
        {
            Write(" ");
        }

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
            Write("finally ");
            WriteBlockStatement(tryStmt.FinallyBlock);
        }
    }

    private void WriteResource(Resource resource)
    {
        WriteAnnotations(resource.Annotations, inline: true);

        if (resource.IsFinal)
            Write("final ");

        WriteTypeReference(resource.Type);
        Write($" {resource.Name} = ");
        WriteExpression(resource.Initializer);
    }

    private void WriteCatchClause(CatchClause catchClause)
    {
        Write("catch (");

        WriteAnnotations(catchClause.Annotations, inline: true);

        if (catchClause.IsFinal)
            Write("final ");

        if (catchClause.ExceptionTypes != null && catchClause.ExceptionTypes.Count > 0)
        {
            WriteList(catchClause.ExceptionTypes, WriteTypeReference, " | ");
        }

        Write($" {catchClause.Identifier}) ");
        WriteBlockStatement(catchClause.Block);
    }

    private void WriteLabeledStatement(LabeledStatement labeled)
    {
        WriteLine($"{labeled.Label}:");
        WriteStatement(labeled.Statement);
    }

    private void WriteYieldStatement(YieldStatement yieldStmt)
    {
        Write("yield ");
        WriteExpression(yieldStmt.Expression);
        WriteLine(";");
    }

    private void WriteAssertStatement(AssertStatement assertStmt)
    {
        Write("assert ");
        WriteExpression(assertStmt.Condition);

        if (assertStmt.Message != null)
        {
            Write(" : ");
            WriteExpression(assertStmt.Message);
        }

        WriteLine(";");
    }

    private void WriteSwitchStatement(SwitchStatement switchStmt)
    {
        Write("switch (");
        WriteExpression(switchStmt.Expression);
        WriteLine(") {");
        Indent();

        if (switchStmt.BlockGroups != null)
        {
            foreach (var group in switchStmt.BlockGroups)
            {
                WriteSwitchBlockGroup(group);
            }
        }

        Unindent();
        WriteLine("}");
    }

    private void WriteSwitchBlockGroup(SwitchBlockStatementGroup group)
    {
        if (group.Labels != null)
        {
            foreach (var label in group.Labels)
            {
                WriteSwitchLabel(label);
            }
        }

        if (group.Statements != null)
        {
            Indent();
            foreach (var stmt in group.Statements)
            {
                WriteStatement(stmt);
            }
            Unindent();
        }
    }

    private void WriteSwitchLabel(SwitchLabel label)
    {
        switch (label)
        {
            case CaseSwitchLabel caseLabel:
                Write("case ");
                WriteExpression(caseLabel.Expression);
                WriteLine(":");
                break;
            case DefaultSwitchLabel:
                WriteLine("default:");
                break;
        }
    }

    // ========================================
    // Expressions
    // ========================================

    private void WriteExpression(Expression expr)
    {
        switch (expr)
        {
            case LiteralExpression literal:
                WriteLiteral(literal);
                break;
            case ThisExpression thisExpr:
                WriteThisExpression(thisExpr);
                break;
            case NameExpression name:
                WriteQualifiedName(name.Name);
                break;
            case ParenthesizedExpression paren:
                Write("(");
                WriteExpression(paren.Expression);
                Write(")");
                break;
            case BinaryExpression binary:
                WriteBinaryExpression(binary);
                break;
            case UnaryExpression unary:
                WriteUnaryExpression(unary);
                break;
            case TernaryExpression ternary:
                WriteTernaryExpression(ternary);
                break;
            case MethodInvocationExpression invocation:
                WriteMethodInvocation(invocation);
                break;
            case FieldAccessExpression fieldAccess:
                WriteFieldAccess(fieldAccess);
                break;
            case ArrayAccessExpression arrayAccess:
                WriteArrayAccess(arrayAccess);
                break;
            case NewObjectExpression newObject:
                WriteNewObject(newObject);
                break;
            case NewArrayExpression newArray:
                WriteNewArray(newArray);
                break;
            case CastExpression cast:
                WriteCast(cast);
                break;
            case InstanceOfExpression instanceof:
                WriteInstanceOf(instanceof);
                break;
            case ClassLiteralExpression classLiteral:
                WriteClassLiteral(classLiteral);
                break;
            case ArrayInitializerExpression arrayInit:
                WriteArrayInitializer(arrayInit);
                break;
        }
    }

    private void WriteLiteral(LiteralExpression literal)
    {
        switch (literal.Kind)
        {
            case LiteralKind.Integer:
                Write(literal.Value?.ToString() ?? "0");
                break;
            case LiteralKind.FloatingPoint:
                Write(literal.Value?.ToString() ?? "0.0");
                break;
            case LiteralKind.Boolean:
                Write((bool)(literal.Value ?? false) ? "true" : "false");
                break;
            case LiteralKind.Character:
                Write($"'{literal.Value}'");
                break;
            case LiteralKind.String:
                Write($"\"{literal.Value}\"");
                break;
            case LiteralKind.TextBlock:
                Write($"\"\"\"{literal.Value}\"\"\"");
                break;
            case LiteralKind.Null:
                Write("null");
                break;
        }
    }

    private void WriteThisExpression(ThisExpression thisExpr)
    {
        if (thisExpr.Qualifier != null)
        {
            WriteQualifiedName(thisExpr.Qualifier);
            Write(".");
        }
        Write("this");
    }

    private void WriteBinaryExpression(BinaryExpression binary)
    {
        WriteExpression(binary.Left);
        Write(" ");
        WriteOperator(binary.Operator);
        Write(" ");
        WriteExpression(binary.Right);
    }

    private void WriteOperator(BinaryOperator op)
    {
        Write(op switch
        {
            BinaryOperator.Multiply => "*",
            BinaryOperator.Divide => "/",
            BinaryOperator.Modulo => "%",
            BinaryOperator.Add => "+",
            BinaryOperator.Subtract => "-",
            BinaryOperator.LeftShift => "<<",
            BinaryOperator.RightShift => ">>",
            BinaryOperator.UnsignedRightShift => ">>>",
            BinaryOperator.LessThan => "<",
            BinaryOperator.GreaterThan => ">",
            BinaryOperator.LessThanOrEqual => "<=",
            BinaryOperator.GreaterThanOrEqual => ">=",
            BinaryOperator.Equal => "==",
            BinaryOperator.NotEqual => "!=",
            BinaryOperator.BitwiseAnd => "&",
            BinaryOperator.BitwiseXor => "^",
            BinaryOperator.BitwiseOr => "|",
            BinaryOperator.ConditionalAnd => "&&",
            BinaryOperator.ConditionalOr => "||",
            BinaryOperator.Assign => "=",
            BinaryOperator.AddAssign => "+=",
            BinaryOperator.SubtractAssign => "-=",
            BinaryOperator.MultiplyAssign => "*=",
            BinaryOperator.DivideAssign => "/=",
            BinaryOperator.ModuloAssign => "%=",
            BinaryOperator.LeftShiftAssign => "<<=",
            BinaryOperator.RightShiftAssign => ">>=",
            BinaryOperator.UnsignedRightShiftAssign => ">>>=",
            BinaryOperator.BitwiseAndAssign => "&=",
            BinaryOperator.BitwiseXorAssign => "^=",
            BinaryOperator.BitwiseOrAssign => "|=",
            _ => "?"
        });
    }

    private void WriteUnaryExpression(UnaryExpression unary)
    {
        if (unary.IsPrefix)
        {
            WriteUnaryOperator(unary.Operator);
            WriteExpression(unary.Operand);
        }
        else
        {
            WriteExpression(unary.Operand);
            WriteUnaryOperator(unary.Operator);
        }
    }

    private void WriteUnaryOperator(UnaryOperator op)
    {
        Write(op switch
        {
            UnaryOperator.Plus => "+",
            UnaryOperator.Minus => "-",
            UnaryOperator.BitwiseComplement => "~",
            UnaryOperator.LogicalComplement => "!",
            UnaryOperator.PreIncrement => "++",
            UnaryOperator.PreDecrement => "--",
            UnaryOperator.PostIncrement => "++",
            UnaryOperator.PostDecrement => "--",
            _ => "?"
        });
    }

    private void WriteTernaryExpression(TernaryExpression ternary)
    {
        WriteExpression(ternary.Condition);
        Write(" ? ");
        WriteExpression(ternary.TrueExpression);
        Write(" : ");
        WriteExpression(ternary.FalseExpression);
    }

    private void WriteMethodInvocation(MethodInvocationExpression invocation)
    {
        if (invocation.Target != null)
        {
            WriteExpression(invocation.Target);
            Write(".");
        }

        if (invocation.TypeArguments != null && invocation.TypeArguments.Count > 0)
        {
            Write("<");
            WriteList(invocation.TypeArguments, WriteTypeArgument);
            Write(">");
        }

        Write(invocation.MethodName);
        Write("(");

        if (invocation.Arguments != null && invocation.Arguments.Count > 0)
        {
            WriteList(invocation.Arguments, WriteExpression);
        }

        Write(")");
    }

    private void WriteFieldAccess(FieldAccessExpression fieldAccess)
    {
        WriteExpression(fieldAccess.Target);
        Write($".{fieldAccess.FieldName}");
    }

    private void WriteArrayAccess(ArrayAccessExpression arrayAccess)
    {
        WriteExpression(arrayAccess.Array);
        Write("[");
        WriteExpression(arrayAccess.Index);
        Write("]");
    }

    private void WriteNewObject(NewObjectExpression newObject)
    {
        if (newObject.Qualifier != null)
        {
            WriteExpression(newObject.Qualifier);
            Write(".");
        }

        Write("new ");

        if (newObject.TypeArguments != null && newObject.TypeArguments.Count > 0)
        {
            Write("<");
            WriteList(newObject.TypeArguments, WriteTypeArgument);
            Write("> ");
        }

        WriteTypeReference(newObject.Type);
        Write("(");

        if (newObject.Arguments != null && newObject.Arguments.Count > 0)
        {
            WriteList(newObject.Arguments, WriteExpression);
        }

        Write(")");

        if (newObject.AnonymousClassBody != null && newObject.AnonymousClassBody.Count > 0)
        {
            WriteLine(" {");
            Indent();

            for (int i = 0; i < newObject.AnonymousClassBody.Count; i++)
            {
                if (i > 0)
                    WriteLine();
                WriteMemberDeclaration(newObject.AnonymousClassBody[i]);
            }

            Unindent();
            Write("}");
        }
    }

    private void WriteNewArray(NewArrayExpression newArray)
    {
        Write("new ");
        WriteTypeReference(newArray.ElementType);

        if (newArray.DimensionExpressions != null)
        {
            foreach (var dim in newArray.DimensionExpressions)
            {
                Write("[");
                WriteExpression(dim);
                Write("]");
            }
        }

        for (int i = 0; i < newArray.EmptyDimensions; i++)
        {
            Write("[]");
        }

        if (newArray.Initializer != null)
        {
            Write(" ");
            WriteArrayInitializer(newArray.Initializer);
        }
    }

    private void WriteArrayInitializer(ArrayInitializerExpression arrayInit)
    {
        Write("{");
        if (arrayInit.Elements != null && arrayInit.Elements.Count > 0)
        {
            WriteList(arrayInit.Elements, WriteExpression);
        }
        Write("}");
    }

    private void WriteCast(CastExpression cast)
    {
        Write("(");
        WriteTypeReference(cast.Type);

        if (cast.AdditionalBounds != null && cast.AdditionalBounds.Count > 0)
        {
            Write(" & ");
            WriteList(cast.AdditionalBounds, WriteTypeReference, " & ");
        }

        Write(") ");
        WriteExpression(cast.Expression);
    }

    private void WriteInstanceOf(InstanceOfExpression instanceof)
    {
        WriteExpression(instanceof.Expression);
        Write(" instanceof ");

        if (instanceof.Type != null)
        {
            WriteTypeReference(instanceof.Type);
        }
        else if (instanceof.Pattern != null)
        {
            WritePattern(instanceof.Pattern);
        }
    }

    private void WritePattern(Pattern pattern)
    {
        switch (pattern)
        {
            case TypePattern typePattern:
                WriteTypeReference(typePattern.Type);
                Write($" {typePattern.Identifier}");
                break;
            case RecordPattern recordPattern:
                WriteTypeReference(recordPattern.Type);
                Write("(");
                if (recordPattern.Patterns != null)
                {
                    WriteList(recordPattern.Patterns, WritePattern);
                }
                Write(")");
                break;
        }
    }

    private void WriteClassLiteral(ClassLiteralExpression classLiteral)
    {
        WriteTypeReference(classLiteral.Type);
        Write(".class");
    }
}

