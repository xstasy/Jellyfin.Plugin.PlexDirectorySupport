using MediaBrowser.Model.Plugins;
using System.Collections.Generic;

namespace Jellyfin.Plugin.PlexDirectorySupport.Configuration;

/// <summary>
/// Plugin configuration for PlexDirectorySupport.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether to overwrite provider IDs
    /// that Jellyfin has already set from another source (e.g. NFO files).
    /// When false (default), existing IDs are preserved and only missing
    /// ones are filled in from the Plex-style folder tokens.
    /// </summary>
    public bool OverwriteExistingIds { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether the startup sweep should run.
    /// The startup sweep is conservative by default: it injects missing IDs but
    /// does not refresh metadata unless <see cref="RefreshOnStartup"/> is enabled.
    /// </summary>
    public bool EnableStartupSweep { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether startup should refresh metadata
    /// for tokened items that have not already completed a PlexDirectorySupport refresh.
    /// </summary>
    public bool RefreshOnStartup { get; set; } = false;

    /// <summary>
    /// Gets or sets the item/provider-id combinations that have already completed
    /// an automatic metadata refresh through this plugin.
    /// </summary>
    public List<string> QueuedRefreshKeys { get; set; } = new();
}
