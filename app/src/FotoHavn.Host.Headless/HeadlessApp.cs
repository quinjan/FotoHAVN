using System.Globalization;
using System.Net;
using System.Text.Json;
using FotoHavn.Core;
using FotoHavn.Catalog;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Catalog;
using FotoHavn.Core.Displays;
using FotoHavn.Core.Media;
using FotoHavn.Core.Fakes;
using FotoHavn.Host.Headless.Preview;
using FotoHavn.Protocol;
using FotoHavn.Storage;
using FotoHavn.Storage.DataPath;
using FotoHavn.Storage.FileSystem;
using FotoHavn.Storage.Reconciliation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Host.Headless;

/// <summary>What the headless host runs with. Tests replace the brain, previews, displays, and logging.</summary>
/// <param name="DataFolder">The Data folder (fully qualified); created at start.</param>
/// <param name="UiFolder">The built UI to serve, or null when it isn't built.</param>
/// <param name="Port">The loopback port; 0 picks a free one.</param>
internal sealed record HeadlessSettings(string DataFolder, string? UiFolder, int Port)
{
    /// <summary>The default fake displays: a primary and a guest display side by side, both 1920×1080 at 100%.</summary>
    public static IReadOnlyList<DisplayInfo> DefaultDisplays { get; } = [FakeDisplayAdapter.DefaultPrimary, FakeDisplayAdapter.DefaultGuest];

    public RoleTokens? Tokens { get; init; }

    /// <summary>Builds the brain; the default is the real <see cref="Brain"/>.</summary>
    public Func<BrainDependencies, IBrain>? BrainFactory { get; init; }

    /// <summary>
    /// Serves <c>preview.fotohavn</c> instead of the shared <see cref="PreviewStore"/> cache, so a test can fix what is found.
    /// Null serves the cache the brain fills.
    /// </summary>
    public IPreviewSource? Previews { get; init; }

    /// <summary>The cache the brain puts composed previews in; null creates one. Served at <c>preview.fotohavn</c> unless <see cref="Previews"/> is set.</summary>
    public PreviewCache? PreviewStore { get; init; }

    public IDisplayAdapter? Displays { get; init; }

    /// <summary>The camera adapter; the default is a <see cref="FakeCameraAdapter"/> with one camera on the real clock.</summary>
    public ICameraAdapter? Camera { get; init; }

    /// <summary>
    /// Builds the camera adapter from the host's preview channel, so a test's adapter delivers frames to the sockets.
    /// Ignored when <see cref="Camera"/> is set.
    /// </summary>
    public Func<IPreviewChannel, ICameraAdapter>? CameraFactory { get; init; }

    public Action<ILoggingBuilder>? ConfigureLogging { get; init; }

    /// <summary>The storage fault to inject before the brain opens the store (E2E builds only; a field build ignores it).</summary>
    public FotoHavn.Protocol.Testing.StorageFaultKind? StorageFault { get; init; }
}

/// <summary>Where a started headless host listens.</summary>
internal sealed record HeadlessEndpoints(int Port, Uri BaseUrl, Uri BridgeUrl, Uri OperatorUrl, Uri GuestUrl, Uri MediaUrl, Uri PreviewUrl);

/// <summary>
/// The headless host: Kestrel on 127.0.0.1 with the real brain, a WebSocket bridge at <c>/bridge?token=</c> (the token
/// decides the role), the built UI with an SPA fallback, and the media and preview routes. Composition happens here, so
/// <c>Program</c> and the integration tests run the same host.
/// </summary>
internal sealed partial class HeadlessApp : IAsyncDisposable
{
    private static readonly TimeSpan StoreDisposeTimeout = TimeSpan.FromSeconds(2);

    private readonly DedicatedThreadBrainExecutor _executor;
    private readonly SqliteEventStore _store;
    private PhysicalFileProvider? _ui;
    private bool _started;
    private bool _disposed;

    private HeadlessApp(WebApplication app, HeadlessSettings settings, RoleTokens tokens, DedicatedThreadBrainExecutor executor, IBrain brain, HeadlessHostServices hostServices, BridgeSessions sessions, WebSocketPreviewChannel previews, SqliteEventStore store, IPreviewSource previewSource)
    {
        App = app;
        Settings = settings;
        Tokens = tokens;
        _executor = executor;
        _store = store;
        PreviewSource = previewSource;
        Brain = brain;
        HostServices = hostServices;
        Sessions = sessions;
        Previews = previews;
    }

    public WebApplication App { get; }

    /// <summary>What <c>/preview/&lt;key&gt;.png</c> serves: the brain's preview cache unless the settings fix another source.</summary>
    public IPreviewSource PreviewSource { get; }

    public HeadlessSettings Settings { get; }

    public RoleTokens Tokens { get; }

