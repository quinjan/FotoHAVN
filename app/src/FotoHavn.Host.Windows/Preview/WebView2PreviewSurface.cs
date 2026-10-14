using Microsoft.Web.WebView2.Core;

namespace FotoHavn.Host.Windows.Preview;

/// <summary>
/// A WebView2's frame surface: shared buffers from its environment, posted to the page with read-only access. Every
/// member runs on the UI thread. Not unit-tested (it needs a live WebView2); the channel's tests use a fake surface and
/// the E2E camera spec covers this one.
/// </summary>
internal sealed class WebView2PreviewSurface(CoreWebView2 core, CoreWebView2Environment environment) : IPreviewSurface
{
    public ISharedFrameBuffer CreateBuffer(long size) => new Buffer(environment.CreateSharedBuffer((ulong)size));

    public void Post(ISharedFrameBuffer buffer, string metadataJson) =>
        core.PostSharedBufferToScript(((Buffer)buffer).Native, CoreWebView2SharedBufferAccess.ReadOnly, metadataJson);

    private sealed class Buffer : ISharedFrameBuffer
    {
        public Buffer(CoreWebView2SharedBuffer native)
        {
            Native = native;
            // Read once, on the UI thread: reading it later or elsewhere breaks or crashes (Spike S1).
            Pointer = native.Buffer;
            Size = (long)native.Size;
        }

        public CoreWebView2SharedBuffer Native { get; }

        public IntPtr Pointer { get; }

        public long Size { get; }

        public void Dispose() => Native.Dispose();
    }
}
