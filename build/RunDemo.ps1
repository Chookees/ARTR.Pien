#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot/.."
Push-Location $root
try {
  Start-Process -FilePath "dotnet" -ArgumentList @("run","--project","samples/ARTR.Pien.SampleSite","-c","Release","--urls","http://127.0.0.1:5088") -PassThru | Out-Null
  Start-Sleep -Seconds 3
  dotnet run --project src/ARTR.Pien.Cli -c Release -- scan --target http://127.0.0.1:5088/ --format console,json --output artifacts/demo --quiet
}
finally {
  Get-Process -Name "ARTR.Pien.SampleSite","dotnet" -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "*SampleSite*" } | Stop-Process -Force -ErrorAction SilentlyContinue
  Pop-Location
}
