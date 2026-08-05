# ADR-007 — Project boundaries

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

A flat “all code in one project” model would prevent reuse and invite cycles. Over-fragmentation would slow delivery.

## Decision

Split into Core, Web, Checks, Engine, Reporting, Storage, Hosting, Cli, CodeAnalysis with an **acyclic** graph:

- Core has zero Pien references.
- Engine / Web / Reporting / Storage → Core only (Checks → Core + Web).
- Hosting composes libraries; Cli → Hosting.
- Enforce with `ARTR.Pien.ArchitectureTests`.

## Consequences

- Clear ownership for SSRF (Web), orchestration (Engine), and IO (Storage/Reporting).
- New features must declare which layer they belong to before coding.
- Embedding uses Hosting without referencing Cli.
