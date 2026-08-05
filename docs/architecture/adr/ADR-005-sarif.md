# ADR-005 — SARIF exporter

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

CI consumers expect SARIF 2.1.0. Full SARIF SDKs pull large object models and often Newtonsoft.

## Decision

Hand-write SARIF 2.1.0 via `System.Text.Json` in `SarifReportExporter`. Reject `Sarif.Sdk` for write-only needs.

## Alternatives

| Option | Trade-off |
|--------|-----------|
| Sarif.Sdk | Completeness; oversized dependency |
| Omit SARIF | Breaks CI integration goal |

## Consequences

Exporter must stay schema-compatible (tool driver, rules, results, levels). Golden-file tests guard drift. Not a full SARIF round-trip library.
