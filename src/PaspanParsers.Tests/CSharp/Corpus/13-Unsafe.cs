using System;
using System.Runtime.InteropServices;

namespace Corpus.Unsafe;

public unsafe struct Buffer
{
    public fixed byte Data[16];
    public int* Pointer;
    public void** DoublePointer;
    public delegate*<int, int> Function;
    public delegate* unmanaged[Cdecl]<int, void> Native;
}

public static unsafe class Pointers
{
    public static int Sum(int[] values)
    {
        int total = 0;
        fixed (int* p = values)
        {
            int* current = p;
            for (int i = 0; i < values.Length; i++)
            {
                total += *current;
                current++;
            }
        }

        Buffer buffer = default;
        Buffer* bp = &buffer;
        bp->Pointer = null;
        int size = sizeof(Buffer);
        int* stack = stackalloc int[4];
        stack[0] = 1;
        nint native = 0;
        nuint unative = 0;
        return total;
    }

    [DllImport("native")]
    public static extern int Call(int value);
}
