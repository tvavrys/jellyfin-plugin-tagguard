# Jellyfin TagGuard

TagGuard keeps only explicitly approved values in the Jellyfin `Tags` metadata field for selected Movies and Series. Jellyfin metadata providers can populate that field with many unrelated tags; TagGuard makes a small, intentional set practical for workflows such as parental controls, where an administrator may maintain a tag such as `kids` using JellyTag or Jellyfin's metadata editor.

> **Destructive operation:** when TagGuard runs, every tag that is not in the configured allowlist is removed from eligible items in the selected libraries. Configure the allowlist and library selection carefully, and verify a small library before using it broadly.

## How it works

- **Allowlist:** only tags in the configured list survive. Matching is case-insensitive and trims surrounding whitespace. Surviving values use the allowlist's spelling/casing; duplicate and blank values are removed.
- **Selected libraries:** the administrator explicitly selects libraries by Jellyfin's stable collection-folder IDs. TagGuard does not apply to unselected libraries.
- **Manual cleanup:** a Jellyfin Scheduled Task can be run on demand to sanitize existing Movies and Series. It has no default periodic trigger and reports scan/change/removal/lock/skip/failure totals.
- **New items:** optional event-driven handling tracks a newly added Movie or Series only while its initial updates settle. An update resets a short quiet-period timer (4 seconds); a two-minute maximum prevents indefinite settling. The item is then sanitized once and removed from the pending set.
- **Tags lock:** after sanitation, TagGuard adds `MetadataField.Tags` to the item's locked metadata fields while preserving other locks. This is intended to stop later metadata-provider refreshes from restoring non-allowlisted tags.
- **No polling:** TagGuard does not periodically enumerate or rescan existing library items. Once processed, a new item is no longer tracked, so later intentional administrator/JellyTag edits are not re-sanitized.

Only Jellyfin Movie and Series items in selected libraries are eligible. Episodes, Seasons, music/audio, people, collections, playlists, and other item kinds are not modified.

## Compatibility

TagGuard targets Jellyfin's 10.11 plugin ABI (`10.11.0.0`) and .NET 9, using the official plugin-template package model with `Jellyfin.Controller` and `Jellyfin.Model` 10.11.5 references excluded at runtime. It is intended for Jellyfin 10.11.x servers that support this ABI; compatibility with other server ABIs is not claimed. The code has been source-checked against Jellyfin 10.11.9, but a live-server integration run is still required before treating restart/refresh behavior as operationally verified.

## Installation

For testing on Jellyfin 10.11.x, add this TagGuard repository in **Dashboard → Plugins → Repositories**:

```text
https://raw.githubusercontent.com/tvavrys/jellyfin-plugin-tagguard/feature/tagguard-initial/manifest.json
```

Then open the plugin catalog, install **TagGuard**, and restart Jellyfin. This is a test catalog hosted from the feature branch, not an entry in Jellyfin's official plugin repository. Its versioned ZIP and MD5 checksum are recorded in `manifest.json`.

There is not yet a published GitHub release. Official Jellyfin plugin-repository inclusion would be a separate future step.

For manual installation, extract a TagGuard plugin archive into a `TagGuard` subdirectory of Jellyfin's plugin data directory. Typical locations include:

- Linux packages: `/var/lib/jellyfin/plugins/TagGuard`
- Docker installations: the server's `/config/plugins/TagGuard` directory
- Windows: `%PROGRAMDATA%\Jellyfin\Server\plugins\TagGuard`

The directory should contain `Jellyfin.Plugin.TagGuard.dll`. Restart Jellyfin after installing or replacing the plugin. The release's notes should identify the supported Jellyfin ABI.

