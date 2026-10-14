using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Storage;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core.Tests.Brains;

public sealed class StorageTests
{
    private const string OlderEventId = "11111111-1111-4111-8111-111111111111";
    private const string MiddleEventId = "22222222-2222-4222-8222-222222222222";
    private const string NewestEventId = "33333333-3333-4333-8333-333333333333";

    public static TheoryData<StorageErrorKind, StorageErrorReason> EveryErrorKind =>
    [
        (StorageErrorKind.DiskFull, StorageErrorReason.DiskFull),
        (StorageErrorKind.AccessDenied, StorageErrorReason.AccessDenied),
        (StorageErrorKind.Locked, StorageErrorReason.Locked),
        (StorageErrorKind.NotLocalDisk, StorageErrorReason.NotLocalDisk),
        (StorageErrorKind.CloudSynced, StorageErrorReason.CloudSynced),
        (StorageErrorKind.NotWritable, StorageErrorReason.NotWritable),
        (StorageErrorKind.IntegrityFailed, StorageErrorReason.IntegrityFailed),
        (StorageErrorKind.Corrupt, StorageErrorReason.Corrupt),
        (StorageErrorKind.NotFound, StorageErrorReason.Unknown),
        (StorageErrorKind.Conflict, StorageErrorReason.Unknown),
        (StorageErrorKind.Unknown, StorageErrorReason.Unknown),
    ];

    public static async Task<InMemoryEventStore> SeededStoreAsync()
    {
        var store = new InMemoryEventStore();
        await store.OpenAsync(CancellationToken.None);
        await store.CreateEventAsync(StorageSamples.Event(OlderEventId, "Older party", new DateOnly(2026, 3, 1), venue: null), CancellationToken.None);
        await store.CreateEventAsync(StorageSamples.Event(NewestEventId, "Lindqvist wedding", new DateOnly(2026, 10, 24)), CancellationToken.None);
        await store.CreateEventAsync(StorageSamples.Event(MiddleEventId, "Harbour gala", new DateOnly(2026, 6, 12), "Harbour Hall"), CancellationToken.None);
        var cycle = await store.StartGuestCycleAsync(NewestEventId, CancellationToken.None);
        var capture = await store.CommitCaptureAsync(StorageSamples.CaptureCommit(cycle.Id, 1), CancellationToken.None);
        await store.CommitPhotoStripAsync(StorageSamples.StripCommit(cycle.Id, [capture.Id]), CancellationToken.None);
        return store;
    }

    [Fact]
    public async Task Startup_lists_the_stored_Events_newest_date_first()
    {
        using var harness = new BrainHarness(store: await SeededStoreAsync());
        var client = harness.Connect(Role.Operator);

        client.Hello();
        harness.Run();

        var welcome = client.Single<OperatorWelcomeMessage>();
        Assert.Equal(2, welcome.Revision);
        Assert.Equal(OperatorSurface.EventList, welcome.Presentation.Surface);
        Assert.Null(welcome.Presentation.StorageError);
        var rows = welcome.Presentation.EventList!.Events;
        Assert.Equal([NewestEventId, MiddleEventId, OlderEventId], rows.Select(row => row.Id));
        Assert.Equal(("Lindqvist wedding", new DateOnly(2026, 10, 24), "Harbour Hall", 1), (rows[0].Name, rows[0].Date, rows[0].Venue, rows[0].CompletedGuestCycles));
        Assert.NotNull(rows[0].LastUsedAt);
        Assert.Null(rows[1].LastUsedAt);
        Assert.Null(rows[2].Venue);
        Assert.All(rows, row => Assert.False(row.Running));
    }

