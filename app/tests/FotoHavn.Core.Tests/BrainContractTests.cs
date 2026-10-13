using FotoHavn.Core.Displays;
using FotoHavn.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace FotoHavn.Core.Tests;

public sealed class BrainContractTests
{
    [Fact]
    public void Dependencies_default_to_a_null_logger()
    {
        var dependencies = new BrainDependencies(new NoExecutor(), new NoClock(), new NoDisplays(), new NoHost());

        Assert.Same(NullLogger.Instance, dependencies.Logger);
    }

    [Fact]
    public void Brain_requires_its_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new Brain(null!));
    }

    // Wave B replaces this with the brain's behavior tests.
    [Fact]
    public void Brain_connections_arrive_in_wave_B()
    {
        using var brain = new Brain(new BrainDependencies(new NoExecutor(), new NoClock(), new NoDisplays(), new NoHost()));

        Assert.Throws<NotImplementedException>(() => brain.Connect(Role.Operator, _ => { }));
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

    private sealed class NoHost : IHostServices
    {
        public void OpenGuestScreen(DisplayInfo display) => throw new NotSupportedException();

        public void CloseGuestScreen() => throw new NotSupportedException();

        public void OpenFolder(string path) => throw new NotSupportedException();
    }
}
