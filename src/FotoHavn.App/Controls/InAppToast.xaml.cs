using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace FotoHavn.App.Controls;

public sealed partial class InAppToast : UserControl
{
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(InAppToast),
        new PropertyMetadata(string.Empty, OnPresentationChanged));

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(InAppToast),
        new PropertyMetadata(false, OnPresentationChanged));

    public static readonly DependencyProperty DismissCommandProperty = DependencyProperty.Register(
        nameof(DismissCommand), typeof(ICommand), typeof(InAppToast), new PropertyMetadata(null));

    private string? lastAnnouncedMessage;
    private bool wasOpen;

    public InAppToast()
    {
        InitializeComponent();
        IsTabStop = false;
        AutomationProperties.SetAutomationId(this, SemanticAutomationIds.Toast);
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
        Loaded += (_, _) => ApplyPresentation();
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public ICommand? DismissCommand
    {
        get => (ICommand?)GetValue(DismissCommandProperty);
        set => SetValue(DismissCommandProperty, value);
    }

    private static void OnPresentationChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args) =>
        ((InAppToast)dependencyObject).ApplyPresentation();

    private void ApplyPresentation()
    {
        VisualStateManager.GoToState(this, IsOpen ? "Open" : "Closed", false);
        AutomationProperties.SetName(this, Message);
        if (IsOpen && !wasOpen)
        {
            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = MotionPolicy.Resolve("MotionFastDuration"),
            };
            Storyboard.SetTarget(fade, ToastFrame);
            Storyboard.SetTargetProperty(fade, "Opacity");
            var storyboard = new Storyboard();
            storyboard.Children.Add(fade);
            storyboard.Begin();
        }
        wasOpen = IsOpen;
        if (IsLoaded && IsOpen && !string.IsNullOrWhiteSpace(Message) && Message != lastAnnouncedMessage)
        {
            AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
            var peer = FrameworkElementAutomationPeer.FromElement(this) ?? FrameworkElementAutomationPeer.CreatePeerForElement(this);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            lastAnnouncedMessage = Message;
        }
        else
        {
            AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Off);
        }
    }

    private void DismissClicked(object sender, RoutedEventArgs args)
    {
        if (DismissCommand?.CanExecute(null) == true)
        {
            DismissCommand.Execute(null);
        }

        IsOpen = false;
    }
}