    [Fact]
    public void When_the_store_answers_late_the_operator_gets_the_Events_and_the_guest_nothing()
    {
        var store = new ScriptedStore();
        using var harness = new BrainHarness(store: store);
        var @operator = harness.Handshake(Role.Operator);
        var guest = harness.Handshake(Role.Guest);

        store.Open.SetResult(new StorageOpenResult(1, ReconcileReport.Empty));
        store.Events.SetResult([new EventSummary(NewestEventId, "Lindqvist wedding", new DateOnly(2026, 10, 24), null, 0, null)]);
        harness.Run();

        var presentation = @operator.Single<OperatorPresentationMessage>();
        Assert.Equal(2, presentation.Revision);
        Assert.Equal(NewestEventId, Assert.Single(presentation.Presentation.EventList!.Events).Id);
        Assert.Empty(guest.Raw);
    }

    [Fact]
    public void The_operator_welcome_before_the_store_answers_is_the_Starting_surface_at_revision_1()
    {
        var store = new ScriptedStore();
        using var harness = new BrainHarness(store: store);
        var client = harness.Connect(Role.Operator);

        client.Hello();
        harness.Run();

        var welcome = client.Single<OperatorWelcomeMessage>();
        Assert.Equal(1, welcome.Revision);
        Assert.Equal(OperatorSurface.Starting, welcome.Presentation.Surface);
        Assert.Null(welcome.Presentation.EventList);
        Assert.Null(welcome.Presentation.StorageError);
        Assert.Equal(1, store.OpenCalls);
    }

    [Fact]
    public void The_operator_sees_Starting_first_and_then_the_Event_list_when_the_store_opens()
    {
        var store = new ScriptedStore();
        using var harness = new BrainHarness(store: store);
        var client = harness.Connect(Role.Operator);
        client.Hello();
        harness.Run();

        store.Open.SetResult(new StorageOpenResult(1, ReconcileReport.Empty));
        store.Events.SetResult([]);
        harness.Run();

        var surfaces = client.Messages.Select(message => message switch
        {
            OperatorWelcomeMessage welcome => (welcome.Revision, welcome.Presentation.Surface),
            OperatorPresentationMessage presentation => (presentation.Revision, presentation.Presentation.Surface),
            _ => throw new InvalidOperationException("Unexpected message."),
        });
        Assert.Equal([(1L, OperatorSurface.Starting), (2L, OperatorSurface.EventList)], surfaces);
    }

    [Fact]
    public void The_operator_sees_Starting_first_and_then_the_storage_error_when_the_store_cannot_open()
    {
        var store = new ScriptedStore();
        using var harness = new BrainHarness(store: store);
        var client = harness.Connect(Role.Operator);
        client.Hello();
        harness.Run();

        store.Open.SetException(new StorageException(StorageErrorKind.AccessDenied, "No access."));
        harness.Run();

        var presentation = client.Messages.OfType<OperatorPresentationMessage>().Single();
        Assert.Equal(2, presentation.Revision);
        Assert.Equal(OperatorSurface.StorageError, presentation.Presentation.Surface);
        Assert.Equal(StorageErrorReason.AccessDenied, presentation.Presentation.StorageError!.Kind);
    }

    [Fact]
    public void The_Starting_surface_carries_no_content_and_a_retry_is_not_available_there()
    {
        var store = new ScriptedStore();
        using var harness = new BrainHarness(store: store);
        var client = harness.Connect(Role.Operator);
        client.Hello();
        var id = client.Command("RetryStorage");
        harness.Run();

        var welcome = client.Messages.OfType<OperatorWelcomeMessage>().Single();
        Assert.Equal(new OperatorPresentation(OperatorSurface.Starting, null, null, null, OperatorOverlays.None, null, null, null), welcome.Presentation);
        Assert.Contains(CommandResultMessage.Rejected(id, RejectReason.NotAvailable), client.Messages);
        Assert.Equal(1, store.OpenCalls);
    }

