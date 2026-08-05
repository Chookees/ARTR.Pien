#!/usr/bin/env pwsh
param(
  [string]$Configuration = "Release",
  [double]$LineThreshold = 90,
  [double]$BranchThreshold = 90
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot/.."
$results = Join-Path $root "artifacts/TestResults"
if (Test-Path $results) { Remove-Item $results -Recurse -Force }
New-Item -ItemType Directory -Force -Path $results | Out-Null

$runsettings = Join-Path $results "coverlet.runsettings"
@"
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Format>cobertura</Format>
          <Include>[ARTR.Pien.Core]*,[ARTR.Pien.Web]*,[ARTR.Pien.Checks]*,[ARTR.Pien.Engine]*,[ARTR.Pien.Reporting]*,[ARTR.Pien.Storage]*,[ARTR.Pien.Hosting]*,[ARTR.Pien.Cli]*</Include>
          <Exclude>[ARTR.Pien.CodeAnalysis]*</Exclude>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
"@ | Set-Content -Path $runsettings -Encoding utf8

dotnet test "$root/ARTR.Pien.sln" -c $Configuration `
  --collect:"XPlat Code Coverage" `
  --results-directory $results `
  --settings $runsettings

$coverageFiles = @(Get-ChildItem -Path $results -Recurse -Filter "coverage.cobertura.xml")
if ($coverageFiles.Count -eq 0) {
  throw "No Cobertura coverage files were produced."
}

$allowed = @(
  "ARTR.Pien.Core", "ARTR.Pien.Web", "ARTR.Pien.Checks", "ARTR.Pien.Engine",
  "ARTR.Pien.Reporting", "ARTR.Pien.Storage", "ARTR.Pien.Hosting", "ARTR.Pien.Cli"
)

# Keep best observed rates per production package across test hosts.
$best = @{}
foreach ($file in $coverageFiles) {
  [xml]$doc = Get-Content -LiteralPath $file.FullName
  foreach ($pkg in @($doc.coverage.packages.package)) {
    $name = [string]$pkg.name
    if ($allowed -notcontains $name) { continue }
    $lineRate = [double]$pkg.'line-rate'
    $branchRate = [double]$pkg.'branch-rate'
    if (-not $best.ContainsKey($name)) {
      $best[$name] = @{ Line = $lineRate; Branch = $branchRate }
    } else {
      if ($lineRate -gt $best[$name].Line) { $best[$name].Line = $lineRate }
      if ($branchRate -gt $best[$name].Branch) { $best[$name].Branch = $branchRate }
    }
  }
}

if ($best.Count -eq 0) {
  throw "No production package coverage was found in Cobertura output."
}

$line = [math]::Round(100.0 * (($best.Values | ForEach-Object { $_.Line } | Measure-Object -Average).Average), 2)
$branch = [math]::Round(100.0 * (($best.Values | ForEach-Object { $_.Branch } | Measure-Object -Average).Average), 2)

Write-Host "Coverage by package (best across hosts):"
foreach ($name in ($best.Keys | Sort-Object)) {
  Write-Host ("  {0}: line={1:n2}% branch={2:n2}%" -f $name, (100.0 * $best[$name].Line), (100.0 * $best[$name].Branch))
}
Write-Host ("Average line coverage:   {0}%" -f $line)
Write-Host ("Average branch coverage: {0}%" -f $branch)

$htmlDir = Join-Path $root "artifacts/coverage-html"
New-Item -ItemType Directory -Force -Path $htmlDir | Out-Null
@"
<html><body><h1>ARTR Pien coverage</h1>
<p>Average line: $line%</p><p>Average branch: $branch%</p>
</body></html>
"@ | Set-Content (Join-Path $htmlDir "index.html")

if ($line -lt $LineThreshold -or $branch -lt $BranchThreshold) {
  throw "Coverage gate failed. Required >= $LineThreshold% line and >= $BranchThreshold% branch. Observed line=$line branch=$branch"
}

Write-Host "Coverage gate passed."
