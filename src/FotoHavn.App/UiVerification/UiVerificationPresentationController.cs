using System.Text.Json;
using System.Text.Json.Serialization;
using FotoHavn.App.Surfaces;
using FotoHavn.Core;

namespace FotoHavn.App.UiVerification;

internal sealed class UiVerificationPresentationController :
    IApplicationPresentationController,
    IApplicationSurfaceOverrideSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower) },
    };

    private readonly IReadOnlyDictionary<string, ApprovedInjection> catalog;
    private readonly UiVerificationCanonicalPresentation canonicalPresentation;
    private readonly UiVerificationRequest request;
    private readonly IReadOnlyDictionary<string, UiVerificationTransition> transitions;

    private UiVerificationPresentationController(
        IReadOnlyDictionary<string, ApprovedInjection> catalog,
        UiVerificationRequest request,
        UiVerificationCanonicalPresentation canonicalPresentation)
    {
        this.catalog = catalog;
        this.request = request;
        this.canonicalPresentation = canonicalPresentation;
        transitions = request.Transitions.ToDictionary(step => step.OnCommand, StringComparer.Ordinal);
        (CurrentPresentation, CurrentSurfaceOverride) = CreateState(request.Identity, null);
    }

    public event EventHandler<ApplicationPresentation>? PresentationChanged;

    public ApplicationPresentation CurrentPresentation { get; private set; }

    public ApplicationSurfaceOverride CurrentSurfaceOverride { get; private set; }

    public static async Task<UiVerificationPresentationController> CreateAsync(
        UiVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var catalogPath = Path.Combine(
            AppContext.BaseDirectory,
            "UiVerification",
            "ApprovedInjectionCatalog.json");
        await using var stream = File.OpenRead(catalogPath);
        var entries = await JsonSerializer.DeserializeAsync<List<ApprovedInjection>>(
            stream,
            JsonOptions,
            cancellationToken) ?? throw new InvalidDataException("The approved injection catalog is empty.");
        var catalog = entries.ToDictionary(entry => entry.Identity, StringComparer.Ordinal);
        if (!catalog.ContainsKey(request.Identity))
        {
            throw new ArgumentException($"Unknown approved injection identity '{request.Identity}'.", nameof(request));
        }

        foreach (var transition in request.Transitions)
        {
            if (!catalog.ContainsKey(transition.InjectionIdentity))
            {
                throw new ArgumentException(
                    $"Transition '{transition.OnCommand}' refers to unknown injection '{transition.InjectionIdentity}'.",
                    nameof(request));
            }
        }

        var canonicalPresentation = await UiVerificationCanonicalPresentation.LoadAsync(
            JsonOptions,
            cancellationToken);
        var presentation = request.Presentation;
        request = request with
        {
            Presentation = presentation is null
                ? canonicalPresentation.Primary
                : presentation with
                {
                    EventId = string.IsNullOrWhiteSpace(presentation.EventId)
                        ? canonicalPresentation.EventId
                        : presentation.EventId,
                    EventName = string.IsNullOrWhiteSpace(presentation.EventName)
                        ? canonicalPresentation.EventName
                        : presentation.EventName,
                },
        };
        return new(catalog, request, canonicalPresentation);
    }

    public Task<ApplicationPresentation> ExecuteAsync(
        ApplicationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (command is not LaunchApplication &&
            transitions.TryGetValue(command.GetType().Name, out var transition))
        {
            (CurrentPresentation, CurrentSurfaceOverride) = CreateState(
                transition.InjectionIdentity,
                transition);
            PresentationChanged?.Invoke(this, CurrentPresentation);
        }

        return Task.FromResult(CurrentPresentation);
    }

    private (ApplicationPresentation Presentation, ApplicationSurfaceOverride SurfaceOverride) CreateState(
        string identity,
        UiVerificationTransition? transition)
    {
        var injection = catalog[identity];
        var presentation = InjectedPresentationFactory.Create(injection, request, canonicalPresentation);
        var itemStatus = request.ExpectedSurfaceStatus ?? injection.ExpectedStatus;
        var surfaceName = injection.Surface switch
        {
            ApplicationSurface.GuestStart => "Guest Start",
            ApplicationSurface.GuestStartUnavailable => "Guest Start unavailable",
            ApplicationSurface.OperatorAssistance => "Operator Assistance",
            _ => injection.ExpectedName,
        };
        var state = itemStatus switch
        {
            "busy" => "in progress.",
            "unavailable" => "needs attention.",
            _ => "ready.",
        };
        var focusAutomationId = transition?.FocusAutomationId ?? injection.State switch
        {
            "exit-holding" => "FotoHavn.ActionButton.ExitEvent",
            "exit-confirmation-open" => "FotoHavn.Confirmation.SafeAction",
            _ when presentation.ActiveEvent?.GuestStart.RequiresEventSetupCorrection == true =>
                "FotoHavn.ActionButton.AssistanceExitOnly",
            _ => DefaultFocus(injection),
        };
        return (
            presentation,
            new(
                injection.Surface,
                injection.ExpectedName,
                itemStatus,
                injection.Identity,
                focusAutomationId,
                transition?.Announcement ??
                    (injection.Surface is ApplicationSurface.GuestStart or
                        ApplicationSurface.GuestStartUnavailable or ApplicationSurface.OperatorAssistance
                            ? $"{surfaceName} {state}"
                            : DefaultAnnouncement(injection)),
                transition?.AnnouncementPriority ??
                    (itemStatus == "unavailable" ? AnnouncementPriority.Assertive : AnnouncementPriority.Polite)));
    }

    private static string DefaultFocus(ApprovedInjection injection) => injection.Surface switch
    {
        ApplicationSurface.SavedEvents => "HeadingText",
        ApplicationSurface.EventSetup => "SetupTitleText",
        ApplicationSurface.GuestStart => "FotoHavn.ActionButton.Primary.GuestStart",
        ApplicationSurface.Confirmation when injection.State == "success-destination" =>
            "FotoHavn.Confirmation.ConfirmingAction",
        ApplicationSurface.Confirmation => "FotoHavn.Confirmation.SafeAction",
        _ => string.Empty,
    };

    private static string DefaultAnnouncement(ApprovedInjection injection)
    {
        if (injection.Surface == ApplicationSurface.Confirmation &&
            injection.State == "success-destination")
        {
            return "Event saved. Your changes have been saved.";
        }

        var surface = injection.Surface switch
        {
            ApplicationSurface.EventSetup => "Event setup",
            ApplicationSurface.Confirmation => "Confirmation",
            _ => injection.ExpectedName,
        };
        var state = injection.ExpectedStatus switch
        {
            "busy" => "in progress",
            "unavailable" => "needs attention",
            _ => "ready",
        };
        return $"{surface} {state}.";
    }

    private static AnnouncementPriority DefaultAnnouncementPriority(ApprovedInjection injection) =>
        injection.ExpectedStatus == "unavailable" ? AnnouncementPriority.Assertive : AnnouncementPriority.Polite;
}

