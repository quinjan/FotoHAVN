using FotoHavn.Core.Camera;
using FotoHavn.Core.Catalog;
using FotoHavn.Core.Displays;
using FotoHavn.Core.Hooks;
using FotoHavn.Core.Media;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FotoHavn.Core;

/// <summary>Everything the brain needs from its host.</summary>
/// <param name="Executor">The single-threaded executor the brain runs on. The host owns and disposes it.</param>
/// <param name="Clock">Time and timers.</param>
/// <param name="Displays">Display enumeration and change notifications.</param>
/// <param name="Camera">Camera discovery and opening. The host owns it; it was constructed with the host's preview channel, so frames never pass through the brain.</param>
/// <param name="Host">What the brain asks the host to do.</param>
/// <param name="Store">The Event store. The brain opens it at startup; the host owns and disposes it.</param>
/// <param name="Catalog">The Templates and Looks.</param>
/// <param name="Compositor">Composes Photo Strips and Look thumbnails. The brain calls it off the executor and posts the result back.</param>
/// <param name="Previews">Where the brain puts composed previews; the host serves the same cache at <c>preview.fotohavn</c>.</param>
/// <param name="WakeLock">Keeps Windows awake while an Event runs (acquired at Start Event step 7, released when the Event ends). <see cref="NoOpWakeLock"/> until M10.</param>
public sealed record BrainDependencies(
    IBrainExecutor Executor,
    IClock Clock,
    IDisplayAdapter Displays,
    ICameraAdapter Camera,
    IHostServices Host,
    IEventStore Store,
    ICatalog Catalog,
    ICompositor Compositor,
    IPreviewStore Previews,
    IWakeLock WakeLock)
{
    public ILogger Logger { get; init; } = NullLogger.Instance;

    /// <summary>The time zone that decides "today" for a new Event's default date. The machine's local zone in the app; tests and fixtures fix it so dates never depend on the machine.</summary>
    public TimeZoneInfo LocalTimeZone { get; init; } = TimeZoneInfo.Local;

    /// <summary>The preview channel's frame counters, for diagnostics only. Null reads as no preview channel.</summary>
    public IPreviewStats? PreviewStats { get; init; }

#if E2E_HOOKS
    /// <summary>The host side of the test hooks. Null makes every host hook fail with <c>notSupported</c>.</summary>
    public E2E.IHostTestHooks? HostTestHooks { get; init; }

    /// <summary>The host side of the storage fault hooks. Null makes them fail with <c>notSupported</c>.</summary>
    public E2E.IStorageTestHooks? StorageTestHooks { get; init; }

    /// <summary>
    /// The handle on the host's <see cref="FaultInjectingCameraAdapter"/>, which the host also passes as <see cref="Camera"/>.
    /// Null makes <c>test.injectCameraFault</c> and <c>test.clearCameraFault</c> fail with <c>notSupported</c>.
    /// </summary>
    public ICameraFaultControl? CameraFaults { get; init; }
#endif
}

/// <summary>
/// The brain: the handshake, role-checked command dispatch, per-role presentations with increasing revisions, display
/// change handling, and the brain-side test hooks. Every member posts its work to <see cref="BrainDependencies.Executor"/>;
/// brain state is touched only there, one work item at a time, in the order posted.
/// </summary>
/// <remarks>
/// <c>RetryStorage</c> and <c>DismissNotice</c> are implemented; the Event list, Event setup, and running Event commands are routed
/// to their own files (<c>Brain.Commands.cs</c>) and are stubs until M6 wave B; every other valid command from an allowed role is
/// answered <c>rejected notAvailable</c>. At startup the brain opens the Event store: the operator sees the Starting surface until
/// storage answers, then the Event list or, when the store can't open, the storage error. The guest sees the Start placeholder.
/// </remarks>
public sealed partial class Brain : IBrain
{
#if E2E_HOOKS
    private const bool HooksCompiled = true;
#else
    private const bool HooksCompiled = false;
#endif

