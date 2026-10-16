namespace FotoHavn.Core.Media;

/// <summary>The brain's side of the preview cache: it puts composed previews in and takes them out.</summary>
public interface IPreviewStore
{
    /// <summary>
    /// Stores <paramref name="png"/> under <paramref name="key"/>, replacing any earlier entry for the key. The cache keeps the
    /// memory as given, so the caller must not change it afterwards.
    /// </summary>
    /// <returns>
    /// False when the entry alone exceeds the cache's byte budget: it isn't cached, and any earlier entry for the key is
    /// removed so a stale image is never served under the key.
    /// </returns>
    bool Put(string key, ReadOnlyMemory<byte> png);

    /// <summary>Whether an entry for <paramref name="key"/> exists, without counting as a use.</summary>
    bool Contains(string key);

    /// <summary>Removes the entry for <paramref name="key"/>. Returns whether there was one.</summary>
    bool Remove(string key);

    /// <summary>Removes every entry.</summary>
    void Clear();
}

/// <summary>
/// The in-memory preview cache: an <see cref="IPreviewStore"/> for the brain and the <see cref="IPreviewSource"/> the hosts
/// serve at <c>https://preview.fotohavn/&lt;key&gt;.png</c>. Eviction is least-recently-used (a <see cref="TryGet"/> counts
/// as a use), bounded by both an entry count and a total byte budget. Every member is thread-safe and returns at once.
/// </summary>
public sealed class PreviewCache : IPreviewSource, IPreviewStore
{
    public const int DefaultMaxEntries = 64;
    public const long DefaultMaxBytes = 128L * 1024 * 1024;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, LinkedListNode<Entry>> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> _byRecency = new();
    private readonly int _maxEntries;
    private readonly long _maxBytes;
    private long _totalBytes;

    public PreviewCache(int maxEntries = DefaultMaxEntries, long maxBytes = DefaultMaxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEntries, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 1);
        _maxEntries = maxEntries;
        _maxBytes = maxBytes;
    }

    /// <summary>How many entries the cache holds.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>The total size of the cached PNGs in bytes.</summary>
    public long TotalBytes
    {
        get
        {
            lock (_gate)
            {
                return _totalBytes;
            }
        }
    }

    public bool TryGet(string key, out ReadOnlyMemory<byte> png)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var node))
            {
                _byRecency.Remove(node);
                _byRecency.AddFirst(node);
                png = node.Value.Png;
                return true;
            }
        }

        png = ReadOnlyMemory<byte>.Empty;
        return false;
    }

    public bool Contains(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_gate)
        {
            return _entries.ContainsKey(key);
        }
    }

    public bool Put(string key, ReadOnlyMemory<byte> png)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (png.IsEmpty)
        {
            throw new ArgumentException("A preview has at least one byte.", nameof(png));
        }

        lock (_gate)
        {
            RemoveLocked(key);
            if (png.Length > _maxBytes)
            {
                return false;
            }

            _entries[key] = _byRecency.AddFirst(new Entry(key, png));
            _totalBytes += png.Length;
            while (_entries.Count > _maxEntries || _totalBytes > _maxBytes)
            {
                // The new entry fits the budget alone and sits at the front, so the loop stops before it reaches it.
                RemoveLocked(_byRecency.Last!.Value.Key);
            }

            return true;
        }
    }

    public bool Remove(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_gate)
        {
            return RemoveLocked(key);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _byRecency.Clear();
            _totalBytes = 0;
        }
    }

    private bool RemoveLocked(string key)
    {
        if (!_entries.Remove(key, out var node))
        {
            return false;
        }

        _byRecency.Remove(node);
        _totalBytes -= node.Value.Png.Length;
        return true;
    }

    private readonly record struct Entry(string Key, ReadOnlyMemory<byte> Png);
}
