namespace FotoHavn.Host.Windows.Preview;

internal static class NativeCopy
{
    /// <summary>Copies <paramref name="source"/> to the native memory at <paramref name="destination"/>, which must hold at least that many bytes.</summary>
    public static unsafe void Copy(ReadOnlySpan<byte> source, IntPtr destination) =>
        source.CopyTo(new Span<byte>((void*)destination, source.Length));
}
