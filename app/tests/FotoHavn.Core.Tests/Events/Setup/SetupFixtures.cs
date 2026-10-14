namespace FotoHavn.Core.Tests.Events.Setup;

/// <summary>
/// The brain-generated presentation fixtures M6 wave B1a adds (Event list and Event setup states). Each entry is a name (the file
/// <c>ui/src/fixtures/presentations/&lt;name&gt;.json</c>, with the prefix <c>operator-event-list-</c> or <c>operator-event-setup-</c>)
/// and a function that drives a real brain to that state and returns the message it sent (see
/// <c>PresentationFixtureTests</c> for the pattern). <c>PresentationFixtureTests</c> merges this table into its own, checks every
/// file against it, and fails on a duplicate name. Wave B1a edits only this file for fixtures.
/// </summary>
internal static class SetupFixtures
{
    public static IReadOnlyDictionary<string, Func<Task<string>>> All { get; } =
        new Dictionary<string, Func<Task<string>>>(StringComparer.Ordinal);
}
