using System;
using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TagGuard.Configuration;

/// <summary>
/// TagGuard settings.
/// </summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        this.AllowedTags = [];
        this.ManagedLibraryIds = [];
    }

    /// <summary>
    /// Gets or sets the tags that TagGuard may keep.
    /// </summary>
    public List<string> AllowedTags { get; set; }

    /// <summary>
    /// Gets or sets the IDs of libraries managed by TagGuard.
    /// </summary>
    public List<Guid> ManagedLibraryIds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether new movies and series are sanitized automatically.
    /// </summary>
    public bool EnforceNewItems { get; set; }
}
