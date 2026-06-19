using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PlexDirectorySupport;

/// <summary>
/// Shared logic for all ICustomMetadataProvider implementations.
/// Extracts Plex-style tokens from an item's path and injects them
/// as Jellyfin provider IDs.
/// </summary>
internal static class PlexIdProviderCore
{
    internal static Task<ItemUpdateType> FetchAsync<T>(
        T item,
        ILogger logger,
        PlexIdDeferredRefreshService deferredRefreshService,
        bool overwrite,
        CancellationToken cancellationToken)
        where T : BaseItem
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(item.Path))
        {
            return Task.FromResult(ItemUpdateType.None);
        }

        var extracted = PlexIdParser.ExtractIds(item.Path);

        if (extracted.Count == 0)
        {
            return Task.FromResult(ItemUpdateType.None);
        }

        var changed = false;

        foreach (var (key, value) in extracted)
        {
            var existing = item.GetProviderId(key);

            if (!string.IsNullOrWhiteSpace(existing) && !overwrite)
            {
                logger.LogDebug(
                    "[PlexDirectorySupport] {Name}: {Key} already set ({Existing}), skipping",
                    item.Name, key, existing);
                continue;
            }

            if (string.Equals(existing, value, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            logger.LogInformation(
                "[PlexDirectorySupport] {Name}: injecting {Key}={Value}",
                item.Name, key, value);

            item.SetProviderId(key, value);
            changed = true;
        }

        if (changed)
        {
            deferredRefreshService.Schedule(item, extracted);
        }

        return Task.FromResult(changed ? ItemUpdateType.MetadataEdit : ItemUpdateType.None);
    }
}

/// <summary>Injects Plex provider ID tokens for <see cref="Movie"/> items.</summary>
public class PlexIdMovieProvider : ICustomMetadataProvider<Movie>
{
    private readonly ILogger<PlexIdMovieProvider> _logger;
    private readonly PlexIdDeferredRefreshService _deferredRefreshService;

    /// <inheritdoc />
    public string Name => "Plex Directory Support";

    /// <summary>Initialises the provider.</summary>
    public PlexIdMovieProvider(
        ILogger<PlexIdMovieProvider> logger,
        PlexIdDeferredRefreshService deferredRefreshService)
    {
        _logger = logger;
        _deferredRefreshService = deferredRefreshService;
    }

    /// <inheritdoc />
    public Task<ItemUpdateType> FetchAsync(Movie item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        var overwrite = Plugin.Instance?.Configuration.OverwriteExistingIds ?? false;
        return PlexIdProviderCore.FetchAsync(item, _logger, _deferredRefreshService, overwrite, cancellationToken);
    }
}

/// <summary>Injects Plex provider ID tokens for <see cref="Series"/> items.</summary>
public class PlexIdSeriesProvider : ICustomMetadataProvider<Series>
{
    private readonly ILogger<PlexIdSeriesProvider> _logger;
    private readonly PlexIdDeferredRefreshService _deferredRefreshService;

    /// <inheritdoc />
    public string Name => "Plex Directory Support";

    /// <summary>Initialises the provider.</summary>
    public PlexIdSeriesProvider(
        ILogger<PlexIdSeriesProvider> logger,
        PlexIdDeferredRefreshService deferredRefreshService)
    {
        _logger = logger;
        _deferredRefreshService = deferredRefreshService;
    }

    /// <inheritdoc />
    public Task<ItemUpdateType> FetchAsync(Series item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        var overwrite = Plugin.Instance?.Configuration.OverwriteExistingIds ?? false;
        return PlexIdProviderCore.FetchAsync(item, _logger, _deferredRefreshService, overwrite, cancellationToken);
    }
}
