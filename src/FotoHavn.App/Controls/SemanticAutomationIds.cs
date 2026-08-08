namespace FotoHavn.App.Controls;

public static class SemanticAutomationIds
{
    public const string ActionButton = "FotoHavn.ActionButton";
    public const string IconAction = "FotoHavn.IconAction";
    public const string TextField = "FotoHavn.TextField";
    public const string SelectField = "FotoHavn.SelectField";
    public const string ReadOnlyValue = "FotoHavn.ReadOnlyValue";
    public const string InlineStatus = "FotoHavn.InlineStatus";
    public const string StatusCallout = "FotoHavn.StatusCallout";
    public const string ProgressIndicator = "FotoHavn.ProgressIndicator";
    public const string Toast = "FotoHavn.Toast";
    public const string ModalDialog = "FotoHavn.ModalDialog";

    public static string Scoped(string prefix, string semanticScope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticScope);
        return $"{prefix}.{semanticScope}";
    }
}