    [Theory]
    [MemberData(nameof(EveryErrorKind))]
    public void A_store_that_cannot_open_shows_the_storage_error_with_the_mapped_kind(StorageErrorKind kind, StorageErrorReason reason)
    {
        var store = new InMemoryEventStore { DataRoot = @"D:\Booth\Data" };
        store.FailOpen(kind);
        using var harness = new BrainHarness(store: store);
        var client = harness.Connect(Role.Operator);

        client.Hello();
        harness.Run();

        var welcome = client.Single<OperatorWelcomeMessage>();
        Assert.Equal(2, welcome.Revision);
        Assert.Equal(OperatorSurface.StorageError, welcome.Presentation.Surface);
        Assert.Equal(new StorageErrorView(reason, @"D:\Booth\Data"), welcome.Presentation.StorageError);
        Assert.Null(welcome.Presentation.EventList);
        Assert.Null(welcome.Presentation.RunningEvent);
    }

    [Fact]
    public void A_storage_failure_leaves_the_guest_presentation_alone()
    {
        var store = new InMemoryEventStore();
        store.FailOpen(StorageErrorKind.DiskFull);
        using var harness = new BrainHarness(store: store);
        var client = harness.Connect(Role.Guest);

        client.Hello();
        harness.Run();

        var welcome = client.Single<GuestWelcomeMessage>();
        Assert.Equal(1, welcome.Revision);
        Assert.Equal(GuestSurface.Start, welcome.Presentation.Surface);
    }

    [Fact]
    public void A_failure_when_listing_the_Events_is_a_storage_error_too()
    {
        var store = new InMemoryEventStore();
        store.FailNext(StorageErrorKind.Locked);
        using var harness = new BrainHarness(store: store);
        var client = harness.Connect(Role.Operator);

        client.Hello();
        harness.Run();

        Assert.Equal(StorageErrorReason.Locked, client.Single<OperatorWelcomeMessage>().Presentation.StorageError!.Kind);
    }

    [Fact]
    public void An_unexpected_store_exception_is_a_storage_error_of_unknown_kind_and_is_logged()
    {
        var store = new ScriptedStore();
        store.Open.SetException(new InvalidOperationException("Not opened."));
        using var harness = new BrainHarness(store: store);
        var client = harness.Connect(Role.Operator);

        client.Hello();
        harness.Run();

        Assert.Equal(StorageErrorReason.Unknown, client.Single<OperatorWelcomeMessage>().Presentation.StorageError!.Kind);
        Assert.Contains(harness.Logger.Entries, entry => entry.Level == LogLevel.Error && entry.Exception is InvalidOperationException);
    }

    [Fact]
    public void A_store_that_throws_before_returning_a_task_is_a_storage_error()
    {
        using var harness = new BrainHarness(store: new ThrowingStore());
        var client = harness.Connect(Role.Operator);

        client.Hello();
        harness.Run();

        Assert.Equal(StorageErrorReason.Corrupt, client.Single<OperatorWelcomeMessage>().Presentation.StorageError!.Kind);
    }

    [Fact]
    public void Retry_opens_the_store_again_and_shows_the_Event_list_on_success()
    {
        var store = new InMemoryEventStore();
        store.FailOpen(StorageErrorKind.CloudSynced);
        using var harness = new BrainHarness(store: store);
        var client = harness.Handshake(Role.Operator);
        store.ClearFailures();

        var id = client.Command("RetryStorage");
        harness.Run();

        Assert.Equal(2, store.OpenCalls);
        var messages = client.Messages;
        Assert.Equal(CommandResultMessage.Accepted(id), messages[0]);
        var presentation = Assert.IsType<OperatorPresentationMessage>(messages[1]);
        Assert.Equal(3, presentation.Revision);
        Assert.Equal(OperatorSurface.EventList, presentation.Presentation.Surface);
        Assert.Null(presentation.Presentation.StorageError);
        Assert.Equal(2, messages.Count);
    }

