using System.Windows;
using FotoHavn.Core;
using FotoHavn.Core.Hardening;
using FotoHavn.Host.Windows.Hardening;

namespace FotoHavn.Host.Windows.Tests;

/// <summary>The windows' close handling (M10): the Operator window asks the brain, the Guest window is refused while an Event runs, Windows ending the session waits a bounded time.</summary>
public sealed class WindowCloseControllerTests
{
    private sealed class FakeCloseRequests : ICloseRequests
    {
        public bool IsEventActive { get; set; }

        public List<CloseSource> Requests { get; } = [];

        public TaskCompletionSource<CloseRequestOutcome> Answer { get; } = new();

        public Task<CloseRequestOutcome> RequestClose(CloseSource source)
        {
            Requests.Add(source);
            return Answer.Task;
        }
    }

    private static (WindowCloseController Controller, FakeCloseRequests Requests, List<CloseRequestOutcome> Outcomes) Create(bool eventActive = false)
    {
        var requests = new FakeCloseRequests { IsEventActive = eventActive };
        var outcomes = new List<CloseRequestOutcome>();
        var controller = new WindowCloseController(requests, work => work()) { OperatorOutcome = outcomes.Add };
        return (controller, requests, outcomes);
    }

    [Fact]
    public void The_Operator_window_cancels_its_close_and_asks_the_brain()
    {
        var (controller, requests, outcomes) = Create(eventActive: true);

        var cancel = controller.ShouldCancelOperatorClose();
        requests.Answer.SetResult(CloseRequestOutcome.AskToEndEvent);

        Assert.True(cancel);
        Assert.Equal([CloseSource.OperatorWindow], requests.Requests);
        Assert.Equal([CloseRequestOutcome.AskToEndEvent], outcomes);
    }

    [Fact]
    public void The_brains_answer_reaches_the_UI_thread_through_the_post()
    {
        var requests = new FakeCloseRequests();
        var posted = new List<Action>();
        var outcomes = new List<CloseRequestOutcome>();
        var controller = new WindowCloseController(requests, posted.Add) { OperatorOutcome = outcomes.Add };

        controller.ShouldCancelOperatorClose();
        requests.Answer.SetResult(CloseRequestOutcome.CloseNow);

        Assert.Empty(outcomes);
        posted.Single()();
        Assert.Equal([CloseRequestOutcome.CloseNow], outcomes);
    }

    [Fact]
    public void A_failed_request_is_not_an_answer_and_does_not_throw()
    {
        var (controller, requests, outcomes) = Create();

        Assert.True(controller.ShouldCancelOperatorClose());
        requests.Answer.SetException(new InvalidOperationException("The host could not close."));

        Assert.Empty(outcomes);
    }

    [Fact]
    public void An_answer_with_no_handler_set_is_dropped()
    {
        var requests = new FakeCloseRequests();
        var controller = new WindowCloseController(requests, work => work());

        controller.ShouldCancelOperatorClose();
        requests.Answer.SetResult(CloseRequestOutcome.AskToEndEvent);

        Assert.Single(requests.Requests);
    }

    [Fact]
    public void Once_the_app_is_closing_no_window_is_cancelled_and_the_brain_is_not_asked()
    {
        var (controller, requests, _) = Create(eventActive: true);

        controller.AllowClose();

        Assert.True(controller.IsClosing);
        Assert.False(controller.ShouldCancelOperatorClose());
        Assert.False(controller.ShouldCancelGuestClose());
        Assert.Empty(requests.Requests);
    }

    [Fact]
    public void The_Guest_window_is_refused_while_an_Event_runs_and_may_close_without_one()
    {
        var (controller, requests, _) = Create(eventActive: true);

        Assert.True(controller.ShouldCancelGuestClose());

        requests.IsEventActive = false;
        Assert.False(controller.ShouldCancelGuestClose());
        Assert.Empty(requests.Requests);
    }

