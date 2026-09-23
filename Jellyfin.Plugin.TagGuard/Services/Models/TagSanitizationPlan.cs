using System.Collections.Generic;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.TagGuard.Services.Models;

/// <summary>
/// The non-persisted result of planning tag sanitization for an item.
/// </summary>
/// <param name="Tags">The canonical tags to store.</param>
/// <param name="LockedFields">The locked fields to store, including Tags.</param>
/// <param name="RemovedTagCount">The number of input tags removed or collapsed as duplicates.</param>
/// <param name="TagsChanged">Whether the tag list differs from the input.</param>
/// <param name="TagsLockAdded">Whether the Tags metadata field was newly locked.</param>
public sealed record TagSanitizationPlan(
    IReadOnlyList<string> Tags,
    IReadOnlyList<MetadataField> LockedFields,
    int RemovedTagCount,
    bool TagsChanged,
    bool TagsLockAdded)
{
    /// <summary>
    /// Gets a value indicating whether persistence is needed.
    /// </summary>
    public bool RequiresUpdate => this.TagsChanged || this.TagsLockAdded;
}
