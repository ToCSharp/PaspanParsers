// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/* THIS (.cs) FILE IS GENERATED. DO NOT CHANGE IT.
 * CHANGE THE .tt FILE INSTEAD. */

namespace Paspan.Common;

public static class TuplesExt
{
    public static (T1, T2, T3) Append<T1, T2, T3>(this (T1, T2) tuple, T3 value)
        => (tuple.Item1, tuple.Item2, value);

    public static (T1, T2, T3, T4) Append<T1, T2, T3, T4>(this (T1, T2, T3) tuple, T4 value)
        => (tuple.Item1, tuple.Item2, tuple.Item3, value);

    public static (T1, T2, T3, T4, T5) Append<T1, T2, T3, T4, T5>(this (T1, T2, T3, T4) tuple, T5 value)
        => (tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, value);

    public static (T1, T2, T3, T4, T5, T6) Append<T1, T2, T3, T4, T5, T6>(this (T1, T2, T3, T4, T5) tuple, T6 value)
        => (tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, value);

    public static (T1, T2, T3, T4, T5, T6, T7) Append<T1, T2, T3, T4, T5, T6, T7>(this (T1, T2, T3, T4, T5, T6) tuple, T7 value)
        => (tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, value);

    public static (T1, T2, T3, T4, T5, T6, T7, T8) Append<T1, T2, T3, T4, T5, T6, T7, T8>(this (T1, T2, T3, T4, T5, T6, T7) tuple, T8 value)
        => (tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, tuple.Item7, value);

    public static (T1, T2, T3, T4, T5, T6, T7, T8, T9) Append<T1, T2, T3, T4, T5, T6, T7, T8, T9>(this (T1, T2, T3, T4, T5, T6, T7, T8) tuple, T9 value)
        => (tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, tuple.Item7, tuple.Item8, value);

    public static (T1, T2, T3, T4, T5, T6, T7, T8, T9, T10) Append<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(this (T1, T2, T3, T4, T5, T6, T7, T8, T9) tuple, T10 value)
        => (tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, tuple.Item7, tuple.Item8, tuple.Item9, value);

    public static (T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11) Append<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(this (T1, T2, T3, T4, T5, T6, T7, T8, T9, T10) tuple, T11 value)
        => (tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, tuple.Item7, tuple.Item8, tuple.Item9, tuple.Item10, value);

    public static (T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12) Append<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(this (T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11) tuple, T12 value)
        => (tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, tuple.Item7, tuple.Item8, tuple.Item9, tuple.Item10, tuple.Item11, value);


}
