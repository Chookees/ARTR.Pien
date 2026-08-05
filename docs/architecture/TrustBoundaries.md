# Trust boundaries

**Status:** Accepted  
**Date:** 2026-08-05

Trust boundaries define where data and control cross security domains. Controls are mandatory for v1.

## Boundary map

```text
┌─────────────────────────────────────────────────────────────┐
│ Operator workstation / CI agent                              │
│  ┌──────────────┐  ┌─────────────┐  ┌────────────────────┐  │
│  │ pien.json    │  │ env/secrets │  │ .pien/ + reports   │  │
│  │ (untrusted   │  │ (secret     │  │ (trusted local     │  │
│  │  until       │  │  material)  │  │  artifacts;        │  │
│  │  validated)  │  │             │  │  treat as sensitive│  │
│  └──────┬───────┘  └──────┬──────┘  └─────────▲──────────┘  │
│         │                 │                   │             │
│         ▼                 ▼                   │             │
│  ┌────────────────────────────────────────────┴───────────┐ │
│  │ Pien process (Trusted Computing Base for scan logic)   │ │
│  │  Core · Engine · Web · Checks · Reporting · Storage    │ │
│  └─────────────┬───────────────────────────┬──────────────┘ │
│                │                           │                │
└────────────────┼───────────────────────────┼────────────────┘
                 │ outbound HTTPS            │ webhook HTTPS
                 ▼                           ▼
        ┌─────────────────┐        ┌─────────────────┐
        │ Scan targets    │        │ Notification    │
        │ (untrusted      │        │ endpoints       │
        │  network)       │        │ (untrusted)     │
        └─────────────────┘        └─────────────────┘
```

## Boundaries and controls

### B1 — Configuration (`pien.json`, CLI, env)

| Aspect | Detail |
|--------|--------|
| Trust | Operator-supplied; **untrusted** until validated |
| Risks | Malformed JSON, path traversal in output paths, private-network enablement, secret literals |
| Controls | Schema version gate; `PienConfigurationValidator`; absolute http(s) URLs only; clamp to `HardLimits`; authorization.confirmed required |

### B2 — Secrets

| Aspect | Detail |
|--------|--------|
| Trust | Environment / secret files / OS store — **high sensitivity** |
| Risks | Leak into reports, logs, webhooks, `.pien/` snapshots |
| Controls | `SecretReference` + `ISecretResolver`; redaction markers; never serialize resolved secrets; CI secrets via env only |

### B3 — Network (scan targets)

| Aspect | Detail |
|--------|--------|
| Trust | **Untrusted** remote HTTP/HTTPS |
| Risks | SSRF, DNS rebinding, redirect-to-private, oversized bodies, TLS downgrade |
| Controls | Destination validation + IP classification; ConnectCallback pinning; explicit redirect loop with revalidation; body/header limits; no automatic HTTPS→HTTP follow |

### B4 — Parsers (HTML, OpenAPI/YAML, JSON Schema, XML in reports)

| Aspect | Detail |
|--------|--------|
| Trust | Content from targets is **hostile** |
| Risks | XXE, entity expansion, regex DoS, script execution in HTML |
| Controls | AngleSharp with **no** script/network; secure XML writers/readers for JUnit/HTML; NJsonSchema with **local `$ref` only**; regex with `HardLimits.MaxRegexTimeout`; size caps before parse |

### B5 — Local state (`.pien/`)

| Aspect | Detail |
|--------|--------|
| Trust | Local filesystem under operator control; contents may include finding text from targets |
| Risks | Path traversal, symlink escape, concurrent corruption |
| Controls | Resolve paths under state root; atomic replace; lock files; retention cleanup |

### B6 — Reports

| Aspect | Detail |
|--------|--------|
| Trust | Downstream CI / humans; may be published |
| Risks | Secret leakage; HTML/Markdown injection; inaccurate “certified secure” language |
| Controls | Redaction before export; HTML encode user-controlled strings; advisory scores only — no certification claims |

### B7 — Webhooks / notifications

| Aspect | Detail |
|--------|--------|
| Trust | Configured URL is **untrusted** destination |
| Risks | SSRF via webhook; credential leak in POST body |
| Controls | Same SSRF validator (or strict allowlist); redacted payloads; no Authorization headers from scan secrets |

### B8 — CI / build

| Aspect | Detail |
|--------|--------|
| Trust | GitHub Actions / local agents |
| Risks | Supply-chain packages; container creep; publishing secrets |
| Controls | Locked restore; NuGet audit; forbid Testcontainers/Docker; coverage gates; Conventional Commits on `main` |

## Cross-boundary rules

1. Scan traffic **must** use `ISafeHttpTransport` — no raw `HttpClient` in Checks/Engine for outbound probes.
2. Findings evidence is **excerpts only**, size-bounded and redacted.
3. Embedders crossing into Hosting must preserve network and secret controls.
4. Compliance docs (`docs/compliance/*`) are **readiness mappings**, not attestations — see ADR-021.
