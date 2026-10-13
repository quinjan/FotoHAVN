using FotoHavn.Core.Displays;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FotoHavn.Core;

/// <summary>Everything the brain needs from its host. Later milestones add the Event store, camera, and compositor.</summary>
/// <param name="Executor">The single-threaded executor the brain runs on. The host owns and disposes it.</param>
/// <param name="Clock">Time and timers.</param>
/// <param name="Displays">Display enumeration and change notifications.</param>
/// <param name="Host">What the brain asks the host to do.</param>
public sealed record BrainDependencies(IBrainExecutor Executor, IClock Clock, IDisplayAdapter Displays, IHostServices Host)
{
    public ILogger Logger { get; init; } = NullLogger.Instance;

#if E2E_HOOKS
    /// <summary>The host side of the test hooks. Null makes every host hook fail with <c>notSupported</c>.</summary>
    public E2E.IHostTestHooks? HostTestHooks { get; init; }
#endif
}

/// <summary>
/// The brain. Wave B of M2 implements it in this file: the handshake, role-checked command dispatch, per-role
/// presentations with increasing revisions, and the brain-side test hooks, all on <see cref="BrainDependencies.Executor"/>.
/// </summary>
public sealed class Brain : IBrain
{
    private readonly BrainDependencies _dependencies;

    public Brain(BrainDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        _dependencies = dependencies;
    }

    public IBrainConnection Connect(Protocol.Role role, Action<string> send) =>
        throw new NotImplementedException($"The brain arrives in M2 wave B; {_dependencies.GetType().Name} is ready for it.");

    public void Dispose()
    {
    }
}
