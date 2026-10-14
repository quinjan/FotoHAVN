using System.Runtime.ExceptionServices;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Catalog;
using SkiaSharp;

namespace FotoHavn.Catalog;

/// <summary>
/// The SkiaSharp <see cref="ICompositor"/>: an operation-for-operation port of the website's <c>renderComposition</c> for
/// Templates (paper, artwork, cover-cropped windows, the Look confined to photograph windows, hairlines between empty windows,
/// the <c>FOTOHAVN</c> signature), producing an opaque lossless PNG at the Template's native size and orientation.
/// </summary>
/// <remarks>
/// Resampling: a photograph is drawn with Mitchell cubic resampling, except that a downscale of 2x or more uses trilinear
/// mipmaps (<see cref="SKFilterMode.Linear"/> + <see cref="SKMipmapMode.Linear"/>) so camera frames don't alias. Both are
/// plain CPU raster operations, so the output is deterministic. An encoded photograph is decoded as stored: EXIF orientation is
/// not applied (saved captures are written from raw frames and carry none).
/// Signature placement: <c>textBaseline = "middle"</c> puts the middle of the font's ascent-to-descent box on the anchor y, so the
/// baseline is <c>y - (ascent + descent) / 2</c> with Skia's ascent (negative) and descent (positive) metrics. A signature wider
/// than its max width is scaled horizontally around the anchor, as canvas <c>fillText</c> does with <c>maxWidth</c>.
/// Every call builds its own surface, bitmaps, paints, and fonts and disposes them; the typeface and artwork come from the
/// read-only <see cref="CatalogAssets"/> cache and are never disposed here, so the compositor is safe to share across threads.
/// </remarks>
public sealed class SkiaCompositor : ICompositor
{
    private const string Signature = "FOTOHAVN";
    private const double MipmapDownscaleThreshold = 2.0;

    private static readonly SKColor Paper = new(0xF3, 0xEB, 0xDD);
    private static readonly SKColor BlankWindow = new(0xE5, 0xE0, 0xD7);
    private static readonly SKColor Hairline = new(0xEE, 0xEA, 0xE3);
    private static readonly SKSamplingOptions CubicSampling = new(SKCubicResampler.Mitchell);
    private static readonly SKSamplingOptions MipmapSampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    private readonly ICatalog _catalog;

    public SkiaCompositor()
        : this(new CatalogData())
    {
    }

    public SkiaCompositor(ICatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    public Task<CompositionResult> ComposeAsync(CompositionRequest request, CancellationToken ct) =>
        Task.Run(() => Guarded(() => Compose(request, ct)), ct);

    public Task<CompositionResult> ComposeLookThumbnailAsync(CompositionPhoto photo, double aspectRatio, string lookId, int width, CancellationToken ct) =>
        Task.Run(() => Guarded(() => Thumbnail(photo, aspectRatio, lookId, width, ct)), ct);

    private static CompositionResult Guarded(Func<CompositionResult> work)
    {
        try
        {
            return work();
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or CompositionException or ArgumentException))
        {
            throw new CompositionException(CompositionErrorKind.Unknown, $"Composing failed: {ex.Message}", ex);
        }
    }

    private CompositionResult Compose(CompositionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.TemplateId);
        ArgumentNullException.ThrowIfNull(request.LookId);
        ArgumentNullException.ThrowIfNull(request.Photos);
        ct.ThrowIfCancellationRequested();

        var template = _catalog.FindTemplate(request.TemplateId)
            ?? throw new CompositionException(CompositionErrorKind.UnknownTemplate, $"No Template {request.TemplateId}.");
        if (_catalog.FindLook(request.LookId) is null || !LookKernels.IsKnown(request.LookId))
        {
            throw new CompositionException(CompositionErrorKind.UnknownLook, $"No Look {request.LookId}.");
        }

        if (request.Photos.Count != template.WindowCount)
        {
            throw new CompositionException(
                CompositionErrorKind.WrongPhotoCount,
                $"{template.Id} has {template.WindowCount} windows, not {request.Photos.Count} photographs.");
        }

