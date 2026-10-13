using FotoHavn.Core.Fakes;
using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Brains;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;

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

    private static readonly IReadOnlyDictionary<string, Func<Task<string>>> Fixtures = new Dictionary<string, Func<Task<string>>>(StringComparer.Ordinal)
    {
        ["operator-starting"] = () => Task.FromResult(Starting()),
        ["operator-event-list-empty"] = () => Task.FromResult(Welcome(Role.Operator)),
        ["operator-event-list-with-events"] = EventListWithEvents,
        ["operator-storage-error-disk-full"] = () => Task.FromResult(StorageError(StorageErrorKind.DiskFull)),
        ["guest-start"] = () => Task.FromResult(Welcome(Role.Guest)),
        ["operator-fatal"] = () => Task.FromResult(FatalPresentation(Role.Operator)),
        ["guest-fatal"] = () => Task.FromResult(FatalPresentation(Role.Guest)),
        ["protocol-mismatch"] = () => Task.FromResult(ProtocolMismatch()),
        ["operator-camera-diagnostics-ready"] = () => Task.FromResult(CameraDiagnostics(new CameraDiagnosticsView(
            [new CameraOption(@"\\?\USB#VID_046D&PID_085E#{e5323777-f976-4f5b-9b55-b94699c46e44}\GLOBAL", "Integrated Webcam"), new CameraOption("fake-camera", "Fake camera")],
            @"\\?\USB#VID_046D&PID_085E#{e5323777-f976-4f5b-9b55-b94699c46e44}\GLOBAL",
            CameraState.Ready,
            null,
            new CameraFormatView(1920, 1080, 30),
            true,
            new CameraCaptureView("capture-20261014-071910-123.jpg", 1920, 1080, 412_345, true)))),
        ["operator-camera-diagnostics-checking"] = () => Task.FromResult(CameraDiagnostics(new CameraDiagnosticsView(
            [new CameraOption("fake-camera", "Fake camera")], "fake-camera", CameraState.Checking, null, null, false, null))),
        ["operator-camera-diagnostics-privacy-denied"] = () => Task.FromResult(CameraDiagnostics(new CameraDiagnosticsView(
            [new CameraOption("fake-camera", "Fake camera")], "fake-camera", CameraState.NotAvailable, CameraFailureKind.PrivacyDenied, null, false, null))),
        ["operator-camera-diagnostics-in-use"] = () => Task.FromResult(CameraDiagnostics(new CameraDiagnosticsView(
            [new CameraOption("fake-camera", "Fake camera")], "fake-camera", CameraState.InUseByAnotherApp, CameraFailureKind.InUseByAnotherApp, null, false, null))),
        ["operator-camera-diagnostics-no-cameras"] = () => Task.FromResult(CameraDiagnostics(new CameraDiagnosticsView([], null, CameraState.Idle, null, null, false, null))),
    };

    public static TheoryData<string> Names => [.. Fixtures.Keys];

    [Theory]
    [MemberData(nameof(Names))]
    public async Task Fixture_matches_what_the_brain_sends(string name)
    {
        var json = await Fixtures[name]() + "\n";
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

    private static string Starting()
    {
        using var harness = new BrainHarness(store: new StorageTests.ScriptedStore());
        var client = harness.Connect(Role.Operator);
        client.Hello();
        harness.Run();
        return Assert.Single(client.Raw);
    }

    private static string Welcome(Role role)
    {
        using var harness = new BrainHarness();
        var client = harness.Connect(role);
        client.Hello();
        harness.Run();
        return Assert.Single(client.Raw);
    }

    private static async Task<string> EventListWithEvents()
    {
        using var harness = new BrainHarness(store: await StorageTests.SeededStoreAsync());
        var client = harness.Connect(Role.Operator);
        client.Hello();
        harness.Run();
        return Assert.Single(client.Raw);
    }

    private static string StorageError(StorageErrorKind kind)
    {
        var store = new InMemoryEventStore();
        store.FailOpen(kind);
        using var harness = new BrainHarness(store: store);
        var client = harness.Connect(Role.Operator);
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

    private static string CameraDiagnostics(CameraDiagnosticsView view)
    {
        using var harness = new BrainHarness();
        var client = harness.Handshake(Role.Operator);
        harness.Brain.ShowCameraDiagnostics(view);
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
