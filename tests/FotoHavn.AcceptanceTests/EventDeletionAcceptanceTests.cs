using FotoHavn.Core;
using Xunit;

namespace FotoHavn.AcceptanceTests;

public sealed class EventDeletionAcceptanceTests
{
    [Fact]
    public async Task Event_is_confirmed_kept_visible_while_cleanup_runs_and_removed_only_after_verified_completion()
    {
        var fileSystem = new ControlledDeletionFileSystem(Configuration("event-1", "Summer Party"));
        var orchestrator = CreateOrchestrator(fileSystem);
        await orchestrator.ExecuteAsync(new LaunchApplication(), TestContext.Current.CancellationToken);

        var confirmation = await orchestrator.ExecuteAsync(
            new DeleteSavedEvent(new EventId("event-1")),
            TestContext.Current.CancellationToken);

        Assert.Equal("Delete “Summer Party”?", confirmation.EventDeletion!.Title);
        Assert.Contains("all Guest Cycles, and all photos", confirmation.EventDeletion.Warning);
        Assert.Contains("cannot be recovered", confirmation.EventDeletion.Warning);
        Assert.Equal("Cancel", confirmation.EventDeletion.CancelActionLabel);
        Assert.Equal("Delete Event", confirmation.EventDeletion.PrimaryActionLabel);
        Assert.Contains(confirmation.EventTiles, tile => tile.EventId == new EventId("event-1"));

        var deletion = orchestrator.ExecuteAsync(new ConfirmDeleteSavedEvent(), TestContext.Current.CancellationToken);
        await fileSystem.DeleteStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        var busy = orchestrator.CurrentPresentation;
        Assert.True(busy.EventDeletion!.IsBusy);
        Assert.False(busy.EventDeletion.IsDismissible);
        Assert.Contains("keep FotoHAVN open", busy.EventDeletion.Message);
        Assert.Contains(busy.EventTiles, tile => tile.EventId == new EventId("event-1"));

        fileSystem.AllowDeleteToComplete.SetResult();
        var completed = await deletion;

        Assert.DoesNotContain(completed.EventTiles, tile => tile.EventId == new EventId("event-1"));
        Assert.Equal("Event deleted", completed.EventDeletion!.Title);
        Assert.Equal("Done", completed.EventDeletion.PrimaryActionLabel);
        Assert.Empty(fileSystem.Quarantines);
    }

    [Fact]
    public async Task Partial_deletion_is_quarantined_across_restart_and_retry_is_idempotent_until_eventual_success()
    {
        var fileSystem = new ControlledDeletionFileSystem(Configuration("event-1", "Summer Party"));
        fileSystem.DeletionResults.Enqueue(EventDeletionResult.Incomplete);
        fileSystem.DeletionResults.Enqueue(EventDeletionResult.Incomplete);
        fileSystem.DeletionResults.Enqueue(EventDeletionResult.Deleted);
        fileSystem.AllowDeleteToComplete.SetResult();
        var orchestrator = CreateOrchestrator(fileSystem);
        await orchestrator.ExecuteAsync(new LaunchApplication(), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new DeleteSavedEvent(new EventId("event-1")), TestContext.Current.CancellationToken);

        var failed = await orchestrator.ExecuteAsync(
            new ConfirmDeleteSavedEvent(),
            TestContext.Current.CancellationToken);

        Assert.Equal("Couldn’t finish deleting “Summer Party”", failed.EventDeletion!.Title);
        var quarantined = Assert.Single(failed.EventTiles, tile => tile.EventId == new EventId("event-1"));
        Assert.Equal("Deletion incomplete", quarantined.SupportingText);
        Assert.False(quarantined.ShowsStart);
        Assert.False(quarantined.ShowsEdit);
        Assert.False(quarantined.ShowsDelete);
        Assert.True(quarantined.ShowsRetryDeletion);
        Assert.Single(fileSystem.Quarantines);
        Assert.Empty(fileSystem.Events);

        await orchestrator.ExecuteAsync(new DismissEventDeletionResult(), TestContext.Current.CancellationToken);
        var failedAgain = await orchestrator.ExecuteAsync(
            new RetryEventDeletion(new EventId("event-1")),
            TestContext.Current.CancellationToken);
        Assert.Equal(EventDeletionStage.Incomplete, failedAgain.EventDeletion!.Stage);
        Assert.Single(fileSystem.Quarantines);

        var restarted = CreateOrchestrator(fileSystem);
        var restored = await restarted.ExecuteAsync(new LaunchApplication(), TestContext.Current.CancellationToken);
        var restoredTile = Assert.Single(restored.EventTiles, tile => tile.EventId == new EventId("event-1"));
        Assert.Equal("Summer Party", restoredTile.Label);
        Assert.True(restoredTile.ShowsRetryDeletion);

        var completed = await restarted.ExecuteAsync(
            new RetryEventDeletion(new EventId("event-1")),
            TestContext.Current.CancellationToken);
        Assert.Equal(EventDeletionStage.Deleted, completed.EventDeletion!.Stage);
        Assert.DoesNotContain(completed.EventTiles, tile => tile.EventId == new EventId("event-1"));
        Assert.Empty(fileSystem.Quarantines);
    }

