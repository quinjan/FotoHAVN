using System.Runtime.CompilerServices;

namespace FotoHavn.Core.Tests.Support;

/// <summary>
/// Many tests wait on work the brain's stores and compositor finish on the thread pool while the test thread polls. Under a
/// coverage run, with every test class in parallel, the pool injects new threads slowly and those waits starve past their
/// deadline. Starting the pool with plenty of threads removes the starvation; it changes nothing the tests assert.
/// </summary>
internal static class ThreadPoolWarmup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        ThreadPool.GetMinThreads(out var workers, out var io);
        ThreadPool.SetMinThreads(Math.Max(workers, 128), Math.Max(io, 128));
    }
}
