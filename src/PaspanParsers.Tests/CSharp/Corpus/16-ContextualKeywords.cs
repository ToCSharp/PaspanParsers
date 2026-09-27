using System;
using System.Collections.Generic;

namespace Corpus.Contextual;

public class Contextual
{
    private int value;
    private int get, set, add, remove, init;
    private int select, from, where, group, by, into, orderby, join, let, on, equals, ascending, descending;
    private int async, await, yield, dynamic, partial, record, required, file, scoped, when, and, or, not, with;
    private int nameof, global, managed, unmanaged, notnull, extension, allows, args, field;

    public int Value
    {
        get { return value; }
        set { this.value = value; }
    }

    public void Use()
    {
        var var = 1;
        int where = var + select + from;
        var async = 2;
        var list = new List<int> { var, where, async };
        record = 1;
        partial = when + and + or + not + with;
        yield = await;
    }

    public void @class(int @int, string @event) { }

    public class var { }
}
