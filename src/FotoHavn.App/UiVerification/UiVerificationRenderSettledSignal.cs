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
    private const string SettledStatus = "settled";
    private const string HostReadySettledStatus = "host-ready-settled";
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
    private readonly Button hostReady = new()
    {
        Width = 1,
        Height = 1,
        Opacity = 0.01,
        IsHitTestVisible = false,
        IsTabStop = false,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
    };
    private ApplicationSurfaceOverride? currentSurfaceOverride;

    public UiVerificationRenderSettledSignal(Grid windowRoot)
    {
        layoutRoot = windowRoot;
        AutomationProperties.SetAutomationId(status, "FotoHavn.Verification.RenderSettled");
        AutomationProperties.SetName(status, "FotoHAVN verification render status");
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        AutomationProperties.SetAutomationId(announcer, "FotoHavn.Verification.Announcement");
        AutomationProperties.SetLiveSetting(announcer, AutomationLiveSetting.Assertive);
        AutomationProperties.SetAutomationId(hostReady, "FotoHavn.Verification.HostReady");
        AutomationProperties.SetName(hostReady, "FotoHAVN verification host ready handshake");
        AutomationProperties.SetAccessibilityView(hostReady, AccessibilityView.Control);
        hostReady.Click += HostReadyClicked;
        windowRoot.Children.Add(status);
        windowRoot.Children.Add(announcer);
        windowRoot.Children.Add(hostReady);
    }

    public void Begin(ApplicationSurfaceOverride surfaceOverride)
    {
        currentSurfaceOverride = surfaceOverride;
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
                () => Complete(surfaceOverride, SettledStatus));
        };

        if (layoutRoot.IsLoaded)
        {
            _ = layoutRoot.DispatcherQueue.TryEnqueue(
                DispatcherQueuePriority.Low,
                () => Complete(surfaceOverride, SettledStatus));
        }
        else
        {
            layoutRoot.Loaded += loaded;
        }
    }

    private void HostReadyClicked(object sender, RoutedEventArgs args)
    {
        if (currentSurfaceOverride is not { } surfaceOverride)
        {
            return;
        }

        AutomationProperties.SetItemStatus(status, "host-ready-rendering");
        _ = layoutRoot.DispatcherQueue.TryEnqueue(
            DispatcherQueuePriority.Low,
            () => Complete(surfaceOverride, HostReadySettledStatus));
    }

    private void Complete(ApplicationSurfaceOverride surfaceOverride, string settledStatus)
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
            var announcementPeer = FrameworkElementAutomationPeer.FromElement(announcer) ??
                FrameworkElementAutomationPeer.CreatePeerForElement(announcer);
            announcementPeer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }

        AutomationProperties.SetItemStatus(status, settledStatus);
        var statusPeer = FrameworkElementAutomationPeer.FromElement(status) ??
            FrameworkElementAutomationPeer.CreatePeerForElement(status);
        statusPeer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
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
