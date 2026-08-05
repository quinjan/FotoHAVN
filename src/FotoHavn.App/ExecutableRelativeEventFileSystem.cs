using System.Text.Json;
using FotoHavn.Core;

namespace FotoHavn.App;

internal sealed class ExecutableRelativeEventFileSystem : IEventFileSystem
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string eventsRoot = Path.Combine(AppContext.BaseDirectory, "Events");

    public async Task<IReadOnlyList<SavedEventSummary>> LoadEventsAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(eventsRoot))
        {
            return [];
        }

        var events = new List<SavedEventSummary>();
        foreach (var manifestPath in Directory.EnumerateFiles(eventsRoot, "event.json", SearchOption.AllDirectories))
        {
            await using var stream = File.OpenRead(manifestPath);
            var savedEvent = await JsonSerializer.DeserializeAsync<SavedEventManifest>(
                stream,
                JsonOptions,
                cancellationToken);

            if (savedEvent is not null)
            {
                events.Add(new SavedEventSummary(
                    new EventId(savedEvent.Id),
                    savedEvent.Name,
                    savedEvent.LastSavedAt));
            }
        }

        return events;
    }

    private sealed record SavedEventManifest(string Id, string Name, DateTimeOffset LastSavedAt);
}
