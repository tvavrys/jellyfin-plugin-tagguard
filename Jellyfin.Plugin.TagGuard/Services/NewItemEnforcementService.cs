using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TagGuard.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TagGuard.Services;

/// <summary>
/// Settles and sanitizes newly added movies and series once, based on Jellyfin library events.
/// </summary>
public sealed class NewItemEnforcementService : IHostedService, IDisposable, IAsyncDisposable
{
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan MaximumSettleTime = TimeSpan.FromMinutes(2);

    private readonly object _lifecycleSync = new();
    private readonly PendingItemQueue _pendingItems = new();
    private readonly SemaphoreSlim _wakeSignal = new(0, 1);
    private readonly ILibraryManager _libraryManager;
    private readonly TagGuardConfigurationValidator _configurationValidator;
    private readonly TagSanitizer _tagSanitizer;
    private readonly ILogger<NewItemEnforcementService> _logger;
    private CancellationTokenSource? _shutdown;
    private Task? _worker;
    private bool _started;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NewItemEnforcementService"/> class.
    /// </summary>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    /// <param name="configurationValidator">The configuration validator.</param>
    /// <param name="tagSanitizer">The tag sanitizer.</param>
    /// <param name="logger">The logger.</param>
    public NewItemEnforcementService(
        ILibraryManager libraryManager,
        TagGuardConfigurationValidator configurationValidator,
        TagSanitizer tagSanitizer,
        ILogger<NewItemEnforcementService> logger)
    {
        this._libraryManager = libraryManager;
        this._configurationValidator = configurationValidator;
        this._tagSanitizer = tagSanitizer;
        this._logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (this._lifecycleSync)
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);
            if (this._started)
            {
                return Task.CompletedTask;
            }

            this._shutdown = new CancellationTokenSource();
            this._libraryManager.ItemAdded += this.OnItemAdded;
            this._libraryManager.ItemUpdated += this.OnItemUpdated;
            this._started = true;
            this._worker = this.RunAsync(this._shutdown.Token);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? shutdown;
        Task? worker;
        lock (this._lifecycleSync)
        {
            if (this._started)
            {
                this._started = false;
                this._libraryManager.ItemAdded -= this.OnItemAdded;
                this._libraryManager.ItemUpdated -= this.OnItemUpdated;
            }

            shutdown = this._shutdown;
            worker = this._worker;
        }

        if (shutdown is null && worker is null)
        {
            return;
        }

        this._pendingItems.Clear();
        if (shutdown is not null && !shutdown.IsCancellationRequested)
        {
            await shutdown.CancelAsync().ConfigureAwait(false);
        }

        if (worker is not null)
        {
            await worker.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        shutdown?.Dispose();
        lock (this._lifecycleSync)
        {
            if (ReferenceEquals(this._shutdown, shutdown))
            {
                this._shutdown = null;
                this._worker = null;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (this._lifecycleSync)
        {
            if (this._disposed)
            {
                return;
            }

            if (this._started)
            {
                this._started = false;
                this._libraryManager.ItemAdded -= this.OnItemAdded;
                this._libraryManager.ItemUpdated -= this.OnItemUpdated;
            }

            this._disposed = true;
        }

        this._pendingItems.Clear();
        this._shutdown?.Dispose();
        this._wakeSignal.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await this.StopAsync(CancellationToken.None).ConfigureAwait(false);
        this.Dispose();
    }

    private void OnItemAdded(object? sender, ItemChangeEventArgs eventArgs)
    {
        var item = eventArgs.Item;
        try
        {
            this.TrackNewItem(item);
        }
        catch (Exception exception)
        {
            this._logger.LogError(exception, "TagGuard could not begin tracking new item {ItemId}", item.Id);
        }
    }

    private void TrackNewItem(BaseItem item)
    {
        var configuration = Plugin.Instance?.Configuration;
        if (configuration?.EnforceNewItems != true)
        {
            return;
        }

        var validation = this._configurationValidator.Validate(configuration);
        if (!validation.IsValid || !IsManagedItem(item, validation.ManagedLibraries.Select(static library => library.Id).ToHashSet()))
        {
            return;
        }

        PendingItemAddResult addResult;
        lock (this._lifecycleSync)
        {
            if (!this._started)
            {
                return;
            }

            addResult = this._pendingItems.TryAdd(item.Id, DateTimeOffset.UtcNow);
            if (addResult == PendingItemAddResult.Added)
            {
                this.SignalWorker();
            }
        }

        if (addResult == PendingItemAddResult.CapacityReached)
        {
            this._logger.LogWarning("TagGuard cannot track new item {ItemId}: the pending item limit of {PendingLimit} has been reached", item.Id, PendingItemQueue.MaximumPendingItems);
        }
    }

    private void OnItemUpdated(object? sender, ItemChangeEventArgs eventArgs)
    {
        var itemId = eventArgs.Item.Id;
        if (this._tagSanitizer.IsPersistingUpdate(itemId))
        {
            return;
        }

        lock (this._lifecycleSync)
        {
            if (this._started && this._pendingItems.NotifyUpdated(itemId, DateTimeOffset.UtcNow))
            {
                this.SignalWorker();
            }
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var now = DateTimeOffset.UtcNow;
                var readyItemIds = this._pendingItems.TakeReady(now, QuietPeriod, MaximumSettleTime);
                foreach (var itemId in readyItemIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await this.ProcessItemAsync(itemId, cancellationToken).ConfigureAwait(false);
                }

                var wait = this._pendingItems.TimeUntilNextReady(DateTimeOffset.UtcNow, QuietPeriod, MaximumSettleTime);
                await this._wakeSignal.WaitAsync(wait, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The hosted service is shutting down.
        }
    }

    private async Task ProcessItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        try
        {
            var configuration = Plugin.Instance?.Configuration;
            if (configuration?.EnforceNewItems != true)
            {
                return;
            }

            var validation = this._configurationValidator.Validate(configuration);
            if (!validation.IsValid)
            {
                this._logger.LogWarning("TagGuard skipped new item {ItemId}: {ValidationError}", itemId, validation.ErrorMessage);
                return;
            }

            var item = this._libraryManager.RetrieveItem(itemId);
            var managedLibraryIds = validation.ManagedLibraries.Select(static library => library.Id).ToHashSet();
            if (!IsManagedItem(item, managedLibraryIds))
            {
                return;
            }

            var result = await this._tagSanitizer.SanitizeAsync(item, configuration.AllowedTags, cancellationToken).ConfigureAwait(false);
            if (result.Changed)
            {
                this._logger.LogInformation("TagGuard sanitized new item {ItemId}; removed {TagsRemoved} tags and added Tags lock: {TagsLockAdded}", itemId, result.TagsRemoved, result.TagsLockAdded);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            this._logger.LogError(exception, "TagGuard failed to sanitize new item {ItemId}", itemId);
        }
    }

    private bool IsManagedItem(BaseItem item, IReadOnlySet<Guid> managedLibraryIds)
    {
        if (item is not Movie && item is not Series)
        {
            return false;
        }

        return this._libraryManager.GetCollectionFolders(item)
            .Any(folder => TagSanitizer.IsEligible(item, folder.Id, managedLibraryIds));
    }

    private void SignalWorker()
    {
        try
        {
            this._wakeSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Multiple library events can share the same pending wake-up.
        }
    }
}
