# AI onboarding

ARTR Pien is a local-first HTTP/HTTPS verification CLI (`pien`) with a four-stage pipeline: Test → Inspect → Examine → Report.

Constraints:

- .NET 10 / net10.0 / CPM / locked restore / TreatWarningsAsErrors
- No Docker/Testcontainers/Redis/DB
- Tests use loopback only
- No TODO/FIXME/NotImplementedException stubs
- Coverage gate ≥90% on production assemblies (Phase 15)

Key projects: Core, Web, Checks, Engine, Reporting, Storage, Hosting, Cli, CodeAnalysis.

## Architecture (start here)

- [architecture/Architecture.md](architecture/Architecture.md)
- [architecture/DataFlow.md](architecture/DataFlow.md)
- [architecture/TrustBoundaries.md](architecture/TrustBoundaries.md)
- [architecture/ExtensionModel.md](architecture/ExtensionModel.md)
- [architecture/adr/README.md](architecture/adr/README.md) (ADR-001..027)
- [security/ThreatModel.md](security/ThreatModel.md)

## Design artifacts (UX / config — not engine bulk)

- [cli/CliUx.md](cli/CliUx.md)
- [configuration/ProfilesAndPolicies.md](configuration/ProfilesAndPolicies.md)
- [checks/CheckCatalog.md](checks/CheckCatalog.md)
- [reporting/ReportFormats.md](reporting/ReportFormats.md)
- Schemas: `config/schemas/pien.schema.json`, `pien-report.schema.json`, `pien-baseline.schema.json`
