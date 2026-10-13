using System.Text.Json;

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
        new(TestHookNames.ListCameras, TestHookSide.Host, null, typeof(ListCamerasResult)),
        new(TestHookNames.Exit, TestHookSide.Host, null, typeof(EmptyHookResult)),
        new(TestHookNames.InjectStorageFault, TestHookSide.Brain, typeof(InjectStorageFaultArgs), typeof(EmptyHookResult)),
        new(TestHookNames.ClearStorageFault, TestHookSide.Brain, null, typeof(EmptyHookResult)),
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

/// <summary>The result of <c>test.getDiagnostics</c>.</summary>
/// <param name="ProtocolVersion">The brain's protocol version.</param>
/// <param name="ConnectedRoles">Roles with at least one connection that completed the handshake, operator first.</param>
/// <param name="Presentations">The current revision and surface of each role, operator first.</param>
/// <param name="LastCommandResults">The last <see cref="MaxCommandResults"/> command results, oldest first.</param>
/// <param name="Windows">The host's open windows (empty in the headless host).</param>
/// <param name="Displays">The display list the brain uses.</param>
/// <param name="GuestScreenOpen">Whether the brain has asked the host to show the Guest Screen.</param>
/// <param name="Storage">The Event store's state.</param>
public sealed record DiagnosticsResult(
    int ProtocolVersion,
    IReadOnlyList<Role> ConnectedRoles,
    IReadOnlyList<PresentationDiagnostics> Presentations,
    IReadOnlyList<CommandResultDiagnostics> LastCommandResults,
    IReadOnlyList<TestWindow> Windows,
    DisplayDiagnostics Displays,
    bool GuestScreenOpen,
    StorageDiagnostics Storage)
{
    public const int MaxCommandResults = 20;
}
