using FotoHavn.Core;
using Xunit;

namespace FotoHavn.AcceptanceTests;

public sealed class LaunchAcceptanceTests
{
    [Fact]
    public async Task Launch_with_no_persisted_events_shows_only_the_New_Event_tile()
    {
        var orchestrator = CreateOrchestrator();

        var state = await orchestrator.ExecuteAsync(
            new LaunchApplication(),
            TestContext.Current.CancellationToken);

        Assert.Equal("Choose an Event", state.Heading);
        var tile = Assert.Single(state.EventTiles);
        Assert.Equal("New Event", tile.Label);
        Assert.Equal(EventTileKind.NewEvent, tile.Kind);
        Assert.Null(state.EmptyStateMessage);
    }

    [Fact]
    public async Task Launch_uses_the_fixed_non_reflowing_booth_canvas()
    {
        var orchestrator = CreateOrchestrator();

        var state = await orchestrator.ExecuteAsync(
            new LaunchApplication(),
            TestContext.Current.CancellationToken);

        Assert.Equal(1280, state.Canvas.Width);
        Assert.Equal(720, state.Canvas.Height);
        Assert.False(state.Canvas.AllowsReflow);
    }

    [Fact]
    public async Task Printer_default_selection_before_Setup_opens_is_ignored()
    {
        var orchestrator = CreateOrchestrator();
        await orchestrator.ExecuteAsync(
            new LaunchApplication(),
            TestContext.Current.CancellationToken);

        var state = await orchestrator.ExecuteAsync(
            new SelectNoPrinter(),
            TestContext.Current.CancellationToken);

        Assert.Null(state.Setup);
        Assert.Equal("Choose an Event", state.Heading);
    }

    [Fact]
    public async Task Application_commands_are_serialized()
    {
        var fileSystem = new BlockingFileSystem();
        var orchestrator = CreateOrchestrator(fileSystem);
        var cancellationToken = TestContext.Current.CancellationToken;

        var firstCommand = orchestrator.ExecuteAsync(new LaunchApplication(), cancellationToken);
        await fileSystem.FirstReadStarted.Task.WaitAsync(cancellationToken);

        var secondCommand = orchestrator.ExecuteAsync(new LaunchApplication(), cancellationToken);
        await Task.Delay(100, cancellationToken);

        Assert.Equal(1, fileSystem.ReadCount);
        fileSystem.AllowReadsToComplete.TrySetResult();
        await Task.WhenAll(firstCommand, secondCommand);
        Assert.Equal(1, fileSystem.MaximumConcurrentReads);
    }

    private static EventGuestCycleOrchestrator CreateOrchestrator(IEventFileSystem? fileSystem = null) =>
        new(fileSystem ?? new StubFileSystem(), new StubCamera(), new StubCompositor(), new StubClock());

    private sealed class BlockingFileSystem : IEventFileSystem
    {
        private int activeReads;
        private int maximumConcurrentReads;
        private int readCount;

        public TaskCompletionSource FirstReadStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllowReadsToComplete { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int MaximumConcurrentReads => Volatile.Read(ref maximumConcurrentReads);

        public int ReadCount => Volatile.Read(ref readCount);

        public async Task<IReadOnlyList<SavedEventSummary>> LoadEventsAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref readCount);
            var concurrentReads = Interlocked.Increment(ref activeReads);
            InterlockedExtensions.Max(ref maximumConcurrentReads, concurrentReads);
            FirstReadStarted.TrySetResult();

            try
            {
                await AllowReadsToComplete.Task.WaitAsync(cancellationToken);
                return [];
            }
            finally
            {
                Interlocked.Decrement(ref activeReads);
            }
        }

        public Task<EventConfiguration?> LoadEventAsync(EventId eventId, CancellationToken cancellationToken) =>
            Task.FromResult<EventConfiguration?>(null);

        public Task<bool> ProbeStorageAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<EventSaveResult> SaveEventAtomicallyAsync(
            EventConfiguration configuration,
            EventSaveMode mode,
            CancellationToken cancellationToken) => Task.FromResult(EventSaveResult.Saved);

        public Task<IReadOnlyList<EventDeletionQuarantine>> LoadEventDeletionQuarantinesAsync(
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EventDeletionQuarantine>>([]);
        public Task QuarantineEventForDeletionAsync(
            EventDeletionQuarantine quarantine,
            CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<EventDeletionResult> DeleteQuarantinedEventAsync(
            EventId eventId,
            CancellationToken cancellationToken) => Task.FromResult(EventDeletionResult.Deleted);
    }

    private sealed class StubCamera : ICameraBoundary
    {
        public event EventHandler? AvailableCamerasChanged { add { } remove { } }
        public IReadOnlyList<AvailableCamera> AvailableCameras => [];
        public string? StreamId => null;
        public Task StartDiscoveryAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<CameraOpenResult> OpenAsync(CameraDeviceId deviceId, CancellationToken cancellationToken) =>
            Task.FromResult(CameraOpenResult.Unavailable);
        public Task ReleaseAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubCompositor : IPhotoStripCompositor
    {
        public Task<PhotoStripCompositionResult> ComposeAsync(
            PhotoStripCompositionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PhotoStripCompositionResult(false, ReadOnlyMemory<byte>.Empty, 0, 0));
    }

    private sealed class StubClock : IApplicationClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubFileSystem : IEventFileSystem
    {
        public Task<IReadOnlyList<SavedEventSummary>> LoadEventsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SavedEventSummary>>([]);

        public Task<EventConfiguration?> LoadEventAsync(EventId eventId, CancellationToken cancellationToken) =>
            Task.FromResult<EventConfiguration?>(null);

        public Task<bool> ProbeStorageAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<EventSaveResult> SaveEventAtomicallyAsync(
            EventConfiguration configuration,
            EventSaveMode mode,
            CancellationToken cancellationToken) => Task.FromResult(EventSaveResult.Saved);

        public Task<IReadOnlyList<EventDeletionQuarantine>> LoadEventDeletionQuarantinesAsync(
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EventDeletionQuarantine>>([]);
        public Task QuarantineEventForDeletionAsync(
            EventDeletionQuarantine quarantine,
            CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<EventDeletionResult> DeleteQuarantinedEventAsync(
            EventId eventId,
            CancellationToken cancellationToken) => Task.FromResult(EventDeletionResult.Deleted);
    }
}

internal static class InterlockedExtensions
{
    public static void Max(ref int location, int value)
    {
        var current = Volatile.Read(ref location);
        while (current < value)
        {
            var observed = Interlocked.CompareExchange(ref location, value, current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }
}
