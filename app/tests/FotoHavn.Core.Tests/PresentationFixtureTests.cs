using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;

namespace FotoHavn.Core.Tests;

/// <summary>
/// Brain-generated presentation fixtures for the UI (plan section 9, Schema and types; contract section 14): each file in
/// <c>ui/src/fixtures/presentations/</c> is a message a real brain sent, serialized with <see cref="ProtocolJson"/>. The
/// tests fail when a file is stale or extra; run them with <c>FOTOHAVN_UPDATE_FIXTURES=1</c> to rewrite the files, then
/// review the diff.
/// </summary>
public sealed class PresentationFixtureTests
{
    private const string FatalReason = "FOTOHAVN stopped. Close it and open it again.";

    private static readonly IReadOnlyDictionary<string, Func<string>> Fixtures = new Dictionary<string, Func<string>>(StringComparer.Ordinal)
    {
        ["operator-event-list-empty"] = () => Welcome(Role.Operator),
        ["guest-start"] = () => Welcome(Role.Guest),
        ["operator-fatal"] = () => FatalPresentation(Role.Operator),
        ["guest-fatal"] = () => FatalPresentation(Role.Guest),
        ["protocol-mismatch"] = ProtocolMismatch,
    };

    public static TheoryData<string> Names => [.. Fixtures.Keys];

    [Theory]
    [MemberData(nameof(Names))]
    public void Fixture_matches_what_the_brain_sends(string name)
    {
        var json = Fixtures[name]() + "\n";
        var path = Path.Combine(SourcePaths.PresentationFixtureFolder, name + ".json");
        if (SourcePaths.UpdatingFixtures)
        {
            Directory.CreateDirectory(SourcePaths.PresentationFixtureFolder);
            File.WriteAllText(path, json);
        }

        Assert.True(File.Exists(path), $"Fixture {path} is missing. Run the Core tests with {SourcePaths.UpdateFixturesVariable}=1 to write it.");
        Assert.Equal(json, SourcePaths.ReadNormalized(path));
        Assert.IsAssignableFrom<ServerMessage>(ServerMessageReader.Read(json));
    }

    [Fact]
    public void Every_fixture_file_comes_from_the_brain()
    {
        string[] files = Directory.Exists(SourcePaths.PresentationFixtureFolder)
            ? [.. Directory.EnumerateFiles(SourcePaths.PresentationFixtureFolder).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal)]
            : [];

        Assert.Equal(Fixtures.Keys.Select(name => name + ".json").Order(StringComparer.Ordinal), files);
    }

    private static string Welcome(Role role)
    {
        using var harness = new BrainHarness();
        var client = harness.Connect(role);
        client.Hello();
        harness.Run();
        return Assert.Single(client.Raw);
    }

    private static string FatalPresentation(Role role)
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(role);
        harness.Brain.ShowFatal(FatalReason);
        harness.Run();
        return Assert.Single(client.Raw);
    }

    private static string ProtocolMismatch()
    {
        using var harness = new BrainHarness();
        var client = harness.Connect(Role.Operator);
        client.Hello(ProtocolVersion.Current - 1);
        harness.Run();
        return Assert.Single(client.Raw);
    }
}
