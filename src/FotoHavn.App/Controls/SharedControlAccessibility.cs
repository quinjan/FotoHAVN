using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace FotoHavn.App.Controls;

public static class SharedControlAccessibility
{
    public static void ConfigureActionButton(Button button, string semanticScope, string label)
    {
        Configure(button, SemanticAutomationIds.ActionButton, semanticScope, label);
    }

    public static void ConfigureIconAction(Button button, string semanticScope, string label)
    {
        Configure(button, SemanticAutomationIds.IconAction, semanticScope, label);
        ToolTipService.SetToolTip(button, label);
    }

    public static void ConfigureTextField(TextBox field, string semanticScope, string label, string helpText = "")
    {
        Configure(field, SemanticAutomationIds.TextField, semanticScope, label);
        AutomationProperties.SetHelpText(field, helpText);
    }

    public static void ConfigureSelectField(ComboBox field, string semanticScope, string label, string helpText = "")
    {
        Configure(field, SemanticAutomationIds.SelectField, semanticScope, label);
        AutomationProperties.SetHelpText(field, helpText);
    }

    public static void ConfigureReadOnlyValue(TextBlock value, string semanticScope, string label)
    {
        ArgumentNullException.ThrowIfNull(value);
        AutomationProperties.SetAutomationId(
            value,
            SemanticAutomationIds.Scoped(SemanticAutomationIds.ReadOnlyValue, semanticScope));
        AutomationProperties.SetName(value, $"{label}: {value.Text}");
    }

    public static void ConfigureProgress(ProgressRing progress, string semanticScope, string label)
    {
        Configure(progress, SemanticAutomationIds.ProgressIndicator, semanticScope, label);
        AutomationProperties.SetLiveSetting(progress, AutomationLiveSetting.Off);
    }

    public static void ConfigureProgress(ProgressBar progress, string semanticScope, string label)
    {
        Configure(progress, SemanticAutomationIds.ProgressIndicator, semanticScope, label);
        AutomationProperties.SetLiveSetting(progress, AutomationLiveSetting.Off);
    }

    private static void Configure(Control control, string prefix, string semanticScope, string label)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        AutomationProperties.SetAutomationId(control, SemanticAutomationIds.Scoped(prefix, semanticScope));
        AutomationProperties.SetName(control, label);
    }
}
