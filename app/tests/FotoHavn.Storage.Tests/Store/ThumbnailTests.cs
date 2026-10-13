using FotoHavn.Core.Storage;
using FotoHavn.Storage.Tests.Support;
using FotoHavn.Storage.Thumbnails;
using SkiaSharp;

namespace FotoHavn.Storage.Tests.Store;

public sealed class ThumbnailTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(StoreHarness Harness, EventRecord Event, GuestCycleRecord Cycle, CaptureRecord Capture)> SeededAsync()
    {
        var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        var cycle = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
        var capture = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 1, Sample.Jpeg(80, 60)), Ct);
        return (harness, record, cycle, capture);
    }

    private static SKImageInfo Info(byte[] bytes)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(bytes));
        return codec.Info;
    }

    private static SKEncodedImageFormat Format(byte[] bytes)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(bytes));
        return codec.EncodedFormat;
    }

    [Fact]
    public async Task A_Capture_thumbnail_is_named_by_source_sha_and_width_and_is_a_resized_jpeg()
    {
        var (harness, _, _, capture) = await SeededAsync();
        await using var lifetime = harness;

        var path = await harness.Store.GetThumbnailPathAsync(capture.Id, 64, Ct);

        Assert.Equal($"Cache/thumbnails/{capture.Id}-{capture.Sha256[..8]}-64.jpg", path);
        Assert.DoesNotContain('\\', path);
        var bytes = await File.ReadAllBytesAsync(harness.Absolute(path), Ct);
        Assert.Equal(SKEncodedImageFormat.Jpeg, Format(bytes));
        Assert.Equal((64, 48), (Info(bytes).Width, Info(bytes).Height));
    }

    [Fact]
    public async Task A_Photo_Strip_thumbnail_is_requested_by_the_Guest_Cycle_id_and_keeps_the_aspect_ratio()
    {
        var (harness, _, cycle, capture) = await SeededAsync();
        await using var lifetime = harness;
        var strip = await harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [capture.Id], Sample.Png(60, 180)), Ct);

        var path = await harness.Store.GetThumbnailPathAsync(cycle.Id, 120, Ct);

        Assert.Equal($"Cache/thumbnails/{cycle.Id}-{strip.Sha256[..8]}-120.jpg", path);
        var bytes = await File.ReadAllBytesAsync(harness.Absolute(path), Ct);
        Assert.Equal((120, 360), (Info(bytes).Width, Info(bytes).Height));
    }

    [Fact]
    public async Task A_thumbnail_wider_than_its_source_is_still_the_requested_width_and_each_width_is_its_own_file()
    {
        var (harness, _, _, capture) = await SeededAsync();
        await using var lifetime = harness;

        var small = await harness.Store.GetThumbnailPathAsync(capture.Id, 64, Ct);
        var wide = await harness.Store.GetThumbnailPathAsync(capture.Id, 160, Ct);

        Assert.NotEqual(small, wide);
        Assert.Equal((160, 120), (Info(await File.ReadAllBytesAsync(harness.Absolute(wide), Ct)).Width, Info(await File.ReadAllBytesAsync(harness.Absolute(wide), Ct)).Height));
        Assert.Equal(2, Directory.GetFiles(Path.Combine(harness.DataRoot, "Cache", "thumbnails")).Length);
    }

    [Fact]
    public async Task A_second_request_returns_the_existing_file_without_writing_it_again()
    {
        var (harness, _, _, capture) = await SeededAsync();
        await using var lifetime = harness;
        var path = await harness.Store.GetThumbnailPathAsync(capture.Id, 64, Ct);
        var stamp = File.GetLastWriteTimeUtc(harness.Absolute(path));
        var calls = harness.Fs.Calls.Count;

        var again = await harness.Store.GetThumbnailPathAsync(capture.Id, 64, Ct);

        Assert.Equal(path, again);
        Assert.Equal(calls, harness.Fs.Calls.Count);
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(harness.Absolute(path)));
    }

    [Fact]
    public async Task A_thumbnail_is_staged_then_renamed_and_never_edited_in_place()
    {
        var (harness, _, _, capture) = await SeededAsync();
        await using var lifetime = harness;
        var before = harness.Fs.Calls.Count;

        var path = await harness.Store.GetThumbnailPathAsync(capture.Id, 64, Ct);

        var calls = harness.Fs.Calls.Skip(before).ToList();
        var final = harness.Absolute(path);
        Assert.Equal(["CreateDirectory", "WriteAllBytesAndFlush", "Move"], calls.Select(call => call.Operation));
        Assert.Contains(Path.Combine("thumbnails", ".staging"), calls[1].Path, StringComparison.Ordinal);
        Assert.EndsWith(".tmp", calls[1].Path, StringComparison.Ordinal);
        Assert.Equal(calls[1].Path, calls[2].Path);
        Assert.DoesNotContain(calls, call => call.Path == final);
        Assert.Empty(Directory.GetFiles(Path.Combine(harness.DataRoot, "Cache", "thumbnails", ".staging")));
    }

    [Fact]
    public async Task A_deleted_cache_is_regenerated_on_the_next_request()
    {
        var (harness, _, _, capture) = await SeededAsync();
        await using var lifetime = harness;
        var path = await harness.Store.GetThumbnailPathAsync(capture.Id, 96, Ct);
        var original = await File.ReadAllBytesAsync(harness.Absolute(path), Ct);
        Directory.Delete(Path.Combine(harness.DataRoot, "Cache"), recursive: true);

        var again = await harness.Store.GetThumbnailPathAsync(capture.Id, 96, Ct);

        Assert.Equal(path, again);
        Assert.Equal(original, await File.ReadAllBytesAsync(harness.Absolute(again), Ct));
    }

    [Fact]
    public async Task A_missing_discarded_or_deleting_source_is_NotFound()
    {
        var (harness, record, cycle, capture) = await SeededAsync();
        await using var lifetime = harness;
        var discarded = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 2), Ct);
        await harness.Store.SetCaptureStateAsync(discarded.Id, CaptureState.Discarded, Ct);
        var noStrip = await harness.Store.StartGuestCycleAsync(record.Id, Ct);

        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.GetThumbnailPathAsync(StorageIds.NewId(), 64, Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.GetThumbnailPathAsync(discarded.Id, 64, Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.GetThumbnailPathAsync(noStrip.Id, 64, Ct))).Kind);
        await harness.Store.MarkGuestCycleDeletingAsync(cycle.Id, Ct);
        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.GetThumbnailPathAsync(capture.Id, 64, Ct))).Kind);
        await harness.Store.DrainAsync(Ct);
    }

    [Fact]
    public async Task A_source_whose_file_is_gone_is_NotFound()
    {
        var (harness, record, cycle, capture) = await SeededAsync();
        await using var lifetime = harness;
        File.Delete(Path.Combine(harness.CycleFolder(record.Id, cycle.Id), capture.FileName));

        var failure = await Assert.ThrowsAsync<StorageException>(() => harness.Store.GetThumbnailPathAsync(capture.Id, 64, Ct));

        Assert.Equal(StorageErrorKind.NotFound, failure.Kind);
    }

    [Fact]
    public async Task Rows_whose_files_are_gone_still_list_and_read_and_only_their_thumbnails_are_NotFound()
    {
        var (harness, record, cycle, capture) = await SeededAsync();
        await using var lifetime = harness;
        _ = await harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [capture.Id], Sample.Png(60, 180)), Ct);
        var folder = harness.CycleFolder(record.Id, cycle.Id);
        File.Delete(Path.Combine(folder, capture.FileName));
        File.Delete(Path.Combine(folder, "photo-strip.png"));

        var page = await harness.Store.ListGuestCyclesAsync(record.Id, GuestCycleFilter.CompletedOnly, null, 10, Ct);
        var detail = await harness.Store.GetGuestCycleAsync(cycle.Id, Ct);

        var item = Assert.Single(page.Items);
        Assert.Equal(1, item.KeptCaptureCount);
        Assert.NotNull(item.PhotoStrip);
        Assert.Equal(GuestCycleEndReason.Completed, detail!.Cycle.EndReason);
        Assert.Equal(capture.Id, Assert.Single(detail.Captures).Id);
        Assert.NotNull(detail.PhotoStrip);
        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.GetThumbnailPathAsync(capture.Id, 64, Ct))).Kind);
        Assert.Equal(StorageErrorKind.NotFound, (await Assert.ThrowsAsync<StorageException>(() => harness.Store.GetThumbnailPathAsync(cycle.Id, 64, Ct))).Kind);
    }

    [Fact]
    public async Task A_source_that_is_not_an_image_fails_and_leaves_no_thumbnail()
    {
        var harness = await StoreHarness.OpenedAsync();
        await using var lifetime = harness;
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        var cycle = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
        var capture = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 1, [1, 2, 3, 4]), Ct);

        var failure = await Assert.ThrowsAsync<StorageException>(() => harness.Store.GetThumbnailPathAsync(capture.Id, 64, Ct));

        Assert.Equal(StorageErrorKind.Unknown, failure.Kind);
        Assert.Empty(StoreHarness.Files(Path.Combine(harness.DataRoot, "Cache")));
    }

    [Fact]
    public async Task The_request_is_validated_before_any_io()
    {
        var (harness, _, _, capture) = await SeededAsync();
        await using var lifetime = harness;

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => harness.Store.GetThumbnailPathAsync(capture.Id, 63, Ct));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => harness.Store.GetThumbnailPathAsync(capture.Id, 2049, Ct));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.GetThumbnailPathAsync("../x", 64, Ct));
        _ = await harness.Store.GetThumbnailPathAsync(capture.Id, 2048, Ct);
    }

    [Fact]
    public void The_generator_encodes_a_resized_JPEG_from_PNG_or_JPEG()
    {
        var fromPng = ThumbnailGenerator.CreateJpeg(Sample.Png(50, 100), 100);
        var fromJpeg = ThumbnailGenerator.CreateJpeg(Sample.Jpeg(300, 100), 150);

        Assert.Equal(SKEncodedImageFormat.Jpeg, Format(fromPng));
        Assert.Equal((100, 200), (Info(fromPng).Width, Info(fromPng).Height));
        Assert.Equal((150, 50), (Info(fromJpeg).Width, Info(fromJpeg).Height));
    }

    [Fact]
    public void The_generator_keeps_at_least_one_pixel_of_height_and_flattens_transparency_to_white()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(4000, 1, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Transparent);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);

        var jpeg = ThumbnailGenerator.CreateJpeg(png.ToArray(), 64);

        Assert.Equal(1, Info(jpeg).Height);
        using var decoded = SKBitmap.Decode(jpeg);
        Assert.True(decoded.GetPixel(10, 0).Red > 240);
    }

    [Fact]
    public void The_generator_refuses_bytes_that_are_not_an_image_and_a_width_below_one()
    {
        var failure = Assert.Throws<StorageException>(() => ThumbnailGenerator.CreateJpeg([1, 2, 3], 64));

        Assert.Equal(StorageErrorKind.Unknown, failure.Kind);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ThumbnailGenerator.CreateJpeg(Sample.Jpeg(), 0));
    }
}
