using FotoHavn.Core.Storage;

namespace FotoHavn.Core.Tests.Storage;

public sealed class StorageValidationTests
{
    private static readonly string Id = StorageIds.NewId();

    [Fact]
    public void A_good_event_passes()
    {
        StorageValidation.ValidateEvent(StorageSamples.Event(Id));
    }

    [Fact]
    public void An_event_with_no_timers_or_offers_passes()
    {
        StorageValidation.ValidateEvent(StorageSamples.Event(Id) with { OfferedLookIds = [], OfferedTemplateIds = [], PhaseTimers = [] });
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void The_allowed_countdowns_pass(int seconds)
    {
        StorageValidation.ValidateEvent(StorageSamples.Event(Id) with { CountdownSeconds = seconds });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-5)]
    [InlineData(15)]
    public void Other_countdowns_are_refused(int seconds)
    {
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidateEvent(StorageSamples.Event(Id) with { CountdownSeconds = seconds }));
    }

    [Fact]
    public void Any_printer_but_none_is_refused()
    {
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidateEvent(StorageSamples.Event(Id) with { Printer = "dnp-ds620" }));
    }

    [Fact]
    public void An_event_with_a_bad_id_or_empty_required_text_is_refused()
    {
        var good = StorageSamples.Event(Id);

        Assert.Throws<ArgumentException>(() => StorageValidation.ValidateEvent(good with { Id = "event-1" }));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidateEvent(good with { Name = " " }));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidateEvent(good with { DefaultTemplateId = string.Empty }));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidateEvent(good with { DefaultLookId = string.Empty }));
        Assert.Throws<ArgumentNullException>(() => StorageValidation.ValidateEvent(null!));
    }

    [Fact]
    public void Offered_lists_must_be_non_empty_and_distinct()
    {
        var good = StorageSamples.Event(Id);

        Assert.Throws<ArgumentException>(() => StorageValidation.ValidateEvent(good with { OfferedTemplateIds = ["a", "a"] }));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidateEvent(good with { OfferedLookIds = ["a", " "] }));
        Assert.Throws<ArgumentNullException>(() => StorageValidation.ValidateEvent(good with { OfferedTemplateIds = null! }));
        Assert.Throws<ArgumentNullException>(() => StorageValidation.ValidateEvent(good with { PhaseTimers = null! }));
    }

    [Fact]
    public void Phase_timers_must_be_non_negative_known_and_unique()
    {
        var good = StorageSamples.Event(Id);

        Assert.Throws<ArgumentException>(() => StorageValidation.ValidateEvent(good with { PhaseTimers = [new(PhaseTimerPhase.Filters, -1)] }));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidateEvent(good with { PhaseTimers = [new(PhaseTimerPhase.Filters, 10), new(PhaseTimerPhase.Filters, 20)] }));
        Assert.Throws<ArgumentOutOfRangeException>(() => StorageValidation.ValidateEvent(good with { PhaseTimers = [new((PhaseTimerPhase)99, 10)] }));
        StorageValidation.ValidateEvent(good with { PhaseTimers = [new(PhaseTimerPhase.Filters, 0)] });
    }

    [Fact]
    public void A_pin_needs_an_event_id_and_text()
    {
        StorageValidation.ValidatePin(Id, "4826");
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidatePin("x", "4826"));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidatePin(Id, string.Empty));
    }

    [Fact]
    public void Completed_is_not_an_end_reason_for_end_guest_cycle()
    {
        foreach (var reason in new[] { GuestCycleEndReason.StartedOver, GuestCycleEndReason.OperatorExit, GuestCycleEndReason.AppClosed, GuestCycleEndReason.Unrecoverable })
        {
            StorageValidation.ValidateEndReason(reason);
        }

        Assert.Throws<ArgumentException>(() => StorageValidation.ValidateEndReason(GuestCycleEndReason.Completed));
        Assert.Throws<ArgumentOutOfRangeException>(() => StorageValidation.ValidateEndReason((GuestCycleEndReason)99));
    }

    [Fact]
    public void A_capture_commit_needs_a_sequence_of_one_or_more_bytes_and_a_size()
    {
        var good = StorageSamples.CaptureCommit(Id, 1);

        StorageValidation.ValidateCaptureCommit(good);
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidateCaptureCommit(good with { Sequence = 0 }));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidateCaptureCommit(good with { Sequence = -3 }));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidateCaptureCommit(good with { Jpeg = ReadOnlyMemory<byte>.Empty }));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidateCaptureCommit(good with { Width = 0 }));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidateCaptureCommit(good with { Height = 0 }));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidateCaptureCommit(good with { GuestCycleId = "x" }));
        Assert.Throws<ArgumentNullException>(() => StorageValidation.ValidateCaptureCommit(null!));
    }

    [Fact]
    public void An_arrangement_is_one_to_four_distinct_capture_ids()
    {
        var ids = Enumerable.Range(0, 5).Select(_ => StorageIds.NewId()).ToList();
        var good = StorageSamples.StripCommit(Id, ids.Take(4).ToList());

        StorageValidation.ValidatePhotoStripCommit(good);
        StorageValidation.ValidatePhotoStripCommit(good with { Arrangement = [ids[0]] });
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidatePhotoStripCommit(good with { Arrangement = [] }));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidatePhotoStripCommit(good with { Arrangement = ids }));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidatePhotoStripCommit(good with { Arrangement = [ids[0], ids[0]] }));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidatePhotoStripCommit(good with { Arrangement = [ids[0], "capture-2"] }));
        Assert.Throws<ArgumentNullException>(() => StorageValidation.ValidatePhotoStripCommit(good with { Arrangement = null! }));
    }

    [Fact]
    public void A_photo_strip_commit_needs_text_bytes_and_a_size()
    {
        var good = StorageSamples.StripCommit(Id, [StorageIds.NewId()]);

        Assert.Throws<ArgumentException>(() => StorageValidation.ValidatePhotoStripCommit(good with { TemplateId = " " }));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidatePhotoStripCommit(good with { LookId = string.Empty }));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidatePhotoStripCommit(good with { Png = ReadOnlyMemory<byte>.Empty }));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidatePhotoStripCommit(good with { Width = 0 }));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidatePhotoStripCommit(good with { Height = -1 }));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidatePhotoStripCommit(good with { GuestCycleId = "x" }));
        Assert.Throws<ArgumentNullException>(() => StorageValidation.ValidatePhotoStripCommit(null!));
    }

    [Fact]
    public void Paging_arguments_are_checked()
    {
        var cursor = GuestCycleCursor.Encode(DateTimeOffset.UnixEpoch, StorageIds.NewId());

        StorageValidation.ValidatePaging(Id, GuestCycleFilter.All, null, 1);
        StorageValidation.ValidatePaging(Id, GuestCycleFilter.CompletedOnly, cursor, StorageValidation.MaximumPageSize);
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidatePaging("x", GuestCycleFilter.All, null, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => StorageValidation.ValidatePaging(Id, (GuestCycleFilter)9, null, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => StorageValidation.ValidatePaging(Id, GuestCycleFilter.All, null, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => StorageValidation.ValidatePaging(Id, GuestCycleFilter.All, null, 201));
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidatePaging(Id, GuestCycleFilter.All, "nonsense", 10));
    }

    [Fact]
    public void A_thumbnail_request_needs_a_source_id_and_a_width_in_range()
    {
        StorageValidation.ValidateThumbnailRequest(Id, 64);
        StorageValidation.ValidateThumbnailRequest(Id, 2048);
        Assert.Throws<ArgumentException>(() => StorageValidation.ValidateThumbnailRequest("x", 320));
        Assert.Throws<ArgumentOutOfRangeException>(() => StorageValidation.ValidateThumbnailRequest(Id, 63));
        Assert.Throws<ArgumentOutOfRangeException>(() => StorageValidation.ValidateThumbnailRequest(Id, 2049));
    }
}
