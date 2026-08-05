# Configuration reference

**Status:** Design (v1)  
**Schema:** [`config/schemas/pien.schema.json`](../../config/schemas/pien.schema.json)

Root file: **`pien.json`** (`schemaVersion: 1`).

## Document outline

| Section | Required | Purpose |
|---------|----------|---------|
| `schemaVersion` | yes | Must be `1` |
| `profile` | no | `quick` \| `standard` \| `deep` \| `api` \| `ci` |
| `targets[]` | yes (≥1) | Authorized website/api targets |
| `network` | no | SSRF allowlists, redirects, timeouts, UA |
| `crawl` | no | Pages, depth, robots, sitemap, same-origin |
| `checks` | no | `enabled` / `disabled` check ID lists |
| `policies` | no | Preset name, failOn, overrides, suppressions |
| `baselines` | no | Compare-on-scan options |
| `output` | no | Directory + formats |
| `storage` | no | `.pien` path + retainRuns |
| `notifications` | no | Webhook URL + secret ref + required |
| `watch` | no | `intervalSeconds` |
| `logging` | no | Level + scopes |

## Target object

| Field | Notes |
|-------|-------|
| `id` | Stable target id |
| `kind` | `website` \| `api` |
| `url` | Absolute `http`/`https` |
| `authorization.confirmed` | Must be `true` to probe |
| `authorization.notes` | Non-secret |
| `authentication` | Optional; secret references only |
| `openApiDocument` | Local path; no auto destructive exec |
| `apiCases[]` | Explicit API cases (see schema) |

## Formats

`console`, `json`, `sarif`, `junit`, `markdown`, `html`.

## Validation

`pien validate` loads schema + `PienConfigurationValidator` (URLs, auth, limits ≤ HardLimits, secret ref syntax).

## Examples

Under [`config/examples/`](../../config/examples/):

- `loopback-website.json` — **validate-ready** (`confirmed: true`, private network allowlist for `127.0.0.1`)
- `quick-website.json` / `complete-website.json` — placeholders with `confirmed: false` (intentional; `validate` exits 3 until confirmed)
- `api-contract.json`
- `authenticated-api.json`
- `watch-mode.json`

## Related

- [ProfilesAndPolicies.md](ProfilesAndPolicies.md)
- [TargetAuthorization.md](TargetAuthorization.md)
- [Authentication.md](Authentication.md)
- [Secrets.md](Secrets.md)
- [Limits.md](Limits.md)
- [Policies.md](Policies.md)
