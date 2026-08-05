# Profiles and policies

**Status:** Design (v1)  
**Date:** 2026-08-05

Built-in **profiles** supply scan defaults (limits, formats). Built-in **policies** supply fail thresholds and check emphasis. Neither replaces explicit `pien.json` fields once set.

See also: [ConfigurationReference.md](ConfigurationReference.md), [Policies.md](Policies.md), [Limits.md](Limits.md).

---

## Precedence (low → high)

```text
1. Product defaults (ScanLimits.Default, balanced policy)
2. Built-in profile (profile key or --profile)
3. pien.json fields
4. Environment ARTR_PIEN_*
5. CLI flags (--fail-on, --max-pages, --format, …)
```

Later layers win for scalars. Arrays use **replace-on-merge** when the higher layer sets the property (non-null / present), not element-wise union — except documented append cases below.

### Array merge / replace semantics

| Property | Semantics when higher layer sets it |
|----------|-------------------------------------|
| `network.allowedHosts` | **Replace** entire list |
| `checks.enabled` | **Replace** (empty = “all applicable”) |
| `checks.disabled` | **Replace** |
| `output.formats` | **Replace** |
| `policies.suppressions` | **Replace** |
| `policies.severityOverrides` | **Replace** object map |
| `notifications.events` | **Replace** |
| `targets` | **Replace** entire array (env rarely sets this; CLI `--target` filters) |
| `targets[].apiCases` | **Replace** per target when that target object is fully replaced |

Profile application happens **before** reading explicit `pien.json` crawl/policy fields that the operator set; implementation should apply profile clamps/defaults only for unset fields **or** document profile-as-ceiling (current scaffold uses min/max clamps — Developer must make behavior deterministic and document here if changed). **Design intent:** profile fills defaults; explicit JSON values win; CLI wins last.

---

## Built-in profiles

| Profile | Intent | Crawl defaults (indicative) | Checks emphasis | Output / policy notes |
|---------|--------|-----------------------------|-----------------|------------------------|
| `quick` | Fast smoke | maxPages ≤ 25, depth ≤ 2 | HTTP, TLS basics, headers subset | console, json; failOn high |
| `standard` | Default balanced | maxPages 100, depth 5 | Website fundamentals | console, json; failOn high |
| `deep` | Broader crawl | maxPages ≥ 500, depth ≥ 8 | Full website catalog applicable | + markdown/html optional |
| `api` | API contract | maxPages ≤ 10, depth ≤ 1 | API / OpenAPI / JSON assertions | crawl effectively off |
| `ci` | CI gate | standard-like limits | Policy `ci-strict` defaults | console, json, sarif, junit, markdown; failOn high |

Profiles never disable the authorization or SSRF gates.

---

## Built-in policies

| Policy name | failOn | Emphasis | Typical use |
|-------------|--------|----------|-------------|
| `balanced` | high | Default mix | Local + general CI |
| `security-focused` | medium | TLS, headers, cookies, redirects | Security review |
| `quality-focused` | medium | HTML, a11y, SEO, links, perf budgets | Content quality |
| `api-contract` | high | API status, JSON, OpenAPI coverage | Contract tests |
| `ci-strict` | high | Same as balanced + treat Warning as soft; optional failOnNew when baselines enabled | Pipelines |

Severity overrides and suppressions remain available on top of any preset via `policies.*`.

Policy evaluation: findings with status **Fail** at severity ≥ `failOn` cause exit **1**. Pass / N/A / Skipped / Suppressed do not. Error status is a scan-quality signal (may count as fail for `ci-strict` — Developer: treat Error as policy fail when `ci-strict`).

---

## Environment overlay

Prefix: `ARTR_PIEN_`. Nested keys use `__` (double underscore), matching Microsoft.Extensions.Configuration conventions.

Examples:

```text
ARTR_PIEN_Profile=ci
ARTR_PIEN_Policies__FailOn=medium
ARTR_PIEN_Crawl__MaxPages=50
ARTR_PIEN_Network__AllowPrivateNetworks=true
ARTR_PIEN_Output__Directory=artifacts/pien
```

Do not put raw secrets in env values that get logged; use `secret://env/NAME` references in config and store the secret in `NAME`.

---

## CLI overrides (highest)

| Flag | Config path |
|------|-------------|
| `--profile` | `profile` |
| `--fail-on` | `policies.failOn` |
| `--max-pages` | `crawl.maxPages` |
| `--max-depth` | `crawl.maxDepth` |
| `--timeout` | overall scan timeout (limits) |
| `--format` | `output.formats` (replace) |
| `--output` | `output.directory` |
| `--baseline` | `baselines.id` + enable compare for that scan |
