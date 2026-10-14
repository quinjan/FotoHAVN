namespace FotoHavn.Core.Tests.Events.Active;

/// <summary>
/// The brain-generated presentation fixtures M6 wave B1b adds (the running Event, the Guest Screen's Start, exit hold, and PIN
/// states). Names carry the prefix <c>operator-running-</c>, <c>operator-guest-screen-</c>, or <c>guest-</c> and are the files in
/// <c>ui/src/fixtures/presentations/</c>. <c>PresentationFixtureTests</c> merges this table into its own and checks every file
/// against it; a duplicate name fails. Wave B1b edits only this file for fixtures.
/// </summary>
internal static class ActiveFixtures
{
    public static IReadOnlyDictionary<string, Func<Task<string>>> All { get; } =
        new Dictionary<string, Func<Task<string>>>(StringComparer.Ordinal);
}
