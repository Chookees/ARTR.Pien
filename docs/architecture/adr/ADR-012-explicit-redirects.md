# ADR-012 — Explicit redirects

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

`HttpClient` automatic redirect following can skip revalidation and enable redirect-to-private SSRF or credential forwarding.

## Decision

Disable automatic redirects. Implement an **explicit hop loop** in `SafeHttpTransport`:

- Re-run destination validation on each `Location`.
- Block HTTPS → HTTP downgrades.
- Strip userinfo when crossing host/port/scheme.
- Enforce `MaxRedirects` (config ≤ `HardLimits.MaxRedirects`).

## Consequences

- Redirect chains appear in probe metadata for checks (`PIEN-HTTP-002`).
- Slightly more code than `AllowAutoRedirect=true`, but safer and testable.
