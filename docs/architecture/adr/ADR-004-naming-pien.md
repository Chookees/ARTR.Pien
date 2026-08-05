# ADR-004 — Naming and PIEN pipeline identity

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

The product needs a stable identity for branding, namespaces, CLI, config keys, and the four-stage mental model without implying historical cybersecurity claims.

## Decision

| Item | Value |
|------|-------|
| Product | ARTR Pien / Pien |
| Acronym | PIEN = Proba · Inspice · Examina · Nuntia |
| UX English | Test · Inspect · Examine · Report (`ScanStage`) |
| Solution | `ARTR.Pien.sln` |
| CLI | `pien` |
| Config file | `pien.json` |
| Config section | `ARTR:Pien` |
| Env prefix | `ARTR_PIEN_` |
| Analyzer IDs | `PIEN0001`–`PIEN0012` |
| Check IDs | `PIEN-{AREA}-{nnn}` |

Latin appears in documentation metaphor only — **never** in CLI progress strings.

## Consequences

- Consistent naming across docs, code, and CI.
- Marketing must retain the disclaimer that Roman engineering is metaphor, not cybersecurity history.
