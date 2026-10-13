namespace FotoHavn.Protocol.Tests;

/// <summary>
/// Serialization snapshots: every command envelope (one per command), every server and client envelope shape, every
/// presentation record, and every test hook's arguments and result. Each lives in <c>Snapshots/&lt;group&gt;/&lt;name&gt;.json</c>.
/// To accept intended changes, run the tests with <c>FOTOHAVN_UPDATE_PROTOCOL=1</c> and review the snapshot diff.
/// </summary>
public sealed class SnapshotTests
{
    public static TheoryData<string> CommandNames => [.. CommandRegistry.All.Select(command => command.Name)];

    public static TheoryData<string> MessageNames => [.. ProtocolSamples.Messages.Select(sample => sample.Name)];

    public static TheoryData<string> HookValueNames => [.. ProtocolSamples.HookValues.Select(sample => sample.Name)];

    [Theory]
    [MemberData(nameof(CommandNames))]
    public void Command_envelope_matches_its_snapshot(string name)
    {
        Assert.True(CommandRegistry.TryGet(name, out var command));
        var payload = ProtocolSamples.Payload(command);
        var json = ProtocolJson.Serialize(new CommandMessage(ProtocolSamples.Id, command.Name, ProtocolJson.ToElement<object>(payload)));

        AssertSnapshot("commands", name, json, typeof(CommandMessage));
    }

    [Theory]
    [MemberData(nameof(MessageNames))]
    public void Message_matches_its_snapshot(string name)
    {
        var message = ProtocolSamples.Messages.Single(sample => sample.Name == name).Message;

        AssertSnapshot("messages", name, ProtocolJson.Serialize(message), message.GetType());
    }

    [Theory]
    [MemberData(nameof(HookValueNames))]
    public void Hook_value_matches_its_snapshot(string name)
    {
        var value = ProtocolSamples.HookValues.Single(sample => sample.Name == name).Value;

        AssertSnapshot("hooks", name, System.Text.Json.JsonSerializer.Serialize(value, value.GetType(), ProtocolJson.Options), value.GetType());
    }

    [Fact]
    public void Every_payload_with_fields_has_a_sample()
    {
        var withFields = CommandRegistry.All
            .Select(command => command.PayloadType)
            .Where(type => type.GetConstructors().All(constructor => constructor.GetParameters().Length > 0 || constructor.IsPrivate))
            .ToHashSet();

        Assert.Equal(
            withFields.Select(type => type.Name).Order(StringComparer.Ordinal),
            ProtocolSamples.PayloadsWithFields.Keys.Select(type => type.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_snapshot_file_belongs_to_a_sample()
    {
        var expected = CommandRegistry.All.Select(command => Path.Combine("commands", command.Name + ".json"))
            .Concat(ProtocolSamples.Messages.Select(sample => Path.Combine("messages", sample.Name + ".json")))
            .Concat(ProtocolSamples.HookValues.Select(sample => Path.Combine("hooks", sample.Name + ".json")))
            .Order(StringComparer.Ordinal);

        var actual = Directory.EnumerateFiles(SourcePaths.SnapshotFolder, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(SourcePaths.SnapshotFolder, path))
            .Order(StringComparer.Ordinal);

        Assert.Equal(expected, actual);
    }

    private static void AssertSnapshot(string group, string name, string json, Type type)
    {
        var path = Path.Combine(SourcePaths.SnapshotFolder, group, name + ".json");
        if (SourcePaths.Updating)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json + "\n");
        }

        Assert.True(File.Exists(path), $"Snapshot {path} is missing. Run the tests with {SourcePaths.UpdateVariable}=1 to write it.");
        Assert.Equal(SourcePaths.ReadNormalized(path), json + "\n");
        Assert.Equal(json, ProtocolSamples.RoundTrip(json, type));
    }
}
