using System.Text.Json;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Protocol.Testing;

// Test hook contracts (plan section 14, E2E build and test hooks). These are plain records compiled into every build so
// the schema and the UI types are the same in field and E2E builds; the behavior behind them is compiled only with
// E2E_HOOKS. Without E2E_HOOKS the brain answers every `test` message with TestHookError.HooksUnavailable.

/// <summary>Where a hook runs. Brain hooks run on the brain's executor; host hooks are forwarded to the host.</summary>
public enum TestHookSide
{
    Brain,
    Host,
}

/// <summary>The wire names of every test hook.</summary>
public static class TestHookNames
{
    public const string Prefix = "test.";

    public const string GetDiagnostics = "test.getDiagnostics";
    public const string UseVirtualDisplays = "test.useVirtualDisplays";
    public const string DisconnectDisplay = "test.disconnectDisplay";
    public const string ConnectDisplay = "test.connectDisplay";

    /// <summary>Development-only stand-in for Start Event until M6: opens the placeholder Guest window.</summary>
    public const string OpenGuestScreen = "test.openGuestScreen";

    /// <summary>Development-only stand-in for ending an Event until M6: closes the placeholder Guest window.</summary>
    public const string CloseGuestScreen = "test.closeGuestScreen";

    public const string SetWindowBounds = "test.setWindowBounds";
    public const string CrashUi = "test.crashUi";
    public const string ListCameras = "test.listCameras";
    public const string Exit = "test.exit";

    /// <summary>Makes the Event store's file writes fail the way the disk would (E2E only).</summary>
    public const string InjectStorageFault = "test.injectStorageFault";

    /// <summary>Removes any fault <see cref="InjectStorageFault"/> set.</summary>
    public const string ClearStorageFault = "test.clearStorageFault";

    /// <summary>Development-only stand-in for Event setup until M6: shows the camera diagnostics surface on the Operator Screen.</summary>
    public const string OpenCameraDiagnostics = "test.openCameraDiagnostics";

    /// <summary>Selects a camera on the diagnostics surface: the brain opens it, checks for a fresh frame, and previews it to the Operator Screen.</summary>
    public const string SelectDiagnosticsCamera = "test.selectDiagnosticsCamera";

    /// <summary>Sets the diagnostics surface's mirror setting, for the preview and for diagnostic captures.</summary>
    public const string SetDiagnosticsMirror = "test.setDiagnosticsMirror";

    /// <summary>Takes a Capture from the selected camera and has the host write it as a JPEG under its <c>diagnostics/</c> folder.</summary>
    public const string CaptureDiagnostics = "test.captureDiagnostics";

    /// <summary>Makes the selected camera fail on top of the real or fake camera (<c>stale</c>, <c>failed</c>, <c>removed</c>, <c>exclusiveLost</c>).</summary>
    public const string InjectCameraFault = "test.injectCameraFault";

    /// <summary>Removes the fault <see cref="InjectCameraFault"/> set; the brain then reopens the camera.</summary>
    public const string ClearCameraFault = "test.clearCameraFault";

    /// <summary>Composes a Template and Look from the last diagnostics captures into the preview cache (E2E only).</summary>
    public const string ComposeDiagnostics = "test.composeDiagnostics";

    /// <summary>Composes a Template's blank preview (all windows empty, Naturale) into the preview cache (E2E only).</summary>
    public const string ComposeBlankPreview = "test.composeBlankPreview";

    /// <summary>Speeds up the brain's timers (countdown, Phase Timers, Keepsake, PIN lockout) by a factor for long scenarios; camera frames stay real-time (E2E only).</summary>
    public const string SetClockScale = "test.setClockScale";
}

/// <summary>One hook: its name, where it runs, its arguments record (null when it takes none), and its result record.</summary>
public sealed record TestHookDescriptor(string Name, TestHookSide Side, Type? ArgsType, Type ResultType);

