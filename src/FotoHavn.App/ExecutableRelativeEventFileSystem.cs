using System.Text.Json;
using System.Text.Json.Serialization;
using FotoHavn.Core;

namespace FotoHavn.App;

internal sealed class ExecutableRelativeEventFileSystem : IEventFileSystem
{
    private const int CurrentRecordVersion = 1;
    private const int CurrentQuarantineVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
    private readonly string eventsRoot;
    private readonly string quarantineRoot;

    public ExecutableRelativeEventFileSystem()
        : this(Path.Combine(AppContext.BaseDirectory, "Events"))
    {
    }

    internal ExecutableRelativeEventFileSystem(string eventsRoot)
    {
        this.eventsRoot = Path.GetFullPath(eventsRoot);
        quarantineRoot = Path.Combine(this.eventsRoot, ".deletion-quarantine");
    }

    public async Task<IReadOnlyList<SavedEventSummary>> LoadEventsAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(eventsRoot))
        {
            return [];
        }

        var quarantinedIds = (await LoadEventDeletionQuarantinesAsync(cancellationToken))
            .Select(item => item.EventId.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var events = new List<SavedEventSummary>();
        foreach (var eventDirectory in Directory.EnumerateDirectories(eventsRoot, "*", SearchOption.TopDirectoryOnly))
        {
            if (quarantinedIds.Contains(Path.GetFileName(eventDirectory)))
            {
                continue;
            }

            var manifestPath = Path.Combine(eventDirectory, "event.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

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

    public async Task<IReadOnlyList<EventDeletionQuarantine>> LoadEventDeletionQuarantinesAsync(
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(quarantineRoot))
        {
            return [];
        }

        var quarantines = new List<EventDeletionQuarantine>();
        foreach (var quarantinePath in Directory.EnumerateFiles(quarantineRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            await using var stream = File.OpenRead(quarantinePath);
            var manifest = await JsonSerializer.DeserializeAsync<EventDeletionQuarantineManifest>(
                stream,
                JsonOptions,
                cancellationToken);
            if (manifest?.ToQuarantine() is { } quarantine)
            {
                quarantines.Add(quarantine);
            }
        }

        return quarantines;
    }

    public async Task QuarantineEventForDeletionAsync(
        EventDeletionQuarantine quarantine,
        CancellationToken cancellationToken)
    {
        _ = GetEventDirectory(quarantine.EventId);
        Directory.CreateDirectory(quarantineRoot);
        var quarantinePath = GetQuarantinePath(quarantine.EventId);
        var temporaryPath = Path.Combine(quarantineRoot, $".{Guid.NewGuid():N}.tmp");
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
                    EventDeletionQuarantineManifest.From(quarantine),
                    JsonOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, quarantinePath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public Task<EventDeletionResult> DeleteQuarantinedEventAsync(
        EventId eventId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var eventDirectory = GetEventDirectory(eventId);
        var quarantinePath = GetQuarantinePath(eventId);
        return Task.Run(() =>
        {
            try
            {
                if (Directory.Exists(eventDirectory))
                {
                    Directory.Delete(eventDirectory, recursive: true);
                }

                if (Directory.Exists(eventDirectory))
                {
                    return EventDeletionResult.Incomplete;
                }

                File.Delete(quarantinePath);
                return File.Exists(quarantinePath)
                    ? EventDeletionResult.Incomplete
                    : EventDeletionResult.Deleted;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return EventDeletionResult.Incomplete;
            }
        }, cancellationToken);
    }

    private string GetQuarantinePath(EventId eventId) =>
        Path.Combine(quarantineRoot, $"{eventId.Value}.json");

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

    private sealed record EventDeletionQuarantineManifest(
        int Version,
        string EventId,
        string EventName,
        DateTimeOffset LastSavedAt)
    {
        public EventDeletionQuarantine? ToQuarantine() =>
            Version == CurrentQuarantineVersion
                ? new EventDeletionQuarantine(new EventId(EventId), EventName, LastSavedAt)
                : null;

        public static EventDeletionQuarantineManifest From(EventDeletionQuarantine quarantine) =>
            new(
                CurrentQuarantineVersion,
                quarantine.EventId.Value,
                quarantine.EventName,
                quarantine.LastSavedAt);
    }
}
