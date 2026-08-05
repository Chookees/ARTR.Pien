# Threat model (STRIDE)

**Status:** Accepted  
**Date:** 2026-08-05  
**Scope:** ARTR Pien v1 CLI + libraries (local-first verification)  
**Related:** [TrustBoundaries.md](../architecture/TrustBoundaries.md), [DataFlow.md](../architecture/DataFlow.md), ADR-011 / ADR-012 / ADR-013

This model enumerates threats relevant to a URL-fetching verification tool. It is **not** a penetration-test report or certification.

**STRIDE:** Spoofing · Tampering · Repudiation · Information disclosure · Denial of service · Elevation of privilege

**Boundaries (see TrustBoundaries.md):** B1 Config · B2 Secrets · B3 Network · B4 Parsers · B5 Storage (`.pien/`) · B6 Reports · B7 Webhooks · B8 CI

---

## T-SSRF-001 — Classic SSRF to internal / metadata

| Field | Value |
|-------|-------|
| STRIDE | Elevation, Information disclosure |
| Asset | Cloud metadata endpoints, internal HTTP services, link-local agents |
| Boundary | B3 Network |
| Impact | Read internal endpoints; steal cloud credentials; pivot into VPC |
| Preventive | `DestinationValidator` + `IpAddressClassifier`; block RFC1918, loopback, link-local, multicast, `169.254.169.254`; `allowPrivateNetworks` + host allowlist only when intentional; `ConnectCallback` pins validated IPs; http/https only |
| Detective | Architecture/unit tests for private IP rejection; CI security tests; operator logs of blocked destinations |
| Residual | Exotic IPv6 / future special-use ranges need classifier updates |

## T-SSRF-002 — DNS rebinding

| Field | Value |
|-------|-------|
| STRIDE | Elevation, Tampering |
| Asset | Trust in DNS results between validate and connect |
| Boundary | B3 Network |
| Impact | Public A-record at validate time; private address at connect → SSRF |
| Preventive | Validate then pin connect addresses; do not re-resolve for the same hop without full revalidation; short-lived handlers per hop |
| Detective | Tests that mock rebind scenarios; transport unit tests asserting pinned endpoints |
| Residual | Rare OS-resolver TOCTOU; mitigated by pinned-IP connect only |

## T-SSRF-003 — Redirect-to-private

| Field | Value |
|-------|-------|
| STRIDE | Elevation |
| Asset | Internal network reachability |
| Boundary | B3 Network |
| Impact | Public seed URL redirects to `127.0.0.1`, RFC1918, or metadata |
| Preventive | Explicit redirects (`AllowAutoRedirect=false`); revalidate every `Location` (ADR-012); block HTTPS→HTTP downgrade; default deny private |
| Detective | Redirect-chain tests; findings/logs for blocked redirects |
| Residual | Overly broad operator allowlists |

## T-CRED-001 — Credential leakage in reports / logs

| Field | Value |
|-------|-------|
| STRIDE | Information disclosure |
| Asset | Bearer tokens, cookies, API keys, URI userinfo |
| Boundary | B2 Secrets, B6 Reports, B7 Webhooks |
| Impact | Secrets published to CI artifacts, chat webhooks, or shared reports |
| Preventive | `SecretReference` + in-memory resolve only; `RedactionHelpers` / sensitive header denylist; strip userinfo on cross-host redirect; never serialize resolved secrets to `.pien/` |
| Detective | Redaction unit tests; exporter snapshot tests asserting no Authorization/Cookie values |
| Residual | Secrets in unexpected body fields; custom headers missing from denylist |

## T-PARSE-001 — Malicious HTML (and embedded script)

| Field | Value |
|-------|-------|
| STRIDE | Tampering, Elevation, Denial of service |
| Asset | Process integrity; unexpected outbound requests |
| Boundary | B4 Parsers |
| Impact | Script execution, parser-driven network, memory blow-up |
| Preventive | AngleSharp configured without script execution and without automatic requests (ADR-027); parse only bounded buffers from `HardLimits` |
| Detective | Malformed HTML fixtures in tests; dependency upgrade review in `DependencyResearch.md` |
| Residual | Parser CVEs — keep AngleSharp current |

## T-PARSE-005 — Malicious JSON / OpenAPI documents

| Field | Value |
|-------|-------|
| STRIDE | Tampering, Denial of service, Elevation |
| Asset | Process integrity; local filesystem via `$ref` |
| Boundary | B4 Parsers |
| Impact | Deeply nested JSON DoS; remote `$ref` SSRF; schema bombs |
| Preventive | `System.Text.Json` / NJsonSchema with **local `$ref` only**; Microsoft.OpenApi readers without implicit network fetch (ADR-003, ADR-024); body size caps before parse |
| Detective | Fixtures with deep nesting / remote `$ref`; size-limit tests |
| Residual | Future schema features that re-enable network refs must be ADR’d and blocked |

