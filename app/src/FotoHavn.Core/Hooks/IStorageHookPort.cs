using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Hooks;

/// <summary>
/// The brain's view of the storage fault hooks. It mirrors <c>FotoHavn.Core.E2E.IStorageTestHooks</c>, which exists only in
/// E2E builds, so the brain's hook dispatcher compiles (and is unit-tested) in every build. In field builds nothing
/// creates a port and the brain answers every <c>test</c> message with <c>hooksUnavailable</c> before reaching it.
/// </summary>
internal interface IStorageHookPort
{
    void InjectFault(StorageFaultKind kind, StorageFaultScope scope);

    void ClearFault();
}
