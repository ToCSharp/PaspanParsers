namespace PaspanParsers.Cpp;

/// <summary>
/// What a name declares, as far as parsing needs to know.
/// </summary>
internal enum SymbolKind : byte
{
    /// <summary>A variable, function or enumerator.</summary>
    Value,
    Type,
    Template,
    Concept,
    Namespace,
}

/// <summary>
/// What a scope belongs to ([basic.scope]).
/// </summary>
internal enum ScopeKind : byte
{
    /// <summary>A block, a function, a lambda, a statement or a requires-expression.</summary>
    Block,
    Namespace,
    Class,
    Enum,

    /// <summary>The template parameters of a template declaration.</summary>
    TemplateParameters,
}

/// <summary>
/// The names declared so far, by scope ([basic.scope]), to tell types from values where the grammar is
/// ambiguous: <c>a * b;</c> declares <c>b</c> when <c>a</c> is a type and multiplies otherwise. Names are
/// declared as their declarations are parsed; names that are not declared in the file (from headers) are
/// unknown, and the parser uses heuristics for them.
/// </summary>
/// <remarks>
/// <para>
/// The scopes of namespaces, classes and enumerations persist after they are left: a namespace can be
/// reopened, and qualified names (<c>outer::inner::value</c>, <c>Holder::Kind</c>) are looked up in them.
/// Unqualified lookup searches the active scopes from the innermost out; in each, the names declared there,
/// then the namespaces its using-directives nominate (<c>using namespace</c>, <c>using enum</c>, inline
/// namespaces) and the scopes of its known base classes. The body of a function defined outside its class or
/// namespace (<c>int S::f() { … }</c>) searches that scope too.
/// </para>
/// <para>
/// Speculative parses that fail are undone: <see cref="Checkpoint"/> marks the state and
/// <see cref="Rollback"/> removes the declarations, scopes and lookup links made after it.
/// </para>
/// </remarks>
internal sealed class Symbols
{
    private readonly record struct Symbol(SymbolKind Kind, bool IsPack);

    private sealed class Scope(ScopeKind kind, string name, Scope parent)
    {
        public ScopeKind Kind { get; } = kind;
        public string Name { get; } = name;

        /// <summary>The enclosing namespace or class, for the scopes of namespaces, classes and enumerations.</summary>
        public Scope Parent { get; } = parent;

        public Dictionary<string, Symbol> Names { get; } = new(StringComparer.Ordinal);

        /// <summary>The namespaces, classes and enumerations declared here, and namespace aliases, by name.</summary>
        public Dictionary<string, Scope> Members { get; set; }

        /// <summary>The namespaces and enumerations whose names are visible here, and the known base classes.</summary>
        public List<Scope> Nominated { get; set; }
    }

    private enum ChangeKind : byte
    {
        Declare,
        Push,
        Pop,
        AddMember,
        Nominate,
    }

    // A change to undo: a declaration (with the symbol it replaced, if any), a scope that was entered or
    // left, a member scope that was added (with the one it replaced) or a nominated scope
    private readonly record struct Change(ChangeKind Kind, Scope Scope, string Name, Symbol? Previous, Scope Other);

    private readonly CppParseOptions _options;
    private readonly List<Change> _log = [];
    private readonly List<Scope> _active;
    private readonly Scope _global = new(ScopeKind.Namespace, null, null);

    public Symbols(CppParseOptions options)
    {
        _options = options;
        _active = [_global];
    }

    private Scope Current => _active[^1];

    // ========================================
    // Scopes
    // ========================================

    /// <summary>
    /// Enters a new scope of <paramref name="kind"/>: a block by default.
    /// </summary>
    public void EnterScope(ScopeKind kind = ScopeKind.Block)
    {
        Push(new Scope(kind, null, null));
    }

    public void ExitScope()
    {
        if (_active.Count > 1)
        {
            _log.Add(new Change(ChangeKind.Pop, _active[^1], null, null, null));
            _active.RemoveAt(_active.Count - 1);
        }
    }

    private void Push(Scope scope)
    {
        _log.Add(new Change(ChangeKind.Push, scope, null, null, null));
        _active.Add(scope);
    }

