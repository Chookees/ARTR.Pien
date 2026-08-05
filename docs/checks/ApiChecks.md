# API checks

**Status:** Design (v1)  
Parent: [CheckCatalog.md](CheckCatalog.md)

Applies to `kind: api` targets with optional `apiCases` and `openApiDocument`.

## Contract execution

| ID | Impl | Evaluates |
|----|------|-----------|
| PIEN-API-001 | **I** | Base URL / default probe contract fundamentals |
| PIEN-API-002 | **I** | Per-case expected status and content-type |
| PIEN-API-003 | **I** | JSON Pointer assertions (`exists`, `eq`, `type`, …) |
| PIEN-API-004 | **I** | Local JSON Schema validation (no remote fetch by default) |
| PIEN-API-005 | **I** | Blocks non-idempotent methods unless `allowNonIdempotent` |

## OpenAPI

| ID | Impl | Evaluates |
|----|------|-----------|
| PIEN-OPENAPI-001 | **I** | Document parse / load success |
| PIEN-OPENAPI-002 | **I** | operationId uniqueness; insecure server URLs |
| PIEN-OPENAPI-003 | **I** | Response status/content-type vs operation |
| PIEN-OPENAPI-004 | **I** | Coverage of configured operations (advisory stats) |

## Non-goals

- Do **not** auto-execute every OpenAPI operation.
- Do **not** auto-generate destructive requests.
- Do **not** arbitrary script / unrestricted JSONPath.
- Bound schema depth/size; disable remote `$ref` fetch by default (ADR-024).

## Auth

Cases may reference `secret://` auth — see [Authentication.md](../configuration/Authentication.md).