internal sealed record ApprovedInjection(
    string Identity,
    ApplicationSurface Surface,
    string State,
    string ExpectedName,
    string ExpectedStatus);

internal sealed record UiVerificationRequest(
    string Identity,
    string? FixtureId = null,
    string? ExpectedSurfaceStatus = null,
    UiVerificationPresentationData? Presentation = null,
    DateTimeOffset? ClockUtc = null,
    DeterministicCameraOutcome CameraOutcome = DeterministicCameraOutcome.Ready,
    DeterministicStorageOutcome StorageOutcome = DeterministicStorageOutcome.Ready,
    string? MediaPath = null,
    IReadOnlyList<UiVerificationTransition>? Script = null)
{
    public UiVerificationPresentationData PresentationData => Presentation ??
        throw new InvalidOperationException("Canonical presentation defaults were not applied.");
    public IReadOnlyList<UiVerificationTransition> Transitions { get; } = Script ?? [];
    public DateTimeOffset Now { get; } = ClockUtc ?? new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
}

internal sealed record UiVerificationPresentationData(
    string EventId,
    string EventName,
    int CaptureNumber = 1,
    int CompletedCaptures = 0,
    int CountdownSeconds = 3);

internal sealed record UiVerificationTransition(
    string OnCommand,
    string InjectionIdentity,
    string? FocusAutomationId = null,
    string? Announcement = null,
    AnnouncementPriority AnnouncementPriority = AnnouncementPriority.Polite);

