> **Superseded:** Duplicate ADR ID from concurrent writes. ID reserved for **ADR-020-apache-2.md**. Coverage gates: Directory.Build.props / DependencyResearch. See [README.md](README.md).

# ADR-020 — Coverage gate strategy

- **Status:** Superseded (ID conflict; see ADR-020-apache-2)
- **Date:** 2026-08-05

## Context

Master prompt requires ≥90% line and branch coverage on production assemblies with Cobertura + HTML reports.

## Decision

- Collect coverage via coverlet in `build/Test.*` and CI
- Enforce thresholds on production projects only (Core, Web, Checks, Engine, Reporting, Storage, Hosting, Cli)
- Exclude generated `*.g.cs` explicitly; do not exclude hard logic to inflate scores
- Mutation testing optional on schedule, not every local build

## Alternatives

| Option | Why not |
|--------|---------|
| Coverage on tests only | Misleading |
| No branch threshold | Hides decision gaps |

## Consequences

Incomplete features must still be tested at boundaries; stubs are forbidden. CI must fail below either threshold once Phase 15 lands.
