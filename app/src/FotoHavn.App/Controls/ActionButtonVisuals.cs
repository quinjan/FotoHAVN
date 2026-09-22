using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace FotoHavn.App.Controls;

public enum ActionButtonEmphasis
{
    Primary,
    Secondary,
    Tertiary,
    Destructive,
}

public static class ActionButtonVisuals
{
    public static readonly DependencyProperty EmphasisProperty = DependencyProperty.RegisterAttached(
        "Emphasis", typeof(ActionButtonEmphasis), typeof(ActionButtonVisuals),
        new PropertyMetadata(ActionButtonEmphasis.Secondary, OnEmphasisChanged));

    private static readonly DependencyProperty IdleContentProperty = DependencyProperty.RegisterAttached(
        "IdleContent", typeof(object), typeof(ActionButtonVisuals), new PropertyMetadata(null));

    private static readonly DependencyProperty IdleAutomationNameProperty = DependencyProperty.RegisterAttached(
        "IdleAutomationName", typeof(string), typeof(ActionButtonVisuals), new PropertyMetadata(string.Empty));

    private static readonly DependencyProperty IsBusyProperty = DependencyProperty.RegisterAttached(
        "IsBusy", typeof(bool), typeof(ActionButtonVisuals), new PropertyMetadata(false));

    private static readonly DependencyProperty ConflictingActionsProperty = DependencyProperty.RegisterAttached(
        "ConflictingActions", typeof(object), typeof(ActionButtonVisuals), new PropertyMetadata(null));

    private static readonly DependencyProperty IdleMinWidthProperty = DependencyProperty.RegisterAttached(
        "IdleMinWidth", typeof(double), typeof(ActionButtonVisuals), new PropertyMetadata(0d));

    public static ActionButtonEmphasis GetEmphasis(DependencyObject target) =>
        (ActionButtonEmphasis)target.GetValue(EmphasisProperty);

    public static void SetEmphasis(DependencyObject target, ActionButtonEmphasis value) =>
        target.SetValue(EmphasisProperty, value);

    public static bool GetIsBusy(DependencyObject target) => (bool)target.GetValue(IsBusyProperty);

    public static void BeginBusy(Button button, string busyLabel, params Control[] conflictingActions)
    {
        ArgumentNullException.ThrowIfNull(button);
        ArgumentException.ThrowIfNullOrWhiteSpace(busyLabel);
        if ((bool)button.GetValue(IsBusyProperty))
        {
            return;
        }

        button.SetValue(IsBusyProperty, true);
        button.SetValue(
            ConflictingActionsProperty,
            conflictingActions.Select(control => (Control: control, WasEnabled: control.IsEnabled)).ToArray());
        foreach (var conflict in conflictingActions)
        {
            conflict.IsEnabled = false;
        }
        button.SetValue(IdleContentProperty, button.Content);
        button.SetValue(IdleAutomationNameProperty, AutomationProperties.GetName(button));
        button.SetValue(IdleMinWidthProperty, button.MinWidth);
        var iconSize = (double)Application.Current.Resources["IconDefault"];
        var progress = new ProgressRing { Width = iconSize, Height = iconSize, IsActive = true, IsTabStop = false };
        AutomationProperties.SetAccessibilityView(progress, AccessibilityView.Raw);
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = (double)Application.Current.Resources["SpaceInline"] };
        content.Children.Add(progress);
        content.Children.Add(new TextBlock { Text = busyLabel, VerticalAlignment = VerticalAlignment.Center });
        button.MinWidth = Math.Max(button.MinWidth, button.ActualWidth);
        AutomationProperties.SetLiveSetting(button, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(button, busyLabel);
        AutomationProperties.SetItemStatus(button, busyLabel);
        button.Content = content;
        button.IsEnabled = false;
        RaiseLiveRegionChanged(button);
    }

    public static void EndBusy(Button button, bool isEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(button);
        if (!(bool)button.GetValue(IsBusyProperty))
        {
            return;
        }

        button.SetValue(IsBusyProperty, false);
        if (button.GetValue(ConflictingActionsProperty) is ValueTuple<Control, bool>[] conflicts)
        {
            foreach (var (control, wasEnabled) in conflicts)
            {
                control.IsEnabled = wasEnabled;
            }
        }
        button.Content = button.GetValue(IdleContentProperty);
        button.MinWidth = (double)button.GetValue(IdleMinWidthProperty);
        AutomationProperties.SetName(button, (string)button.GetValue(IdleAutomationNameProperty));
        AutomationProperties.SetLiveSetting(button, AutomationLiveSetting.Off);
        AutomationProperties.SetItemStatus(button, string.Empty);
        button.IsEnabled = isEnabled;
    }

    private static void OnEmphasisChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not Button button)
        {
            throw new ArgumentException("Action Button emphasis applies only to Button.");
        }

        button.Loaded -= ButtonLoaded;
        button.Loaded += ButtonLoaded;
        button.ActualThemeChanged -= ButtonThemeChanged;
        button.ActualThemeChanged += ButtonThemeChanged;
        if (button.IsLoaded)
        {
            ApplyStateResources(button);
        }
    }

    private static void ButtonLoaded(object sender, RoutedEventArgs args) => ApplyStateResources((Button)sender);

    private static void ButtonThemeChanged(FrameworkElement sender, object args) => ApplyStateResources((Button)sender);

    private static void ApplyStateResources(Button button)
    {
        var prefix = GetEmphasis(button) switch
        {
            ActionButtonEmphasis.Primary => "Primary",
            ActionButtonEmphasis.Secondary => "Secondary",
            ActionButtonEmphasis.Tertiary => "Tertiary",
            ActionButtonEmphasis.Destructive => "Destructive",
            _ => throw new InvalidOperationException("Unsupported Action Button emphasis."),
        };
        var resources = Application.Current.Resources;
        button.Resources["ButtonBackgroundPointerOver"] = resources[$"ColorAction{prefix}HoverBrush"];
        button.Resources["ButtonBackgroundPressed"] = resources[$"ColorAction{prefix}PressedBrush"];
        button.Resources["ButtonBackgroundDisabled"] = resources[$"ColorAction{prefix}DisabledBrush"];
        button.Resources["ButtonForegroundPointerOver"] = resources[$"ColorAction{prefix}ForegroundBrush"];
        button.Resources["ButtonForegroundPressed"] = resources[$"ColorAction{prefix}ForegroundBrush"];
        button.Resources["ButtonForegroundDisabled"] = resources["ColorTextDisabledBrush"];
        button.Resources["ButtonBorderBrushPointerOver"] = resources[$"ColorAction{prefix}HoverBrush"];
        button.Resources["ButtonBorderBrushPressed"] = resources[$"ColorAction{prefix}PressedBrush"];
        button.Resources["ButtonBorderBrushDisabled"] = resources["ColorBorderDisabledBrush"];
    }

    private static void RaiseLiveRegionChanged(FrameworkElement element)
    {
        var peer = FrameworkElementAutomationPeer.FromElement(element) ?? FrameworkElementAutomationPeer.CreatePeerForElement(element);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
