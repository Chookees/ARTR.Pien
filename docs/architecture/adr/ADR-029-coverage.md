# ADR-029 — Coverage gate strategy

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Master scope requires ≥90% line and branch coverage on production assemblies with Cobertura and HTML reports.

## Decision

- Collect coverage via coverlet in `build/Test.*` and CI
- Enforce thresholds on production projects only (Core, Web, Checks, Engine, Reporting, Storage, Hosting, Cli)
- Exclude generated `*.g.cs` explicitly; do not exclude difficult production logic to inflate scores
- Mutation testing may run on a schedule; not mandatory for every local build

## Consequences

Incomplete features must still have boundary tests. CI fails below either threshold once the gate is enabled (Developer GAP-09). Stub handlers are forbidden.
