# ADR-015 — Local file storage

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Scans need run history, baselines, and locks without a database.

## Decision

Persist under a configurable state directory (default **`.pien/`**) via `FileScanStore`:

- `runs/`, `baselines/`, `state/`, `locks/`
- JSON serialization with camelCase
- **Atomic replace** writes (temp + move)
- Retention cleanup APIs (`CleanAsync` / `CleanupAsync`)

No SQLite/LiteDB in v1 unless a future ADR overturns this for scale.

## Consequences

- Easy backup/gitignore of `.pien/`.
- Concurrent scans need lock files; multi-machine sharing is out of scope.
- Path traversal defenses required when resolving IDs to paths.
