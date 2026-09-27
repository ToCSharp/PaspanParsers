#define TRACE_ENABLED
#undef DEBUG_ONLY
#if !DEBUG_ONLY && TRACE_ENABLED
#define FEATURE
#endif
#nullable enable
using System;
using System.Collections.Generic;

#pragma warning disable CS0168, CS0219 // unused variables

namespace Corpus.Preprocessor2
{
#region Types
    public class Configuration
#nullable disable
    {
#if FEATURE
        public string Feature => "on";
#else
        public string Feature => "off";
#endif

#if DEBUG
        public bool IsDebug => true;
#elif RELEASE
        public bool IsDebug => false;
#else
        public bool IsDebug => false;
#endif

#nullable restore
        public string? Name { get; set; }

        public object Current
#nullable disable
        {
#nullable restore
            get => Name ?? "";
        }

        public void Run(
#nullable disable
            string oblivious,
#nullable enable
            string? annotated)
        {
            var value =
#if FEATURE
                1
#else
                2
#endif
                ;

#if false
            this is { not valid ( code
    #if nested
            #endif
#endif

            switch (value)
            {
#nullable disable
                case 1:
                    break;
#nullable restore
                default:
                    break;
            }

#line 100 "generated.cs"
            int unused;
#line default
#line hidden
            string text = @"
#if FEATURE
not a directive
#endif
";
            /* #if FEATURE */
            // #endif
#nullable disable
        }

        public T Create<T>()
#nullable disable
            where T : new()
#nullable restore
        {
            return new T();
        }
    }
#endregion

#if FEATURE && (TRACE_ENABLED || false) && true != false
    public class Enabled { }
#endif

#if DEBUG_ONLY
    public class Disabled { }
#endif
}

#pragma warning restore CS0168, CS0219
#nullable restore