    /// <summary>
    /// Enters the namespace <paramref name="name"/> of the current scope, creating it the first time, or an
    /// unnamed namespace when the name is null. The names of an inline or unnamed namespace are visible in
    /// the enclosing one.
    /// </summary>
    public void EnterNamespace(string name, bool isInline)
    {
        var parent = DeclarationScope();
        var scope = name != null ? parent.Members?.GetValueOrDefault(name) : null;
        if (scope?.Kind != ScopeKind.Namespace)
        {
            scope = new Scope(ScopeKind.Namespace, name, parent);
            if (name != null)
            {
                AddMember(parent, name, scope);
                Declare(name, SymbolKind.Namespace);
            }
        }

        if (isInline || name == null)
        {
            Nominate(parent, scope);
        }

        Push(scope);
    }

    /// <summary>
    /// Enters the scope of the class or enumeration <paramref name="name"/> of the current scope, where the
    /// name is declared as a type (or a template) when <paramref name="declare"/> is set: the name of
    /// <c>Box&lt;void&gt;</c> or of a class defined in another scope (<c>struct Outer::Inner</c>) is not. An
    /// unnamed class gets a scope of its own, whose names are visible in the enclosing scope, as those of an
    /// unnamed union member are. The enumerators of an unscoped enumeration are visible there too.
    /// </summary>
    public void EnterClass(ScopeKind kind, string name, bool declare, bool isScopedEnum = false)
    {
        var parent = DeclarationScope();
        Scope scope;
        if (name != null)
        {
            scope = parent.Members?.GetValueOrDefault(name);
            if (scope?.Kind != kind)
            {
                scope = new Scope(kind, name, parent);
                AddMember(parent, name, scope);
            }

            if (declare)
            {
                Declare(name, SymbolKind.Type);
            }
        }
        else
        {
            scope = new Scope(kind, null, parent);
            if (kind == ScopeKind.Class)
            {
                Nominate(parent, scope);
            }
        }

        if (kind == ScopeKind.Enum && !isScopedEnum)
        {
            Nominate(parent, scope);
        }

        Push(scope);
    }

    /// <summary>
    /// Makes the class <paramref name="base"/>'s members visible in the current class scope.
    /// </summary>
    public void AddBase(Name @base)
    {
        var scope = ResolveScope(@base, global: false);
        if (scope != null && scope != Current)
        {
            Nominate(Current, scope);
        }
    }

    /// <summary>
    /// Enters the scope of the class or namespace that qualifies the name of a declaration defined outside
    /// it, <c>S</c> in <c>int S::f() { … }</c>, and the enclosing scopes that are not active. Returns the
    /// number of scopes entered, for <see cref="ExitScopes"/>.
    /// </summary>
    public int EnterQualifiedScope(Name qualifier, bool global)
    {
        var scope = ResolveScope(qualifier, global);
        var chain = new List<Scope>();
        for (; scope != null && !_active.Contains(scope); scope = scope.Parent)
        {
            chain.Add(scope);
        }

        for (var i = chain.Count - 1; i >= 0; i--)
        {
            Push(chain[i]);
        }

        return chain.Count;
    }

    public void ExitScopes(int count)
    {
        for (var i = 0; i < count; i++)
        {
            ExitScope();
        }
    }

    /// <summary>
    /// The name of the class whose members are being declared, or null outside a class.
    /// </summary>
    public string CurrentClassName
    {
        get
        {
            var scope = DeclarationScope();
            return scope.Kind == ScopeKind.Class ? scope.Name : null;
        }
    }

    // ========================================
    // Declarations
    // ========================================

    /// <summary>
    /// Declares <paramref name="name"/> in the innermost scope that is not the scope of template parameters.
    /// The entity a template declaration declares is a template: <c>template &lt;class T&gt; T zero;</c>.
    /// </summary>
    public void Declare(string name, SymbolKind kind, bool isPack = false)
    {
        if (Current.Kind == ScopeKind.TemplateParameters && kind is SymbolKind.Type or SymbolKind.Value)
        {
            kind = SymbolKind.Template;
        }

        Declare(DeclarationScope(), name, new Symbol(kind, isPack));
    }

    /// <summary>
    /// Declares a template parameter in the current scope.
    /// </summary>
    public void DeclareTemplateParameter(string name, SymbolKind kind, bool isPack)
    {
        Declare(Current, name, new Symbol(kind, isPack));
    }

