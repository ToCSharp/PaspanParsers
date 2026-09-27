using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Corpus.Statements;

public class Statements
{
    public async Task<int> All(int[] items, object gate, IAsyncEnumerable<int> stream)
    {
        ;
        int a = 0, b = 1;
        const int limit = 10;
        var list = new List<int>();
        ref int first = ref items[0];
        ref readonly int second = ref items[1];
        (int x, int y) = (1, 2);
        var (p, q) = (3, 4);
        (a, b) = (b, a);

        if (a > b) a++; else if (a < b) b--; else { }

        while (a < limit) { a++; if (a == 5) continue; if (a == 8) break; }

        do { b++; } while (b < limit);

        for (int i = 0, j = 10; i < j; i++, j--) { }
        for (;;) { break; }

        foreach (var item in items) { }
        foreach (var (k, v) in new Dictionary<int, int>()) { }
        await foreach (var value in stream) { }

        switch (a)
        {
            case 0:
            case 1 when b > 0:
                break;
            case int n and > 100:
                goto default;
            case > 50:
                goto case 0;
            default:
                break;
        }

        try
        {
            throw new InvalidOperationException();
        }
        catch (InvalidOperationException ex) when (ex.Message != null)
        {
        }
        catch (Exception)
        {
            throw;
        }
        catch
        {
        }
        finally
        {
        }

        using (var stream1 = new MemoryStream()) { }
        using (new MemoryStream()) { }
        using var stream2 = new MemoryStream();
        await using var stream3 = new MemoryStream();

        lock (gate) { }

        checked { a = a * 2; }
        unchecked { b = b * 2; }

    label:
        a++;
        if (a < 3) goto label;

        int Local(int value) => value * 2;
        static async Task<T> LocalGeneric<T>(T value) where T : struct { await Task.Yield(); return value; }

        return Local(a);
    }

    public IEnumerable<int> Iterator()
    {
        yield return 1;
        yield break;
    }
}
