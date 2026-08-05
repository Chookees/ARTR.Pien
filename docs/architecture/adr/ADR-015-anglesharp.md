> **Superseded:** Duplicate ADR ID from concurrent writes. Use **ADR-027-anglesharp.md** instead. See [README.md](README.md).

# ADR-015 — HTML parsing with AngleSharp

- **Status:** Superseded (see ADR-027)
- **Date:** 2026-08-05
- **Verified:** NuGet `AngleSharp` **1.7.0** (latest stable; MIT)

## Context

Crawl and HTML/a11y/SEO checks need a DOM without executing scripts or fetching resources.

## Decision

Use **AngleSharp 1.7.0** in Web for HTML parse/link extraction. Configure parsers with no script execution and no automatic requests. Reject HtmlAgilityPack (weaker HTML5 story) and regex-only HTML (fragile).

## Alternatives

| Option | Trade-off |
|--------|-----------|
| HtmlAgilityPack | Familiar; weaker HTML5 |
| Custom regex | Brittle, insecure |

## Consequences

Keep AngleSharp out of Core. Crawler and checks must still bound input size before parse.