internal enum DeterministicCameraOutcome
{
    Ready,
    Unavailable,
    AccessDenied,
    InUse,
    Disconnected,
}

internal enum DeterministicStorageOutcome
{
    Ready,
    Insufficient,
    Unavailable,
}

internal static class InjectedPresentationFactory
{
    private static readonly ApplicationCanvasPresentation Canvas = new(1280, 720, false);

    public static ApplicationPresentation Create(
        ApprovedInjection injection,
        UiVerificationRequest request,
        UiVerificationCanonicalPresentation canonicalPresentation) =>
        injection.Surface switch
        {
            ApplicationSurface.SavedEvents => SavedEvents(injection, request, canonicalPresentation),
            ApplicationSurface.EventSetup => EventSetup(injection, request),
            ApplicationSurface.GuestStart => Active(request, GuestCyclePresentation.Start,
                showsExitConfirmation: injection.State == "exit-confirmation-open",
                exitHoldState: injection.State switch
                {
                    "exit-holding" => ExitHoldState.Holding,
                    "exit-hold-cancelled" => ExitHoldState.Cancelled,
                    _ => ExitHoldState.Idle,
                }),
            ApplicationSurface.GuestStartUnavailable => Active(
                request,
                GuestUnavailable(injection, request),
                guestStart: GuestUnavailableStart(injection, request)),
            ApplicationSurface.Capture => Active(request, Capture(injection, request)),
            ApplicationSurface.OperatorAssistance => Active(request, Assistance(injection, request)),
            ApplicationSurface.PhotoStrip => Active(request, PhotoStrip(injection, request)),
            ApplicationSurface.Confirmation => Confirmation(injection, request),
            _ => throw new InvalidDataException($"Unsupported injection surface '{injection.Surface}'."),
        };

    private static ApplicationPresentation SavedEvents(
        ApprovedInjection injection,
        UiVerificationRequest request,
        UiVerificationCanonicalPresentation canonicalPresentation)
    {
        var data = request.PresentationData;
        var savedCount = injection.State == "maximum-cards" ? 6 : 3;
        var tiles = new List<EventTilePresentation>
        {
            new(EventTileKind.NewEvent, "New Event", "Create a new Event", "+"),
        };
        for (var index = 0; index < savedCount; index++)
        {
            var fixture = canonicalPresentation.SavedEvents[index];
            var cardState = index == 0 ? injection.State switch
            {
                "card-hover" => EventCardState.Hover,
                "card-focus" => EventCardState.Focus,
                "unavailable" => EventCardState.Unavailable,
                "busy" => EventCardState.Busy,
                _ => EventCardState.Ready,
            } : EventCardState.Ready;
            tiles.Add(new(
                EventTileKind.SavedEvent,
                index == 0 ? data.EventName : fixture.EventName,
                fixture.SavedMetadata,
                string.Empty,
                new EventId(index == 0 ? data.EventId : fixture.EventId),
                request.Now,
                DeletionIncomplete: injection.State == "deletion-incomplete" && index == 0,
                State: cardState));
        }

        return new("Saved Events", tiles, null, Canvas);
    }

