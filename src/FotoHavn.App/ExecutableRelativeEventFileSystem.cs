using System.Text.Json;
using System.Text.Json.Serialization;
using FotoHavn.Core;
using Windows.Graphics.Imaging;
using Windows.Storage;

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
        return await ProbeDirectoryAsync(eventsRoot, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> ProbeEventStorageAsync(EventId eventId, CancellationToken cancellationToken)
    {
        var eventDirectory = GetEventDirectory(eventId);
        return Directory.Exists(eventDirectory)
            ? ProbeDirectoryAsync(eventDirectory, cancellationToken)
            : Task.FromResult(false);
    }

    private static async Task<bool> ProbeDirectoryAsync(string directory, CancellationToken cancellationToken)
    {
        var probePath = Path.Combine(directory, $".write-probe-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(directory);
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

    public async Task<GuestCycleCreateResult> CreateGuestCycleAsync(
        EventId eventId,
        GuestCycleId guestCycleId,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var guestCycleDirectory = GetGuestCycleDirectory(eventId, guestCycleId);
        if (Directory.Exists(guestCycleDirectory))
        {
            return GuestCycleCreateResult.IdentityCollision;
        }

        Directory.CreateDirectory(guestCycleDirectory);
        var claimPath = Path.Combine(guestCycleDirectory, ".identity-claim");
        var ownsIdentity = false;
        try
        {
            await using (var claim = new FileStream(
                claimPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await claim.FlushAsync(cancellationToken);
            }
            ownsIdentity = true;

            await SaveGuestCycleManifestAsync(
                guestCycleDirectory,
                new GuestCycleManifest(
                    CurrentRecordVersion,
                    guestCycleId.Value,
                    startedAt,
                    CompletedAt: null,
                    Captures: [],
                    PhotoStrip: null),
                cancellationToken).ConfigureAwait(false);
            return GuestCycleCreateResult.Created;
        }
        catch (IOException) when (!ownsIdentity)
        {
            return GuestCycleCreateResult.IdentityCollision;
        }
        finally
        {
            File.Delete(claimPath);
            if (Directory.Exists(guestCycleDirectory) &&
                !File.Exists(Path.Combine(guestCycleDirectory, "guest-cycle.json")) &&
                !Directory.EnumerateFileSystemEntries(guestCycleDirectory).Any())
            {
                Directory.Delete(guestCycleDirectory);
            }
        }
    }

    public async Task<CaptureCommitResult> CommitCaptureAsync(
        EventId eventId,
        GuestCycleId guestCycleId,
        int captureNumber,
        CapturedFrame frame,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(captureNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(captureNumber, 4);
        var guestCycleDirectory = GetGuestCycleDirectory(eventId, guestCycleId);
        var canonicalName = $"capture-{captureNumber}.jpg";
        var canonicalPath = Path.Combine(guestCycleDirectory, canonicalName);
        if (File.Exists(canonicalPath))
        {
            var interruptedManifest = await LoadGuestCycleManifestAsync(guestCycleDirectory, cancellationToken).ConfigureAwait(false);
            if (interruptedManifest.Captures.Contains(canonicalName, StringComparer.Ordinal) ||
                !await ValidateImageAsync(
                    canonicalPath,
                    BitmapDecoder.JpegDecoderId,
                    frame.Width,
                    frame.Height).ConfigureAwait(false))
            {
                return new CaptureCommitResult(false, new CaptureReference(canonicalPath));
            }

            await SaveGuestCycleManifestAsync(
                guestCycleDirectory,
                interruptedManifest with { Captures = [.. interruptedManifest.Captures, canonicalName] },
                cancellationToken).ConfigureAwait(false);
            return new CaptureCommitResult(true, new CaptureReference(canonicalPath));
        }

        var committed = await CommitValidatedImageAsync(
            guestCycleDirectory,
            canonicalPath,
            frame.JpegBytes,
            BitmapDecoder.JpegDecoderId,
            frame.Width,
            frame.Height,
            cancellationToken).ConfigureAwait(false);
        if (!committed)
        {
            return new CaptureCommitResult(false, new CaptureReference(canonicalPath));
        }

        var manifest = await LoadGuestCycleManifestAsync(guestCycleDirectory, cancellationToken).ConfigureAwait(false);
        await SaveGuestCycleManifestAsync(
            guestCycleDirectory,
            manifest with { Captures = [.. manifest.Captures, canonicalName] },
            cancellationToken).ConfigureAwait(false);
        return new CaptureCommitResult(true, new CaptureReference(canonicalPath));
    }

    public async Task<PhotoStripCommitResult> CommitPhotoStripAsync(
        EventId eventId,
        GuestCycleId guestCycleId,
        PhotoStripCompositionResult composition,
        CancellationToken cancellationToken)
    {
        if (!composition.IsAvailable)
        {
            return new PhotoStripCommitResult(false, string.Empty);
        }

        var guestCycleDirectory = GetGuestCycleDirectory(eventId, guestCycleId);
        var canonicalName = "photo-strip.png";
        var canonicalPath = Path.Combine(guestCycleDirectory, canonicalName);
        if (File.Exists(canonicalPath))
        {
            var interruptedManifest = await LoadGuestCycleManifestAsync(guestCycleDirectory, cancellationToken).ConfigureAwait(false);
            if (interruptedManifest.PhotoStrip is not null ||
                !await ValidateImageAsync(
                    canonicalPath,
                    BitmapDecoder.PngDecoderId,
                    composition.Width,
                    composition.Height).ConfigureAwait(false))
            {
                return new PhotoStripCommitResult(false, canonicalPath);
            }

            await SaveGuestCycleManifestAsync(
                guestCycleDirectory,
                interruptedManifest with { PhotoStrip = canonicalName },
                cancellationToken).ConfigureAwait(false);
            return new PhotoStripCommitResult(true, canonicalPath);
        }

        var committed = await CommitValidatedImageAsync(
            guestCycleDirectory,
            canonicalPath,
            composition.PngBytes,
            BitmapDecoder.PngDecoderId,
            composition.Width,
            composition.Height,
            cancellationToken).ConfigureAwait(false);
        if (!committed)
        {
            return new PhotoStripCommitResult(false, canonicalPath);
        }

        var manifest = await LoadGuestCycleManifestAsync(guestCycleDirectory, cancellationToken).ConfigureAwait(false);
        await SaveGuestCycleManifestAsync(
            guestCycleDirectory,
            manifest with { PhotoStrip = canonicalName },
            cancellationToken).ConfigureAwait(false);
        return new PhotoStripCommitResult(true, canonicalPath);
    }

    public async Task CompleteGuestCycleAsync(
        EventId eventId,
        GuestCycleId guestCycleId,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        var guestCycleDirectory = GetGuestCycleDirectory(eventId, guestCycleId);
        var manifest = await LoadGuestCycleManifestAsync(guestCycleDirectory, cancellationToken).ConfigureAwait(false);
        if (manifest.Captures.Count != 4 || manifest.PhotoStrip is null)
        {
            throw new InvalidOperationException("A Guest Cycle can complete only after four Captures and its Photo Strip are committed.");
        }

        await SaveGuestCycleManifestAsync(
            guestCycleDirectory,
            manifest with { CompletedAt = completedAt },
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> CommitValidatedImageAsync(
        string guestCycleDirectory,
        string canonicalPath,
        ReadOnlyMemory<byte> bytes,
        Guid expectedCodecId,
        int expectedWidth,
        int expectedHeight,
        CancellationToken cancellationToken)
    {
        var partialPath = Path.Combine(
            guestCycleDirectory,
            $".{Path.GetFileName(canonicalPath)}-{Guid.NewGuid():N}.partial");
        try
        {
            await using (var stream = new FileStream(
                partialPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (!await ValidateImageAsync(
                partialPath,
                expectedCodecId,
                expectedWidth,
                expectedHeight).ConfigureAwait(false))
            {
                return false;
            }

            try
            {
                File.Move(partialPath, canonicalPath);
                return true;
            }
            catch (IOException) when (File.Exists(canonicalPath))
            {
                return false;
            }
        }
        finally
        {
            File.Delete(partialPath);
        }
    }

    private static async Task<bool> ValidateImageAsync(
        string path,
        Guid expectedCodecId,
        int expectedWidth,
        int expectedHeight)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var imageStream = await file.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(imageStream);
            return decoder.DecoderInformation.CodecId == expectedCodecId &&
                decoder.PixelWidth == expectedWidth &&
                decoder.PixelHeight == expectedHeight;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            InvalidCastException or
            IOException or
            System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    private static async Task<GuestCycleManifest> LoadGuestCycleManifestAsync(
        string guestCycleDirectory,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(Path.Combine(guestCycleDirectory, "guest-cycle.json"));
        return await JsonSerializer.DeserializeAsync<GuestCycleManifest>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("The Guest Cycle manifest is invalid.");
    }

    private static async Task SaveGuestCycleManifestAsync(
        string guestCycleDirectory,
        GuestCycleManifest manifest,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(guestCycleDirectory, "guest-cycle.json");
        var temporaryPath = Path.Combine(guestCycleDirectory, $".guest-cycle-{Guid.NewGuid():N}.tmp");
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
                await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, manifestPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
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

    private string GetGuestCycleDirectory(EventId eventId, GuestCycleId guestCycleId)
    {
        var guestCyclesRoot = Path.Combine(GetEventDirectory(eventId), "GuestCycles");
        var guestCycleDirectory = Path.GetFullPath(Path.Combine(guestCyclesRoot, guestCycleId.Value));
        var expectedPrefix = guestCyclesRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!guestCycleDirectory.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The Guest Cycle identity does not resolve beneath Event storage.");
        }

        return guestCycleDirectory;
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

    private sealed record GuestCycleManifest(
        int Version,
        string Id,
        DateTimeOffset StartedAt,
        DateTimeOffset? CompletedAt,
        IReadOnlyList<string> Captures,
        string? PhotoStrip);
}
