# Contributing to ARTR Pien

Thanks for contributing. Please read [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) and [SECURITY.md](SECURITY.md) first.

## Development setup

1. Install the .NET SDK pinned in `global.json` (10.0.302).
2. Clone this repository at `ARTR/Pien` (repository root).
3. Restore and build:

```powershell
dotnet restore
dotnet build ARTR.Pien.sln -c Release
dotnet test ARTR.Pien.sln -c Release --no-build
```

Or use `build/Build.ps1` / `build/Test.ps1` when present.

## Pull requests

External contributors should open pull requests against `main` with:

- Conventional Commits (`feat:`, `fix:`, `docs:`, `test:`, `ci:`, `chore:`)
- Tests for behavioral changes
- No secrets, no `inDev/` artifacts, no `bin/`/`obj/`
- No Docker/Testcontainers/Redis/database dependencies

## Coding standards

- `TreatWarningsAsErrors` is enabled
- Nullable reference types are required
- Follow `docs/development/PowerOfTen.md` when available
- Do not add `TODO`/`FIXME`/`NotImplementedException` stubs

## License

By contributing, you agree that your contributions are licensed under Apache-2.0.
