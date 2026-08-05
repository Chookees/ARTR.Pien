# Limits

**Status:** Design (v1)

Configuration may lower product defaults but **must never exceed** `HardLimits` (`ARTR.Pien.Core.Limits.HardLimits`). Validator clamps or rejects values outside range.

## Product defaults (`ScanLimits.Default`)

| Limit | Default |
|-------|---------|
| Global request concurrency | 8 |
| Per-host concurrency | 4 |
| Max crawl pages | 100 |
| Max crawl depth | 5 |
| Max redirects | 10 |
| Max links per page | 1,000 |
| Max DNS addresses | 16 |
| Connect timeout | 10s |
| Request timeout | 30s |
| Overall scan timeout | 10m |
| Body inspection | 5 MiB |
| Max header count | 200 |
| Evidence excerpt | 4 KiB |
| Findings per check | 1,000 |
| Report findings | 10,000 |
| Regex timeout | 250ms |

## Hard ceilings (non-bypassable)

| Limit | Ceiling |
|-------|---------|
| Global concurrency | 64 |
| Per-host concurrency | 32 |
| Crawl pages | 10,000 |
| Crawl depth | 50 |
| Redirects | 50 |
| Links per page | 10,000 |
| DNS addresses | 64 |
| Connect timeout | 2m |
| Request timeout | 5m |
| Overall scan timeout | 2h |
| Body inspection | 50 MiB |
| Header count | 1,000 |
| Evidence excerpt | 64 KiB |
| Findings per check | 10,000 |
| Report findings | 100,000 |
| Regex timeout | 5s |

## Config mapping

- `crawl.maxPages` / `maxDepth` / `maxLinksPerPage`
- `network.maxRedirects`, `connectTimeoutSeconds`, `requestTimeoutSeconds`
- CLI `--max-pages`, `--max-depth`, `--timeout`
- Profile clamps (see [ProfilesAndPolicies.md](ProfilesAndPolicies.md))

Exhaustion is a failure mode, not unbounded scale-out.
