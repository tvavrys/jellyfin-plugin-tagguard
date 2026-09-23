using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.TagGuard.Services.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.TagGuard.Services;

/// <summary>
/// Creates deterministic tag and locked-field changes for a Jellyfin item.
/// </summary>
public sealed class TagSanitizer
{
    private readonly ILibraryManager _libraryManager;
    private readonly ConcurrentDictionary<Guid, byte> _activeItemIds = new();
    private readonly ConcurrentDictionary<Guid, byte> _persistingItemIds = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TagSanitizer"/> class.
    /// </summary>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    public TagSanitizer(ILibraryManager libraryManager)
    {
        this._libraryManager = libraryManager;
    }

    /// <summary>
    /// Builds the sanitized tag list and lock list without modifying or persisting the item.
    /// </summary>
    /// <param name="currentTags">The item's current tags.</param>
    /// <param name="allowedTags">The configured tags to retain.</param>
    /// <param name="lockedFields">The item's currently locked metadata fields.</param>
    /// <returns>A plan describing the changes required.</returns>
    public static TagSanitizationPlan CreatePlan(
        IEnumerable<string?>? currentTags,
        IEnumerable<string?>? allowedTags,
        IEnumerable<MetadataField>? lockedFields)
    {
        var originalTags = currentTags?.ToArray() ?? [];
        var allowedTagSpellings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var allowedTag in allowedTags ?? [])
        {
            var normalizedTag = Normalize(allowedTag);
            if (normalizedTag is not null)
            {
                allowedTagSpellings.TryAdd(normalizedTag, normalizedTag);
            }
        }

        var sanitizedTags = new List<string>(originalTags.Length);
        var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var currentTag in originalTags)
        {
            var normalizedTag = Normalize(currentTag);
            if (normalizedTag is not null
                && allowedTagSpellings.TryGetValue(normalizedTag, out var canonicalTag)
                && seenTags.Add(normalizedTag))
            {
                sanitizedTags.Add(canonicalTag);
            }
        }

        var tagsChanged = !originalTags.SequenceEqual(sanitizedTags.Select(static tag => (string?)tag), StringComparer.Ordinal);
        var resultingLockedFields = lockedFields?.ToList() ?? [];
        var tagsLockAdded = !resultingLockedFields.Contains(MetadataField.Tags);
        if (tagsLockAdded)
        {
            resultingLockedFields.Add(MetadataField.Tags);
        }

        return new TagSanitizationPlan(
            sanitizedTags,
            resultingLockedFields,
            originalTags.Length - sanitizedTags.Count,
            tagsChanged,
            tagsLockAdded);
    }

    /// <summary>
    /// Sanitizes an item and persists only the planned Tags and locked-field values.
    /// </summary>
    /// <param name="item">The Jellyfin item to sanitize.</param>
    /// <param name="allowedTags">The configured tags to retain.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A result describing the persisted change.</returns>
    public async Task<TagSanitizationResult> SanitizeAsync(
        BaseItem item,
        IEnumerable<string?> allowedTags,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(allowedTags);

        if (item is not Movie && item is not Series)
        {
            return TagSanitizationResult.Skipped;
        }

        if (!this._activeItemIds.TryAdd(item.Id, 0))
        {
            return TagSanitizationResult.Skipped;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var plan = CreatePlan(item.Tags, allowedTags, item.LockedFields);
            if (!plan.RequiresUpdate)
            {
                return TagSanitizationResult.Skipped;
            }

            var parent = item.ParentId == Guid.Empty ? null : this._libraryManager.GetItemById(item.ParentId);
            if (parent is null)
            {
                throw new InvalidOperationException($"Cannot persist TagGuard changes for item {item.Id}: the parent item could not be resolved.");
            }

            var originalTags = item.Tags;
            var originalLockedFields = item.LockedFields;
            item.Tags = plan.Tags.ToArray();
            item.LockedFields = plan.LockedFields.ToArray();

            this._persistingItemIds.TryAdd(item.Id, 0);
            try
            {
                await this._libraryManager.UpdateItemAsync(item, parent, ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                item.Tags = originalTags;
                item.LockedFields = originalLockedFields;
                throw;
            }
            finally
            {
                this._persistingItemIds.TryRemove(item.Id, out _);
            }

            return new TagSanitizationResult(true, plan.RemovedTagCount, plan.TagsLockAdded, false);
        }
        finally
        {
            this._activeItemIds.TryRemove(item.Id, out _);
        }
    }

    /// <summary>
    /// Determines whether an item update is currently being persisted by TagGuard.
    /// </summary>
    /// <param name="itemId">The item ID.</param>
    /// <returns><see langword="true"/> while TagGuard's library update is in progress.</returns>
    public bool IsPersistingUpdate(Guid itemId) => this._persistingItemIds.ContainsKey(itemId);

    /// <summary>
    /// Determines whether an item is a movie or series in a configured library.
    /// </summary>
    /// <param name="item">The Jellyfin item to inspect.</param>
    /// <param name="libraryId">The stable ID of the library containing the item.</param>
    /// <param name="managedLibraryIds">The configured library IDs.</param>
    /// <returns><see langword="true"/> when the item is supported and its library is configured.</returns>
    public static bool IsEligible(BaseItem item, Guid libraryId, IReadOnlySet<Guid> managedLibraryIds)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(managedLibraryIds);

        return (item is Movie or Series) && managedLibraryIds.Contains(libraryId);
    }

    private static string? Normalize(string? tag)
    {
        if (tag is null)
        {
            return null;
        }

        var normalizedTag = tag.Trim();
        return normalizedTag.Length == 0 ? null : normalizedTag;
    }
}
