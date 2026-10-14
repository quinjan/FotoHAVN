#if E2E_HOOKS
using FotoHavn.Core.Camera;
using FotoHavn.Host.Headless.E2E;
using Microsoft.Extensions.Hosting;

namespace FotoHavn.Host.Headless.Tests;

/// <summary>The headless host's <c>SaveDiagnosticsCaptureAsync</c> hook (E2E builds): it writes under the host's Data folder.</summary>
public sealed class HeadlessDiagnosticsHookTests : IDisposable
{
    private readonly string _data = Path.Combine(TestHost.NewScratchFolder(), "Data");

    [Fact]
    public async Task The_hook_saves_the_capture_in_the_diagnostics_folder_of_the_data_folder()
    {
        var hooks = new HeadlessTestHooks(new BridgeSessions(), new StoppedLifetime(), _data);
        var bgra = new byte[16 * 8 * 4];
        Array.Fill(bgra, (byte)200);

        var view = await hooks.SaveDiagnosticsCaptureAsync(new CapturedFrame(bgra, 16, 8, 64, TimeSpan.Zero), mirrored: true);

        var path = Path.Combine(_data, "diagnostics", view.FileName);
        Assert.True(File.Exists(path));
        Assert.Equal(new FileInfo(path).Length, view.ByteLength);
        Assert.Equal(16, view.Width);
        Assert.Equal(8, view.Height);
        Assert.True(view.Mirrored);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path.GetDirectoryName(_data)!, recursive: true);
        }
        catch (IOException)
        {
            // Harmless: the scratch folder lives under the test output.
        }
    }

    private sealed class StoppedLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }
}
#endif
