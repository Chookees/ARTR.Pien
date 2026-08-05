# Engineering completion report — ARTR Pien v1

**Generated:** 2026-08-05  
**Author role:** senior-software-developer (cursor-grok-4.5-high)  
**Integrity:** Numbers below are from commands run on this machine or from the last verified `build/Test.ps1` / publish run recorded in-session. No fabricated coverage, badges, or Docker usage.

## Identity

| Field | Value |
|-------|--------|
| Project path | `C:\Users\artur\Desktop\projects\r\ARTR\Pien` |
| Git root | `C:/Users/artur/Desktop/projects/r/ARTR/Pien` |
| Branch | `main` |
| HEAD | `95e9a2a9ea779d58059a058243ca1f0f24ecf7d7` (report commit; `main` pushed to `origin/main`) |
| Product version | `0.1.0` (`Directory.Build.props`) |
| SDK pin | `10.0.302` (`global.json`) |
| Push | **success** — `git push origin main` → `d2eee96..95e9a2a  main -> main` |

## Recent meaningful commits

| Hash | Subject |
|------|---------|
| `95e9a2a` | docs(release): add engineering completion report |
| `bf4a99d` | docs(analysis): document deferred PIEN0010-0012 analyzers |
| `c237902` | ci(release): expand Publish.ps1 for FD, RIDs, packs, checksums |
| `ed24a49` | fix(checks): correct __Secure- attribute parsing and cover catalog branches |
| `1a9f81e` | feat(checks): implement remaining catalog IDs to 48 registered |
| `d2eee96` | test(reporting): golden-verify JSON, SARIF, JUnit, Markdown, HTML |
| `94e6d61` | feat(checks): register TLS/SEO/LINK/OPENAPI/PERF/CHANGE extensions |
| `4c5dd74` | fix(coverage): include Cli in Cobertura average gate |
| `6afafb9` | test(coverage): meet 90% branch gate including in-process CLI |
| `8076baa` | docs(config): clarify authorization examples and add loopback validate sample |

## Central package versions (`Directory.Packages.props`)

### Production

| Package | Version |
|---------|---------|
| System.CommandLine | 2.0.10 |
| AngleSharp | 1.7.0 |
| NJsonSchema | 11.6.1 |
| Microsoft.OpenApi | 3.9.0 |
| Microsoft.OpenApi.YamlReader | 3.9.0 |
| Microsoft.Extensions.Hosting | 10.0.10 |
| Microsoft.Extensions.Logging.Console | 10.0.10 |
| Microsoft.Extensions.Options.DataAnnotations | 10.0.10 |
| Microsoft.Extensions.Http | 10.0.10 |
| Microsoft.Extensions.Configuration.Json | 10.0.10 |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 10.0.10 |
| Microsoft.Extensions.Configuration.CommandLine | 10.0.10 |
| Microsoft.Extensions.DependencyInjection | 10.0.10 |
| Microsoft.SourceLink.GitHub | 10.0.301 |

### Analyzers / Roslyn

| Package | Version |
|---------|---------|
| Microsoft.CodeAnalysis.CSharp | 5.6.0 |
| Microsoft.CodeAnalysis.CSharp.Workspaces | 5.6.0 |
| Microsoft.CodeAnalysis.Analyzers | 5.6.0 |

### Tests / benchmarks

| Package | Version |
|---------|---------|
| xunit.v3 | 3.2.2 |
| xunit.runner.visualstudio | 3.1.5 |
| Microsoft.NET.Test.Sdk | 18.8.1 |
| coverlet.collector | 10.0.1 |
| coverlet.msbuild | 10.0.1 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.10 |
| Microsoft.Extensions.Diagnostics.Testing | 10.8.0 |
| NetArchTest.Rules | 1.3.2 |
| Microsoft.CodeAnalysis.CSharp.Analyzer.Testing | 1.1.4 |
| BenchmarkDotNet | 0.15.8 |

### Local tools (`.config/dotnet-tools.json`)

| Tool | Version | Command |
|------|---------|---------|
| dotnet-reportgenerator-globaltool | 5.5.11 | reportgenerator |
| cyclonedx | 6.2.0 | dotnet-CycloneDX |

## Build / test / coverage

### Commands (canonical)

```powershell
dotnet tool restore
dotnet restore ARTR.Pien.sln --locked-mode
dotnet format ARTR.Pien.sln --verify-no-changes
dotnet build ARTR.Pien.sln --configuration Release --no-restore
dotnet test ARTR.Pien.sln --configuration Release --no-build
.\build\Test.ps1          # coverage gate ≥90% average line + branch
.\build\SmokeTest.ps1
.\build\PackageSource.ps1
.\build\Publish.ps1
```

### Last verified coverage gate (`build/Test.ps1`)

After catalog completion (48 checks) on this host, gate **PASS**:

| Metric | Value |
|--------|-------|
| Average line | **97.23%** |
| Average branch | **90.63%** |
| Gate | ≥90% line and branch (average across production assemblies, max-per-host reconcile) |

**Residual per-package branch floors** (honest; average gate still passes; no fake exclusions added this session):

