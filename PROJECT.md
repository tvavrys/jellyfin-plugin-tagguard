# Project context

## Goal and scope

TagGuard is a Jellyfin plugin that keeps only explicitly allowed tags on configured movie and series libraries, then locks the Tags metadata field against provider refreshes.

## Architecture decisions

- Target the official Jellyfin 10.11 plugin model on .NET 9, matching the current Jellyfin plugin template package references.
- Keep tag normalization, filtering, lock planning, and item eligibility in a small testable service.
- Use stable library IDs and restrict supported item types to movies and series.

## Current status

The solution, plugin/configuration types, tag planning logic, and unit test project are scaffolded. No scheduled task, event handler, configuration page, or persistence integration is implemented yet.

## Known limitations and open questions

- Lock persistence and provider refresh behavior still need verification against Jellyfin 10.11 before integration is added.
- Library ID resolution, task/event registration, and settling behavior remain to be implemented.

## Immediate next steps

Implement Jellyfin persistence integration, manual cleanup task, and event-driven new-item settling, then add the configuration page and public documentation.
