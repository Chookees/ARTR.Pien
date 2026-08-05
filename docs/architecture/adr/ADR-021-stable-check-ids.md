> **Superseded:** Duplicate ADR ID from concurrent writes. Use **ADR-016-stable-check-ids.md** instead. See [README.md](README.md).

# ADR-021 — Stable check identifiers

- **Status:** Superseded (see ADR-016)
- **Date:** 2026-08-05

## Context

Findings must be comparable across runs and baselines.

## Decision

Stable string IDs in `CheckIds` (`PIEN-HTTP-001`, …). IDs never depend on discovery order. Rule versions are semver strings on `CheckDefinition`. Renames require ADR + migration notes.

## Consequences

Catalog docs (`docs/checks/CheckCatalog.md`) must stay synchronized with metadata.
