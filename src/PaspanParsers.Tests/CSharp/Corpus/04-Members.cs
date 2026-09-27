using System;

namespace Corpus.Members;

public class Members : IEquatable<Members>
{
    public const int Max = 10, Min = -10;
    private static readonly object gate = new object();
    private volatile bool flag;
    private int[] values = { 1, 2, 3 };

    public event EventHandler Changed;

    public event EventHandler<int> Custom
    {
        add { Changed += (s, e) => { }; }
        remove { }
    }

    public int this[int index]
    {
        get => values[index];
        set => values[index] = value;
    }

    public string this[string key, int n] => key + n;

    public int Auto { get; private set; } = 42;

    public int Computed => Auto * 2;

    public required string Name { get; init; }

    public int Full
    {
        get { return Auto; }
        protected set { Auto = value; }
    }

    static Members() { }

    public Members() : this(0) { }

    public Members(int value) : base()
    {
        Auto = value;
    }

    ~Members() { }

    public static Members operator +(Members a, Members b) => a;

    public static bool operator ==(Members a, Members b) => ReferenceEquals(a, b);

    public static bool operator !=(Members a, Members b) => !(a == b);

    public static implicit operator int(Members m) => m.Auto;

    public static explicit operator Members(int value) => new Members(value);

    public static bool operator true(Members m) => m.flag;

    public static bool operator false(Members m) => !m.flag;

    public static Members operator checked -(Members m) => m;

    public static Members operator -(Members m) => m;

    public static Members operator >>>(Members m, int shift) => m;

    bool IEquatable<Members>.Equals(Members other) => other is not null;

    public override bool Equals(object obj) => obj is Members m && m.Auto == Auto;

    public override int GetHashCode() => Auto;

    public void Parameters(ref int a, out int b, in int c, params int[] rest)
    {
        b = a + c;
    }

    public void Defaults(int a = 1, string b = null, bool c = default) { }

    public partial class Inner { }

    public extern static void Native();
}
