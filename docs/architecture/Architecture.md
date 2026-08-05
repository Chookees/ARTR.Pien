# ARTR Pien — Architecture Overview

**Status:** Accepted (v1 architecture)  
**Date:** 2026-08-05  
**Audience:** Developers, reviewers, embedders

## 1. Product identity

| Name | Meaning |
|------|---------|
| **ARTR Pien** | Product / solution name (`ARTR.Pien.sln`) |
| **Pien** | Short product name |
| **PIEN** | Acronym and pipeline: **P**roba · **I**nspice · **E**xamina · **N**untia |
| English UX | **Test → Inspect → Examine → Report** (CLI never shows Latin stage names) |
| CLI | `pien` |
| Config | `pien.json`, configuration section `ARTR:Pien`, env prefix `ARTR_PIEN_` |
| Namespace | `ARTR.Pien.*` |
| License | Apache-2.0 |

Pien is a **local-first, deterministic verification CLI** for **authorized** HTTP/HTTPS websites and APIs. It produces actionable findings and multi-format reports. It does **not** claim NASA/JPL certification, SOC 2 / ISO 27001 certification, or penetration-test equivalence.

## 2. PIEN stages

| Stage | Latin | English (`ScanStage`) | Responsibility |
|-------|-------|------------------------|----------------|
| 1 | Proba | Testing | Probe authorized targets via SSRF-safe HTTP/TLS; respect limits and cancellation |
| 2 | Inspice | Inspecting | Bound and normalize response evidence (headers, truncated body, TLS facts, crawl pages) |
| 3 | Examina | Examining | Run registered checks against evidence; emit findings with stable check IDs |
| 4 | Nuntia | Reporting | Policy evaluation, advisory scores, exporters, optional `.pien/` persistence, notifications |

Orchestration lives in `ARTR.Pien.Engine.ScanEngine`. Transport and crawl live in `ARTR.Pien.Web`. Checks live in `ARTR.Pien.Checks`. Exporters live in `ARTR.Pien.Reporting`.

## 3. Project map

| Project | Role |
|---------|------|
| `ARTR.Pien.Core` | Domain models, contracts (`ICheck`, stores, transport interfaces), config types, limits, findings, redaction helpers — **no** project references to other Pien assemblies |
| `ARTR.Pien.Web` | Destination validation, safe HTTP transport, crawler, TLS probe |
| `ARTR.Pien.Checks` | Built-in check implementations (compile-time registered) |
| `ARTR.Pien.Engine` | Scan orchestration, policy evaluator, score calculator, check catalog |
| `ARTR.Pien.Reporting` | Report exporters (JSON, console, SARIF, JUnit, Markdown, HTML) |
| `ARTR.Pien.Storage` | Local filesystem store under `.pien/` |
| `ARTR.Pien.Hosting` | DI composition (`AddPien`), config loader, secret resolver wiring |
| `ARTR.Pien.Cli` | `pien` entry point (`System.CommandLine`) |
| `ARTR.Pien.CodeAnalysis` | Roslyn analyzers **PIEN0001–PIEN0009** implemented; **PIEN0010–PIEN0012** deferred (ADR-030, PowerOfTen.md, Analyzers.md) |
| Samples / benchmarks / tests | Loopback-only verification; no containers |

## 4. Acyclic dependency graph

```text
Cli ──► Hosting ──► Engine ──► Core
              │         ▲
              ├─────────┼──► Web ──► Core
              ├─────────┼──► Checks ──► Web ──► Core
              ├─────────┼──► Reporting ──► Core
              └─────────┴──► Storage ──► Core

CodeAnalysis  (standalone analyzer package; referenced as analyzer, not runtime graph)
```

**Rules (enforced by architecture tests):**

- `Core` references **no** other `ARTR.Pien.*` assemblies.
- `Engine`, `Web`, `Reporting`, `Storage` reference **only** `Core` among Pien projects (`Checks` may also reference `Web`).
- `Hosting` composes Engine + Web + Checks + Reporting + Storage + Core; never referenced by those libraries.
- `Cli` references `Hosting` (and thus the composition root).
- No cycles. No runtime plugin loading. No MediatR-style bus between projects.

## 5. Lifecycle

1. **Init / validate** — operator creates or validates `pien.json` (`pien init`, `pien validate`).
2. **Authorize** — each target requires `authorization.confirmed: true` before probes.
3. **Compose** — Hosting builds DI; checks registered explicitly in `AddPienChecks`.
4. **Scan** — Engine runs four stages with `HardLimits` / `ScanLimits` and cancellation.
5. **Persist (optional)** — runs, reports, baselines under `.pien/` via atomic writes.
6. **Export / notify** — exporters write redacted reports; webhook/file notifications never include secrets.
7. **Exit** — `PienExitCode` drives CI fail/pass.

## 6. Hard limits (non-bypassable ceilings)

Configuration may lower defaults but **must never exceed** `HardLimits` (see `ARTR.Pien.Core.Limits.HardLimits`): concurrency, crawl pages/depth, redirects, body size, findings caps, regex timeout, scan timeout. Product defaults live in `ScanLimits.Default`.

## 7. Non-goals (v1)

- Docker / containers / Testcontainers
- Databases, Redis, message brokers, mandatory cloud services
- Browser automation (Playwright/Puppeteer)
- Runtime plugin / MEF / AssemblyLoadContext extensions
- Destructive API mutation without explicit, separate operator confirmation (default: **read-only** probes)
- Claiming certification, exploit delivery, or offensive security tooling
- Nested `ARTR/Pien/ARTR/Pien` path layouts

## 8. Embedding

Host applications reference `ARTR.Pien.Hosting` and call `services.AddPien(options => …)`. Supply working directory, state directory, network allowlists for loopback/private targets. Embedders must not bypass `ISafeHttpTransport` / `IDestinationValidator` for scan traffic.

## 9. Deployment model — no containers

Ship as a .NET 10 tool / published binary. CI installs the SDK from `global.json` and runs `dotnet test`. Operators run `pien` on the machine that may reach authorized targets. **No Dockerfile, no compose files, no containerized test dependency.**

## 10. Observability & scaling

- Progress via `IProgress<ScanProgress>`; console logging via `Microsoft.Extensions.Logging`.
- Optional `ActivitySource` / `Meter` hooks (BCL) — no required OTLP backend.
- Scale limit: single-process, bounded concurrency; large crawls are capped by `HardLimits`. Exhaustion is a failure mode, not an unbounded scale-out story.

## 11. Related documents

- [DataFlow.md](DataFlow.md)
- [TrustBoundaries.md](TrustBoundaries.md)
- [ExtensionModel.md](ExtensionModel.md)
- [adr/](adr/)
- [../development/PowerOfTen.md](../development/PowerOfTen.md)
- [../development/DependencyResearch.md](../development/DependencyResearch.md)
- [../security/ThreatModel.md](../security/ThreatModel.md)
