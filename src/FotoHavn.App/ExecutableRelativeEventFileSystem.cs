using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using FotoHavn.Core;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace FotoHavn.App;

internal sealed class ExecutableRelativeEventFileSystem : IEventFileSystem
{
    private const int CurrentRecordVersion = 1;
    private const int CurrentGuestCycleVersion = 3;
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
        Directory.CreateDirectory(this.eventsRoot);
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

    public async Task<GuestCycleCreateResult> CreateGuestCycleAsync(
        EventId eventId,
        GuestCycleId guestCycleId,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var guestCycleDirectory = GetGuestCycleDirectory(eventId, guestCycleId);
        var guestCyclesDirectory = Path.GetDirectoryName(guestCycleDirectory)
            ?? throw new InvalidOperationException("The Guest Cycle directory has no parent.");
        Directory.CreateDirectory(guestCyclesDirectory);
        var claimPath = Path.Combine(guestCyclesDirectory, $".{guestCycleId.Value}.identity-claim");
        FileStream claim;
        try
        {
            claim = new FileStream(
                claimPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
        }
        catch (IOException) when (File.Exists(claimPath) || Directory.Exists(guestCycleDirectory))
        {
            return GuestCycleCreateResult.IdentityCollision;
        }

        try
        {
            await using (claim)
            {
                await claim.FlushAsync(cancellationToken);
            }

            if (Directory.Exists(guestCycleDirectory))
            {
                return GuestCycleCreateResult.IdentityCollision;
            }

            Directory.CreateDirectory(guestCycleDirectory);
            await SaveGuestCycleManifestAsync(
                guestCycleDirectory,
                new GuestCycleManifest(
                    CurrentGuestCycleVersion,
                    guestCycleId.Value,
                    startedAt,
                    CompletedAt: null,
                    Captures: [],
                    PhotoStrip: null,
                    Interruptions: []),
                cancellationToken).ConfigureAwait(false);
            return GuestCycleCreateResult.Created;
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
            if (interruptedManifest.Captures.Any(capture => capture.FileName == canonicalName) ||
                !await ValidateImageAsync(
                    canonicalPath,
                    BitmapDecoder.JpegDecoderId,
                    frame.Width,
                    frame.Height).ConfigureAwait(false))
            {
                return new CaptureCommitResult(false, new CaptureReference(canonicalPath));
            }

            var existingCapture = await CreateCaptureArtifactAsync(
                canonicalPath,
                canonicalName,
                frame.Width,
                frame.Height,
                cancellationToken).ConfigureAwait(false);
            await SaveGuestCycleManifestAsync(
                guestCycleDirectory,
                interruptedManifest with { Captures = [.. interruptedManifest.Captures, existingCapture] },
                cancellationToken).ConfigureAwait(false);
            return new CaptureCommitResult(true, existingCapture.ToReference(canonicalPath));
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
        var capture = await CreateCaptureArtifactAsync(
            canonicalPath,
            canonicalName,
            frame.Width,
            frame.Height,
            cancellationToken).ConfigureAwait(false);
        await SaveGuestCycleManifestAsync(
            guestCycleDirectory,
            manifest with { Captures = [.. manifest.Captures, capture] },
            cancellationToken).ConfigureAwait(false);
        return new CaptureCommitResult(true, capture.ToReference(canonicalPath));
    }

    public async Task RecordGuestCycleInterruptionAsync(
        EventId eventId,
        GuestCycleId guestCycleId,
        GuestCycleInterruption interruption,
        CancellationToken cancellationToken)
    {
        var guestCycleDirectory = GetGuestCycleDirectory(eventId, guestCycleId);
        var manifest = await LoadGuestCycleManifestAsync(guestCycleDirectory, cancellationToken).ConfigureAwait(false);
        if (manifest.Version != CurrentGuestCycleVersion ||
            manifest.Id != guestCycleId.Value ||
            manifest.CompletedAt is not null ||
            manifest.Captures.Count != interruption.CompletedCaptures)
        {
            throw new InvalidDataException("The interrupted Guest Cycle checkpoint does not match its durable manifest.");
        }

        if (manifest.Interruptions.LastOrDefault() != interruption)
        {
            await SaveGuestCycleManifestAsync(
                guestCycleDirectory,
                manifest with { Interruptions = [.. manifest.Interruptions, interruption] },
                cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<GuestCycleRetryValidation> PrepareGuestCycleRetryAsync(
        EventId eventId,
        GuestCycleId guestCycleId,
        IReadOnlyList<CaptureReference> completedCaptures,
        CancellationToken cancellationToken)
    {
        var guestCycleDirectory = GetGuestCycleDirectory(eventId, guestCycleId);
        if (!Directory.Exists(guestCycleDirectory))
        {
            return GuestCycleRetryValidation.Unrecoverable;
        }

        GuestCycleManifest manifest;
        try
        {
            manifest = await LoadGuestCycleManifestAsync(guestCycleDirectory, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            return GuestCycleRetryValidation.Unrecoverable;
        }

        if (manifest.Version != CurrentGuestCycleVersion ||
            manifest.Id != guestCycleId.Value ||
            manifest.CompletedAt is not null ||
            manifest.Captures.Count != completedCaptures.Count)
        {
            return GuestCycleRetryValidation.Unrecoverable;
        }

        for (var index = 0; index < manifest.Captures.Count; index++)
        {
            var artifact = manifest.Captures[index];
            var expectedName = $"capture-{index + 1}.jpg";
            var path = Path.Combine(guestCycleDirectory, expectedName);
            var inProcess = completedCaptures[index];
            if (artifact.FileName != expectedName ||
                !Path.GetFullPath(inProcess.ArtifactPath).Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase) ||
                artifact.ByteLength != inProcess.ByteLength ||
                artifact.Sha256 != inProcess.Sha256 ||
                artifact.Width != inProcess.Width ||
                artifact.Height != inProcess.Height ||
                !File.Exists(path))
            {
                return GuestCycleRetryValidation.Unrecoverable;
            }

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            if (bytes.LongLength != artifact.ByteLength ||
                !CreateSha256(bytes).Equals(artifact.Sha256, StringComparison.Ordinal) ||
                !await ValidateImageAsync(
                    path,
                    BitmapDecoder.JpegDecoderId,
                    artifact.Width,
                    artifact.Height).ConfigureAwait(false))
            {
                return GuestCycleRetryValidation.Unrecoverable;
            }
        }

        if (manifest.PhotoStrip is { } photoStrip)
        {
            var expectedName = "photo-strip.png";
            var path = Path.Combine(guestCycleDirectory, expectedName);
            if (photoStrip.FileName != expectedName || !File.Exists(path))
            {
                return GuestCycleRetryValidation.Unrecoverable;
            }

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            if (bytes.LongLength != photoStrip.ByteLength ||
                !CreateSha256(bytes).Equals(photoStrip.Sha256, StringComparison.Ordinal) ||
                !await ValidateImageAsync(
                    path,
                    BitmapDecoder.PngDecoderId,
                    photoStrip.Width,
                    photoStrip.Height).ConfigureAwait(false))
            {
                return GuestCycleRetryValidation.Unrecoverable;
            }
        }
        else if (manifest.Interruptions.LastOrDefault()?.Step is
                 GuestCycleInterruptedStep.Preview or GuestCycleInterruptedStep.Completion)
        {
            return GuestCycleRetryValidation.Unrecoverable;
        }

        var nextCaptureName = $"capture-{completedCaptures.Count + 1}.jpg";
        if (File.Exists(Path.Combine(guestCycleDirectory, nextCaptureName)))
        {
            return GuestCycleRetryValidation.Unrecoverable;
        }

        return GuestCycleRetryValidation.Ready;
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
            var canonicalBytes = await File.ReadAllBytesAsync(canonicalPath, cancellationToken).ConfigureAwait(false);
            if (interruptedManifest.PhotoStrip is not null ||
                !canonicalBytes.AsSpan().SequenceEqual(composition.PngBytes.Span) ||
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
                interruptedManifest with
                {
                    PhotoStrip = await CreatePhotoStripArtifactAsync(
                        canonicalPath,
                        canonicalName,
                        composition.Width,
                        composition.Height,
                        cancellationToken).ConfigureAwait(false),
                },
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
            manifest with
            {
                PhotoStrip = await CreatePhotoStripArtifactAsync(
                    canonicalPath,
                    canonicalName,
                    composition.Width,
                    composition.Height,
                    cancellationToken).ConfigureAwait(false),
            },
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

    private static async Task<CaptureArtifactManifest> CreateCaptureArtifactAsync(
        string canonicalPath,
        string canonicalName,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(canonicalPath, cancellationToken).ConfigureAwait(false);
        return new CaptureArtifactManifest(
            canonicalName,
            bytes.LongLength,
            CreateSha256(bytes),
            width,
            height);
    }

    private static async Task<PhotoStripArtifactManifest> CreatePhotoStripArtifactAsync(
        string canonicalPath,
        string canonicalName,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(canonicalPath, cancellationToken).ConfigureAwait(false);
        return new PhotoStripArtifactManifest(
            canonicalName,
            bytes.LongLength,
            CreateSha256(bytes),
            width,
            height);
    }

    private static string CreateSha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static async Task<bool> ValidateImageAsync(
        string path,
        Guid expectedCodecId,
        int expectedWidth,
        int expectedHeight)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
            using var imageStream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(imageStream))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
                writer.DetachStream();
            }
            imageStream.Seek(0);
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
        try
        {
            await using var stream = File.OpenRead(Path.Combine(guestCycleDirectory, "guest-cycle.json"));
            return await JsonSerializer.DeserializeAsync<GuestCycleManifest>(stream, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("The Guest Cycle manifest is invalid.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The Guest Cycle manifest is invalid.", exception);
        }
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

    private sealed record GuestCycleManifest(
        int Version,
        string Id,
        DateTimeOffset StartedAt,
        DateTimeOffset? CompletedAt,
        IReadOnlyList<CaptureArtifactManifest> Captures,
        PhotoStripArtifactManifest? PhotoStrip,
        IReadOnlyList<GuestCycleInterruption> Interruptions);

    private sealed record PhotoStripArtifactManifest(
        string FileName,
        long ByteLength,
        string Sha256,
        int Width,
        int Height);

    private sealed record CaptureArtifactManifest(
        string FileName,
        long ByteLength,
        string Sha256,
        int Width,
        int Height)
    {
        public CaptureReference ToReference(string artifactPath) =>
            new(artifactPath, ByteLength, Sha256, Width, Height);
    }
}
