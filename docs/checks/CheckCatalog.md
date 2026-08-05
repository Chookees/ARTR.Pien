# Check catalog

**Status:** Implemented (v1)  
**Date:** 2026-08-05

Stable IDs never depend on execution order (ADR-016 / ADR-021). Format: `PIEN-{AREA}-{nnn}`.

**Legend — Implementation**

| Tag | Meaning |
|-----|---------|
| **I** | Implemented and registered in `AddPienChecks` |
| **D** | Designed for Developer (ID reserved / planned) |
| **C** | Constant exists in `CheckIds` but check body not yet registered |

`CheckIds` defines **48** constants. All **48** designed checks are implemented and registered.

## Summary counts

| Category | Designed | In CheckIds | Implemented (I) |
|----------|----------|-------------|-----------------|
| HTTP | 6 | 6 | 6 |
| TLS | 3 | 3 | 3 |
| Headers | 6 | 6 | 6 |
| Cookies | 3 | 3 | 3 |
| HTML | 4 | 4 | 4 |
| Accessibility | 5 | 5 | 5 |
| SEO | 4 | 4 | 4 |
| Link | 3 | 3 | 3 |
| API | 5 | 5 | 5 |
| OpenAPI | 4 | 4 | 4 |
| Change | 2 | 2 | 2 |
| Perf | 3 | 3 | 3 |
| **Total** | **48** | **48** | **48** |

## Master table

| ID | Category enum | Default severity | Name | Impl |
|----|---------------|------------------|------|------|
| PIEN-HTTP-001 | Reliability | High | HTTP availability / primary status | **I** |
| PIEN-HTTP-002 | Reliability | Medium | Redirect chain sanity | **I** |
| PIEN-HTTP-003 | Reliability | High | HTTPS downgrade / scheme regression | **I** |
| PIEN-HTTP-004 | Reliability | Medium | Excessive redirects | **I** |
| PIEN-HTTP-005 | Reliability | Medium | Unexpected server error (5xx) | **I** |
| PIEN-HTTP-006 | Reliability | Low | Content-Type / charset fundamentals | **I** |
| PIEN-TLS-001 | TransportSecurity | High | TLS protocol and certificate fundamentals | **I** |
| PIEN-TLS-002 | TransportSecurity | High | Certificate expiration proximity | **I** |
| PIEN-TLS-003 | TransportSecurity | Medium | Weak protocol / cipher observation | **I** |
| PIEN-HEADERS-001 | HttpSecurity | Medium | Security headers presence | **I** |
| PIEN-HEADERS-002 | HttpSecurity | Medium | CSP fundamentals | **I** |
| PIEN-HEADERS-003 | HttpSecurity | Medium | HSTS on HTTPS | **I** |
| PIEN-HEADERS-004 | HttpSecurity | Low | Frame ancestors / X-Frame-Options | **I** |
| PIEN-HEADERS-005 | HttpSecurity | Low | COOP / CORP / COEP observations | **I** |
| PIEN-HEADERS-006 | HttpSecurity | Info | Server disclosure headers | **I** |
| PIEN-COOKIE-001 | Cookies | Medium | Secure / HttpOnly / SameSite | **I** |
| PIEN-COOKIE-002 | Cookies | Medium | Cookie over HTTP / Secure missing | **I** |
| PIEN-COOKIE-003 | Cookies | Low | `__Host-` / `__Secure-` prefix rules | **I** |
| PIEN-HTML-001 | ContentQuality | Low | HTML structure fundamentals | **I** |
| PIEN-HTML-002 | ContentQuality | Low | Meta / viewport / charset | **I** |
| PIEN-HTML-003 | ContentQuality | Medium | Mixed-content references | **I** |
| PIEN-HTML-004 | ContentQuality | Low | Duplicate IDs / heading basics | **I** |
| PIEN-A11Y-001 | Accessibility | Low | lang, title, alt fundamentals | **I** |
| PIEN-A11Y-002 | Accessibility | Low | Form controls without labels | **I** |
| PIEN-A11Y-003 | Accessibility | Low | Empty links / buttons | **I** |
| PIEN-A11Y-004 | Accessibility | Low | Heading-level jumps | **I** |
| PIEN-A11Y-005 | Accessibility | Info | Positive tabindex observation | **I** |
| PIEN-SEO-001 | Discoverability | Low | SEO / discoverability fundamentals | **I** |
| PIEN-SEO-002 | Discoverability | Low | Title / description length budgets | **I** |
| PIEN-SEO-003 | Discoverability | Low | Canonical / indexability conflicts | **I** |
| PIEN-SEO-004 | Discoverability | Info | robots.txt / sitemap reachability | **I** |
| PIEN-LINK-001 | Discoverability | Medium | Broken or unsafe link detection | **I** |
| PIEN-LINK-002 | Discoverability | Medium | Broken internal links (crawl) | **I** |
| PIEN-LINK-003 | Discoverability | Low | Missing link text | **I** |
| PIEN-API-001 | ApiContract | High | API response contract fundamentals | **I** |
| PIEN-API-002 | ApiContract | High | Expected status / content-type | **I** |
| PIEN-API-003 | ApiContract | Medium | JSON assertions (pointer ops) | **I** |
| PIEN-API-004 | ApiContract | Medium | Local JSON Schema validation | **I** |
| PIEN-API-005 | ApiContract | High | Non-idempotent guard (blocked unless allowed) | **I** |
| PIEN-OPENAPI-001 | ApiContract | Medium | OpenAPI document fundamentals | **I** |
| PIEN-OPENAPI-002 | ApiContract | Medium | Operation ID / server URL hygiene | **I** |
| PIEN-OPENAPI-003 | ApiContract | Low | Response vs documented status/content-type | **I** |
| PIEN-OPENAPI-004 | ApiContract | Info | Configured operation coverage stats | **I** |
| PIEN-CHANGE-001 | ChangeStability | Medium | Baseline change detection | **I** |
| PIEN-CHANGE-002 | ChangeStability | Low | Volatile content fingerprint drift note | **I** |
| PIEN-PERF-001 | Performance | Low | Performance budget fundamentals | **I** |
| PIEN-PERF-002 | Performance | Low | TTFB / total duration budgets | **I** |
| PIEN-PERF-003 | Performance | Info | Body size budget | **I** |

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
