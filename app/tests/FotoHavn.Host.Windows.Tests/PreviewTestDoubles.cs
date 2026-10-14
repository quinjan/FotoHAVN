using System.Runtime.InteropServices;
using FotoHavn.Host.Windows.Preview;

namespace FotoHavn.Host.Windows.Tests;

/// <summary>A time provider the test moves by hand.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 10, 14, 8, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>A shared buffer in native memory, like WebView2's: the channel writes through <see cref="Pointer"/>.</summary>
internal sealed class FakeFrameBuffer : ISharedFrameBuffer
{
    // A pinned array stands in for native memory: stable address, collected with the buffer.
    private readonly byte[] _memory;

    public FakeFrameBuffer(long size)
    {
        Size = size;
        _memory = GC.AllocateArray<byte>((int)size, pinned: true);
        Pointer = Marshal.UnsafeAddrOfPinnedArrayElement(_memory, 0);
    }

    public IntPtr Pointer { get; }

    public long Size { get; }

    public bool Disposed { get; private set; }

    public byte[] Read(int count)
    {
        var bytes = new byte[count];
        Marshal.Copy(Pointer, bytes, 0, count);
        return bytes;
    }

    public void Dispose() => Disposed = true;
}

/// <summary>A WebView2 stand-in: it makes native buffers and records what is posted, with the pixels as they were at the post.</summary>
internal sealed class FakeSurface : IPreviewSurface
{
    public List<FakeFrameBuffer> Buffers { get; } = [];

    public List<(FakeFrameBuffer Buffer, string Json, byte[] Pixels)> Posts { get; } = [];

    public Exception? CreateFailure { get; set; }

    public Exception? PostFailure { get; set; }

    public ISharedFrameBuffer CreateBuffer(long size)
    {
        if (CreateFailure is { } failure)
        {
            throw failure;
        }

        var buffer = new FakeFrameBuffer(size);
        Buffers.Add(buffer);
        return buffer;
    }

    public void Post(ISharedFrameBuffer buffer, string metadataJson)
    {
        if (PostFailure is { } failure)
        {
            throw failure;
        }

        var fake = (FakeFrameBuffer)buffer;
        Posts.Add((fake, metadataJson, fake.Read((int)Math.Min(fake.Size, 64))));
    }
}