    private static ApplicationPresentation EventSetup(
        ApprovedInjection injection,
        UiVerificationRequest request)
    {
        var data = request.PresentationData;
        var editing = injection.State.StartsWith("edit-", StringComparison.Ordinal) ||
            injection.Surface == ApplicationSurface.Confirmation &&
            (injection.State.StartsWith("save-", StringComparison.Ordinal) ||
             injection.State.StartsWith("discard-", StringComparison.Ordinal));
        var cameraState = injection.State switch
        {
            "new-empty" => CameraConnectionState.Unavailable,
            "camera-checking" => CameraConnectionState.Connecting,
            "camera-unavailable" => CameraConnectionState.Unavailable,
            "camera-access-denied" => CameraConnectionState.AccessDenied,
            "camera-in-use" => CameraConnectionState.InUseByAnotherApp,
            "camera-disconnected" => CameraConnectionState.Disconnected,
            _ => request.CameraOutcome switch
            {
                DeterministicCameraOutcome.Ready => CameraConnectionState.Ready,
                DeterministicCameraOutcome.Unavailable => CameraConnectionState.Unavailable,
                DeterministicCameraOutcome.AccessDenied => CameraConnectionState.AccessDenied,
                DeterministicCameraOutcome.InUse => CameraConnectionState.InUseByAnotherApp,
                DeterministicCameraOutcome.Disconnected => CameraConnectionState.Disconnected,
                _ => throw new ArgumentOutOfRangeException(),
            },
        };
        var storageReady = request.StorageOutcome == DeterministicStorageOutcome.Ready &&
            injection.State is not "storage-insufficient" and not "storage-unavailable";
        var camera = new AvailableCamera("verification-camera", "Logitech BRIO", "deterministic");
        var setup = new EventSetupPresentation(
            true,
            true,
            false,
            injection.State == "new-empty" ? string.Empty : data.EventName,
            [camera],
            injection.State == "new-empty" ? null : camera,
            cameraState,
            cameraState == CameraConnectionState.Ready,
            new(false, true, false),
            new(true, 16, 9, true),
            true,
            storageReady,
            injection.State is not "actions-disabled" and not "saving",
            EventId: editing ? new(data.EventId) : null,
            IsDirty: injection.State == "edit-dirty",
            IsNameDirty: injection.State == "edit-dirty",
            Title: editing ? "Edit Event" : "New Event",
            IsBusy: injection.State == "saving",
            IsSavingAndStarting: injection.State == "saving");
        return new("Saved Events", [], null, Canvas, Setup: setup);
    }

    private static GuestCyclePresentation GuestUnavailable(
        ApprovedInjection injection,
        UiVerificationRequest request) =>
        new(
            GuestCyclePhase.StartUnavailable,
            Failure: request.FixtureId?.Contains("longest-recovery-copy", StringComparison.Ordinal) == true ||
                injection.State.StartsWith("storage-", StringComparison.Ordinal)
                ? GuestCycleFailure.StorageUnavailable
                : GuestCycleFailure.CameraUnavailable);

    private static GuestStartPresentation GuestUnavailableStart(
        ApprovedInjection injection,
        UiVerificationRequest request)
    {
        var responsiveExitOnly = request.FixtureId?.Contains("longest-recovery-copy", StringComparison.Ordinal) == true;
        var cameraReady = responsiveExitOnly || injection.State.StartsWith("storage-", StringComparison.Ordinal);
        var storageReady = !cameraReady;
        var actionState = injection.State switch
        {
            "retrying" => GuestStartActionState.Retrying,
            "retry-failed" => GuestStartActionState.RetryFailed,
            _ => GuestStartActionState.Idle,
        };
        return GuestStartPresentation.FromReadiness(
            cameraReady,
            storageReady,
            requiresEventSetupCorrection: responsiveExitOnly || injection.State.EndsWith("exit-only", StringComparison.Ordinal)) with
        {
            ActionState = actionState,
        };
    }

    private static GuestCyclePresentation Capture(
        ApprovedInjection injection,
        UiVerificationRequest request)
    {
        var data = request.PresentationData;
        if (injection.State.StartsWith("countdown-", StringComparison.Ordinal))
        {
            return new(
                GuestCyclePhase.Countdown,
                data.CaptureNumber,
                data.CompletedCaptures,
                int.Parse(injection.State[^1..], System.Globalization.CultureInfo.InvariantCulture));
        }

        return injection.State switch
        {
            "flash" => new(GuestCyclePhase.Flash, data.CaptureNumber, data.CompletedCaptures),
            "photo-saved" => new(GuestCyclePhase.CaptureSaved, data.CaptureNumber, data.CompletedCaptures + 1),
            "camera-failure" => new(GuestCyclePhase.Countdown, data.CaptureNumber, data.CompletedCaptures,
                Failure: GuestCycleFailure.CameraUnavailable),
            "storage-failure" => new(GuestCyclePhase.Countdown, data.CaptureNumber, data.CompletedCaptures,
                Failure: GuestCycleFailure.StorageUnavailable),
            _ => new(GuestCyclePhase.Countdown,
                int.Parse(injection.State[^1..], System.Globalization.CultureInfo.InvariantCulture),
                Math.Max(0, int.Parse(injection.State[^1..], System.Globalization.CultureInfo.InvariantCulture) - 1),
                data.CountdownSeconds),
        };
    }

