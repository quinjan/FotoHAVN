using FotoHavn.Core.Gallery;
using FotoHavn.Core.Storage;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace FotoHavn.Core.Tests.Gallery;

public sealed class GalleryPageTests
{
    private static GalleryEntry<string> Entry(int number) => new(GalleryKit.Id(number), GalleryKit.Noon.AddMinutes(number), $"view-{number}");

    private static string CursorAfter(int number) => GuestCycleCursor.Encode(Entry(number).StartedAt, Entry(number).GuestCycleId);

    private static GalleryPage<GalleryEntry<string>> Empty() => GalleryPage.Empty<GalleryEntry<string>>();

    private static string[] Ids(GalleryPage<GalleryEntry<string>> page) => [.. page.Items.Select(entry => entry.GuestCycleId)];

    private static string[] Expected(params int[] numbers) => [.. numbers.Select(GalleryKit.Id)];

    [Fact]
    public void A_new_page_has_nothing_loaded_and_asks_for_the_first_page()
    {
        var page = Empty();

        Assert.False(page.Loaded);
        Assert.Null(page.RequestCursor);
        Assert.True(page.CanLoadMore);
        Assert.False(page.IsEmpty);
        Assert.False(page.HasMore);
        Assert.Empty(page.Items);
    }

    [Fact]
    public void The_first_page_loads_newest_first_and_remembers_the_cursor()
    {
        var page = Empty().ApplyPage(null, [Entry(3), Entry(5), Entry(4)], CursorAfter(3));

        Assert.Equal([GalleryKit.Id(5), GalleryKit.Id(4), GalleryKit.Id(3)], Ids(page));
        Assert.True(page.Loaded);
        Assert.True(page.HasMore);
        Assert.Equal(CursorAfter(3), page.RequestCursor);
    }

    [Fact]
    public void A_first_page_with_nothing_in_it_is_empty_and_complete()
    {
        var page = Empty().ApplyPage(null, [], null);

        Assert.True(page.IsEmpty);
        Assert.False(page.CanLoadMore);
        Assert.Null(page.RequestCursor);
    }

    [Fact]
    public void The_next_page_is_appended_after_the_first_and_the_last_page_ends_the_list()
    {
        var first = Empty().ApplyPage(null, [Entry(9), Entry(8)], CursorAfter(8));
        var second = first.ApplyPage(CursorAfter(8), [Entry(7), Entry(6)], CursorAfter(6));
        var last = second.ApplyPage(CursorAfter(6), [Entry(5)], null);

        Assert.Equal(Expected(9, 8, 7, 6, 5), Ids(last));
        Assert.False(last.HasMore);
        Assert.False(last.CanLoadMore);
        Assert.False(last.IsEmpty);
    }

    [Fact]
    public void An_answer_to_a_request_that_is_no_longer_current_is_ignored()
    {
        var first = Empty().ApplyPage(null, [Entry(9), Entry(8)], CursorAfter(8));
        var second = first.ApplyPage(CursorAfter(8), [Entry(7)], CursorAfter(7));

        Assert.Same(second, second.ApplyPage(CursorAfter(8), [Entry(7), Entry(6)], CursorAfter(6)));
        Assert.Same(first, first.ApplyPage(null, [Entry(1)], null));
        Assert.Same(first, first.ApplyPage("somewhere-else", [Entry(1)], null));
    }

    [Fact]
    public void Nothing_follows_the_last_page()
    {
        var done = Empty().ApplyPage(null, [Entry(2)], null);

        Assert.Same(done, done.ApplyPage(null, [Entry(1)], null));
    }

    [Fact]
    public void A_cursor_that_does_not_move_to_older_cycles_is_ignored()
    {
        var first = Empty().ApplyPage(null, [Entry(9), Entry(8)], CursorAfter(8));

        Assert.Same(first, first.ApplyPage(CursorAfter(8), [Entry(7)], CursorAfter(8)));
        Assert.Same(first, first.ApplyPage(CursorAfter(8), [Entry(7)], CursorAfter(9)));
    }

    [Fact]
    public void A_cursor_the_page_cannot_decode_is_taken_as_the_store_issued_it()
    {
        var first = Empty().ApplyPage(null, [Entry(9)], "opaque-1");
        var second = first.ApplyPage("opaque-1", [Entry(8)], "opaque-2");

        Assert.Equal("opaque-2", second.NextCursor);
        Assert.Equal(Expected(9, 8), Ids(second));
    }

