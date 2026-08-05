# Coding standards — ARTR Pien

## Language and build

- C# latest / `net10.0` / nullable enabled / deterministic builds
- `TreatWarningsAsErrors`, analyzers as errors, `AllowUnsafeBlocks=false`
- XML docs on public APIs (and security-sensitive internals)
- No `TODO` / `FIXME` / `NotImplementedException` / silent swallow in production paths

## Power of Ten (summary)

Follow [PowerOfTen.md](../development/PowerOfTen.md). Key hard rules:

- No `goto`, no unsafe, no `async void`, no sync-over-async
- Bound all loops/allocations (crawl, redirects, bodies, retries)
- Methods ≤ 60 logical lines (prefer ≤ 40) — **PIEN0001**
- Prefer `IClock` / `TimeProvider` over ambient time
- Observe all tasks; cancellation tokens flow through async APIs

## Domain style

- Immutable records/`Create` validators for Core contracts
- Stable check IDs (`PIEN-*`) never depend on execution order
- Explicit DI registration; no reflection-driven plugin discovery
- English identifiers in code and CLI; Latin only in architecture narrative

## Security coding

- All outbound HTTP through `ISafeHttpTransport`
- Re-validate DNS/IP on every redirect hop; block HTTPS→HTTP downgrade
- Redact secrets/cookies/auth headers before logging, storage, webhooks
- User-controlled regex must use timeout + `RegexOptions.NonBacktracking` where applicable
- Secure XML readers only; HTML via AngleSharp with script/network disabled

## Testing expectations

- Unit tests for Core/Web/Checks/Engine/Reporting/Storage pure logic
- Integration tests use loopback Kestrel samples only
- Functional smoke against SampleSite/SampleApi
- Architecture tests for dependency graph and forbidden deps
- Coverage gate: **≥ 90% line and branch** on production assemblies (Phase 15)

## Comments

Explain **why**, invariants, security decisions, and limits—not restatements of syntax.
