using FotoHavn.Core.Storage;

namespace FotoHavn.Core.Tests.Gallery;

/// <summary>Store records for the gallery tests, built with fixed ids, times, and hashes.</summary>
internal static class GalleryKit
{
    public const string EventId = "11111111-1111-4111-8111-111111111111";

    public static readonly DateTimeOffset Noon = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A lowercase-GUID id made from <paramref name="number"/>, so ids sort like the numbers.</summary>
    public static string Id(int number) => $"{number:x8}-0000-4000-8000-000000000000";

    public static string Sha(int number) => $"{number:x8}{new string('a', 56)}";

    public static GuestCycleRecord Cycle(int number, GuestCycleEndReason? reason = GuestCycleEndReason.Completed, int minutesAfterNoon = 0) =>
        new(Id(number), EventId, Noon.AddMinutes(minutesAfterNoon), reason is null ? null : Noon.AddMinutes(minutesAfterNoon + 3), reason);

    public static CaptureRecord Capture(int cycle, int sequence, CaptureState state = CaptureState.Kept) => new(
        $"{cycle:x4}{sequence:x4}-0000-4000-8000-000000000000",
        Id(cycle),
        sequence,
        $"capture-{sequence}.jpg",
        1000,
        Sha(sequence),
        1640,
        1230,
        true,
        Noon,
        state);

    public static PhotoStripRecord Strip(int cycle, IReadOnlyList<CaptureRecord> arrangement, int width = 600, int height = 1800) => new(
        Id(cycle),
        "signature-strip",
        "naturale-v1",
        [.. arrangement.Select(capture => capture.Id)],
        "photo-strip.png",
        2000,
        Sha(cycle),
        width,
        height,
        Noon);

    public static GuestCycleSummary Summary(int number, GuestCycleEndReason? reason = GuestCycleEndReason.Completed, bool withStrip = true, int minutesAfterNoon = 0)
    {
        var cycle = Cycle(number, reason, minutesAfterNoon);
        var captures = Enumerable.Range(1, 4).Select(sequence => Capture(number, sequence)).ToList();
        return new GuestCycleSummary(cycle, captures.Count, withStrip ? Strip(number, captures) : null);
    }

    /// <summary>A cycle with kept Captures 1 to 4 (in sequence order), plus any <paramref name="extra"/> Captures; the strip arranges <paramref name="arranged"/> sequence numbers.</summary>
    public static GuestCycleDetail Detail(int number, GuestCycleEndReason? reason = GuestCycleEndReason.Completed, int[]? arranged = null, bool withStrip = true, params CaptureRecord[] extra)
    {
        var captures = Enumerable.Range(1, 4).Select(sequence => Capture(number, sequence)).Concat(extra).ToList();
        var order = (arranged ?? [3, 1, 4, 2]).Select(sequence => captures.First(capture => capture.Sequence == sequence && capture.State == CaptureState.Kept)).ToList();
        return new GuestCycleDetail(Cycle(number, reason), captures, withStrip ? Strip(number, order) : null);
    }
}
