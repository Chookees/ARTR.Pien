# ADR-001 — Minimal dependency strategy

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Pien must stay redistributable under Apache-2.0, auditable, and free of container/cloud/DB dependencies. NuGet surface area increases supply-chain and license risk.

## Decision

Prefer BCL and `Microsoft.Extensions.*`. Use Central Package Management with exact versions, locked restore, and NuGet audit (`low` / `all`). Record every direct dependency in `docs/development/DependencyResearch.md`.

## Alternatives

| Option | Trade-off |
|--------|-----------|
| Ad-hoc package sprawl | Faster features; worse audit/license risk |
| Vendoring forks | Control; high maintenance |
| JsonSchema.Net / Sarif.Sdk | Feature-rich; rejected for EULA/weight (see ADR-003, ADR-005) |

## Consequences

Smaller feature velocity for niche parsers; clearer compliance story for future commercial ARTR products.
