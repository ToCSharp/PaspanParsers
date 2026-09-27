using System;
using System.Linq;
using System.Threading.Tasks;

namespace Corpus.Lambdas;

public class Lambdas
{
    public void All(int[] numbers, string[] words)
    {
        Func<int, int> square = x => x * x;
        Func<int, int, int> add = (x, y) => x + y;
        Func<int, int, int> typed = (int x, int y) => x + y;
        Action noArgs = () => { };
        Func<Task> asyncLambda = async () => await Task.Delay(1);
        Func<int, Task<int>> asyncSingle = async x => { await Task.Yield(); return x; };
        Func<int, int> staticLambda = static x => x + 1;
        var natural = (int x) => x.ToString();
        var withReturnType = int (string s) => s.Length;
        var withAttribute = [Obsolete] (int x) => x;
        var withDefault = (int x = 5) => x;
        Func<int, int, int> discards = (_, _) => 0;
        Action<int> anonymous = delegate (int x) { };
        Action anonymousNoParams = delegate { };
        var refParam = (ref int x) => x++;

        var query = from n in numbers
                    where n > 0
                    let doubled = n * 2
                    orderby doubled descending, n
                    select new { n, doubled };

        var joined = from n in numbers
                     join w in words on n equals w.Length into grouped
                     from g in grouped
                     select g;

        var groups = from w in words
                     group w by w.Length into byLength
                     where byLength.Count() > 1
                     select byLength.Key;

        var typedFrom = from int n in numbers select n;

        var nested = from a in numbers
                     from b in numbers
                     where a < b
                     select (a, b);
    }
}
