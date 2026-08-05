# PIEN pipeline

The engine executes four English stages. Latin stage names appear in architecture docs only.

| Stage | Latin | Responsibility |
|-------|-------|----------------|
| Testing | Proba | Bounded authorized probes (DNS/TLS/HTTP/API cases, timings) |
| Inspecting | Inspice | Normalize evidence (status, headers, cookies, TLS metadata, bodies truncated to limits, OpenAPI/JSON, crawl pages) |
| Examining | Examina | Deterministic checks, policy, schemas, budgets, baselines |
| Reporting | Nuntia | Reports, exit codes, optional webhooks, history persistence |

## Orchestration (`ScanEngine`)

1. Validate scan definition and authorization per target.
2. Build `ScanPlan` from selected check IDs (enable/disable filters).
3. **Testing:** for each target, probe via `ISafeHttpTransport` (and TLS/`ICrawler` when target kind and config require it).
4. **Inspecting:** assemble `InspectionEvidence` (primary probe + optional crawl/TLS/API evidence bags).
5. **Examining:** evaluate each selected `ICheck` with cancellation and per-check finding caps.
6. **Reporting:** evaluate policy, compute advisory category scores, persist run under `.pien/`, export formats.

Progress is reported as `ScanProgress(ScanStage, message, percent, completedChecks, totalChecks)`.

## Evidence model

- `ProbeRequest` / `ProbeResult` — single HTTP exchange (bounded body, redirect chain, timings).
- `InspectionEvidence` — target-scoped bag of probes and derived facts for checks.
- `Finding` — immutable result with stable `CheckId`, severity, status, redacted evidence excerpts.
- `PolicyResult` — fail-on severity gate for CI exit code 1.

## Crawl integration (required for website completeness)

When crawl is enabled (`Crawl.MaxPages` > 1 or profile default):

1. Engine obtains `ICrawler` from Hosting composition.
2. Crawler yields same-origin pages iteratively (robots/sitemap optional, depth/page/link caps).
3. Engine probes pages (or reuses crawler fetches) and merges into evidence for HTML/a11y/SEO/link checks.

**Current gap:** crawler and TLS probe are registered but not yet driven by `ScanEngine` (see Developer gap list).

## Check execution rules

- Explicit catalog registration (no assembly scanning).
- Checks are pure relative to injected evidence + safe transport helpers they may call through abstractions.
- No destructive HTTP methods by default; API cases must declare idempotent methods unless explicitly allowed.
- Resource bounds enforced via `ScanLimits` capped by `HardLimits`.

## Policy and scoring

- Policy: fail when any finding with `Status=Fail` and severity ≥ configured threshold.
- Scoring: advisory pass-rate by check/category; **never** certification language.

## Baselines (Nuntia + Examina)

Baselines store normalized fingerprints and finding sets (no secrets/cookies/bodies).  
Comparison classifies: `New | Resolved | Unchanged | Changed`.
