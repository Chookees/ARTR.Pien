# AI onboarding

ARTR Pien is a local-first HTTP/HTTPS verification CLI (`pien`) with a four-stage pipeline: Test → Inspect → Examine → Report.

Constraints:

- .NET 10 / net10.0 / CPM / locked restore / TreatWarningsAsErrors
- No Docker/Testcontainers/Redis/DB
- Tests use loopback only
- No TODO/FIXME/NotImplementedException stubs
- Coverage gate ≥90% on production assemblies (Phase 15)

Key projects: Core, Web, Checks, Engine, Reporting, Storage, Hosting, Cli, CodeAnalysis.