    [Fact]
    public void Retry_that_fails_with_another_kind_shows_the_new_kind()
    {
        var store = new InMemoryEventStore();
        store.FailOpen(StorageErrorKind.CloudSynced);
        using var harness = new BrainHarness(store: store);
        var client = harness.Handshake(Role.Operator);
        store.FailOpen(StorageErrorKind.AccessDenied);

        var id = client.Command("RetryStorage");
        harness.Run();

        var messages = client.Messages;
        Assert.Equal(CommandResultMessage.Accepted(id), messages[0]);
        var presentation = Assert.IsType<OperatorPresentationMessage>(messages[1]);
        Assert.Equal(3, presentation.Revision);
        Assert.Equal(StorageErrorReason.AccessDenied, presentation.Presentation.StorageError!.Kind);
    }

    [Fact]
    public void Retry_that_fails_the_same_way_changes_nothing_the_operator_sees()
    {
        var store = new InMemoryEventStore();
        store.FailOpen(StorageErrorKind.Locked);
        using var harness = new BrainHarness(store: store);
        var client = harness.Handshake(Role.Operator);

        var id = client.Command("RetryStorage");
        harness.Run();

        client.AssertReceived(CommandResultMessage.Accepted(id));
        Assert.Equal(2, store.OpenCalls);
    }

    [Fact]
    public void Retry_can_be_repeated_until_it_works()
    {
        var store = new InMemoryEventStore();
        store.FailOpen(StorageErrorKind.NotWritable);
        using var harness = new BrainHarness(store: store);
        var client = harness.Handshake(Role.Operator);

        client.Command("RetryStorage");
        harness.Run();
        store.ClearFailures();
        client.Command("RetryStorage");
        harness.Run();
        var again = client.Command("RetryStorage");
        harness.Run();

        Assert.Equal(CommandResultMessage.Rejected(again, RejectReason.NotAvailable), client.Messages[^1]);
        Assert.Equal(3, store.OpenCalls);
    }

