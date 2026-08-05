# Architecture overview — ARTR Pien

**ARTR Pien** (`pien`) is a local-first, deterministic website and API verification engine.  
**PIEN** = Proba → Inspice → Examina → Nuntia (**Test → Inspect → Examine → Report**).

## Product identity

| Item | Value |
|------|-------|
| Solution | `ARTR.Pien.sln` |
| Namespace | `ARTR.Pien` |
| CLI | `pien` |
| Config | `pien.json` / `ARTR:Pien` / `ARTR_PIEN_` |
| State | `.pien/` |
| License | Apache-2.0 |
| Platform | .NET 10 / `net10.0` |

Pien is **not** an exploit framework, compliance certification tool, or hosted control plane. A successful scan is **not** proof that a system is secure.

## Goals (v1)

- Authorized, non-destructive HTTP/HTTPS probing with SSRF-resistant transport
- Bounded website crawl (no JS execution, no browser automation)
- Deterministic website and API checks with stable IDs
- Policy evaluation, advisory scoring, baselines, multi-format reports
- Embeddable library + CLI + CI gate + local watch mode
- Offline tests after restore; **no Docker**, databases, Redis, or cloud dependencies

## Logical components

```text
Cli ──► Hosting (DI composition)
          ├── Engine      (PIEN orchestration, policy, scoring, catalog)
          ├── Web         (SSRF-safe HTTP, TLS probe, crawler)
          ├── Checks      (built-in deterministic checks)
          ├── Reporting   (console/json/sarif/junit/md/html)
          ├── Storage     (`.pien` runs/baselines)
          └── Core        (immutable domain, config, abstractions)
CodeAnalysis (Roslyn Power-of-Ten analyzers; build-time only)
```

## Trust boundaries

| Boundary | Rule |
|----------|------|
| Operator → CLI | Config/flags only; no remote control plane |
| CLI → targets | Explicit `authorization.confirmed`; destination validation before connect |
| Secrets | Reference-only (`env:`, `file:`); never logged or stored in baselines/reports |
| Notifications | Optional HTTPS webhooks; HMAC optional; failure must not erase scan results |
| Extension | Compile-time DI registration only; no runtime plugin loading |

## Deployment model

- Publish self-contained or framework-dependent CLI via `build/Publish.*`
- Local scripts under `build/` (PowerShell + Bash); GitHub Actions for CI
- **No containers** in product or mandatory test workflows

## Observability

- Console progress by PIEN stage (English names in UX)
- Structured logging via `Microsoft.Extensions.Logging` (Hosting)
- Opt-in `ActivitySource` / `Meter` hooks (no mandatory telemetry egress)
- Exit codes `0–10` (`PienExitCode`) for CI

## Scaling limits (v1)

Hard ceilings live in `HardLimits` (e.g. crawl pages ≤ 10 000, body inspection ≤ 50 MiB).  
v1 is single-process, local-file state. Concurrent overlapping watch runs are forbidden. Distributed scheduling/storage are non-goals.

## Failure modes

| Failure | Handling |
|---------|----------|
| Invalid config/args | Exit 2–3; no network |
| Unauthorized / unsafe target | Exit 4; no connect |
| Probe/scan failure | Exit 5; partial findings may still persist |
| Report export failure | Exit 6 |
| Baseline ops failure | Exit 7 |
| Storage I/O failure | Exit 8 |
| Required notification failure | Exit 9 |
| Cancellation | Exit 10 |

## Related docs

- [Architecture.md](Architecture.md) — expanded overview (identity, limits, lifecycle)
- [PIEN-Pipeline.md](PIEN-Pipeline.md)
- [DependencyGraph.md](DependencyGraph.md)
- [CodingStandards.md](CodingStandards.md)
- [DataFlow.md](DataFlow.md) · [TrustBoundaries.md](TrustBoundaries.md) · [ExtensionModel.md](ExtensionModel.md)
- [ADR index](adr/README.md) (ADR-001–ADR-029)
- [DependencyResearch.md](../development/DependencyResearch.md)
- [PowerOfTen.md](../development/PowerOfTen.md)
