using FotoHavn.App.Surfaces;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FotoHavn.App.UiVerification;

internal sealed class UiVerificationRenderSettledSignal
{
    private readonly FrameworkElement layoutRoot;
    private readonly TextBlock status = new()
    {
        Width = 1,
        Height = 1,
        Opacity = 0.01,
        IsHitTestVisible = false,
    };
    private readonly TextBlock announcer = new()
    {
        Width = 1,
        Height = 1,
        Opacity = 0.01,
        IsHitTestVisible = false,
    };

    public UiVerificationRenderSettledSignal(Grid windowRoot)
    {
        layoutRoot = windowRoot;
        AutomationProperties.SetAutomationId(status, "FotoHavn.Verification.RenderSettled");
        AutomationProperties.SetName(status, "FotoHAVN verification render status");
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        AutomationProperties.SetAutomationId(announcer, "FotoHavn.Verification.Announcement");
        AutomationProperties.SetLiveSetting(announcer, AutomationLiveSetting.Assertive);
        windowRoot.Children.Add(status);
        windowRoot.Children.Add(announcer);
    }

    public void Begin(ApplicationSurfaceOverride surfaceOverride)
    {
        status.Text = surfaceOverride.InjectionIdentity;
        AutomationProperties.SetHelpText(status, surfaceOverride.InjectionIdentity);
        AutomationProperties.SetItemStatus(status, "rendering");
    }

    public void CompleteAfterLayout(ApplicationSurfaceOverride surfaceOverride)
    {
        RoutedEventHandler? loaded = null;
        loaded = (_, _) =>
        {
            layoutRoot.Loaded -= loaded;
            _ = layoutRoot.DispatcherQueue.TryEnqueue(
                DispatcherQueuePriority.Low,
                () => Complete(surfaceOverride));
        };

        if (layoutRoot.IsLoaded)
        {
            _ = layoutRoot.DispatcherQueue.TryEnqueue(
                DispatcherQueuePriority.Low,
                () => Complete(surfaceOverride));
        }
        else
        {
            layoutRoot.Loaded += loaded;
        }
    }

    private void Complete(ApplicationSurfaceOverride surfaceOverride)
    {
        layoutRoot.UpdateLayout();
        if (surfaceOverride.FocusAutomationId is { Length: > 0 } focusId &&
            FindByAutomationId(layoutRoot, focusId) is FrameworkElement element)
        {
            element.Focus(FocusState.Programmatic);
        }

        if (surfaceOverride.Announcement is { Length: > 0 } announcement)
        {
            AutomationProperties.SetLiveSetting(
                announcer,
                surfaceOverride.AnnouncementPriority == AnnouncementPriority.Assertive
                    ? AutomationLiveSetting.Assertive
                    : AutomationLiveSetting.Polite);
            AutomationProperties.SetItemStatus(
                announcer,
                (surfaceOverride.AnnouncementPriority ?? AnnouncementPriority.Polite).ToString());
            announcer.Text = announcement;
            FrameworkElementAutomationPeer.FromElement(announcer)?.RaiseAutomationEvent(
                AutomationEvents.LiveRegionChanged);
        }

        AutomationProperties.SetItemStatus(status, "settled");
        FrameworkElementAutomationPeer.FromElement(status)?.RaiseAutomationEvent(
            AutomationEvents.LiveRegionChanged);
    }

    private static FrameworkElement? FindByAutomationId(DependencyObject root, string automationId)
    {
        if (root is FrameworkElement element &&
            AutomationProperties.GetAutomationId(element) == automationId)
        {
            return element;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            if (FindByAutomationId(VisualTreeHelper.GetChild(root, index), automationId) is { } match)
            {
                return match;
            }
        }

        return null;
    }
}
