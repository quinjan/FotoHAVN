using System.Security.Cryptography;
using FotoHavn.App;
using FotoHavn.Core;
using Xunit;

namespace FotoHavn.WindowsIntegrationTests;

public sealed class GuestStartStorageIntegrationTests
{
    [Fact]
    public async Task Failed_admission_leaves_no_Guest_Cycle_directory_manifest_or_partial_artifact()
    {
        var root = Path.Combine(Path.GetTempPath(), "FotoHAVN-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var now = new DateTimeOffset(2026, 8, 5, 8, 0, 0, TimeSpan.Zero);
            var eventId = new EventId("01989c3a-61d2-7000-8000-000000000029");
            var fileSystem = new ExecutableRelativeEventFileSystem(root);
            await fileSystem.SaveEventAtomicallyAsync(
                new EventConfiguration(
                    eventId,
                    "Summer Party",
                    new CameraBinding("camera-bound", "Booth Camera"),
                    PrinterChoice.NoPrinter,
                    now,
                    now),
                EventSaveMode.CreateNew,
                TestContext.Current.CancellationToken);
            var camera = new AdmissionCamera(now);
            var clock = new MutableClock(now);
            var orchestrator = new EventGuestCycleOrchestrator(
                fileSystem,
                camera,
                new StubCompositor(),
                clock);
            await orchestrator.ExecuteAsync(new LaunchApplication(), TestContext.Current.CancellationToken);
            await orchestrator.ExecuteAsync(new StartSavedEvent(eventId), TestContext.Current.CancellationToken);
            await orchestrator.ExecuteAsync(new ConfirmStartSavedEvent(), TestContext.Current.CancellationToken);
            clock.UtcNow = now.AddSeconds(3);
            var before = Snapshot(root);

            var blocked = await orchestrator.ExecuteAsync(
                new StartGuestCycle(),
                TestContext.Current.CancellationToken);

            Assert.False(blocked.ActiveEvent!.GuestStart.IsStartEnabled);
            Assert.Equal(before, Snapshot(root));
            Assert.Empty(Directory.EnumerateDirectories(root, "guest-cycles", SearchOption.AllDirectories));
            Assert.Empty(Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories));
            Assert.Empty(Directory.EnumerateFiles(root, "*.partial", SearchOption.AllDirectories));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static IReadOnlyList<string> Snapshot(string root) =>
        Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
            .Select(path => Directory.Exists(path)
                ? $"directory:{Path.GetRelativePath(root, path)}"
                : $"file:{Path.GetRelativePath(root, path)}:{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}")
            .Order(StringComparer.Ordinal)
            .ToArray();

    private sealed class AdmissionCamera(DateTimeOffset frameAt) : ICameraBoundary
    {
        public event EventHandler? AvailableCamerasChanged
        {
            add { }
            remove { }
        }

        public IReadOnlyList<AvailableCamera> AvailableCameras { get; } =
            [new AvailableCamera("camera-bound", "Booth Camera", "Port 4")];

        public string? StreamId => StreamHealth.StreamId;

        public CameraStreamHealth StreamHealth { get; private set; } = CameraStreamHealth.Unavailable;

        public Task StartDiscoveryAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<CameraOpenResult> OpenAsync(CameraDeviceId deviceId, CancellationToken cancellationToken)
        {
            StreamHealth = new CameraStreamHealth(deviceId, "stream-1", frameAt, CameraStreamFailure.None);
            return Task.FromResult(CameraOpenResult.Ready);
        }

        public Task ReleaseAsync(CancellationToken cancellationToken)
        {
            StreamHealth = CameraStreamHealth.Unavailable;
            return Task.CompletedTask;
        }
    }

    private sealed class MutableClock(DateTimeOffset now) : IApplicationClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubCompositor : IPhotoStripCompositor
    {
        public Task<PhotoStripCompositionResult> ComposeAsync(
            PhotoStripCompositionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PhotoStripCompositionResult(false, ReadOnlyMemory<byte>.Empty, 0, 0));
    }
}