/// <summary>Every test hook. The brain dispatches by name; the schema export generates the UI's hook types from it.</summary>
public static class TestHookRegistry
{
    public static IReadOnlyList<TestHookDescriptor> All { get; } =
    [
        new(TestHookNames.GetDiagnostics, TestHookSide.Brain, null, typeof(DiagnosticsResult)),
        new(TestHookNames.UseVirtualDisplays, TestHookSide.Brain, typeof(UseVirtualDisplaysArgs), typeof(UseVirtualDisplaysResult)),
        new(TestHookNames.DisconnectDisplay, TestHookSide.Brain, typeof(DisconnectDisplayArgs), typeof(EmptyHookResult)),
        new(TestHookNames.ConnectDisplay, TestHookSide.Brain, typeof(ConnectDisplayArgs), typeof(EmptyHookResult)),
        new(TestHookNames.OpenGuestScreen, TestHookSide.Brain, typeof(OpenGuestScreenArgs), typeof(EmptyHookResult)),
        new(TestHookNames.CloseGuestScreen, TestHookSide.Brain, null, typeof(EmptyHookResult)),
        new(TestHookNames.SetWindowBounds, TestHookSide.Host, typeof(SetWindowBoundsArgs), typeof(TestWindow)),
        new(TestHookNames.CrashUi, TestHookSide.Host, typeof(CrashUiArgs), typeof(EmptyHookResult)),
        new(TestHookNames.ListCameras, TestHookSide.Brain, null, typeof(ListCamerasResult)),
        new(TestHookNames.Exit, TestHookSide.Host, null, typeof(EmptyHookResult)),
        new(TestHookNames.InjectStorageFault, TestHookSide.Brain, typeof(InjectStorageFaultArgs), typeof(EmptyHookResult)),
        new(TestHookNames.ClearStorageFault, TestHookSide.Brain, null, typeof(EmptyHookResult)),
        new(TestHookNames.OpenCameraDiagnostics, TestHookSide.Brain, null, typeof(EmptyHookResult)),
        new(TestHookNames.SelectDiagnosticsCamera, TestHookSide.Brain, typeof(SelectDiagnosticsCameraArgs), typeof(EmptyHookResult)),
        new(TestHookNames.SetDiagnosticsMirror, TestHookSide.Brain, typeof(SetDiagnosticsMirrorArgs), typeof(EmptyHookResult)),
        new(TestHookNames.CaptureDiagnostics, TestHookSide.Brain, null, typeof(CameraCaptureView)),
        new(TestHookNames.InjectCameraFault, TestHookSide.Brain, typeof(InjectCameraFaultArgs), typeof(EmptyHookResult)),
        new(TestHookNames.ClearCameraFault, TestHookSide.Brain, null, typeof(EmptyHookResult)),
        new(TestHookNames.ComposeDiagnostics, TestHookSide.Brain, typeof(ComposeDiagnosticsArgs), typeof(ComposePreviewResult)),
        new(TestHookNames.ComposeBlankPreview, TestHookSide.Brain, typeof(ComposeBlankPreviewArgs), typeof(ComposePreviewResult)),
        new(TestHookNames.SetClockScale, TestHookSide.Brain, typeof(SetClockScaleArgs), typeof(EmptyHookResult)),
    ];

    public static bool TryGet(string name, out TestHookDescriptor hook)
    {
        foreach (var candidate in All)
        {
            if (string.Equals(candidate.Name, name, StringComparison.Ordinal))
            {
                hook = candidate;
                return true;
            }
        }

        hook = null!;
        return false;
    }

