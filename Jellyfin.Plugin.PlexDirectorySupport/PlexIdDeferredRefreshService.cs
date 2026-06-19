using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PlexDirectorySupport;

/// <summary>
/// Schedules a delayed metadata refresh after live provider-id injection.
/// </summary>
public class PlexIdDeferredRefreshService
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(10);

    private readonly IProviderManager _providerManager;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<PlexIdDeferredRefreshService> _logger;
    private readonly HashSet<string> _pendingRefreshKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncLock = new();

    /// <summary>Initialises a new instance of the <see cref="PlexIdDeferredRefreshService"/> class.</summary>
    public PlexIdDeferredRefreshService(
        IProviderManager providerManager,
        IFileSystem fileSystem,
        ILogger<PlexIdDeferredRefreshService> logger)
    {
        _providerManager = providerManager;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>Schedules a delayed refresh if this item/id set has not already been handled.</summary>
    public void Schedule(BaseItem item, IReadOnlyDictionary<string, string> providerIds)
    {
        var refreshKey = PlexIdRefresh.GetRefreshKey(item, providerIds);

        if (!PlexIdRefresh.ShouldQueueRefresh(refreshKey, providerIdsChanged: false, forceRefresh: false))
        {
            return;
        }

        var providerSummary = string.Join(", ", providerIds.Select(pair => $"{pair.Key}={pair.Value}"));

        lock (_syncLock)
        {
            if (!_pendingRefreshKeys.Add(refreshKey))
            {
                return;
            }
        }

        _logger.LogInformation(
            "[PlexDirectorySupport] Live provider: scheduled delayed metadata refresh for {Name} ({ProviderIds}) from {Path} in {DelaySeconds} seconds",
            item.Name,
            providerSummary,
            item.Path,
            Delay.TotalSeconds);

        _ = RefreshAfterDelayAsync(item, refreshKey);
    }

    private async Task RefreshAfterDelayAsync(BaseItem item, string refreshKey)
    {
        try
        {
            await Task.Delay(Delay).ConfigureAwait(false);

            await PlexIdRefresh.RefreshMetadataAsync(
                item,
                _providerManager,
                _fileSystem,
                _logger,
                "Live provider delayed refresh",
                CancellationToken.None).ConfigureAwait(false);

            if (PlexIdRefresh.MarkRefreshQueued(refreshKey))
            {
                Plugin.Instance?.SaveConfiguration();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PlexDirectorySupport] Live provider: delayed metadata refresh failed for {Name}", item.Name);
        }
        finally
        {
            lock (_syncLock)
            {
                _pendingRefreshKeys.Remove(refreshKey);
            }
        }
    }
}
