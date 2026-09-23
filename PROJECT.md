# Project context

## Goal and scope

TagGuard is a Jellyfin plugin that keeps only explicitly allowed tags on configured movie and series libraries, then locks the Tags metadata field against provider refreshes.

## Architecture decisions

- Target the official Jellyfin 10.11 plugin model on .NET 9, matching the current Jellyfin plugin template package references.
- Keep tag normalization, filtering, lock planning, and item eligibility in a small testable service.
- Use stable library IDs and restrict supported item types to movies and series.

## Current status

The solution, plugin/configuration types, tag planner, configuration validator, Jellyfin persistence service, manual cleanup task, and event-driven new-item handler are implemented. Unit tests cover tag planning, configuration checks, persistence outcomes, and the bounded debounce queue. The configuration page and public documentation/CI remain to be added.

## Known limitations and open questions

- Inspection of Jellyfin 10.11.9 source confirms locked metadata fields are stored with item records, and metadata refresh merges tags only when `MetadataField.Tags` is not locked.
- Jellyfin's metadata editor applies explicitly submitted tags even when the Tags field is locked, so deliberate administrator edits remain possible. A restart/provider-refresh test against a running server has not yet been performed.

## Immediate next steps

Add the configuration page, README, packaging metadata, and CI; document the source-verified lock behavior and a manual server integration test.
