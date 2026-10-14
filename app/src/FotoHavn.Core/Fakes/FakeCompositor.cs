using System.IO.Compression;

namespace FotoHavn.Core.Fakes;

/// <summary>
/// An <see cref="Catalog.ICompositor"/> for tests and the headless host: it validates the request as the real compositor
/// does and answers a solid Warm Ivory PNG of the Template's native size (SkiaSharp-free, so it lives in Core). It records
/// every request it receives. <see cref="Failure"/> makes every call fail with that exception.
/// </summary>
public sealed class FakeCompositor(Catalog.ICatalog catalog) : Catalog.ICompositor
{
    private readonly Lock _gate = new();
    private readonly List<Catalog.CompositionRequest> _requests = [];
    private readonly List<ThumbnailRequest> _thumbnails = [];

    /// <summary>The exception every call fails with while set; null composes normally.</summary>
    public Exception? Failure { get; set; }

    /// <summary>The strip requests received, oldest first.</summary>
    public IReadOnlyList<Catalog.CompositionRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>The thumbnail requests received, oldest first.</summary>
    public IReadOnlyList<ThumbnailRequest> ThumbnailRequests
    {
        get
        {
            lock (_gate)
            {
                return [.. _thumbnails];
            }
        }
    }

    public Task<Catalog.CompositionResult> ComposeAsync(Catalog.CompositionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            _requests.Add(request);
        }

        if (Failure is { } failure)
        {
            return Task.FromException<Catalog.CompositionResult>(failure);
        }

        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<Catalog.CompositionResult>(ct);
        }

        if (catalog.FindTemplate(request.TemplateId) is not { } template)
        {
            return Task.FromException<Catalog.CompositionResult>(new Catalog.CompositionException(Catalog.CompositionErrorKind.UnknownTemplate, $"No Template {request.TemplateId}."));
        }

        if (catalog.FindLook(request.LookId) is null)
        {
            return Task.FromException<Catalog.CompositionResult>(new Catalog.CompositionException(Catalog.CompositionErrorKind.UnknownLook, $"No Look {request.LookId}."));
        }

        if (request.Photos.Count != template.WindowCount)
        {
            return Task.FromException<Catalog.CompositionResult>(new Catalog.CompositionException(Catalog.CompositionErrorKind.WrongPhotoCount, $"{template.Id} has {template.WindowCount} windows, not {request.Photos.Count}."));
        }

        return Task.FromResult(new Catalog.CompositionResult(SolidPng(template.Width, template.Height), template.Width, template.Height));
    }

    public Task<Catalog.CompositionResult> ComposeLookThumbnailAsync(Catalog.CompositionPhoto photo, double aspectRatio, string lookId, int width, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(photo);
        lock (_gate)
        {
            _thumbnails.Add(new ThumbnailRequest(photo, aspectRatio, lookId, width));
        }

        if (Failure is { } failure)
        {
            return Task.FromException<Catalog.CompositionResult>(failure);
        }

        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<Catalog.CompositionResult>(ct);
        }

        if (catalog.FindLook(lookId) is null)
        {
            return Task.FromException<Catalog.CompositionResult>(new Catalog.CompositionException(Catalog.CompositionErrorKind.UnknownLook, $"No Look {lookId}."));
        }

        if (width < 1 || !double.IsFinite(aspectRatio) || aspectRatio <= 0)
        {
            return Task.FromException<Catalog.CompositionResult>(new Catalog.CompositionException(Catalog.CompositionErrorKind.Unknown, "The thumbnail size is not usable."));
        }

        var height = Math.Max(1, (int)Math.Round(width / aspectRatio, MidpointRounding.AwayFromZero));
        return Task.FromResult(new Catalog.CompositionResult(SolidPng(width, height), width, height));
    }

    /// <summary>A thumbnail request as received.</summary>
    public sealed record ThumbnailRequest(Catalog.CompositionPhoto Photo, double AspectRatio, string LookId, int Width);

    /// <summary>A minimal opaque 8-bit RGB PNG filled with Warm Ivory (<c>#F3EBDD</c>), written without any imaging library.</summary>
    internal static byte[] SolidPng(int width, int height)
    {
        var row = new byte[1 + (width * 3)];
        for (var x = 0; x < width; x++)
        {
            row[1 + (x * 3)] = 0xF3;
            row[2 + (x * 3)] = 0xEB;
            row[3 + (x * 3)] = 0xDD;
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (var y = 0; y < height; y++)
            {
                zlib.Write(row);
            }
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        WriteInt32(header, 0, width);
        WriteInt32(header, 4, height);
        header[8] = 8; // bit depth
        header[9] = 2; // colour type: RGB
        WriteChunk(png, "IHDR"u8, header);
        WriteChunk(png, "IDAT"u8, compressed.ToArray());
        WriteChunk(png, "IEND"u8, []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var length = new byte[4];
        WriteInt32(length, 0, data.Length);
        stream.Write(length);
        stream.Write(type);
        stream.Write(data);
        var crc = 0xFFFFFFFFu;
        crc = UpdateCrc(crc, type);
        crc = UpdateCrc(crc, data);
        var crcBytes = new byte[4];
        WriteInt32(crcBytes, 0, unchecked((int)(crc ^ 0xFFFFFFFFu)));
        stream.Write(crcBytes);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static void WriteInt32(byte[] target, int offset, int value)
    {
        target[offset] = (byte)(value >> 24);
        target[offset + 1] = (byte)(value >> 16);
        target[offset + 2] = (byte)(value >> 8);
        target[offset + 3] = (byte)value;
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
