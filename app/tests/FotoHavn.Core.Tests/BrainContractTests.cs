using FotoHavn.Core.Camera;
using FotoHavn.Core.Displays;
using FotoHavn.Core.Fakes;
using FotoHavn.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace FotoHavn.Core.Tests;

public sealed class BrainContractTests
{
    [Fact]
    public void Dependencies_default_to_a_null_logger()
    {
        var dependencies = new BrainDependencies(new NoExecutor(), new NoClock(), new NoDisplays(), new NoCamera(), new NoHost(), new InMemoryEventStore());

        Assert.Same(NullLogger.Instance, dependencies.Logger);
        Assert.Null(dependencies.PreviewStats);
    }

    [Fact]
    public void Brain_requires_its_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new Brain(null!));
    }

    [Fact]
    public void Brain_posts_its_first_display_read_and_connections_to_the_executor()
    {
        var executor = new QueueExecutor();
        using var brain = new Brain(new BrainDependencies(executor, new NoClock(), new NoDisplays(), new NoCamera(), new NoHost(), new InMemoryEventStore()));

        var connection = brain.Connect(Role.Guest, _ => { });

        Assert.Equal(Role.Guest, connection.Role);
        Assert.Equal(3, executor.Posted);
    }

    [Fact]
    public void Displays_compare_by_value()
    {
        var bounds = new DisplayRect(0, 0, 1920, 1080);
        var display = new DisplayInfo(@"\\?\DISPLAY#DEL41A8#1", "DELL P2419H", bounds, bounds with { Height = 1040 }, 1.25, true);

        Assert.Equal(display, display with { });
        Assert.NotEqual(display, display with { Id = @"\\?\DISPLAY#DEL41A8#2" });
        Assert.Equal(1040, display.WorkArea.Height);
    }

    private sealed class NoExecutor : IBrainExecutor
    {
        public bool IsCurrentThread => false;

        public void Post(Action work) => throw new NotSupportedException();
    }

    private sealed class QueueExecutor : IBrainExecutor
    {
        public int Posted { get; private set; }

        public bool IsCurrentThread => false;

        public void Post(Action work) => Posted++;
    }

    private sealed class NoClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;

        public IDisposable Schedule(TimeSpan delay, Action callback) => throw new NotSupportedException();
    }

    private sealed class NoDisplays : IDisplayAdapter
    {
        public event EventHandler? DisplaysChanged
        {
            add { }
            remove { }
        }

        public IReadOnlyList<DisplayInfo> GetDisplays() => [];
    }

    private sealed class NoCamera : ICameraAdapter
    {
        public event EventHandler? CamerasChanged
        {
            add { }
            remove { }
        }

        public Task<IReadOnlyList<AvailableCamera>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ICameraSession> OpenAsync(string deviceId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoHost : IHostServices
    {
        public void OpenGuestScreen(DisplayInfo display) => throw new NotSupportedException();

        public void CloseGuestScreen() => throw new NotSupportedException();

        public void OpenFolder(string path) => throw new NotSupportedException();

        public string? GetOperatorDisplayId() => throw new NotSupportedException();
    }
}