    private void Declare(Scope scope, string name, Symbol symbol)
    {
        // A variable or function hides a class of the same name: struct stat; int stat(const char *, struct stat *);
        Symbol? previous = scope.Names.TryGetValue(name, out var existing) ? existing : null;
        _log.Add(new Change(ChangeKind.Declare, scope, name, previous, null));
        scope.Names[name] = symbol;
    }

    /// <summary>
    /// Declares <paramref name="alias"/> as another name of the namespace <paramref name="target"/>.
    /// </summary>
    public void DeclareNamespaceAlias(string alias, Name target)
    {
        Declare(alias, SymbolKind.Namespace);
        if (ResolveScope(target, global: false) is { } scope)
        {
            AddMember(DeclarationScope(), alias, scope);
        }
    }

    /// <summary>
    /// <c>using namespace name;</c> or <c>using enum name;</c>: the names of the namespace or enumeration
    /// become visible in the current scope.
    /// </summary>
    public void UseScope(Name name)
    {
        if (ResolveScope(name, global: false) is { } scope)
        {
            Nominate(Current, scope);
        }
    }

    private Scope DeclarationScope()
    {
        for (var i = _active.Count - 1; i > 0; i--)
        {
            if (_active[i].Kind != ScopeKind.TemplateParameters)
            {
                return _active[i];
            }
        }

        return _global;
    }

    private void AddMember(Scope scope, string name, Scope member)
    {
        scope.Members ??= new Dictionary<string, Scope>(StringComparer.Ordinal);
        _log.Add(new Change(ChangeKind.AddMember, scope, name, null, scope.Members.GetValueOrDefault(name)));
        scope.Members[name] = member;
    }

    private void Nominate(Scope scope, Scope nominated)
    {
        _log.Add(new Change(ChangeKind.Nominate, scope, null, null, nominated));
        (scope.Nominated ??= []).Add(nominated);
    }

    // ========================================
    // Lookup
    // ========================================

    /// <summary>
    /// The kind of an unqualified name: declared in an active scope or visible in one, or given by the options.
    /// Null when the name is unknown.
    /// </summary>
    public SymbolKind? Lookup(string name) => Find(name)?.Kind;

    /// <summary>
    /// The unqualified name is a pack: a template parameter pack.
    /// </summary>
    public bool IsPack(string name) => Find(name)?.IsPack == true;

    private Symbol? Find(string name)
    {
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            if (FindIn(_active[i], name, 0) is { } symbol)
            {
                return symbol;
            }
        }

        if (_options.TemplateNames.Contains(name))
        {
            return new Symbol(SymbolKind.Template, false);
        }

