using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FotoHavn.App.Controls;

public enum FieldCondition
{
    Valid,
    Invalid,
    Unavailable,
}

public static class FieldFeedback
{
    private static readonly DependencyProperty OriginalIsEnabledProperty = DependencyProperty.RegisterAttached(
        "OriginalIsEnabled", typeof(bool), typeof(FieldFeedback), new PropertyMetadata(true));

    private static readonly DependencyProperty TransitionProperty = DependencyProperty.RegisterAttached(
        "Transition", typeof(string), typeof(FieldFeedback), new PropertyMetadata(string.Empty));

    public static void Present(
        Control field,
        InlineStatus visibleStatus,
        FieldCondition condition,
        string statusMessage = "",
        bool focusInvalidField = false)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(visibleStatus);
        if (field.GetValue(TransitionProperty) is not string previous || string.IsNullOrEmpty(previous))
        {
            field.SetValue(OriginalIsEnabledProperty, field.IsEnabled);
        }

        var transition = $"{condition}:{statusMessage}";
        field.SetValue(TransitionProperty, transition);
        field.BorderBrush = (Brush)Application.Current.Resources[condition switch
        {
            FieldCondition.Valid => "ColorBorderDefaultBrush",
            FieldCondition.Invalid => "ColorStatusDangerBorderBrush",
            FieldCondition.Unavailable => "ColorBorderDisabledBrush",
            _ => throw new InvalidOperationException("Unsupported field condition."),
        }];
        field.IsEnabled = condition != FieldCondition.Unavailable && (bool)field.GetValue(OriginalIsEnabledProperty);
        AutomationProperties.SetHelpText(field, statusMessage);
        visibleStatus.Visibility = condition == FieldCondition.Valid ? Visibility.Collapsed : Visibility.Visible;
        visibleStatus.Present(condition switch
        {
            FieldCondition.Valid => StatusSeverity.Neutral,
            FieldCondition.Invalid => StatusSeverity.Danger,
            FieldCondition.Unavailable => StatusSeverity.Warning,
            _ => throw new InvalidOperationException("Unsupported field condition."),
        }, condition == FieldCondition.Valid ? string.Empty : statusMessage, AutomationLiveSetting.Assertive);
        AutomationProperties.SetLiveSetting(field, AutomationLiveSetting.Off);

        if (focusInvalidField && condition == FieldCondition.Invalid)
        {
            field.Focus(FocusState.Programmatic);
        }

    }

    public static void SetAutomationScope(Control field, string prefix, string semanticScope) =>
        AutomationProperties.SetAutomationId(field, SemanticAutomationIds.Scoped(prefix, semanticScope));

}