    public IBrain Brain { get; }

    public HeadlessHostServices HostServices { get; }

    public BridgeSessions Sessions { get; }

    /// <summary>The frame channel: the camera adapter posts preview frames to it; it sends them to the role's socket.</summary>
    public WebSocketPreviewChannel Previews { get; }

    public static HeadlessApp Create(HeadlessSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!Path.IsPathFullyQualified(settings.DataFolder))
        {
            throw new ArgumentException("The Data folder must be a fully qualified path.", nameof(settings));
        }

        Directory.CreateDirectory(settings.DataFolder);

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory });
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, settings.Port));
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
        settings.ConfigureLogging?.Invoke(builder.Logging);
        var app = builder.Build();

        var loggers = app.Services.GetRequiredService<ILoggerFactory>();
        var hostLogger = loggers.CreateLogger("FotoHavn.Host.Headless");
        var tokens = settings.Tokens ?? RoleTokens.Create();
        var displays = settings.Displays ?? new FakeDisplayAdapter([.. HeadlessSettings.DefaultDisplays]);
        var hostServices = new HeadlessHostServices(displays, hostLogger);
        var sessions = new BridgeSessions();
        var previews = new WebSocketPreviewChannel(TimeProvider.System);
        var brainLogger = loggers.CreateLogger("FotoHavn.Brain");
        var executor = new DedicatedThreadBrainExecutor(exception => LogBrainWorkFailed(brainLogger, exception));
        IStorageFileSystem fileSystem = new PhysicalStorageFileSystem(settings.DataFolder);
#if E2E_HOOKS
        var faults = new FaultInjectingStorageFileSystem(fileSystem);
        fileSystem = faults;
        var storageHooks = new E2E.HeadlessStorageTestHooks(faults);
        if (settings.StorageFault is { } startupFault)
        {
            // Before the brain exists, so the store's first write (OpenAsync) fails.
            storageHooks.InjectAtStartup(startupFault);
        }
#endif
        var store = new SqliteEventStore(settings.DataFolder, SystemClock.Instance, fileSystem, new WindowsDataPathProbe(), new StartupReconciler(SystemClock.Instance));
        var camera = settings.Camera
            ?? settings.CameraFactory?.Invoke(previews)
            ?? new FakeCameraAdapter(SystemClock.Instance, previews);
#if E2E_HOOKS
        var cameraFaults = new FotoHavn.Core.Hooks.FaultInjectingCameraAdapter(camera);
        camera = cameraFaults;
