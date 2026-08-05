# ADR-026 — Roslyn analyzers for Power-of-Ten

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Documentation-only rules drift. Build-enforced analyzers keep scan-path code honest.

## Decision

Ship `ARTR.Pien.CodeAnalysis` with diagnostics **PIEN0001–PIEN0012**, packaged as an analyzer referenced by production projects.

- Use `Microsoft.CodeAnalysis.CSharp` **5.6.0** aligned to SDK 10.0.302.
- Treat analyzer failures as errors where severity is Error.
- Rules that are too noisy statically stay as Warning or procedural enforcement (see PowerOfTen.md).

## Consequences

- Contributors see violations at build time.
- Analyzer tests use `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`.
- Version skew with older SDKs can cause CS9057 — hence SDK pin (ADR-006).
