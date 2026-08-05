# ADR-009 — No containers

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Containers complicate local-first CLI development, CI supply chain, and the “no external services” posture. Testcontainers would pull Docker into the critical path.

## Decision

**Forbid** Dockerfiles, compose files, Kubernetes manifests for the product, and **Testcontainers.*** NuGet packages.

Tests use loopback Kestrel samples (`Microsoft.AspNetCore.Mvc.Testing`) or in-process fakes.

## Consequences

- Architecture tests assert absence of Docker artifacts and Testcontainers packages.
- Contributors run the SDK directly on the host/CI agent.
- Deployment docs describe `dotnet tool` / published binaries only.
