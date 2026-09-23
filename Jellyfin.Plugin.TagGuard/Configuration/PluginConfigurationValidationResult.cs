using System.Collections.Generic;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.TagGuard.Configuration;

/// <summary>
/// The result of validating TagGuard's destructive-operation settings.
/// </summary>
/// <param name="IsValid">Whether the configuration can be used safely.</param>
/// <param name="ErrorMessage">A message describing invalid configuration, if any.</param>
/// <param name="ManagedLibraries">The resolved libraries selected by the configuration.</param>
public sealed record PluginConfigurationValidationResult(
    bool IsValid,
    string? ErrorMessage,
    IReadOnlyList<CollectionFolder> ManagedLibraries)
{
    /// <summary>
    /// Creates an invalid validation result.
    /// </summary>
    /// <param name="message">The validation error.</param>
    /// <returns>The invalid result.</returns>
    public static PluginConfigurationValidationResult Invalid(string message) => new(false, message, []);

    /// <summary>
    /// Creates a valid validation result with the resolved selected libraries.
    /// </summary>
    /// <param name="libraries">The configured libraries.</param>
    /// <returns>The valid result.</returns>
    public static PluginConfigurationValidationResult Valid(IReadOnlyList<CollectionFolder> libraries) => new(true, null, libraries);
}
