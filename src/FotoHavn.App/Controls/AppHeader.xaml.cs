using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace FotoHavn.App.Controls;

public sealed partial class AppHeader : UserControl
{
    public static readonly DependencyProperty ContextProperty = DependencyProperty.Register(
        nameof(Context), typeof(string), typeof(AppHeader), new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty EventNameProperty = DependencyProperty.Register(
        nameof(EventName), typeof(string), typeof(AppHeader), new PropertyMetadata(string.Empty, OnContentChanged));

    public static readonly DependencyProperty TrailingContentProperty = DependencyProperty.Register(
        nameof(TrailingContent), typeof(object), typeof(AppHeader), new PropertyMetadata(null, OnContentChanged));

    public AppHeader()
    {
        InitializeComponent();
        AutomationProperties.SetAutomationId(this, SemanticAutomationIds.AppHeader);
    }

    public string Context
    {
        get => (string)GetValue(ContextProperty);
        set => SetValue(ContextProperty, value);
    }

    public string EventName
    {
        get => (string)GetValue(EventNameProperty);
        set => SetValue(EventNameProperty, value);
    }

    public object? TrailingContent
    {
        get => GetValue(TrailingContentProperty);
        set => SetValue(TrailingContentProperty, value);
    }

    private static void OnContentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is AppHeader header && header.ContextText is not null && header.TrailingPresenter is not null)
        {
            var hasTrailingContent = header.TrailingContent is not null;
            header.ContextText.Visibility = hasTrailingContent ? Visibility.Collapsed : Visibility.Visible;
            header.TrailingPresenter.Visibility = hasTrailingContent ? Visibility.Visible : Visibility.Collapsed;
            AutomationProperties.SetHelpText(header, header.EventName);
        }
    }
}
