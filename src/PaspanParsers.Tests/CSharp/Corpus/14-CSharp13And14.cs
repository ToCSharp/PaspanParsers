using System;
using System.Collections.Generic;

namespace Corpus.Modern;

public partial class Modern
{
    public partial int Size { get; set; }

    public void Params(params ReadOnlySpan<int> values) { }

    public char Escape => '\e';

    public string Name
    {
        get => field;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    }

    public void NullConditionalAssignment(Modern other)
    {
        other?.Name = "x";
        var type = nameof(List<>);
    }

    public static Modern operator +(Modern a, Modern b) => a;

    public void operator +=(Modern other) { }

    public Func<int, int> Lambda = (scoped ref int x) => x;
}

public partial class Modern
{
    public partial int Size { get => 0; set { } }
}

public static class ModernExtensions
{
    extension(string text)
    {
        public bool IsBlank => string.IsNullOrWhiteSpace(text);

        public string Repeat(int count) => string.Concat(System.Linq.Enumerable.Repeat(text, count));
    }

    extension<T>(List<T> list) where T : class
    {
        public static List<T> Create() => new();
    }
}

public ref struct Scoped
{
    public void Method(scoped Span<int> span, scoped ref int value) { }
}
