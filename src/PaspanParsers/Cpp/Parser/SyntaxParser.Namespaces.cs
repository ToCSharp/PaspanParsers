using System.Text;

namespace PaspanParsers.Cpp;

// Namespaces ([basic.namespace]), using declarations and directives, alias declarations, linkage
// specifications ([dcl.link]), modules ([module]) and asm declarations ([dcl.asm]).
internal ref partial struct SyntaxParser
{
    // ========================================
    // Namespaces
    // ========================================

    /// <summary>
    /// A namespace alias (<c>namespace a = b;</c>), or in a namespace, a namespace definition.
    /// </summary>
    private Declaration ParseNamespaceDeclaration(DeclarationContext context)
    {
        if (Peek(1).IsIdentifier && Peek(2).IsPunctuator("="))
        {
            return ParseNamespaceAliasDefinition();
        }

        return context == DeclarationContext.Namespace ? ParseNamespaceDefinition() : null;
    }

    /// <summary>
    /// <c>inline namespace [[attributes]] a::inline b { declarations }</c>, or an unnamed namespace.
    /// </summary>
    private NamespaceDefinition ParseNamespaceDefinition()
    {
        var start = NodeStart;
        var isInline = TryEatKeyword("inline");
        EatToken();
        var attributes = ParseAttributeSpecifiers();
        if (attributes == null)
        {
            return null;
        }

        var names = new List<NamespaceName>();
        if (!IsPunctuator("{"))
        {
            do
            {
                var isComponentInline = names.Count > 0 && TryEatKeyword("inline");
                var identifier = TryEatIdentifier();
                if (identifier == null)
                {
                    return null;
                }

                names.Add(new NamespaceName(identifier, isComponentInline));
            }
            while (TryEatPunctuator("::"));
        }

        var symbols = _cache.Symbols;
        if (names.Count == 0)
        {
            symbols.EnterNamespace(null, isInline);
        }

        for (var i = 0; i < names.Count; i++)
        {
            symbols.EnterNamespace(names[i].Identifier, i == 0 ? isInline : names[i].IsInline);
        }

        var declarations = ParseDeclarationBlock(DeclarationContext.Namespace, out var closeBraceDirectives);
        if (declarations == null)
        {
            return null;
        }

        symbols.ExitScopes(Math.Max(names.Count, 1));
        return Finish(new NamespaceDefinition(names, declarations)
        {
            IsInline = isInline,
            Attributes = attributes,
            CloseBraceDirectives = closeBraceDirectives,
        }, start);
    }

    /// <summary>
    /// <c>namespace alias = outer::inner;</c>.
    /// </summary>
    private NamespaceAliasDefinition ParseNamespaceAliasDefinition()
    {
        var start = NodeStart;
        EatToken();
        var alias = EatToken().Text;
        EatToken();
        var target = ParseName(NameContext.Type);
        if (target == null || !TryEatPunctuator(";"))
        {
            return null;
        }

        _cache.Symbols.DeclareNamespaceAlias(alias, target);
        return Finish(new NamespaceAliasDefinition(alias, target), start);
    }

    // ========================================
    // Using
    // ========================================

    /// <summary>
    /// A declaration that starts with <c>using</c>: a using-directive, <c>using enum</c>, an alias declaration
    /// or a using-declaration.
    /// </summary>
    private Declaration ParseUsingDeclaration()
    {
        var start = NodeStart;
        EatToken();
        if (IsKeyword("namespace") || IsKeyword("enum"))
        {
            var isDirective = EatToken().Text == "namespace";
            var name = ParseName(NameContext.Type);
            if (name == null || !TryEatPunctuator(";"))
            {
                return null;
            }

            _cache.Symbols.UseScope(name);
            return isDirective ? Finish(new UsingDirective(name), start) : Finish(new UsingEnumDeclaration(name), start);
        }

        if (Current.IsIdentifier && (Peek(1).IsPunctuator("=") || IsAttributeSpecifierStartAt(1)))
        {
            return ParseAliasDeclarationRest(start);
        }

        var declarators = new List<UsingDeclarator>();
        do
        {
            var declaratorStart = NodeStart;
            var isTypename = TryEatKeyword("typename");
            var name = ParseName(NameContext.Expression);
            if (name == null)
            {
                return null;
            }

            var isPackExpansion = TryEatPunctuator("...");
            declarators.Add(Finish(new UsingDeclarator(name) { IsTypename = isTypename, IsPackExpansion = isPackExpansion }, declaratorStart));
            DeclareUsingName(name, isTypename);
        }
        while (TryEatPunctuator(","));

        return TryEatPunctuator(";") ? Finish(new UsingDeclaration(declarators), start) : null;
    }

    /// <summary>
    /// The tokens at <paramref name="offset"/> start an attribute specifier.
    /// </summary>
    private bool IsAttributeSpecifierStartAt(int offset)
    {
        var token = Peek(offset);
        return (token.IsPunctuator("[") && Peek(offset + 1).IsPunctuator("[")) || token.IsKeyword("alignas")
            || (token.IsIdentifier && token.Text is "__attribute__" or "__attribute");
    }

    /// <summary>
    /// Declares the name a using-declaration introduces, with the kind it has where it is declared: a
    /// type after <c>typename</c>. The name of inherited constructors (<c>using Base::Base;</c>) and names
    /// declared nowhere in the file declare nothing.
    /// </summary>
    private readonly void DeclareUsingName(Name name, bool isTypename)
    {
        if (name is not QualifiedName { Name: IdentifierName identifier } qualified || LastIdentifier(qualified.Qualifier) == identifier.Identifier)
        {
            return;
        }

        var kind = isTypename ? SymbolKind.Type : _cache.Symbols.Lookup(name);
        if (kind != null)
        {
            _cache.Symbols.Declare(identifier.Identifier, kind.Value);
        }
    }

    /// <summary>
    /// <c>Alias [[attributes]] = type-id;</c> after <c>using</c>. The alias is declared as a type, and names
    /// qualified by it are looked up in the class it names.
    /// </summary>
    private AliasDeclaration ParseAliasDeclarationRest(int start)
    {
        var identifier = EatToken().Text;
        var attributes = ParseAttributeSpecifiers();
        if (attributes == null || !TryEatPunctuator("="))
        {
            return null;
        }

        var type = ParseTypeId();
        if (type == null || !TryEatPunctuator(";"))
        {
            return null;
        }

        _cache.Symbols.DeclareTypeAlias(identifier, AliasedClass(type.Specifiers, type.Declarator));
        return Finish(new AliasDeclaration(identifier, type) { Attributes = attributes }, start);
    }

    // ========================================
    // Linkage specifications and exports
    // ========================================

    /// <summary>
    /// <c>extern "C" declaration</c> or <c>extern "C" { declarations }</c>.
    /// </summary>
    private LinkageSpecification ParseLinkageSpecification()
    {
        var start = NodeStart;
        EatToken();
        var literal = EatToken().Text;
        if (literal.Length < 2 || literal[0] != '"' || literal[^1] != '"')
        {
            return null;
        }

        var language = literal[1..^1];
        if (IsPunctuator("{"))
        {
            var declarations = ParseDeclarationBlock(DeclarationContext.Namespace, out var closeBraceDirectives);
            return declarations == null
                ? null
                : Finish(new LinkageSpecification(language, declarations) { HasBraces = true, CloseBraceDirectives = closeBraceDirectives }, start);
        }

        var declaration = ParseDeclaration(DeclarationContext.Namespace);
        return declaration == null ? null : Finish(new LinkageSpecification(language, [declaration]), start);
    }

    /// <summary>
    /// <c>export declaration</c>, <c>export { declarations }</c>, or an exported module or import declaration.
    /// </summary>
    private Declaration ParseExportDeclaration()
    {
        var start = NodeStart;
        if (IsModuleDeclarationStart(1) || IsImportDeclarationStart(1))
        {
            EatToken();
            return ParseModuleOrImportDeclaration(start, isExport: true);
        }

        EatToken();
        if (IsPunctuator("{"))
        {
            var declarations = ParseDeclarationBlock(DeclarationContext.Namespace, out var closeBraceDirectives);
            return declarations == null
                ? null
                : Finish(new ExportDeclaration(declarations) { HasBraces = true, CloseBraceDirectives = closeBraceDirectives }, start);
        }

        var declaration = ParseDeclaration(DeclarationContext.Namespace);
        return declaration == null ? null : Finish(new ExportDeclaration([declaration]), start);
    }

    // ========================================
    // Modules
    // ========================================

    /// <summary>
    /// The token at <paramref name="offset"/> starts a module declaration: <c>module</c> followed by ';', a
    /// name or ':'. Elsewhere <c>module</c> is an identifier.
    /// </summary>
    private bool IsModuleDeclarationStart(int offset)
    {
        var token = Peek(offset);
        if (!token.IsIdentifier || token.Text != "module")
        {
            return false;
        }

        var next = Peek(offset + 1);
        return next.IsPunctuator(";") || next.IsPunctuator(":") || (next.IsIdentifier && !Peek(offset + 2).IsPunctuator("::"));
    }

    /// <summary>
    /// The token at <paramref name="offset"/> starts an import declaration: <c>import</c> followed by a
    /// module name, ':', or a header name.
    /// </summary>
    private bool IsImportDeclarationStart(int offset)
    {
        var token = Peek(offset);
        if (!token.IsIdentifier || token.Text != "import")
        {
            return false;
        }

        var next = Peek(offset + 1);
        return next.IsPunctuator(":") || next.IsPunctuator("<") || next.Kind == TokenKind.StringLiteral
            || (next.IsIdentifier && Peek(offset + 2) is var after && (after.IsPunctuator(";") || after.IsPunctuator(".") || after.IsPunctuator(":") || after.IsPunctuator("[")));
    }

    /// <summary>
    /// <c>module name:partition [[attributes]];</c>, <c>module;</c>, <c>module :private;</c>, or
    /// <c>import name;</c>, <c>import :partition;</c>, <c>import &lt;header&gt;;</c> after an optional <c>export</c>.
    /// </summary>
    private Declaration ParseModuleOrImportDeclaration(int start, bool isExport)
    {
        var isModule = EatToken().Text == "module";
        string name = null;
        string header = null;
        if (!isModule && IsPunctuator("<"))
        {
            header = ParseHeaderName();
            if (header == null)
            {
                return null;
            }
        }
        else if (!isModule && Current.Kind == TokenKind.StringLiteral)
        {
            header = EatToken().Text;
        }
        else if (Current.IsIdentifier)
        {
            name = ParseModuleName();
        }

        string partition = null;
        if (header == null && TryEatPunctuator(":"))
        {
            partition = IsKeyword("private") && isModule && name == null ? EatToken().Text : ParseModuleName();
            if (partition == null)
            {
                return null;
            }
        }

        var attributes = ParseAttributeSpecifiers();
        if (attributes == null || !TryEatPunctuator(";"))
        {
            return null;
        }

        if (isModule)
        {
            return Finish(new ModuleDeclaration(name, partition) { IsExport = isExport, Attributes = attributes }, start);
        }

        return name == null && partition == null && header == null
            ? null
            : Finish(new ImportDeclaration(name, partition, header) { IsExport = isExport, Attributes = attributes }, start);
    }

    /// <summary>
    /// A dotted module name: <c>std.core</c>.
    /// </summary>
    private string ParseModuleName()
    {
        var name = TryEatIdentifier();
        while (name != null && IsPunctuator(".") && Peek(1).IsIdentifier)
        {
            EatToken();
            name += "." + EatToken().Text;
        }

        return name;
    }

    /// <summary>
    /// <c>&lt;header&gt;</c> of a header unit import, as written.
    /// </summary>
    private string ParseHeaderName()
    {
        var start = EatToken().Start;
        while (!IsPunctuator(">"))
        {
            if (Current.Kind == TokenKind.EndOfFile || IsPunctuator(";"))
            {
                return null;
            }

            EatToken();
        }

        var end = EatToken().End;
        return Encoding.UTF8.GetString(_source[start..end]);
    }

    // ========================================
    // Asm declarations
    // ========================================

    /// <summary>
    /// <c>asm("text");</c>, with the GNU spellings <c>__asm__</c>, <c>__asm</c> and qualifiers:
    /// <c>__asm__ volatile("" ::: "memory");</c>.
    /// </summary>
    private AsmDeclaration ParseAsmDeclaration()
    {
        var start = NodeStart;
        var keyword = EatToken().Text;
        var qualifiers = new List<string>();
        while (IsKeyword("volatile") || IsKeyword("inline") || IsKeyword("goto") || (Current.IsIdentifier && Current.Text is "__volatile__" or "__volatile" or "__inline__" or "__inline"))
        {
            qualifiers.Add(EatToken().Text);
        }

        if (!IsPunctuator("("))
        {
            return null;
        }

        var text = ParseParenthesizedText();
        return text != null && TryEatPunctuator(";") ? Finish(new AsmDeclaration(keyword, text) { Qualifiers = qualifiers }, start) : null;
    }
}