        var windows = RenderWindows(template, request, ct);
        try
        {
            using var surface = SKSurface.Create(new SKImageInfo(template.Width, template.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            var canvas = surface.Canvas;
            canvas.Clear(Paper);
            if (template.HasArtwork)
            {
                canvas.DrawImage(CatalogAssets.Artwork(template.Id), new SKRect(0, 0, template.Width, template.Height));
            }

            for (var index = 0; index < template.WindowCount; index++)
            {
                var rect = template.Windows[index];
                if (windows[index] is { } window)
                {
                    using var copy = new SKPaint { BlendMode = SKBlendMode.Src };
                    canvas.DrawBitmap(window, rect.X, rect.Y, copy);
                }
                else
                {
                    using var blank = new SKPaint { Color = BlankWindow };
                    canvas.DrawRect(SKRect.Create(rect.X, rect.Y, rect.Width, rect.Height), blank);
                }
            }

            ct.ThrowIfCancellationRequested();
            if (!template.HasArtwork)
            {
                DrawHairlines(canvas, template, request.Photos);
                DrawSignature(canvas, template);
            }

            using var pixels = surface.PeekPixels();
            return new CompositionResult(EncodePng(pixels), template.Width, template.Height);
        }
        finally
        {
            foreach (var window in windows)
            {
                window?.Dispose();
            }
        }
    }

    // Decodes, cover-crops, and applies the Look to every photograph window, each into its own bitmap. The windows share nothing,
    // so they run in parallel (a strip is mostly decode time), and the result is the same as doing them one after another.
    // Cancellation wins over errors; among several errors the lowest window's is reported.
    private static SKBitmap?[] RenderWindows(TemplateDefinition template, CompositionRequest request, CancellationToken ct)
    {
        var windows = new SKBitmap?[template.WindowCount];
        var errors = new Exception?[template.WindowCount];
        var applyLook = request.LookId != CatalogData.NaturaleId;
        var options = new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Math.Max(1, Math.Min(template.WindowCount, Environment.ProcessorCount)) };
        try
        {
            ct.ThrowIfCancellationRequested();
            Parallel.For(0, template.WindowCount, options, (index, state) =>
            {
                if (request.Photos[index] is not { } photo)
                {
                    return;
                }

                try
                {
                    ct.ThrowIfCancellationRequested();
                    var rect = template.Windows[index];
                    windows[index] = RenderPhoto(photo, rect.Width, rect.Height, rect.Width / (double)rect.Height, request.LookId, applyLook);
                }
                catch (Exception ex)
                {
                    errors[index] = ex;
                    state.Stop();
                }
            });

            ct.ThrowIfCancellationRequested();
            if (errors.FirstOrDefault(error => error is not null) is { } failure)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }

            return windows;
        }
        catch
        {
            foreach (var window in windows)
            {
                window?.Dispose();
            }

            throw;
        }
    }
    private CompositionResult Thumbnail(CompositionPhoto photo, double aspectRatio, string lookId, int width, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(photo);
        ArgumentNullException.ThrowIfNull(lookId);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        if (!double.IsFinite(aspectRatio) || aspectRatio <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(aspectRatio), "The aspect ratio must be a positive finite number.");
        }

        ct.ThrowIfCancellationRequested();
        if (_catalog.FindLook(lookId) is null || !LookKernels.IsKnown(lookId))
        {
            throw new CompositionException(CompositionErrorKind.UnknownLook, $"No Look {lookId}.");
        }

        ct.ThrowIfCancellationRequested();
        var height = Math.Max(1, (int)Math.Round(width / aspectRatio, MidpointRounding.AwayFromZero));
        using var bitmap = RenderPhoto(photo, width, height, aspectRatio, lookId, lookId != CatalogData.NaturaleId);
        using var pixels = bitmap.PeekPixels();
        return new CompositionResult(EncodePng(pixels), width, height);
    }

    // Draws the photograph centre-covered into its own window-sized bitmap and runs the Look on exactly those pixels. The bitmap
    // is then copied onto the paper, so the Look can never reach artwork or paper. It starts as the blank window colour so a
    // photograph with transparency lands on the same background the artwork has there.
    private static SKBitmap RenderPhoto(CompositionPhoto photo, int width, int height, double ratio, string lookId, bool applyLook)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        try
        {
            using var source = Decode(photo);
            var crop = CoverCrop.Compute(source.Width, source.Height, ratio);
            var left = (float)Math.Clamp(crop.X, 0, source.Width);
            var top = (float)Math.Clamp(crop.Y, 0, source.Height);
            var from = new SKRect(
                left,
                top,
                (float)Math.Clamp(crop.X + crop.Width, left, source.Width),
                (float)Math.Clamp(crop.Y + crop.Height, top, source.Height));
            var downscale = Math.Min(from.Width / width, from.Height / height);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(BlankWindow);
                canvas.DrawImage(source, from, new SKRect(0, 0, width, height), downscale >= MipmapDownscaleThreshold ? MipmapSampling : CubicSampling);
            }

            if (applyLook)
            {
                LookKernels.Apply(bitmap.GetPixelSpan(), lookId);
            }

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static SKImage Decode(CompositionPhoto photo) =>
        photo is CompositionPhoto.Encoded encoded ? DecodeEncoded(encoded.Bytes) : WrapFrame(((CompositionPhoto.Bgra)photo).Frame);

    private static SKImage DecodeEncoded(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            throw new CompositionException(CompositionErrorKind.UndecodablePhoto, "The photograph has no data.");
        }

        using var data = SKData.CreateCopy(bytes.Span);
        using var encoded = SKImage.FromEncodedData(data)
            ?? throw new CompositionException(CompositionErrorKind.UndecodablePhoto, "The photograph can't be decoded.");
        return encoded.ToRasterImage(ensurePixelData: true)
            ?? throw new CompositionException(CompositionErrorKind.UndecodablePhoto, "The photograph can't be decoded.");
    }

    private static SKImage WrapFrame(CapturedFrame frame)
    {
        var minimumLength = frame.Height > 0 ? ((long)(frame.Height - 1) * frame.Stride) + ((long)frame.Width * 4) : 0;
        if (frame.Width < 1 || frame.Height < 1 || frame.Stride < frame.Width * 4 || frame.Bgra.Length < minimumLength)
        {
            throw new CompositionException(CompositionErrorKind.UndecodablePhoto, "The raw frame's size and stride don't match its data.");
        }

        // Camera frames carry no meaningful alpha, so the frame is read as opaque.
        return SKImage.FromPixelCopy(new SKImageInfo(frame.Width, frame.Height, SKColorType.Bgra8888, SKAlphaType.Opaque), frame.Bgra.Span, frame.Stride)
            ?? throw new CompositionException(CompositionErrorKind.UndecodablePhoto, "The raw frame can't be read.");
    }

    // Empty windows get a 1 px hairline along their leading edge only when the previous window is empty too, so a hairline never
    // sits beside a photograph and never ends up in a captured strip.
    private static void DrawHairlines(SKCanvas canvas, TemplateDefinition template, IReadOnlyList<CompositionPhoto?> photos)
    {
        using var paint = new SKPaint { Color = Hairline, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        for (var index = 1; index < template.WindowCount; index++)
        {
            if (photos[index] is not null || photos[index - 1] is not null)
            {
                continue;
            }

            var rect = template.Windows[index];
            if (template.IsLandscape)
            {
                canvas.DrawLine(rect.X, rect.Y, rect.X, template.Height, paint);
            }
            else
            {
                canvas.DrawLine(rect.X, rect.Y, template.Width, rect.Y, paint);
            }
        }
    }

    private static void DrawSignature(SKCanvas canvas, TemplateDefinition template)
    {
        var panorama = template.Id == CatalogData.PanoramaFourId;
        if (!panorama && template.Id != CatalogData.SignatureStripId)
        {
            return;
        }

        var size = panorama ? 48f : 58f;
        var maxWidth = panorama ? 300f : 390f;
        var anchorX = panorama ? template.Width - 24f : template.Width / 2f;
        var anchorY = panorama ? template.Height - 35f : template.Height / 2f;
        var alignment = panorama ? 1f : 0.5f;

        using var font = new SKFont(CatalogAssets.CormorantGaramondMedium, size)
        {
            Edging = SKFontEdging.Antialias,
            Subpixel = true,
            Hinting = SKFontHinting.None,
        };
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        var measured = font.MeasureText(Signature);
        var scale = measured > maxWidth ? maxWidth / measured : 1f;
        var metrics = font.Metrics;
        var baseline = anchorY - ((metrics.Ascent + metrics.Descent) / 2);

        canvas.Save();
        canvas.Translate(anchorX, baseline);
        canvas.Scale(scale, 1);
        canvas.DrawText(Signature, -alignment * measured, 0, font, paint);
        canvas.Restore();
    }

    // Lossless either way; the Sub filter at zlib level 1 is several times faster than Skia's default and about a third larger,
    // which is the right trade for a strip that must be ready within a fraction of a second.
    private static byte[] EncodePng(SKPixmap pixels)
    {
        using var data = pixels.Encode(new SKPngEncoderOptions(SKPngEncoderFilterFlags.Sub, 1))
            ?? throw new CompositionException(CompositionErrorKind.Unknown, "The composition can't be encoded as PNG.");
        return data.ToArray();
    }
}
