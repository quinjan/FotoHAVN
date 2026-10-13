using FotoHavn.Core.Storage;

namespace FotoHavn.Core.Tests.Storage;

public sealed class StorageConventionsTests
{
    [Fact]
    public void Phase_names_round_trip_and_match_the_schema_check()
    {
        string[] sql = ["template", "photographs_capture", "photographs_review", "filters", "keepsake", "guest_gallery"];

        var phases = Enum.GetValues<PhaseTimerPhase>();

        Assert.Equal(sql, phases.Select(StorageSqlNames.ToSql));
        Assert.Equal(phases, sql.Select(StorageSqlNames.PhaseFromSql));
        Assert.Throws<ArgumentOutOfRangeException>(() => StorageSqlNames.ToSql((PhaseTimerPhase)99));
        Assert.Throws<ArgumentException>(() => StorageSqlNames.PhaseFromSql("Template"));
    }

    [Fact]
    public void End_reason_names_round_trip_and_match_the_schema_check()
    {
        string[] sql = ["completed", "started_over", "operator_exit", "app_closed", "unrecoverable"];

        var reasons = Enum.GetValues<GuestCycleEndReason>();

        Assert.Equal(sql, reasons.Select(StorageSqlNames.ToSql));
        Assert.Equal(reasons, sql.Select(StorageSqlNames.EndReasonFromSql));
        Assert.Throws<ArgumentOutOfRangeException>(() => StorageSqlNames.ToSql((GuestCycleEndReason)99));
        Assert.Throws<ArgumentException>(() => StorageSqlNames.EndReasonFromSql("done"));
    }

    [Fact]
    public void Capture_state_names_round_trip_and_match_the_schema_check()
    {
        Assert.Equal(["kept", "discarded"], Enum.GetValues<CaptureState>().Select(StorageSqlNames.ToSql));
        Assert.Equal(CaptureState.Kept, StorageSqlNames.CaptureStateFromSql("kept"));
        Assert.Equal(CaptureState.Discarded, StorageSqlNames.CaptureStateFromSql("discarded"));
        Assert.Throws<ArgumentOutOfRangeException>(() => StorageSqlNames.ToSql((CaptureState)99));
        Assert.Throws<ArgumentException>(() => StorageSqlNames.CaptureStateFromSql("lost"));
    }

    [Fact]
    public void Timestamps_are_fixed_width_utc_text_that_sorts_by_time()
    {
        var early = new DateTimeOffset(2026, 10, 13, 9, 5, 7, 120, TimeSpan.FromHours(8));
        var late = early.AddTicks(1);

        var text = StorageTimestamp.Format(early);

        Assert.Equal("2026-10-13T01:05:07.1200000Z", text);
        Assert.Equal(early, StorageTimestamp.Parse(text));
        Assert.Equal(TimeSpan.Zero, StorageTimestamp.Parse(text).Offset);
        Assert.True(string.CompareOrdinal(text, StorageTimestamp.Format(late)) < 0);
        Assert.Throws<FormatException>(() => StorageTimestamp.Parse("2026-10-13"));
    }

    [Fact]
    public void Dates_are_written_as_year_month_day()
    {
        var date = new DateOnly(2026, 1, 5);

        Assert.Equal("2026-01-05", StorageTimestamp.FormatDate(date));
        Assert.Equal(date, StorageTimestamp.ParseDate("2026-01-05"));
        Assert.Throws<FormatException>(() => StorageTimestamp.ParseDate("05/01/2026"));
    }

    [Fact]
    public void Ids_are_lowercase_guids_in_d_format()
    {
        var id = StorageIds.NewId();

        Assert.True(StorageIds.IsValid(id));
        Assert.False(StorageIds.IsValid(id.ToUpperInvariant()));
        Assert.False(StorageIds.IsValid(id.Replace("-", string.Empty, StringComparison.Ordinal)));
        Assert.False(StorageIds.IsValid(@"..\..\evil"));
        Assert.False(StorageIds.IsValid("event-1"));
        Assert.False(StorageIds.IsValid(string.Empty));
        Assert.False(StorageIds.IsValid(null));
        StorageIds.ThrowIfInvalid(id, "id");
        Assert.Throws<ArgumentException>(() => StorageIds.ThrowIfInvalid("event-1", "id"));
    }

