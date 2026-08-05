# ADR-013 — No destructive API auto-execution

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

OpenAPI documents describe POST/PUT/PATCH/DELETE operations. Auto-executing them could mutate production systems and violate authorization ethics.

## Decision

v1 default probes are **read-only** (`GET`/`HEAD`/`OPTIONS` as needed for verification).

- Do **not** auto-execute mutating methods from OpenAPI.
- Any future mutating probe requires an explicit, separate operator opt-in, dedicated ADR update, and clear UX warnings.
- Coverage checks may **report** undocumented/untested operations without invoking them.

## Consequences

- Safer CI defaults against shared environments.
- OpenAPI “coverage” is analytical, not traffic-generating for writes.
- Reduces legal/operational risk for authorized testing.
