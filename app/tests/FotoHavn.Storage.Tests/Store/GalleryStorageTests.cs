using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using FotoHavn.Core.Catalog;
using FotoHavn.Core.Gallery;
using FotoHavn.Core.Gallery.Orchestration;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol.Presentations;
using FotoHavn.Storage.Tests.Crash;
using FotoHavn.Storage.Tests.Support;
using Microsoft.Data.Sqlite;
using SkiaSharp;

namespace FotoHavn.Storage.Tests.Store;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GalleryTimingDefinition
{
    public const string Name = "Gallery timing";
}

/// <summary>
/// The Event Gallery against the real store (M9, plan section 10, Thumbnails): a Photo Strip's list thumbnail is made on request and again when it
/// was deleted, and a 300-cycle Event pages through in pages of 40 with the thumbnails warm well inside the plan's 300 ms. The timing tests run
/// alone so other tests don't slow the disk, and report the cold and warm times as a diagnostic message.
/// </summary>
[Collection(GalleryTimingDefinition.Name)]
public sealed class GalleryStorageTests
{
    private const int Cycles = 300;
    private const int StripWidth = 600;
    private const int StripHeight = 1800;
    private const int CaptureWidth = 820;
    private const int CaptureHeight = 615;

    /// <summary>
    /// The plan's "pages load in under 300 ms" is measured with the thumbnails made (warm: a page is 40 file-exists checks and a list query). The
    /// 300 ms criterion is gated by the end-to-end `large-event` spec, which measures it through the brain. Here a warm page is measured and reported
    /// against the target, but only a generous bound is asserted, so a loaded machine can't flake the test while a real regression (seconds per page) still fails.
    /// The cold pass (every thumbnail decoded and resized) is reported, not asserted tightly: it is what an old Event pays once.
    /// </summary>
    private static readonly TimeSpan WarmPageTarget = TimeSpan.FromMilliseconds(300);

    private static readonly TimeSpan WarmPageBound = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan ColdPageBudget = TimeSpan.FromSeconds(10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class NoNamesCatalog : ICatalog
    {
        public IReadOnlyList<TemplateDefinition> Templates => [];

        public IReadOnlyList<LookDefinition> Looks => [];

        public string DefaultLookId => "naturale-v1";

        public TemplateDefinition? FindTemplate(string id) => null;

        public LookDefinition? FindLook(string id) => null;
    }

    private static void Report(string message)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(message);
        TestContext.Current.SendDiagnosticMessage(message);
    }

