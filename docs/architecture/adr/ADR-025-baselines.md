# ADR-025 — Baselines

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Operators need change detection across scans without a SaaS dashboard.

## Decision

- Store baselines as JSON under `.pien/baselines/` via `IBaselineStore`.
- Compare using explicit `BaselineComparisonKind` policies (e.g., new failures, severity regressions).
- Emit findings through stable check ID `PIEN-CHANGE-001`.
- Baseline identifiers are operator-chosen safe path segments (no traversal).

## Consequences

- CI can fail on drift relative to an approved baseline.
- First-run baseline creation is an explicit CLI action (`pien baseline`), not implicit surprise.
- Residual: semantic changes with same check ID may need `RuleVersion` awareness.
