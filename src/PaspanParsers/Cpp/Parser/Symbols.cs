using System.Runtime.CompilerServices;

namespace PaspanParsers.Cpp;

/// <summary>
/// What a name declares, as far as parsing needs to know.
/// </summary>
internal enum SymbolKind : byte
{
    /// <summary>A variable, function or enumerator.</summary>
    Value,
    Type,

    /// <summary>A class or alias template: its template-ids are types.</summary>
    Template,

    /// <summary>A function or variable template: its template-ids are expressions.</summary>
    ValueTemplate,
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

    /// <summary>A typedef or alias of a type, as a member of a scope: the scope of the type it names.</summary>
    Alias,
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

        // The names declared here; created with the first, as most blocks declare none
        private Dictionary<string, Symbol> _names;

        public bool TryGetName(string name, out Symbol symbol)
        {
            if (_names == null)
            {
                symbol = default;
                return false;
            }

            return _names.TryGetValue(name, out symbol);
        }

        public void SetName(string name, Symbol symbol) => (_names ??= new(StringComparer.Ordinal))[name] = symbol;

        public void RemoveName(string name) => _names?.Remove(name);

        /// <summary>The namespaces, classes and enumerations declared here, and namespace aliases, by name.</summary>
        public Dictionary<string, Scope> Members { get; set; }

        /// <summary>The namespaces and enumerations whose names are visible here, and the known base classes.</summary>
        public List<Scope> Nominated { get; set; }

        /// <summary>For an alias, the name of the type it names, looked up from <see cref="Parent"/>.</summary>
        public Name AliasTarget { get; init; }

