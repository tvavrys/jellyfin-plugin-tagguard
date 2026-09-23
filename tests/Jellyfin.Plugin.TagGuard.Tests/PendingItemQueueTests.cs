using Jellyfin.Plugin.TagGuard.Services;
using Xunit;

namespace Jellyfin.Plugin.TagGuard.Tests;

public sealed class PendingItemQueueTests
{
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan MaximumSettleTime = TimeSpan.FromMinutes(2);

    [Fact]
    public void NotifyUpdated_RestartsQuietPeriod()
    {
        var queue = new PendingItemQueue();
        var start = DateTimeOffset.UtcNow;
        var itemId = Guid.NewGuid();

        Assert.Equal(PendingItemAddResult.Added, queue.TryAdd(itemId, start));
        Assert.True(queue.NotifyUpdated(itemId, start.AddSeconds(3)));
        Assert.Empty(queue.TakeReady(start.AddSeconds(6), QuietPeriod, MaximumSettleTime));
        Assert.Equal([itemId], queue.TakeReady(start.AddSeconds(7), QuietPeriod, MaximumSettleTime));
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void MaximumSettleTime_EndsTrackingDespiteRepeatedUpdates()
    {
        var queue = new PendingItemQueue();
        var start = DateTimeOffset.UtcNow;
        var itemId = Guid.NewGuid();

        Assert.Equal(PendingItemAddResult.Added, queue.TryAdd(itemId, start));
        Assert.True(queue.NotifyUpdated(itemId, start.AddSeconds(30)));
        Assert.True(queue.NotifyUpdated(itemId, start.AddSeconds(60)));

        Assert.Equal([itemId], queue.TakeReady(start.AddMinutes(2), QuietPeriod, MaximumSettleTime));
        Assert.False(queue.NotifyUpdated(itemId, start.AddMinutes(2)));
    }

    [Fact]
    public void PendingSet_IsBounded()
    {
        var queue = new PendingItemQueue();
        var now = DateTimeOffset.UtcNow;

        for (var index = 0; index < PendingItemQueue.MaximumPendingItems; index++)
        {
            Assert.Equal(PendingItemAddResult.Added, queue.TryAdd(Guid.NewGuid(), now));
        }

        Assert.Equal(PendingItemAddResult.CapacityReached, queue.TryAdd(Guid.NewGuid(), now));
        Assert.Equal(PendingItemQueue.MaximumPendingItems, queue.Count);
    }

    [Fact]
    public void UpdateForUntrackedItem_DoesNotAddPendingWork()
    {
        var queue = new PendingItemQueue();

        Assert.False(queue.NotifyUpdated(Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.Equal(0, queue.Count);
    }
}
