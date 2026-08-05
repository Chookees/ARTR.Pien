# Report formats

**Status:** Design (v1)  
**Date:** 2026-08-05  
Schema: [`config/schemas/pien-report.schema.json`](../../config/schemas/pien-report.schema.json)  
ADR: [ADR-017](../architecture/adr/ADR-017-report-formats.md), [ADR-005](../architecture/adr/ADR-005-sarif.md)

## Formats

| Token | Consumer | Destination | Requirements |
|-------|----------|-------------|--------------|
| `console` | Humans | stdout | Summary + top ≤50 findings; bracketed severity; no color-only meaning |
| `json` | Scripts | `{runId}.json` or stdout | camelCase; `schemaVersion: 1`; matches report schema |
| `sarif` | IDEs / code scanning | `{runId}.sarif` | SARIF **2.1.0**; hand-written STJ |
| `junit` | CI | `{runId}.junit` | Failures = `FindingStatus.Fail` |
| `markdown` | PRs/docs | `{runId}.markdown` | Tables; escape `\|`; `md` CLI alias |
| `html` | Browser | `{runId}.html` | Self-contained; **no CDN**; encoded text |

Multi-format: `--format console,json,sarif`. Console → stdout; files → `--output` / `output.directory`.

## Redaction (all formats)

Before export:

- Strip/mask Authorization, Cookie, Set-Cookie, API keys
- Bound evidence excerpts
- No raw secret material in metadata
- Webhook payloads use the same rules

## SARIF 2.1.0 mapping

| Pien | SARIF |
|------|-------|
| Tool name | `ARTR Pien` |
| Rule id | `checkId` (e.g. `PIEN-HTTP-001`) |
| Result message | finding summary |
| Critical / High | `level: error` |
| Medium | `level: warning` |
| Low / Info | `level: note` |
| Locations | URI/path when `FindingLocation` present |

`$schema`: `https://json.schemastore.org/sarif-2.1.0.json`, `version`: `2.1.0`.

## JUnit

- One `testsuite` named `ARTR.Pien`
- One `testcase` per finding (or per check — **design:** per Fail finding as failure; Pass may be omitted or included as success — prefer **one testcase per finding**, failures for Fail status)
- `failures` attribute = count of Fail

## Markdown

```markdown
# ARTR Pien report
Run: …
Policy: PASS|FAIL
| Severity | Check | Title |
|----------|-------|-------|
```

## HTML (self-contained)

| Requirement | Spec |
|-------------|------|
| Assets | Inline CSS only; **no** external scripts/fonts/CDN |
| Language | `lang="en"` |
| Landmarks | header, main#main-content, footer optional |
| Skip link | First focusable → main |
| Tables | caption + `th scope="col"`; scroll region on small screens |
| Empty | Visible “No findings.” |
| Contrast | Neutrals; FAIL `#b00020`, PASS `#0b6a0b`; body ≥ 4.5:1 |
| Encoding | HTML-encode all dynamic strings |
| Motion | Respect `prefers-reduced-motion`; no required animation |
| Title | `ARTR Pien Report — {runId}` |

Scores are **advisory** only — no certification language.

## Empty / error states

| State | Behavior |
|-------|----------|
| Zero findings | Explicit empty message in console/HTML/MD; exit still 0 if policy pass |
| Partial export failure | Prefer exit **6**; mention which paths succeeded |
| Unknown format | Exit **6** + list supported tokens |
