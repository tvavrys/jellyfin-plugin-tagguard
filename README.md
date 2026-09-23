# jellyfin-plugin-tagguard
Jellyfin plugin that cleans metadata tags using a configurable allowlist, locks the Tags field against metadata refreshes, and automatically sanitizes newly added movies and shows. Ideal for simple tag-based parental control workflows.

## Dev Container

In VS Code with the Dev Containers extension, open this repository and run **Dev Containers: Reopen in Container**. The container provides the .NET 10 SDK and installs the Codex CLI, so `codex` is available in the integrated terminal. Sign in to Codex interactively; authentication is intentionally not stored in this repository.

Use `dotnet build` and `dotnet test` for normal development.
