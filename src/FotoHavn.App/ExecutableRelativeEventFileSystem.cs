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

    public async Task<EventConfiguration?> LoadEventAsync(EventId eventId, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(eventsRoot, eventId.Value, "event.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(manifestPath);
        var savedEvent = await JsonSerializer.DeserializeAsync<SavedEventManifest>(stream, JsonOptions, cancellationToken);
        return savedEvent?.ToConfiguration();
    }

    public async Task<bool> ProbeStorageAsync(CancellationToken cancellationToken)
    {
        var probePath = Path.Combine(eventsRoot, $".write-probe-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(eventsRoot);
            await File.WriteAllBytesAsync(probePath, [], cancellationToken);
            File.Delete(probePath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async Task SaveEventAsync(EventConfiguration configuration, CancellationToken cancellationToken)
    {
        var eventDirectory = Path.Combine(eventsRoot, configuration.Id.Value);
        Directory.CreateDirectory(eventDirectory);
        var manifestPath = Path.Combine(eventDirectory, "event.json");
        await using var stream = File.Create(manifestPath);
        await JsonSerializer.SerializeAsync(
            stream,
            SavedEventManifest.From(configuration),
            JsonOptions,
            cancellationToken);
    }

    private sealed record SavedEventManifest(
        string Id,
        string Name,
        DateTimeOffset LastSavedAt,
        string? CameraDeviceId = null,
        string? CameraDisplayName = null,
        string? PrinterId = null)
    {
        public EventConfiguration? ToConfiguration() =>
            CameraDeviceId is null || CameraDisplayName is null
                ? null
                : new EventConfiguration(
                    new EventId(Id),
                    Name,
                    new CameraBinding(CameraDeviceId, CameraDisplayName),
                    PrinterId,
                    LastSavedAt);

        public static SavedEventManifest From(EventConfiguration configuration) =>
            new(
                configuration.Id.Value,
                configuration.Name,
                configuration.LastSavedAt,
                configuration.Camera.DeviceId.Value,
                configuration.Camera.DisplayName,
                configuration.PrinterId);
    }
}
