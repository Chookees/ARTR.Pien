# Authentication

**Status:** Design (v1)

Optional request authentication for API (and website) targets. Credentials never appear in `pien.json` as raw values.

## Schemes

| Scheme | Behavior |
|--------|----------|
| `none` | Default |
| `bearer` | `Authorization: Bearer <resolved>` from `secretReference` |
| `basic` | HTTP Basic; password from `secretReference`; optional `usernameSecretReference` |
| `header` | Custom header (`headerName` required) value from `secretReference` |
| `cookie` | Cookie header value from `secretReference` (value never logged) |

## Placement

- Target-level `authentication` applies to default probes / all cases without override.
- `apiCases[].authentication` overrides per case.

## Safety

- Resolved secrets exist **only in memory** via `ISecretResolver`.
- Redaction strips `Authorization`, `Cookie`, `Set-Cookie`, and known API key headers before evidence, logs, reports, webhooks.
- Non-idempotent methods (`POST`/`PUT`/`PATCH`/`DELETE`) require `allowNonIdempotent: true` **and** `authorization.confirmed` (ADR-013 / ADR-022). Default remains read-only.

## Example

See [`config/examples/authenticated-api.json`](../../config/examples/authenticated-api.json).

Related: [Secrets.md](Secrets.md).
