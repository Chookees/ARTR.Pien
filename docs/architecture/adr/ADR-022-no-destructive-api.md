> **Superseded:** Duplicate ADR ID from concurrent writes. Use **ADR-013-no-destructive-api-auto-exec.md** instead. See [README.md](README.md).

# ADR-022 — No automatic destructive API execution

- **Status:** Superseded (see ADR-013)
- **Date:** 2026-08-05

## Context

API verification must not mutate production data by default.

## Decision

Default probe methods are safe/idempotent (`GET`, `HEAD`, configured safe `OPTIONS`). Non-idempotent methods require explicit per-case allow flags in configuration. No automatic form posts, fuzzing, or exploit payloads.

## Consequences

OpenAPI-driven execution must filter methods; document operator responsibility for any explicitly enabled writes.
