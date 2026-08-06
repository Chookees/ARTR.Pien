# ARTR Pien

[![CI](https://github.com/Chookees/ARTR.Pien/actions/workflows/ci.yml/badge.svg)](https://github.com/Chookees/ARTR.Pien/actions/workflows/ci.yml)
[![CodeQL](https://github.com/Chookees/ARTR.Pien/actions/workflows/codeql.yml/badge.svg)](https://github.com/Chookees/ARTR.Pien/actions/workflows/codeql.yml)
[![Coverage](https://img.shields.io/badge/coverage-%E2%89%A590%25_line%2Fbranch-brightgreen)](docs/architecture/adr/ADR-029-coverage.md)

**PIEN — Proba. Inspice. Examina. Nuntia.**  
**Test. Inspect. Examine. Report.**

Long before software systems existed, Roman engineers understood that critical infrastructure could not rely on hope alone. Aqueducts were surveyed, inspected, maintained, and built with durability in mind. Parts of those systems still stand today because reliability was treated as an engineering discipline rather than an assumption.

Pien applies the same principle to modern websites and APIs: test what can fail, inspect what was returned, examine the evidence, and report what requires attention.

> This is an engineering metaphor. It does **not** claim that ancient Rome practiced modern cybersecurity. Surviving Roman structures symbolize inspection, maintenance, durability, and disciplined engineering. The Latin phrase is the source of the **PIEN** acronym and the conceptual pipeline (Test → Inspect → Examine → Report).

## What it is

ARTR Pien (`pien`) is a local-first, deterministic verification CLI for **authorized** HTTP/HTTPS websites and APIs. It produces actionable findings and multi-format reports without requiring Docker, databases, Redis, or cloud services.

## Disclaimer

Pien findings are engineering observations for operators who confirmed authorization to probe a target. Reports are **not** certifications, compliance attestations, penetration-test results, or guarantees of security.

## Install / build

```powershell
dotnet tool restore
dotnet restore ARTR.Pien.sln --locked-mode
dotnet build ARTR.Pien.sln -c Release --no-restore
dotnet run --project src/ARTR.Pien.Cli -c Release -- version
```

## Quick start

```powershell
dotnet run --project src/ARTR.Pien.Cli -c Release -- init --website
dotnet run --project src/ARTR.Pien.Cli -c Release -- validate
dotnet run --project src/ARTR.Pien.Cli -c Release -- scan --quiet
```

Or validate a shipped loopback sample (exit 0 without editing placeholders):

```powershell
dotnet run --project src/ARTR.Pien.Cli -c Release -- validate --config config/examples/loopback-website.json
```

Placeholder examples under `config/examples/quick-website.json` and `complete-website.json` keep `authorization.confirmed=false` on purpose — `validate`/`scan` require `confirmed=true`. See [docs/HowToUse.md](docs/HowToUse.md).

Configuration: `pien.json` (schema under `config/schemas/`). State: `.pien/`.

## Security checklist (operators)

- Set `authorization.confirmed=true` only for targets you own/are allowed to probe
- Prefer allowlisted hosts; enable `allowPrivateNetworks` only for intentional loopback/lab use
- Never put raw secrets in `pien.json` — use `secret://env/...` or `secret://file/...`
- Use `--confirm-authorization` when overriding with `--target`

## Coverage / quality commands

```powershell
dotnet format ARTR.Pien.sln --verify-no-changes
dotnet test ARTR.Pien.sln -c Release
pwsh -File build/Test.ps1          # Cobertura + ≥90% line/branch gate
pwsh -File build/SmokeTest.ps1
pwsh -File build/PackageSource.ps1 # artifacts/ARTR.Pien-source.zip + sha256 + manifest
```

## Non-goals (v1)

No Docker/K8s, no DB/Redis, no browser automation, no exploit/fuzz engine, no hosted control plane, no compliance certification claims.

## License

Apache-2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE).

## Further reading

- [docs/HowToUse.md](docs/HowToUse.md)
- [docs/AI_Onboard.md](docs/AI_Onboard.md)
- [docs/Dev_Onboard.md](docs/Dev_Onboard.md)
- [docs/development/CodeTour.md](docs/development/CodeTour.md)
- [docs/checks/CheckCatalog.md](docs/checks/CheckCatalog.md)
- [docs/cli/CliUx.md](docs/cli/CliUx.md)
- [docs/architecture/Overview.md](docs/architecture/Overview.md)
- [docs/operations/Runbooks.md](docs/operations/Runbooks.md)
- [docs/compliance/NIST-SSDF-OWASP.md](docs/compliance/NIST-SSDF-OWASP.md)
- [SECURITY.md](SECURITY.md) / [CONTRIBUTING.md](CONTRIBUTING.md)
