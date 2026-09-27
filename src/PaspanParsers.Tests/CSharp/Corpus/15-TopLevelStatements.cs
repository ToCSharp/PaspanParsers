using System;
using System.Threading.Tasks;

Console.WriteLine("Hello");
var total = Add(1, 2);
await Task.Delay(1);

if (args.Length > 0)
{
    Console.WriteLine(args[0]);
}

return total;

static int Add(int a, int b) => a + b;

record Message(string Text);