    [Fact]
    public void Retry_is_not_available_while_the_Event_list_shows()
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);

        var id = client.Command("RetryStorage");
        harness.Run();

        Assert.Equal(CommandResultMessage.Rejected(id, RejectReason.NotAvailable), client.Single());
        Assert.Equal(1, ((InMemoryEventStore)harness.Store).OpenCalls);
    }

    [Fact]
    public void Retry_is_not_available_after_the_brain_has_failed()
    {
        var store = new InMemoryEventStore();
        store.FailOpen(StorageErrorKind.Corrupt);
        using var harness = new BrainHarness(store: store);
        var client = harness.Handshake(Role.Operator);
        harness.Brain.ShowFatal("FOTOHAVN stopped.");
        harness.Run();
        client.Clear();

        var id = client.Command("RetryStorage");
        harness.Run();

        Assert.Equal(CommandResultMessage.Rejected(id, RejectReason.NotAvailable), client.Single());
    }

    [Fact]
    public void Retry_from_the_guest_is_the_wrong_role()
    {
        var store = new InMemoryEventStore();
        store.FailOpen(StorageErrorKind.Corrupt);
        using var harness = new BrainHarness(store: store);
        var guest = harness.Handshake(Role.Guest);

        var id = guest.Command("RetryStorage");
        harness.Run();

        Assert.Equal(CommandResultMessage.Rejected(id, RejectReason.WrongRole), guest.Single());
        Assert.Equal(1, store.OpenCalls);
    }

    [Fact]
    public void A_second_retry_while_one_is_in_flight_is_not_available()
    {
        var store = new ScriptedStore();
        store.Open.SetException(new StorageException(StorageErrorKind.Locked, "Locked."));
        using var harness = new BrainHarness(store: store);
        var client = harness.Handshake(Role.Operator);
        store.ResetOpen();

        var first = client.Command("RetryStorage");
        var second = client.Command("RetryStorage");
        harness.Run();

        Assert.Equal(2, store.OpenCalls);
        Assert.Equal([CommandResultMessage.Accepted(first), CommandResultMessage.Rejected(second, RejectReason.NotAvailable)], client.Messages);

        store.Open.SetResult(new StorageOpenResult(1, ReconcileReport.Empty));
        store.Events.SetResult([]);
        harness.Run();

        var presentation = client.Messages.OfType<OperatorPresentationMessage>().Single();
        Assert.Equal(OperatorSurface.EventList, presentation.Presentation.Surface);
    }

    [Fact]
    public void Revisions_only_increase_through_a_failure_two_retries_and_a_success()
    {
        var store = new InMemoryEventStore();
        store.FailOpen(StorageErrorKind.DiskFull);
        using var harness = new BrainHarness(store: store);
        var client = harness.Handshake(Role.Operator);

        store.FailOpen(StorageErrorKind.AccessDenied);
        client.Command("RetryStorage");
        harness.Run();
        store.FailOpen(StorageErrorKind.Locked);
        client.Command("RetryStorage");
        harness.Run();
        store.ClearFailures();
        client.Command("RetryStorage");
        harness.Run();

        var revisions = client.Messages.OfType<OperatorPresentationMessage>().Select(message => message.Revision).ToList();
        Assert.Equal([3L, 4L, 5L], revisions);
    }

    [Fact]
    public void A_disposed_brain_cancels_the_open_and_ignores_its_answer()
    {
        var store = new ScriptedStore();
        var harness = new BrainHarness(store: store);
        var client = harness.Handshake(Role.Operator);

        harness.Dispose();
        Assert.True(store.LastToken.IsCancellationRequested);
        store.Open.SetResult(new StorageOpenResult(1, ReconcileReport.Empty));
        store.Events.SetResult([]);
        harness.Run();

        Assert.Empty(client.Raw);
    }

    [Fact]
    public void Diagnostics_follow_the_store_from_opening_to_error_to_ready()
    {
        var store = new ScriptedStore();
        using var harness = new BrainHarness(hooks: true, store: store);
        var client = harness.Connect(Role.Operator);

        client.Test(TestHookNames.GetDiagnostics);
        harness.Run();
        var opening = Diagnostics(client).Storage;

        store.Open.SetException(new StorageException(StorageErrorKind.IntegrityFailed, "Bad."));
        harness.Run();
        client.Clear();
        client.Test(TestHookNames.GetDiagnostics);
        harness.Run();
        var error = Diagnostics(client).Storage;

        Assert.Equal(new StorageDiagnostics(StorageDiagnosticsState.Opening, null, null, null), opening);
        Assert.Equal(new StorageDiagnostics(StorageDiagnosticsState.Error, StorageErrorReason.IntegrityFailed, null, null), error);
    }

    [Fact]
    public void Diagnostics_after_a_successful_open_report_the_schema_version_and_reconcile_counts()
    {
        var store = new InMemoryEventStore
        {
            SchemaVersion = 3,
            ReconcileOnOpen = new ReconcileReport(1, 2, 3, 4, 5, 6, 7, 8, 9, ["One.", "Two."]),
        };
        using var harness = new BrainHarness(hooks: true, store: store);
        var client = harness.Connect(Role.Operator);

        client.Test(TestHookNames.GetDiagnostics);
        harness.Run();

        Assert.Equal(
            new StorageDiagnostics(StorageDiagnosticsState.Ready, null, 3, new ReconcileDiagnostics(1, 2, 3, 4, 5, 6, 7, 8, 9, 2)),
            Diagnostics(client).Storage);
    }

    [Fact]
    public void The_fault_hooks_forward_to_the_storage_hook_port()
    {
        var port = new FakeStorageHookPort();
        using var harness = new BrainHarness(hooks: true, storageHooks: port);
        var client = harness.Connect(Role.Operator);

        var inject = client.Test(TestHookNames.InjectStorageFault, """{"kind":"accessDenied","scope":"nextWrite"}""");
        var clear = client.Test(TestHookNames.ClearStorageFault);
        harness.Run();

        client.AssertReceived(TestResultMessage.Success(inject, EmptyHookResult.Instance), TestResultMessage.Success(clear, EmptyHookResult.Instance));
        Assert.Equal(["InjectFault(AccessDenied, NextWrite)", "ClearFault"], port.Calls);
    }

    [Fact]
    public void A_storage_hook_port_that_throws_fails_the_hook()
    {
        var port = new FakeStorageHookPort { Failure = new IOException("Nope.") };
        using var harness = new BrainHarness(hooks: true, storageHooks: port);
        var client = harness.Connect(Role.Operator);

        var inject = client.Test(TestHookNames.InjectStorageFault, """{"kind":"locked","scope":"allWrites"}""");
        harness.Run();

        client.AssertReceived(TestResultMessage.Failure(inject, TestHookError.HookFailed));
    }

    [Fact]
    public void The_fault_hooks_are_not_supported_without_a_storage_hook_port()
    {
        using var harness = new BrainHarness(hooks: true);
        var client = harness.Connect(Role.Operator);

        var inject = client.Test(TestHookNames.InjectStorageFault, """{"kind":"locked","scope":"allWrites"}""");
        var clear = client.Test(TestHookNames.ClearStorageFault);
        harness.Run();

        client.AssertReceived(TestResultMessage.Failure(inject, TestHookError.NotSupported), TestResultMessage.Failure(clear, TestHookError.NotSupported));
    }

    [Theory]
    [InlineData("""{"kind":"corrupt","scope":"nextWrite"}""")]
    [InlineData("""{"kind":"diskFull"}""")]
    [InlineData(null)]
    public void The_inject_hook_checks_its_arguments(string? args)
    {
        var port = new FakeStorageHookPort();
        using var harness = new BrainHarness(hooks: true, storageHooks: port);
        var client = harness.Connect(Role.Operator);

        var inject = client.Test(TestHookNames.InjectStorageFault, args);
        harness.Run();

        client.AssertReceived(TestResultMessage.Failure(inject, TestHookError.InvalidArgs));
        Assert.Empty(port.Calls);
    }

    [Fact]
    public void The_brain_requires_a_store()
    {
        using var harness = new BrainHarness();

        Assert.Throws<ArgumentNullException>(() => new Brain(harness.Dependencies with { Store = null! }));
    }

    private static DiagnosticsResult Diagnostics(TestClient client)
    {
        var result = Assert.IsType<TestResultMessage>(client.Messages[^1]);
        return ProtocolJson.Deserialize<DiagnosticsResult>(result.Result!.Value);
    }

    /// <summary>A store whose open and list calls finish when the test says so.</summary>
    internal sealed class ScriptedStore : IEventStore
    {
        public TaskCompletionSource<StorageOpenResult> Open { get; private set; } = new();

        public TaskCompletionSource<IReadOnlyList<EventSummary>> Events { get; } = new();

        public int OpenCalls { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public string DataRoot => @"C:\FOTOHAVN\Data";

        public void ResetOpen() => Open = new();

        public Task<StorageOpenResult> OpenAsync(CancellationToken ct)
        {
            OpenCalls++;
            LastToken = ct;
            return Open.Task;
        }

        public Task<IReadOnlyList<EventSummary>> ListEventsAsync(CancellationToken ct) => Events.Task;

        public Task DrainAsync(CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<EventRecord?> GetEventAsync(string eventId, CancellationToken ct) => throw new NotSupportedException();

        public Task CreateEventAsync(EventRecord record, CancellationToken ct) => throw new NotSupportedException();

        public Task UpdateEventAsync(EventRecord record, CancellationToken ct) => throw new NotSupportedException();

        public Task SetPinAsync(string eventId, string pin, CancellationToken ct) => throw new NotSupportedException();

        public Task<bool> VerifyPinAsync(string eventId, string pin, CancellationToken ct) => throw new NotSupportedException();

        public Task MarkEventDeletingAsync(string eventId, CancellationToken ct) => throw new NotSupportedException();

        public Task<GuestCycleRecord> StartGuestCycleAsync(string eventId, CancellationToken ct) => throw new NotSupportedException();

        public Task EndGuestCycleAsync(string guestCycleId, GuestCycleEndReason reason, CancellationToken ct) => throw new NotSupportedException();

        public Task<CaptureRecord> CommitCaptureAsync(CaptureCommit commit, CancellationToken ct) => throw new NotSupportedException();

        public Task SetCaptureStateAsync(string captureId, CaptureState state, CancellationToken ct) => throw new NotSupportedException();

        public Task<PhotoStripRecord> CommitPhotoStripAsync(PhotoStripCommit commit, CancellationToken ct) => throw new NotSupportedException();

        public Task MarkGuestCycleDeletingAsync(string guestCycleId, CancellationToken ct) => throw new NotSupportedException();

        public Task<GuestCyclePage> ListGuestCyclesAsync(string eventId, GuestCycleFilter filter, string? cursor, int pageSize, CancellationToken ct) => throw new NotSupportedException();

        public Task<GuestCycleDetail?> GetGuestCycleAsync(string guestCycleId, CancellationToken ct) => throw new NotSupportedException();

        public Task<string> GetThumbnailPathAsync(string sourceId, int width, CancellationToken ct) => throw new NotSupportedException();
    }

    /// <summary>A store whose <c>OpenAsync</c> throws instead of returning a faulted task.</summary>
    private sealed class ThrowingStore : IEventStore
    {
        public string DataRoot => @"C:\FOTOHAVN\Data";

        public Task<StorageOpenResult> OpenAsync(CancellationToken ct) => throw new StorageException(StorageErrorKind.Corrupt, "Corrupt.");

        public Task<IReadOnlyList<EventSummary>> ListEventsAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task DrainAsync(CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task<EventRecord?> GetEventAsync(string eventId, CancellationToken ct) => throw new NotSupportedException();

        public Task CreateEventAsync(EventRecord record, CancellationToken ct) => throw new NotSupportedException();

        public Task UpdateEventAsync(EventRecord record, CancellationToken ct) => throw new NotSupportedException();

        public Task SetPinAsync(string eventId, string pin, CancellationToken ct) => throw new NotSupportedException();

        public Task<bool> VerifyPinAsync(string eventId, string pin, CancellationToken ct) => throw new NotSupportedException();

        public Task MarkEventDeletingAsync(string eventId, CancellationToken ct) => throw new NotSupportedException();

        public Task<GuestCycleRecord> StartGuestCycleAsync(string eventId, CancellationToken ct) => throw new NotSupportedException();

        public Task EndGuestCycleAsync(string guestCycleId, GuestCycleEndReason reason, CancellationToken ct) => throw new NotSupportedException();

        public Task<CaptureRecord> CommitCaptureAsync(CaptureCommit commit, CancellationToken ct) => throw new NotSupportedException();

        public Task SetCaptureStateAsync(string captureId, CaptureState state, CancellationToken ct) => throw new NotSupportedException();

        public Task<PhotoStripRecord> CommitPhotoStripAsync(PhotoStripCommit commit, CancellationToken ct) => throw new NotSupportedException();

        public Task MarkGuestCycleDeletingAsync(string guestCycleId, CancellationToken ct) => throw new NotSupportedException();

        public Task<GuestCyclePage> ListGuestCyclesAsync(string eventId, GuestCycleFilter filter, string? cursor, int pageSize, CancellationToken ct) => throw new NotSupportedException();

        public Task<GuestCycleDetail?> GetGuestCycleAsync(string guestCycleId, CancellationToken ct) => throw new NotSupportedException();

        public Task<string> GetThumbnailPathAsync(string sourceId, int width, CancellationToken ct) => throw new NotSupportedException();
    }
}