#endif
        var previewCache = settings.PreviewStore ?? new PreviewCache();
        var dependencies = new BrainDependencies(executor, ClockComposition.ForBuild(SystemClock.Instance), displays, camera, hostServices, store, new CatalogData(), new SkiaCompositor(), previewCache, new NoOpWakeLock())
        {
            Logger = brainLogger,
            PreviewStats = previews,
            CaptureEncoder = new Diagnostics.HostCaptureEncoder(),
#if E2E_HOOKS
            CameraFaults = cameraFaults,
            HostTestHooks = new E2E.HeadlessTestHooks(sessions, app.Lifetime, settings.DataFolder),
            StorageTestHooks = storageHooks,
#endif
        };

        IBrain brain;
        try
        {
            brain = (settings.BrainFactory ?? (deps => new Brain(deps)))(dependencies);
        }
        catch
        {
            executor.Dispose();
            store.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw;
        }

        var host = new HeadlessApp(app, settings, tokens, executor, brain, hostServices, sessions, previews, store, settings.Previews ?? previewCache);
        host.MapRoutes(hostLogger);
        return host;
    }

    /// <summary>Starts Kestrel and returns the URLs, with the port Kestrel picked.</summary>
    public async Task<HeadlessEndpoints> StartAsync(CancellationToken cancellationToken = default)
    {
        await App.StartAsync(cancellationToken).ConfigureAwait(false);
        _started = true;
        var address = App.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("Kestrel reported no address.");
        var port = new Uri(address).Port;
        var baseUrl = new Uri(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/"));
        return new HeadlessEndpoints(
            port,
            baseUrl,
            new Uri(string.Create(CultureInfo.InvariantCulture, $"ws://127.0.0.1:{port}/bridge")),
            new Uri(baseUrl, "?token=" + Tokens.Operator),
            new Uri(baseUrl, "?token=" + Tokens.Guest),
            new Uri(baseUrl, "media/"),
            new Uri(baseUrl, "preview/"));
    }

    /// <summary>
    /// Writes the startup lines: human-readable URLs, then one JSON line (<c>{"event":"listening",…}</c>) for tooling.
    /// </summary>
    public void WriteStartup(TextWriter output, HeadlessEndpoints endpoints)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(endpoints);
        output.WriteLine($"FOTOHAVN headless host listening on {endpoints.BaseUrl}");
        output.WriteLine($"data: {Settings.DataFolder}");
        output.WriteLine($"ui: {Settings.UiFolder ?? "not built (run npm run build in ui/)"}");
        output.WriteLine($"operator: {endpoints.OperatorUrl}");
        output.WriteLine($"guest: {endpoints.GuestUrl}");
        output.WriteLine("Press Ctrl+C to stop.");
        output.WriteLine(JsonSerializer.Serialize(new
        {
            @event = "listening",
            port = endpoints.Port,
            url = endpoints.BaseUrl.ToString(),
            bridgeUrl = endpoints.BridgeUrl.ToString(),
            operatorUrl = endpoints.OperatorUrl.ToString(),
            guestUrl = endpoints.GuestUrl.ToString(),
            operatorToken = Tokens.Operator,
            guestToken = Tokens.Guest,
            mediaUrl = endpoints.MediaUrl.ToString(),
            previewUrl = endpoints.PreviewUrl.ToString(),
            dataFolder = Settings.DataFolder,
            uiFolder = Settings.UiFolder,
        }));
        output.Flush();
    }

    /// <summary>Stops Kestrel (closing every socket), then disposes the brain and its executor.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_started)
        {
            await App.StopAsync().ConfigureAwait(false);
        }

        Brain.Dispose();
        _executor.Dispose();
        await DisposeStoreAsync().ConfigureAwait(false);
        _ui?.Dispose();
        await App.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Disposes the store after the brain; its background deletion gets a bounded moment to finish (the reconciler completes the rest at the next start).</summary>
    private async Task DisposeStoreAsync()
    {
        try
        {
            await _store.DisposeAsync().AsTask().WaitAsync(StoreDisposeTimeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Unfinished deletion is invisible and the next start's reconciler finishes it.
        }
    }

    private void MapRoutes(ILogger logger)
    {
        App.Use(LoopbackGuard.RequireLoopbackHost);
        App.UseWebSockets();

        _ui = Settings.UiFolder is null ? null : new PhysicalFileProvider(Settings.UiFolder);
        var ui = _ui;
        if (ui is not null)
        {
            App.UseDefaultFiles(new DefaultFilesOptions { FileProvider = ui });
            App.UseStaticFiles(new StaticFileOptions { FileProvider = ui });
        }

        string[] readOnly = [HttpMethods.Get, HttpMethods.Head];
        App.MapMethods("/media/{**path}", readOnly, (HttpContext context) => MediaRoutes.Media(context, Settings.DataFolder));
        App.MapMethods("/preview/{**key}", readOnly, (HttpContext context) => MediaRoutes.Preview(context, PreviewSource));
        App.Map("/bridge", (HttpContext context) => AcceptBridgeAsync(context, logger));
        App.MapFallback((HttpContext context) => Fallback(context, ui));
    }

    private async Task AcceptBridgeAsync(HttpContext context, ILogger logger)
    {
        var token = context.Request.Query["token"];
        if (token.Count != 1 || !Tokens.TryGetRole(token[0], out var role))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!LoopbackGuard.IsAllowedOrigin(context.Request.Headers.Origin))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        var session = new BridgeSession(socket, role, logger, Previews);
        Sessions.Add(session);
        LogClientConnected(logger, role);
        try
        {
            await session.RunAsync(Brain, App.Lifetime.ApplicationStopping, context.RequestAborted).ConfigureAwait(false);
        }
        finally
        {
            Sessions.Remove(session);
            LogClientDisconnected(logger, role);
        }
    }

    /// <summary>
    /// The SPA fallback: GET/HEAD of any other extension-less path serves <c>index.html</c>, so client routes and
    /// <c>/?token=</c> load the UI. The media, preview, and bridge prefixes never fall back.
    /// </summary>
    private static IResult Fallback(HttpContext context, PhysicalFileProvider? ui)
    {
        var path = context.Request.Path;
        if (!(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
            || path.StartsWithSegments("/media", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/preview", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/bridge", StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound();
        }

        if (ui is null)
        {
            return Results.Text("The FOTOHAVN UI is not built. Run npm run build in ui/, or pass --ui <folder>.", "text/plain");
        }

        var index = ui.GetFileInfo("index.html");
        if (!index.Exists || index.PhysicalPath is null)
        {
            return Results.NotFound();
        }

        context.Response.Headers.CacheControl = "no-cache";
        return Results.File(index.PhysicalPath, "text/html; charset=utf-8");
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "A brain work item failed.")]
    private static partial void LogBrainWorkFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Role} client connected.")]
    private static partial void LogClientConnected(ILogger logger, Role role);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Role} client disconnected.")]
    private static partial void LogClientDisconnected(ILogger logger, Role role);
}