    [Fact]
    public void A_cycle_that_ends_while_the_gallery_is_open_goes_to_the_top_once()
    {
        var page = Empty().ApplyPage(null, [Entry(5), Entry(4)], CursorAfter(4));

        var inserted = page.Insert(Entry(7));

        Assert.Equal(Expected(7, 5, 4), Ids(inserted));
        Assert.Equal(page.NextCursor, inserted.NextCursor);
        Assert.Same(inserted, inserted.Insert(Entry(7)));
        Assert.Same(page, page.Insert(Entry(4)));
    }

    [Fact]
    public void An_inserted_cycle_is_not_repeated_by_a_page_read_a_moment_later()
    {
        var page = Empty().ApplyPage(null, [Entry(5)], CursorAfter(5)).Insert(Entry(7));

        var next = page.ApplyPage(CursorAfter(5), [Entry(7), Entry(4)], null);

        Assert.Equal(Expected(7, 5, 4), Ids(next));
    }

    [Fact]
    public void A_removed_cycle_goes_at_once_and_never_comes_back()
    {
        var page = Empty().ApplyPage(null, [Entry(9), Entry(8)], CursorAfter(8));

        var removed = page.Remove(GalleryKit.Id(8));
        var staleNext = removed.ApplyPage(CursorAfter(8), [Entry(8), Entry(7)], null);

        Assert.Equal(Expected(9), Ids(removed));
        Assert.Equal(Expected(9, 7), Ids(staleNext));
        Assert.Same(staleNext, staleNext.Insert(Entry(8)));
    }

    [Fact]
    public void Removing_an_unknown_cycle_changes_nothing_but_remembers_it()
    {
        var page = Empty().ApplyPage(null, [Entry(3)], null);

        var removed = page.Remove(GalleryKit.Id(99));

        Assert.Equal(Ids(page), Ids(removed));
        Assert.Same(removed, removed.Insert(Entry(99)));
    }

    [Fact]
    public void A_cycle_is_found_by_id()
    {
        var page = Empty().ApplyPage(null, [Entry(3), Entry(2)], null);

        Assert.True(page.Contains(GalleryKit.Id(2)));
        Assert.False(page.Contains(GalleryKit.Id(5)));
        Assert.Equal("view-3", page.Find(GalleryKit.Id(3))!.View);
        Assert.Null(page.Find(GalleryKit.Id(5)));
    }

    [Fact]
    public void Cycles_that_started_together_are_ordered_by_id_like_the_store()
    {
        var a = new GalleryEntry<string>(GalleryKit.Id(1), GalleryKit.Noon, "a");
        var b = new GalleryEntry<string>(GalleryKit.Id(2), GalleryKit.Noon, "b");

        var page = Empty().ApplyPage(null, [a, b], null);

        Assert.Equal([GalleryKit.Id(2), GalleryKit.Id(1)], Ids(page));
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        Assert.Throws<ArgumentNullException>(() => Empty().ApplyPage(null, null!, null));
        Assert.Throws<ArgumentNullException>(() => Empty().Insert(null!));
        Assert.Throws<ArgumentNullException>(() => Empty().Remove(null!));
    }

    // The page state against a simulated store, under random operations

    private const int Cycles = 30;
    private const int StorePageSize = 4;

    /// <summary>One random operation: a kind and two numbers it reads.</summary>
    public sealed record Operation(int Kind, int A, int B);

    /// <summary>A random operation sequence.</summary>
    public sealed record Script(IReadOnlyList<Operation> Steps)
    {
        public override string ToString() => string.Join(" ", Steps.Select(step => $"{step.Kind}:{step.A}.{step.B}"));
    }

    public static class Generators
    {
        public static Arbitrary<Script> Scripts()
        {
            var step = Gen.Frequency([(10, Gen.Constant(0)), (2, Gen.Constant(1)), (3, Gen.Constant(2)), (3, Gen.Constant(3)), (2, Gen.Constant(4))])
                .SelectMany(kind => Gen.Choose(1, Cycles).SelectMany(a => Gen.Choose(1, Cycles).Select(b => new Operation(kind, a, b))));
            return Gen.ArrayOf(step, 80).Select(steps => new Script(steps)).ToArbitrary();
        }
    }

