# Website checks

**Status:** Design (v1)  
Parent: [CheckCatalog.md](CheckCatalog.md)

Applies primarily to `kind: website` (some HTTP/TLS also apply to API base URL).

## HTTP

| ID | Impl | Evaluates |
|----|------|-----------|
| PIEN-HTTP-001 | **I** | Primary probe status 2xx/3xx |
| PIEN-HTTP-002 | **C** | Redirect loop / chain sanity |
| PIEN-HTTP-003 | **D** | HTTPS → HTTP downgrade |
| PIEN-HTTP-004 | **D** | Redirect count vs `maxRedirects` policy |
| PIEN-HTTP-005 | **D** | 5xx on primary or crawled pages |
| PIEN-HTTP-006 | **D** | Content-Type / charset when HTML expected |

## TLS

| ID | Impl | Evaluates |
|----|------|-----------|
| PIEN-TLS-001 | **C** | Handshake, cert chain trust, hostname |
| PIEN-TLS-002 | **C** | NotAfter proximity (warn/fail thresholds) |
| PIEN-TLS-003 | **D** | Obsolete protocol observation |

Evidence from `ITlsProbe`. HTTP-only targets → NotApplicable for TLS checks.

## Headers

| ID | Impl | Evaluates |
|----|------|-----------|
| PIEN-HEADERS-001 | **I** | CSP, X-Content-Type-Options, Referrer-Policy, Permissions-Policy presence |
| PIEN-HEADERS-002 | **I** | CSP quality (e.g. unsafe-inline) |
| PIEN-HEADERS-003 | **D** | HSTS (HTTPS only) |
| PIEN-HEADERS-004 | **D** | frame-ancestors / X-Frame-Options |
| PIEN-HEADERS-005 | **D** | COOP/CORP/COEP (contextual; often Info/Low) |
| PIEN-HEADERS-006 | **D** | Server / X-Powered-By disclosure (Info) |

Missing headers are not automatically Critical.

## Cookies

| ID | Impl | Evaluates |
|----|------|-----------|
| PIEN-COOKIE-001 | **I** | Secure, HttpOnly, SameSite on Set-Cookie |
| PIEN-COOKIE-002 | **D** | Secure cookies over HTTP |
| PIEN-COOKIE-003 | **D** | Prefix attribute rules |

**Never** include cookie values in findings.

## HTML

| ID | Impl | Evaluates |
|----|------|-----------|
| PIEN-HTML-001 | **I** | html/title/body fundamentals (AngleSharp) |
| PIEN-HTML-002 | **D** | meta description, viewport, charset |
| PIEN-HTML-003 | **D** | mixed-content refs on HTTPS pages |
| PIEN-HTML-004 | **D** | duplicate IDs / heading outline basics |

No regex HTML parsing.

## SEO / links / perf (website)

| ID | Impl | Area |
|----|------|------|
| PIEN-SEO-001..004 | **C/D** | Title, description, canonical, robots/sitemap |
| PIEN-LINK-001..003 | **C/D** | Broken/unsafe links, internal crawl, empty text |
| PIEN-PERF-001..003 | **C/D** | Timing and size budgets (not Core Web Vitals) |

Crawl-bounded only (`HardLimits`). Same-origin default.
