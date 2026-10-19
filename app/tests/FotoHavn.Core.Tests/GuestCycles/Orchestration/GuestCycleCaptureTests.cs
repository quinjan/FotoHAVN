using FotoHavn.Core.Storage;
using FotoHavn.Core.Tests.Events.Active;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Cameras;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.GuestCycles.Orchestration;

/// <summary>The capture sequence and Retake (plan section 7, Capture sequence): the countdown, the flash, the Capture's frame, the commit, the pause, and stopping.</summary>
public sealed class GuestCycleCaptureTests
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(100);

    private static async Task<GuestCycleRig> OnCameraTabAsync(GuestRigOptions? options = null)
    {
        var rig = await GuestCycleRig.CreateAsync(options);
        rig.Begin();
        rig.ToPhotographs();
        return rig;
    }

    private static List<CaptureSequenceView> Sample(GuestCycleRig rig, Func<bool> until, TimeSpan? budget = null)
    {
        var samples = new List<CaptureSequenceView>();
        Assert.True(rig.AdvanceUntil(
            () =>
            {
                samples.Add(rig.Photographs.Sequence);
                return until();
            },
            budget ?? TimeSpan.FromMinutes(2),
            Tick));
        return samples;
    }

    [Fact]
    public async Task The_countdown_is_pushed_once_a_second_and_is_null_between_countdowns()
    {
        using var rig = await OnCameraTabAsync();
        rig.Guest("TakePhotographs").ShouldBeAccepted();

        var samples = Sample(rig, () => rig.Photographs.Windows[1].CaptureId is not null);

        var counts = samples.Select(sample => sample.CountdownRemaining).Where(count => count is not null).Distinct().ToArray();
        Assert.Equal([5, 4, 3, 2, 1], counts.Take(5));
        Assert.Contains(samples, sample => sample.Running && sample.CountdownRemaining is null);
        Assert.All(samples.Where(sample => sample.CountdownRemaining is not null && sample.CapturedInSequence == 0), sample => Assert.Equal(0, sample.TargetWindow));
    }

    [Fact]
    public async Task One_flash_is_counted_for_each_Capture_when_its_frame_is_taken_before_it_is_committed()
    {
        using var rig = await OnCameraTabAsync();
        rig.Store.CommitGate = new TaskCompletionSource();
        rig.Guest("TakePhotographs").ShouldBeAccepted();

        Assert.True(rig.AdvanceUntil(() => rig.Photographs.Sequence.FlashSequence == 1, TimeSpan.FromSeconds(20), Tick));
        Assert.Null(rig.Photographs.Windows[0].CaptureId);
        Assert.Null(rig.Photographs.Sequence.CountdownRemaining);

        rig.Store.CommitGate.SetResult();
        rig.Store.CommitGate = null;
        rig.Settle();
        Assert.NotNull(rig.Photographs.Windows[0].CaptureId);
        Assert.Equal(1, rig.Photographs.Sequence.FlashSequence);

        var flashes = Sample(rig, () => !rig.Photographs.Sequence.Running).Select(sample => sample.FlashSequence).ToArray();
        Assert.Equal(flashes.Order(), flashes);
        Assert.Equal(4, rig.Photographs.Sequence.FlashSequence);
    }

    [Fact]
    public async Task The_sequence_fills_every_empty_window_in_order_pausing_between_and_ends_on_Arrange()
    {
        using var rig = await OnCameraTabAsync();
        rig.Guest("TakePhotographs").ShouldBeAccepted();
        Assert.Equal(new CaptureSequenceView(true, 0, 5, 0, 0, 4, false, true), rig.Photographs.Sequence);
        Assert.False(rig.Photographs.CanTakePhotographs);

        var samples = Sample(rig, () => !rig.Photographs.Sequence.Running);

        Assert.Contains(samples, sample => sample.Pausing && sample.CountdownRemaining is null);
        Assert.Equal<int>([0, 1, 2, 3], samples.Where(sample => sample.TargetWindow is not null).Select(sample => sample.TargetWindow!.Value).Distinct());
        Assert.Equal<int>([0, 1, 2, 3, 4], samples.Select(sample => sample.CapturedInSequence).Distinct());
        var view = rig.Photographs;
        Assert.Equal(PhotographsTabName.Arrange, view.Tab);
        Assert.All(view.Windows, window => Assert.StartsWith("https://media.fotohavn/events/" + ActiveRig.EventId + "/guest-cycles/", window.ImageUrl, StringComparison.Ordinal));
        Assert.True(view.CanContinue);
        Assert.True(view.CanReview);
        Assert.False(view.CanTakePhotographs);
        Assert.Equal(new CaptureSequenceView(false, null, null, 4, 0, 0, false, true), view.Sequence);
        Assert.Equal(4, view.Revision);
    }

    [Fact]
    public async Task The_pause_after_a_flash_lasts_800_ms()
    {
        using var rig = await OnCameraTabAsync();
        rig.Guest("TakePhotographs").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Photographs.Sequence.Pausing, TimeSpan.FromSeconds(20), Tick));

        rig.Advance(TimeSpan.FromMilliseconds(600));
        Assert.True(rig.Photographs.Sequence.Pausing);
        rig.Advance(TimeSpan.FromMilliseconds(300));
        Assert.False(rig.Photographs.Sequence.Pausing);
        Assert.Equal(5, rig.Photographs.Sequence.CountdownRemaining);
    }

    [Fact]
    public async Task Each_Capture_is_committed_with_the_next_sequence_number_and_kept()
    {
        using var rig = await OnCameraTabAsync();
        rig.TakeAll();

        var detail = await rig.OnlyCycleAsync();
        Assert.Equal([1, 2, 3, 4], detail.Captures.Select(capture => capture.Sequence));
        Assert.All(detail.Captures, capture =>
        {
            Assert.Equal(CaptureState.Kept, capture.State);
            Assert.True(capture.Mirrored);
            Assert.Equal((1280, 720), (capture.Width, capture.Height));
            Assert.Equal(0xFF, rig.Memory.GetCaptureFile(capture.Id)!.Value.Span[0]);
        });
        Assert.Equal(detail.Captures.Select(capture => capture.Id), rig.Photographs.Windows.Select(window => window.CaptureId));
        Assert.Contains($"capture-1.jpg?v={detail.Captures[0].Sha256[..8]}", rig.Photographs.Windows[0].ImageUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Capture_takes_the_first_frame_after_the_countdowns_end_and_is_mirrored_when_the_Event_says_so()
    {
        RecordingCameraAdapter? recorder = null;
        using var rig = await OnCameraTabAsync(new GuestRigOptions { CameraDecorator = fake => recorder = new RecordingCameraAdapter(fake) });
        rig.TakeAll();

        Assert.Equal(4, recorder!.Captures.Count);
        Assert.All(recorder.Captures, capture => Assert.True(capture.Timestamp >= capture.NotBefore));
        Assert.Equal(recorder.Captures.Select(capture => capture.NotBefore).Order(), recorder.Captures.Select(capture => capture.NotBefore));
        Assert.All(recorder.Captures, capture => Assert.NotEqual(capture.FirstPixel, capture.LastPixelOfRow));

        // The Event mirrors: the encoder was handed each frame with its row reversed.
        Assert.Equal(recorder.Captures.Select(capture => capture.LastPixelOfRow), rig.Encoder.Frames.Select(frame => frame.FirstPixel));
    }

    [Fact]
    public async Task The_sequence_carries_the_Events_Guest_sounds_setting_whether_or_not_a_sequence_runs()
    {
        using var on = await OnCameraTabAsync();
        using var off = await OnCameraTabAsync(new GuestRigOptions { Tweak = record => record with { GuestSoundsEnabled = false } });

        Assert.True(on.Photographs.Sequence.SoundsOn);
        Assert.False(off.Photographs.Sequence.SoundsOn);
        off.Guest("TakePhotographs").ShouldBeAccepted();
        Assert.False(off.Photographs.Sequence.SoundsOn);
        Assert.True(off.Photographs.Sequence.Running);
    }

    [Fact]
    public async Task A_Capture_is_not_mirrored_when_the_Event_does_not_mirror()
    {
        RecordingCameraAdapter? recorder = null;
        using var rig = await GuestCycleRig.CreateAsync(new GuestRigOptions
        {
            Tweak = record => record with { Mirror = false },
            CameraDecorator = fake => recorder = new RecordingCameraAdapter(fake),
        });
        rig.Begin();
        rig.ToPhotographs();
        Assert.False(rig.Photographs.Mirrored);
        rig.TakeAll();

        Assert.Equal(recorder!.Captures.Select(capture => capture.FirstPixel), rig.Encoder.Frames.Select(frame => frame.FirstPixel));
        var detail = await rig.OnlyCycleAsync();
        Assert.All(detail.Captures, capture => Assert.False(capture.Mirrored));
    }

    [Fact]
    public async Task The_countdown_length_is_the_Events()
    {
        using var rig = await OnCameraTabAsync(new GuestRigOptions { Tweak = record => record with { CountdownSeconds = 3 } });

        rig.Guest("TakePhotographs").ShouldBeAccepted();

        Assert.Equal(3, rig.Photographs.Sequence.CountdownRemaining);
    }

    [Fact]
    public async Task The_preview_goes_to_the_Guest_only_on_the_Camera_tab()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        Assert.Equal(PreviewTarget.Guest, rig.Active.Camera.OpenSessionCount > 0 ? PreviewOf(rig) : PreviewTarget.None);

        rig.Begin();
        Assert.Equal(PreviewTarget.None, PreviewOf(rig));
        rig.ToPhotographs();
        Assert.Equal(PreviewTarget.Guest, PreviewOf(rig));
        rig.TakeAll();
        Assert.Equal(PreviewTarget.None, PreviewOf(rig));
        rig.Guest("ShowCameraTab").ShouldBeAccepted();
        Assert.Equal(PreviewTarget.Guest, PreviewOf(rig));
        rig.Guest("ShowArrangeTab").ShouldBeAccepted();
        rig.ToFilters();
        Assert.Equal(PreviewTarget.None, PreviewOf(rig));
    }

    private static PreviewTarget PreviewOf(GuestCycleRig rig) => rig.Diagnostics().Camera.PreviewTarget;

    [Fact]
    public async Task TAKE_PHOTOGRAPHS_is_refused_while_a_sequence_runs_on_the_Arrange_tab_and_when_no_window_is_empty()
    {
        using var rig = await OnCameraTabAsync();
        rig.Guest("TakePhotographs").ShouldBeAccepted();
        rig.Guest("TakePhotographs").ShouldBeRejected();
        rig.Guest("ShowArrangeTab").ShouldBeRejected();

        Assert.True(rig.AdvanceUntil(() => !rig.Photographs.Sequence.Running, TimeSpan.FromMinutes(2), Tick));
        rig.Guest("TakePhotographs").ShouldBeRejected();
    }

    [Fact]
    public async Task A_running_capture_sequence_cannot_be_stopped_STOP_is_an_unknown_command()
    {
        using var rig = await OnCameraTabAsync();
        rig.Guest("TakePhotographs").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Photographs.Windows[0].CaptureId is not null && rig.Photographs.Sequence.CountdownRemaining == 4, TimeSpan.FromSeconds(30), Tick));

        rig.Guest("StopCapture").ShouldBeRejected(RejectReason.UnknownCommand);

        Assert.True(rig.Photographs.Sequence.Running);
        Assert.True(rig.CycleDiagnostics().Timer.Paused);
        Assert.True(rig.AdvanceUntil(() => !rig.Photographs.Sequence.Running, TimeSpan.FromMinutes(2), Tick));
        Assert.All(rig.Photographs.Windows, window => Assert.NotNull(window.CaptureId));
        Assert.Equal(4, rig.Photographs.Sequence.FlashSequence);
    }

    [Fact]
    public async Task A_Retake_counts_down_for_one_window_and_stops_at_a_candidate_the_original_staying()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        var original = rig.Photographs.Windows[2].CaptureId;
        var revision = rig.Photographs.Revision;

        rig.Guest("BeginRetake", """{"index":2}""").ShouldBeAccepted();

        var view = rig.Photographs;
        Assert.Equal(PhotographsTabName.Camera, view.Tab);
        Assert.Equal(new PhotographSelectionView(2, PhotographSelectionMode.Retaking), view.Selection);
        Assert.Equal(new CaptureSequenceView(true, 2, 5, 4, 0, 1, false, true), view.Sequence);
        Assert.Equal(600.0 / 450.0, view.CropAspect, 6);
        Assert.False(view.CanReview);

        Assert.True(rig.AdvanceUntil(() => rig.Photographs.RetakeCandidate is not null, TimeSpan.FromSeconds(30), Tick));
        view = rig.Photographs;
        Assert.Equal(PhotographsTabName.Arrange, view.Tab);
        Assert.False(view.Sequence.Running);
        Assert.Equal(5, view.Sequence.FlashSequence);
        Assert.Equal(original, view.Windows[2].CaptureId);
        Assert.Equal(2, view.RetakeCandidate!.Index);
        Assert.NotEqual(view.Windows[2].ImageUrl, view.RetakeCandidate.ImageUrl);
        Assert.Equal(revision, view.Revision);
        Assert.Equal(new PhotographSelectionView(2, PhotographSelectionMode.Retaking), view.Selection);
        Assert.True(view.CanReview);
        Assert.False(view.CanContinue);
        Assert.Equal(PreviewTarget.None, PreviewOf(rig));

        var detail = await rig.OnlyCycleAsync();
        Assert.Equal(5, detail.Captures.Count(capture => capture.State == CaptureState.Kept));
    }

    [Fact]
    public async Task ACCEPT_gives_the_window_to_the_candidate_and_discards_the_original_in_the_store()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        var original = rig.Photographs.Windows[2].CaptureId;
        await RetakeToCandidateAsync(rig, 2);

        rig.Guest("AcceptRetake").ShouldBeAccepted();

        var view = rig.Photographs;
        Assert.Null(view.RetakeCandidate);
        Assert.NotEqual(original, view.Windows[2].CaptureId);
        Assert.Equal(new PhotographSelectionView(2, PhotographSelectionMode.Selected), view.Selection);
        Assert.Equal(PhotographsTabName.Arrange, view.Tab);
        Assert.True(view.CanContinue);
        var detail = await rig.OnlyCycleAsync();
        Assert.DoesNotContain(detail.Captures, capture => capture.Id == original);
        Assert.Equal(4, detail.Captures.Count);
        Assert.Equal(view.Windows[2].CaptureId, detail.Captures[^1].Id);
    }

    [Fact]
    public async Task TRY_AGAIN_discards_the_candidate_and_counts_down_for_the_same_window()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        await RetakeToCandidateAsync(rig, 1);
        var firstCandidate = rig.Photographs.RetakeCandidate!.ImageUrl;

        rig.Guest("TryAgain").ShouldBeAccepted();

        Assert.Null(rig.Photographs.RetakeCandidate);
        Assert.Equal(new CaptureSequenceView(true, 1, 5, 5, 0, 1, false, true), rig.Photographs.Sequence);
        Assert.Equal(PhotographsTabName.Camera, rig.Photographs.Tab);
        Assert.True(rig.AdvanceUntil(() => rig.Photographs.RetakeCandidate is not null, TimeSpan.FromSeconds(30), Tick));
        Assert.NotEqual(firstCandidate, rig.Photographs.RetakeCandidate!.ImageUrl);
        var detail = await rig.OnlyCycleAsync();
        Assert.Equal<int>([1, 2, 3, 4, 6], detail.Captures.Select(capture => capture.Sequence));
    }

    [Fact]
    public async Task KEEP_ORIGINAL_with_a_candidate_discards_it_and_keeps_the_original()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        var original = rig.Photographs.Windows[0].CaptureId;
        await RetakeToCandidateAsync(rig, 0);

        rig.Guest("KeepOriginal").ShouldBeAccepted();

        Assert.Null(rig.Photographs.RetakeCandidate);
        Assert.Equal(original, rig.Photographs.Windows[0].CaptureId);
        Assert.Equal(PhotographsTabName.Arrange, rig.Photographs.Tab);
        Assert.Equal<int>([1, 2, 3, 4], (await rig.OnlyCycleAsync()).Captures.Select(capture => capture.Sequence));
    }

    [Fact]
    public async Task KEEP_ORIGINAL_during_the_countdown_cancels_the_Retake()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.Guest("BeginRetake", """{"index":3}""").ShouldBeAccepted();

        rig.Guest("KeepOriginal").ShouldBeAccepted();

        Assert.False(rig.Photographs.Sequence.Running);
        Assert.Equal(PhotographsTabName.Arrange, rig.Photographs.Tab);
        Assert.Equal(PhotographSelectionMode.Selected, rig.Photographs.Selection.Mode);
        Assert.Equal(4, (await rig.OnlyCycleAsync()).Captures.Count);
    }

    [Fact]
    public async Task KEEP_ORIGINAL_waits_once_the_Retakes_frame_is_taken_and_the_candidate_then_arrives()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        rig.Store.CommitGate = new TaskCompletionSource();
        rig.Guest("BeginRetake", """{"index":3}""").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Photographs.Sequence.FlashSequence == 5, TimeSpan.FromSeconds(20), Tick));

        rig.Guest("KeepOriginal").ShouldBeRejected();

        rig.Store.CommitGate.SetResult();
        rig.Store.CommitGate = null;
        Assert.True(rig.AdvanceUntil(() => rig.Photographs.RetakeCandidate is not null, TimeSpan.FromSeconds(5), Tick));
        rig.Guest("KeepOriginal").ShouldBeAccepted();
        Assert.Null(rig.Photographs.RetakeCandidate);
    }

    [Fact]
    public async Task KEEP_ORIGINAL_is_refused_while_a_whole_sequence_runs_and_in_flight_Retake_commands_wait()
    {
        using var rig = await OnCameraTabAsync();
        rig.Guest("TakePhotographs").ShouldBeAccepted();

        rig.Guest("KeepOriginal").ShouldBeRejected();
        rig.Guest("TryAgain").ShouldBeRejected();
        rig.Guest("AcceptRetake").ShouldBeRejected();
    }

    [Fact]
    public async Task A_Retake_is_refused_for_an_empty_window_an_out_of_range_one_and_while_something_runs()
    {
        using var rig = await OnCameraTabAsync();
        rig.Guest("BeginRetake", """{"index":0}""").ShouldBeRejected();
        rig.Guest("BeginRetake", """{"index":9}""").ShouldBeRejected(RejectReason.InvalidPayload);
        rig.Guest("BeginRetake", """{"index":-1}""").ShouldBeRejected(RejectReason.InvalidPayload);

        rig.Guest("TakePhotographs").ShouldBeAccepted();
        rig.Guest("BeginRetake", """{"index":0}""").ShouldBeRejected();
    }

    [Fact]
    public async Task Neither_going_on_nor_going_back_interrupts_a_running_sequence_or_Retake()
    {
        using var rig = await OnCameraTabAsync();
        rig.Guest("TakePhotographs").ShouldBeAccepted();

        rig.Guest("GoToStep", """{"step":"template"}""").ShouldBeRejected();
        rig.Guest("GoToStep", """{"step":"filters"}""").ShouldBeRejected();
        Assert.True(rig.Photographs.Sequence.Running);
        Assert.True(rig.AdvanceUntil(() => !rig.Photographs.Sequence.Running, TimeSpan.FromMinutes(2), Tick));

        rig.Guest("BeginRetake", """{"index":1}""").ShouldBeAccepted();
        rig.Guest("GoToStep", """{"step":"filters"}""").ShouldBeRejected();
        Assert.True(rig.Photographs.Sequence.Running);
        Assert.Equal(GuestSurface.Photographs, rig.Surface);
    }

    private static Task RetakeToCandidateAsync(GuestCycleRig rig, int index)
    {
        rig.Guest("BeginRetake", $$"""{"index":{{index}}}""").ShouldBeAccepted();
        Assert.True(rig.AdvanceUntil(() => rig.Photographs.RetakeCandidate is not null, TimeSpan.FromSeconds(30), Tick));
        return Task.CompletedTask;
    }
}
