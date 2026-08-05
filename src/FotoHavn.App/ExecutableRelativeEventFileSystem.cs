using System.Text.Json;
using System.Text.Json.Serialization;
using FotoHavn.Core;

namespace FotoHavn.App;

internal sealed class ExecutableRelativeEventFileSystem : IEventFileSystem
{
    private const int CurrentRecordVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
    private readonly string eventsRoot;

    public ExecutableRelativeEventFileSystem()
        : this(Path.Combine(AppContext.BaseDirectory, "Events"))
    {
    }

    internal ExecutableRelativeEventFileSystem(string eventsRoot)
    {
        this.eventsRoot = Path.GetFullPath(eventsRoot);
    }

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

            if (savedEvent?.ToSummary() is { } summary)
            {
                events.Add(summary);
            }
        }

        return events;
    }

    public async Task<EventConfiguration?> LoadEventAsync(EventId eventId, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(GetEventDirectory(eventId), "event.json");
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

    public async Task<EventSaveResult> SaveEventAtomicallyAsync(
        EventConfiguration configuration,
        EventSaveMode mode,
        CancellationToken cancellationToken)
    {
        var eventDirectory = GetEventDirectory(configuration.Id);
        var claimPath = Path.Combine(eventDirectory, ".identity-claim");
        var ownsNewIdentity = false;
        if (mode == EventSaveMode.CreateNew)
        {
            if (Directory.Exists(eventDirectory))
            {
                return EventSaveResult.IdentityCollision;
            }

            Directory.CreateDirectory(eventDirectory);
            try
            {
                await using var claim = new FileStream(
                    claimPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.WriteThrough);
                ownsNewIdentity = true;
                await claim.FlushAsync(cancellationToken);
            }
            catch (IOException)
            {
                if (ownsNewIdentity)
                {
                    CleanupUncommittedIdentity(eventDirectory, claimPath);
                    throw;
                }

                return EventSaveResult.IdentityCollision;
            }
            catch
            {
                CleanupUncommittedIdentity(eventDirectory, claimPath);
                throw;
            }
        }
        else if (!Directory.Exists(eventDirectory))
        {
            throw new DirectoryNotFoundException($"Event '{configuration.Id}' does not exist.");
        }

        Directory.CreateDirectory(eventDirectory);
        var manifestPath = Path.Combine(eventDirectory, "event.json");
        var temporaryPath = Path.Combine(eventDirectory, $".event-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    SavedEventManifest.From(configuration),
                    JsonOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, manifestPath, overwrite: true);
            return EventSaveResult.Saved;
        }
        finally
        {
            File.Delete(temporaryPath);
            if (ownsNewIdentity && !File.Exists(manifestPath))
            {
                CleanupUncommittedIdentity(eventDirectory, claimPath);
            }
        }
    }

    private static void CleanupUncommittedIdentity(string eventDirectory, string claimPath)
    {
        File.Delete(claimPath);
        if (Directory.Exists(eventDirectory) && !Directory.EnumerateFileSystemEntries(eventDirectory).Any())
        {
            Directory.Delete(eventDirectory);
        }
    }

    public Task DeleteEventAsync(EventId eventId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var eventDirectory = GetEventDirectory(eventId);
        if (Directory.Exists(eventDirectory))
        {
            Directory.Delete(eventDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    private string GetEventDirectory(EventId eventId)
    {
        var eventDirectory = Path.GetFullPath(Path.Combine(eventsRoot, eventId.Value));
        var expectedPrefix = eventsRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!eventDirectory.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The Event identity does not resolve beneath Event storage.");
        }

        return eventDirectory;
    }

    private sealed record SavedEventManifest(
        int Version,
        string Id,
        string Name,
        DateTimeOffset CreatedAt,
        DateTimeOffset LastSavedAt,
        SavedCameraBinding? Camera,
        PrinterChoice Printer)
    {
        public SavedEventSummary? ToSummary() =>
            Version == CurrentRecordVersion && Camera is not null
                ? new SavedEventSummary(new EventId(Id), Name, LastSavedAt)
                : null;

        public EventConfiguration? ToConfiguration() =>
            Version != CurrentRecordVersion || Camera is null
                ? null
                : new EventConfiguration(
                    new EventId(Id),
                    Name,
                    new CameraBinding(Camera.DeviceId, Camera.DisplayName),
                    Printer,
                    CreatedAt,
                    LastSavedAt);

        public static SavedEventManifest From(EventConfiguration configuration) =>
            new(
                CurrentRecordVersion,
                configuration.Id.Value,
                configuration.Name,
                configuration.CreatedAt,
                configuration.LastSavedAt,
                new SavedCameraBinding(configuration.Camera.DeviceId.Value, configuration.Camera.DisplayName),
                configuration.Printer);
    }

    private sealed record SavedCameraBinding(string DeviceId, string DisplayName);
}
