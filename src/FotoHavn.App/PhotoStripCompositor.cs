using FotoHavn.Core;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.UI;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace FotoHavn.App;

internal sealed class PhotoStripCompositor : IPhotoStripCompositor
{
    private const int StripWidth = 600;
    private const int StripHeight = 1800;
    private const float CaptureLeft = 30;
    private const float CaptureWidth = 540;
    private const float CaptureHeight = 360;
    private const float CaptureTop = 30;
    private const float CaptureGutter = 18;

    public async Task<PhotoStripCompositionResult> ComposeAsync(
        PhotoStripCompositionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Captures.Count != 4)
        {
            return new PhotoStripCompositionResult(false, ReadOnlyMemory<byte>.Empty, 0, 0);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var device = CanvasDevice.GetSharedDevice();
        using var target = new CanvasRenderTarget(device, StripWidth, StripHeight, 96);
        var bitmaps = new List<CanvasBitmap>(4);
        try
        {
            foreach (var capture in request.Captures)
            {
                bitmaps.Add(await CanvasBitmap.LoadAsync(device, capture.ArtifactPath));
            }

            using (var drawing = target.CreateDrawingSession())
            {
                drawing.Clear(Colors.White);
                for (var index = 0; index < bitmaps.Count; index++)
                {
                    var bitmap = bitmaps[index];
                    var source = CenterCrop(bitmap.SizeInPixels.Width, bitmap.SizeInPixels.Height, request.CaptureCropAspectRatio);
                    var destination = new Rect(
                        CaptureLeft,
                        CaptureTop + (index * (CaptureHeight + CaptureGutter)),
                        CaptureWidth,
                        CaptureHeight);
                    drawing.DrawImage(bitmap, destination, source, 1, CanvasImageInterpolation.HighQualityCubic);
                }

                using var textFormat = new CanvasTextFormat
                {
                    FontFamily = "Inter",
                    FontSize = 22,
                    FontWeight = new Windows.UI.Text.FontWeight { Weight = 600 },
                    HorizontalAlignment = CanvasHorizontalAlignment.Center,
                    VerticalAlignment = CanvasVerticalAlignment.Center,
                    WordWrapping = CanvasWordWrapping.Wrap,
                };
                drawing.DrawText(
                    request.EventName.ToUpperInvariant(),
                    new Rect(30, 1584, 540, 150),
                    Colors.Black,
                    textFormat);
            }

            cancellationToken.ThrowIfCancellationRequested();
            using var stream = new InMemoryRandomAccessStream();
            await target.SaveAsync(stream, CanvasBitmapFileFormat.Png);
            var bytes = new byte[checked((int)stream.Size)];
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)bytes.Length);
            reader.ReadBytes(bytes);
            return new PhotoStripCompositionResult(true, bytes, StripWidth, StripHeight);
        }
        catch (Exception exception) when (
            exception is FileNotFoundException or
            InvalidDataException or
            System.Runtime.InteropServices.COMException)
        {
            return new PhotoStripCompositionResult(false, ReadOnlyMemory<byte>.Empty, 0, 0);
        }
        finally
        {
            foreach (var bitmap in bitmaps)
            {
                bitmap.Dispose();
            }
        }
    }

    private static Rect CenterCrop(uint width, uint height, double targetAspectRatio)
    {
        var sourceAspectRatio = (double)width / height;
        if (sourceAspectRatio > targetAspectRatio)
        {
            var cropWidth = height * targetAspectRatio;
            return new Rect((width - cropWidth) / 2, 0, cropWidth, height);
        }

        var cropHeight = width / targetAspectRatio;
        return new Rect(0, (height - cropHeight) / 2, width, cropHeight);
    }
}
