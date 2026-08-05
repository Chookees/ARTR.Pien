# ADR-029 — Coverage gate strategy

- **Status:** Accepted (amended)
- **Date:** 2026-08-05

## Context

Master scope requires ≥90% line and branch coverage on production assemblies with Cobertura and HTML reports.

L-COV-1: historical `AssemblyName=pien` for the CLI exe caused Cobertura hosts to omit `ARTR.Pien.Cli`. Process-spawned functional tests also do not attribute hits to the parent Coverlet session.

## Decision

- Collect coverage via coverlet in `build/Test.ps1` and CI.
- Enforce ≥90% line and branch **average** across production packages: Core, Web, Checks, Engine, Reporting, Storage, Hosting, **and Cli**.
- Keep Cli `AssemblyName` as `ARTR.Pien.Cli` (`ToolCommandName` remains `pien`).
- Exercise Cli **in-process** (`Program.RunAsync`) so Coverlet records hits; retain process-spawned exit-code tests for realism.
- Run `dotnet test` sequentially (`maxcpucount:1`) and reconcile per-package rates as the **max across Cobertura hosts** so a raced zero-hit Cli package cannot erase a real host.
- Fail the gate if `ARTR.Pien.Cli` is absent from Cobertura.
- Exclude generated `*.g.cs` / compiler-generated attributes; do not exclude difficult production logic solely to inflate scores.
- Mutation testing may run on a schedule; not mandatory for every local build.

## Consequences

- L-COV-1 is closed with real Cli measurement in the same average gate as libraries.
- Incomplete features must still have boundary tests. Stub handlers remain forbidden.
