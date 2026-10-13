using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Storage.FileSystem;
using FotoHavn.Storage.Reconciliation;

namespace FotoHavn.Storage.Tests.Reconciliation;

/// <summary>Shared plumbing for the reconciler tests.</summary>
internal static class ReconcilerKit
{
    public static readonly DateTimeOffset Now = new(2026, 10, 25, 8, 30, 0, TimeSpan.Zero);

    private static readonly string[] Tables = ["events", "event_templates", "event_looks", "event_phase_timers", "guest_cycles", "captures", "photo_strips"];

    public static string NowText => StorageTimestamp.Format(Now);

    public static async Task<ReconcileReport> RunAsync(SeededDataFolder folder, IStorageFileSystem? fs = null, CancellationToken? ct = null) =>
        await new StartupReconciler(new FakeClock(Now)).RunAsync(folder.Connection, fs ?? folder.Fs, folder.DataRoot, ct ?? TestContext.Current.CancellationToken);

    /// <summary>Rows per table, to compare two runs of the same seed.</summary>
    public static IReadOnlyList<long> RowCounts(SeededDataFolder folder) =>
        Tables
            .Select(folder.Count)
            .ToList();
}
