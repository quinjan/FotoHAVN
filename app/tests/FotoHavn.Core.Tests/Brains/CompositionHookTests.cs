using System.Collections.Concurrent;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Catalog;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Media;
using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Tests.Brains;

/// <summary>The diagnostics composition hooks: the capture ring, the arrangement revision, composing off the executor, and the preview cache.</summary>
public sealed class CompositionHookTests
{
    private const string Signature = "signature-strip";
    private const string Naturale = "naturale-v1";

    [Theory]
    [InlineData(TestHookNames.ComposeDiagnostics, """{"templateId":"signature-strip","lookId":"naturale-v1"}""")]
    [InlineData(TestHookNames.ComposeBlankPreview, """{"templateId":"panorama-four"}""")]
    public void The_composition_hooks_are_unavailable_without_the_e2e_hooks(string name, string args)
    {
        using var harness = new BrainHarness(hooks: false, hostHooks: new FakeHostHookPort());
        var client = harness.Connect(Role.Operator);

        var id = client.Test(name, args);
        harness.Run();
        harness.Run();

        Assert.Equal(TestResultMessage.Failure(id, TestHookError.HooksUnavailable), client.Single());
        Assert.Empty(harness.FakeCompositor.Requests);
        Assert.Equal(0, harness.Previews.Count);
    }

    [Theory]
    [InlineData(TestHookNames.ComposeDiagnostics, """{"templateId":"signature-strip"}""")]
    [InlineData(TestHookNames.ComposeBlankPreview, """{}""")]
    public void The_composition_hooks_validate_their_arguments(string name, string args)
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Connect(Role.Operator);

        var id = client.Test(name, args);
        harness.Run();

