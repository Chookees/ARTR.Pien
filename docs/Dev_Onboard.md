# Developer onboarding

1. Install .NET SDK **10.0.302** (`global.json`).
2. `dotnet restore` / `dotnet build` / `dotnet test`.
3. Read `docs/development/PowerOfTen.md` and `docs/development/DependencyResearch.md`.
4. Architecture ADRs live under `docs/architecture/adr/`.
5. Never commit `inDev/`, secrets, or `bin`/`obj`.
6. Prefer Conventional Commits on `main` for this repository's initial implementation workflow.
