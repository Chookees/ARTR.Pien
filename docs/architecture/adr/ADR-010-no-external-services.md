# ADR-010 — No external services

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Pien must work offline after restore (except when scanning operator-authorized network targets). Mandatory cloud APIs, databases, or Redis would violate local-first design.

## Decision

v1 **forbids** required dependencies on:

- Databases / ORM providers
- Redis / caches as a service
- Cloud secret managers as hard requirements (optional future embedder adapters only)
- Telemetry backends as hard requirements
- Remote plugin/check marketplaces

Optional outbound calls are limited to: authorized scan targets and operator-configured notification webhooks.

## Consequences

- State lives under `.pien/` on local disk.
- CI remains self-contained after `dotnet restore`.
- Observability is best-effort console / optional BCL metrics, not a SaaS contract.