## T-PARSE-002 — XXE / entity expansion

| Field | Value |
|-------|-------|
| STRIDE | Elevation, Denial of service, Information disclosure |
| Asset | Host filesystem; process CPU/memory |
| Boundary | B4 Parsers |
| Impact | File disclosure or DoS via XML DTDs/entities |
| Preventive | Prefer JSON writers for reports; secure XML settings if XML is read; do not parse untrusted target XML with DTD resolution enabled; OpenAPI via Microsoft.OpenApi (not raw XmlDocument defaults) |
| Detective | Code review gate for any new XML parse path; tests if XML ingest is added |
| Residual | Future XML-based checks must re-review reader settings |

## T-PARSE-003 — Oversized responses

| Field | Value |
|-------|-------|
| STRIDE | Denial of service |
| Asset | Memory, disk, scan completion |
| Boundary | B3 Network, B5 Storage |
| Impact | OOM, disk fill, hung scans |
| Preventive | `HardLimits.MaxBodyInspectionBytes`; header count caps; evidence excerpt caps; finding caps; overall scan timeout |
| Detective | Body-over-limit tests; metrics/logs of truncated bodies |
| Residual | Many parallel targets near max size — concurrency ceilings apply |

## T-PARSE-004 — Regex denial of service

| Field | Value |
|-------|-------|
| STRIDE | Denial of service |
| Asset | CPU / scan worker |
| Boundary | B4 Parsers / Checks |
| Impact | Hang or starve the scan process |
| Preventive | `Regex` with match timeout ≤ `HardLimits.MaxRegexTimeout`; prefer AngleSharp/JSON parsers over regex; planned PIEN0012 / centralized safe-regex helper |
| Detective | Timeout unit tests; Power-of-Ten review for new regexes |
| Residual | Concurrent regexes still consume CPU within timeout budget |

## T-REPORT-001 — Report injection (HTML / Markdown / console)

| Field | Value |
|-------|-------|
| STRIDE | Tampering, Information disclosure |
| Asset | Operator browser / CI log viewers |
| Boundary | B6 Reports |
| Impact | XSS if HTML report opened; misleading markdown; ANSI injection in consoles |
| Preventive | HTML-encode finding fields; treat all target-derived text as untrusted; avoid `javascript:` / raw HTML in exporters; console sanitization where applicable |
| Detective | HTML exporter encoding tests; snapshot review of adversarial titles/URLs |
| Residual | Third-party viewers that mis-render encoded text |

## T-PATH-001 — Path traversal in `.pien/` or outputs

| Field | Value |
|-------|-------|
| STRIDE | Tampering, Elevation |
| Asset | Host filesystem outside state/output roots |
| Boundary | B1 Config, B5 Storage |
| Impact | Read/write arbitrary files via crafted IDs or output paths |
| Preventive | `Path.GetFullPath` + root-prefix checks; reject `..` and absolute escapes in IDs; atomic writes under state root only (ADR-015) |
| Detective | Storage tests with `../` and absolute path IDs |
| Residual | Symlink races on some OSes — refuse/detect symlinks for state root when feasible |

## T-SEC-001 — Secret exposure (config / state / VCS)

| Field | Value |
|-------|-------|
| STRIDE | Information disclosure |
| Asset | Credentials and tokens |
| Boundary | B1 Config, B2 Secrets, B5 Storage, B8 CI |
| Impact | Repository or artifact secret exposure |
| Preventive | Env-based `SecretReference`; `.gitignore` for `.pien/` and local secret files; never write resolved secrets to disk reports |
| Detective | `pien doctor` / docs checks; secret-scanning in CI where available |
| Residual | Operator commits plaintext secrets despite guidance |

## T-HOOK-001 — Webhook abuse (SSRF / leak / spam)

| Field | Value |
|-------|-------|
| STRIDE | Elevation, Information disclosure, Denial of service |
| Asset | Internal network; secrets; notification endpoints |
| Boundary | B7 Webhooks |
| Impact | Notify URL used as SSRF; body leaks secrets; webhook flooding |
| Preventive | Validate webhook destination like scan targets; redacted payloads only; no forwarding of Authorization headers; optional disable in CI profiles |
| Detective | Webhook unit tests with private destinations; payload redaction asserts |
| Residual | Misconfigured allowlist |