    private static GuestCyclePresentation Assistance(
        ApprovedInjection injection,
        UiVerificationRequest request)
    {
        var completed = injection.State.Contains("-3-", StringComparison.Ordinal) ? 3 :
            injection.State.Contains("-4-", StringComparison.Ordinal) ? 4 : request.PresentationData.CompletedCaptures;
        var failure = injection.State.StartsWith("storage-", StringComparison.Ordinal)
            ? GuestCycleFailure.StorageUnavailable
            : GuestCycleFailure.CameraUnavailable;
        return new(
            GuestCyclePhase.OperatorAssistance,
            CompletedCaptures: completed,
            Failure: failure,
            Recovery: injection.State == "exit-only" ? GuestCycleRecovery.ExitOnly : GuestCycleRecovery.Retry,
            ActionState: injection.State switch
            {
                "retrying" => GuestCycleActionState.Retrying,
                "retry-failed" => GuestCycleActionState.RetryFailed,
                _ => GuestCycleActionState.Idle,
            });
    }

    private static GuestCyclePresentation PhotoStrip(
        ApprovedInjection injection,
        UiVerificationRequest request) =>
        new(
            injection.State == "returning" ? GuestCyclePhase.Fading : GuestCyclePhase.PhotoStripPreview,
            CompletedCaptures: 4,
            PhotoStripPath: injection.State.StartsWith("visible-", StringComparison.Ordinal) ? request.MediaPath : null,
            PreviewSecondsRemaining: injection.State.Contains("5-seconds", StringComparison.Ordinal) ? 5 : 10);

    private static ApplicationPresentation Confirmation(
        ApprovedInjection injection,
        UiVerificationRequest request)
    {
        var data = request.PresentationData;
        if (injection.State.StartsWith("start-", StringComparison.Ordinal))
        {
            return new("Saved Events", [], null, Canvas,
                StartEventConfirmation: new(new(data.EventId), data.EventName));
        }

        if (injection.State.StartsWith("save-", StringComparison.Ordinal))
        {
            var basePresentation = EventSetup(injection, request);
            return basePresentation with
            {
                Setup = basePresentation.Setup! with
                {
                    Confirmation = EventSetupConfirmation.SaveAndClose,
                },
            };
        }

        if (injection.State.StartsWith("discard-", StringComparison.Ordinal))
        {
            var basePresentation = EventSetup(injection, request);
            return basePresentation with
            {
                Setup = basePresentation.Setup! with
                {
                    Confirmation = EventSetupConfirmation.DiscardChanges,
                },
            };
        }

        if (injection.State.StartsWith("exit-", StringComparison.Ordinal))
        {
            return Active(request, GuestCyclePresentation.Start, showsExitConfirmation: true);
        }

        var stage = injection.State switch
        {
            "delete-busy" => EventDeletionStage.Deleting,
            "delete-failed" or "retry" => EventDeletionStage.Incomplete,
            "success-destination" => EventDeletionStage.Deleted,
            _ => EventDeletionStage.Confirmation,
        };
        return new("Saved Events", [], null, Canvas,
            EventDeletion: new(new(data.EventId), data.EventName, stage));
    }

    private static ApplicationPresentation Active(
        UiVerificationRequest request,
        GuestCyclePresentation cycle,
        bool showsExitConfirmation = false,
        GuestStartPresentation? guestStart = null,
        ExitHoldState exitHoldState = ExitHoldState.Idle)
    {
        var data = request.PresentationData;
        return new(
            "Saved Events",
            [],
            null,
            Canvas,
            ActiveEvent: new(
                new(data.EventId),
                data.EventName,
                new("verification-camera", "FotoHAVN verification Camera"),
                "verification-stream",
                showsExitConfirmation,
                guestStart ?? GuestStartPresentation.FromReadiness(true, true),
                cycle,
                ExitHoldState: exitHoldState));
    }
}
