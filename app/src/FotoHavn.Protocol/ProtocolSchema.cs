using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Protocol;

/// <summary>
/// Exports the protocol as one JSON Schema (draft 2020-12) with <see cref="JsonSchemaExporter"/>. Every protocol record
/// and enum becomes a named definition under <c>$defs</c>, referenced by <c>$ref</c>, so the generated TypeScript has
/// one named type per record. Hand-built definitions add what the exporter can't express: the <c>type</c> and
/// <c>role</c> constants of each envelope, one envelope per command (<c>XxxCommand</c>, keyed by its <c>name</c>), the
/// <c>Command</c>, <c>ClientMessage</c>, and <c>ServerMessage</c> unions, and the <c>CommandPayloadMap</c> and
/// <c>TestHookMap</c> lookup types. The committed copy is <c>schema/protocol.schema.json</c>; a test fails when it is stale.
/// </summary>
public static class ProtocolSchema
{
    public const string FileName = "protocol.schema.json";

    private const string DefsPrefix = "#/$defs/";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true, NewLine = "\n", IndentSize = 2 };

    private static readonly Type[] ClientMessageTypes = [typeof(HelloMessage), typeof(TestMessage), typeof(FrameAckMessage)];

    /// <summary>Messages that never travel on the bridge but whose shape the UI and the hosts share.</summary>
    private static readonly Type[] HostMessageTypes = [typeof(PreviewFrameMetadata)];

    private static readonly Type[] ServerMessageTypes =
    [
        typeof(OperatorWelcomeMessage),
        typeof(GuestWelcomeMessage),
        typeof(OperatorPresentationMessage),
        typeof(GuestPresentationMessage),
        typeof(CommandResultMessage),
        typeof(ProtocolMismatchMessage),
        typeof(ErrorMessage),
        typeof(TestResultMessage),
    ];

    /// <summary>The schema as committed: indented with two spaces, <c>\n</c> line endings, and a final newline.</summary>
    public static string ExportJson() => Export().ToJsonString(WriteOptions) + "\n";

    public static JsonObject Export()
    {
        var definitions = new SortedDictionary<string, JsonNode>(StringComparer.Ordinal);
        var pending = new Queue<Type>();
        var seen = new HashSet<Type>();

        void Include(Type type)
        {
            if (seen.Add(type))
            {
                pending.Enqueue(type);
            }
        }

        foreach (var type in ClientMessageTypes.Concat(ServerMessageTypes).Concat(HostMessageTypes))
        {
            Include(type);
        }

        foreach (var command in CommandRegistry.All)
        {
            Include(command.PayloadType);
        }

        foreach (var hook in TestHookRegistry.All)
        {
            if (hook.ArgsType is not null)
            {
                Include(hook.ArgsType);
            }

            Include(hook.ResultType);
        }

        Include(typeof(Role));

        while (pending.TryDequeue(out var type))
        {
            definitions[type.Name] = ExportDefinition(type, Include);
        }

        foreach (var command in CommandRegistry.All)
        {
            definitions[CommandDefinitionName(command)] = CommandEnvelope(command);
        }

        definitions["Command"] = Union("Command", CommandRegistry.All.Select(CommandDefinitionName));
        definitions["ClientMessage"] = Union("ClientMessage", [nameof(HelloMessage), "Command", nameof(TestMessage), nameof(FrameAckMessage)]);
        definitions["ServerMessage"] = Union("ServerMessage", ServerMessageTypes.Select(type => type.Name));
        definitions["CommandPayloadMap"] = Map(
            "CommandPayloadMap",
            CommandRegistry.All.Select(command => (command.Name, (JsonNode)Ref(command.PayloadType.Name))));
        definitions["TestHookMap"] = Map(
            "TestHookMap",
            TestHookRegistry.All.Select(hook => (hook.Name, (JsonNode)Map(
                null,
                [
                    ("args", hook.ArgsType is null ? new JsonObject { ["type"] = "null" } : Ref(hook.ArgsType.Name)),
                    ("result", Ref(hook.ResultType.Name)),
                ]))));

        var defs = new JsonObject();
        foreach (var (name, definition) in definitions)
        {
            defs[name] = definition;
        }

        return new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = "https://app.fotohavn/protocol.schema.json",
            ["title"] = "FotoHavnProtocol",
            ["description"] = "The FOTOHAVN UI and brain protocol. Generated from FotoHavn.Protocol by scripts/generate-protocol.ps1; do not edit.",
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["protocolVersion"] = new JsonObject { ["const"] = ProtocolVersion.Current },
                ["clientMessage"] = Ref("ClientMessage"),
                ["serverMessage"] = Ref("ServerMessage"),
                ["commandPayloads"] = Ref("CommandPayloadMap"),
                ["testHooks"] = Ref("TestHookMap"),
            },
            ["required"] = new JsonArray("protocolVersion", "clientMessage", "serverMessage", "commandPayloads", "testHooks"),
            ["additionalProperties"] = false,
            ["$defs"] = defs,
        };
    }

    private static JsonObject ExportDefinition(Type root, Action<Type> include)
    {
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = (context, node) =>
            {
                var type = context.TypeInfo.Type;
                if (context.Path.IsEmpty && type == root)
                {
                    return node;
                }

                var named = Nullable.GetUnderlyingType(type) ?? type;
                if (!IsNamed(named))
                {
                    return node;
                }

                include(named);
                return IsNullable(type, node)
                    ? new JsonObject { ["anyOf"] = new JsonArray(Ref(named.Name), new JsonObject { ["type"] = "null" }) }
                    : Ref(named.Name);
            },
        };

        var schema = ProtocolJson.Options.GetJsonSchemaAsNode(root, exporterOptions).AsObject();
        var definition = new JsonObject { ["title"] = root.Name };
        foreach (var (key, value) in schema.ToList())
        {
            schema.Remove(key);
            definition[key] = value;
        }

        if (typeof(ProtocolMessage).IsAssignableFrom(root))
        {
            AddConstants(root, definition);
        }

        // Any JSON value (test hook args and results): `true`, so the TypeScript is `unknown`. The exporter would add a
        // `default` to an optional one, which json-schema-to-typescript reads as an object.
        foreach (var property in ProtocolJson.Options.GetTypeInfo(root).Properties)
        {
            if ((Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType) == typeof(JsonElement))
            {
                definition["properties"]![property.Name] = JsonValue.Create(true);
            }
        }

        return definition;
    }

    /// <summary>Makes the envelope's get-only <c>type</c> (and <c>role</c>) required constants.</summary>
    private static void AddConstants(Type messageType, JsonObject definition)
    {
        var message = (ProtocolMessage)RuntimeHelpers.GetUninitializedObject(messageType);
        var properties = definition["properties"]!.AsObject();
        var constants = new List<(string Name, string Value)> { ("type", message.Type) };
        if (message is IRoleMessage roleMessage)
        {
            constants.Add(("role", JsonNamingPolicy.CamelCase.ConvertName(roleMessage.Role.ToString())));
        }

        var required = definition["required"]!.AsArray();
        definition.Remove("required");
        var requiredNames = required.Select(node => node!.GetValue<string>()).ToList();
        foreach (var (name, value) in constants)
        {
            properties[name] = new JsonObject { ["const"] = value };
            requiredNames.Remove(name);
        }

        definition["required"] = new JsonArray([.. constants.Select(c => (JsonNode)c.Name), .. requiredNames.Select(n => (JsonNode)n)]);
    }

    private static JsonObject CommandEnvelope(CommandDescriptor command) => new()
    {
        ["title"] = CommandDefinitionName(command),
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["type"] = new JsonObject { ["const"] = MessageTypes.Command },
            ["id"] = new JsonObject { ["type"] = "string", ["format"] = "uuid" },
            ["name"] = new JsonObject { ["const"] = command.Name },
            ["payload"] = Ref(command.PayloadType.Name),
        },
        ["required"] = new JsonArray("type", "id", "name", "payload"),
        ["additionalProperties"] = false,
    };

    private static string CommandDefinitionName(CommandDescriptor command) => command.Name + "Command";

    private static JsonObject Union(string title, IEnumerable<string> members) => new()
    {
        ["title"] = title,
        ["anyOf"] = new JsonArray([.. members.Select(name => (JsonNode)Ref(name))]),
    };

    private static JsonObject Map(string? title, IEnumerable<(string Name, JsonNode Schema)> entries)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        foreach (var (name, schema) in entries)
        {
            properties[name] = schema;
            required.Add(name);
        }

        var map = new JsonObject();
        if (title is not null)
        {
            map["title"] = title;
        }

        map["type"] = "object";
        map["properties"] = properties;
        map["required"] = required;
        map["additionalProperties"] = false;
        return map;
    }

    private static JsonObject Ref(string definitionName) => new() { ["$ref"] = DefsPrefix + definitionName };

    private static bool IsNamed(Type type) =>
        type.FullName!.StartsWith("FotoHavn.Protocol.", StringComparison.Ordinal);

    private static bool IsNullable(Type type, JsonNode node)
    {
        if (Nullable.GetUnderlyingType(type) is not null)
        {
            return true;
        }

        return node is JsonObject schema
            && schema["type"] is JsonArray types
            && types.Any(entry => entry!.GetValue<string>() == "null");
    }
}
