using System.Text.Json;

namespace FotoHavn.App.UiVerification;

internal sealed record UiVerificationCanonicalPresentation(
    string EventId,
    string EventName,
    string LongEventName,
    IReadOnlyList<UiVerificationSavedEvent> SavedEvents)
{
    public UiVerificationPresentationData Primary => new(EventId, EventName);

    public static async Task<UiVerificationCanonicalPresentation> LoadAsync(
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "UiVerification", "canonical-presentation.json");
        await using var stream = File.OpenRead(path);
        var presentation = await JsonSerializer.DeserializeAsync<UiVerificationCanonicalPresentation>(
            stream,
            jsonOptions,
            cancellationToken) ?? throw new InvalidDataException("The canonical UI presentation data is empty.");
        if (presentation.SavedEvents.Count < 6)
        {
            throw new InvalidDataException("The canonical UI presentation must define six Saved Events.");
        }

        return presentation;
    }
}

internal sealed record UiVerificationSavedEvent(
    string EventId,
    string EventName,
    string CompactEventId,
    string SavedMetadata);
