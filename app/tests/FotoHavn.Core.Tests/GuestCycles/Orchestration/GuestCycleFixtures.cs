namespace FotoHavn.Core.Tests.GuestCycles.Orchestration;

/// <summary>
/// The brain-generated presentation fixtures of the Guest Cycle (M8 wave B1): names carry the prefixes <c>guest-cycle-</c>,
/// <c>guest-assistance-</c>, and <c>operator-running-assistance</c>, and are the files in <c>ui/src/fixtures/presentations/</c>.
/// <c>PresentationFixtureTests</c> merges this table into its own and checks every file against it; a duplicate name fails. Wave B1 edits
/// only this file for fixtures. Each one is the welcome a freshly connected UI gets from a real brain in that state.
/// </summary>
internal static class GuestCycleFixtures
{
    public static IReadOnlyDictionary<string, Func<Task<string>>> All { get; } =
        new Dictionary<string, Func<Task<string>>>(StringComparer.Ordinal);
}
