using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace FotoHavn.App.Controls;

public sealed partial class InlineStatus : UserControl
{
    public static readonly DependencyProperty SeverityProperty = DependencyProperty.Register(
        nameof(Severity), typeof(StatusSeverity), typeof(InlineStatus),
        new PropertyMetadata(StatusSeverity.Neutral, OnPresentationChanged));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(InlineStatus),
        new PropertyMetadata(string.Empty, OnPresentationChanged));

    public static readonly DependencyProperty AnnouncementPriorityProperty = DependencyProperty.Register(
        nameof(AnnouncementPriority), typeof(AutomationLiveSetting), typeof(InlineStatus),
        new PropertyMetadata(AutomationLiveSetting.Polite, OnPresentationChanged));

    private (StatusSeverity Severity, string Message)? lastAnnouncement;
    private bool batchingPresentation;

    public InlineStatus()
    {
        InitializeComponent();
        AutomationProperties.SetAutomationId(this, SemanticAutomationIds.InlineStatus);
        Loaded += (_, _) => ApplyPresentation(true);
    }

    public StatusSeverity Severity
    {
        get => (StatusSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public AutomationLiveSetting AnnouncementPriority
    {
        get => (AutomationLiveSetting)GetValue(AnnouncementPriorityProperty);
        set => SetValue(AnnouncementPriorityProperty, value);
    }

    public void Present(StatusSeverity severity, string message, AutomationLiveSetting announcementPriority)
    {
        batchingPresentation = true;
        Severity = severity;
        Message = message;
        AnnouncementPriority = announcementPriority;
        batchingPresentation = false;
        if (string.IsNullOrWhiteSpace(message))
        {
            lastAnnouncement = null;
        }
        ApplyPresentation(true);
    }

    private static void OnPresentationChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var status = (InlineStatus)dependencyObject;
        if (!status.batchingPresentation)
        {
            status.ApplyPresentation(args.Property == MessageProperty || args.Property == SeverityProperty);
        }
    }

    private void ApplyPresentation(bool announce)
    {
        VisualStateManager.GoToState(this, Severity.ToString(), false);
        AutomationProperties.SetName(this, Message);

        var transition = (Severity, Message);
        if (announce && IsLoaded && !string.IsNullOrWhiteSpace(Message) && transition != lastAnnouncement)
        {
            AutomationProperties.SetLiveSetting(this, AnnouncementPriority);
            var peer = FrameworkElementAutomationPeer.FromElement(this) ?? FrameworkElementAutomationPeer.CreatePeerForElement(this);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            lastAnnouncement = transition;
        }
        else if (!announce)
        {
            AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Off);
        }
    }
}