        Assert.Equal(TestResultMessage.Failure(id, TestHookError.InvalidArgs), client.Single());
    }

    [Theory]
    [InlineData("""{"templateId":"nope","lookId":"naturale-v1"}""")]
    [InlineData("""{"templateId":"signature-strip","lookId":"nope"}""")]
    [InlineData("""{"templateId":"Signature-Strip","lookId":"naturale-v1"}""")]
    [InlineData("""{"templateId":"signature-strip","lookId":"naturale"}""")]
    [InlineData("""{"templateId":"","lookId":"naturale-v1"}""")]
    [InlineData("""{"templateId":"signature-strip","lookId":""}""")]
    [InlineData("""{"templateId":null,"lookId":"naturale-v1"}""")]
    [InlineData("""{"templateId":"signature-strip","lookId":null}""")]
    public void An_unknown_template_or_look_is_an_invalid_argument_and_composes_nothing(string args)
    {
        using var rig = new CameraRig();

        var result = rig.Call(TestHookNames.ComposeDiagnostics, args);

        Assert.Equal(TestHookError.InvalidArgs, result.Error);
        Assert.Empty(rig.Harness.FakeCompositor.Requests);
    }

    [Theory]
    [InlineData("""{"templateId":"nope"}""")]
    [InlineData("""{"templateId":""}""")]
    [InlineData("""{"templateId":null}""")]
    public void A_blank_preview_of_an_unknown_template_is_an_invalid_argument(string args)
    {
        using var rig = new CameraRig();

        Assert.Equal(TestHookError.InvalidArgs, rig.Call(TestHookNames.ComposeBlankPreview, args).Error);
        Assert.Empty(rig.Harness.FakeCompositor.Requests);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(4, 4)]
    [InlineData(5, 4)]
    public void The_captures_fill_the_windows_oldest_first_and_the_ring_keeps_the_last_four(int captures, int filled)
    {
        using var rig = ReadyRig();
        var stamps = Capture(rig, captures);

        var result = Compose(rig, Signature, Naturale);

        Assert.True(result.Ok);
        var request = Assert.Single(rig.Harness.FakeCompositor.Requests);
        Assert.Equal(Signature, request.TemplateId);
        Assert.Equal(Naturale, request.LookId);
        Assert.Equal(4, request.Photos.Count);
        var photos = request.Photos.Select(photo => (photo as CompositionPhoto.Bgra)?.Frame.Timestamp).ToList();
        var expected = stamps.TakeLast(4).Select(stamp => (TimeSpan?)stamp).Concat(Enumerable.Repeat<TimeSpan?>(null, 4 - filled)).ToList();
        Assert.Equal(expected, photos);
        Assert.Equal(filled, photos.Count(stamp => stamp is not null));
    }

    [Fact]
    public void A_three_window_template_takes_the_newest_three_captures_in_capture_order()
    {
        using var rig = ReadyRig();
        var stamps = Capture(rig, 5);

        var result = Compose(rig, "butter-gingham", "sepia-v1");

        Assert.True(result.Ok);
        var request = Assert.Single(rig.Harness.FakeCompositor.Requests);
        Assert.Equal(3, request.Photos.Count);
        Assert.Equal(
            stamps.TakeLast(3).ToList(),
            request.Photos.Select(photo => ((CompositionPhoto.Bgra)photo!).Frame.Timestamp).ToList());
    }

    [Fact]
    public void The_composition_is_cached_under_its_key_and_shown_as_the_last_composition()
    {
        using var rig = ReadyRig();
        Capture(rig, 2);

        var result = Compose(rig, "panorama-four", "sepia-v1");

        var composed = ProtocolJson.Deserialize<ComposePreviewResult>(result.Result!.Value);
        var png = FakeCompositor.SolidPng(1800, 600);
        Assert.Equal(new ComposePreviewResult("panorama-four.sepia-v1.r2", 1800, 600, png.Length), composed);
        Assert.True(rig.Harness.Previews.TryGet(composed.PreviewKey, out var cached));
        Assert.Equal(png, cached.ToArray());
        Assert.Equal(new CompositionView("panorama-four.sepia-v1.r2", "panorama-four", "sepia-v1", 1800, 600), rig.View.LastComposition);
        Assert.Equal(CameraState.Ready, rig.View.State);
    }

    [Fact]
    public void The_preview_key_changes_with_the_arrangement_revision()
    {
        using var rig = ReadyRig();
        Capture(rig, 1);
        var first = Key(Compose(rig, Signature, Naturale));
        var again = Key(Compose(rig, Signature, Naturale));

        Capture(rig, 1);
        var second = Key(Compose(rig, Signature, Naturale));
        var otherLook = Key(Compose(rig, Signature, "classic-v1"));

        Assert.Equal("signature-strip.naturale-v1.r1", first);
        Assert.Equal(first, again);
        Assert.Equal("signature-strip.naturale-v1.r2", second);
        Assert.Equal("signature-strip.classic-v1.r2", otherLook);
        Assert.True(rig.Harness.Previews.TryGet(first, out _));
        Assert.True(rig.Harness.Previews.TryGet(second, out _));
    }

    [Fact]
    public void A_blank_preview_has_every_window_empty_in_naturale_under_the_reserved_revision()
    {
        using var rig = ReadyRig();
        Capture(rig, 3);

        var result = rig.Call(TestHookNames.ComposeBlankPreview, """{"templateId":"polka-keepsake"}""");

        Assert.True(result.Ok);
        var composed = ProtocolJson.Deserialize<ComposePreviewResult>(result.Result!.Value);
        Assert.Equal("polka-keepsake.naturale-v1.r0", composed.PreviewKey);
        Assert.Equal((600, 1800), (composed.Width, composed.Height));
        var request = Assert.Single(rig.Harness.FakeCompositor.Requests);
        Assert.Equal(("polka-keepsake", Naturale), (request.TemplateId, request.LookId));
        Assert.Equal(4, request.Photos.Count);
        Assert.All(request.Photos, Assert.Null);
        Assert.True(rig.Harness.Previews.TryGet(composed.PreviewKey, out _));
        Assert.Equal(new CompositionView("polka-keepsake.naturale-v1.r0", "polka-keepsake", Naturale, 600, 1800), rig.View.LastComposition);
    }

    [Fact]
    public void A_compositor_failure_is_a_composition_failed_hook_error_and_is_logged_with_its_kind()
    {
        using var rig = ReadyRig();
        rig.Harness.FakeCompositor.Failure = new CompositionException(CompositionErrorKind.UndecodablePhoto, "Window 2 is not an image.");

        var result = Compose(rig, Signature, Naturale);
        rig.Harness.FakeCompositor.Failure = new InvalidOperationException("Other failure.");
        var other = rig.Call(TestHookNames.ComposeBlankPreview, """{"templateId":"signature-strip"}""");

        Assert.Equal(TestHookError.CompositionFailed, result.Error);
        Assert.Equal(TestHookError.CompositionFailed, other.Error);
        Assert.Null(rig.View.LastComposition);
        Assert.Equal(0, rig.Harness.Previews.Count);
        Assert.Contains(rig.Harness.Logger.Entries, entry => entry.Message == "Composing Template signature-strip with Look naturale-v1 failed (UndecodablePhoto).");
        Assert.Contains(rig.Harness.Logger.Entries, entry => entry.Message == "Composing Template signature-strip with Look naturale-v1 failed (Unknown).");
    }

    [Fact]
    public void A_synchronously_throwing_compositor_fails_the_hook_too()
    {
        using var rig = ReadyRig(compositor: new ThrowingCompositor());

        Assert.Equal(TestHookError.CompositionFailed, Compose(rig, Signature, Naturale).Error);
    }

    [Fact]
    public void A_compositor_whose_image_cannot_be_cached_fails_the_hook()
    {
        using var small = ReadyRig(previews: new PreviewCache(maxBytes: 10));
        Assert.Equal(TestHookError.CompositionFailed, Compose(small, Signature, Naturale).Error);
        Assert.Null(small.View.LastComposition);

        using var empty = ReadyRig(compositor: new EmptyImageCompositor());
        Assert.Equal(TestHookError.CompositionFailed, Compose(empty, Signature, Naturale).Error);
        Assert.Null(empty.View.LastComposition);
        Assert.Equal(0, empty.Harness.Previews.Count);
    }

    [Fact]
    public void Composing_runs_off_the_executor_and_never_blocks_it()
    {
        var gated = new GatedCompositor();
        using var rig = ReadyRig(compositor: gated);

        var id = rig.Operator.Test(TestHookNames.ComposeDiagnostics, """{"templateId":"signature-strip","lookId":"naturale-v1"}""");
        WaitUntil(() => gated.Pending.Count == 1, rig);

        // The compositor is still working; the brain answers other hooks and publishes meanwhile.
        Assert.DoesNotContain(rig.Operator.Messages, message => message is TestResultMessage { Id: var answered } && answered == id);
        Assert.Equal(CameraState.Ready, rig.Diagnostics().State);
        Assert.NotEqual(Environment.CurrentManagedThreadId, Assert.Single(gated.ThreadIds));
        Assert.Null(rig.View.LastComposition);

        gated.Complete(Signature, 600, 1800);
        var result = rig.Await(id);

        Assert.True(result.Ok);
        Assert.Equal("signature-strip.naturale-v1.r0", rig.View.LastComposition!.PreviewKey);
    }

    [Fact]
    public void Only_the_newest_composition_is_shown_when_two_overlap()
    {
        var gated = new GatedCompositor();
        using var rig = ReadyRig(compositor: gated);
        var first = rig.Operator.Test(TestHookNames.ComposeDiagnostics, """{"templateId":"signature-strip","lookId":"naturale-v1"}""");
        var second = rig.Operator.Test(TestHookNames.ComposeDiagnostics, """{"templateId":"panorama-four","lookId":"classic-v1"}""");
        WaitUntil(() => gated.Pending.Count == 2, rig);

        gated.Complete("panorama-four", 1800, 600);
        Assert.True(rig.Await(second).Ok);
        gated.Complete(Signature, 600, 1800);
        Assert.True(rig.Await(first).Ok);

        Assert.Equal("panorama-four.classic-v1.r0", rig.View.LastComposition!.PreviewKey);
        Assert.True(rig.Harness.Previews.TryGet("signature-strip.naturale-v1.r0", out _));
    }

    [Fact]
    public void A_composition_of_an_outdated_arrangement_still_answers_but_is_not_shown()
    {
        var gated = new GatedCompositor();
        using var rig = ReadyRig(compositor: gated);
        var id = rig.Operator.Test(TestHookNames.ComposeDiagnostics, """{"templateId":"signature-strip","lookId":"naturale-v1"}""");
        WaitUntil(() => gated.Pending.Count == 1, rig);

        Capture(rig, 1);
        gated.Complete(Signature, 600, 1800);

        Assert.True(rig.Await(id).Ok);
        Assert.Null(rig.View.LastComposition);
    }

    [Fact]
    public void Reopening_the_surface_releases_the_captures_the_composition_and_its_cached_image()
    {
        using var rig = ReadyRig();
        Capture(rig, 2);
        var key = Key(Compose(rig, Signature, Naturale));
        Assert.True(rig.Harness.Previews.TryGet(key, out _));

        rig.OpenDiagnostics();

        Assert.Null(rig.View.LastComposition);
        Assert.False(rig.Harness.Previews.TryGet(key, out _));
        Assert.Equal("signature-strip.naturale-v1.r3", Key(Compose(rig, Signature, Naturale)));
        Assert.All(rig.Harness.FakeCompositor.Requests[^1].Photos, Assert.Null);
    }

    [Fact]
    public void Selecting_another_camera_releases_the_captures_but_selecting_the_same_one_keeps_them()
    {
        using var rig = new CameraRig([FakeCameraAdapter.DefaultCamera, new AvailableCamera("other", "Other")]);
        rig.SelectAndWaitUntilReady();
        Capture(rig, 2);
        var key = Key(Compose(rig, Signature, Naturale));

        Assert.True(rig.Select("fake-camera").Ok);
        Assert.Equal(key, rig.View.LastComposition!.PreviewKey);
        rig.Run(TimeSpan.FromMilliseconds(500));

        Assert.True(rig.Select("other").Ok);
        Assert.Null(rig.View.LastComposition);
        Assert.False(rig.Harness.Previews.TryGet(key, out _));
        rig.Run(TimeSpan.FromMilliseconds(500));
        Assert.True(Compose(rig, Signature, Naturale).Ok);
        Assert.All(rig.Harness.FakeCompositor.Requests[^1].Photos, Assert.Null);
    }

    [Fact]
    public void Hiding_the_diagnostics_surface_releases_the_captures()
    {
        using var rig = ReadyRig();
        Capture(rig, 1);
        Compose(rig, Signature, Naturale);

        rig.Harness.Brain.ShowCameraDiagnostics(null);
        rig.Harness.Run();
        rig.Harness.Brain.ShowCameraDiagnostics(null);
        rig.Harness.Run();

        Assert.Equal(0, rig.Harness.Previews.Count);
        Assert.True(Compose(rig, Signature, Naturale).Ok);
        Assert.All(rig.Harness.FakeCompositor.Requests[^1].Photos, Assert.Null);
    }

    [Fact]
    public void A_capture_the_host_failed_to_save_is_not_kept()
    {
        using var rig = ReadyRig();
        rig.Host.SaveCapture = (_, _) => Task.FromException<CameraCaptureView>(new HookFailure(TestHookError.HookFailed));
        var id = rig.Operator.Test(TestHookNames.CaptureDiagnostics);
        rig.Harness.Run();
        rig.Advance(CameraRig.Frame * 2);
        Assert.False(rig.Await(id).Ok);

        Compose(rig, Signature, Naturale);

        Assert.All(rig.Harness.FakeCompositor.Requests[^1].Photos, Assert.Null);
    }

    [Fact]
    public void The_last_composition_reaches_the_operator_presentation_with_a_new_revision()
    {
        using var rig = ReadyRig();
        Capture(rig, 1);
        var revision = rig.Revisions[^1];

        Compose(rig, "butter-gingham", "monochrome-v1");

        Assert.True(rig.Revisions[^1] > revision);
        Assert.Equal(new CompositionView("butter-gingham.monochrome-v1.r1", "butter-gingham", "monochrome-v1", 600, 1800), rig.View.LastComposition);
    }

    [Fact]
    public void The_brain_requires_a_catalog_a_compositor_and_a_preview_store()
    {
        using var harness = new BrainHarness();

        Assert.Throws<ArgumentNullException>(() => new Brain(harness.Dependencies with { Catalog = null! }));
        Assert.Throws<ArgumentNullException>(() => new Brain(harness.Dependencies with { Compositor = null! }));
        Assert.Throws<ArgumentNullException>(() => new Brain(harness.Dependencies with { Previews = null! }));
    }

    private static CameraRig ReadyRig(PreviewCache? previews = null, ICompositor? compositor = null)
    {
        var rig = new CameraRig(previews: previews, compositor: compositor);
        rig.SelectAndWaitUntilReady();
        return rig;
    }

    /// <summary>Takes <paramref name="count"/> diagnostics captures and returns their frame timestamps, oldest first.</summary>
    private static List<TimeSpan> Capture(CameraRig rig, int count)
    {
        var stamps = new List<TimeSpan>();
        rig.Host.SaveCapture = (frame, mirrored) =>
        {
            stamps.Add(frame.Timestamp);
            return Task.FromResult(new CameraCaptureView($"capture-{stamps.Count}.jpg", frame.Width, frame.Height, 100, mirrored));
        };
        for (var index = 0; index < count; index++)
        {
            var id = rig.Operator.Test(TestHookNames.CaptureDiagnostics);
            rig.Harness.Run();
            rig.Advance(CameraRig.Frame * 2);
            Assert.True(rig.Await(id).Ok);
        }

        return stamps;
    }

    private static TestResultMessage Compose(CameraRig rig, string templateId, string lookId) =>
        rig.Call(TestHookNames.ComposeDiagnostics, $$"""{"templateId":"{{templateId}}","lookId":"{{lookId}}"}""");

    private static string Key(TestResultMessage result) =>
        ProtocolJson.Deserialize<ComposePreviewResult>(result.Result!.Value).PreviewKey;

    private static void WaitUntil(Func<bool> condition, CameraRig rig)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            rig.Harness.Run();
            Assert.True(DateTime.UtcNow < deadline, "The condition never held.");
            Thread.Sleep(2);
        }
    }

    private sealed class ThrowingCompositor : ICompositor
    {
        public Task<CompositionResult> ComposeAsync(CompositionRequest request, CancellationToken ct) => throw new InvalidOperationException("No compositing today.");

        public Task<CompositionResult> ComposeLookThumbnailAsync(CompositionPhoto photo, double aspectRatio, string lookId, int width, CancellationToken ct) =>
            throw new InvalidOperationException("No compositing today.");
    }

    private sealed class EmptyImageCompositor : ICompositor
    {
        public Task<CompositionResult> ComposeAsync(CompositionRequest request, CancellationToken ct) =>
            Task.FromResult(new CompositionResult(ReadOnlyMemory<byte>.Empty, 600, 1800));

        public Task<CompositionResult> ComposeLookThumbnailAsync(CompositionPhoto photo, double aspectRatio, string lookId, int width, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    /// <summary>A compositor whose results the test releases by hand, recording the thread each call arrived on.</summary>
    private sealed class GatedCompositor : ICompositor
    {
        private readonly Lock _gate = new();
        private readonly List<(string TemplateId, TaskCompletionSource<CompositionResult> Source)> _pending = [];

        public ConcurrentQueue<int> ThreadIds { get; } = new();

        public IReadOnlyList<(string TemplateId, TaskCompletionSource<CompositionResult> Source)> Pending
        {
            get
            {
                lock (_gate)
                {
                    return [.. _pending];
                }
            }
        }

        public Task<CompositionResult> ComposeAsync(CompositionRequest request, CancellationToken ct)
        {
            ThreadIds.Enqueue(Environment.CurrentManagedThreadId);
            var source = new TaskCompletionSource<CompositionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
            {
                _pending.Add((request.TemplateId, source));
            }

            return source.Task;
        }

        public Task<CompositionResult> ComposeLookThumbnailAsync(CompositionPhoto photo, double aspectRatio, string lookId, int width, CancellationToken ct) =>
            throw new NotSupportedException();

        public void Complete(string templateId, int width, int height) =>
            Pending.Single(pending => pending.TemplateId == templateId).Source.SetResult(new CompositionResult(FakeCompositor.SolidPng(width, height), width, height));
    }
}