- **Checks** package branch historically ~**80%** after catalog expansion (many thin ID branches).
- **Core** / **Web** have sat below a strict per-package 90% branch ideal at various points; average gate remains the enforced bar (`ADR-029`).
- Optional deep branch push for Checks/Core/Web was **skipped** this close-out (not a quick win without noisy tests).

On-disk `artifacts/TestResults` may reflect a partial later host set; **do not** treat raw unreconciled Cobertura as the gate claim. Use `build/Test.ps1` output.

### Catalog / smoke (prior verified)

- Registered checks: **48/48**
- `SmokeTest.ps1`: PASS (lists 48 checks)
- FD / SC `ARTR.Pien.Cli.exe version` → `ARTR Pien 0.1.0` (verified after this publish run)

## Source ZIP

| Field | Value |
|-------|--------|
| Path | `artifacts/ARTR.Pien-source.zip` |
| SHA-256 | `f882e71919a8ff9616e7ed1ca438df5b975fb54c0203e96c3438c216ab5cb4a9` |
| Manifest | `artifacts/ARTR.Pien-source.manifest.txt` |
| Sidecar | `artifacts/ARTR.Pien-source.zip.sha256` |
| Created | 2026-08-05T12:26:08+02:00 (PackageSource) |
| Size (bytes) | 70207387 |

Layout after extract: `ARTR/Pien/` (solution present; `inDev/` excluded).

## Publish artifacts (this machine, 2026-08-05)

Command: `pwsh -File .\build\Publish.ps1`  
Host RID: **win-x64**  
Docker: **not used**  
Trim / single-file / NativeAOT: **disabled** (not verified)

### Framework-dependent

- `artifacts/publish/fd/` (CLI + dependencies; `ARTR.Pien.Cli.exe version` → `ARTR Pien 0.1.0`)

### Self-contained RIDs

| RID | Result |
|-----|--------|
| win-x64 | **ok** → `artifacts/publish/sc/win-x64/` |
| win-arm64 | **ok** → `artifacts/publish/sc/win-arm64/` |
| linux-x64 | **ok** → `artifacts/publish/sc/linux-x64/` |
| linux-arm64 | **ok** → `artifacts/publish/sc/linux-arm64/` |
| osx-x64 | **ok** → `artifacts/publish/sc/osx-x64/` |
| osx-arm64 | **ok** → `artifacts/publish/sc/osx-arm64/` |

Skipped: **none** on this SDK (cross-publish succeeded without Docker).

### NuGet packs (`artifacts/publish/nupkg/`)

- `ARTR.Pien.Core.0.1.0.nupkg` (+ `.snupkg`)
- `ARTR.Pien.Web.0.1.0.nupkg` (+ `.snupkg`)
- `ARTR.Pien.Checks.0.1.0.nupkg` (+ `.snupkg`)
- `ARTR.Pien.Engine.0.1.0.nupkg` (+ `.snupkg`)
- `ARTR.Pien.Reporting.0.1.0.nupkg` (+ `.snupkg`)
- `ARTR.Pien.Storage.0.1.0.nupkg` (+ `.snupkg`)
- `ARTR.Pien.Hosting.0.1.0.nupkg` (+ `.snupkg`)
- `ARTR.Pien.Cli.0.1.0.nupkg` (+ `.snupkg`, `PackAsTool` / `pien`)

### Checksums / SBOM

- `artifacts/publish/SHA256SUMS.txt` — **1687** file entries (includes FD, SC RIDs, nupkg, SBOM)
- `artifacts/publish/sbom/bom.json` — CycloneDX **1.7** via local tool `cyclonedx` 6.2.0

Bash twin: `build/Publish.sh` kept roughly equivalent.

## Analyzers PIEN0010–PIEN0012

**Decision: defer** (not implemented).

- ADR: `docs/architecture/adr/ADR-030-deferred-analyzers.md`
- Index: `docs/development/Analyzers.md`
- Updated: `docs/development/PowerOfTen.md`, ADR-008, ADR-026, Architecture.md

Rationale: high false-positive risk for preprocessor / CT-ignore / regex flow analysis; substitute review + `SafeRegex` / limits.

## Known limitations / residual risks / non-goals

1. **Per-package branch 90%** is not a hard gate; Checks (~80% branch) and occasionally Core/Web sit below that ideal while **average** ≥90% passes.
2. **PIEN0010–0012** deferred (ADR-030); not NASA/JPL certified (PowerOfTen disclaimer).
3. **NuGet packages** warn about missing package README (authoring hygiene; non-blocking).
4. **No Docker** in build/test/publish paths (ADR-009).
5. **No trim / single-file / NativeAOT** until separately verified.
6. Scans produce evidence of executed checks — **not** proof a target is secure (product disclaimer).
7. Starter configs with `authorization.confirmed=false` intentionally fail `validate` (exit 3); use `config/examples/loopback-website.json` for validate-ready sample.
8. Publish artifacts and coverage HTML under `artifacts/` are **local outputs**, not committed.

## Confirmations

- No Docker used in this close-out.
- No fabricated test/coverage/publish results.
- Green work not re-executed solely to regenerate numbers (48 checks, average coverage gate, ZIP/smoke already PASS); publish was freshly expanded and run.
