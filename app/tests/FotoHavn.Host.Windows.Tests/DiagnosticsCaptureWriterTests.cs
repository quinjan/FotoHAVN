using FotoHavn.Camera.Windows;
using FotoHavn.Core.Camera;
using FotoHavn.Host.Diagnostics;

namespace FotoHavn.Host.Windows.Tests;

/// <summary>The WPF host links the same diagnostics Capture writer as the headless host (its full tests live there).</summary>
public sealed class DiagnosticsCaptureWriterTests : IDisposable
{
    private readonly string _data = Path.Combine(AppContext.BaseDirectory, "scratch", Guid.NewGuid().ToString("N"), "Data");

    [Fact]
    public async Task Saves_a_jpeg_under_the_data_folders_diagnostics_folder()
    {
        var writer = new DiagnosticsCaptureWriter(_data, TimeProvider.System);
        var bgra = new byte[16 * 8 * 4];
        Array.Fill(bgra, (byte)100);

        var view = await writer.SaveAsync(new CapturedFrame(bgra, 16, 8, 64, TimeSpan.Zero), mirrored: false);

        var path = Path.Combine(_data, "diagnostics", view.FileName);
        Assert.Equal(new FileInfo(path).Length, view.ByteLength);
        Assert.Equal([0xFF, 0xD8], (await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken))[..2]);
    }

    [Fact]
    public async Task Encodes_byte_for_byte_like_the_cameras_jpeg_encoder()
    {
        var writer = new DiagnosticsCaptureWriter(_data, TimeProvider.System);
        var bgra = new byte[64 * 48 * 4];
        for (var i = 0; i < bgra.Length; i++)
        {
            bgra[i] = (byte)((i * 31) + (i / 256));
        }

        var frame = new CapturedFrame(bgra, 64, 48, 256, TimeSpan.Zero);

        var view = await writer.SaveAsync(frame, mirrored: false);

        var written = await File.ReadAllBytesAsync(Path.Combine(_data, "diagnostics", view.FileName), TestContext.Current.CancellationToken);
        Assert.Equal(JpegEncoder.Encode(frame), written);
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
}
