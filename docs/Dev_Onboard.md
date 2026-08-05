# Developer onboarding

1. Install .NET SDK **10.0.302** (`global.json`).
2. `dotnet restore` / `dotnet build` / `dotnet test`.
3. Read `docs/development/PowerOfTen.md` and `docs/development/DependencyResearch.md`.
4. Architecture ADRs live under `docs/architecture/adr/` — start with [Architecture.md](architecture/Architecture.md), [DataFlow.md](architecture/DataFlow.md), [TrustBoundaries.md](architecture/TrustBoundaries.md), [adr/README.md](architecture/adr/README.md).
5. Design contracts for CLI/config/checks/reports: [CliUx.md](cli/CliUx.md), [CheckCatalog.md](checks/CheckCatalog.md), `config/schemas/`, [HowToUse.md](HowToUse.md).
6. Never commit `inDev/`, secrets, or `bin`/`obj`.
7. Prefer Conventional Commits on `main` for this repository's initial implementation workflow.
8. No Docker / nested `ARTR/Pien` paths.
