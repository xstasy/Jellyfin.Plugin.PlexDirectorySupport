using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PlexDirectorySupport;

/// <summary>Shared Plex token sweep used by startup, post-scan, and manual actions.</summary>
public class PlexIdSweepService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<PlexIdSweepService> _logger;
    private readonly SemaphoreSlim _sweepLock = new(1, 1);

    /// <summary>Initialises a new instance of the <see cref="PlexIdSweepService"/> class.</summary>
    public PlexIdSweepService(
        ILibraryManager libraryManager,
        IProviderManager providerManager,
        IFileSystem fileSystem,
        ILogger<PlexIdSweepService> logger)
    {
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>Runs a Plex token sweep.</summary>
    public async Task<PlexIdSweepResult> RunAsync(
        string source,
        bool forceRefresh,
        bool refreshMetadata,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        await _sweepLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await RunInternalAsync(source, forceRefresh, refreshMetadata, progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sweepLock.Release();
        }
    }

    private async Task<PlexIdSweepResult> RunInternalAsync(
        string source,
        bool forceRefresh,
        bool refreshMetadata,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[PlexDirectorySupport] {Source}: scanning movie/series parent items for explicit Plex tokens. Refresh metadata: {RefreshMetadata}. Force refresh: {ForceRefresh}",
            source,
            refreshMetadata,
            forceRefresh);

        var result = new PlexIdSweepResult();
        var overwrite = Plugin.Instance?.Configuration.OverwriteExistingIds ?? false;
        var configurationChanged = false;

        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[]
            {
                BaseItemKind.Movie,
                BaseItemKind.Series,
            },
            IsVirtualItem = false,
            Recursive = true,
        };

        IReadOnlyList<BaseItem> items;
        try
        {
            items = _libraryManager.GetItemList(query);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PlexDirectorySupport] {Source}: library not ready", source);
            return result;
        }

        result.Scanned = items.Count;

        for (var index = 0; index < items.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(items.Count == 0 ? 100 : 100.0 * (index + 1) / items.Count);

            var item = items[index];
            if (string.IsNullOrWhiteSpace(item.Path))
            {
                continue;
            }

            var extracted = PlexIdParser.ExtractIds(item.Path);

            if (extracted.Count == 0)
            {
                continue;
            }

            result.Tokened++;
            var extractedSummary = string.Join(", ", extracted.Select(pair => $"{pair.Key}={pair.Value}"));

            var changed = false;
            foreach (var (key, value) in extracted)
            {
                var existing = item.GetProviderId(key);

                if (!string.IsNullOrWhiteSpace(existing) && !overwrite)
                {
                    continue;
                }

                if (string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _logger.LogInformation(
                    "[PlexDirectorySupport] {Source}: {Name} -> {Key}={Value}",
                    source,
                    item.Name,
                    key,
                    value);

                item.SetProviderId(key, value);
                changed = true;
                result.Injected++;
            }

            if (changed)
            {
                try
                {
                    await _libraryManager.UpdateItemAsync(
                        item,
                        item.GetParent(),
                        ItemUpdateType.MetadataEdit,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[PlexDirectorySupport] {Source}: failed to save {Name}", source, item.Name);
                    result.Failed++;
                    continue;
                }
            }

            if (!refreshMetadata)
            {
                result.Skipped++;
                continue;
            }

            var refreshKey = PlexIdRefresh.GetRefreshKey(item, extracted);
            if (!PlexIdRefresh.ShouldQueueRefresh(refreshKey, changed, forceRefresh))
            {
                result.Skipped++;
                continue;
            }

            try
            {
                _logger.LogInformation(
                    "[PlexDirectorySupport] {Source}: refreshing tokened item {Name} ({ProviderIds}) from {Path}",
                    source,
                    item.Name,
                    extractedSummary,
                    item.Path);

                await PlexIdRefresh.RefreshMetadataAsync(
                    item,
                    _providerManager,
                    _fileSystem,
                    _logger,
                    source,
                    cancellationToken).ConfigureAwait(false);

                configurationChanged |= PlexIdRefresh.MarkRefreshQueued(refreshKey);
                result.Refreshed++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PlexDirectorySupport] {Source}: metadata refresh failed for {Name}", source, item.Name);
                result.Failed++;
            }
        }

        if (configurationChanged)
        {
            Plugin.Instance?.SaveConfiguration();
        }

        _logger.LogInformation(
            "[PlexDirectorySupport] {Source} done. Scanned {Scanned}, tokened {Tokened}, injected {Injected}, refreshed {Refreshed}, skipped {Skipped}, failed {Failed}",
            source,
            result.Scanned,
            result.Tokened,
            result.Injected,
            result.Refreshed,
            result.Skipped,
            result.Failed);

        return result;
    }
}
