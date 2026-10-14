using FotoHavn.Core.Media;

namespace FotoHavn.Core.Tests.Media;

public sealed class PreviewCacheTests
{
    private static byte[] Bytes(int length, byte fill = 1) => Enumerable.Repeat(fill, length).ToArray();

    [Fact]
    public void A_put_entry_is_found_and_an_unknown_key_misses()
    {
        var cache = new PreviewCache();

        Assert.True(cache.Put("a", Bytes(10, 7)));

        Assert.True(cache.TryGet("a", out var png));
        Assert.Equal(Bytes(10, 7), png.ToArray());
        Assert.False(cache.TryGet("b", out var missing));
        Assert.True(missing.IsEmpty);
        Assert.Equal((1, 10L), (cache.Count, cache.TotalBytes));
    }

    [Fact]
    public void The_defaults_are_sixty_four_entries_and_128_MiB()
    {
        Assert.Equal(64, PreviewCache.DefaultMaxEntries);
        Assert.Equal(128L * 1024 * 1024, PreviewCache.DefaultMaxBytes);
    }

    [Fact]
    public void Eviction_by_count_drops_the_least_recently_used()
    {
        var cache = new PreviewCache(maxEntries: 3);
        cache.Put("a", Bytes(1));
        cache.Put("b", Bytes(1));
        cache.Put("c", Bytes(1));

        cache.Put("d", Bytes(1));

        Assert.False(cache.TryGet("a", out _));
        Assert.True(cache.TryGet("b", out _));
        Assert.True(cache.TryGet("c", out _));
        Assert.True(cache.TryGet("d", out _));
        Assert.Equal(3, cache.Count);
    }

    [Fact]
    public void A_get_counts_as_a_use_for_the_eviction_order()
    {
        var cache = new PreviewCache(maxEntries: 3);
        cache.Put("a", Bytes(1));
        cache.Put("b", Bytes(1));
        cache.Put("c", Bytes(1));
        Assert.True(cache.TryGet("a", out _));

        cache.Put("d", Bytes(1));

        Assert.True(cache.TryGet("a", out _));
        Assert.False(cache.TryGet("b", out _));
        Assert.True(cache.TryGet("c", out _));
    }

    [Fact]
    public void Eviction_by_bytes_drops_oldest_entries_until_the_budget_holds()
    {
        var cache = new PreviewCache(maxEntries: 10, maxBytes: 100);
        cache.Put("a", Bytes(40));
        cache.Put("b", Bytes(40));

        Assert.True(cache.Put("c", Bytes(50)));
        Assert.Equal((2, 90L), (cache.Count, cache.TotalBytes));

        cache.Put("d", Bytes(60));

        Assert.False(cache.TryGet("b", out _));
        Assert.False(cache.TryGet("c", out _));
        Assert.True(cache.TryGet("d", out _));
        Assert.Equal((1, 60L), (cache.Count, cache.TotalBytes));
    }

    [Fact]
    public void An_entry_over_the_byte_budget_is_not_cached_and_drops_the_stale_entry_for_its_key()
    {
        var cache = new PreviewCache(maxEntries: 10, maxBytes: 100);
        cache.Put("a", Bytes(10));
        cache.Put("big", Bytes(10));

        Assert.False(cache.Put("big", Bytes(101)));

        Assert.False(cache.TryGet("big", out _));
        Assert.True(cache.TryGet("a", out _));
        Assert.Equal((1, 10L), (cache.Count, cache.TotalBytes));
        Assert.True(cache.Put("exact", Bytes(100)));
    }

    [Fact]
    public void Replacing_a_key_updates_the_byte_accounting_and_makes_it_the_newest()
    {
        var cache = new PreviewCache(maxEntries: 2);
        cache.Put("a", Bytes(10));
        cache.Put("b", Bytes(20));

        cache.Put("a", Bytes(5, 9));
        cache.Put("c", Bytes(1));

        Assert.True(cache.TryGet("a", out var replaced));
        Assert.Equal(Bytes(5, 9), replaced.ToArray());
        Assert.False(cache.TryGet("b", out _));
        Assert.Equal((2, 6L), (cache.Count, cache.TotalBytes));
    }

    [Fact]
    public void Remove_and_clear_release_entries_and_bytes()
    {
        var cache = new PreviewCache();
        cache.Put("a", Bytes(10));
        cache.Put("b", Bytes(20));

        Assert.True(cache.Remove("a"));
        Assert.False(cache.Remove("a"));
        Assert.Equal((1, 20L), (cache.Count, cache.TotalBytes));

        cache.Clear();

        Assert.Equal((0, 0L), (cache.Count, cache.TotalBytes));
        Assert.False(cache.TryGet("b", out _));
        Assert.True(cache.Put("b", Bytes(1)));
    }

    [Fact]
    public void Arguments_are_validated()
    {
        var cache = new PreviewCache();

        Assert.Throws<ArgumentNullException>(() => cache.TryGet(null!, out _));
        Assert.Throws<ArgumentNullException>(() => cache.Remove(null!));
        Assert.Throws<ArgumentNullException>(() => cache.Put(null!, Bytes(1)));
        Assert.Throws<ArgumentException>(() => cache.Put(string.Empty, Bytes(1)));
        Assert.Throws<ArgumentException>(() => cache.Put("a", ReadOnlyMemory<byte>.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PreviewCache(maxEntries: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PreviewCache(maxBytes: 0));
    }

    [Fact]
    public void The_cache_is_both_the_brains_store_and_the_hosts_source()
    {
        var cache = new PreviewCache();

        Exercise(cache, cache);
    }

#pragma warning disable CA1859 // The test exercises the interfaces the brain and the hosts see.
    private static void Exercise(IPreviewStore store, IPreviewSource source)
#pragma warning restore CA1859
    {
        Assert.True(store.Put("k", Bytes(3)));

        Assert.True(source.TryGet("k", out _));
        Assert.True(store.Remove("k"));
        store.Clear();
        Assert.False(source.TryGet("k", out _));
    }

    [Fact]
    public void Concurrent_puts_and_gets_keep_the_bounds_and_the_accounting()
    {
        var cache = new PreviewCache(maxEntries: 16, maxBytes: 1_000);

        Parallel.For(0, 4_000, i =>
        {
            var key = "k" + (i % 40);
            cache.Put(key, Bytes(10 + (i % 50), (byte)(i % 200)));
            cache.TryGet("k" + ((i * 7) % 40), out _);
            if (i % 97 == 0)
            {
                cache.Remove(key);
            }
        });

        Assert.InRange(cache.Count, 0, 16);
        Assert.InRange(cache.TotalBytes, 0L, 1_000L);
        long counted = 0;
        for (var i = 0; i < 40; i++)
        {
            if (cache.TryGet("k" + i, out var png))
            {
                counted += png.Length;
            }
        }

        Assert.Equal(cache.TotalBytes, counted);
    }
}
