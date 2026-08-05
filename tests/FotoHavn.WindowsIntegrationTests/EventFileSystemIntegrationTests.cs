using System.Text.Json;
using FotoHavn.App;
using FotoHavn.Core;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Xunit;

namespace FotoHavn.WindowsIntegrationTests;

public sealed class EventFileSystemIntegrationTests
{
    [Fact]
    public async Task Guest_Cycle_commits_only_validated_canonical_artifacts_and_completion_manifest()
    {
        using var directory = new TemporaryDirectory();
        var fileSystem = new ExecutableRelativeEventFileSystem(directory.Path);
        var savedEvent = Configuration(
            new EventId("event-1"),
            "Wedding",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);
        await fileSystem.SaveEventAtomicallyAsync(
            savedEvent,
            EventSaveMode.CreateNew,
            TestContext.Current.CancellationToken);
        var guestCycleId = new GuestCycleId(Guid.CreateVersion7().ToString());
        var jpeg = await EncodeSolidAsync(BitmapEncoder.JpegEncoderId, 6, 4);

        var created = await fileSystem.CreateGuestCycleAsync(
            savedEvent.Id,
            guestCycleId,
            savedEvent.LastSavedAt,
            TestContext.Current.CancellationToken);
        for (var captureNumber = 1; captureNumber <= 4; captureNumber++)
        {
            var committed = await fileSystem.CommitCaptureAsync(
                savedEvent.Id,
                guestCycleId,
                captureNumber,
                new CapturedFrame(captureNumber, savedEvent.LastSavedAt, 6, 4, jpeg),
                TestContext.Current.CancellationToken);
            Assert.True(committed.Committed);
        }

        var png = await EncodeSolidAsync(BitmapEncoder.PngEncoderId, 2, 6);
        var strip = await fileSystem.CommitPhotoStripAsync(
            savedEvent.Id,
            guestCycleId,
            new PhotoStripCompositionResult(true, png, 2, 6),
            TestContext.Current.CancellationToken);
        await fileSystem.CompleteGuestCycleAsync(
            savedEvent.Id,
            guestCycleId,
            savedEvent.LastSavedAt.AddMinutes(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(GuestCycleCreateResult.Created, created);
        Assert.True(strip.Committed);
        var guestCycleDirectory = Path.Combine(directory.Path, savedEvent.Id.Value, "GuestCycles", guestCycleId.Value);
        Assert.Equal(
            ["capture-1.jpg", "capture-2.jpg", "capture-3.jpg", "capture-4.jpg", "guest-cycle.json", "photo-strip.png"],
            Directory.GetFiles(guestCycleDirectory).Select(path => Path.GetFileName(path)!).Order().ToArray());
        var manifest = await File.ReadAllTextAsync(
            Path.Combine(guestCycleDirectory, "guest-cycle.json"),
            TestContext.Current.CancellationToken);
        Assert.Contains("\"completedAt\"", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain(".partial", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_Capture_never_replaces_or_creates_a_canonical_artifact()
    {
        using var directory = new TemporaryDirectory();
        var fileSystem = new ExecutableRelativeEventFileSystem(directory.Path);
        var savedEvent = Configuration(
            new EventId("event-1"),
            "Wedding",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);
        await fileSystem.SaveEventAtomicallyAsync(
            savedEvent,
            EventSaveMode.CreateNew,
            TestContext.Current.CancellationToken);
        var guestCycleId = new GuestCycleId(Guid.CreateVersion7().ToString());
        await fileSystem.CreateGuestCycleAsync(
            savedEvent.Id,
            guestCycleId,
            savedEvent.LastSavedAt,
            TestContext.Current.CancellationToken);

        var committed = await fileSystem.CommitCaptureAsync(
            savedEvent.Id,
            guestCycleId,
            1,
            new CapturedFrame(1, savedEvent.LastSavedAt, 6, 4, new byte[] { 1, 2, 3 }),
            TestContext.Current.CancellationToken);

        Assert.False(committed.Committed);
        var guestCycleDirectory = Path.Combine(directory.Path, savedEvent.Id.Value, "GuestCycles", guestCycleId.Value);
        Assert.Equal(["guest-cycle.json"], Directory.GetFiles(guestCycleDirectory).Select(path => Path.GetFileName(path)!));
    }

    [Fact]
    public async Task Retry_repairs_a_valid_canonical_Capture_missing_from_the_manifest_without_overwriting_it()
    {
        using var directory = new TemporaryDirectory();
        var fileSystem = new ExecutableRelativeEventFileSystem(directory.Path);
        var savedEvent = Configuration(
            new EventId("event-1"),
            "Wedding",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);
        await fileSystem.SaveEventAtomicallyAsync(
            savedEvent,
            EventSaveMode.CreateNew,
            TestContext.Current.CancellationToken);
        var guestCycleId = new GuestCycleId(Guid.CreateVersion7().ToString());
        await fileSystem.CreateGuestCycleAsync(
            savedEvent.Id,
            guestCycleId,
            savedEvent.LastSavedAt,
            TestContext.Current.CancellationToken);
        var guestCycleDirectory = Path.Combine(directory.Path, savedEvent.Id.Value, "GuestCycles", guestCycleId.Value);
        var canonicalPath = Path.Combine(guestCycleDirectory, "capture-1.jpg");
        var canonicalBytes = await EncodeSolidAsync(BitmapEncoder.JpegEncoderId, 6, 4);
        await File.WriteAllBytesAsync(canonicalPath, canonicalBytes, TestContext.Current.CancellationToken);

        var committed = await fileSystem.CommitCaptureAsync(
            savedEvent.Id,
            guestCycleId,
            1,
            new CapturedFrame(1, savedEvent.LastSavedAt, 6, 4, new byte[] { 1, 2, 3 }),
            TestContext.Current.CancellationToken);

        Assert.True(committed.Committed);
        Assert.Equal(canonicalBytes, await File.ReadAllBytesAsync(canonicalPath, TestContext.Current.CancellationToken));
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(guestCycleDirectory, "guest-cycle.json"),
            TestContext.Current.CancellationToken));
        Assert.Equal("capture-1.jpg", manifest.RootElement.GetProperty("captures")[0].GetString());
    }

    [Fact]
    public async Task Concurrent_Guest_Cycle_creation_has_exactly_one_winner_and_preserves_its_manifest()
    {
        using var directory = new TemporaryDirectory();
        var fileSystem = new ExecutableRelativeEventFileSystem(directory.Path);
        var savedEvent = Configuration(
            new EventId("event-1"),
            "Wedding",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);
        await fileSystem.SaveEventAtomicallyAsync(
            savedEvent,
            EventSaveMode.CreateNew,
            TestContext.Current.CancellationToken);
        var guestCycleId = new GuestCycleId(Guid.CreateVersion7().ToString());
        var firstStartedAt = savedEvent.LastSavedAt.AddSeconds(1);
        var secondStartedAt = savedEvent.LastSavedAt.AddSeconds(2);

        var results = await Task.WhenAll(
            fileSystem.CreateGuestCycleAsync(
                savedEvent.Id,
                guestCycleId,
                firstStartedAt,
                TestContext.Current.CancellationToken),
            fileSystem.CreateGuestCycleAsync(
                savedEvent.Id,
                guestCycleId,
                secondStartedAt,
                TestContext.Current.CancellationToken));

        Assert.Single(results, result => result == GuestCycleCreateResult.Created);
        Assert.Single(results, result => result == GuestCycleCreateResult.IdentityCollision);
        var manifestPath = Path.Combine(
            directory.Path,
            savedEvent.Id.Value,
            "GuestCycles",
            guestCycleId.Value,
            "guest-cycle.json");
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            manifestPath,
            TestContext.Current.CancellationToken));
        var startedAt = manifest.RootElement.GetProperty("startedAt").GetDateTimeOffset();
        Assert.Contains(startedAt, new[] { firstStartedAt, secondStartedAt });
    }

    private static async Task<byte[]> EncodeSolidAsync(Guid encoderId, int width, int height)
    {
        using var bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
        var pixels = Enumerable.Repeat(new byte[] { 32, 96, 192, 255 }, width * height)
            .SelectMany(pixel => pixel)
            .ToArray();
        bitmap.CopyFromBuffer(pixels.AsBuffer());
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();
        var result = new byte[checked((int)stream.Size)];
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)result.Length);
        reader.ReadBytes(result);
        return result;
    }

    [Fact]
    public async Task Cancelled_first_save_leaves_no_Event_identity_or_record()
    {
        var root = Path.Combine(Path.GetTempPath(), "FotoHAVN-tests", Guid.NewGuid().ToString("N"));
        var eventId = new EventId("01989c3a-61d2-7000-8000-000000000003");
        try
        {
            var fileSystem = new ExecutableRelativeEventFileSystem(root);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                fileSystem.SaveEventAtomicallyAsync(
                    Configuration(eventId, "Cancelled", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch),
                    EventSaveMode.CreateNew,
                    cancellation.Token));

            Assert.False(Directory.Exists(Path.Combine(root, eventId.Value)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Event_records_are_versioned_atomic_and_isolated_by_full_identity()
    {
        var root = Path.Combine(Path.GetTempPath(), "FotoHAVN-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var fileSystem = new ExecutableRelativeEventFileSystem(root);
            var firstId = new EventId("01989c3a-61d2-7000-8000-000000000001");
            var secondId = new EventId("01989c3a-61d2-7000-8000-000000000002");
            var createdAt = new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero);
            var first = Configuration(firstId, "Duplicate Name", createdAt, createdAt.AddMinutes(5));
            var second = Configuration(secondId, "Duplicate Name", createdAt, createdAt.AddMinutes(10));

            Assert.Equal(
                EventSaveResult.Saved,
                await fileSystem.SaveEventAtomicallyAsync(first, EventSaveMode.CreateNew, TestContext.Current.CancellationToken));
            Assert.Equal(
                EventSaveResult.Saved,
                await fileSystem.SaveEventAtomicallyAsync(second, EventSaveMode.CreateNew, TestContext.Current.CancellationToken));
            Assert.Equal(
                EventSaveResult.IdentityCollision,
                await fileSystem.SaveEventAtomicallyAsync(first, EventSaveMode.CreateNew, TestContext.Current.CancellationToken));

            var firstManifest = Path.Combine(root, firstId.Value, "event.json");
            var secondManifest = Path.Combine(root, secondId.Value, "event.json");
            Assert.True(File.Exists(firstManifest));
            Assert.True(File.Exists(secondManifest));
            Assert.Empty(Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories));

            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(firstManifest, TestContext.Current.CancellationToken));
            var record = json.RootElement;
            Assert.Equal(1, record.GetProperty("version").GetInt32());
            Assert.Equal(firstId.Value, record.GetProperty("id").GetString());
            Assert.Equal("Duplicate Name", record.GetProperty("name").GetString());
            Assert.Equal("camera-1", record.GetProperty("camera").GetProperty("deviceId").GetString());
            Assert.Equal("Booth Camera", record.GetProperty("camera").GetProperty("displayName").GetString());
            Assert.Equal("noPrinter", record.GetProperty("printer").GetString());
            Assert.False(record.TryGetProperty("cameraTuning", out _));
            Assert.False(record.TryGetProperty("photoMode", out _));
            Assert.False(record.TryGetProperty("cameraFormat", out _));
            Assert.False(record.TryGetProperty("captureResolution", out _));
            Assert.False(record.TryGetProperty("framingAcceptance", out _));

            Assert.Equal(first, await fileSystem.LoadEventAsync(firstId, TestContext.Current.CancellationToken));
            Assert.Equal(second, await fileSystem.LoadEventAsync(secondId, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Updating_an_Event_leaves_existing_Guest_Cycle_manifests_and_artifacts_byte_for_byte_unchanged()
    {
        var root = Path.Combine(Path.GetTempPath(), "FotoHAVN-tests", Guid.NewGuid().ToString("N"));
        var eventId = new EventId("01989c3a-61d2-7000-8000-000000000004");
        try
        {
            var fileSystem = new ExecutableRelativeEventFileSystem(root);
            var createdAt = new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero);
            await fileSystem.SaveEventAtomicallyAsync(
                Configuration(eventId, "Summer Party", createdAt, createdAt),
                EventSaveMode.CreateNew,
                TestContext.Current.CancellationToken);
            var guestCycleDirectory = Path.Combine(root, eventId.Value, "guest-cycles", "cycle-1");
            Directory.CreateDirectory(guestCycleDirectory);
            var manifestPath = Path.Combine(guestCycleDirectory, "manifest.json");
            var artifactPath = Path.Combine(guestCycleDirectory, "capture-1.jpg");
            byte[] manifest = [0x7B, 0x22, 0x76, 0x22, 0x3A, 0x31, 0x7D];
            byte[] artifact = [0xFF, 0xD8, 0x00, 0x7F, 0xFF, 0xD9];
            await File.WriteAllBytesAsync(manifestPath, manifest, TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(artifactPath, artifact, TestContext.Current.CancellationToken);

            await fileSystem.SaveEventAtomicallyAsync(
                Configuration(eventId, "Winter Party", createdAt, createdAt.AddHours(1)),
                EventSaveMode.UpdateExisting,
                TestContext.Current.CancellationToken);

            Assert.Equal(manifest, await File.ReadAllBytesAsync(manifestPath, TestContext.Current.CancellationToken));
            Assert.Equal(artifact, await File.ReadAllBytesAsync(artifactPath, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static EventConfiguration Configuration(
        EventId id,
        string name,
        DateTimeOffset createdAt,
        DateTimeOffset lastSavedAt) =>
        new(
            id,
            name,
            new CameraBinding("camera-1", "Booth Camera"),
            PrinterChoice.NoPrinter,
            createdAt,
            lastSavedAt);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "FotoHAVN-tests",
                Guid.NewGuid().ToString("N"));
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
