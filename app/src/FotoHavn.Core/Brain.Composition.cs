using FotoHavn.Core.Camera;
using FotoHavn.Core.Catalog;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;
using Microsoft.Extensions.Logging;

namespace FotoHavn.Core;

// Composing in the brain for the diagnostics hooks (M7, ADR 0006, contract section 3). The brain keeps the last few diagnostics
// captures (the mirrored BGRA frames that were saved as JPEG), numbers each arrangement of them with a revision, and composes
// a Template over them through the compositor, off the executor. The PNG goes into the preview cache under a key made from the
// Template, the Look, and the revision, and the Operator Screen shows it from there.
public sealed partial class Brain
{
    /// <summary>
    /// How many diagnostics captures the brain keeps, newest last. They are 1920x1080 BGRA frames, so the ring holds about
    /// 33 MB at most; that is acceptable for a diagnostics surface and is released when the surface is reopened or the
    /// selected camera changes.
    /// </summary>
    internal const int DiagnosticsCaptureLimit = 4;

    /// <summary>The revision of a blank preview: it shows no capture, so it doesn't depend on the arrangement.</summary>
    internal const long BlankPreviewRevision = 0;

    private readonly List<CapturedFrame> _diagnosticsCaptures = [];
    private long _arrangementRevision;
    private int _compositionRequest;
    private CompositionView? _lastComposition;

    /// <summary>Adds a capture as the newest (dropping the oldest beyond the limit) and starts a new arrangement.</summary>
    private void AddDiagnosticsCapture(CapturedFrame frame)
    {
        if (_diagnosticsCaptures.Count == DiagnosticsCaptureLimit)
        {
            _diagnosticsCaptures.RemoveAt(0);
        }

        _diagnosticsCaptures.Add(frame);
        _arrangementRevision++;
    }

    /// <summary>
    /// Drops the captures and the composition made from them (and its cached image), so a later composition is a new
    /// arrangement. A composition still being made is no longer shown when it arrives.
    /// </summary>
    private void ReleaseDiagnosticsCaptures()
    {
        if (_diagnosticsCaptures.Count == 0 && _lastComposition is null)
        {
            return;
        }

        _diagnosticsCaptures.Clear();
        _arrangementRevision++;
        if (_lastComposition is { } last)
        {
            _dependencies.Previews.Remove(last.PreviewKey);
            _lastComposition = null;
        }
    }

    /// <summary>
    /// <c>test.composeDiagnostics</c>: the newest captures that fit, oldest of them first, fill the Template's windows in order;
    /// with fewer captures than windows, the rest stay empty.
    /// </summary>
    private void ComposeDiagnosticsHook(Connection connection, string id, string? templateId, string? lookId) =>
        StartComposition(connection, id, templateId, lookId, blank: false);

    /// <summary><c>test.composeBlankPreview</c>: every window empty, in Naturale.</summary>
    private void ComposeBlankPreviewHook(Connection connection, string id, string? templateId) =>
        StartComposition(connection, id, templateId, _dependencies.Catalog.DefaultLookId, blank: true);

    private void StartComposition(Connection connection, string id, string? templateId, string? lookId, bool blank)
    {
        if (string.IsNullOrEmpty(templateId)
            || string.IsNullOrEmpty(lookId)
            || _dependencies.Catalog.FindTemplate(templateId) is not { } template
            || _dependencies.Catalog.FindLook(lookId) is null)
        {
            Reply(connection, id, HookOutcome.Fail(TestHookError.InvalidArgs));
            return;
        }

        var arrangement = _arrangementRevision;
        var photos = new CompositionPhoto?[template.WindowCount];
        if (!blank)
        {
            // The newest captures that fit, in capture order: with more captures than windows the oldest ones are left out.
            var used = Math.Min(photos.Length, _diagnosticsCaptures.Count);
            var first = _diagnosticsCaptures.Count - used;
            for (var window = 0; window < used; window++)
            {
                photos[window] = new CompositionPhoto.Bgra(_diagnosticsCaptures[first + window]);
            }
        }

        var request = ++_compositionRequest;
        var keyRevision = blank ? BlankPreviewRevision : arrangement;
        var compositor = _dependencies.Compositor;
        var composition = new CompositionRequest(template.Id, lookId, photos);

        // Task.Run: the compositor does its work off the executor even when its own first steps run synchronously.
        Task.Run(() => compositor.ComposeAsync(composition, CancellationToken.None)).ContinueWith(
            completed => Post(() => CompleteComposition(connection, id, completed, template.Id, lookId, keyRevision, request, arrangement)),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void CompleteComposition(Connection connection, string id, Task<CompositionResult> completed, string templateId, string lookId, long keyRevision, int request, long arrangement)
    {
        if (!completed.IsCompletedSuccessfully)
        {
            var exception = completed.Exception?.GetBaseException();
            BrainLog.CompositionFailed(_logger, exception, templateId, lookId, (exception as CompositionException)?.Kind ?? CompositionErrorKind.Unknown);
            Reply(connection, id, HookOutcome.Fail(TestHookError.CompositionFailed));
            return;
        }

        var result = completed.Result;
        var key = PreviewKey.For(templateId, lookId, keyRevision);
        if (!TryCache(key, result.Png))
        {
            BrainLog.CompositionFailed(_logger, null, templateId, lookId, CompositionErrorKind.Unknown);
            Reply(connection, id, HookOutcome.Fail(TestHookError.CompositionFailed));
            return;
        }

        // Only the newest request of the current arrangement is the one shown; an older or outdated one still answers its caller.
        if (request == _compositionRequest && arrangement == _arrangementRevision)
        {
            _lastComposition = new CompositionView(key, templateId, lookId, result.Width, result.Height);
            PublishChanges();
        }

        Reply(connection, id, HookOutcome.Ok(new ComposePreviewResult(key, result.Width, result.Height, result.Png.Length)));
    }

    private bool TryCache(string key, ReadOnlyMemory<byte> png)
    {
        try
        {
            return _dependencies.Previews.Put(key, png);
        }
        catch (ArgumentException)
        {
            // A compositor that answers an unusable (empty) image fails the hook; the cache keeps nothing.
            return false;
        }
    }
}

internal static partial class BrainLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Composing Template {TemplateId} with Look {LookId} failed ({Kind}).")]
    public static partial void CompositionFailed(ILogger logger, Exception? exception, string templateId, string lookId, CompositionErrorKind kind);
}
