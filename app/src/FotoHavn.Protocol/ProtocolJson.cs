using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace FotoHavn.Protocol;

/// <summary>
/// JSON settings shared by the brain, the hosts, the tests, and the schema export. Strict on input: camelCase names
/// only, no unknown members, no numbers as strings, enums only as their camelCase names, required constructor
/// parameters and nullable annotations enforced. Compact on output, and every property is always written (null
/// included), so a presentation is a complete snapshot.
/// </summary>
public static class ProtocolJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    /// <summary>Serializes a message by its runtime type, so its <c>type</c> (and <c>role</c>) are written.</summary>
    public static string Serialize(ProtocolMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return JsonSerializer.Serialize(message, message.GetType(), Options);
    }

    /// <summary>
    /// Serializes any protocol value (payloads, presentations, hook arguments and results) by its runtime type, so a
    /// value held as a base type or <see cref="object"/> still writes all of its fields.
    /// </summary>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, value?.GetType() ?? typeof(T), Options);

    /// <summary>Converts a protocol value into a <see cref="JsonElement"/> by its runtime type, for command payloads and hook results.</summary>
    public static JsonElement ToElement<T>(T value) => JsonSerializer.SerializeToElement(value, value?.GetType() ?? typeof(T), Options);

    /// <summary>Reads a protocol value; throws <see cref="JsonException"/> when the JSON doesn't match it exactly.</summary>
    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException($"Expected {typeof(T).Name}, got null.");

    /// <summary>Reads a protocol value from an element; throws <see cref="JsonException"/> when it doesn't match exactly.</summary>
    public static T Deserialize<T>(JsonElement element) =>
        element.Deserialize<T>(Options) ?? throw new JsonException($"Expected {typeof(T).Name}, got null.");

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            NumberHandling = JsonNumberHandling.Strict,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            WriteIndented = false,
            MaxDepth = 32,

            // Messages go to WebView2 and WebSockets as JSON, never into HTML, so brain copy keeps its apostrophes and arrows.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly();
        return options;
    }
}
