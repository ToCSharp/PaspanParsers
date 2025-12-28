// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/* THIS (.cs) FILE IS GENERATED. DO NOT CHANGE IT.
 * CHANGE THE .tt FILE INSTEAD. */

namespace Paspan.Fluent
{
    public static partial class Parsers
    {
        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2)> And<T1, T2>(this Parser<T1> parser, Parser<T2> and)
            => new Sequence<T1, T2>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3)> And<T1, T2, T3>(this Parser<(T1, T2)> parser, Parser<T3> and)
            => new Sequence<T1, T2, T3>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4)> And<T1, T2, T3, T4>(this Parser<(T1, T2, T3)> parser, Parser<T4> and)
            => new Sequence<T1, T2, T3, T4>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4, T5)> And<T1, T2, T3, T4, T5>(this Parser<(T1, T2, T3, T4)> parser, Parser<T5> and)
            => new Sequence<T1, T2, T3, T4, T5>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4, T5, T6)> And<T1, T2, T3, T4, T5, T6>(this Parser<(T1, T2, T3, T4, T5)> parser, Parser<T6> and)
            => new Sequence<T1, T2, T3, T4, T5, T6>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4, T5, T6, T7)> And<T1, T2, T3, T4, T5, T6, T7>(this Parser<(T1, T2, T3, T4, T5, T6)> parser, Parser<T7> and)
            => new Sequence<T1, T2, T3, T4, T5, T6, T7>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4, T5, T6, T7, T8)> And<T1, T2, T3, T4, T5, T6, T7, T8>(this Parser<(T1, T2, T3, T4, T5, T6, T7)> parser, Parser<T8> and)
            => new Sequence<T1, T2, T3, T4, T5, T6, T7, T8>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9)> And<T1, T2, T3, T4, T5, T6, T7, T8, T9>(this Parser<(T1, T2, T3, T4, T5, T6, T7, T8)> parser, Parser<T9> and)
            => new Sequence<T1, T2, T3, T4, T5, T6, T7, T8, T9>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)> And<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9)> parser, Parser<T10> and)
            => new Sequence<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11)> And<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(this Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)> parser, Parser<T11> and)
            => new Sequence<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12)> And<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(this Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11)> parser, Parser<T12> and)
            => new Sequence<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(parser, and);

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13)> And<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(this Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12)> parser, Parser<T13> and)
            => new Sequence<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(parser, and);


        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, Labelled<T2>)> And<T1, T2>(this Parser<T1> parser, string label, Parser<T2> and)
            => new Sequence<T1, Labelled<T2>>(parser, and.Labelled(label));

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, Labelled<T3>)> And<T1, T2, T3>(this Parser<(T1, T2)> parser, string label, Parser<T3> and)
            => new Sequence<T1, T2, Labelled<T3>>(parser, and.Labelled(label));

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, Labelled<T4>)> And<T1, T2, T3, T4>(this Parser<(T1, T2, T3)> parser, string label, Parser<T4> and)
            => new Sequence<T1, T2, T3, Labelled<T4>>(parser, and.Labelled(label));

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4, Labelled<T5>)> And<T1, T2, T3, T4, T5>(this Parser<(T1, T2, T3, T4)> parser, string label, Parser<T5> and)
            => new Sequence<T1, T2, T3, T4, Labelled<T5>>(parser, and.Labelled(label));

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4, T5, Labelled<T6>)> And<T1, T2, T3, T4, T5, T6>(this Parser<(T1, T2, T3, T4, T5)> parser, string label, Parser<T6> and)
            => new Sequence<T1, T2, T3, T4, T5, Labelled<T6>>(parser, and.Labelled(label));

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4, T5, T6, Labelled<T7>)> And<T1, T2, T3, T4, T5, T6, T7>(this Parser<(T1, T2, T3, T4, T5, T6)> parser, string label, Parser<T7> and)
            => new Sequence<T1, T2, T3, T4, T5, T6, Labelled<T7>>(parser, and.Labelled(label));

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4, T5, T6, T7, Labelled<T8>)> And<T1, T2, T3, T4, T5, T6, T7, T8>(this Parser<(T1, T2, T3, T4, T5, T6, T7)> parser, string label, Parser<T8> and)
            => new Sequence<T1, T2, T3, T4, T5, T6, T7, Labelled<T8>>(parser, and.Labelled(label));

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4, T5, T6, T7, T8, Labelled<T9>)> And<T1, T2, T3, T4, T5, T6, T7, T8, T9>(this Parser<(T1, T2, T3, T4, T5, T6, T7, T8)> parser, string label, Parser<T9> and)
            => new Sequence<T1, T2, T3, T4, T5, T6, T7, T8, Labelled<T9>>(parser, and.Labelled(label));

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, Labelled<T10>)> And<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9)> parser, string label, Parser<T10> and)
            => new Sequence<T1, T2, T3, T4, T5, T6, T7, T8, T9, Labelled<T10>>(parser, and.Labelled(label));

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, Labelled<T11>)> And<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(this Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)> parser, string label, Parser<T11> and)
            => new Sequence<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, Labelled<T11>>(parser, and.Labelled(label));

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, Labelled<T12>)> And<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(this Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11)> parser, string label, Parser<T12> and)
            => new Sequence<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, Labelled<T12>>(parser, and.Labelled(label));

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively.
        /// </summary>
        public static Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, Labelled<T13>)> And<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(this Parser<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12)> parser, string label, Parser<T13> and)
            => new Sequence<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, Labelled<T13>>(parser, and.Labelled(label));


    }
}
