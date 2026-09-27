using System.Runtime.ExceptionServices;

namespace PaspanParsers;

/// <summary>
/// Runs code whose recursion guards against stack overflow with
/// <see cref="System.Runtime.CompilerServices.RuntimeHelpers.EnsureSufficientExecutionStack"/> again on a
/// thread with a large stack, for input nested too deeply for the caller's stack.
/// </summary>
internal static class LargeStack
{
    /// <summary>
    /// The stack size of the thread (256 MB).
    /// </summary>
    public const int Size = 256 * 1024 * 1024;

    /// <summary>
    /// Runs <paramref name="action"/> on a new thread with a large stack and waits for it; an exception
    /// it throws is rethrown here.
    /// </summary>
    public static void Run(Action action)
    {
        ExceptionDispatchInfo failure = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    failure = ExceptionDispatchInfo.Capture(e);
                }
            },
            Size);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
