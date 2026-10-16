namespace FotoHavn.Core.Tests.Gallery;

/// <summary>
/// The brain-generated presentation fixtures of the Event Gallery (M9 wave B1): names carry the prefixes <c>operator-gallery-</c>,
/// <c>operator-guest-cycle-detail-</c>, <c>guest-gallery-</c>, <c>guest-strip-detail-</c>, and <c>guest-start-gallery-link</c>, and are the files in
/// <c>ui/src/fixtures/presentations/</c>. <c>PresentationFixtureTests</c> merges this table into its own and checks every file against it; a duplicate
/// name fails. Wave B1 edits only this file for fixtures. Each one is the welcome a freshly connected UI gets from a real brain in that state.
/// </summary>
internal static class GalleryFixtures
{
    public static IReadOnlyDictionary<string, Func<Task<string>>> All { get; } =
        new Dictionary<string, Func<Task<string>>>(StringComparer.Ordinal);
}
