using FotoHavn.Core.Hardening;
using FotoHavn.Core.Tests.Events.Active;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.Hardening;

/// <summary>The wave A seams of runtime hardening: the close request, the close confirmation commands, and the diagnostics (M10 wave B1 fills the bodies).</summary>
public sealed class HardeningScaffoldTests
{
    [Fact]
    public async Task The_scaffold_close_request_shuts_the_app_down_at_once()
    {
        var lifetime = new NoOpAppLifetime();
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { AppLifetime = lifetime });

        var request = rig.Brain.RequestClose(CloseSource.OperatorWindow);
        Assert.False(request.IsCompleted);
        rig.Run();

        Assert.Equal(CloseRequestOutcome.CloseNow, await request);
        Assert.Equal(1, lifetime.ShutdownRequests);
    }

    [Fact]
    public async Task A_close_request_after_the_brain_is_disposed_completes_without_shutting_down()
    {
        var lifetime = new NoOpAppLifetime();
        var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { AppLifetime = lifetime });
        rig.Dispose();

        Assert.Equal(CloseRequestOutcome.CloseNow, await rig.Brain.RequestClose(CloseSource.SessionEnd));
        Assert.Equal(0, lifetime.ShutdownRequests);
    }

    [Fact]
    public async Task A_close_source_that_is_not_one_is_refused()
    {
        using var rig = await ActiveRig.CreateAsync();

        var thrown = Record.Exception((Action)(() => _ = rig.Brain.RequestClose((CloseSource)99)));

        Assert.IsType<ArgumentOutOfRangeException>(thrown);
    }

    [Fact]
    public async Task The_close_confirmation_commands_are_not_available_until_wave_B1_fills_them()
    {
        using var rig = await ActiveRig.CreateAsync();

        Assert.Equal(RejectReason.NotAvailable, rig.OperatorCommand("ConfirmCloseApp").Reason);
        Assert.Equal(RejectReason.NotAvailable, rig.OperatorCommand("CancelCloseApp").Reason);
    }

    [Fact]
    public async Task The_close_confirmation_commands_are_operator_only()
    {
        using var rig = await ActiveRig.CreateAsync();

        Assert.Equal(RejectReason.WrongRole, rig.GuestCommand("ConfirmCloseApp").Reason);
        Assert.Equal(RejectReason.WrongRole, rig.GuestCommand("CancelCloseApp").Reason);
    }

    [Fact]
    public async Task Ending_the_Event_closes_a_close_confirmation_that_was_open()
    {
        using var rig = await ActiveRig.CreateAsync();
        rig.StartAndExpectSuccess();
        rig.Brain.SetConfirmation(new ConfirmationOverlay(ConfirmationKind.CloseApp, null, null, null));
        rig.OperatorCommand("KeepRunning");
        Assert.Equal(ConfirmationKind.CloseApp, rig.LatestOperator().Overlays.Confirmation?.Kind);

        rig.OperatorCommand("EndEvent");
        rig.OperatorCommand("ConfirmEndEvent");

        Assert.Null(rig.LatestOperator().Overlays.Confirmation);
    }

    [Fact]
    public async Task The_diagnostics_report_no_instance_name_when_the_host_has_none()
    {
        using var rig = await ActiveRig.CreateAsync();

        Assert.Null(rig.Diagnostics().Instance.MutexName);
    }

    [Fact]
    public async Task The_diagnostics_report_the_hosts_instance_mutex_name()
    {
        var names = SingleInstancePolicy.NamesFor(@"C:\FOTOHAVN\Data");
        using var rig = await ActiveRig.CreateAsync(new ActiveRigOptions { SingleInstance = names });

        Assert.Equal(names.MutexName, rig.Diagnostics().Instance.MutexName);
    }
}
