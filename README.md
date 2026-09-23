# Jellyfin TagGuard

TagGuard keeps only approved tags on Movies and Series in selected Jellyfin libraries. It is useful when metadata providers add tags you do not want to use, for example in a parental-control workflow based on a small tag set such as `kids`.

> **Destructive operation:** TagGuard removes every tag that is not in the allowlist from eligible items in selected libraries. Start with a small test library and check the results before using it more broadly.

## Install

TagGuard currently provides a test catalog for Jellyfin **10.11.x**. In Jellyfin, open **Dashboard → Plugins → Repositories**, add this URL, then install TagGuard from the catalog and restart the server:

```text
https://raw.githubusercontent.com/tvavrys/jellyfin-plugin-tagguard/feature/tagguard-initial/manifest.json
```

This manifest is hosted on the project's feature branch; TagGuard is not listed in Jellyfin's official plugin catalog.

## Configure and use

In **Dashboard → Plugins → TagGuard**:

1. Select the libraries TagGuard should manage. Only Movies and Series in those libraries are eligible.
2. Add each permitted tag to the allowlist. For example, an allowlist containing only `kids` means all other tags are removed when TagGuard runs. TagGuard does not add `kids` to items automatically.
3. Save the settings and run **TagGuard library cleanup** from Jellyfin's Scheduled Tasks page to clean existing items. Review the task results and inspect a few items.
4. Optionally enable automatic processing for newly added Movies and Series.

For bulk tag editing, we recommend [JellyTag from Jellyfin Powertoys](https://github.com/lennykean/jellyfin-powertoys). Its context menu can add or remove tags on one item, and it supports selecting multiple items to edit their tags together. A practical workflow is to use TagGuard to clean provider tags, then use JellyTag to assign approved tags such as `kids` where needed.

TagGuard locks the Tags metadata field after cleanup to prevent metadata refreshes from restoring unwanted tags. Jellyfin 10.11 source indicates that the lock is persisted and honored by provider refreshes, while direct administrator tag edits remain possible. This has not yet been verified on a live server: sanitize a test item, restart Jellyfin, refresh its metadata, then use JellyTag to add an allowed tag and confirm the lock persists, provider tags stay absent, and the edit remains.

## What it does

- Compares tags case-insensitively, trims surrounding whitespace, removes duplicates, and keeps the allowlist's spelling.
- Preserves other locked metadata fields and ignores item types other than Movies and Series.
- Provides an on-demand scheduled cleanup task and optional one-time processing for new items after Jellyfin's metadata updates settle.
- Does not continuously poll or rescan the library. A new item is tracked only until it has been processed.

## Development

Requires the .NET 9 SDK.

```sh
dotnet restore Jellyfin.Plugin.TagGuard.slnx
dotnet build Jellyfin.Plugin.TagGuard.slnx --configuration Release
dotnet test Jellyfin.Plugin.TagGuard.slnx --configuration Release
dotnet publish Jellyfin.Plugin.TagGuard/Jellyfin.Plugin.TagGuard.csproj --configuration Release
```

The published plugin files are under `Jellyfin.Plugin.TagGuard/bin/Release/net9.0/publish/`. `build.yaml` records the plugin version, Jellyfin ABI, framework, and assembly used by the catalog package. Jellyfin's controller and model packages are compile-time API references; the server supplies those assemblies at runtime.

## Architecture

`PluginConfiguration` stores the allowlist, selected library IDs, and new-item option. `TagSanitizer` plans and persists tag and lock changes through Jellyfin's library API. `TagGuardCleanupTask` handles existing items; `NewItemEnforcementService` listens for add/update events and uses a bounded debounce queue for newly added items. TagGuard does not call Jellyfin over HTTP or write directly to its database.
