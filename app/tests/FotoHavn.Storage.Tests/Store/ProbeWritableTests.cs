using FotoHavn.Core.Storage;
using FotoHavn.Storage.FileSystem;
using FotoHavn.Storage.Tests.Support;

namespace FotoHavn.Storage.Tests.Store;

public sealed class ProbeWritableTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<StorageFaultKind, StorageErrorKind, string, StorageFaultScope> Cases()
    {
        var data = new TheoryData<StorageFaultKind, StorageErrorKind, string, StorageFaultScope>();
        (StorageFaultKind, StorageErrorKind)[] kinds =
        [
            (StorageFaultKind.DiskFull, StorageErrorKind.DiskFull),
            (StorageFaultKind.AccessDenied, StorageErrorKind.AccessDenied),
            (StorageFaultKind.Locked, StorageErrorKind.Locked),
        ];
        foreach (var (fault, kind) in kinds)
        {
            foreach (var stage in new[] { "CreateDirectory", "WriteAllBytesAndFlush", "Delete" })
            {
                foreach (var scope in new[] { StorageFaultScope.NextWrite, StorageFaultScope.AllWrites })
                {
                    data.Add(fault, kind, stage, scope);
                }
            }
        }

        return data;
    }

    private static string[] StagingFiles(StoreHarness harness) => StoreHarness.Files(Path.Combine(harness.DataRoot, ".staging"));

    [Fact]
    public async Task A_probe_writes_flushes_and_deletes_a_small_file_under_the_staging_area_of_the_Data_folder()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var stages = new List<string>();
        harness.Fs.WriteStage = (stage, _) => stages.Add(stage);

        await harness.Store.ProbeWritableAsync(Ct);

        var write = Assert.Single(harness.Fs.Calls, call => call.Operation == nameof(IStorageFileSystem.WriteAllBytesAndFlush));
        Assert.Equal(Path.Combine(harness.DataRoot, ".staging"), Path.GetDirectoryName(write.Path));
        Assert.EndsWith(".tmp", write.Path, StringComparison.Ordinal);
        Assert.Equal(["written", "flushed"], stages);
        Assert.Contains(harness.Fs.Calls, call => call.Operation == nameof(IStorageFileSystem.Delete) && call.Path == write.Path);
        Assert.Empty(StagingFiles(harness));
        Assert.DoesNotContain(harness.Fs.Calls, call => call.Path.StartsWith(Path.Combine(harness.DataRoot, "Events"), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Every_probe_reuses_one_file_name_so_a_leftover_never_piles_up_and_nothing_stays_behind()
    {
        await using var harness = await StoreHarness.OpenedAsync();

        await harness.Store.ProbeWritableAsync(Ct);
        await harness.Store.ProbeWritableAsync(Ct);

        var writes = harness.Fs.Calls.Where(call => call.Operation == nameof(IStorageFileSystem.WriteAllBytesAndFlush)).Select(call => call.Path).ToArray();
        Assert.Equal(2, writes.Length);
        Assert.Single(writes.Distinct(StringComparer.Ordinal));
        Assert.Empty(StagingFiles(harness));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task A_probe_fault_maps_the_kind_and_a_later_probe_works_once_it_clears(StorageFaultKind fault, StorageErrorKind kind, string stage, StorageFaultScope scope)
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var armed = false;
        harness.Fs.Before = (operation, _) =>
        {
            if (!armed && operation == stage)
            {
                armed = true;
                harness.Faults.Inject(fault, scope);
            }
        };

        var failure = await Assert.ThrowsAsync<StorageException>(() => harness.Store.ProbeWritableAsync(Ct));

        Assert.Equal(kind, failure.Kind);
        Assert.DoesNotContain(harness.DataRoot, failure.Message, StringComparison.Ordinal);
        if (scope == StorageFaultScope.NextWrite)
        {
            Assert.Empty(StagingFiles(harness));
        }

        harness.Faults.Clear();
        harness.Fs.Before = null;
        await harness.Store.ProbeWritableAsync(Ct);
        Assert.Empty(StagingFiles(harness));
    }

    [Fact]
    public async Task A_probe_before_the_store_is_open_throws_InvalidOperation()
    {
        await using var harness = new StoreHarness();

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Store.ProbeWritableAsync(Ct));
    }

    [Fact]
    public async Task A_probe_with_a_cancelled_token_is_cancelled_and_writes_nothing()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var callsBefore = harness.Fs.Calls.Count;
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Store.ProbeWritableAsync(cancelled.Token));

        Assert.Equal(callsBefore, harness.Fs.Calls.Count);
    }
}
