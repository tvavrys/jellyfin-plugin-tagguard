using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.TagGuard.Services.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.TagGuard.Services;

/// <summary>
/// Creates deterministic tag and locked-field changes for a Jellyfin item.
/// </summary>
public sealed class TagSanitizer
{
    /// <summary>
    /// Builds the sanitized tag list and lock list without modifying or persisting the item.
    /// </summary>
    /// <param name="currentTags">The item's current tags.</param>
    /// <param name="allowedTags">The configured tags to retain.</param>
    /// <param name="lockedFields">The item's currently locked metadata fields.</param>
    /// <returns>A plan describing the changes required.</returns>
    public TagSanitizationPlan CreatePlan(
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
