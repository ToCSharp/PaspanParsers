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
/// The names declared so far, by scope ([basic.scope]), to tell types from values where the grammar is
/// ambiguous: <c>a * b;</c> declares <c>b</c> when <c>a</c> is a type and multiplies otherwise. Names are
/// declared as their declarations are parsed; names that are not declared in the file (from headers) are
/// unknown, and the parser uses heuristics for them.
/// </summary>
/// <remarks>
/// Speculative parses that fail are undone: <see cref="Checkpoint"/> marks the state and
/// <see cref="Rollback"/> removes the declarations and scopes made after it.
/// </remarks>
internal sealed class Symbols
{
    private sealed class Scope(Scope parent)
    {
        public Scope Parent { get; } = parent;
        public Dictionary<string, SymbolKind> Names { get; } = new(StringComparer.Ordinal);
    }

    // A change to undo: a declaration (with the kind it replaced, if any) or a scope that was entered or left
    private readonly record struct Change(Scope Scope, string Name, SymbolKind? Previous, Scope ScopeBefore);

    private readonly CppParseOptions _options;
    private readonly List<Change> _log = [];
    private Scope _current = new(null);

    public Symbols(CppParseOptions options)
    {
        _options = options;
    }

    public void EnterScope()
    {
        _log.Add(new Change(null, null, null, _current));
        _current = new Scope(_current);
    }

    public void ExitScope()
    {
        _log.Add(new Change(null, null, null, _current));
        _current = _current.Parent ?? _current;
    }

    public void Declare(string name, SymbolKind kind)
    {
        // A variable or function hides a class of the same name: struct stat; int stat(const char *, struct stat *);
        SymbolKind? previous = _current.Names.TryGetValue(name, out var existing) ? existing : null;
        _log.Add(new Change(_current, name, previous, null));
        _current.Names[name] = kind;
    }

    /// <summary>
    /// The kind of an unqualified name: declared in this scope or an enclosing one, or given by the options.
    /// Null when the name is unknown.
    /// </summary>
    public SymbolKind? Lookup(string name)
    {
        for (var scope = _current; scope != null; scope = scope.Parent)
        {
            if (scope.Names.TryGetValue(name, out var kind))
            {
                return kind;
            }
        }

        if (_options.TemplateNames.Contains(name))
        {
            return SymbolKind.Template;
        }

        return _options.TypeNames.Contains(name) ? SymbolKind.Type : null;
    }

    /// <summary>
    /// The kind of a name. A qualified name is looked up by its last identifier: members of namespaces
    /// and classes are not tracked yet, so this is a guess that holds for names declared once.
    /// </summary>
    public SymbolKind? Lookup(Name name) => name switch
    {
        IdentifierName identifier => Lookup(identifier.Identifier),
        TemplateIdName { Template: IdentifierName template } => Lookup(template.Identifier) is SymbolKind.Concept ? SymbolKind.Concept : SymbolKind.Type,
        QualifiedName qualified => Lookup(qualified.Name),
        _ => null,
    };

    public int Checkpoint() => _log.Count;

    public void Rollback(int checkpoint)
    {
        for (var i = _log.Count - 1; i >= checkpoint; i--)
        {
            var change = _log[i];
            if (change.ScopeBefore != null)
            {
                _current = change.ScopeBefore;
            }
            else if (change.Previous is { } previous)
            {
                change.Scope.Names[change.Name] = previous;
            }
            else
            {
                change.Scope.Names.Remove(change.Name);
            }
        }

        _log.RemoveRange(checkpoint, _log.Count - checkpoint);
    }
}
