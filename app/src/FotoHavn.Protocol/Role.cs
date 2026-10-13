namespace FotoHavn.Protocol;

/// <summary>
/// The role of a connection: the Operator Screen or the Guest Screen. The host assigns it (one WebView2 per role, or a
/// per-role startup token in the headless host); the page never declares or changes it and learns it from <c>welcome</c>.
/// </summary>
public enum Role
{
    Operator,
    Guest,
}
