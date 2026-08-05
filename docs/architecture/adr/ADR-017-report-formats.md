# ADR-017 — Report formats

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Different consumers need different artifacts: humans, GitHub code scanning, JUnit CI, and raw automation.

## Decision

Support first-class exporters implementing `IReportExporter`:

| Format | Purpose |
|--------|---------|
| `json` | Canonical machine-readable `ReportDocument` |
| `console` | Human summary |
| `sarif` | SARIF 2.1.0 (hand-written; ADR-005) |
| `junit` | CI test reporting |
| `markdown` | PR comments / docs |
| `html` | Local browsable report (encoded) |

All formats apply redaction rules. Scores are **advisory**, never certification language.

## Consequences

- CLI `--format` selects exporter(s).
- New formats follow ExtensionModel compile-time registration.
- HTML/Markdown must encode untrusted finding text (ThreatModel T-REPORT-INJECT).
