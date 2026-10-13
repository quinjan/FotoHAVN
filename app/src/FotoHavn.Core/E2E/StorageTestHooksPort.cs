#if E2E_HOOKS
using FotoHavn.Core.Hooks;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.E2E;

/// <summary>The E2E-only glue between the host's <see cref="IStorageTestHooks"/> and the brain's hook dispatcher: plain delegation.</summary>
internal sealed class StorageTestHooksPort(IStorageTestHooks hooks) : IStorageHookPort
{
    public void InjectFault(StorageFaultKind kind, StorageFaultScope scope) => hooks.InjectFault(kind, scope);

    public void ClearFault() => hooks.ClearFault();
}
#endif
