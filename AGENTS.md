# AGENTS.md

## Working style

* Keep changes small, focused, and easy to review.
* Prefer simple solutions over abstractions.
* Follow existing Jellyfin plugin conventions.
* Do not introduce dependencies unless clearly justified.
* Update tests and documentation when behavior changes.

## Project context

Maintain a root-level `PROJECT.md` as lightweight living project context.

Keep it concise and update it when relevant. It should contain:

* current project goal and scope
* important architecture decisions
* current implementation status
* known limitations or open questions
* immediate next steps

Do not duplicate the README or turn `PROJECT.md` into a detailed changelog.

Read `PROJECT.md` before starting substantial work and update it before finishing when the project state changed.

## Git workflow

* Work on a dedicated branch.
* Make atomic commits with clear messages.
* Do not mix unrelated refactors with feature work.
* Rebase or sync with the target branch before finalizing.
* Before completion, review the full diff and run build/tests.

## Public repository

Assume everything committed may be publicly visible.

Never commit:

* secrets, API keys, tokens, passwords
* private URLs or infrastructure details
* local paths containing personal information
* `.env` files
* generated credentials or certificates
* debug dumps containing sensitive data

Use examples/placeholders in documentation.

## Security

* Treat library metadata changes as destructive operations.
* Validate configuration before modifying data.
* Avoid direct database access when Jellyfin APIs exist.
* Preserve unrelated metadata and locked fields.
* Do not add unnecessary network access.
* Avoid unsafe logging of paths, credentials, or user data.
* Keep dependencies minimal and maintained.

## Quality gate

Before marking work complete:

```bash
dotnet build
dotnet test
```

Ensure the repository is clean, documentation matches behavior, `PROJECT.md` reflects the current state, and no sensitive data is included.
