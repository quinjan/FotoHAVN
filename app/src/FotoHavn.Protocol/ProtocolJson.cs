using System.Text.Json;

namespace FotoHavn.Protocol;

/// <summary>
/// JSON settings shared by the brain, the hosts, and the UI bridge. Commands and presentations arrive in M2.
/// </summary>
public static class ProtocolJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