    /// <summary>
    /// Reads a hook's arguments into its arguments record. A hook without arguments accepts none (absent or null) or
    /// <c>{}</c>, and yields null. Returns false when the arguments don't match; answer <see cref="TestHookError.InvalidArgs"/>.
    /// </summary>
    public static bool TryReadArgs(TestHookDescriptor hook, JsonElement? args, out object? value)
    {
        ArgumentNullException.ThrowIfNull(hook);
        value = null;
        if (hook.ArgsType is null)
        {
            return args is null || (args.Value.ValueKind == JsonValueKind.Object && !args.Value.EnumerateObject().Any());
        }

        if (args is not { ValueKind: JsonValueKind.Object } element)
        {
            return false;
        }

        try
        {
            value = element.Deserialize(hook.ArgsType, ProtocolJson.Options);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

/// <summary>A rectangle in physical pixels, in Windows virtual-screen coordinates.</summary>
public sealed record PixelRect(int X, int Y, int Width, int Height);

/// <summary>A display as the brain sees it (real or virtual).</summary>
/// <param name="Id">The stable identity: the monitor device path, or the ID given to a virtual display.</param>
/// <param name="Name">The friendly name.</param>
/// <param name="Bounds">The full bounds, in physical pixels.</param>
/// <param name="WorkArea">The bounds minus the taskbar; equal to <paramref name="Bounds"/> for virtual displays.</param>
/// <param name="Scale">The Windows display scale (1.0 is 96 DPI).</param>
/// <param name="IsPrimary">Whether this is the Windows primary display.</param>
public sealed record TestDisplay(string Id, string Name, PixelRect Bounds, PixelRect WorkArea, double Scale, bool IsPrimary);

/// <summary>A virtual display to create.</summary>
/// <param name="Id">Its identity, unique in the list.</param>
/// <param name="Name">Its friendly name.</param>
/// <param name="Bounds">Its rectangle in physical pixels, relative to the real primary display's top-left corner; it must fit inside the real primary display.</param>
/// <param name="IsPrimary">Exactly one virtual display is primary.</param>
/// <param name="Scale">Its scale; null uses the real primary display's scale.</param>
public sealed record VirtualDisplaySpec(string Id, string Name, PixelRect Bounds, bool IsPrimary, double? Scale = null);

/// <summary>Replaces real display enumeration with these virtual displays until the app exits.</summary>
public sealed record UseVirtualDisplaysArgs(IReadOnlyList<VirtualDisplaySpec> Displays);

/// <summary>The virtual displays as the brain now sees them, mapped onto the real primary display.</summary>
public sealed record UseVirtualDisplaysResult(IReadOnlyList<TestDisplay> Displays);

/// <summary>Removes a virtual display from the list, as if it were unplugged.</summary>
public sealed record DisconnectDisplayArgs(string DisplayId);

/// <summary>Returns a disconnected virtual display to the list, as if it were plugged back in.</summary>
public sealed record ConnectDisplayArgs(string DisplayId);

/// <summary>Opens the placeholder Guest window on a display from the current list.</summary>
public sealed record OpenGuestScreenArgs(string DisplayId);

/// <summary>Resizes one role's window (for layout checks at 1280×720, 1366×768, 1920×1080, and 2560×1440).</summary>
public sealed record SetWindowBoundsArgs(Role Role, int Width, int Height);

/// <summary>Ends one role's WebView2 process, so the host's crash recovery runs.</summary>
public sealed record CrashUiArgs(Role Role);

/// <summary>How an injected storage write fails (plan section 14).</summary>
public enum StorageFaultKind
{
    DiskFull,
    AccessDenied,
    Locked,
}

/// <summary>How long an injected storage fault lasts.</summary>
public enum StorageFaultScope
{
    /// <summary>Only the next write fails.</summary>
    NextWrite,

    /// <summary>Every write fails until <c>test.clearStorageFault</c>.</summary>
    AllWrites,
}

/// <summary>Makes the Event store's file writes fail with <paramref name="Kind"/> for <paramref name="Scope"/>.</summary>
public sealed record InjectStorageFaultArgs(StorageFaultKind Kind, StorageFaultScope Scope);

/// <summary>The result of a hook that returns nothing: <c>{}</c>.</summary>
public sealed record EmptyHookResult
{
    public static EmptyHookResult Instance { get; } = new();
}

/// <summary>An Available Camera as Windows reports it.</summary>
public sealed record TestCamera(string Id, string Name);

public sealed record ListCamerasResult(IReadOnlyList<TestCamera> Cameras);

/// <summary>Selects the camera with <paramref name="DeviceId"/> on the diagnostics surface. An ID not in the camera list fails with <c>unknownCamera</c>.</summary>
public sealed record SelectDiagnosticsCameraArgs(string DeviceId);

/// <summary>Turns mirroring of the diagnostics preview and diagnostic captures on or off.</summary>
public sealed record SetDiagnosticsMirrorArgs(bool Mirrored);

/// <summary>Makes the selected camera fail the way <paramref name="Kind"/> says.</summary>
public sealed record InjectCameraFaultArgs(CameraFaultKind Kind);

/// <summary>
/// Composes <paramref name="TemplateId"/> with <paramref name="LookId"/> from the last N diagnostics captures (N is the
/// Template's window count; fewer captures leave the remaining windows empty).
/// </summary>
public sealed record ComposeDiagnosticsArgs(string TemplateId, string LookId);

/// <summary>Composes <paramref name="TemplateId"/> with every window empty and the Naturale Look.</summary>
public sealed record ComposeBlankPreviewArgs(string TemplateId);

/// <summary>
/// Runs the brain's timers <paramref name="Factor"/> times faster from now on: 1 is real time, the range is 1 to 100 (anything
/// else is <c>invalidArgs</c>). Needs a scalable clock; a host without one answers <c>notSupported</c>.
/// </summary>
public sealed record SetClockScaleArgs(double Factor);

/// <summary>A composed PNG now in the preview cache, served at <c>https://preview.fotohavn/&lt;PreviewKey&gt;.png</c>.</summary>
/// <param name="PreviewKey">The cache key.</param>
/// <param name="Width">The PNG width in pixels.</param>
/// <param name="Height">The PNG height in pixels.</param>
/// <param name="ByteLength">The PNG's size in bytes.</param>
public sealed record ComposePreviewResult(string PreviewKey, int Width, int Height, int ByteLength);

/// <summary>One open host window.</summary>
/// <param name="Role">The role of the window's WebView2.</param>
/// <param name="Bounds">The window's outer bounds, in physical pixels.</param>
/// <param name="DisplayId">The display the window is mostly on, or null if unknown.</param>
public sealed record TestWindow(Role Role, PixelRect Bounds, string? DisplayId);

/// <summary>A role's current presentation revision and surface (an operator or guest surface name).</summary>
public sealed record PresentationDiagnostics(Role Role, long Revision, string Surface);

/// <summary>One command result as the brain sent it.</summary>
public sealed record CommandResultDiagnostics(string Id, string Name, Role Role, CommandStatus Status, RejectReason? Reason);

/// <summary>The displays the brain currently sees, and whether they are virtual.</summary>
public sealed record DisplayDiagnostics(bool Virtual, IReadOnlyList<TestDisplay> Displays);

/// <summary>Where the Event store is in its open sequence.</summary>
public enum StorageDiagnosticsState
{
    /// <summary>Opening for the first time, or retrying.</summary>
    Opening,
    Ready,

    /// <summary>The last open failed; the operator sees the storage error surface.</summary>
    Error,
}

/// <summary>What the startup reconciler did on the last successful open: counts only, in the order of plan section 10.</summary>
public sealed record ReconcileDiagnostics(
    int StagingFilesDeleted,
    int OrphanFilesDeleted,
    int ArtifactProblems,
    int CyclesMarkedUnrecoverable,
    int CyclesClosedAsAppClosed,
    int GuestCyclesDeleted,
    int EventsDeleted,
    int DiscardedFilesDeleted,
    int OrphanThumbnailsDeleted,
    int Problems);

/// <summary>The Event store as the brain sees it.</summary>
/// <param name="State">Opening, ready, or error.</param>
/// <param name="Kind">The cause when <paramref name="State"/> is <see cref="StorageDiagnosticsState.Error"/>, else null.</param>
/// <param name="SchemaVersion">The schema version after the last successful open, or null before one.</param>
/// <param name="Reconcile">The reconciler's counts from the last successful open, or null before one.</param>
public sealed record StorageDiagnostics(
    StorageDiagnosticsState State,
    Presentations.StorageErrorReason? Kind,
    int? SchemaVersion,
    ReconcileDiagnostics? Reconcile);

/// <summary>
/// The camera as the brain sees it, for <c>test.getDiagnostics</c>. The frame figures come from the camera session and the
/// preview channel's counters, not from presentations (which must not change at frame rate).
/// </summary>
/// <param name="State">The selected camera's state.</param>
/// <param name="DeviceId">The selected camera's device ID, or null when none is selected.</param>
/// <param name="Format">The open camera's format, or null when none is open.</param>
/// <param name="FrameAgeMs">How old the latest camera frame is in milliseconds, or null when no frame has arrived.</param>
/// <param name="DeliveredFps">Preview frames the page drew per second over the last two seconds, or null with no preview channel.</param>
/// <param name="DroppedFrames">Preview frames dropped because one was in flight since the app started, or null with no preview channel.</param>
/// <param name="PreviewTarget">Which page receives preview frames now.</param>
public sealed record CameraDiagnostics(
    CameraState State,
    string? DeviceId,
    CameraFormatView? Format,
    int? FrameAgeMs,
    double? DeliveredFps,
    long? DroppedFrames,
    PreviewTarget PreviewTarget);

/// <summary>One request to open a folder that the brain made of the host (plan section 8, Open folder).</summary>
/// <param name="Path">The absolute folder path (an Event's folder; it holds the Event's ID, not its name).</param>
/// <param name="At">When the brain asked, on the brain's clock.</param>
public sealed record OpenFolderRequest(string Path, DateTimeOffset At);

/// <summary>One display an <c>IdentifyDisplays</c> asked the host to number.</summary>
/// <param name="DisplayId">The display's stable identity.</param>
/// <param name="Number">The number shown on it.</param>
/// <param name="Visible">True from the request until the brain's 3-second timer asks the host to hide the overlays.</param>
public sealed record IdentifyOverlayDiagnostics(string DisplayId, int Number, bool Visible);

/// <summary>Whether the brain holds the wake lock (held while an Event runs), and why (protocol version 10).</summary>
/// <param name="Held">Whether the lock is held.</param>
/// <param name="Reason">Why it is held (names the Event by its ID), or null while it is not held.</param>
public sealed record WakeLockDiagnostics(bool Held, string? Reason);

/// <summary>The single-instance guard of the host the brain runs in, for <c>test.getDiagnostics</c> (protocol version 10).</summary>
/// <param name="MutexName">The name of the mutex derived from the Data folder (<c>Local\FOTOHAVN-&lt;16 hex&gt;</c>), or null in a host with no single-instance guard (headless).</param>
public sealed record InstanceDiagnostics(string? MutexName);

/// <summary>The Phase Timer as the brain sees it, for <c>test.getDiagnostics</c>.</summary>
/// <param name="Phase">The phase being timed, or null when no timer is active.</param>
/// <param name="RemainingMs">Milliseconds left, or null when no timer is active or the phase's timer is off.</param>
/// <param name="Paused">Whether the timer is paused (a capture or Operator Assistance is running).</param>
/// <param name="Warning">Whether the timer is in its warning window.</param>
public sealed record GuestCycleTimerDiagnostics(PhaseTimer? Phase, long? RemainingMs, bool Paused, bool Warning);

/// <summary>The running Guest Cycle as the brain sees it, for <c>test.getDiagnostics</c> (the end-to-end scenarios read it instead of scraping the UI).</summary>
/// <param name="Id">The Guest Cycle's id.</param>
/// <param name="Step">The step the guest is on (the step the cycle returns to during Operator Assistance).</param>
/// <param name="Tab">The Photographs tab, or null on any other step.</param>
/// <param name="WindowsFilled">Windows holding a Capture.</param>
/// <param name="WindowCount">The selected Template's windows.</param>
/// <param name="SequenceRunning">Whether a capture sequence or Retake is running.</param>
/// <param name="CountdownRemaining">Whole seconds left in the countdown, or null between countdowns.</param>
/// <param name="Timer">The Phase Timer.</param>
public sealed record GuestCycleDiagnostics(
    string Id,
    GuestCycleStepName Step,
    PhotographsTabName? Tab,
    int WindowsFilled,
    int WindowCount,
    bool SequenceRunning,
    int? CountdownRemaining,
    GuestCycleTimerDiagnostics Timer);

/// <summary>The operator's Event Gallery as the brain sees it, for <c>test.getDiagnostics</c> (protocol version 9).</summary>
/// <param name="EventId">The Event whose gallery is open.</param>
/// <param name="ItemCount">The Guest Cycles loaded so far.</param>
/// <param name="NextCursor">The cursor for the next page, or null when every page is loaded.</param>
/// <param name="DetailId">The Guest Cycle whose detail is open, or null.</param>
public sealed record OperatorGalleryDiagnostics(string EventId, int ItemCount, string? NextCursor, string? DetailId);

/// <summary>The guest gallery as the brain sees it, for <c>test.getDiagnostics</c> (protocol version 9).</summary>
/// <param name="StripCount">The Photo Strips loaded so far.</param>
/// <param name="NextCursor">The cursor for the next page, or null when every page is loaded.</param>
/// <param name="DetailId">The Guest Cycle whose strip detail is open, or null.</param>
/// <param name="Timer">The guest gallery's Phase Timer.</param>
public sealed record GuestGalleryDiagnostics(int StripCount, string? NextCursor, string? DetailId, GuestCycleTimerDiagnostics Timer);

/// <summary>The galleries as the brain sees them (protocol version 9): each side is null while that gallery isn't open.</summary>
/// <param name="Operator">The operator's Event Gallery, or null.</param>
/// <param name="Guest">The guest gallery, or null.</param>
public sealed record GalleryDiagnostics(OperatorGalleryDiagnostics? Operator, GuestGalleryDiagnostics? Guest);

/// <summary>The result of <c>test.getDiagnostics</c>.</summary>
/// <param name="ProtocolVersion">The brain's protocol version.</param>
/// <param name="ConnectedRoles">Roles with at least one connection that completed the handshake, operator first.</param>
/// <param name="Presentations">The current revision and surface of each role, operator first.</param>
/// <param name="LastCommandResults">The last <see cref="MaxCommandResults"/> command results, oldest first.</param>
/// <param name="Windows">The host's open windows (empty in the headless host).</param>
/// <param name="Displays">The display list the brain uses.</param>
/// <param name="GuestScreenOpen">Whether the brain has asked the host to show the Guest Screen.</param>
/// <param name="Storage">The Event store's state.</param>
/// <param name="Camera">The camera's state and frame statistics.</param>
/// <param name="OpenFolderRequests">The last <see cref="MaxOpenFolderRequests"/> folders the brain asked the host to open, oldest first.</param>
/// <param name="IdentifyOverlays">The displays of the last <c>IdentifyDisplays</c>, with whether they are showing now; empty before the first.</param>
/// <param name="WakeLock">Whether the brain holds the wake lock, and why (the reason arrived in protocol version 10).</param>
/// <param name="ClockScale">The clock's speed factor (1 unless <c>test.setClockScale</c> changed it).</param>
/// <param name="GuestCycle">The running Guest Cycle, or null when none runs (protocol version 8).</param>
/// <param name="Gallery">The open galleries, or null when neither is open (protocol version 9).</param>
/// <param name="Instance">The host's single-instance guard (protocol version 10).</param>
public sealed record DiagnosticsResult(
    int ProtocolVersion,
    IReadOnlyList<Role> ConnectedRoles,
    IReadOnlyList<PresentationDiagnostics> Presentations,
    IReadOnlyList<CommandResultDiagnostics> LastCommandResults,
    IReadOnlyList<TestWindow> Windows,
    DisplayDiagnostics Displays,
    bool GuestScreenOpen,
    StorageDiagnostics Storage,
    CameraDiagnostics Camera,
    IReadOnlyList<OpenFolderRequest> OpenFolderRequests,
    IReadOnlyList<IdentifyOverlayDiagnostics> IdentifyOverlays,
    WakeLockDiagnostics WakeLock,
    double ClockScale,
    GuestCycleDiagnostics? GuestCycle,
    GalleryDiagnostics? Gallery,
    InstanceDiagnostics Instance)
{
    public const int MaxCommandResults = 20;

    public const int MaxOpenFolderRequests = 10;
}
