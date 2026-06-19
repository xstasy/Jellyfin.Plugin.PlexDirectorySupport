# Jellyfin.Plugin.PlexDirectorySupport

A Jellyfin plugin that reads **Plex-style provider ID tokens** from folder and file names and injects them into Jellyfin's own provider ID store — so you can run Plex and Jellyfin against the same media library **without renaming anything**.

## What it does

Plex embeds provider IDs in curly braces, e.g.:

```
/media/movies/The Batman (2022) {tmdb-414906}/The Batman (2022).mkv
/media/shows/Breaking Bad (2008) {tvdb-81189}/Season 01/S01E01.mkv
/media/movies/Inception (2010) {imdb-tt1375666}.mkv
```

Jellyfin uses square brackets (`[tmdbid-…]`) which Plex ignores, but Plex's curly-brace format is completely invisible to Jellyfin out of the box. This plugin bridges that gap for movie and series parent items via three complementary mechanisms:

### 1. `ICustomMetadataProvider` — live injection during metadata fetch

Fires automatically as part of Jellyfin's normal metadata pipeline every time an item is refreshed (including the very first time a newly-added item is identified). The provider runs *before* remote providers (TMDB, TVDB) look up the item, so they receive the injected IDs and fetch the right record immediately.

### 2. `IHostedService` startup sweep — catch existing tokened items

Runs once, 15 seconds after Jellyfin starts. Scans movie and series parent items for explicit Plex tokens in the item path or direct parent folder. By default it only injects missing IDs; metadata refresh during startup can be enabled from the plugin settings page.

### 3. `ILibraryPostScanTask` — mop-up after every scan

Runs at the end of every library scan. Only processes movie and series parent items where an explicit Plex token exists in the item path or direct parent folder.

### Supported tokens

| Plex token        | Jellyfin key |
|-------------------|--------------|
| `{tmdb-<id>}`     | `Tmdb`       |
| `{tvdb-<id>}`     | `Tvdb`       |
| `{imdb-<id>}`     | `Imdb`       |

Multiple tokens in one name are supported: `Show Name (2020) {tvdb-12345} {tmdb-67890}/`

---

## Building

### Prerequisites

- [.NET SDK 9.0+](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

### 1. Match the NuGet version to your Jellyfin server

Open `Jellyfin.Plugin.PlexDirectorySupport/Jellyfin.Plugin.PlexDirectorySupport.csproj` and set the
`Jellyfin.Controller` / `Jellyfin.Model` versions to match your server:

| Jellyfin server | NuGet version |
|-----------------|---------------|
| 10.9.x          | 10.9.11       |
| 10.10.x         | 10.10.7       |
| 10.11.x         | 10.11.11      |

See https://www.nuget.org/packages/Jellyfin.Controller for the latest.

### 2. Build

```bash
dotnet publish Jellyfin.Plugin.PlexDirectorySupport/Jellyfin.Plugin.PlexDirectorySupport.csproj \
  -c Release -o ./publish
```

### 3. Install

```bash
# Linux (typical data path)
mkdir -p ~/.local/share/jellyfin/plugins/PlexDirectorySupport
cp publish/Jellyfin.Plugin.PlexDirectorySupport.dll ~/.local/share/jellyfin/plugins/PlexDirectorySupport/

# Docker — adjust to your volume mount
cp publish/Jellyfin.Plugin.PlexDirectorySupport.dll /path/to/jellyfin/config/plugins/PlexDirectorySupport/
```

Restart Jellyfin. The startup sweep runs automatically ~15 seconds after boot and injects missing IDs for tokened movie and series parent items. New items are handled live as they are discovered and refreshed.

---

## Configuration

**Dashboard → Plugins → Plex Directory Support**

| Setting | Default | Description |
|---------|---------|-------------|
| **Overwrite Existing IDs** | `false` | When `false`, IDs already set (e.g. from NFO files) are left alone. Set to `true` to let Plex tokens always win. |
| **Run token sweep at startup** | `true` | Scans tokened movie and series parent items on Jellyfin startup. |
| **Refresh metadata during startup sweep** | `false` | When enabled, startup also refreshes metadata for tokened items that have not already completed a PlexDirectorySupport refresh for the same IDs. |

The settings page also provides:

| Action | Description |
|--------|-------------|
| **Run Normal Sweep** | Injects missing IDs and refreshes tokened items that have not already completed a PlexDirectorySupport refresh for the same IDs. |
| **Run Force Sweep** | Refreshes every tokened movie and series parent item, including items already handled before. |

---

## What triggers each mechanism

| Situation | What fires |
|-----------|-----------|
| New item added to library | `ICustomMetadataProvider` (live, during metadata fetch) |
| Plugin installed into existing library | `IHostedService` startup sweep (15s after boot) |
| Library scan run manually | `ILibraryPostScanTask` (end of scan, tokened parent items only) |
| Item metadata manually refreshed | `ICustomMetadataProvider` (live) |

---

## Non-destructive by design

- Folder and file names are **never modified**
- Only writes to Jellyfin's internal database
- Existing provider IDs are preserved by default (`OverwriteExistingIds = false`)
- Already-identified items are skipped in the startup sweep and post-scan task

---

## Troubleshooting

**Tokens not being picked up?**  
Check **Dashboard → Logs** and filter for `PlexDirectorySupport`. The parser checks the file name, the immediate parent folder, and the grandparent folder (two levels up), which covers all common Plex layouts for both movies and series.

**Plugin shows as "Not Supported"?**  
The NuGet version in the `.csproj` must exactly match your Jellyfin server version.

---

## License

GPL-3.0 (required when linking against Jellyfin NuGet packages).