    [Fact]
    public void A_cursor_round_trips_and_is_url_safe()
    {
        var startedAt = new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
        var id = StorageIds.NewId();

        var cursor = GuestCycleCursor.Encode(startedAt, id);

        Assert.Matches("^[A-Za-z0-9_-]+$", cursor);
        Assert.True(GuestCycleCursor.TryDecode(cursor, out var decodedAt, out var decodedId));
        Assert.Equal(startedAt, decodedAt);
        Assert.Equal(id, decodedId);
        Assert.Throws<ArgumentNullException>(() => GuestCycleCursor.Encode(startedAt, null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64 !")]
    [InlineData("YWJj")]
    [InlineData("MjAyNi0xMC0xM3xub3QtYW4taWQ")]
    public void A_cursor_this_store_did_not_issue_is_refused(string? cursor)
    {
        Assert.False(GuestCycleCursor.TryDecode(cursor, out _, out var id));
        Assert.Equal(string.Empty, id);
    }

    [Fact]
    public void A_cursor_with_a_bad_timestamp_or_padding_or_length_is_refused()
    {
        var id = StorageIds.NewId();
        var badTimestamp = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("yesterday|" + id)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var valid = GuestCycleCursor.Encode(DateTimeOffset.UnixEpoch, id);

        Assert.False(GuestCycleCursor.TryDecode(badTimestamp, out _, out _));
        Assert.False(GuestCycleCursor.TryDecode(valid + "=", out _, out _));
        Assert.False(GuestCycleCursor.TryDecode(new string('A', 201), out _, out _));
    }

    [Fact]
    public void Thumbnail_names_carry_source_version_and_width()
    {
        var source = StorageIds.NewId();
        var sha = "ABCDEF0123456789" + new string('0', 48);

        var name = ThumbnailName.Format(source, sha, 320);

        Assert.Equal($"{source}-abcdef01-320.jpg", name);
        Assert.Equal($"Cache/thumbnails/{name}", ThumbnailName.RelativePath(source, sha, 320));
        Assert.Equal("abcdef01", ThumbnailName.Sha8(sha));
    }

    [Fact]
    public void Thumbnail_names_refuse_bad_input()
    {
        var source = StorageIds.NewId();
        var sha = new string('a', 64);

        Assert.Throws<ArgumentException>(() => ThumbnailName.Format("event-1", sha, 320));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThumbnailName.Format(source, sha, ThumbnailName.MinimumWidth - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThumbnailName.Format(source, sha, ThumbnailName.MaximumWidth + 1));
        Assert.Throws<ArgumentException>(() => ThumbnailName.Sha8("abc"));
        Assert.Throws<ArgumentNullException>(() => ThumbnailName.Sha8(null!));
    }

    [Fact]
    public void Thumbnail_names_parse_back()
    {
        var source = StorageIds.NewId();

        Assert.True(ThumbnailName.TryParse(ThumbnailName.Format(source, new string('b', 64), 640), out var parsedSource, out var sha8, out var width));
        Assert.Equal(source, parsedSource);
        Assert.Equal("bbbbbbbb", sha8);
        Assert.Equal(640, width);
    }

    [Theory]
    [InlineData("")]
    [InlineData("capture-1.jpg")]
    [InlineData("00000000-0000-0000-0000-000000000001-abcdef01-320.png")]
    [InlineData("00000000-0000-0000-0000-000000000001-abcdef0-320.jpg")]
    [InlineData("00000000-0000-0000-0000-000000000001-abcdef01-99999999.jpg")]
    public void Files_that_are_not_thumbnails_do_not_parse(string fileName)
    {
        Assert.False(ThumbnailName.TryParse(fileName, out var source, out var sha8, out var width));
        Assert.Equal((string.Empty, string.Empty, 0), (source, sha8, width));
    }

    [Fact]
    public void A_null_thumbnail_name_does_not_parse()
    {
        Assert.False(ThumbnailName.TryParse(null!, out _, out _, out _));
    }
}