    [Fact]
    public async Task Quarantine_persistence_failure_reports_that_deletion_never_started_and_keeps_the_Event_intact()
    {
        var fileSystem = new ControlledDeletionFileSystem(Configuration("event-1", "Summer Party"))
        {
            AllowQuarantineToComplete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            QuarantineFailuresRemaining = 1,
        };
        fileSystem.AllowDeleteToComplete.SetResult();
        var orchestrator = CreateOrchestrator(fileSystem);
        await orchestrator.ExecuteAsync(new LaunchApplication(), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new DeleteSavedEvent(new EventId("event-1")), TestContext.Current.CancellationToken);

        var confirmation = orchestrator.ExecuteAsync(
            new ConfirmDeleteSavedEvent(),
            TestContext.Current.CancellationToken);
        await fileSystem.QuarantineStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        Assert.True(orchestrator.CurrentPresentation.EventDeletion!.IsBusy);
        Assert.Contains(orchestrator.CurrentPresentation.EventTiles, tile => tile.EventId == new EventId("event-1"));

        fileSystem.AllowQuarantineToComplete.SetResult();
        var failed = await confirmation;

        Assert.Equal("Couldn’t start deleting “Summer Party”", failed.EventDeletion!.Title);
        var intact = Assert.Single(failed.EventTiles, tile => tile.EventId == new EventId("event-1"));
        Assert.True(intact.ShowsStart);
        Assert.True(intact.ShowsEdit);
        Assert.True(intact.ShowsDelete);
        Assert.False(intact.ShowsRetryDeletion);
        Assert.Equal(0, fileSystem.DeleteCount);
        Assert.Single(fileSystem.Events);
        Assert.Empty(fileSystem.Quarantines);

        await orchestrator.ExecuteAsync(new DismissEventDeletionResult(), TestContext.Current.CancellationToken);
        await orchestrator.ExecuteAsync(new DeleteSavedEvent(new EventId("event-1")), TestContext.Current.CancellationToken);
        var completed = await orchestrator.ExecuteAsync(
            new ConfirmDeleteSavedEvent(),
            TestContext.Current.CancellationToken);
        Assert.Equal(EventDeletionStage.Deleted, completed.EventDeletion!.Stage);
        Assert.Equal(1, fileSystem.DeleteCount);
    }

    private static EventGuestCycleOrchestrator CreateOrchestrator(IEventFileSystem fileSystem) =>
        new(fileSystem, new StubCamera(), new StubCompositor(), new StubClock());

    private static EventConfiguration Configuration(string id, string name) =>
        new(
            new EventId(id),
            name,
            new CameraBinding("camera-1", "Booth Camera"),
            PrinterChoice.NoPrinter,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);

    private sealed class ControlledDeletionFileSystem(params EventConfiguration[] events) : IEventFileSystem
    {
        public List<EventConfiguration> Events { get; } = [.. events];
        public List<EventDeletionQuarantine> Quarantines { get; } = [];
        public TaskCompletionSource DeleteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowDeleteToComplete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource QuarantineStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? AllowQuarantineToComplete { get; init; }
        public int QuarantineFailuresRemaining { get; set; }
        public int DeleteCount { get; private set; }
        public Queue<EventDeletionResult> DeletionResults { get; } = [];

        public Task<IReadOnlyList<SavedEventSummary>> LoadEventsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SavedEventSummary>>(Events
                .Select(item => new SavedEventSummary(item.Id, item.Name, item.LastSavedAt))
                .ToArray());

        public Task<EventConfiguration?> LoadEventAsync(EventId eventId, CancellationToken cancellationToken) =>
            Task.FromResult<EventConfiguration?>(Events.SingleOrDefault(item => item.Id == eventId));

        public Task<bool> ProbeStorageAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<EventSaveResult> SaveEventAtomicallyAsync(
            EventConfiguration configuration,
            EventSaveMode mode,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<EventDeletionQuarantine>> LoadEventDeletionQuarantinesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EventDeletionQuarantine>>([.. Quarantines]);

        public async Task QuarantineEventForDeletionAsync(
            EventDeletionQuarantine quarantine,
            CancellationToken cancellationToken)
        {
            QuarantineStarted.TrySetResult();
            if (AllowQuarantineToComplete is not null)
            {
                await AllowQuarantineToComplete.Task.WaitAsync(cancellationToken);
            }

            if (QuarantineFailuresRemaining > 0)
            {
                QuarantineFailuresRemaining--;
                throw new IOException("Controlled quarantine persistence failure.");
            }

            Quarantines.RemoveAll(item => item.EventId == quarantine.EventId);
            Quarantines.Add(quarantine);
        }

        public async Task<EventDeletionResult> DeleteQuarantinedEventAsync(
            EventId eventId,
            CancellationToken cancellationToken)
        {
            DeleteCount++;
            DeleteStarted.TrySetResult();
            await AllowDeleteToComplete.Task.WaitAsync(cancellationToken);
            Events.RemoveAll(item => item.Id == eventId);
            var result = DeletionResults.TryDequeue(out var configured)
                ? configured
                : EventDeletionResult.Deleted;
            if (result == EventDeletionResult.Deleted)
            {
                Quarantines.RemoveAll(item => item.EventId == eventId);
            }

            return result;
        }
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
}
