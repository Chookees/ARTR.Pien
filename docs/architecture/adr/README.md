# ADR index (canonical)

**Date:** 2026-08-05  
**Policy:** Concurrent architect writes produced duplicate numeric IDs. This index defines the **single canonical file per theme**. Duplicate files remain on disk for history but are **Superseded** (see banner at top of each duplicate). Prefer links from this index only.

## Required themes → canonical ADR

| Theme (master requirements) | Canonical ADR | File |
|-----------------------------|---------------|------|
| Minimal dependency strategy | ADR-001 | [ADR-001-minimal-dependencies.md](ADR-001-minimal-dependencies.md) |
| CLI framework | ADR-002 | [ADR-002-cli-framework.md](ADR-002-cli-framework.md) |
| JSON Schema engine | ADR-003 | [ADR-003-json-schema.md](ADR-003-json-schema.md) |
| Product naming and PIEN phase model | ADR-004 | [ADR-004-naming-pien.md](ADR-004-naming-pien.md) |
| SARIF exporter (subset of report formats) | ADR-005 | [ADR-005-sarif.md](ADR-005-sarif.md) |
| .NET 10 and C# | ADR-006 | [ADR-006-dotnet10.md](ADR-006-dotnet10.md) |
| Project boundaries | ADR-007 | [ADR-007-project-boundaries.md](ADR-007-project-boundaries.md) |
| Power-of-Ten adaptation | ADR-008 | [ADR-008-power-of-ten.md](ADR-008-power-of-ten.md) |
| No containers | ADR-009 | [ADR-009-no-containers.md](ADR-009-no-containers.md) |
| No external mandatory services | ADR-010 | [ADR-010-no-external-services.md](ADR-010-no-external-services.md) |
| Safe HTTP transport and SSRF controls | ADR-011 | [ADR-011-safe-http-ssrf.md](ADR-011-safe-http-ssrf.md) |
| Explicit redirect handling | ADR-012 | [ADR-012-explicit-redirects.md](ADR-012-explicit-redirects.md) |
| No automatic destructive API execution | ADR-013 | [ADR-013-no-destructive-api-auto-exec.md](ADR-013-no-destructive-api-auto-exec.md) |
| JSON configuration | ADR-014 | [ADR-014-json-config.md](ADR-014-json-config.md) |
| Local file storage | ADR-015 | [ADR-015-local-file-storage.md](ADR-015-local-file-storage.md) |
| Stable check identifiers | ADR-016 | [ADR-016-stable-check-ids.md](ADR-016-stable-check-ids.md) |
| Report formats | ADR-017 | [ADR-017-report-formats.md](ADR-017-report-formats.md) |
| Compile-time extension model | ADR-018 | [ADR-018-compile-time-extensions.md](ADR-018-compile-time-extensions.md) |
| No runtime plugin loading | ADR-019 | [ADR-019-no-runtime-plugins.md](ADR-019-no-runtime-plugins.md) |
| Apache-2.0 licensing | ADR-020 | [ADR-020-apache-2.md](ADR-020-apache-2.md) |
| Compliance-readiness boundaries | ADR-021 | [ADR-021-compliance-boundaries.md](ADR-021-compliance-boundaries.md) |
| Direct work on main (initial implementation) | ADR-022 | [ADR-022-work-on-main.md](ADR-022-work-on-main.md) |
| Regular commit policy | ADR-023 | [ADR-023-commit-policy.md](ADR-023-commit-policy.md) |

## Supporting ADRs (unique IDs, no theme conflict)

| ID | Title | File |
|----|-------|------|
| ADR-024 | OpenAPI document handling | [ADR-024-openapi.md](ADR-024-openapi.md) |
| ADR-025 | Baselines | [ADR-025-baselines.md](ADR-025-baselines.md) |
| ADR-026 | Roslyn analyzers for Power-of-Ten | [ADR-026-analyzers.md](ADR-026-analyzers.md) |
| ADR-027 | HTML parsing with AngleSharp | [ADR-027-anglesharp.md](ADR-027-anglesharp.md) |

## Superseded duplicates (do not cite as primary)

| Duplicate file | Conflicts with | Use instead |
|----------------|----------------|-------------|
| [ADR-004-openapi.md](ADR-004-openapi.md) | ADR-004 naming | ADR-024 |
| [ADR-006-ssrf-safe-http.md](ADR-006-ssrf-safe-http.md) | ADR-006 .NET 10 | ADR-011 |
| [ADR-007-local-file-storage.md](ADR-007-local-file-storage.md) | ADR-007 boundaries | ADR-015 |
| [ADR-008-power-of-ten-analyzers.md](ADR-008-power-of-ten-analyzers.md) | ADR-008 Power-of-Ten | ADR-026 |
| [ADR-009-baselines.md](ADR-009-baselines.md) | ADR-009 no containers | ADR-025 |
| [ADR-010-report-formats.md](ADR-010-report-formats.md) | ADR-010 no external services | ADR-017 |
| [ADR-011-project-boundaries.md](ADR-011-project-boundaries.md) | ADR-011 SSRF | ADR-007 |
| [ADR-012-no-containers.md](ADR-012-no-containers.md) | ADR-012 redirects | ADR-009 (+ ADR-010) |
| [ADR-013-compile-time-extensions.md](ADR-013-compile-time-extensions.md) | ADR-013 no destructive API | ADR-018 |
| [ADR-014-webhooks.md](ADR-014-webhooks.md) | ADR-014 JSON config | TrustBoundaries B7 + Architecture (optional future ADR-028) |
| [ADR-015-anglesharp.md](ADR-015-anglesharp.md) | ADR-015 local storage | ADR-027 |
| [ADR-016-product-naming.md](ADR-016-product-naming.md) | ADR-016 check IDs | ADR-004 |
| [ADR-017-dotnet10.md](ADR-017-dotnet10.md) | ADR-017 report formats | ADR-006 |
| [ADR-018-licensing-compliance.md](ADR-018-licensing-compliance.md) | ADR-018 extensions | ADR-020 + ADR-021 |
| [ADR-019-main-branch-workflow.md](ADR-019-main-branch-workflow.md) | ADR-019 no plugins | ADR-022 |
| [ADR-020-coverage.md](ADR-020-coverage.md) | ADR-020 Apache-2.0 | Coverage gates in `Directory.Build.props` / DependencyResearch (optional future ADR-029) |
| [ADR-021-stable-check-ids.md](ADR-021-stable-check-ids.md) | ADR-021 compliance | ADR-016 |
| [ADR-022-no-destructive-api.md](ADR-022-no-destructive-api.md) | ADR-022 work on main | ADR-013 |

All canonical ADRs dated **2026-08-05** unless amended later.
