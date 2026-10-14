using System.Runtime.InteropServices;

namespace FotoHavn.Camera.Windows;

/// <summary>A reusable byte array on the pinned object heap, so native code (Skia) can read and write it by address.</summary>
internal sealed class PinnedBuffer
{
    private byte[] _array = [];

    public IntPtr Pointer { get; private set; }

    public int Capacity => _array.Length;

    /// <summary>The backing array (at least <see cref="Capacity"/> long), for APIs that fill a byte[].</summary>
    public byte[] Array => _array;

    /// <summary>Grows the buffer when it is too small and returns true when it reallocated. Never shrinks, never copies old content.</summary>
    public bool EnsureCapacity(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (length <= _array.Length)
        {
            return false;
        }

        _array = GC.AllocateUninitializedArray<byte>(length, pinned: true);
        Pointer = Marshal.UnsafeAddrOfPinnedArrayElement(_array, 0);
        return true;
    }

    public Span<byte> AsSpan(int length) => _array.AsSpan(0, length);

    public Memory<byte> AsMemory(int length) => _array.AsMemory(0, length);
}
