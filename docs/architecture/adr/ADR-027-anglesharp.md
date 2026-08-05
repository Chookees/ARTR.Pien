# ADR-027 — HTML parsing with AngleSharp

- **Status:** Accepted
- **Date:** 2026-08-05

## Context

Website checks need a tolerant HTML DOM. Regex-only HTML is fragile; browser engines are out of scope.

## Decision

Use **AngleSharp 1.7.0** (MIT; verified nuget.org 2026-08-05) with:

- **No** script execution
- **No** automatic network requests / resource loading
- Parse only bounded body excerpts already downloaded via safe transport

Reject HtmlAgilityPack for new code unless AngleSharp cannot meet a need (re-ADR required). Reject Playwright/Puppeteer (non-goal).

## Consequences

- Deterministic HTML inspection in-process.
- Must configure browsing context carefully to avoid footguns.
- Transitive surface reviewed in DependencyResearch.
