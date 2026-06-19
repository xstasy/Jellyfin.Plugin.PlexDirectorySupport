using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PlexDirectorySupport;

/// <summary>
/// Runs after every library scan and injects Plex-style provider IDs for items
/// that have Plex tokens in their path but are still missing those IDs in Jellyfin.
///
/// This task is intentionally narrow: it only processes items where the path
/// contains a Plex token AND Jellyfin is still missing the corresponding provider ID.
/// Items that were already handled by the custom metadata providers during their
/// normal metadata fetch are skipped immediately, keeping the post-scan pass fast.
///
/// Typical scenarios where this task does real work:
///   - First install: existing library items that Jellyfin has never re-fetched
///   - Items that were added before the plugin was installed
///   - Items locked in Jellyfin (metadata refresh skipped them)
///
/// Only movie and series parent items are processed. Episode IDs are distinct
/// from series IDs, so folder-level TVDB/TMDB tokens must not be copied onto
/// every episode.
/// </summary>
public class PlexIdPostScanTask : ILibraryPostScanTask
{
    private readonly PlexIdSweepService _sweepService;
    private readonly ILogger<PlexIdPostScanTask> _logger;

    /// <summary>Initialises the scan task via DI.</summary>
    public PlexIdPostScanTask(
        PlexIdSweepService sweepService,
        ILogger<PlexIdPostScanTask> logger)
    {
        _sweepService = sweepService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Run(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[PlexDirectorySupport] Post-scan sweep requested");
        await _sweepService.RunAsync(
            "Post-scan",
            forceRefresh: false,
            refreshMetadata: true,
            progress,
            cancellationToken).ConfigureAwait(false);
    }
}
