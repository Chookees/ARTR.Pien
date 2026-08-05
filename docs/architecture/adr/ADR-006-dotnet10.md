# ADR-006 — .NET 10 / net10.0

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Pien needs a modern LTS-aligned runtime with BCL HTTP/JSON improvements, `TimeProvider`, and analyzer alignment.

## Decision

- Target **`net10.0`** for all production and test projects.
- Pin SDK in `global.json` to **10.0.302** (`rollForward: latestFeature`, `allowPrerelease: false`).
- Align `Microsoft.Extensions.*` package versions to the **10.0.10** band (verified nuget.org 2026-08-05).
- Align Roslyn analyzer packages to **5.6.0** (ships with SDK 10.0.301+).

## Consequences

- Contributors must install SDK ≥ 10.0.302.
- No multi-targeting in v1 (reduces matrix complexity).
- Reject jumping to .NET 11 preview packages for production dependencies.
