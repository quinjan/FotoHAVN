using System.Text.Json;
using FotoHavn.App;
using FotoHavn.Core;
using Xunit;

namespace FotoHavn.WindowsIntegrationTests;

public sealed class EventFileSystemIntegrationTests
{
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
}
