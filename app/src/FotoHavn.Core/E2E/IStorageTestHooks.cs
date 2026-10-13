#if E2E_HOOKS
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.E2E;

/// <summary>
/// The host side of the storage fault hooks (compiled only with <c>E2E_HOOKS</c>). The host implements it by driving the
/// Event store's fault-injecting file system; the brain calls it on its executor from <c>test.injectStorageFault</c> and
/// <c>test.clearStorageFault</c>. A method that throws fails the hook with <see cref="TestHookError.HookFailed"/>.
/// </summary>
public interface IStorageTestHooks
{
    /// <summary>Makes the store's file writes fail with <paramref name="kind"/>: once (<see cref="StorageFaultScope.NextWrite"/>) or until cleared.</summary>
    void InjectFault(StorageFaultKind kind, StorageFaultScope scope);

    /// <summary>Removes any injected fault.</summary>
    void ClearFault();
}
#endif
