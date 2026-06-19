using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PlexDirectorySupport;

/// <summary>
/// Registers <see cref="PlexIdStartupService"/> as a hosted background service
/// so it runs once when the Jellyfin server starts.
/// </summary>
public class PlexIdServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<PlexIdSweepService>();
        serviceCollection.AddSingleton<PlexIdDeferredRefreshService>();
        serviceCollection.AddHostedService<PlexIdStartupService>();
    }
}

/// <summary>
/// Runs once at server startup and injects Plex provider IDs into any items
/// that have Plex tokens in their path but are still missing those IDs.
///
/// This handles the common case of installing the plugin into an existing library:
/// items that Jellyfin has already scanned (so the custom metadata providers never
/// ran for them) get their IDs populated without needing a full library re-scan.
///
/// A small delay is added before querying the library to allow the Jellyfin
/// database and library manager to fully initialise after startup.
///
/// Only movie and series parent items are processed. Episode IDs are distinct
/// from series IDs, so folder-level TVDB/TMDB tokens must not be copied onto
/// every episode.
/// </summary>
public class PlexIdStartupService : BackgroundService
{
    private readonly PlexIdSweepService _sweepService;
    private readonly ILogger<PlexIdStartupService> _logger;

    /// <summary>Initialises the service via DI.</summary>
    public PlexIdStartupService(
        PlexIdSweepService sweepService,
        ILogger<PlexIdStartupService> logger)
    {
        _sweepService = sweepService;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var assembly = typeof(PlexIdStartupService).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";

        _logger.LogInformation(
            "[PlexDirectorySupport] Startup sweep service started. Version {Version}, loaded from {Location}",
            version,
            assembly.Location);

        // Jellyfin's library manager isn't guaranteed to be fully loaded the
        // instant the hosted service starts. A short delay lets everything settle.
        // This is the same pattern used by other Jellyfin plugins (e.g. Trakt).
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ConfigureAwait(false);

        var configuration = Plugin.Instance?.Configuration;
        if (configuration?.EnableStartupSweep == false)
        {
            _logger.LogInformation("[PlexDirectorySupport] Startup sweep disabled by configuration");
            return;
        }

        await _sweepService.RunAsync(
            "Startup",
            forceRefresh: false,
            refreshMetadata: configuration?.RefreshOnStartup ?? false,
            progress: null,
            stoppingToken).ConfigureAwait(false);
    }
}
