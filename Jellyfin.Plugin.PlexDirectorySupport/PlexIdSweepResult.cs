namespace Jellyfin.Plugin.PlexDirectorySupport;

/// <summary>Summary of a Plex token sweep.</summary>
public class PlexIdSweepResult
{
    /// <summary>Gets or sets the number of library items scanned.</summary>
    public int Scanned { get; set; }

    /// <summary>Gets or sets the number of movie/series items with Plex-style tokens.</summary>
    public int Tokened { get; set; }

    /// <summary>Gets or sets the number of provider IDs injected or overwritten.</summary>
    public int Injected { get; set; }

    /// <summary>Gets or sets the number of metadata refreshes completed.</summary>
    public int Refreshed { get; set; }

    /// <summary>Gets or sets the number of tokened items skipped because they were already handled.</summary>
    public int Skipped { get; set; }

    /// <summary>Gets or sets the number of failed metadata refreshes.</summary>
    public int Failed { get; set; }
}