    [Fact]
    public async Task A_Photo_Strip_thumbnail_deleted_behind_the_galleries_back_is_made_again_when_the_page_is_next_built()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        var cycle = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
        var capture = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 1, Sample.Jpeg(80, 60)), Ct);
        await harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [capture.Id], Sample.Png(StripWidth, StripHeight)), Ct);
        var builder = new GalleryPageBuilder(harness.Store, new NoNamesCatalog());
        var first = (await builder.GuestPageAsync(record.Id, null, Ct)).Entries.Single().View;
        var file = Path.Combine(harness.DataRoot, "Cache", "thumbnails", first.ThumbnailUrl.Split('/')[^1]);
        Assert.True(File.Exists(file));
        File.Delete(file);

        var again = (await builder.GuestPageAsync(record.Id, null, Ct)).Entries.Single().View;

        Assert.Equal(first.ThumbnailUrl, again.ThumbnailUrl);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task A_thumbnail_of_an_older_version_of_the_Photo_Strip_is_not_reused_the_new_version_gets_its_own()
    {
        await using var harness = await StoreHarness.OpenedAsync();
        var record = Sample.Event();
        await harness.Store.CreateEventAsync(record, Ct);
        var cycle = await harness.Store.StartGuestCycleAsync(record.Id, Ct);
        var capture = await harness.Store.CommitCaptureAsync(Sample.Capture(cycle.Id, 1, Sample.Jpeg(80, 60)), Ct);
        await harness.Store.CommitPhotoStripAsync(Sample.Strip(cycle.Id, [capture.Id], Sample.Png(StripWidth, StripHeight)), Ct);
        var builder = new GalleryPageBuilder(harness.Store, new NoNamesCatalog());
        var oldUrl = (await builder.GuestPageAsync(record.Id, null, Ct)).Entries.Single().View.ThumbnailUrl;
        var strip = Path.Combine(harness.CycleFolder(record.Id, cycle.Id), "photo-strip.png");
        var replacement = Sample.Png(StripWidth, StripHeight, SKColors.Orange);
        await File.WriteAllBytesAsync(strip, replacement, Ct);
        harness.Execute($"UPDATE photo_strips SET sha256 = '{Convert.ToHexStringLower(SHA256.HashData(replacement))}', byte_length = {replacement.Length} WHERE guest_cycle_id = '{cycle.Id}'");

        var newUrl = (await builder.GuestPageAsync(record.Id, null, Ct)).Entries.Single().View.ThumbnailUrl;

        Assert.NotEqual(oldUrl, newUrl);
        Assert.True(File.Exists(Path.Combine(harness.DataRoot, "Cache", "thumbnails", newUrl.Split('/')[^1])));
    }

    [Fact]
    public async Task The_operator_gallery_pages_through_300_cycles_and_every_warm_page_loads_within_the_budget()
    {
        using var scratch = new ScratchFolder();
        var data = scratch.Combine("Data");
        var eventId = await SeedAsync(data);
        var (store, _) = await RealStore.OpenAsync(data);
        await using (store)
        {
            var builder = new GalleryPageBuilder(store, new NoNamesCatalog());

            var cold = await PageThroughAsync(builder, eventId, operatorList: true);
            var warm = await PageThroughAsync(builder, eventId, operatorList: true);

            Report(string.Create(CultureInfo.InvariantCulture, $"Operator gallery, {Cycles} cycles in {cold.Pages.Count} pages: cold pages {Format(cold.Pages)}; warm pages {Format(warm.Pages)} (target {WarmPageTarget.TotalMilliseconds:0} ms per page, asserted bound {WarmPageBound.TotalSeconds:0} s)."));
            Assert.Equal(8, cold.Pages.Count);
            Assert.Equal(Cycles, cold.Ids.Count);
            Assert.Equal(cold.Ids, warm.Ids);
            Assert.Equal(Cycles, cold.Ids.Distinct().Count());
            Assert.True(cold.Pages.Max() < ColdPageBudget, $"A cold page took {cold.Pages.Max().TotalSeconds:0.0} s; budget {ColdPageBudget.TotalSeconds:0} s.");
            Assert.True(warm.Pages.Max() < WarmPageBound, $"A warm page took {warm.Pages.Max().TotalMilliseconds:0} ms; bound {WarmPageBound.TotalMilliseconds:0} ms (target {WarmPageTarget.TotalMilliseconds:0} ms).");
        }
    }

    [Fact]
    public async Task The_guest_gallery_pages_through_every_completed_strip_of_a_300_cycle_Event_within_the_budget()
    {
        using var scratch = new ScratchFolder();
        var data = scratch.Combine("Data");
        var eventId = await SeedAsync(data);
        var (store, _) = await RealStore.OpenAsync(data);
        await using (store)
        {
            var builder = new GalleryPageBuilder(store, new NoNamesCatalog());

            var cold = await PageThroughAsync(builder, eventId, operatorList: false);
            var warm = await PageThroughAsync(builder, eventId, operatorList: false);

            Report(string.Create(CultureInfo.InvariantCulture, $"Guest gallery, {cold.Ids.Count} strips in {cold.Pages.Count} pages: cold pages {Format(cold.Pages)}; warm pages {Format(warm.Pages)} (target {WarmPageTarget.TotalMilliseconds:0} ms per page, asserted bound {WarmPageBound.TotalSeconds:0} s)."));
            Assert.Equal(6, cold.Pages.Count);
            Assert.Equal(CompletedCycles, cold.Ids.Count);
            Assert.Equal(cold.Ids, warm.Ids);
            Assert.True(warm.Pages.Max() < WarmPageBound, $"A warm page took {warm.Pages.Max().TotalMilliseconds:0} ms; bound {WarmPageBound.TotalMilliseconds:0} ms (target {WarmPageTarget.TotalMilliseconds:0} ms).");
        }
    }

    [Fact]
    public async Task Forty_real_size_Photo_Strip_thumbnails_are_made_cold_with_four_at_a_time_and_the_time_is_reported()
    {
        using var scratch = new ScratchFolder();
        var data = scratch.Combine("Data");
        var eventId = await SeedAsync(data);
        var (store, _) = await RealStore.OpenAsync(data);
        await using (store)
        {
            var builder = new GalleryPageBuilder(store, new NoNamesCatalog());
            var clock = Stopwatch.StartNew();

            var page = await builder.GuestPageAsync(eventId, null, Ct);

            clock.Stop();
            Report(string.Create(CultureInfo.InvariantCulture, $"{page.Entries.Count} real-size ({StripWidth} x {StripHeight}) Photo Strip thumbnails made cold, {GalleryPageBuilder.Parallelism} at a time: {clock.Elapsed.TotalMilliseconds:0} ms."));
            Assert.Equal(GalleryRules.PageSize, page.Entries.Count);
            Assert.All(page.Entries, entry => Assert.True(File.Exists(Path.Combine(data, "Cache", "thumbnails", entry.View.ThumbnailUrl.Split('/')[^1]))));
            Assert.True(clock.Elapsed < ColdPageBudget);
        }
    }

    private sealed record Paged(List<TimeSpan> Pages, List<string> Ids);

    private static async Task<Paged> PageThroughAsync(GalleryPageBuilder builder, string eventId, bool operatorList)
    {
        var pages = new List<TimeSpan>();
        var ids = new List<string>();
        string? cursor = null;
        do
        {
            var clock = Stopwatch.StartNew();
            string? next;
            if (operatorList)
            {
                var page = await builder.OperatorPageAsync(eventId, cursor, Ct);
                ids.AddRange(page.Entries.Select(entry => entry.GuestCycleId));
                Assert.All(page.Entries, entry => Assert.False(entry.View.Unavailable));
                next = page.NextCursor;
            }
            else
            {
                var page = await builder.GuestPageAsync(eventId, cursor, Ct);
                ids.AddRange(page.Entries.Select(entry => entry.GuestCycleId));
                next = page.NextCursor;
            }

            clock.Stop();
            pages.Add(clock.Elapsed);
            cursor = next;
        }
        while (cursor is not null);

        return new Paged(pages, ids);
    }

    private static string Format(IEnumerable<TimeSpan> pages) => string.Join(", ", pages.Select(page => string.Create(CultureInfo.InvariantCulture, $"{page.TotalMilliseconds:0}")));

    private static int CompletedCycles => Enumerable.Range(0, Cycles).Count(IsCompleted);

    private static bool IsCompleted(int index) => index % 4 != 3;

    private static readonly GuestCycleEndReason[] OtherReasons =
        [GuestCycleEndReason.StartedOver, GuestCycleEndReason.OperatorExit, GuestCycleEndReason.AppClosed, GuestCycleEndReason.Unrecoverable];

    /// <summary>One Event with 300 ended cycles: three in four completed (four Captures and a real-size Photo Strip), the rest ended another way with three Captures and no strip.</summary>
    private static async Task<string> SeedAsync(string data)
    {
        var record = Sample.Event(name: "Large Event");
        await using (var store = RealStore.Create(data))
        {
            await store.OpenAsync(CancellationToken.None);
            await store.CreateEventAsync(record, CancellationToken.None);
        }

        var captureBytes = Enumerable.Range(0, 4).Select(index => Image(SKEncodedImageFormat.Jpeg, CaptureWidth, CaptureHeight, index)).ToArray();
        var stripBytes = Image(SKEncodedImageFormat.Png, StripWidth, StripHeight, 9);
        var captureHashes = captureBytes.Select(bytes => Convert.ToHexStringLower(SHA256.HashData(bytes))).ToArray();
        var stripHash = Convert.ToHexStringLower(SHA256.HashData(stripBytes));
        var started = new DateTimeOffset(2026, 10, 24, 8, 0, 0, TimeSpan.Zero);

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(data, SqliteEventStore.DatabaseFileName), Pooling = false }.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction();
        for (var index = 0; index < Cycles; index++)
        {
            var cycleId = StorageIds.NewId();
            var folder = CrashDataFolder.CycleFolder(data, record.Id, cycleId);
            Directory.CreateDirectory(folder);
            var cycleStart = started.AddMinutes(index);
            var completed = IsCompleted(index);
            var reason = completed ? "completed" : StorageSqlNames.ToSql(OtherReasons[index % OtherReasons.Length]);
            Execute(connection, transaction, "INSERT INTO guest_cycles (id, event_id, started_at, ended_at, end_reason) VALUES ($id, $event, $started, $ended, $reason)", ("$id", cycleId), ("$event", record.Id), ("$started", StorageTimestamp.Format(cycleStart)), ("$ended", StorageTimestamp.Format(cycleStart.AddMinutes(1))), ("$reason", reason));

            var captureIds = new List<string>();
            for (var sequence = 0; sequence < (completed ? 4 : 3); sequence++)
            {
                var captureId = StorageIds.NewId();
                captureIds.Add(captureId);
                var fileName = $"capture-{sequence + 1}.jpg";
                File.WriteAllBytes(Path.Combine(folder, fileName), captureBytes[sequence]);
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO captures (id, guest_cycle_id, sequence, file_name, byte_length, sha256, width, height, mirrored, taken_at, state) VALUES ($id, $cycle, $sequence, $file, $length, $sha, $width, $height, 1, $taken, 'kept')",
                    ("$id", captureId),
                    ("$cycle", cycleId),
                    ("$sequence", sequence + 1),
                    ("$file", fileName),
                    ("$length", (long)captureBytes[sequence].Length),
                    ("$sha", captureHashes[sequence]),
                    ("$width", CaptureWidth),
                    ("$height", CaptureHeight),
                    ("$taken", StorageTimestamp.Format(cycleStart)));
            }

            if (completed)
            {
                File.WriteAllBytes(Path.Combine(folder, "photo-strip.png"), stripBytes);
                Execute(
                    connection,
                    transaction,
                    "INSERT INTO photo_strips (guest_cycle_id, template_id, look_id, arrangement, file_name, byte_length, sha256, width, height, committed_at) VALUES ($cycle, 'signature-strip', 'naturale-v1', $arrangement, 'photo-strip.png', $length, $sha, $width, $height, $committed)",
                    ("$cycle", cycleId),
                    ("$arrangement", JsonSerializer.Serialize(captureIds)),
                    ("$length", (long)stripBytes.Length),
                    ("$sha", stripHash),
                    ("$width", StripWidth),
                    ("$height", StripHeight),
                    ("$committed", StorageTimestamp.Format(cycleStart.AddMinutes(1))));
            }
        }

        transaction.Commit();
        return record.Id;
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }

    /// <summary>A real-size image with a gradient and a pattern, so decoding and resizing it costs what a photograph's would.</summary>
    private static byte[] Image(SKEncodedImageFormat format, int width, int height, int seed)
    {
        using var bitmap = new SKBitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, new SKColor((byte)((x * 255 / width) + seed), (byte)(y * 255 / height), (byte)(((x * y) >> 4) + (seed * 17))));
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }
}
