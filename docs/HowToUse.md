# How to use ARTR Pien

Local-first verification CLI for **authorized** HTTP/HTTPS websites and APIs.

## Table of contents

1. [Installation / run from source](#installation--run-from-source)
2. [Configuration](#configuration)
3. [Profiles and policies](#profiles-and-policies)
4. [Website scans](#website-scans)
5. [API tests](#api-tests)
6. [Authentication and secrets](#authentication-and-secrets)
7. [Private-network authorization](#private-network-authorization)
8. [Crawl controls](#crawl-controls)
9. [Checks](#checks)
10. [Baselines](#baselines)
11. [Watch mode](#watch-mode)
12. [Reports](#reports)
13. [CI integration](#ci-integration)
14. [Local scheduling](#local-scheduling)
15. [Exit codes](#exit-codes)
16. [Troubleshooting](#troubleshooting)
17. [Secure-use checklist](#secure-use-checklist)
18. [Further reading](#further-reading)

---

## Installation / run from source

```powershell
dotnet build ARTR.Pien.sln -c Release
dotnet run --project src/ARTR.Pien.Cli -c Release -- --help
```

Published tool / versioned packages may arrive later; source run is the supported path for this repository stage.

---

## Configuration

```powershell
dotnet run --project src/ARTR.Pien.Cli -c Release -- init --website --force
```

- File: `pien.json` (`schemaVersion: 1`)
- Schema: [`config/schemas/pien.schema.json`](../config/schemas/pien.schema.json)
- Examples: [`config/examples/`](../config/examples/)
- Reference: [ConfigurationReference.md](configuration/ConfigurationReference.md)

**`validate` / `scan` require `authorization.confirmed=true` for every target.** Placeholder starters such as [`quick-website.json`](../config/examples/quick-website.json) and [`complete-website.json`](../config/examples/complete-website.json) intentionally ship with `confirmed: false` so they cannot probe `example.com` by accident — `pien validate` exits **3** until you confirm. For a validate-ready sample (exit 0), use [`loopback-website.json`](../config/examples/loopback-website.json).

```powershell
# Placeholder (expected exit 3 until you set confirmed=true):
dotnet run --project src/ARTR.Pien.Cli -c Release -- validate --config config/examples/quick-website.json

# Validate-ready loopback sample (exit 0):
dotnet run --project src/ARTR.Pien.Cli -c Release -- validate --config config/examples/loopback-website.json
```

Precedence: defaults → profile → `pien.json` → `ARTR_PIEN_*` → CLI. Details: [ProfilesAndPolicies.md](configuration/ProfilesAndPolicies.md).

---

## Profiles and policies

Profiles: `quick`, `standard`, `deep`, `api`, `ci`.  
Policies: `balanced`, `security-focused`, `quality-focused`, `api-contract`, `ci-strict`.

```powershell
dotnet run --project src/ARTR.Pien.Cli -c Release -- scan --profile ci --fail-on high
```

---

## Website scans

Edit `authorization.confirmed` only for systems you may test. Until `confirmed` is `true`, both `validate` and `scan` refuse the config (exit 3 / 4).

```powershell
dotnet run --project src/ARTR.Pien.Cli -c Release -- scan --format console,json --output artifacts/pien
```

- Placeholder shape (will not validate until confirmed): [`quick-website.json`](../config/examples/quick-website.json)
- Validate-ready loopback: [`loopback-website.json`](../config/examples/loopback-website.json) (`confirmed: true`, `allowPrivateNetworks`, `allowedHosts` for `127.0.0.1`)

---

## API tests

Use `kind: api`, optional `openApiDocument`, and explicit `apiCases`. Non-idempotent methods require `allowNonIdempotent: true`.

Examples: [`api-contract.json`](../config/examples/api-contract.json), [`authenticated-api.json`](../config/examples/authenticated-api.json).

---

## Authentication and secrets

Prefer `secret://env/...` or `secret://file/...`. Never put raw tokens in `pien.json` or reports.

See [Authentication.md](configuration/Authentication.md) and [Secrets.md](configuration/Secrets.md).

---

## Private-network authorization

Loopback/private targets need:

1. `authorization.confirmed: true`
2. `network.allowPrivateNetworks: true`
3. Host on `network.allowedHosts`

See [TargetAuthorization.md](configuration/TargetAuthorization.md).

---

## Crawl controls

`crawl.maxPages`, `maxDepth`, `respectRobotsTxt`, `useSitemap`, `sameOriginOnly` — always capped by [Limits.md](configuration/Limits.md) / `HardLimits`.

---

## Checks

```powershell
dotnet run --project src/ARTR.Pien.Cli -c Release -- list-checks
dotnet run --project src/ARTR.Pien.Cli -c Release -- explain PIEN-HTTP-001
```

Catalog: [CheckCatalog.md](checks/CheckCatalog.md). Severity/status: [SeverityModel.md](checks/SeverityModel.md).

---

## Baselines

```text
pien baseline create
pien baseline compare
pien baseline show
pien baseline remove <id>
```

Schema: [`config/schemas/pien-baseline.schema.json`](../config/schemas/pien-baseline.schema.json). No secrets in baselines.

---

## Watch mode

```powershell
dotnet run --project src/ARTR.Pien.Cli -c Release -- watch --interval 300
```

Interval only — **no cron parser**. Example: [`watch-mode.json`](../config/examples/watch-mode.json). Ctrl+C → exit 10.

---

## Reports

Formats: `console`, `json`, `sarif`, `junit`, `markdown` (`md`), `html` (self-contained, no CDN).

```powershell
dotnet run --project src/ARTR.Pien.Cli -c Release -- report --run-id <id> --format html
```

Details: [ReportFormats.md](reporting/ReportFormats.md). CLI UX: [CliUx.md](cli/CliUx.md).

---

## CI integration

- Treat exit `0` as pass, `1` as policy failure, `2–9` as infrastructure/config errors, `10` as cancelled.
- Prefer `--profile ci` and formats `sarif,junit,json`.
- Authorize only staging/loopback targets in pipelines.

---

## Local scheduling

Use OS schedulers (Windows Task Scheduler, cron, systemd timer) to invoke `pien scan` or `pien watch`. Pien does not embed a cron expression parser.

---

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | Success / policy passed |
| 1 | Policy failed |
| 2 | Invalid arguments |
| 3 | Invalid configuration |
| 4 | Target rejected (auth/safety) |
| 5 | Scan failed |
| 6 | Report failed |
| 7 | Baseline failed |
| 8 | Storage failed |
| 9 | Notification failed (when required) |
| 10 | Cancelled |

---

## Troubleshooting

| Symptom | Action |
|---------|--------|
| Exit 3 on starter examples | Expected when `confirmed: false` (placeholders). Set `confirmed: true` or use `loopback-website.json` |
| Exit 3 | `pien validate`; check schemaVersion, URLs, and `authorization.confirmed` |
| Exit 4 | Confirm `authorization.confirmed`; allowlist hosts |
| Exit 5 | Target reachability; `pien doctor` |
| Exit 8 | Disk permissions for `.pien` / output dir |
| Empty history | Run a successful `scan` first |

```powershell
dotnet run --project src/ARTR.Pien.Cli -c Release -- doctor
```

---

## Secure-use checklist

- [ ] Only authorized targets (`confirmed: true`)
- [ ] No raw secrets in config or commits
- [ ] Private networks explicitly allowlisted
- [ ] Non-idempotent API cases explicitly opted in
- [ ] Reports reviewed for redaction expectations
- [ ] Third-party systems never scanned without permission

---

## Further reading

- [CliUx.md](cli/CliUx.md)
- [ProfilesAndPolicies.md](configuration/ProfilesAndPolicies.md)
- [architecture/Architecture.md](architecture/Architecture.md)
- [product/ReadmeCatcher.md](product/ReadmeCatcher.md)
- [AI_Onboard.md](AI_Onboard.md) · [Dev_Onboard.md](Dev_Onboard.md)
