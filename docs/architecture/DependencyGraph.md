# Dependency graph

## Project references (acyclic)

```text
Core ◄── Web
Core ◄── Checks ◄── Web          (Checks may use Web helpers; no Engine/Cli)
Core ◄── Engine                  (orchestration only; no Web/Checks project refs)
Core ◄── Reporting
Core ◄── Storage
Core ◄── Hosting ◄── Engine, Web, Checks, Reporting, Storage
Hosting ◄── Cli
Core ◄── CodeAnalysis (analyzer; ReferenceOutputAssembly=false into production projects)
```

Forbidden edges:

- `Core` → any other `ARTR.Pien.*`
- `Engine` → `Web` | `Checks` | `Reporting` | `Storage` | `Cli` | `Hosting`
- `Web` | `Checks` | `Reporting` | `Storage` → `Engine` | `Hosting` | `Cli`
- Any production project → test/sample/benchmark projects
- Cycles of any kind

Composition root is **Hosting** (and Cli for the executable). Engine depends on abstractions in Core (`ISafeHttpTransport`, `ICheck`, `ICrawler`, stores) implemented in sibling projects and wired by Hosting.

## Package dependencies (production)

| Project | Direct NuGet |
|---------|----------------|
| Core | none (BCL only) |
| Web | AngleSharp |
| Checks | NJsonSchema, Microsoft.OpenApi, Microsoft.OpenApi.YamlReader |
| Engine | none |
| Reporting | none |
| Storage | none |
| Hosting | Microsoft.Extensions.* (Hosting, Logging.Console, Options.DataAnnotations, Http, Configuration.*, DI) |
| Cli | System.CommandLine (+ Hosting) |
| CodeAnalysis | Microsoft.CodeAnalysis.CSharp (+ Analyzers) |

## Enforcement

- `tests/ARTR.Pien.ArchitectureTests` asserts assembly references and absence of forbidden packages/Docker.
- CPM + `packages.lock.json` + CI `--locked-mode`.
- NuGet audit enabled in `Directory.Build.props`.

See [DependencyResearch.md](../development/DependencyResearch.md) for version rationale.
