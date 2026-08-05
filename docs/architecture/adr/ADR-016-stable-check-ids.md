# ADR-016 — Stable check IDs

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

CI policies, baselines, and SARIF rules must not break when check execution order or class names change.

## Decision

- Assign immutable string IDs: **`PIEN-{AREA}-{nnn}`** (e.g., `PIEN-HTTP-001`).
- Centralize built-ins in `CheckIds`.
- IDs are **never** derived from registration order or reflection order.
- Changing check semantics that would invalidate baselines requires a **new ID** or documented `RuleVersion` bump with migration notes.
- Do not reuse retired IDs.

## Consequences

- Baselines and suppressions remain stable.
- Catalog listing is sorted by ID for deterministic UX.
- Contributors must reserve IDs in docs before merging new checks.
