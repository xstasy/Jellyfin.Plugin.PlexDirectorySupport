using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.PlexDirectorySupport;

internal static class PlexIdRefresh
{
    private static MetadataRefreshOptions CreateRefreshOptions(IFileSystem fileSystem)
    {
        return new MetadataRefreshOptions(new DirectoryService(fileSystem))
        {
            MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
            ImageRefreshMode = MetadataRefreshMode.FullRefresh,
            ReplaceAllMetadata = true,
            ReplaceAllImages = true,
            ForceSave = true,
        };
    }

    internal static string GetRefreshKey(BaseItem item, IReadOnlyDictionary<string, string> providerIds)
    {
        var ids = string.Join(
            "|",
            providerIds
                .OrderBy(pair => pair.Key)
                .Select(pair => $"{pair.Key}:{pair.Value}"));

        return $"{item.Id:N}:{ids}";
    }

    internal static bool ShouldQueueRefresh(string refreshKey, bool providerIdsChanged, bool forceRefresh)
    {
        var configuration = Plugin.Instance?.Configuration;

        return forceRefresh
            || providerIdsChanged
            || configuration is null
            || configuration.QueuedRefreshKeys is null
            || !configuration.QueuedRefreshKeys.Contains(refreshKey);
    }

    internal static async Task RefreshMetadataAsync(
        BaseItem item,
        IProviderManager providerManager,
        IFileSystem fileSystem,
        ILogger logger,
        string source,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[PlexDirectorySupport] {Source}: refreshing metadata for {Name}",
            source,
            item.Name);

        await providerManager.RefreshFullItem(
            item,
            CreateRefreshOptions(fileSystem),
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "[PlexDirectorySupport] {Source}: metadata refresh completed for {Name}",
            source,
            item.Name);
    }

    internal static bool MarkRefreshQueued(string refreshKey)
    {
        var plugin = Plugin.Instance;

        if (plugin is null)
        {
            return false;
        }

        plugin.Configuration.QueuedRefreshKeys ??= new List<string>();

        if (plugin.Configuration.QueuedRefreshKeys.Contains(refreshKey))
        {
            return false;
        }

        plugin.Configuration.QueuedRefreshKeys.Add(refreshKey);
        return true;
    }
}
