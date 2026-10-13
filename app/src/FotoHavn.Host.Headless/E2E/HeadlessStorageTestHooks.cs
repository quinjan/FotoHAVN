#if E2E_HOOKS
using FotoHavn.Core.E2E;
using FotoHavn.Storage.FileSystem;
using Wire = FotoHavn.Protocol.Testing;

namespace FotoHavn.Host.Headless.E2E;

/// <summary>
/// The headless host's storage fault hooks (E2E builds only): they drive the Event store's fault-injecting file system.
/// <see cref="InjectAtStartup"/> arms <c>--e2e-storage-fault</c> before the brain exists, so the store's first write fails.
/// </summary>
internal sealed class HeadlessStorageTestHooks(FaultInjectingStorageFileSystem faults) : IStorageTestHooks
{
    public void InjectFault(Wire.StorageFaultKind kind, Wire.StorageFaultScope scope) => faults.Inject(Map(kind), Map(scope));

    public void ClearFault() => faults.Clear();

    public void InjectAtStartup(Wire.StorageFaultKind kind) => InjectFault(kind, Wire.StorageFaultScope.AllWrites);

    private static StorageFaultKind Map(Wire.StorageFaultKind kind) => kind switch
    {
        Wire.StorageFaultKind.DiskFull => StorageFaultKind.DiskFull,
        Wire.StorageFaultKind.AccessDenied => StorageFaultKind.AccessDenied,
        Wire.StorageFaultKind.Locked => StorageFaultKind.Locked,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a storage fault."),
    };

    private static StorageFaultScope Map(Wire.StorageFaultScope scope) => scope switch
    {
        Wire.StorageFaultScope.NextWrite => StorageFaultScope.NextWrite,
        Wire.StorageFaultScope.AllWrites => StorageFaultScope.AllWrites,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Not a storage fault scope."),
    };
}
#endif
