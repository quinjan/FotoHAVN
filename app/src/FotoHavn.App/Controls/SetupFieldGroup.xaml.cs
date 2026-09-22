using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace FotoHavn.App.Controls;

public enum SetupFieldState
{
    Clean,
    Dirty,
    Checking,
    Ready,
    Invalid,
    Unavailable,
}

public sealed partial class SetupFieldGroup : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(SetupFieldGroup), new PropertyMetadata(string.Empty, OnAssociationChanged));
    public static readonly DependencyProperty FieldContentProperty = DependencyProperty.Register(
        nameof(FieldContent), typeof(object), typeof(SetupFieldGroup), new PropertyMetadata(null, OnAssociationChanged));
    public static readonly DependencyProperty HelperContentProperty = DependencyProperty.Register(
        nameof(HelperContent), typeof(object), typeof(SetupFieldGroup), new PropertyMetadata(null));

    public SetupFieldGroup()
    {
        InitializeComponent();
        AutomationProperties.SetAutomationId(this, SemanticAutomationIds.SetupFieldGroup);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public object? FieldContent
    {
        get => GetValue(FieldContentProperty);
        set => SetValue(FieldContentProperty, value);
    }

    public object? HelperContent
    {
        get => GetValue(HelperContentProperty);
        set => SetValue(HelperContentProperty, value);
    }

    public void Present(SetupFieldState state, string status = "")
    {
        var helpText = state == SetupFieldState.Dirty ? "Changed from the saved Event." : status;
        AutomationProperties.SetItemStatus(this, state.ToString());
        AutomationProperties.SetHelpText(this, helpText);
        if (FieldContent is FrameworkElement field)
        {
            AutomationProperties.SetItemStatus(field, state.ToString());
            AutomationProperties.SetHelpText(field, helpText);
        }
        DirtyIndicator.Visibility = state == SetupFieldState.Dirty ? Visibility.Visible : Visibility.Collapsed;
        HelperHost.Visibility = string.IsNullOrWhiteSpace(status) ? Visibility.Visible : Visibility.Collapsed;
        if (state is SetupFieldState.Clean or SetupFieldState.Dirty or SetupFieldState.Ready)
        {
            StatusHost.Visibility = Visibility.Collapsed;
            StatusHost.Present(StatusSeverity.Neutral, string.Empty, AutomationLiveSetting.Off);
            return;
        }

        StatusHost.Visibility = Visibility.Visible;
        StatusHost.Present(
            state == SetupFieldState.Checking ? StatusSeverity.Info :
            state == SetupFieldState.Invalid ? StatusSeverity.Danger : StatusSeverity.Warning,
            status,
            state == SetupFieldState.Checking ? AutomationLiveSetting.Polite : AutomationLiveSetting.Assertive);
    }

    private static void OnAssociationChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is SetupFieldGroup group && group.FieldContent is FrameworkElement field && group.LabelText is not null)
        {
            AutomationProperties.SetLabeledBy(field, group.LabelText);
            AutomationProperties.SetName(group, group.Label);
            AutomationProperties.SetAutomationId(
                group,
                SemanticAutomationIds.Scoped(SemanticAutomationIds.SetupFieldGroup, group.Label.Replace(" ", string.Empty)));
        }
    }
}
