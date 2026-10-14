using FotoHavn.Core;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Media;

namespace FotoHavn.Host.Headless.Tests.Support;

/// <summary>
/// A started headless host on a random loopback port with a <see cref="FakeBrain"/>, a scratch Data folder, and a scratch
/// UI folder, all under the test output folder. Disposing stops the host and deletes the folders.
/// </summary>
internal sealed class TestHost : IAsyncDisposable
{
    public const string IndexHtml = "<!doctype html><title>FOTOHAVN test UI</title>";
    public const string AppJs = "console.log('FOTOHAVN test UI');";

    private readonly string _root;

    private TestHost(string root, HeadlessApp app, HeadlessEndpoints endpoints, FakeBrain brain, BrainDependencies? dependencies)
    {
        Dependencies = dependencies;
        _root = root;
        App = app;
        Endpoints = endpoints;
        Brain = brain;
        Http = new HttpClient { BaseAddress = endpoints.BaseUrl };
    }

    public HeadlessApp App { get; }

    public HeadlessEndpoints Endpoints { get; }

    public FakeBrain Brain { get; }

    /// <summary>What the host gave its brain (null with the real brain).</summary>
    public BrainDependencies? Dependencies { get; }

    public HttpClient Http { get; }

    public string DataFolder => App.Settings.DataFolder;

    public static string NewScratchFolder()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "scratch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static async Task<TestHost> StartAsync(bool withUi = true, bool failConnect = false, IPreviewSource? previews = null, bool realBrain = false, FotoHavn.Protocol.Testing.StorageFaultKind? storageFault = null, Func<IPreviewChannel, ICameraAdapter>? cameraFactory = null)
    {
        var root = NewScratchFolder();
        string? ui = null;
        if (withUi)
        {
            ui = Path.Combine(root, "ui");
            Directory.CreateDirectory(Path.Combine(ui, "assets"));
            await File.WriteAllTextAsync(Path.Combine(ui, "index.html"), IndexHtml, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(ui, "assets", "app.js"), AppJs, TestContext.Current.CancellationToken);
        }

        var brain = new FakeBrain { FailConnect = failConnect };
        BrainDependencies? dependencies = null;
        var settings = new HeadlessSettings(Path.Combine(root, "Data"), ui, 0)
        {
            BrainFactory = realBrain ? null : given =>
            {
                dependencies = given;
                return brain;
            },
            CameraFactory = cameraFactory,
            StorageFault = storageFault,
            Previews = previews,
            ConfigureLogging = logging => logging.ClearProviders(),
        };
        var app = HeadlessApp.Create(settings);
        var endpoints = await app.StartAsync(TestContext.Current.CancellationToken);
        return new TestHost(root, app, endpoints, brain, dependencies);
    }

    public Task<BridgeClient> ConnectAsync(Role role) =>
        BridgeClient.ConnectAsync(Endpoints.BridgeUrl, role == Role.Operator ? App.Tokens.Operator : App.Tokens.Guest);

    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        await App.DisposeAsync();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A file still open by the OS; the scratch folder lives under bin/ and is harmless.
        }
    }
}
