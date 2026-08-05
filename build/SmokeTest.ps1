#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"
dotnet run --project "$PSScriptRoot/../src/ARTR.Pien.Cli" -c Release -- version
dotnet run --project "$PSScriptRoot/../src/ARTR.Pien.Cli" -c Release -- doctor
dotnet run --project "$PSScriptRoot/../src/ARTR.Pien.Cli" -c Release -- list-checks
dotnet run --project "$PSScriptRoot/../src/ARTR.Pien.Cli" -c Release -- explain PIEN-HTTP-001
Write-Host "Smoke test completed."
