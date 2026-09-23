# Project context

## Goal and scope

TagGuard is a Jellyfin plugin that keeps only explicitly allowed tags on configured movie and series libraries, then locks the Tags metadata field against provider refreshes.

## Architecture decisions

- Target the official Jellyfin 10.11 plugin model on .NET 9, matching the current Jellyfin plugin template package references.
- Keep tag normalization, filtering, lock planning, and item eligibility in a small testable service.
- Use stable library IDs and restrict supported item types to movies and series.

## Current status

The .NET 9 plugin and tests, configuration validator, tag sanitizer/persistence service, manual cleanup task, and one-shot event-driven new-item handler are implemented. The Jellyfin dashboard page configures allowed tags, explicit library IDs, and new-item enforcement. Public documentation, changelog, `build.yaml`, and restore/build/test CI are in place.

## Known limitations and open questions

- Inspection of Jellyfin 10.11.9 source confirms locked metadata fields are stored with item records, metadata refresh merges tags only when `MetadataField.Tags` is not locked, and explicit metadata-editor tag updates remain possible. These semantics are documented with upstream source references.
- A restart/provider-refresh test against a running Jellyfin server has not yet been performed; verify on the exact server and JellyTag versions before relying on this behavior for parental controls.

## Immediate next steps

Run the documented manual Jellyfin integration check: verify Tags lock persistence across restart, metadata-provider refresh behavior, and deliberate Jellyfin/JellyTag edits after sanitation. Publish a release and consider plugin repository submission separately.