Until a release archive exists, build the plugin as described under [Development](#development) and copy `Jellyfin.Plugin.TagGuard.dll` from `Jellyfin.Plugin.TagGuard/bin/Release/net9.0/publish/` into a `TagGuard` subdirectory of the plugin directory, then restart the server.

## Configuration

Open **Dashboard → Plugins → TagGuard**. Add allowed tags with the input and **Add tag** button (or Enter), choose one or more Jellyfin libraries, and decide whether to enforce the policy for new Movies and Series. Tags can be removed from the list before saving. The selector reads Jellyfin's `GET /Library/VirtualFolders` API, whose `ItemId` values correspond to collection-folder IDs; the server also validates selected IDs before processing.

For example, an allowlist containing only:

```text
kids
```

means that every other tag is removed from managed Movies and Series when TagGuard runs. It does not add `kids` to every item; it only retains that tag when already present or when an administrator adds it later.

An empty allowlist or empty library selection is rejected by the configuration page. The cleanup task and new-item handler also validate configuration against Jellyfin's current libraries before making changes, so stale/missing library IDs do not broaden the operation.

## Recommended initial workflow

1. Install TagGuard and restart Jellyfin.
2. Select the Movies and Shows libraries TagGuard should manage.
3. Configure allowed tags such as `kids`.
4. Run **TagGuard library cleanup** manually once from Jellyfin's Scheduled Tasks dashboard.
5. Verify the results on representative items and confirm the Tags lock is present.
6. Enable automatic new-item enforcement if desired.
7. Continue using JellyTag or Jellyfin's metadata editor to assign the allowed tag to items that need it.

Do not enable automatic enforcement until the allowlist and library scope are correct. Manual cleanup remains available whether automatic processing is enabled or disabled.

## Lock behavior and verification status

The implementation is based on Jellyfin 10.11 source behavior:

- `BaseItemRepository` includes the locked-field relation when settings are loaded and writes locked fields to the metadata-field table during item persistence ([Jellyfin 10.11.9 source](https://github.com/jellyfin/jellyfin/blob/v10.11.9/Jellyfin.Server.Implementations/Item/BaseItemRepository.cs#L447-L450), [persistence](https://github.com/jellyfin/jellyfin/blob/v10.11.9/Jellyfin.Server.Implementations/Item/BaseItemRepository.cs#L658-L671)).
- `MetadataService` skips replacing or merging Tags when `MetadataField.Tags` is locked ([Jellyfin 10.11.9 source](https://github.com/jellyfin/jellyfin/blob/v10.11.9/MediaBrowser.Providers/Manager/MetadataService.cs#L1125-L1134)).
- Jellyfin's metadata update endpoint assigns the explicitly submitted tag list to the item independently of the Tags lock. The lock check applies when propagating those edits from a Series to child Seasons/Episodes, not to the deliberate edit on the Series itself ([Jellyfin 10.11.9 source](https://github.com/jellyfin/jellyfin/blob/v10.11.9/Jellyfin.Api/Controllers/ItemUpdateController.cs#L286-L290), [lock update](https://github.com/jellyfin/jellyfin/blob/v10.11.9/Jellyfin.Api/Controllers/ItemUpdateController.cs#L391-L394)).

This supports the intended behavior: provider refreshes honor the field lock, while direct administrator edits to that item's Tags remain possible. The source also represents the lock as persisted item metadata, but TagGuard has **not yet been tested against a running Jellyfin server across a restart and metadata refresh**. Server plugins or alternate tag-editing flows may differ, so verify the behavior on the exact server/JellyTag versions in use before relying on it for parental controls.

Manual integration check before broad use:

1. Use a test library with one Movie and one Series; configure one allowed tag (for example `kids`) and run TagGuard's manual task.
2. Confirm non-allowlisted tags are gone, an existing allowed tag remains with configured spelling, and the Tags field is locked without removing unrelated locks.
3. Restart Jellyfin, reopen each item, and confirm the Tags lock is still present.
4. Refresh metadata for a test item and confirm provider tags do not return.
5. After sanitation, add the allowlisted tag through the Jellyfin metadata editor and through the JellyTag workflow you intend to use. Reload the item and confirm the edit persists; then repeat a metadata refresh and confirm unrelated tags remain absent.

## Development

Requirements: .NET 9 SDK.

```sh
dotnet restore Jellyfin.Plugin.TagGuard.slnx
dotnet build Jellyfin.Plugin.TagGuard.slnx --configuration Release
dotnet test Jellyfin.Plugin.TagGuard.slnx --configuration Release
dotnet publish Jellyfin.Plugin.TagGuard/Jellyfin.Plugin.TagGuard.csproj --configuration Release
```

The solution includes automated tests for tag normalization and allowlist planning, library/item eligibility, idempotence, locked-field preservation, persistence outcomes, and the bounded pending-item debounce queue. CI runs restore, build, and tests on .NET 9.

`build.yaml` records the plugin GUID, version, ABI, framework, and assembly artifact using Jellyfin plugin-template conventions. Jellyfin's controller/model packages are compile-time API references and are excluded from the plugin runtime output; the server provides its own shared Jellyfin assemblies. Do not bundle copies of those server assemblies. A future release workflow/catalog submission should package the plugin DLL with this manifest and keep its advertised ABI aligned with the server API packages used for the build.

## Architecture

- `PluginConfiguration` stores allowed tags, selected library IDs, and the new-item toggle; the validator resolves IDs against Jellyfin's current collection folders and fails closed.
- `TagSanitizer` contains the shared deterministic tag/lock plan and persists only `Tags` and `LockedFields` through Jellyfin's `ILibraryManager.UpdateItemAsync` API.
- `TagGuardCleanupTask` paginates through eligible Movies and Series in the selected libraries and isolates individual item failures.
- `NewItemEnforcementService` subscribes to `ItemAdded` and `ItemUpdated`, uses a bounded in-memory pending set and debounce worker, and unsubscribes/cancels on shutdown.
- Jellyfin's item persistence and metadata-provider merge logic handle storage and refresh behavior; TagGuard does not make HTTP calls to the local server or write directly to its database.
