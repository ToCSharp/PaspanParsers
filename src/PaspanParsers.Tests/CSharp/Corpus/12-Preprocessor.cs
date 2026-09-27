#define FEATURE
#undef OBSOLETE
#nullable enable
using System;

namespace Corpus.Preprocessor;

#region Types
public class Preprocessed
{
#if FEATURE && !OBSOLETE
    public int Enabled => 1;
#elif DEBUG
    public int Debug => 2;
#else
    public int Disabled => 3 this is not valid code;
#endif

#if false
    garbage that is never parsed {
#endif

#pragma warning disable CS0168
    public void Method()
    {
        int unused;
#line 200 "generated.cs"
        int other;
#line default
#line hidden
    }
#pragma warning restore CS0168

#if (FEATURE || OTHER) && (true == true)
    public string? Nullable { get; set; }
#endif
}
#endregion
#nullable restore
