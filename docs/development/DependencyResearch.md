# Dependency research

**Research date:** 2026-08-05  
**Re-verified at lock time:** 2026-08-05 against `https://api.nuget.org/v3-flatcontainer/{id}/index.json`  
**SDK pin:** `global.json` → `10.0.302`  
**Policy:** Prefer BCL / `Microsoft.Extensions.*`; Central Package Management; locked restore; NuGet audit; Apache-2.0-compatible redistribution.

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
| JsonSchema.Net | 9.4.0 | Upstream NuGet binary EULA / maintenance-fee obligation for revenue-generating use |
| JsonPointer.Net | 7.0.2 | Same vendor EULA risk; implement RFC 6901 on `JsonNode` |
| Sarif.Sdk | 5.6.0 | Newtonsoft + oversized OM for write-only SARIF 2.1.0 |
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
