namespace Jellyfin.Plugin.TagGuard.Services.Models;

/// <summary>
/// Describes the result of sanitizing one item.
/// </summary>
/// <param name="Changed">Whether the item was successfully persisted.</param>
/// <param name="TagsRemoved">The number of tags removed or collapsed as duplicates.</param>
/// <param name="TagsLockAdded">Whether the Tags metadata field was newly locked.</param>
/// <param name="WasSkipped">Whether no update was needed or another update for the item was already running.</param>
public sealed record TagSanitizationResult(bool Changed, int TagsRemoved, bool TagsLockAdded, bool WasSkipped)
{
    /// <summary>
    /// Gets the result used when an item needs no work or is already being processed.
    /// </summary>
    public static TagSanitizationResult Skipped { get; } = new(false, 0, false, true);
}
