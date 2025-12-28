// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/* THIS (.cs) FILE IS GENERATED. DO NOT CHANGE IT.
 * CHANGE THE .tt FILE INSTEAD. */

namespace Paspan.Fluent;

public static partial class Parsers
{
    // Commented out to prefer specific overloads for Region and Unit
    //public static Parser<string> AsString<T>(this Parser<T> parser) => new SequenceSkipAnd<T, string>(parser, new StringValue());
        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively and adds Value from reader as string.
        /// </summary>
    public static Parser<(T1, string)> AsString<T1, T2>(this Parser<(T1, T2)> parser)
        => new SequenceSkipAnd<T1, T2, string>(parser, new StringValue());

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively and adds Value from reader as string.
        /// </summary>
    public static Parser<(T1, T2, string)> AsString<T1, T2, T3>(this Parser<(T1, T2, T3)> parser)
        => new SequenceSkipAnd<T1, T2, T3, string>(parser, new StringValue());

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively and adds Value from reader as string.
        /// </summary>
    public static Parser<(T1, T2, T3, string)> AsString<T1, T2, T3, T4>(this Parser<(T1, T2, T3, T4)> parser)
        => new SequenceSkipAnd<T1, T2, T3, T4, string>(parser, new StringValue());

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively and adds Value from reader as string.
        /// </summary>
    public static Parser<(T1, T2, T3, T4, string)> AsString<T1, T2, T3, T4, T5>(this Parser<(T1, T2, T3, T4, T5)> parser)
        => new SequenceSkipAnd<T1, T2, T3, T4, T5, string>(parser, new StringValue());

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively and adds Value from reader as string.
        /// </summary>
    public static Parser<(T1, T2, T3, T4, T5, string)> AsString<T1, T2, T3, T4, T5, T6>(this Parser<(T1, T2, T3, T4, T5, T6)> parser)
        => new SequenceSkipAnd<T1, T2, T3, T4, T5, T6, string>(parser, new StringValue());

        /// <summary>
        /// Builds a parser that ensure the specified parsers match consecutively and adds Value from reader as string.
        /// </summary>
    public static Parser<(T1, T2, T3, T4, T5, T6, string)> AsString<T1, T2, T3, T4, T5, T6, T7>(this Parser<(T1, T2, T3, T4, T5, T6, T7)> parser)
        => new SequenceSkipAnd<T1, T2, T3, T4, T5, T6, T7, string>(parser, new StringValue());


}
