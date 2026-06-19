using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.PlexDirectorySupport;

/// <summary>
/// Parses Plex-style provider ID tokens from a file system path.
///
/// Plex embeds IDs in curly braces anywhere in a folder or file name:
///   The Batman (2022) {tmdb-414906}/
///   Breaking Bad (2008) {tvdb-81189}/
///   Inception (2010) {imdb-tt1375666}.mkv
///
/// Multiple tokens in a single name are also supported:
///   Show Name (2020) {tvdb-12345} {tmdb-67890}/
/// </summary>
public static partial class PlexIdParser
{
    // Matches {tmdb-12345}, {tvdb-79168}, {imdb-tt1234567}
    // The provider name is captured in group 1, the ID value in group 2.
    // We accept any alphanumeric ID value so that IMDB "tt" prefixes work too.
    [GeneratedRegex(
        @"\{(?<provider>tmdb|tvdb|imdb)-(?<id>[a-zA-Z0-9]+)\}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PlexTokenRegex();

    /// <summary>
    /// Maps the lower-case Plex provider name to the Jellyfin provider key
    /// expected by <c>IHasProviderIds.SetProviderId</c>.
    /// </summary>
    private static readonly Dictionary<string, string> ProviderKeyMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["tmdb"] = "Tmdb",
            ["tvdb"] = "Tvdb",
            ["imdb"] = "Imdb",
        };

    /// <summary>
    /// Extracts all Plex provider ID tokens found in <paramref name="path"/>.
    /// The path is inspected segment by segment so tokens in either the folder
    /// name or the file name are picked up.
    /// </summary>
    /// <param name="path">Absolute file-system path of the item.</param>
    /// <returns>
    /// A dictionary mapping Jellyfin provider key → ID value.
    /// Empty when no tokens are found.
    /// </returns>
    public static Dictionary<string, string> ExtractIds(string path)
    {
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(path))
        {
            return results;
        }

        // Check only the current item path and its direct parent. The plugin
        // only processes movie and series parent items, so walking higher can
        // accidentally treat unrelated library folders as item identifiers.
        // Direct parent lookup still covers the common folder-per-movie shape:
        //   /media/movies/The Batman (2022) {tmdb-414906}/The Batman (2022).mkv
        //   /media/shows/Breaking Bad (2008) {tvdb-81189}/
        var segments = new[]
        {
            Path.GetFileNameWithoutExtension(path),
            Path.GetFileName(Path.GetDirectoryName(path)) ?? string.Empty,
        };

        foreach (var segment in segments)
        {
            foreach (Match match in PlexTokenRegex().Matches(segment))
            {
                var plexKey = match.Groups["provider"].Value.ToLowerInvariant();
                var idValue = match.Groups["id"].Value;

                if (ProviderKeyMap.TryGetValue(plexKey, out var jellyfinKey)
                    && !results.ContainsKey(jellyfinKey))
                {
                    results[jellyfinKey] = idValue;
                }
            }
        }

        return results;
    }
}
