# Data flow — CLI → stages → outputs

**Status:** Accepted  
**Date:** 2026-08-05

## End-to-end flow

```text
┌─────────────┐    ┌──────────────────┐    ┌─────────────────────────────┐
│ pien CLI    │───►│ Config merge     │───►│ Hosting DI (AddPien)        │
│ args/env    │    │ pien.json +      │    │ transport, checks, stores   │
└─────────────┘    │ ARTR_PIEN_* +    │    └──────────────┬──────────────┘
                   │ CLI overrides    │                   │
                   └────────┬─────────┘                   ▼
                            │              ┌──────────────────────────────┐
                            │              │ ScanEngine                   │
                            └─────────────►│ 1 Testing (Proba)            │
                                           │ 2 Inspecting (Inspice)       │
                                           │ 3 Examining (Examina)        │
                                           │ 4 Reporting (Nuntia)         │
                                           └──────────────┬───────────────┘
                                                          │
              ┌───────────────┬───────────────┬───────────┼───────────┐
              ▼               ▼               ▼           ▼           ▼
         Reports         .pien/runs      Baselines   Notifications  Exit code
         (exporters)     history         compare     (webhook/file) CI gate
```

## Stage detail

### 0. Configuration

1. Resolve path: `--config` or `./pien.json`.
2. Load JSON (`schemaVersion: 1`).
3. Overlay env `ARTR_PIEN_*` and CLI flags (`--target`, `--profile`, `--max-pages`, …).
4. `PienConfigurationValidator` enforces authorization, URLs, network options, and clamps to `HardLimits`.
5. Secret references resolve via `ISecretResolver` **into memory only** — never written to reports.

**Redaction checkpoint R0:** reject configs that embed raw secrets in fields meant for reports; prefer `SecretReference`.

### 1. Testing (Proba)

- For each authorized target, `ISafeHttpTransport.SendAsync` builds probes.
- **SSRF checkpoint S1:** `IDestinationValidator` resolves DNS, classifies IPs, blocks private/link-local/metadata unless allowlisted with `allowPrivateNetworks`.
- **SSRF checkpoint S2:** `ConnectCallback` pins connect to validated addresses (no post-resolve swap without revalidation).
- TLS probe may run via `ITlsProbe` for TLS checks.
- Crawler (`ICrawler`) may enqueue same-origin pages within crawl limits — each URL revalidated.

### 2. Inspecting (Inspice)

- Bound body (`BodyInspectionLimitBytes`), header count, evidence excerpts.
- Canonicalize URIs; strip userinfo on cross-host redirects.
- Build `InspectionEvidence` (probes dictionary, optional crawl pages).

**Redaction checkpoint R1:** sensitive headers (`Authorization`, `Cookie`, `Set-Cookie`, API keys) marked for redaction before evidence enters findings/logs.

### 3. Examining (Examina)

- `ICheckCatalog` supplies compile-time-registered checks.
- Each `ICheck.EvaluateAsync(context, evidence)` returns `CheckResult` + `Finding`s.
- Findings capped per check / report via limits.
- Policy evaluator and advisory category scores run after checks.

### 4. Reporting (Nuntia)

- Build `ReportDocument` (schemaVersion 1).
- Exporters: JSON, console, SARIF 2.1.0, JUnit, Markdown, HTML.
- Optional `IScanStore.SaveRunAsync` → `.pien/runs/{id}/`.
- Optional baseline compare → findings for change detection.
- Optional `INotificationSender` (file / webhook) with **redacted** payloads only.

**Redaction checkpoint R2:** exporters and notification senders must call redaction helpers; secrets never appear in SARIF/HTML/webhook bodies.

**SSRF checkpoint S3 (webhooks):** webhook URLs undergo the same destination validation as scan targets (or a dedicated allowlist). No open SSRF via notification config.

## Storage layout

```text
.pien/
  runs/{runId}/run.json
  runs/{runId}/report.json
  baselines/{baselineId}.json
  state/
  locks/
```

Writes use temp-file + atomic replace. Paths must stay under the configured state root (no traversal).

## Failure and cancellation

| Condition | Behavior |
|-----------|----------|
| Unauthorized target | `AuthorizationException`; non-zero exit |
| SSRF / unsafe redirect | `TargetSafetyException`; fail closed |
| Timeout / cancel | `ScanRunStatus.Cancelled` or `Failed` |
| Policy fail | Completed run + failing exit code |

## CI embedding

CI runs `pien scan` (or `dotnet run --project src/ARTR.Pien.Cli`) against loopback samples or authorized staging URLs. Artifacts are report files; no external Pien service is required.
