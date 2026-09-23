using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.TagGuard.Configuration;

/// <summary>
/// Validates TagGuard settings against the libraries currently known to Jellyfin.
/// </summary>
public sealed class TagGuardConfigurationValidator
{
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TagGuardConfigurationValidator"/> class.
    /// </summary>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    public TagGuardConfigurationValidator(ILibraryManager libraryManager)
    {
        this._libraryManager = libraryManager;
    }

    /// <summary>
    /// Validates configuration against Jellyfin's current collection folders.
    /// </summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <returns>The validation result.</returns>
    public PluginConfigurationValidationResult Validate(PluginConfiguration? configuration)
    {
        var currentLibraries = this._libraryManager.GetUserRootFolder().Children.OfType<CollectionFolder>().ToArray();
        return Validate(configuration, currentLibraries);
    }

    /// <summary>
    /// Validates configuration against a supplied collection-folder list.
    /// </summary>
    /// <param name="configuration">The configuration to validate.</param>
    /// <param name="availableLibraries">The collection folders currently available.</param>
    /// <returns>The validation result.</returns>
    public static PluginConfigurationValidationResult Validate(
        PluginConfiguration? configuration,
        IEnumerable<CollectionFolder> availableLibraries)
    {
        ArgumentNullException.ThrowIfNull(availableLibraries);

        if (configuration is null)
        {
            return PluginConfigurationValidationResult.Invalid("TagGuard configuration is unavailable.");
        }

        if (!(configuration.AllowedTags ?? []).Any(static tag => !string.IsNullOrWhiteSpace(tag)))
        {
            return PluginConfigurationValidationResult.Invalid("Configure at least one non-empty allowed tag before running TagGuard.");
        }

        var selectedLibraryIds = (configuration.ManagedLibraryIds ?? []).Distinct().ToArray();
        if (selectedLibraryIds.Length == 0 || selectedLibraryIds.Contains(Guid.Empty))
        {
            return PluginConfigurationValidationResult.Invalid("Select at least one valid Jellyfin library before running TagGuard.");
        }

        var currentLibraries = availableLibraries.ToArray();
        var currentLibraryIds = currentLibraries.Select(static library => library.Id).ToHashSet();
        var missingLibraryIds = selectedLibraryIds.Where(id => !currentLibraryIds.Contains(id)).ToArray();
        if (missingLibraryIds.Length > 0)
        {
            return PluginConfigurationValidationResult.Invalid("One or more selected libraries no longer exist. Review the TagGuard library selection before running it.");
        }

        var selectedLibraries = currentLibraries
            .Where(library => selectedLibraryIds.Contains(library.Id))
            .ToArray();
        return PluginConfigurationValidationResult.Valid(selectedLibraries);
    }
}
