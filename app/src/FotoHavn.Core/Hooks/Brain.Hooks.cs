using FotoHavn.Core.Displays;
using FotoHavn.Core.Hooks;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core;

// The brain side of the E2E test hooks (plan section 14, contract section 8). This dispatcher compiles into every build so
// the normal test run covers it; only the public constructor of an E2E_HOOKS build switches it on. Field builds answer
// every `test` message with hooksUnavailable and never reach the hooks.
public sealed partial class Brain
{
    private readonly bool _hooksEnabled;
    private readonly IHostHookPort? _hostHooks;
    private readonly IStorageHookPort? _storageHooks;
    private readonly Queue<CommandResultDiagnostics> _lastCommandResults = new();
    private VirtualDisplaySet? _virtualDisplays;

    private void HandleTest(Connection connection, TestMessage message)
    {
        if (!_hooksEnabled)
        {
            Reply(connection, message.Id, HookOutcome.Fail(TestHookError.HooksUnavailable));
            return;
        }

        if (!TestHookRegistry.TryGet(message.Name, out var hook))
        {
            Reply(connection, message.Id, HookOutcome.Fail(TestHookError.UnknownHook));
            return;
        }

        if (!TestHookRegistry.TryReadArgs(hook, message.Args, out var args))
        {
            Reply(connection, message.Id, HookOutcome.Fail(TestHookError.InvalidArgs));
            return;
        }

        RunHook(connection, message.Id, hook.Name, args);
    }

    private void RunHook(Connection connection, string id, string name, object? args)
    {
        switch (name)
        {
            case TestHookNames.GetDiagnostics:
                GetDiagnostics(connection, id);
                break;
            case TestHookNames.UseVirtualDisplays:
                Reply(connection, id, UseVirtualDisplays(((UseVirtualDisplaysArgs)args!).Displays));
                break;
            case TestHookNames.DisconnectDisplay:
                Reply(connection, id, ChangeVirtualDisplay(set => set.Disconnect(((DisconnectDisplayArgs)args!).DisplayId)));
                break;
            case TestHookNames.ConnectDisplay:
                Reply(connection, id, ChangeVirtualDisplay(set => set.Connect(((ConnectDisplayArgs)args!).DisplayId)));
                break;
            case TestHookNames.OpenGuestScreen:
                Reply(connection, id, OpenGuestScreen(((OpenGuestScreenArgs)args!).DisplayId));
                break;
            case TestHookNames.CloseGuestScreen:
                Reply(connection, id, CloseGuestScreen());
                break;
            case TestHookNames.SetWindowBounds:
                var bounds = (SetWindowBoundsArgs)args!;
                RunHostHook(connection, id, hooks => hooks.SetWindowBoundsAsync(bounds.Role, bounds.Width, bounds.Height), window => window);
                break;
            case TestHookNames.CrashUi:
                Reply(connection, id, CrashUi(((CrashUiArgs)args!).Role));
                break;
            case TestHookNames.ListCameras:
                RunHostHook(connection, id, hooks => hooks.ListCamerasAsync(), cameras => new ListCamerasResult(cameras));
                break;
            case TestHookNames.Exit:
                Exit(connection, id);
                break;
            case TestHookNames.InjectStorageFault:
                var fault = (InjectStorageFaultArgs)args!;
                Reply(connection, id, RunStorageHook(hooks => hooks.InjectFault(fault.Kind, fault.Scope)));
                break;
            case TestHookNames.ClearStorageFault:
                Reply(connection, id, RunStorageHook(hooks => hooks.ClearFault()));
                break;
            default:
                throw new InvalidOperationException($"The test hook {name} has no brain handler.");
        }
    }

    private void GetDiagnostics(Connection connection, string id)
    {
        if (_hostHooks is null)
        {
            Reply(connection, id, HookOutcome.Ok(Diagnostics([])));
            return;
        }

        RunHostHook(connection, id, hooks => hooks.GetWindowsAsync(), Diagnostics);
    }

    private DiagnosticsResult Diagnostics(IReadOnlyList<TestWindow> windows) => new(
        ProtocolVersion.Current,
        [.. new[] { Role.Operator, Role.Guest }.Where(role => _connections.Any(connection => connection.Role == role && connection.Handshaken))],
        [
            new PresentationDiagnostics(Role.Operator, _operator.Revision, _operator.Surface),
            new PresentationDiagnostics(Role.Guest, _guest.Revision, _guest.Surface),
        ],
        [.. _lastCommandResults],
        windows,
        new DisplayDiagnostics(_virtualDisplays is not null, [.. CurrentDisplays.Select(VirtualDisplaySet.ToTestDisplay)]),
        _guestScreenDisplay is not null,
        StorageDiagnosticsNow());

