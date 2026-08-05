# ARTR Pien

**PIEN — Proba. Inspice. Examina. Nuntia.**  
**Test. Inspect. Examine. Report.**

Long before software systems existed, Roman engineers understood that critical infrastructure could not rely on hope alone. Aqueducts were surveyed, inspected, maintained, and built with durability in mind. Parts of those systems still stand today because reliability was treated as an engineering discipline rather than an assumption.

Pien applies the same principle to modern websites and APIs: test what can fail, inspect what was returned, examine the evidence, and report what requires attention.

> This is an engineering metaphor. It does **not** claim that ancient Rome practiced modern cybersecurity. Surviving Roman structures symbolize inspection, maintenance, durability, and disciplined engineering. The Latin phrase is the source of the **PIEN** acronym and the conceptual pipeline (Test → Inspect → Examine → Report).

## What it is

ARTR Pien (`pien`) is a local-first, deterministic verification CLI for **authorized** HTTP/HTTPS websites and APIs. It produces actionable findings and multi-format reports without requiring Docker, databases, Redis, or cloud services.

## Quick start

```powershell
dotnet build ARTR.Pien.sln -c Release
dotnet run --project src/ARTR.Pien.Cli -c Release -- --help
```

Configuration lives in `pien.json` (schema under `config/`). Local state is stored under `.pien/`.

## License

Apache-2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE).

## Further reading

- [CONTRIBUTING.md](CONTRIBUTING.md)
- [SECURITY.md](SECURITY.md)
- [docs/HowToUse.md](docs/HowToUse.md)
- [docs/product/ReadmeCatcher.md](docs/product/ReadmeCatcher.md) (catcher + README outline)
- [docs/cli/CliUx.md](docs/cli/CliUx.md)
- [docs/architecture/Architecture.md](docs/architecture/Architecture.md)
