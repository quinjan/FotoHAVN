using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace FotoHavn.App.Controls;

public sealed partial class StatusCallout : UserControl
{
    public static readonly DependencyProperty SeverityProperty = DependencyProperty.Register(
        nameof(Severity), typeof(StatusSeverity), typeof(StatusCallout),
        new PropertyMetadata(StatusSeverity.Neutral, OnPresentationChanged));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(StatusCallout),
        new PropertyMetadata(string.Empty, OnPresentationChanged));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(StatusCallout),
        new PropertyMetadata(string.Empty, OnPresentationChanged));

    public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(
        nameof(Action), typeof(UIElement), typeof(StatusCallout), new PropertyMetadata(null));

    private StatusSeverity? lastAnnouncedSeverity;
    private string? lastAnnouncedMessage;

    public StatusCallout()
    {
        InitializeComponent();
        AutomationProperties.SetAutomationId(this, SemanticAutomationIds.StatusCallout);
        Loaded += (_, _) => ApplyPresentation(true);
    }

    public StatusSeverity Severity
    {
        get => (StatusSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public UIElement? Action
    {
        get => (UIElement?)GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    private static void OnPresentationChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args) =>
        ((StatusCallout)dependencyObject).ApplyPresentation(args.Property == SeverityProperty);

    private void ApplyPresentation(bool announceActivation = false)
    {
        VisualStateManager.GoToState(this, Severity.ToString(), false);
        AutomationProperties.SetName(this, string.Join(" ", new[] { Title, Message }.Where(value => !string.IsNullOrWhiteSpace(value))));
        if (announceActivation && IsLoaded && Severity != lastAnnouncedSeverity)
        {
            AutomationProperties.SetLiveSetting(
                this,
                Severity is StatusSeverity.Warning or StatusSeverity.Danger
                    ? AutomationLiveSetting.Assertive
                    : AutomationLiveSetting.Polite);
            var peer = FrameworkElementAutomationPeer.FromElement(this) ?? FrameworkElementAutomationPeer.CreatePeerForElement(this);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            lastAnnouncedSeverity = Severity;
            lastAnnouncedMessage = Message;
        }
        else if (IsLoaded && !string.IsNullOrWhiteSpace(Message) && Message != lastAnnouncedMessage)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(this) ?? FrameworkElementAutomationPeer.CreatePeerForElement(this);
            peer?.RaiseNotificationEvent(
                AutomationNotificationKind.Other,
                AutomationNotificationProcessing.ImportantMostRecent,
                Message,
                "FotoHavn.StatusCallout.Update");
            lastAnnouncedMessage = Message;
        }
    }
}
