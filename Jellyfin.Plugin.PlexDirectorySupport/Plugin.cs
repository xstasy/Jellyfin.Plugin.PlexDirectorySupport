using System;
using System.Collections.Generic;
using Jellyfin.Plugin.PlexDirectorySupport.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.PlexDirectorySupport;

/// <summary>
/// Plugin entry point for PlexDirectorySupport.
/// Teaches Jellyfin to recognise Plex-style provider ID tokens in folder/file names,
/// e.g. {tmdb-12345}, {tvdb-79168}, {imdb-tt1234567}.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>Singleton accessor used by other plugin classes.</summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Plex Directory Support";

    /// <inheritdoc />
    public override string Description =>
        "Reads Plex-style provider ID tokens ({tmdb-…}, {tvdb-…}, {imdb-…}) from " +
        "folder and file names and injects them as Jellyfin provider IDs so that " +
        "both Plex and Jellyfin can coexist on the same media library without renaming.";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("d7a3f1c2-4b5e-6d7f-8e9a-0b1c2d3e4f50");

    /// <summary>
    /// Initialises the plugin.
    /// </summary>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "configPage",
                DisplayName = Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html",
                EnableInMainMenu = false,
            },
        };
    }
}
