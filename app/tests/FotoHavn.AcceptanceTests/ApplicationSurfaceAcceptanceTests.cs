using FotoHavn.Core;
using Xunit;

namespace FotoHavn.AcceptanceTests;

public sealed class ApplicationSurfaceAcceptanceTests
{
    private static readonly ApplicationCanvasPresentation Canvas = new(1280, 720, false);
    private static readonly EventId EventId = new("0198f5d1-test-event");
    private static readonly CameraBinding Camera = new("camera-1", "FotoHAVN Camera");

    public static TheoryData<ApplicationPresentation, ApplicationSurface> SurfaceCases => new()
    {
        { SavedEvents(), ApplicationSurface.SavedEvents },
        { SavedEvents(setup: Setup()), ApplicationSurface.EventSetup },
        { Active(GuestCyclePresentation.Start), ApplicationSurface.GuestStart },
        { Active(new(GuestCyclePhase.StartUnavailable, Failure: GuestCycleFailure.CameraUnavailable)), ApplicationSurface.GuestStartUnavailable },
        { Active(new(GuestCyclePhase.Countdown, CaptureNumber: 1, CountdownSeconds: 3)), ApplicationSurface.Capture },
        { Active(new(GuestCyclePhase.OperatorAssistance, CompletedCaptures: 3, Failure: GuestCycleFailure.StorageUnavailable)), ApplicationSurface.OperatorAssistance },
        { Active(new(GuestCyclePhase.PhotoStripPreview, CompletedCaptures: 4, PreviewSecondsRemaining: 10)), ApplicationSurface.PhotoStrip },
        { SavedEvents(startConfirmation: new(EventId, "Community Night")), ApplicationSurface.Confirmation },
    };

    [Theory]
    [MemberData(nameof(SurfaceCases))]
    public void Authoritative_presentation_selects_named_production_surface(
        ApplicationPresentation presentation,
        ApplicationSurface expected)
    {
        Assert.Equal(expected, ApplicationSurfaceResolver.Resolve(presentation));
    }

    [Fact]
    public void Confirmation_takes_precedence_over_its_host_surface()
    {
        var presentation = Active(GuestCyclePresentation.Start) with
        {
            ActiveEvent = Active(GuestCyclePresentation.Start).ActiveEvent! with
            {
                ShowsExitConfirmation = true,
            },
        };

        Assert.Equal(ApplicationSurface.Confirmation, ApplicationSurfaceResolver.Resolve(presentation));
    }

    private static ApplicationPresentation SavedEvents(
        EventSetupPresentation? setup = null,
        StartEventConfirmationPresentation? startConfirmation = null) =>
        new("Saved Events", [], null, Canvas, Setup: setup, StartEventConfirmation: startConfirmation);

    private static ApplicationPresentation Active(GuestCyclePresentation cycle) =>
        new(
            "Saved Events",
            [],
            null,
            Canvas,
            ActiveEvent: new ActiveEventPresentation(
                EventId,
                "Community Night",
                Camera,
                "verification-stream",
                GuestStartState: GuestStartPresentation.FromReadiness(true, true),
                Cycle: cycle));

    private static EventSetupPresentation Setup() =>
        new(
            true,
            true,
            false,
            "Community Night",
            [],
            null,
            CameraConnectionState.NotSelected,
            false,
            new(false, true, false),
            new(true, 16, 9, true),
            true,
            true,
            false);
}