    private readonly BrainDependencies _dependencies;
    private readonly ILogger _logger;
    private readonly List<Connection> _connections = [];
    private readonly PresentationChannel<OperatorPresentation> _operator;
    private readonly PresentationChannel<GuestPresentation> _guest;
    private CameraDiagnosticsView? _cameraDiagnosticsOverride;
    private IReadOnlyList<DisplayInfo> _realDisplays = [];
    private string? _fatalReason;
    private int _disposed;

    public Brain(BrainDependencies dependencies)
        : this(dependencies, HooksCompiled, CreateHostHookPort(dependencies), CreateStorageHookPort(dependencies), CreateCameraFaultControl(dependencies))
    {
    }

    /// <summary>The brain with its hook dispatcher switched on or off explicitly, so tests reach it in every build.</summary>
    internal Brain(BrainDependencies dependencies, bool hooksEnabled, IHostHookPort? hostHooks, IStorageHookPort? storageHooks = null, ICameraFaultControl? cameraFaults = null)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(dependencies.Store);
        ArgumentNullException.ThrowIfNull(dependencies.Camera);
        ArgumentNullException.ThrowIfNull(dependencies.Catalog);
        ArgumentNullException.ThrowIfNull(dependencies.Compositor);
        ArgumentNullException.ThrowIfNull(dependencies.Previews);
        ArgumentNullException.ThrowIfNull(dependencies.WakeLock);
        _dependencies = dependencies;
        _logger = dependencies.Logger;
        _hooksEnabled = hooksEnabled;
        _hostHooks = hostHooks;
        _storageHooks = storageHooks;
        _cameraFaults = cameraFaults;
        _operator = new PresentationChannel<OperatorPresentation>(
            Role.Operator,
            RenderOperator(),
            presentation => presentation.Surface.ToString(),
            (revision, presentation) => new OperatorWelcomeMessage(ProtocolVersion.Current, revision, presentation),
            (revision, presentation) => new OperatorPresentationMessage(revision, presentation));
        _guest = new PresentationChannel<GuestPresentation>(
            Role.Guest,
            RenderGuest(),
            presentation => presentation.Surface.ToString(),
            (revision, presentation) => new GuestWelcomeMessage(ProtocolVersion.Current, revision, presentation),
            (revision, presentation) => new GuestPresentationMessage(revision, presentation));

        dependencies.Displays.DisplaysChanged += OnDisplaysChanged;
        Post(ReadRealDisplays);
        StartCameraRuntime();
        Post(StartStorageOpen);
    }

    private IReadOnlyList<DisplayInfo> CurrentDisplays => _virtualDisplays?.Current ?? _realDisplays;

    public IBrainConnection Connect(Role role, Action<string> send)
    {
        ArgumentNullException.ThrowIfNull(send);
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Not a role.");
        }

        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        var connection = new Connection(this, role, send);
        Post(() => _connections.Add(connection));
        return connection;
    }

    /// <summary>Stops the brain: it unsubscribes from display changes and ignores all work still queued or posted later.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _dependencies.Displays.DisplaysChanged -= OnDisplaysChanged;
        _storageCancellation.Cancel();
        ShutdownCamera();
    }

    /// <summary>
    /// Shows (or, with null, hides) the camera diagnostics surface with <paramref name="view"/>. M5 wave A's stand-in so the
    /// UI's fixtures come from a real brain; wave B2 drives the view from the camera's real state and
    /// <c>test.openCameraDiagnostics</c>.
    /// </summary>
    internal void ShowCameraDiagnostics(CameraDiagnosticsView? view) =>
        Post(() =>
        {
            _cameraDiagnosticsOverride = view;
            if (view is null)
            {
                ReleaseDiagnosticsCaptures();
            }

            PublishChanges();
        });

    /// <summary>
    /// Switches both roles to their fatal surface with <paramref name="reason"/> (brain copy), for failures the brain can't
    /// recover from. Later milestones call it from the brain itself (storage, for one); M2 has no such failure.
    /// </summary>
    internal void ShowFatal(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Post(() =>
        {
            _fatalReason = reason;
            PublishChanges();
        });
    }

