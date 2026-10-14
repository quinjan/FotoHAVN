using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FotoHavn.Core.Storage;
using FotoHavn.Storage.Tests.Crash;
using FotoHavn.Storage.Tests.Support;
using Microsoft.Data.Sqlite;

namespace FotoHavn.Storage.Tests.Reconciliation;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StartupTimingDefinition
{
    public const string Name = "Startup timing";
}

/// <summary>
/// Plan section 10 hashes every kept Capture and Photo Strip at startup, so start-up cost grows with the Data folder. This
/// times <c>OpenAsync</c> (migrations, integrity check, the whole reconciler) on a large Event: 300 Guest Cycles of four
/// 200 KB Captures and a 1 MB Photo Strip each (about 540 MB). It runs alone so other tests don't slow its disk.
/// </summary>
[Collection(StartupTimingDefinition.Name)]
public sealed class StartupTimingTests
{
    private const int Cycles = 300;
    private const int CapturesPerCycle = 4;
    private const int CaptureBytes = 200 * 1024;
    private const int StripBytes = 1024 * 1024;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Opening_a_store_with_300_Guest_Cycles_hashes_every_file_within_ten_seconds()
    {
        using var scratch = new ScratchFolder();
        var data = scratch.Combine("Data");
        await SeedAsync(data);

        var clock = Stopwatch.StartNew();
        var (store, opened) = await RealStore.OpenAsync(data);
        clock.Stop();
        await using (store)
        {
            var message = string.Create(
                CultureInfo.InvariantCulture,
                $"OpenAsync with {Cycles} cycles ({Cycles * CapturesPerCycle} Captures, {Cycles} Photo Strips, {(Cycles * ((CapturesPerCycle * CaptureBytes) + StripBytes)) / (1024 * 1024)} MiB): {clock.Elapsed.TotalSeconds:0.00} s");
            TestContext.Current.TestOutputHelper?.WriteLine(message);
            TestContext.Current.SendDiagnosticMessage(message);

            Assert.False(opened.Reconcile.HasRepairs, "The seeded data was meant to be healthy.");
            Assert.True(clock.Elapsed < Budget, $"{message}. The budget is {Budget.TotalSeconds:0} s; report it instead of weakening the hashing rule.");
        }
    }

    private static async Task SeedAsync(string data)
    {
        var @event = Sample.Event(name: "Large Event");
        await using (var store = RealStore.Create(data))
        {
            await store.OpenAsync(CancellationToken.None);
            await store.CreateEventAsync(@event, CancellationToken.None);
        }

        var captureBytes = Enumerable.Range(0, CapturesPerCycle).Select(index => Payload(CaptureBytes, index)).ToArray();
        var stripBytes = Payload(StripBytes, 99);
        var captureHashes = captureBytes.Select(bytes => Convert.ToHexStringLower(SHA256.HashData(bytes))).ToArray();
        var stripHash = Convert.ToHexStringLower(SHA256.HashData(stripBytes));
        var started = new DateTimeOffset(2026, 10, 24, 10, 0, 0, TimeSpan.Zero);

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(data, SqliteEventStore.DatabaseFileName), Pooling = false }.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction();
        for (var cycleIndex = 0; cycleIndex < Cycles; cycleIndex++)
        {
            var cycleId = StorageIds.NewId();
            var cycleFolder = CrashDataFolder.CycleFolder(data, @event.Id, cycleId);
            Directory.CreateDirectory(cycleFolder);
            var cycleStart = started.AddMinutes(cycleIndex);
            Execute(connection, transaction, "INSERT INTO guest_cycles (id, event_id, started_at, ended_at, end_reason) VALUES ($id, $event, $started, $ended, 'completed')", ("$id", cycleId), ("$event", @event.Id), ("$started", StorageTimestamp.Format(cycleStart)), ("$ended", StorageTimestamp.Format(cycleStart.AddMinutes(1))));

            var captureIds = new List<string>();
            for (var index = 0; index < CapturesPerCycle; index++)
            {
                var captureId = StorageIds.NewId();
                captureIds.Add(captureId);
                var fileName = $"capture-{index + 1}.jpg";
                File.WriteAllBytes(Path.Combine(cycleFolder, fileName), captureBytes[index]);
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO captures (id, guest_cycle_id, sequence, file_name, byte_length, sha256, width, height, mirrored, taken_at, state) VALUES ($id, $cycle, $sequence, $file, $length, $sha, 1920, 1080, 1, $taken, 'kept')",
                    ("$id", captureId),
                    ("$cycle", cycleId),
                    ("$sequence", index + 1),
                    ("$file", fileName),
                    ("$length", (long)captureBytes[index].Length),
                    ("$sha", captureHashes[index]),
                    ("$taken", StorageTimestamp.Format(cycleStart)));
            }

            File.WriteAllBytes(Path.Combine(cycleFolder, "photo-strip.png"), stripBytes);
            Execute(
                connection,
                transaction,
                "INSERT INTO photo_strips (guest_cycle_id, template_id, look_id, arrangement, file_name, byte_length, sha256, width, height, committed_at) VALUES ($cycle, 'signature-strip', 'naturale-v1', $arrangement, 'photo-strip.png', $length, $sha, 600, 1800, $committed)",
                ("$cycle", cycleId),
                ("$arrangement", JsonSerializer.Serialize(captureIds)),
                ("$length", (long)stripBytes.Length),
                ("$sha", stripHash),
                ("$committed", StorageTimestamp.Format(cycleStart.AddMinutes(1))));
        }

        transaction.Commit();
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }

    /// <summary>Distinct, incompressible bytes standing in for an encoded image.</summary>
    private static byte[] Payload(int length, int seed)
    {
        var bytes = new byte[length];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }
}