        public bool IsInline { get; set; }
    }

    private enum ChangeKind : byte
    {
        Declare,
        Push,
        Pop,
        AddMember,
        Nominate,

        // Scopes entered again, or left, all at once: the scopes are the array in Other
        PushScopes,
        PopScopes,
    }

    // A change to undo: a declaration (with the symbol it replaced, if any), a scope that was entered or
    // left, a member scope that was added (with the one it replaced), a nominated scope, or scopes that were
    // entered again or left together
    private readonly record struct Change(ChangeKind Kind, Scope Scope, string Name, Symbol? Previous, object Other);

    /// <summary>
    /// The largest log kept for reuse by the next parse on the thread (about 4 MB).
    /// </summary>
    private const int MaxPooledLog = 1 << 17;

    [ThreadStatic]
    private static List<Change> s_pooledLog;

    private readonly CppParseOptions _options;
    private List<Change> _log = RentLog();
    private readonly List<Scope> _active;
    private readonly Scope _global = new(ScopeKind.Namespace, null, null);

    // The array ActiveScopes returned last
    private object[] _lastActiveScopes = [];

    // The scopes of the classes whose members the options give, by name
    private readonly Dictionary<string, Scope> _optionClasses = new(StringComparer.Ordinal);

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
            scope.IsInline |= isInline;
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
        var scope = ResolveScope(@base, global: false) ?? OptionsClass(@base);
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
        var scope = ResolveScope(qualifier, global) ?? OptionsClass(qualifier);
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

    /// <summary>
    /// The number of active scopes.
    /// </summary>
    public int Depth => _active.Count;

    /// <summary>
    /// The active scopes from <paramref name="depth"/> on without the <paramref name="omitted"/> innermost ones,
    /// innermost last, to enter again with <see cref="EnterScopes"/>. The member functions of a class share
    /// the array.
    /// </summary>
    public object[] ActiveScopes(int depth, int omitted)
    {
        var count = _active.Count - omitted - depth;
        var scopes = _lastActiveScopes;
        if (scopes.Length == count)
        {
            var i = 0;
            while (i < count && ReferenceEquals(scopes[i], _active[depth + i]))
            {
                i++;
            }

            if (i == count)
            {
                return scopes;
            }
        }

        scopes = new object[count];
        for (var i = 0; i < count; i++)
        {
            scopes[i] = _active[depth + i];
        }

        _lastActiveScopes = scopes;
        return scopes;
    }

    /// <summary>
    /// Enters again scopes from <see cref="ActiveScopes"/>, with the names declared in them, until
    /// <see cref="ExitScopes(object[])"/>. One change is logged for all: the scopes of nested classes are
    /// entered for each member function.
    /// </summary>
    public void EnterScopes(object[] scopes)
    {
        _log.Add(new Change(ChangeKind.PushScopes, null, null, null, scopes));
        foreach (var scope in scopes)
        {
            _active.Add((Scope)scope);
        }
    }

    /// <summary>
    /// Leaves the scopes of <see cref="EnterScopes"/>.
    /// </summary>
    public void ExitScopes(object[] scopes)
    {
        _log.Add(new Change(ChangeKind.PopScopes, null, null, null, scopes));
        _active.RemoveRange(_active.Count - scopes.Length, scopes.Length);
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
    /// The entity a template declaration declares is a template: <c>template &lt;class T&gt; struct Box;</c>,
    /// <c>template &lt;class T&gt; T zero;</c>. A function that overloads a function template leaves the name
    /// a template: <c>f&lt;int&gt;(x)</c> still has template arguments.
    /// </summary>
    public void Declare(string name, SymbolKind kind, bool isPack = false)
    {
        if (Current.Kind == ScopeKind.TemplateParameters)
        {
            kind = kind switch
            {
                SymbolKind.Type => SymbolKind.Template,
                SymbolKind.Value => SymbolKind.ValueTemplate,
                _ => kind,
            };
        }

        var scope = DeclarationScope();
        if (kind == SymbolKind.Value && scope.TryGetName(name, out var previous) && previous.Kind == SymbolKind.ValueTemplate)
        {
            return;
        }

        Declare(scope, name, new Symbol(kind, isPack));
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
        Symbol? previous = scope.TryGetName(name, out var existing) ? existing : null;
        _log.Add(new Change(ChangeKind.Declare, scope, name, previous, null));
        scope.SetName(name, symbol);
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
    /// <paramref name="name"/> is the type of a declaration, so a name that is not known is a type declared outside
    /// the file (<c>StringRef s;</c>, <c>llvm::Twine t;</c>, <c>SmallVector&lt;int&gt; v;</c>): its last identifier
    /// is declared as a type, or a template, in the global scope, for the rest of the file.
    /// </summary>
    public void LearnType(Name name)
    {
        var last = name is QualifiedName qualified ? qualified.Name : name;
        var (identifier, kind) = last switch
        {
            IdentifierName id => (id.Identifier, SymbolKind.Type),
            TemplateIdName { Template: IdentifierName template } => (template.Identifier, SymbolKind.Template),
            _ => (null, SymbolKind.Type),
        };

        if (identifier != null && Find(identifier) == null)
        {
            Declare(_global, identifier, new Symbol(kind, false));
        }
    }

    /// <summary>
    /// Declares <paramref name="alias"/> as a type, another name of the type <paramref name="target"/>: when that
    /// is a class or enumeration of the file, names qualified by the alias are looked up in it
    /// (<c>using json = basic_json&lt;&gt;;</c> and <c>json::json_pointer</c>). <paramref name="target"/> is null
    /// for a type that is no class.
    /// </summary>
    public void DeclareTypeAlias(string alias, Name target)
    {
        Declare(alias, SymbolKind.Type);
        if (target != null)
        {
            // Resolved when used: the class may be defined after the alias (class basic_json; using json = basic_json<>;)
            var scope = DeclarationScope();
            AddMember(scope, alias, new Scope(ScopeKind.Alias, alias, scope) { AliasTarget = target });
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

        return FromOptions(name);
    }

    /// <summary>
    /// The kind of a name declared outside the file, as the options give it.
    /// </summary>
    private Symbol? FromOptions(string name)
    {
        if (_options.TemplateNames.Contains(name))
        {
            return new Symbol(SymbolKind.Template, false);
        }

        if (_options.FunctionTemplateNames.Contains(name))
        {
            return new Symbol(SymbolKind.ValueTemplate, false);
        }

        if (_options.ConceptNames.Contains(name))
        {
            return new Symbol(SymbolKind.Concept, false);
        }

        return _options.TypeNames.Contains(name) ? new Symbol(SymbolKind.Type, false) : null;
    }

    /// <summary>
    /// The kind of a name qualified by a namespace or class that is not known in the file (<c>std::</c>,
    /// <c>T::</c>): none of the file's declarations is its member, so only the options can know it, by the
    /// qualified name (<c>std::system_error</c>) or by the name alone.
    /// </summary>
    private SymbolKind? LookupMember(string qualifier, string name)
    {
        return (qualifier != null ? FromOptions(qualifier + "::" + name) : null)?.Kind ?? FromOptions(name)?.Kind;
    }

    /// <summary>
    /// The qualifier of a name as written, without template arguments: <c>std::chrono</c>, <c>Box</c> for
    /// <c>Box&lt;int&gt;::</c>; null when it has other components, such as <c>decltype(x)::</c>.
    /// </summary>
    private static string QualifierText(Name qualifier, bool global)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
        switch (qualifier)
        {
            case null:
                return global ? "" : null;
            case QualifiedName qualified:
            {
                var outer = QualifierText(qualified.Qualifier, qualified.Qualifier == null);
                var last = LastIdentifier(qualified.Name);
                return outer == null || last == null ? null : outer.Length == 0 ? last : outer + "::" + last;
            }

            default:
                return LastIdentifier(qualifier);
        }
    }

    /// <summary>
    /// The name of a namespace or class of the file qualified by the namespaces and classes around it, without
    /// inline and unnamed namespaces: <c>llvm::json</c>.
    /// </summary>
    private static string ScopeText(Scope scope)
    {
        var names = new List<string>();
        for (; scope?.Name != null; scope = scope.Parent)
        {
            if (!scope.IsInline)
            {
                names.Add(scope.Name);
            }
        }

        names.Reverse();
        return string.Join("::", names);
    }

    private static Symbol? FindIn(Scope scope, string name, int depth)
    {
        if (scope.TryGetName(name, out var symbol))
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
    /// when the qualifier is not known (<c>std::</c>, <c>T::</c>), its last identifier in the options, a guess
    /// that holds for names declared once. A template-id is a type, a value or a concept-id when its template
    /// is known, and unknown otherwise.
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
        if (scope == null)
        {
            return LookupMember(QualifierText(qualifier, global), identifier);
        }

        // A namespace of the file may be reopened in a header: std::string in a file that specializes std::hash
        return FindIn(scope, identifier, 0)?.Kind ?? (scope.Kind == ScopeKind.Namespace ? LookupMember(ScopeText(scope), identifier) : null);
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

        // A name known as no template (T::template size<int>() with a variable size elsewhere) is not the template
        return template switch
        {
            SymbolKind.Template => SymbolKind.Type,
            SymbolKind.ValueTemplate => SymbolKind.Value,
            SymbolKind.Concept => SymbolKind.Concept,
            _ => null,
        };
    }

    /// <summary>
    /// The scope a name designates: a namespace, class or enumeration known in the file, or null. With
    /// <paramref name="global"/> and no name, the global namespace.
    /// </summary>
    private Scope ResolveScope(Name name, bool global)
    {
        RuntimeHelpers.EnsureSufficientExecutionStack();
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
            return member.Kind == ScopeKind.Alias ? ResolveAlias(member, depth) : member;
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

    /// <summary>
    /// A scope with the members that the options give for the class <paramref name="name"/>, which is not
    /// known in the file; null when they give none. A member function template is a template, and the name of
    /// the class a type.
    /// </summary>
    private Scope OptionsClass(Name name)
    {
        var last = name is QualifiedName qualified ? qualified.Name : name;
        if (LastIdentifier(last) is not { } className || !_options.ClassMembers.TryGetValue(className, out var members))
        {
            return null;
        }

        if (!_optionClasses.TryGetValue(className, out var scope))
        {
            scope = new Scope(ScopeKind.Class, className, null);
            foreach (var member in members)
            {
                scope.SetName(member, new Symbol(_options.FunctionTemplateNames.Contains(member) ? SymbolKind.ValueTemplate : SymbolKind.Value, false));
            }

            // The injected-class-name: classes of the same name in other scopes may have a member of the name.
            // That of a class template takes template arguments: formatter<T>::format in a derived class
            scope.SetName(className, new Symbol(_options.TemplateNames.Contains(className) ? SymbolKind.Template : SymbolKind.Type, false));
            _optionClasses[className] = scope;
        }

        return scope;
    }

    /// <summary>
    /// The class or enumeration an alias names, looked up from the scope that declares the alias; null when it
    /// is not known.
    /// </summary>
    private static Scope ResolveAlias(Scope alias, int depth)
    {
        if (depth >= 16)
        {
            return null;
        }

        var target = alias.AliasTarget;
        var global = target is QualifiedName { Qualifier: null };
        var first = global ? ((QualifiedName)target).Name : target;
        var components = new List<string>();
        for (var name = first; ; )
        {
            if (name is QualifiedName qualified)
            {
                if (LastIdentifier(qualified.Name) is not { } component)
                {
                    return null;
                }

                components.Add(component);
                name = qualified.Qualifier;
                continue;
            }

            if (LastIdentifier(name) is not { } identifier)
            {
                return null;
            }

            components.Add(identifier);
            break;
        }

        components.Reverse();

        // The first component is looked up in the enclosing scopes, the others in the scope before them
        Scope scope = null;
        if (global)
        {
            scope = MemberScope(FindRoot(alias), components[0], depth + 1);
        }

        for (var outer = global ? null : alias.Parent; outer != null && scope == null; outer = outer.Parent)
        {
            scope = MemberScope(outer, components[0], depth + 1);
        }

        for (var i = 1; i < components.Count && scope != null; i++)
        {
            scope = MemberScope(scope, components[i], depth + 1);
        }

        return scope?.Kind is ScopeKind.Class or ScopeKind.Enum ? scope : null;
    }

    private static Scope FindRoot(Scope scope)
    {
        while (scope.Parent != null)
        {
            scope = scope.Parent;
        }

        return scope;
    }

    /// <summary>
    /// The identifier of an identifier or of the template of a template-id; null for other names.
    /// </summary>
    private static string LastIdentifier(Name name) => name switch
    {
        IdentifierName identifier => identifier.Identifier,
        TemplateIdName { Template: IdentifierName template } => template.Identifier,
        _ => null,
    };

    // ========================================
    // Speculation
    // ========================================

    public int Checkpoint() => _log.Count;

    /// <summary>
    /// Called when the parse is over: the log, which grows with every scope and declaration, is kept for
    /// the next parse on this thread.
    /// </summary>
    public void Release()
    {
        var log = _log;
        _log = null;
        if (log.Capacity <= MaxPooledLog)
        {
            log.Clear();
            s_pooledLog = log;
        }
    }

    private static List<Change> RentLog()
    {
        var log = s_pooledLog ?? [];
        s_pooledLog = null;
        return log;
    }

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
                        change.Scope.SetName(change.Name, previous);
                    }
                    else
                    {
                        change.Scope.RemoveName(change.Name);
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
                        change.Scope.Members[change.Name] = (Scope)change.Other;
                    }
                    else
                    {
                        change.Scope.Members.Remove(change.Name);
                    }

                    break;
                case ChangeKind.PushScopes:
                    _active.RemoveRange(_active.Count - ((object[])change.Other).Length, ((object[])change.Other).Length);
                    break;
                case ChangeKind.PopScopes:
                    foreach (var scope in (object[])change.Other)
                    {
                        _active.Add((Scope)scope);
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
