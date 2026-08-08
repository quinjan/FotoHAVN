using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace FotoHavn.App.Controls;

public sealed partial class GuardedExitAction : UserControl
{
    public static readonly TimeSpan RequiredHoldDuration = TimeSpan.FromSeconds(1.5);

    private readonly GuardedHoldInteraction interaction = new(RequiredHoldDuration);
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private bool keyboardHolding;
    private bool completionRaised;
    private bool destructive;

    public GuardedExitAction()
    {
        InitializeComponent();
        timer.Tick += HoldTimerTick;
    }

    public event EventHandler? HoldCompleted;

    public string ActionAutomationId
    {
        get => AutomationProperties.GetAutomationId(HoldButton);
        set => AutomationProperties.SetAutomationId(HoldButton, value);
    }

    public bool IsDestructive
    {
        get => destructive;
        set
        {
            destructive = value;
            HoldButton.Style = (Style)Application.Current.Resources[
                value ? "FotoHavnActionButtonDestructiveStyle" : "FotoHavnActionButtonTertiaryStyle"];
            ExitGlyph.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            HoldButton.MinWidth = value ? 115 : 96;
            HoldButton.MinHeight = value ? 54 : (double)Application.Current.Resources["TargetOperatorMinimum"];
        }
    }

    public void ShowIdle()
    {
        _ = interaction.Cancel();
        completionRaised = false;
        timer.Stop();
        IsEnabled = true;
        Apply(new(GuardedHoldState.Idle, 0));
    }

    public void ShowHolding(double progress = 0.5) =>
        Apply(new(GuardedHoldState.Holding, Math.Clamp(progress, 0, 1)));

    public bool FocusAction(FocusState focusState = FocusState.Programmatic) => HoldButton.Focus(focusState);

    public void ShowBusy(string label)
    {
        _ = interaction.Cancel();
        completionRaised = true;
        timer.Stop();
        LabelText.Text = label;
        HoldingIndicator.Visibility = Visibility.Collapsed;
        HoldProgress.Visibility = Visibility.Collapsed;
        AutomationProperties.SetItemStatus(HoldButton, "Busy");
        IsEnabled = false;
        Announce("Exiting event…", AutomationLiveSetting.Polite);
    }

    private void HoldButtonPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        var point = args.GetCurrentPoint(HoldButton);
        if (!point.Properties.IsLeftButtonPressed && !point.IsInContact)
        {
            return;
        }

        _ = HoldButton.CapturePointer(args.Pointer);
        BeginHold();
        args.Handled = true;
    }

    private void HoldButtonPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        HoldButton.ReleasePointerCapture(args.Pointer);
        CancelIncompleteHold();
        args.Handled = true;
    }

    private void HoldButtonPointerExited(object sender, PointerRoutedEventArgs args)
    {
        if (!completionRaised)
        {
            HoldButton.ReleasePointerCapture(args.Pointer);
            CancelIncompleteHold();
        }
    }

    private void HoldButtonPointerCaptureLost(object sender, PointerRoutedEventArgs args) =>
        CancelIncompleteHold();

    private void HoldButtonKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key is not (VirtualKey.Enter or VirtualKey.Space) || keyboardHolding)
        {
            return;
        }

        keyboardHolding = true;
        BeginHold();
        args.Handled = true;
    }

    private void HoldButtonKeyUp(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key is not (VirtualKey.Enter or VirtualKey.Space))
        {
            return;
        }

        keyboardHolding = false;
        CancelIncompleteHold();
        args.Handled = true;
    }

    private void BeginHold()
    {
        if (completionRaised || timer.IsEnabled)
        {
            return;
        }

        interaction.Begin(DateTimeOffset.UtcNow);
        timer.Start();
        Apply(interaction.Update(DateTimeOffset.UtcNow));
        Announce("Exit Event hold started. Keep holding for 1.5 seconds.", AutomationLiveSetting.Polite);
    }

    private void HoldTimerTick(object? sender, object args)
    {
        var update = interaction.Update(DateTimeOffset.UtcNow);
        Apply(update);
        if (update.State != GuardedHoldState.Completed || completionRaised)
        {
            return;
        }

        completionRaised = true;
        timer.Stop();
        Announce("Exit Event hold complete. Confirmation opened.", AutomationLiveSetting.Assertive);
        HoldCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void CancelIncompleteHold()
    {
        if (completionRaised)
        {
            return;
        }

        var wasHolding = timer.IsEnabled;
        timer.Stop();
        Apply(interaction.Cancel());
        if (wasHolding)
        {
            Announce("Exit Event hold cancelled.", AutomationLiveSetting.Assertive);
        }
    }

    private void Apply(GuardedHoldUpdate update)
    {
        var holding = update.State is GuardedHoldState.Holding or GuardedHoldState.Completed;
        HoldButton.MinWidth = holding ? 218 : destructive ? 115 : 96;
        LabelText.Text = holding ? "Keep holding…" : "Exit Event";
        HoldingIndicator.Visibility = holding ? Visibility.Visible : Visibility.Collapsed;
        HoldProgress.Value = update.Progress;
        HoldProgress.Visibility = holding ? Visibility.Visible : Visibility.Collapsed;
        HoldButton.BorderBrush = holding
            ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ColorBorderDefaultBrush"]
            : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        HoldButton.Background = holding
            ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ColorSurfacePanelBrush"]
            : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        AutomationProperties.SetItemStatus(HoldButton, update.State.ToString());
    }

    private void Announce(string message, AutomationLiveSetting liveSetting)
    {
        HoldAnnouncement.Text = message;
        AutomationProperties.SetName(HoldAnnouncement, message);
        AutomationProperties.SetLiveSetting(HoldAnnouncement, liveSetting);
        var peer = FrameworkElementAutomationPeer.FromElement(HoldAnnouncement) ??
            FrameworkElementAutomationPeer.CreatePeerForElement(HoldAnnouncement);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