#pragma warning disable CA1859 // The field build of this method has no E2E port type to return.
    private static IHostHookPort? CreateHostHookPort(BrainDependencies dependencies)
#pragma warning restore CA1859
    {
        ArgumentNullException.ThrowIfNull(dependencies);
#if E2E_HOOKS
        return dependencies.HostTestHooks is { } hooks ? new E2E.HostTestHooksPort(hooks) : null;
#else
        return null;
#endif
    }

#pragma warning disable CA1859 // The field build of this method has no E2E type to read from.
    private static ICameraFaultControl? CreateCameraFaultControl(BrainDependencies dependencies)
#pragma warning restore CA1859
    {
        ArgumentNullException.ThrowIfNull(dependencies);
#if E2E_HOOKS
        return dependencies.CameraFaults;
#else
        return null;
#endif
    }

#pragma warning disable CA1859 // The field build of this method has no E2E port type to return.
    private static IStorageHookPort? CreateStorageHookPort(BrainDependencies dependencies)
#pragma warning restore CA1859
    {
        ArgumentNullException.ThrowIfNull(dependencies);
#if E2E_HOOKS
        return dependencies.StorageTestHooks is { } hooks ? new E2E.StorageTestHooksPort(hooks) : null;
#else
        return null;
#endif
    }

    private void Post(Action work)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        _dependencies.Executor.Post(() =>
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                work();
            }
        });
    }

    private void HandleMessage(Connection connection, string json)
    {
        var parsed = ClientMessageParser.Parse(json, connection.Role);
        switch (parsed)
        {
            case InvalidMessage invalid:
                connection.Send(invalid.Reply);
                break;
            case ParsedHello hello:
                HandleHello(connection, hello.Message.ProtocolVersion);
                break;
            case RejectedCommand rejected:
                Answer(connection, rejected.Name, CommandResultMessage.Rejected(
                    rejected.Id,
                    connection.Handshaken ? rejected.Reason : RejectReason.ProtocolMismatch));
                break;
            case ParsedCommand command:
                // Each command is routed to the method in the file that owns it (Brain.Commands.cs); the ones no milestone has built yet are not available.
                Answer(connection, command.Command.Name, !connection.Handshaken
                    ? CommandResultMessage.Rejected(command.Id, RejectReason.ProtocolMismatch)
                    : DispatchCommand(command.Id, command.Payload));
                break;
            case ParsedFrameAck:
                // The hosts ack preview frames themselves and never forward one; if one arrives it changes nothing.
                break;
            default:
                HandleTest(connection, ((ParsedTest)parsed).Message);
                break;
        }

        PublishChanges();
    }

    private void HandleHello(Connection connection, int uiVersion)
    {
        if (uiVersion != ProtocolVersion.Current)
        {
            connection.Handshaken = false;
            BrainLog.ProtocolMismatch(_logger, connection.Role, ProtocolVersion.Current, uiVersion);
            connection.Send(new ProtocolMismatchMessage(ProtocolVersion.Current, uiVersion));
            return;
        }

        connection.Handshaken = true;
        connection.Send(Channel(connection.Role).Welcome());
        if (connection.Role == Role.Guest)
        {
            OnGuestScreenReady();
        }
    }

    private void Answer(Connection connection, string name, CommandResultMessage result)
    {
        _lastCommandResults.Enqueue(new CommandResultDiagnostics(result.Id, name, connection.Role, result.Status, result.Reason));
        if (_lastCommandResults.Count > DiagnosticsResult.MaxCommandResults)
        {
            _lastCommandResults.Dequeue();
        }

        connection.Send(result);
    }

    /// <summary>Renders both presentations; each one that changed gets the next revision and goes to every handshaken connection of its role.</summary>
    private void PublishChanges()
    {
        if (_operator.TryUpdate(RenderOperator()))
        {
            Broadcast(_operator);
        }

        if (_guest.TryUpdate(RenderGuest()))
        {
            Broadcast(_guest);
        }
    }

    private void Broadcast(PresentationChannel channel)
    {
        var message = channel.Presentation();
        foreach (var connection in _connections)
        {
            if (connection.Role == channel.Role && connection.Handshaken)
            {
                connection.Send(message);
            }
        }
    }

    private PresentationChannel Channel(Role role) => role == Role.Operator ? _operator : _guest;

    private void OnDisplaysChanged(object? sender, EventArgs e) => Post(ReadRealDisplays);

    private void ReadRealDisplays()
    {
        _realDisplays = _dependencies.Displays.GetDisplays();
        if (_virtualDisplays is null)
        {
            ApplyDisplayChange();
            PublishChanges();
        }
    }

    /// <summary>One UI's connection. <see cref="Handshaken"/> is brain state, touched only on the executor.</summary>
    private sealed class Connection(Brain brain, Role role, Action<string> send) : IBrainConnection
    {
        private int _disposed;

        public Role Role => role;

        public bool Handshaken { get; set; }

        public void Receive(string json)
        {
            ArgumentNullException.ThrowIfNull(json);
            if (Volatile.Read(ref _disposed) == 0)
            {
                brain.Post(() => brain.HandleMessage(this, json));
            }
        }

        public void Send(ServerMessage message)
        {
            var json = ProtocolJson.Serialize(message);
            try
            {
                send(json);
            }
#pragma warning disable CA1031 // A host's send must not throw; if one does, the brain logs it and serves the other connections.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                BrainLog.SendFailed(brain._logger, exception, role);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                brain.Post(() => brain._connections.Remove(this));
            }
        }
    }

    /// <summary>One role's current presentation and revision.</summary>
    private abstract class PresentationChannel(Role role)
    {
        public Role Role => role;

        public long Revision { get; protected set; }

        /// <summary>The surface's wire name (camelCase).</summary>
        public abstract string Surface { get; }

        public abstract ServerMessage Welcome();

        public abstract ServerMessage Presentation();
    }

    private sealed class PresentationChannel<T> : PresentationChannel
        where T : class
    {
        private readonly Func<T, string> _surface;
        private readonly Func<long, T, ServerMessage> _welcome;
        private readonly Func<long, T, ServerMessage> _presentation;
        private T _current;
        private string _json;

        public PresentationChannel(Role role, T initial, Func<T, string> surface, Func<long, T, ServerMessage> welcome, Func<long, T, ServerMessage> presentation)
            : base(role)
        {
            _surface = surface;
            _welcome = welcome;
            _presentation = presentation;
            _current = initial;
            _json = ProtocolJson.Serialize(initial);
            Revision = 1;
        }

        public override string Surface => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(_surface(_current));

        public override ServerMessage Welcome() => _welcome(Revision, _current);

        public override ServerMessage Presentation() => _presentation(Revision, _current);

        /// <summary>Takes <paramref name="next"/> as the next revision when it differs from the current presentation.</summary>
        public bool TryUpdate(T next)
        {
            var json = ProtocolJson.Serialize(next);
            if (string.Equals(json, _json, StringComparison.Ordinal))
            {
                return false;
            }

            _current = next;
            _json = json;
            Revision++;
            return true;
        }
    }
}

internal static partial class BrainLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "The {Role} UI speaks protocol {UiVersion}; the brain speaks {BrainVersion}.")]
    public static partial void ProtocolMismatch(ILogger logger, Role role, int brainVersion, int uiVersion);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sending to a {Role} connection failed.")]
    public static partial void SendFailed(ILogger logger, Exception exception, Role role);

    [LoggerMessage(Level = LogLevel.Information, Message = "The Guest Screen's display {DisplayId} is gone; closing the Guest Screen.")]
    public static partial void GuestDisplayLost(ILogger logger, string displayId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Opening the Guest Screen on display {DisplayId} (single screen: {SingleScreen}).")]
    public static partial void GuestScreenOpening(ILogger logger, string displayId, bool singleScreen);

    [LoggerMessage(Level = LogLevel.Error, Message = "The host failed to schedule the exit.")]
    public static partial void ExitFailed(ILogger logger, Exception exception);
}