## T-DOS-001 — Resource exhaustion (crawl / concurrency / retention)

| Field | Value |
|-------|-------|
| STRIDE | Denial of service |
| Asset | CPU, sockets, disk, operator host |
| Boundary | B3 Network, Engine, B5 Storage |
| Impact | Host degradation; never-finishing scans; disk fill from history |
| Preventive | `HardLimits` on pages, depth, concurrency, timeouts, findings; cancellation tokens; history retention/cleanup |
| Detective | Limits clamp tests; cancellation tests |
| Residual | Operator raising all limits to ceilings on small hosts |

## T-TLS-001 — TLS validation bypass

| Field | Value |
|-------|-------|
| STRIDE | Spoofing, Information disclosure |
| Asset | Confidentiality/integrity of probes; trust in TLS findings |
| Boundary | B3 Network |
| Impact | MITM; false sense of TLS health if validation disabled |
| Preventive | Default SSL validation **on**; no global “ignore cert errors”; TLS checks report certificate facts without disabling validation for general traffic |
| Detective | Tests that invalid certs fail connect (except explicitly isolated future modes); code search for `ServerCertificateCustomValidationCallback` bypasses |
| Residual | Any future insecure-mode needs loud warnings + dedicated ADR |

## T-CONC-001 — Inaccurate security conclusions

| Field | Value |
|-------|-------|
| STRIDE | Repudiation, integrity of decision |
| Asset | Operator trust; compliance posture; CI gates |
| Boundary | B6 Reports, B8 CI |
| Impact | Treating advisory scores as certification; empty/partial check set implies “secure” |
| Preventive | Advisory wording only; ADR-021 compliance boundaries; incomplete catalog must not claim full coverage; exit codes reflect **policy**, not certification |
| Detective | Doc/review forbid certification strings in exporters; product copy review |
| Residual | Human misuse of reports — cannot fully prevent |

## T-AUTH-001 — Unauthorized scanning

| Field | Value |
|-------|-------|
| STRIDE | Elevation, Repudiation |
| Asset | Third-party systems; legal/compliance risk |
| Boundary | B1 Config |
| Impact | Scanning systems without permission |
| Preventive | `authorization.confirmed` gate before probes; SECURITY.md scope; docs require authorized targets only |
| Detective | Engine throws `AuthorizationException` when unconfirmed; tests for gate |
| Residual | Operators falsifying config — cannot fully prevent |

## T-SUPPLY-001 — Malicious or vulnerable NuGet dependency

| Field | Value |
|-------|-------|
| STRIDE | Tampering, Elevation |
| Asset | Build and runtime integrity |
| Boundary | B8 CI |
| Impact | Compromised package at restore/build/runtime |
| Preventive | Minimal deps (ADR-001); CPM + lockfiles; `NuGetAudit`; license review in `DependencyResearch.md` |
| Detective | Locked restore in CI; audit failures fail build |
| Residual | Compromised upstream despite audit |

---

## Coverage checklist (orchestrator gate)

| Required theme | Threat ID |
|----------------|-----------|
| SSRF | T-SSRF-001 |
| DNS rebinding | T-SSRF-002 |
| Redirect-to-private | T-SSRF-003 |
| Credential leakage | T-CRED-001 |
| Malicious HTML | T-PARSE-001 |
| Malicious JSON/OpenAPI | T-PARSE-005 |
| XXE | T-PARSE-002 |
| Oversized responses | T-PARSE-003 |
| Regex DoS | T-PARSE-004 |
| Report injection | T-REPORT-001 |
| Path traversal | T-PATH-001 |
| Secret exposure | T-SEC-001 |
| Webhook abuse | T-HOOK-001 |
| Resource exhaustion | T-DOS-001 |
| TLS validation bypass | T-TLS-001 |
| Inaccurate security conclusions | T-CONC-001 |

## Test strategy linkage

| Threat | Primary test idea |
|--------|-------------------|
| T-SSRF-* | Classifier + redirect revalidation; loopback allowlist cases |
| T-CRED-001 / T-SEC-001 | Redaction unit tests; exporter snapshots without secrets |
| T-PARSE-* | Malformed HTML/JSON/OpenAPI fixtures; body over-limit; regex timeout |
| T-REPORT-001 | HTML exporter encoding tests |
| T-PATH-001 | Storage tests with `../` IDs |
| T-DOS-001 | Limits clamp + cancellation tests |
| T-TLS-001 | Invalid certificate connect failure |
| T-CONC-001 | Forbid certification language in exporters |
| T-AUTH-001 | Unconfirmed target rejected before network |
