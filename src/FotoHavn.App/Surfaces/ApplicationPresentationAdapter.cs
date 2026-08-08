using FotoHavn.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;

namespace FotoHavn.App.Surfaces;

public sealed record ApplicationSurfaceOverride(
    ApplicationSurface Surface,
    string AccessibleName,
    string ItemStatus,
    string InjectionIdentity,
    string? FocusAutomationId = null,
    string? Announcement = null);

public interface IApplicationSurfaceOverrideSource
{
    ApplicationSurfaceOverride CurrentSurfaceOverride { get; }
}

public sealed class ApplicationPresentationAdapter
{
    private readonly IReadOnlyDictionary<ApplicationSurface, ProductionSurfaceComposition> compositions;

    public ApplicationPresentationAdapter(
        FrameworkElement savedEventsRoot,
        FrameworkElement eventSetupRoot,
        FrameworkElement guestStartRoot,
        FrameworkElement guestStartUnavailableRoot,
        FrameworkElement captureRoot,
        FrameworkElement operatorAssistanceRoot,
        FrameworkElement photoStripRoot,
        params FrameworkElement[] confirmationRoots)
    {
        compositions = new Dictionary<ApplicationSurface, ProductionSurfaceComposition>
        {
            [ApplicationSurface.SavedEvents] = new SavedEventsSurface(savedEventsRoot),
            [ApplicationSurface.EventSetup] = new EventSetupSurface(eventSetupRoot),
            [ApplicationSurface.GuestStart] = new GuestStartSurface(guestStartRoot),
            [ApplicationSurface.GuestStartUnavailable] = new GuestStartUnavailableSurface(guestStartUnavailableRoot),
            [ApplicationSurface.Capture] = new CaptureSurface(captureRoot),
            [ApplicationSurface.OperatorAssistance] = new OperatorAssistanceSurface(operatorAssistanceRoot),
            [ApplicationSurface.PhotoStrip] = new PhotoStripSurface(photoStripRoot),
            [ApplicationSurface.Confirmation] = new ConfirmationSurface(confirmationRoots),
        };
    }

    public ApplicationSurface Apply(
        ApplicationPresentation presentation,
        ApplicationSurfaceOverride? surfaceOverride = null)
    {
        var activeSurface = surfaceOverride?.Surface ?? ApplicationSurfaceResolver.Resolve(presentation);
        foreach (var composition in compositions.Values)
        {
            composition.ClearSemantics();
        }

        compositions[activeSurface].ApplySemantics(presentation, surfaceOverride);
        return activeSurface;
    }
}

public abstract class ProductionSurfaceComposition
{
    private readonly IReadOnlyList<FrameworkElement> roots;

    protected ProductionSurfaceComposition(string automationId, params FrameworkElement[] roots)
    {
        AutomationId = automationId;
        this.roots = roots;
    }

    public string AutomationId { get; }

    protected abstract string AccessibleName(ApplicationPresentation presentation);

    protected virtual string ItemStatus(ApplicationPresentation presentation) => "ready";

    internal void ApplySemantics(
        ApplicationPresentation presentation,
        ApplicationSurfaceOverride? surfaceOverride)
    {
        var root = roots.FirstOrDefault(candidate => candidate.Visibility == Visibility.Visible) ?? roots[0];
        AutomationProperties.SetAutomationId(root, AutomationId);
        AutomationProperties.SetName(root, surfaceOverride?.AccessibleName ?? AccessibleName(presentation));
        AutomationProperties.SetItemStatus(root, surfaceOverride?.ItemStatus ?? ItemStatus(presentation));
    }

    internal void ClearSemantics()
    {
        foreach (var root in roots)
        {
            AutomationProperties.SetAutomationId(root, string.Empty);
            AutomationProperties.SetName(root, string.Empty);
            AutomationProperties.SetItemStatus(root, string.Empty);
        }
    }
}

public sealed class SavedEventsSurface(FrameworkElement root)
    : ProductionSurfaceComposition("FotoHavn.Surface.SavedEvents", root)
{
    protected override string AccessibleName(ApplicationPresentation presentation) => "Saved Events";
}

public sealed class EventSetupSurface(FrameworkElement root)
    : ProductionSurfaceComposition("FotoHavn.Surface.EventSetup", root)
{
    protected override string AccessibleName(ApplicationPresentation presentation) =>
        presentation.Setup?.Title ?? "Event setup";
}

public sealed class GuestStartSurface(FrameworkElement root)
    : ProductionSurfaceComposition("FotoHavn.Surface.GuestStart", root)
{
    protected override string AccessibleName(ApplicationPresentation presentation) =>
        presentation.ActiveEvent?.Heading ?? "Guest Start";
}

public sealed class GuestStartUnavailableSurface(FrameworkElement root)
    : ProductionSurfaceComposition("FotoHavn.Surface.GuestStartUnavailable", root)
{
    protected override string AccessibleName(ApplicationPresentation presentation) => "Please call the operator";
}

public sealed class CaptureSurface(FrameworkElement root)
    : ProductionSurfaceComposition("FotoHavn.Surface.Capture", root)
{
    protected override string AccessibleName(ApplicationPresentation presentation) => "Photo capture";
}

public sealed class OperatorAssistanceSurface(FrameworkElement root)
    : ProductionSurfaceComposition("FotoHavn.Surface.OperatorAssistance", root)
{
    protected override string AccessibleName(ApplicationPresentation presentation) => "Please call the operator";
}

public sealed class PhotoStripSurface(FrameworkElement root)
    : ProductionSurfaceComposition("FotoHavn.Surface.PhotoStrip", root)
{
    protected override string AccessibleName(ApplicationPresentation presentation) => "Here’s your Photo Strip";

    protected override string ItemStatus(ApplicationPresentation presentation) =>
        presentation.ActiveEvent?.GuestCycle.PhotoStripPath is null ? "busy" : "ready";
}

public sealed class ConfirmationSurface(params FrameworkElement[] roots)
    : ProductionSurfaceComposition("FotoHavn.Surface.Confirmation", roots)
{
    protected override string AccessibleName(ApplicationPresentation presentation) =>
        presentation.EventDeletion?.Title ??
        presentation.StartEventConfirmation?.Prompt ??
        (presentation.Setup?.Confirmation switch
        {
            EventSetupConfirmation.DiscardChanges => "Discard changes?",
            EventSetupConfirmation.SaveAndClose or EventSetupConfirmation.SaveAndStart => "Save this Event?",
            _ when presentation.ActiveEvent?.ShowsExitConfirmation == true => "Exit Event?",
            _ => "Confirmation",
        });

    protected override string ItemStatus(ApplicationPresentation presentation) =>
        presentation.EventDeletion?.IsBusy == true ? "busy" : "ready";
}
