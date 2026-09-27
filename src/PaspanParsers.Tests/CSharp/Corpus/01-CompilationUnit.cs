extern alias Legacy;
global using System;
global using static System.Math;
using System.Collections.Generic;
using static System.Console;
using IntList = System.Collections.Generic.List<int>;
using Pair = (int First, string Second);
using Legacy::Old.Namespace;

[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[module: System.CLSCompliant(true)]

namespace Corpus.CompilationUnit
{
    using System.Text;

    namespace Nested.Inner
    {
        class A { }
    }

    class B { }
}

namespace Corpus.Other
{
    delegate void Handler(object sender);
}
