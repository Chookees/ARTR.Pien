# CodeTour — ARTR Pien

Guided reading order for contributors.

1. **Product identity** — `README.md`, `docs/product/ReadmeCatcher.md`
2. **Pipeline** — `docs/architecture/PIEN-Pipeline.md`, `src/ARTR.Pien.Engine/ScanEngine.cs`
3. **Safety** — `docs/security/ThreatModel.md`, `src/ARTR.Pien.Web/Network/DestinationValidator.cs`, `src/ARTR.Pien.Web/Transport/SafeHttpTransport.cs`
4. **Checks** — `docs/checks/CheckCatalog.md`, `src/ARTR.Pien.Checks/Website/WebsiteChecks.cs`, `src/ARTR.Pien.Checks/Api/ApiChecks.cs`
5. **Config** — `config/schemas/pien.schema.json`, `src/ARTR.Pien.Core/Configuration/PienConfigurationValidator.cs`
6. **CLI** — `docs/cli/CliUx.md`, `src/ARTR.Pien.Cli/Program.cs`, `src/ARTR.Pien.Cli/ScanCommandHandler.cs`
7. **Reports** — `docs/reporting/ReportFormats.md`, `src/ARTR.Pien.Reporting/Exporters/ReportExporters.cs`
8. **Storage / baselines** — `src/ARTR.Pien.Storage/FileScanStore.cs`
9. **Hosting** — `src/ARTR.Pien.Hosting/PienServiceCollectionExtensions.cs`
10. **Build / CI** — `build/Test.ps1`, `.github/workflows/ci.yml`

Stop conditions: do not introduce Docker, remote `$ref` fetch, or auto-execution of destructive OpenAPI operations.
