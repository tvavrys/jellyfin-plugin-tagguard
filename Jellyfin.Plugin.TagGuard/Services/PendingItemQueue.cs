using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Jellyfin.Plugin.TagGuard.Services;

internal enum PendingItemAddResult
{
    Added,
    AlreadyPending,
    CapacityReached
}

/// <summary>
/// A bounded, thread-safe set of newly added items awaiting a quiet period.
/// </summary>
internal sealed class PendingItemQueue
{
    public const int MaximumPendingItems = 256;

    private readonly object _sync = new();
    private readonly Dictionary<Guid, PendingItem> _items = [];

    public int Count
    {
        get
        {
            lock (this._sync)
            {
                return this._items.Count;
            }
        }
    }

    public PendingItemAddResult TryAdd(Guid itemId, DateTimeOffset now)
    {
        lock (this._sync)
        {
            if (this._items.ContainsKey(itemId))
            {
                return PendingItemAddResult.AlreadyPending;
            }

            if (this._items.Count >= MaximumPendingItems)
            {
                return PendingItemAddResult.CapacityReached;
            }

            this._items.Add(itemId, new PendingItem(now, now));
            return PendingItemAddResult.Added;
        }
    }

    public bool NotifyUpdated(Guid itemId, DateTimeOffset now)
    {
        lock (this._sync)
        {
            if (!this._items.TryGetValue(itemId, out var pendingItem))
            {
                return false;
            }

            this._items[itemId] = pendingItem with { LastUpdatedAt = now };
            return true;
        }
    }

    public IReadOnlyList<Guid> TakeReady(DateTimeOffset now, TimeSpan quietPeriod, TimeSpan maximumSettleTime)
    {
        lock (this._sync)
        {
            var readyItemIds = this._items
                .Where(pair => IsReady(pair.Value, now, quietPeriod, maximumSettleTime))
                .Select(static pair => pair.Key)
                .ToArray();

            foreach (var itemId in readyItemIds)
            {
                this._items.Remove(itemId);
            }

            return readyItemIds;
        }
    }

    public TimeSpan TimeUntilNextReady(DateTimeOffset now, TimeSpan quietPeriod, TimeSpan maximumSettleTime)
    {
        lock (this._sync)
        {
            if (this._items.Count == 0)
            {
                return Timeout.InfiniteTimeSpan;
            }

            var nextReadyAt = this._items.Values
                .Select(item => Min(item.AddedAt + maximumSettleTime, item.LastUpdatedAt + quietPeriod))
                .Min();
            var wait = nextReadyAt - now;
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }
    }

    public void Clear()
    {
        lock (this._sync)
        {
            this._items.Clear();
        }
    }

    private static bool IsReady(PendingItem item, DateTimeOffset now, TimeSpan quietPeriod, TimeSpan maximumSettleTime)
    {
        return now >= item.AddedAt + maximumSettleTime || now >= item.LastUpdatedAt + quietPeriod;
    }

    private static DateTimeOffset Min(DateTimeOffset first, DateTimeOffset second) => first <= second ? first : second;

    private sealed record PendingItem(DateTimeOffset AddedAt, DateTimeOffset LastUpdatedAt);
}
