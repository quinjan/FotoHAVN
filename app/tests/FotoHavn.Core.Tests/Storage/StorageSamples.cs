using FotoHavn.Core.Storage;

namespace FotoHavn.Core.Tests.Storage;

/// <summary>Sample records for the storage tests.</summary>
internal static class StorageSamples
{
    public static EventRecord Event(string id, string name = "Lindqvist wedding", DateOnly? date = null, string? venue = "Harbour Hall") => new(
        id,
        name,
        date ?? new DateOnly(2026, 10, 24),
        venue,
        "Ana Lindqvist",
        null,
        "camera-1",
        "Studio camera",
        "display-2",
        "Guest display",
        EventRecord.NoPrinter,
        true,
        5,
        "classic-strip",
        "naturale",
        true,
        ["classic-strip", "grid-four"],
        ["naturale", "noir"],
        [new PhaseTimerSetting(PhaseTimerPhase.Template, 60), new PhaseTimerSetting(PhaseTimerPhase.Keepsake, 0)]);

    public static CaptureCommit CaptureCommit(string guestCycleId, int sequence, byte fill = 7) => new(
        guestCycleId,
        sequence,
        Enumerable.Repeat(fill, 64).ToArray(),
        1280,
        720,
        true,
        new DateTimeOffset(2026, 10, 24, 15, 0, sequence, TimeSpan.Zero));

    public static PhotoStripCommit StripCommit(string guestCycleId, IReadOnlyList<string> arrangement) => new(
        guestCycleId,
        "classic-strip",
        "naturale",
        arrangement,
        Enumerable.Repeat((byte)9, 128).ToArray(),
        600,
        1800);
}
