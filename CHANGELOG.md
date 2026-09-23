# Changelog

## 0.1.0.1

- Fix the configuration page loading failure and show available libraries.
- Replace the tags text area with add/remove tag controls and improve the form layout.

## 0.1.0.0

- Initial TagGuard implementation for Jellyfin 10.11.
- Add an allowlist-based manual cleanup task for selected Movie and Series libraries.
- Add one-shot, event-driven sanitation for newly added Movies and Series.
- Preserve other locked metadata fields and lock Tags against metadata refresh.
- Add a Jellyfin dashboard configuration page and automated tests.
- Provide a test catalog manifest and versioned ZIP for installation through Jellyfin's plugin catalog.
