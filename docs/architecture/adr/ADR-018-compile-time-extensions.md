# ADR-018 — Compile-time extensions

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Pien must grow checks, reporters, and secret resolvers without opening a dynamic plugin surface.

## Decision

Extensions are **compile-time**: implement interfaces (`ICheck`, `IReportExporter`, `ISecretResolver`, `INotificationSender`) and register explicitly in DI (`AddPien` / `AddPienChecks`).

Embedders may add registrations in their own composition root.

## Consequences

- Closed, reviewable check sets per binary.
- Slightly more ceremony than convention scanning — accepted trade-off.
- See `docs/architecture/ExtensionModel.md`.