    [Fact]
    public void Windows_ending_the_session_waits_for_the_brain_and_lets_every_window_go()
    {
        var (controller, requests, _) = Create(eventActive: true);
        requests.Answer.SetResult(CloseRequestOutcome.EndEventAndClose);

        var finished = controller.OnSessionEnding();

        Assert.True(finished);
        Assert.Equal([CloseSource.SessionEnd], requests.Requests);
        Assert.True(controller.IsClosing);
        Assert.False(controller.ShouldCancelOperatorClose());
    }

    [Fact]
    public void Windows_ending_the_session_does_not_wait_longer_than_its_limit()
    {
        var (controller, _, _) = Create(eventActive: true);
        var started = DateTime.UtcNow;

        var finished = controller.OnSessionEnding(TimeSpan.FromMilliseconds(100));

        Assert.False(finished);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(4));
        Assert.True(controller.IsClosing);
        Assert.Equal(TimeSpan.FromSeconds(5), WindowCloseController.SessionEndWait);
    }

    [Fact]
    public void Windows_ending_the_session_survives_a_brain_that_fails()
    {
        var (controller, requests, _) = Create(eventActive: true);
        requests.Answer.SetException(new InvalidOperationException("The host could not close."));

        Assert.False(controller.OnSessionEnding());
        Assert.True(controller.IsClosing);
    }

    [Fact]
    public void Shutdown_lets_every_window_go_and_ends_the_app_with_the_success_code_on_the_UI_thread()
    {
        var (controller, _, _) = Create(eventActive: true);
        var posted = new List<Action>();
        var exits = new List<int>();
        var lifetime = new WindowsAppLifetime(controller, posted.Add, exits.Add);

        lifetime.Shutdown();

        Assert.True(controller.IsClosing);
        Assert.Empty(exits);
        posted.Single()();
        Assert.Equal([HostExitCodes.Success], exits);
    }

    [Fact]
    public async Task Without_a_brain_a_close_request_just_closes()
    {
        var requests = new BrainCloseRequests();

        Assert.False(requests.IsEventActive);
        Assert.Equal(CloseRequestOutcome.CloseNow, await requests.RequestClose(CloseSource.OperatorWindow));
    }

    // Application.Shutdown (the test.exit hook, the smoke run, a failed WebView2) closes every window and ignores a Closing that cancels, so no
    // confirmation can hold the app open. WPF allows one Application per process, so this is the only test that makes one.
    [Fact]
    public void Shutting_the_application_down_closes_the_windows_even_with_an_Event_running_and_a_veto()
    {
        var (controller, requests, _) = Create(eventActive: true);
        var guestStayedOpen = false;
        var closed = new List<string>();
        var exitCode = -1;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var guest = new Window { Width = 120, Height = 80, ShowInTaskbar = false, WindowStyle = WindowStyle.None, ShowActivated = false };
                var operatorWindow = new Window { Width = 120, Height = 80, ShowInTaskbar = false, ShowActivated = false };
                guest.Closing += (_, e) => e.Cancel = controller.ShouldCancelGuestClose();
                operatorWindow.Closing += (_, e) => e.Cancel = controller.ShouldCancelOperatorClose();
                guest.Closed += (_, _) => closed.Add("guest");
                operatorWindow.Closed += (_, _) => closed.Add("operator");
                guest.Show();
                operatorWindow.Show();
                application.Dispatcher.InvokeAsync(() =>
                {
                    guest.Close();
                    guestStayedOpen = guest.IsVisible;
                    application.Shutdown(0);
                });
                exitCode = application.Run();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "The application did not shut down.");

        Assert.Null(failure);
        Assert.Equal(0, exitCode);
        Assert.True(guestStayedOpen, "A plain close of the Guest window should be refused while an Event runs.");
        Assert.Contains("guest", closed);
        Assert.Contains("operator", closed);
        Assert.Contains(CloseSource.OperatorWindow, requests.Requests);
    }
}
