# ADR-026 — Roslyn analyzers for Power-of-Ten

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Documentation-only rules drift. Build-enforced analyzers keep scan-path code honest.

## Decision

Ship `ARTR.Pien.CodeAnalysis` with diagnostics **PIEN0001–PIEN0009**, packaged as an analyzer referenced by production projects. IDs **PIEN0010–PIEN0012** remain reserved and are **intentionally deferred** (see **ADR-030**).

- Use `Microsoft.CodeAnalysis.CSharp` **5.6.0** aligned to SDK 10.0.302.
- Treat analyzer failures as errors where severity is Error.
- Rules that are too noisy statically stay as Warning or procedural enforcement (see PowerOfTen.md / ADR-030).

## Consequences

- Contributors see violations at build time for PIEN0001–PIEN0009.
- Analyzer tests use `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`.
- Version skew with older SDKs can cause CS9057 — hence SDK pin (ADR-006).
- PIEN0010–PIEN0012 are not implemented until a low-FP design is accepted (ADR-030).
