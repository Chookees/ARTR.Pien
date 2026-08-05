# Dependency research

**Research date:** 2026-08-05  
**Re-verified:** 2026-08-05 via `https://api.nuget.org/v3-flatcontainer/{id}/index.json` and package pages  
**SDK pin:** `global.json` → `10.0.302`  
**Policy:** Prefer BCL / `Microsoft.Extensions.*`; Central Package Management; locked restore; NuGet audit; Apache-2.0-compatible redistribution.

## Verification notes (architect pass)

| Package | Locked in repo | Latest stable on NuGet (2026-08-05) | Notes |
|---------|----------------|--------------------------------------|-------|
| System.CommandLine | 2.0.10 | **2.0.10** (3.x preview-only) | MIT; keep on 2.x until 3.0 stable + ADR |
| AngleSharp | 1.7.0 | **1.7.0** | MIT; 1.8.0 still beta |
| NJsonSchema | 11.6.1 | **11.6.1** | MIT |
| Microsoft.OpenApi | 3.9.0 | **3.9.0** | MIT |
| Microsoft.OpenApi.YamlReader | 3.9.0 | **3.9.0** | MIT |
| Microsoft.Extensions.Hosting (and siblings) | 10.0.10 | **10.0.10** | MIT; net10 line |
| Microsoft.SourceLink.GitHub | 10.0.301 | aligned with SDK line | MIT |

**JsonSchema.Net rejection (reconfirmed):** Upstream Open Source Maintenance Fee / binary EULA for revenue-generating NuGet binary use (effective 2026-02-01; see json-everything OSMF EULA). Source remains MIT if self-compiled; Pien rejects NuGet consumption to avoid redistribution/compliance ambiguity for future commercial ARTR products.

**No vulnerability audit numbers are fabricated here.** CI enables `NuGetAudit=true` with `NuGetAuditLevel=low`; treat audit failures in CI as authoritative at build time.

## Direct production dependencies

| Package | Locked version | License | Consuming project | Purpose | Alternatives considered |
|---------|----------------|---------|-------------------|---------|-------------------------|
| System.CommandLine | **2.0.10** | MIT | Cli | Parsing, help, invocations | Spectre.Console.Cli; manual argv |
| AngleSharp | **1.7.0** | MIT | Web | HTML DOM without script/network | HtmlAgilityPack 1.12.x; custom regex (rejected) |
| NJsonSchema | **11.6.1** | MIT | Checks | JSON Schema validation (local `$ref` only) | JsonSchema.Net 9.x (**rejected** — binary EULA/maintenance fee for revenue use) |
| Microsoft.OpenApi | **3.9.0** | MIT | Checks | OpenAPI object model | NSwag (heavier) |
| Microsoft.OpenApi.YamlReader | **3.9.0** | MIT | Checks | YAML OpenAPI documents | YamlDotNet direct |
| Microsoft.Extensions.Hosting | **10.0.10** | MIT | Hosting | Generic host composition | custom host |
| Microsoft.Extensions.Logging.Console | **10.0.10** | MIT | Hosting | Console logging | Serilog (avoid extra surface) |
| Microsoft.Extensions.Options.DataAnnotations | **10.0.10** | MIT | Hosting | Options validation | FluentValidation (avoid) |
| Microsoft.Extensions.Http | **10.0.10** | MIT | Hosting/Web as needed | Named handlers only behind safe transport | raw HttpClient sprawl |
| Microsoft.Extensions.Configuration.Json | **10.0.10** | MIT | Hosting | `pien.json` loading | custom parser |
| Microsoft.Extensions.Configuration.EnvironmentVariables | **10.0.10** | MIT | Hosting | `ARTR_PIEN_*` overrides | custom |
| Microsoft.Extensions.Configuration.CommandLine | **10.0.10** | MIT | Hosting | Flag overrides | System.CommandLine-only merge |
| Microsoft.Extensions.DependencyInjection | **10.0.10** | MIT | Hosting | Explicit registration | MediatR (rejected) |
| Microsoft.SourceLink.GitHub | **10.0.301** | MIT | build (src) | Source Link | none |

Framework-provided (no package reference): `System.Text.Json`, `HttpClient`/`SocketsHttpHandler`, `SslStream`, `System.Threading.Channels`, `TimeProvider`, `ActivitySource`/`Meter`, secure XML readers.

## Explicitly rejected

| Package | Version seen | Reason |
|---------|--------------|--------|
| JsonSchema.Net | 9.x+ | Upstream NuGet binary EULA / OSMF maintenance-fee obligation for revenue-generating use |
| JsonPointer.Net | same family | Same vendor EULA risk; implement RFC 6901 on `JsonNode` if needed |
| Sarif.Sdk | 5.x | Newtonsoft + oversized OM for write-only SARIF 2.1.0 |
| Testcontainers.* | any | Containers forbidden |
| StackExchange.Redis / DB drivers | any | Forbidden |
| Playwright / Puppeteer | any | Browser automation non-goal |
| MediatR / AutoMapper / Polly (generic) | any | Unnecessary for v1 |

## Test and tool dependencies

| Package | Locked version | License | Purpose |
|---------|----------------|---------|---------|
| xunit.v3 | **3.2.2** | Apache-2.0 | Unit/integration/functional/architecture tests |
| xunit.runner.visualstudio | **3.1.5** | Apache-2.0 | VSTest adapter |
| Microsoft.NET.Test.Sdk | **18.8.1** | MIT | Test SDK |
| coverlet.collector | **10.0.1** | MIT | Coverage collector |
| coverlet.msbuild | **10.0.1** | MIT | Coverage threshold gates |
| ReportGenerator (tool) | **5.5.11** | Apache-2.0 | Coverage HTML reports |
| Microsoft.AspNetCore.Mvc.Testing | **10.0.10** | MIT | `WebApplicationFactory` / loopback |
| Microsoft.Extensions.Diagnostics.Testing | **10.8.0** | MIT | Logger test helpers |
| NetArchTest.Rules | **1.3.2** | MIT | Assembly dependency rules |
| BenchmarkDotNet | **0.15.8** | MIT | Microbenchmarks |
| Microsoft.CodeAnalysis.CSharp | **5.6.0** | MIT | Analyzer project |
| Microsoft.CodeAnalysis.CSharp.Workspaces | **5.6.0** | MIT | Analyzer tests |
| Microsoft.CodeAnalysis.Analyzers | **5.6.0** | Apache-2.0/MIT | Analyzer SDK analyzers |
| Microsoft.CodeAnalysis.CSharp.Analyzer.Testing | **1.1.4** | MIT | Analyzer unit tests |

## Transitive watchlist

- **NJsonSchema** → `Newtonsoft.Json`, `Namotion.Reflection` — listed in NOTICE; keep Newtonsoft out of Core/Reporting hot paths.
- **AngleSharp** — configure with no script execution and no automatic requests.
- **Microsoft.OpenApi.YamlReader** — review YamlDotNet transitive license (MIT historically).

## Lockfile process

1. Versions pinned in `Directory.Packages.props`.
2. `RestorePackagesWithLockFile=true` in `Directory.Build.props`.
3. `dotnet restore` generates per-project `packages.lock.json`.
4. CI uses `RestoreLockedMode=true` when `CI`/`GITHUB_ACTIONS` is set.
5. Re-run this research when upgrading any direct dependency.
