using System.Collections.Immutable;
using FotoHavn.Core.Storage;

namespace FotoHavn.Core.Gallery;

/// <summary>What a gallery page keeps an item by: the Guest Cycle's id and start, which fix its place in the newest-first order.</summary>
public interface IGalleryEntry
{
    string GuestCycleId { get; }

    DateTimeOffset StartedAt { get; }
}

/// <summary>A ready-made <see cref="IGalleryEntry"/>: the key plus the view the brain presents for it.</summary>
/// <typeparam name="TView">The protocol view of the item.</typeparam>
public sealed record GalleryEntry<TView>(string GuestCycleId, DateTimeOffset StartedAt, TView View) : IGalleryEntry;

/// <summary>Creates the empty state of a <see cref="GalleryPage{TEntry}"/>.</summary>
public static class GalleryPage
{
    /// <summary>The state before the first page has arrived.</summary>
    public static GalleryPage<TEntry> Empty<TEntry>()
        where TEntry : IGalleryEntry => new([], null, false, ImmutableHashSet.Create<string>(StringComparer.Ordinal));
}

/// <summary>
/// The loaded part of one gallery view (plan section 8, Event Gallery): an immutable page state the brain keeps for the operator view and for
/// the guest view and replaces with the result of each operation. Items are newest first, ordered by (<c>StartedAt</c>, <c>GuestCycleId</c>)
/// descending like the store's keyset cursor, and never repeat. A Guest Cycle removed with <see cref="Remove"/> never comes back, even when a
/// page that was read before the delete arrives later.
/// </summary>
/// <typeparam name="TEntry">The entry type.</typeparam>
public sealed class GalleryPage<TEntry>
    where TEntry : IGalleryEntry
{
    private readonly ImmutableHashSet<string> _removed;

    internal GalleryPage(ImmutableArray<TEntry> items, string? nextCursor, bool loaded, ImmutableHashSet<string> removed)
    {
        Items = items;
        NextCursor = nextCursor;
        Loaded = loaded;
        _removed = removed;
    }

    /// <summary>The entries loaded so far, newest first.</summary>
    public ImmutableArray<TEntry> Items { get; }

    /// <summary>The cursor for the next page, or null when none follows (or before the first page).</summary>
    public string? NextCursor { get; }

    /// <summary>Whether the first page has been applied.</summary>
    public bool Loaded { get; }

    /// <summary>Whether another page follows the loaded ones.</summary>
    public bool HasMore => NextCursor is not null;

    /// <summary>The cursor the next page request must carry: null for the first page, otherwise <see cref="NextCursor"/>. See <see cref="CanLoadMore"/>.</summary>
    public string? RequestCursor => Loaded ? NextCursor : null;

    /// <summary>Whether a page request makes sense: the first page isn't loaded yet, or another page follows.</summary>
    public bool CanLoadMore => !Loaded || HasMore;

    /// <summary>Whether the first page arrived and holds no Guest Cycle.</summary>
    public bool IsEmpty => Loaded && Items.IsEmpty;

    public bool Contains(string guestCycleId) => Items.Any(entry => string.Equals(entry.GuestCycleId, guestCycleId, StringComparison.Ordinal));

    public TEntry? Find(string guestCycleId) =>
        Items.FirstOrDefault(entry => string.Equals(entry.GuestCycleId, guestCycleId, StringComparison.Ordinal));

    /// <summary>
    /// Applies the answer to a page request. It is ignored (the same state is returned) when <paramref name="requestedCursor"/> isn't the cursor
    /// the page was waiting for (a repeated or stale answer), when nothing follows the loaded pages, or when <paramref name="nextCursor"/> doesn't
    /// move to older Guest Cycles, so the cursor only ever moves forward. Entries already shown or removed are dropped from <paramref name="entries"/>.
    /// </summary>
    public GalleryPage<TEntry> ApplyPage(string? requestedCursor, IEnumerable<TEntry> entries, string? nextCursor)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (!CanLoadMore || !string.Equals(requestedCursor, RequestCursor, StringComparison.Ordinal))
        {
            return this;
        }

        if (nextCursor is not null && RequestCursor is not null && !IsOlder(nextCursor, RequestCursor))
        {
            return this;
        }

        return new GalleryPage<TEntry>(Merge(entries), nextCursor, true, _removed);
    }

    /// <summary>Adds a Guest Cycle that ended while the gallery is open. An entry already shown or removed changes nothing.</summary>
    public GalleryPage<TEntry> Insert(TEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Contains(entry.GuestCycleId) || _removed.Contains(entry.GuestCycleId) ? this : new GalleryPage<TEntry>(Merge([entry]), NextCursor, Loaded, _removed);
    }

    /// <summary>Removes a deleted Guest Cycle and remembers it, so no later page can show it again.</summary>
    public GalleryPage<TEntry> Remove(string guestCycleId)
    {
        ArgumentNullException.ThrowIfNull(guestCycleId);
        return new GalleryPage<TEntry>(
            [.. Items.Where(entry => !string.Equals(entry.GuestCycleId, guestCycleId, StringComparison.Ordinal))],
            NextCursor,
            Loaded,
            _removed.Add(guestCycleId));
    }

    private ImmutableArray<TEntry> Merge(IEnumerable<TEntry> entries)
    {
        var known = new HashSet<string>(Items.Select(entry => entry.GuestCycleId), StringComparer.Ordinal);
        var merged = Items.ToList();
        foreach (var entry in entries)
        {
            if (!_removed.Contains(entry.GuestCycleId) && known.Add(entry.GuestCycleId))
            {
                merged.Add(entry);
            }
        }

        return [.. merged.OrderByDescending(entry => entry.StartedAt).ThenByDescending(entry => entry.GuestCycleId, StringComparer.Ordinal)];
    }

    // An undecodable cursor can't be compared; it is accepted, because the store only ever issues valid ones.
    private static bool IsOlder(string next, string current)
    {
        if (!GuestCycleCursor.TryDecode(next, out var nextStarted, out var nextId) || !GuestCycleCursor.TryDecode(current, out var currentStarted, out var currentId))
        {
            return true;
        }

        var byTime = nextStarted.CompareTo(currentStarted);
        return byTime < 0 || (byTime == 0 && string.CompareOrdinal(nextId, currentId) < 0);
    }
}
