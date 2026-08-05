# Check catalog

**Status:** Design (v1)  
**Date:** 2026-08-05

Stable IDs never depend on execution order (ADR-016 / ADR-021). Format: `PIEN-{AREA}-{nnn}`.

**Legend — Implementation**

| Tag | Meaning |
|-----|---------|
| **I** | Implemented in current scaffold (`ARTR.Pien.Checks`) |
| **D** | Designed for Developer (ID reserved / planned) |
| **C** | Constant exists in `CheckIds` but check body not yet registered |

`CheckIds` currently defines **15** constants. Scaffold implements **6**. This catalog designs **48** checks total (15 core + 33 extensions).

## Summary counts

| Category | Designed | In CheckIds | Implemented (I) |
|----------|----------|-------------|-----------------|
| HTTP | 6 | 2 | 1 |
| TLS | 3 | 2 | 0 |
| Headers | 6 | 2 | 2 |
| Cookies | 3 | 1 | 1 |
| HTML | 4 | 1 | 1 |
| Accessibility | 5 | 1 | 1 |
| SEO | 4 | 1 | 0 |
| Link | 3 | 1 | 0 |
| API | 5 | 1 | 0 |
| OpenAPI | 4 | 1 | 0 |
| Change | 2 | 1 | 0 |
| Perf | 3 | 1 | 0 |
| **Total** | **48** | **15** | **6** |

## Master table

| ID | Category enum | Default severity | Name | Impl |
|----|---------------|------------------|------|------|
| PIEN-HTTP-001 | Reliability | High | HTTP availability / primary status | **I** |
| PIEN-HTTP-002 | Reliability | Medium | Redirect chain sanity | **C** |
| PIEN-HTTP-003 | Reliability | High | HTTPS downgrade / scheme regression | **D** |
| PIEN-HTTP-004 | Reliability | Medium | Excessive redirects | **D** |
| PIEN-HTTP-005 | Reliability | Medium | Unexpected server error (5xx) | **D** |
| PIEN-HTTP-006 | Reliability | Low | Content-Type / charset fundamentals | **D** |
| PIEN-TLS-001 | TransportSecurity | High | TLS protocol and certificate fundamentals | **C** |
| PIEN-TLS-002 | TransportSecurity | High | Certificate expiration proximity | **C** |
| PIEN-TLS-003 | TransportSecurity | Medium | Weak protocol / cipher observation | **D** |
| PIEN-HEADERS-001 | HttpSecurity | Medium | Security headers presence | **I** |
| PIEN-HEADERS-002 | HttpSecurity | Medium | CSP fundamentals | **I** |
| PIEN-HEADERS-003 | HttpSecurity | Medium | HSTS on HTTPS | **D** |
| PIEN-HEADERS-004 | HttpSecurity | Low | Frame ancestors / X-Frame-Options | **D** |
| PIEN-HEADERS-005 | HttpSecurity | Low | COOP / CORP / COEP observations | **D** |
| PIEN-HEADERS-006 | HttpSecurity | Info | Server disclosure headers | **D** |
| PIEN-COOKIE-001 | Cookies | Medium | Secure / HttpOnly / SameSite | **I** |
| PIEN-COOKIE-002 | Cookies | Medium | Cookie over HTTP / Secure missing | **D** |
| PIEN-COOKIE-003 | Cookies | Low | `__Host-` / `__Secure-` prefix rules | **D** |
| PIEN-HTML-001 | ContentQuality | Low | HTML structure fundamentals | **I** |
| PIEN-HTML-002 | ContentQuality | Low | Meta / viewport / charset | **D** |
| PIEN-HTML-003 | ContentQuality | Medium | Mixed-content references | **D** |
| PIEN-HTML-004 | ContentQuality | Low | Duplicate IDs / heading basics | **D** |
| PIEN-A11Y-001 | Accessibility | Low | lang, title, alt fundamentals | **I** |
| PIEN-A11Y-002 | Accessibility | Low | Form controls without labels | **D** |
| PIEN-A11Y-003 | Accessibility | Low | Empty links / buttons | **D** |
| PIEN-A11Y-004 | Accessibility | Low | Heading-level jumps | **D** |
| PIEN-A11Y-005 | Accessibility | Info | Positive tabindex observation | **D** |
| PIEN-SEO-001 | Discoverability | Low | SEO / discoverability fundamentals | **C** |
| PIEN-SEO-002 | Discoverability | Low | Title / description length budgets | **D** |
| PIEN-SEO-003 | Discoverability | Low | Canonical / indexability conflicts | **D** |
| PIEN-SEO-004 | Discoverability | Info | robots.txt / sitemap reachability | **D** |
| PIEN-LINK-001 | Discoverability | Medium | Broken or unsafe link detection | **C** |
| PIEN-LINK-002 | Discoverability | Medium | Broken internal links (crawl) | **D** |
| PIEN-LINK-003 | Discoverability | Low | Missing link text | **D** |
| PIEN-API-001 | ApiContract | High | API response contract fundamentals | **C** |
| PIEN-API-002 | ApiContract | High | Expected status / content-type | **D** |
| PIEN-API-003 | ApiContract | Medium | JSON assertions (pointer ops) | **D** |
| PIEN-API-004 | ApiContract | Medium | Local JSON Schema validation | **D** |
| PIEN-API-005 | ApiContract | High | Non-idempotent guard (blocked unless allowed) | **D** |
| PIEN-OPENAPI-001 | ApiContract | Medium | OpenAPI document fundamentals | **C** |
| PIEN-OPENAPI-002 | ApiContract | Medium | Operation ID / server URL hygiene | **D** |
| PIEN-OPENAPI-003 | ApiContract | Low | Response vs documented status/content-type | **D** |
| PIEN-OPENAPI-004 | ApiContract | Info | Configured operation coverage stats | **D** |
| PIEN-CHANGE-001 | ChangeStability | Medium | Baseline change detection | **C** |
| PIEN-CHANGE-002 | ChangeStability | Low | Volatile content fingerprint drift note | **D** |
| PIEN-PERF-001 | Performance | Low | Performance budget fundamentals | **C** |
| PIEN-PERF-002 | Performance | Low | TTFB / total duration budgets | **D** |
| PIEN-PERF-003 | Performance | Info | Body size budget | **D** |

## Category docs

- [WebsiteChecks.md](WebsiteChecks.md) — HTTP, TLS, headers, cookies, HTML, SEO, link, perf
- [ApiChecks.md](ApiChecks.md) — API, OpenAPI
- [AccessibilityChecks.md](AccessibilityChecks.md) — A11Y
- [SeverityModel.md](SeverityModel.md)

## Non-claims

These checks do **not** prove WCAG, SEO ranking, or security certification. No exploit payloads. No full browser/JS execution in v1.

## Developer notes

1. Register new checks in `AddPienChecks` (compile-time only).
2. Add constants to `CheckIds` before shipping a new ID; never renumber.
3. Prefer extending `WebsiteChecks` / new files per area rather than one mega-class.
4. Keep `pien explain` metadata in `CheckDefinition`.
