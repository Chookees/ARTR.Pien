> **Superseded:** Duplicate ADR ID from concurrent writes. Use **ADR-004-naming-pien.md** instead. See [README.md](README.md).

# ADR-016 — Product naming and PIEN phase model

- **Status:** Superseded (see ADR-004)
- **Date:** 2026-08-05

## Context

Brand and architecture must stay consistent: ARTR Pien / Pien / PIEN stages.

## Decision

- Public name: **ARTR Pien**; short: **Pien**; acronym: **PIEN**
- Pipeline stages in UX/code enums: Test/Inspect/Examine/Report
- Latin names only in architecture narrative
- Namespaces `ARTR.Pien.*`; CLI `pien`; no `ARTR.PIEN` / `WebsiteScanner` product identity

## Consequences

Docs and CLI help must avoid certification and exploit-framework language.