        return _options.TypeNames.Contains(name) ? new Symbol(SymbolKind.Type, false) : null;
    }

    private static Symbol? FindIn(Scope scope, string name, int depth)
    {
        if (scope.Names.TryGetValue(name, out var symbol))
        {
            return symbol;
        }

        // Namespaces can nominate each other: using namespace a; in b and using namespace b; in a
        if (scope.Nominated != null && depth < 16)
        {
            foreach (var nominated in scope.Nominated)
            {
                if (FindIn(nominated, name, depth + 1) is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The kind of a name. A qualified name is looked up in the namespace or class its qualifier names;
    /// when the qualifier is not known (<c>std::</c>, <c>T::</c>), by its last identifier, a guess that
    /// holds for names declared once. A template-id is a type or a concept-id when its template is known,
    /// and unknown otherwise.
    /// </summary>
    public SymbolKind? Lookup(Name name) => name switch
    {
        IdentifierName identifier => Lookup(identifier.Identifier),
        TemplateIdName templateId => TemplateIdKind(templateId, Lookup(templateId.Template)),
        QualifiedName qualified => LookupComponent(qualified.Qualifier, qualified.Qualifier == null, qualified.Name),
        _ => null,
    };

    /// <summary>
    /// The kind of the component <paramref name="identifier"/> of a name qualified by
    /// <paramref name="qualifier"/> (<paramref name="global"/> for a leading '::'); unqualified when both are unset.
    /// </summary>
    public SymbolKind? LookupComponent(Name qualifier, bool global, string identifier)
    {
        if (qualifier == null && !global)
        {
            return Lookup(identifier);
        }

        var scope = ResolveScope(qualifier, global);
        return scope != null ? FindIn(scope, identifier, 0)?.Kind : Lookup(identifier);
    }

    private SymbolKind? LookupComponent(Name qualifier, bool global, Name component) => component switch
    {
        IdentifierName identifier => LookupComponent(qualifier, global, identifier.Identifier),
        TemplateIdName { Template: IdentifierName template } templateId =>
            TemplateIdKind(templateId, LookupComponent(qualifier, global, template.Identifier)),
        _ => null,
    };

    private static SymbolKind? TemplateIdKind(TemplateIdName templateId, SymbolKind? template)
    {
        if (templateId.Template is not IdentifierName)
        {
            return null;
        }

        return template switch
        {
            SymbolKind.Concept => SymbolKind.Concept,
            null => null,
            _ => SymbolKind.Type,
        };
    }

    /// <summary>
    /// The name designates a class defined in the file: <c>Box&lt;int&gt;</c>, <c>Outer::Inner</c>.
    /// </summary>
    public bool NamesClass(Name name)
    {
        var global = name is QualifiedName { Qualifier: null };
        return ResolveScope(global ? ((QualifiedName)name).Name : name, global)?.Kind == ScopeKind.Class;
    }

    /// <summary>
    /// The scope a name designates: a namespace, class or enumeration known in the file, or null. With
    /// <paramref name="global"/> and no name, the global namespace.
    /// </summary>
    private Scope ResolveScope(Name name, bool global)
    {
        switch (name)
        {
            case null:
                return global ? _global : null;
            case IdentifierName identifier:
                return global ? MemberScope(_global, identifier.Identifier) : FindScope(identifier.Identifier);
            case TemplateIdName { Template: IdentifierName template }:
                return global ? MemberScope(_global, template.Identifier) : FindScope(template.Identifier);
            case QualifiedName qualified:
            {
                var outer = ResolveScope(qualified.Qualifier, qualified.Qualifier == null);
                var last = qualified.Name switch
                {
                    IdentifierName identifier => identifier.Identifier,
                    TemplateIdName { Template: IdentifierName template } => template.Identifier,
                    _ => null,
                };

                return outer != null && last != null ? MemberScope(outer, last) : null;
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// The scope of an unqualified name, looked up in the active scopes.
    /// </summary>
    private Scope FindScope(string name)
    {
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            if (MemberScope(_active[i], name) is { } scope)
            {
                return scope;
            }

            // Another entity of the name, like a template parameter, hides the namespaces and classes outside
            if (FindIn(_active[i], name, 0) != null)
            {
                return null;
            }
        }

        return null;
    }

    private static Scope MemberScope(Scope scope, string name, int depth = 0)
    {
        if (scope.Members != null && scope.Members.TryGetValue(name, out var member))
        {
            return member;
        }

        if (scope.Nominated != null && depth < 16)
        {
            foreach (var nominated in scope.Nominated)
            {
                if (MemberScope(nominated, name, depth + 1) is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    // ========================================
    // Speculation
    // ========================================

    public int Checkpoint() => _log.Count;

    public void Rollback(int checkpoint)
    {
        for (var i = _log.Count - 1; i >= checkpoint; i--)
        {
            var change = _log[i];
            switch (change.Kind)
            {
                case ChangeKind.Declare:
                    if (change.Previous is { } previous)
                    {
                        change.Scope.Names[change.Name] = previous;
                    }
                    else
                    {
                        change.Scope.Names.Remove(change.Name);
                    }

                    break;
                case ChangeKind.Push:
                    _active.RemoveAt(_active.Count - 1);
                    break;
                case ChangeKind.Pop:
                    _active.Add(change.Scope);
                    break;
                case ChangeKind.AddMember:
                    if (change.Other != null)
                    {
                        change.Scope.Members[change.Name] = change.Other;
                    }
                    else
                    {
                        change.Scope.Members.Remove(change.Name);
                    }

                    break;
                case ChangeKind.Nominate:
                    change.Scope.Nominated.RemoveAt(change.Scope.Nominated.Count - 1);
                    break;
            }
        }

        _log.RemoveRange(checkpoint, _log.Count - checkpoint);
    }
}
