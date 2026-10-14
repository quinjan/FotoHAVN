using System.Diagnostics;
using System.Text.Json;
using FotoHavn.Core;
using FotoHavn.Core.Storage;
using FotoHavn.Storage;
using FotoHavn.Storage.DataPath;
using FotoHavn.Storage.FileSystem;
using FotoHavn.Storage.Reconciliation;

// The crash harness (see the project file). Usage:
//   FotoHavn.Storage.CrashHarness --data <dir> --scenario <capture|strip|eventDelete|cycleDelete> --crash-at <step|none>
//                                 [--event <id>] [--cycle <id>]
// Exit codes: 0 finished without crashing, 2 bad arguments, 3 the operation failed. A crash at the step writes
// crash-marker.json beside the Data folder, then ends the process at once (no cleanup, no flushing), so the exit code
// is whatever the operating system gives a killed process.
try
{
    var options = HarnessOptions.Parse(args);
    return await HarnessRun.RunAsync(options);
}
catch (HarnessUsageException exception)
{
    await Console.Error.WriteLineAsync(exception.Message);
    return 2;
}
#pragma warning disable CA1031 // The harness reports any failure of the operation as exit code 3.
catch (Exception exception)
#pragma warning restore CA1031
{
    await Console.Error.WriteLineAsync(exception.ToString());
    return 3;
}

internal sealed class HarnessUsageException(string message) : Exception(message);

internal sealed record HarnessOptions(string DataRoot, string Scenario, string CrashAt, string? EventId, string? CycleId)
{
    public const string NoCrash = "none";

    public static string[] Scenarios { get; } = ["capture", "strip", "eventDelete", "cycleDelete"];

    public static HarnessOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
            {
                throw new HarnessUsageException($"Bad argument '{args[index]}'.");
            }

            values[args[index]] = args[index + 1];
        }

        string Required(string name) => values.TryGetValue(name, out var value) ? value : throw new HarnessUsageException($"Missing {name}.");

        var scenario = Required("--scenario");
        if (!Scenarios.Contains(scenario, StringComparer.Ordinal))
        {
            throw new HarnessUsageException($"Unknown scenario '{scenario}'.");
        }

        var crashAt = Required("--crash-at");
        if (crashAt != NoCrash && !CrashPoint.All.Contains(crashAt))
        {
            throw new HarnessUsageException($"Unknown crash point '{crashAt}'.");
        }

        return new HarnessOptions(
            Path.GetFullPath(Required("--data")),
            scenario,
            crashAt,
            values.GetValueOrDefault("--event"),
            values.GetValueOrDefault("--cycle"));
    }
}

/// <summary>
/// Ends the process at one named step, after leaving a marker file beside the Data folder. The marker lists every step reached
/// so far, so a test can tell a crash at <c>staged</c> (before the flush) from one at <c>flushed</c> (after it).
/// </summary>
internal sealed class KillAtStep(string step, string markerPath, Func<string?> cycleId) : ICrashPoints
{
    private readonly List<string> _reached = [];

    public void Reached(string stepName)
    {
        _reached.Add(stepName);
        if (stepName != step)
        {
            return;
        }

        File.WriteAllText(markerPath, JsonSerializer.Serialize(new { step = stepName, cycleId = cycleId(), reached = _reached }));
        Process.GetCurrentProcess().Kill();
        Thread.Sleep(Timeout.Infinite);
    }
}

internal static class HarnessRun
{
    private static readonly DateTimeOffset TakenAt = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    public static async Task<int> RunAsync(HarnessOptions options)
    {
        var markerPath = Path.Combine(Path.GetDirectoryName(options.DataRoot)!, "crash-marker.json");
        string? cycleId = options.CycleId;
        ICrashPoints crash = options.CrashAt == HarnessOptions.NoCrash
            ? NoCrashPoints.Instance
            : new KillAtStep(options.CrashAt, markerPath, () => cycleId);

        var clock = SystemClock.Instance;
        await using var store = new SqliteEventStore(
            options.DataRoot,
            clock,
            new PhysicalStorageFileSystem(options.DataRoot),
            new WindowsDataPathProbe(),
            new StartupReconciler(clock),
            crashPoints: crash);
        await store.OpenAsync(CancellationToken.None);

        var created = new List<string>();
        switch (options.Scenario)
        {
            case "capture":
            {
                cycleId = (await store.StartGuestCycleAsync(Required(options.EventId, "--event"), CancellationToken.None)).Id;
                created.Add((await CommitCaptureAsync(store, cycleId, 1)).Id);
                break;
            }

            case "strip":
            {
                cycleId = (await store.StartGuestCycleAsync(Required(options.EventId, "--event"), CancellationToken.None)).Id;
                var first = await CommitCaptureAsync(store, cycleId, 1);
                var second = await CommitCaptureAsync(store, cycleId, 2);
                created.AddRange([first.Id, second.Id]);
                await store.CommitPhotoStripAsync(
                    new PhotoStripCommit(cycleId, "signature-strip", "naturale-v1", [first.Id, second.Id], Bytes(1_000_000, 9), 600, 1800),
                    CancellationToken.None);
                break;
            }

            case "eventDelete":
                await store.MarkEventDeletingAsync(Required(options.EventId, "--event"), CancellationToken.None);
                await store.DrainAsync(CancellationToken.None);
                break;

            case "cycleDelete":
                await store.MarkGuestCycleDeletingAsync(Required(options.CycleId, "--cycle"), CancellationToken.None);
                await store.DrainAsync(CancellationToken.None);
                break;
        }

        Console.Out.WriteLine(JsonSerializer.Serialize(new { scenario = options.Scenario, cycleId, created }));
        return 0;
    }

    private static string Required(string? value, string name) => value ?? throw new HarnessUsageException($"Missing {name}.");

    private static Task<CaptureRecord> CommitCaptureAsync(SqliteEventStore store, string guestCycleId, int sequence) =>
        store.CommitCaptureAsync(new CaptureCommit(guestCycleId, sequence, Bytes(200_000, sequence), 1920, 1080, true, TakenAt), CancellationToken.None);

    /// <summary>Synthetic artifact bytes: the store hashes them but never decodes them.</summary>
    private static byte[] Bytes(int length, int seed)
    {
        var bytes = new byte[length];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }
}
