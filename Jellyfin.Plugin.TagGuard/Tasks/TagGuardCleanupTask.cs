using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.TagGuard.Configuration;
using Jellyfin.Plugin.TagGuard.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TagGuard.Tasks;

/// <summary>
/// Manually sanitizes tags on the selected movie and series libraries.
/// </summary>
public sealed class TagGuardCleanupTask : IScheduledTask
{
    private const int PageSize = 100;
    private readonly ILogger<TagGuardCleanupTask> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly TagGuardConfigurationValidator _configurationValidator;
    private readonly TagSanitizer _tagSanitizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TagGuardCleanupTask"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    /// <param name="configurationValidator">The configuration validator.</param>
    /// <param name="tagSanitizer">The tag sanitizer.</param>
    public TagGuardCleanupTask(
        ILogger<TagGuardCleanupTask> logger,
        ILibraryManager libraryManager,
        TagGuardConfigurationValidator configurationValidator,
        TagSanitizer tagSanitizer)
    {
        this._logger = logger;
        this._libraryManager = libraryManager;
        this._configurationValidator = configurationValidator;
        this._tagSanitizer = tagSanitizer;
    }

    /// <inheritdoc />
    public string Name => "TagGuard library cleanup";

    /// <inheritdoc />
    public string Key => "TagGuardCleanup";

    /// <inheritdoc />
    public string Description => "Keep only the configured allowed tags on selected movie and series libraries.";

    /// <inheritdoc />
    public string Category => "Library maintenance";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);

        long itemsScanned = 0;
        long itemsChanged = 0;
        long tagsRemoved = 0;
        long tagsLocked = 0;
        long skippedItems = 0;
        long failures = 0;

        try
        {
            var configuration = Plugin.Instance?.Configuration;
            var validation = this._configurationValidator.Validate(configuration);
            if (!validation.IsValid)
            {
                this._logger.LogError("TagGuard cleanup was not started: {ValidationError}", validation.ErrorMessage);
                throw new InvalidOperationException(validation.ErrorMessage);
            }

            var validConfiguration = configuration!;
            var managedLibraryIds = validation.ManagedLibraries.Select(static library => library.Id).ToHashSet();
            var query = new InternalItemsQuery
            {
                TopParentIds = managedLibraryIds.ToArray(),
                IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
                SourceTypes = [SourceType.Library],
                IsVirtualItem = false,
                Recursive = true,
                Limit = PageSize,
                EnableTotalRecordCount = true
            };
            var offset = 0;
            var totalItems = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                query.StartIndex = offset;
                var result = this._libraryManager.GetItemsResult(query);
                totalItems = result.TotalRecordCount;
                if (result.Items.Count == 0)
                {
                    break;
                }

                foreach (var item in result.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    itemsScanned++;
                    try
                    {
                        if ((item is not Movie && item is not Series)
                            || !this._libraryManager.GetCollectionFolders(item).Any(folder => managedLibraryIds.Contains(folder.Id)))
                        {
                            skippedItems++;
                            continue;
                        }

                        var sanitizeResult = await this._tagSanitizer
                            .SanitizeAsync(item, validConfiguration.AllowedTags, cancellationToken)
                            .ConfigureAwait(false);
                        if (sanitizeResult.Changed)
                        {
                            itemsChanged++;
                            tagsRemoved += sanitizeResult.TagsRemoved;
                            if (sanitizeResult.TagsLockAdded)
                            {
                                tagsLocked++;
                            }
                        }
                        else
                        {
                            skippedItems++;
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        failures++;
                        this._logger.LogError(exception, "TagGuard failed to sanitize item {ItemId} ({ItemName})", item.Id, item.Name);
                    }
                }

                offset += result.Items.Count;
                var taskProgress = totalItems == 0 ? 100d : 100d * Math.Min((double)offset / totalItems, 1d);
                progress.Report(taskProgress);
                if (offset >= totalItems)
                {
                    break;
                }
            }

            progress.Report(100d);
        }
        finally
        {
            this._logger.LogInformation(
                "TagGuard cleanup finished. Items scanned: {ItemsScanned}; items changed: {ItemsChanged}; tags removed: {TagsRemoved}; Tags locks added: {TagsLocked}; skipped items: {SkippedItems}; failures: {Failures}",
                itemsScanned,
                itemsChanged,
                tagsRemoved,
                tagsLocked,
                skippedItems,
                failures);
        }
    }
}
