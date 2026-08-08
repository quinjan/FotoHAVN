using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace FotoHavn.App.Controls;

public sealed partial class DialogEventIdentity : UserControl
{
    public static readonly DependencyProperty EventNameProperty = DependencyProperty.Register(
        nameof(EventName),
        typeof(string),
        typeof(DialogEventIdentity),
        new PropertyMetadata(string.Empty, OnIdentityChanged));

    public static readonly DependencyProperty EventIdProperty = DependencyProperty.Register(
        nameof(EventId),
        typeof(string),
        typeof(DialogEventIdentity),
        new PropertyMetadata(string.Empty, OnIdentityChanged));

    public DialogEventIdentity()
    {
        InitializeComponent();
    }

    public string EventName
    {
        get => (string)GetValue(EventNameProperty);
        set => SetValue(EventNameProperty, value);
    }

    public string EventId
    {
        get => (string)GetValue(EventIdProperty);
        set => SetValue(EventIdProperty, value);
    }

    public void ApplyResponsiveLayout(bool stress) =>
        IdentityGrid.Padding = stress ? new Thickness(10, 8, 10, 8) : new Thickness(12, 16, 12, 16);

    private static void OnIdentityChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var identity = (DialogEventIdentity)dependencyObject;
        AutomationProperties.SetName(
            identity,
            $"Event identity. Event {identity.EventName}. Full Event ID {identity.EventId}.");
    }
}
