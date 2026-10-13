using System.Text.Json.Nodes;

namespace FotoHavn.Protocol.Tests;

public sealed class SchemaShapeTests
{
    private static readonly JsonObject Schema = ProtocolSchema.Export();

    private static JsonObject Definitions => Schema["$defs"]!.AsObject();

    [Fact]
    public void Root_pins_the_protocol_version()
    {
        Assert.Equal(ProtocolVersion.Current, Schema["properties"]!["protocolVersion"]!["const"]!.GetValue<int>());
    }

    [Fact]
    public void Every_command_has_an_envelope_keyed_by_its_name()
    {
        foreach (var command in CommandRegistry.All)
        {
            var envelope = Definitions[command.Name + "Command"]!;

            Assert.Equal("command", envelope["properties"]!["type"]!["const"]!.GetValue<string>());
            Assert.Equal(command.Name, envelope["properties"]!["name"]!["const"]!.GetValue<string>());
            Assert.Equal("#/$defs/" + command.PayloadType.Name, envelope["properties"]!["payload"]!["$ref"]!.GetValue<string>());
            Assert.NotNull(Definitions[command.PayloadType.Name]);
        }

        Assert.Equal(CommandRegistry.All.Count, Definitions["Command"]!["anyOf"]!.AsArray().Count);
        Assert.Equal(CommandRegistry.All.Count, Definitions["CommandPayloadMap"]!["required"]!.AsArray().Count);
    }

    [Fact]
    public void Envelopes_declare_their_type_and_role_as_required_constants()
    {
        var welcome = Definitions[nameof(GuestWelcomeMessage)]!;

        Assert.Equal("welcome", welcome["properties"]!["type"]!["const"]!.GetValue<string>());
        Assert.Equal("guest", welcome["properties"]!["role"]!["const"]!.GetValue<string>());
        Assert.Equal(["type", "role", "protocolVersion", "revision", "presentation"], welcome["required"]!.AsArray().Select(n => n!.GetValue<string>()));
    }

    [Fact]
    public void Every_reference_resolves_to_a_definition()
    {
        var references = new List<string>();
        Collect(Schema, references);

        Assert.NotEmpty(references);
        Assert.All(references, reference =>
        {
            Assert.StartsWith("#/$defs/", reference, StringComparison.Ordinal);
            Assert.True(Definitions.ContainsKey(reference["#/$defs/".Length..]), reference);
        });
    }

    [Fact]
    public void Nullable_records_are_a_reference_or_null()
    {
        var eventList = Definitions["OperatorPresentation"]!["properties"]!["eventList"]!;

        Assert.Equal("#/$defs/EventListView", eventList["anyOf"]![0]!["$ref"]!.GetValue<string>());
        Assert.Equal("null", eventList["anyOf"]![1]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Hooks_without_arguments_take_null()
    {
        var hooks = Definitions["TestHookMap"]!["properties"]!;

        Assert.Equal("null", hooks["test.getDiagnostics"]!["properties"]!["args"]!["type"]!.GetValue<string>());
        Assert.Equal("#/$defs/CrashUiArgs", hooks["test.crashUi"]!["properties"]!["args"]!["$ref"]!.GetValue<string>());
    }

    private static void Collect(JsonNode? node, List<string> references)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (key == "$ref")
                    {
                        references.Add(value!.GetValue<string>());
                    }
                    else
                    {
                        Collect(value, references);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Collect(item, references);
                }

                break;
        }
    }
}