    private HookOutcome RunStorageHook(Action<IStorageHookPort> run)
    {
        if (_storageHooks is not { } hooks)
        {
            return HookOutcome.Fail(TestHookError.NotSupported);
        }

        try
        {
            run(hooks);
            return HookOutcome.Ok(EmptyHookResult.Instance);
        }
#pragma warning disable CA1031 // Any host failure becomes the hook's error (contract section 8).
        catch (Exception)
#pragma warning restore CA1031
        {
            return HookOutcome.Fail(TestHookError.HookFailed);
        }
    }

    private HookOutcome UseVirtualDisplays(IReadOnlyList<VirtualDisplaySpec> specs)
    {
        if (VirtualDisplaySet.TryCreate(_dependencies.Displays.GetDisplays(), specs, out var set) is { } error)
        {
            return HookOutcome.Fail(error);
        }

        _virtualDisplays = set;
        ApplyDisplayChange();
        return HookOutcome.Ok(new UseVirtualDisplaysResult([.. set.Current.Select(VirtualDisplaySet.ToTestDisplay)]));
    }

    private HookOutcome ChangeVirtualDisplay(Func<VirtualDisplaySet, bool> change)
    {
        if (_virtualDisplays is null || !change(_virtualDisplays))
        {
            return HookOutcome.Fail(TestHookError.UnknownDisplay);
        }

        ApplyDisplayChange();
        return HookOutcome.Ok(EmptyHookResult.Instance);
    }

    private HookOutcome OpenGuestScreen(string displayId)
    {
        var displays = CurrentDisplays;
        if (DisplayMatching.Find(displays, displayId) is not { } display)
        {
            return HookOutcome.Fail(TestHookError.UnknownDisplay);
        }

        var singleScreen = DisplayRules.IsSingleScreen(displays, display.Id, OperatorDisplayId());
        BrainLog.GuestScreenOpening(_logger, display.Id, singleScreen);
        _dependencies.Host.OpenGuestScreen(display);
        _guestScreenDisplay = display;
        return HookOutcome.Ok(EmptyHookResult.Instance);
    }

    private HookOutcome CloseGuestScreen()
    {
        _dependencies.Host.CloseGuestScreen();
        _guestScreenDisplay = null;
        return HookOutcome.Ok(EmptyHookResult.Instance);
    }

    private HookOutcome CrashUi(Role role)
    {
        if (_hostHooks is null)
        {
            return HookOutcome.Fail(TestHookError.NotSupported);
        }

        try
        {
            return _hostHooks.ScheduleCrashUi(role)
                ? HookOutcome.Ok(EmptyHookResult.Instance)
                : HookOutcome.Fail(TestHookError.HookFailed);
        }
#pragma warning disable CA1031 // Any host failure becomes the hook's error (contract section 8).
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return HookOutcome.Fail(_hostHooks.Classify(exception));
        }
    }

    private void Exit(Connection connection, string id)
    {
        if (_hostHooks is null)
        {
            Reply(connection, id, HookOutcome.Fail(TestHookError.NotSupported));
            return;
        }

        Reply(connection, id, HookOutcome.Ok(EmptyHookResult.Instance));
        try
        {
            _hostHooks.ScheduleExit();
        }
#pragma warning disable CA1031 // The reply has gone; the brain can only log the host's failure.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            BrainLog.ExitFailed(_logger, exception);
        }
    }

    /// <summary>
    /// Starts a host hook and answers when its task completes, back on the executor. The brain never waits for the host:
    /// the continuation (run on whichever thread completes the task) only posts the reply as a new work item.
    /// </summary>
    private void RunHostHook<T>(Connection connection, string id, Func<IHostHookPort, Task<T>> start, Func<T, object> result)
    {
        if (_hostHooks is not { } hooks)
        {
            Reply(connection, id, HookOutcome.Fail(TestHookError.NotSupported));
            return;
        }

        Task<T> task;
        try
        {
            task = start(hooks);
        }
#pragma warning disable CA1031 // Any host failure becomes the hook's error (contract section 8).
        catch (Exception exception)
#pragma warning restore CA1031
        {
            Reply(connection, id, HookOutcome.Fail(hooks.Classify(exception)));
            return;
        }

        task.ContinueWith(
            completed => Post(() => Reply(connection, id, completed.IsCompletedSuccessfully
                ? HookOutcome.Ok(result(completed.Result))
                : HookOutcome.Fail(completed.Exception?.InnerException is { } exception ? hooks.Classify(exception) : TestHookError.HookFailed))),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void Reply(Connection connection, string id, HookOutcome outcome) =>
        connection.Send(outcome.Error is { } error ? TestResultMessage.Failure(id, error) : TestResultMessage.Success(id, outcome.Result));

    private readonly record struct HookOutcome(object? Result, TestHookError? Error)
    {
        public static HookOutcome Ok(object result) => new(result, null);

        public static HookOutcome Fail(TestHookError error) => new(null, error);
    }
}
