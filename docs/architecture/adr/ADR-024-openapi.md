# ADR-024 — OpenAPI document handling

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

API verification needs to parse OpenAPI 3.x documents (JSON/YAML) for contract and coverage checks without pulling NSwag’s full codegen stack.

## Decision

Use **Microsoft.OpenApi 3.9.0** and **Microsoft.OpenApi.YamlReader 3.9.0** (MIT; verified nuget.org 2026-08-05).

Constraints:

- Documents loaded from local paths or already-fetched bounded responses — not arbitrary remote `$ref` graphs without validation.
- Mutating operations are **not** auto-executed (ADR-013).
- Prefer these packages over NSwag for OM-only needs.

## Consequences

- Checks `PIEN-OPENAPI-001` / API contract checks can share one OM.
- YamlDotNet may appear transitively — watch license/NOTICE.