    /// <summary>The store's view: every cycle 1 to 30 that isn't deleted, newest first, read a page at a time by keyset cursor.</summary>
    private static (IReadOnlyList<GalleryEntry<string>> Entries, string? Next) ReadPage(string? cursor, HashSet<int> deleted)
    {
        var visible = Enumerable.Range(1, Cycles).Where(number => !deleted.Contains(number)).OrderByDescending(number => number).Select(Entry);
        if (cursor is not null)
        {
            Assert.True(GuestCycleCursor.TryDecode(cursor, out var startedAt, out var id));
            visible = visible.Where(entry => entry.StartedAt < startedAt || (entry.StartedAt == startedAt && string.CompareOrdinal(entry.GuestCycleId, id) < 0));
        }

        var rows = visible.ToList();
        var page = rows.Take(StorePageSize).ToList();
        return (page, rows.Count > StorePageSize ? GuestCycleCursor.Encode(page[^1].StartedAt, page[^1].GuestCycleId) : null);
    }

    private static void Run(Script script, Action<GalleryPage<GalleryEntry<string>>, GalleryPage<GalleryEntry<string>>, HashSet<string>> check)
    {
        var page = Empty();
        var deleted = new HashSet<int>();
        var removedIds = new HashSet<string>(StringComparer.Ordinal);
        (string? Requested, IReadOnlyList<GalleryEntry<string>> Entries, string? Next)? lastAnswer = null;

        foreach (var step in script.Steps)
        {
            var before = page;
            switch (step.Kind)
            {
                case 0:
                {
                    var requested = page.RequestCursor;
                    var (entries, next) = ReadPage(requested, deleted);
                    lastAnswer = (requested, entries, next);
                    page = page.ApplyPage(requested, entries, next);
                    break;
                }

                case 1 when lastAnswer is { } answer:
                    page = page.ApplyPage(answer.Requested, answer.Entries, answer.Next);
                    break;
                case 2:
                    page = page.Insert(Entry(step.A));
                    break;
                case 3:
                    page = page.Remove(GalleryKit.Id(step.A));
                    deleted.Add(step.A);
                    removedIds.Add(GalleryKit.Id(step.A));
                    break;
                case 4:
                {
                    // A page read before the deletes: it still holds what has since been removed.
                    var (entries, next) = ReadPage(page.RequestCursor, new HashSet<int>());
                    page = page.ApplyPage(page.RequestCursor, entries, next);
                    break;
                }
            }

            check(before, page, removedIds);
        }
    }

    private static bool IsNewestFirst(GalleryPage<GalleryEntry<string>> page) =>
        page.Items.Zip(page.Items.Skip(1)).All(pair => pair.First.StartedAt > pair.Second.StartedAt || (pair.First.StartedAt == pair.Second.StartedAt && string.CompareOrdinal(pair.First.GuestCycleId, pair.Second.GuestCycleId) > 0));

    [Property(Arbitrary = [typeof(Generators)], MaxTest = 300)]
    public bool Items_never_repeat_and_stay_newest_first(Script script)
    {
        Run(script, (_, page, _) =>
        {
            Assert.Equal(page.Items.Length, page.Items.Select(entry => entry.GuestCycleId).Distinct(StringComparer.Ordinal).Count());
            Assert.True(IsNewestFirst(page), script.ToString());
        });
        return true;
    }

    [Property(Arbitrary = [typeof(Generators)], MaxTest = 300)]
    public bool A_removed_cycle_never_reappears(Script script)
    {
        Run(script, (_, page, removed) => Assert.DoesNotContain(page.Items, entry => removed.Contains(entry.GuestCycleId)));
        return true;
    }

    [Property(Arbitrary = [typeof(Generators)], MaxTest = 300)]
    public bool The_cursor_only_moves_to_older_cycles_and_loading_never_unloads(Script script)
    {
        Run(script, (before, after, _) =>
        {
            Assert.True(!before.Loaded || after.Loaded);
            if (before.NextCursor is { } was && after.NextCursor is { } now && was != now)
            {
                Assert.True(GuestCycleCursor.TryDecode(was, out var wasStarted, out var wasId));
                Assert.True(GuestCycleCursor.TryDecode(now, out var nowStarted, out var nowId));
                Assert.True(nowStarted < wasStarted || (nowStarted == wasStarted && string.CompareOrdinal(nowId, wasId) < 0), script.ToString());
            }

            // Once nothing follows, no later page brings a cursor back.
            Assert.True(!(before.Loaded && before.NextCursor is null) || after.NextCursor is null);
        });
        return true;
    }

    [Property(Arbitrary = [typeof(Generators)], MaxTest = 300)]
    public bool Every_loaded_cycle_is_one_the_store_or_an_insert_supplied(Script script)
    {
        Run(script, (_, page, _) => Assert.All(page.Items, entry => Assert.InRange(int.Parse(entry.GuestCycleId[..8], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture), 1, Cycles)));
        return true;
    }
}
