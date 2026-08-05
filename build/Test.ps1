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
          <SkipAutoProps>true</SkipAutoProps>
          <ExcludeByAttribute>Obsolete,GeneratedCodeAttribute,CompilerGeneratedAttribute,ExcludeFromCodeCoverageAttribute</ExcludeByAttribute>
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

$mergedDir = Join-Path $root "artifacts/coverage-merged"
if (Test-Path $mergedDir) { Remove-Item $mergedDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $mergedDir | Out-Null

$reports = ($coverageFiles | ForEach-Object { $_.FullName }) -join ";"
Push-Location $root
try {
  dotnet tool restore | Out-Null
  dotnet tool run reportgenerator `
    "-reports:$reports" `
    "-targetdir:$mergedDir" `
    "-reporttypes:Cobertura;Html" `
    "-classfilters:-System.*;-Microsoft.*" | Out-Null
}
finally {
  Pop-Location
}

$merged = Join-Path $mergedDir "Cobertura.xml"
if (-not (Test-Path $merged)) {
  throw "Merged Cobertura.xml was not produced by ReportGenerator."
}

$allowed = @(
  "ARTR.Pien.Core", "ARTR.Pien.Web", "ARTR.Pien.Checks", "ARTR.Pien.Engine",
  "ARTR.Pien.Reporting", "ARTR.Pien.Storage", "ARTR.Pien.Hosting", "ARTR.Pien.Cli"
)

[xml]$doc = Get-Content -LiteralPath $merged
$packages = @{}
foreach ($pkg in @($doc.coverage.packages.package)) {
  $name = [string]$pkg.name
  if ($allowed -notcontains $name) { continue }
  $packages[$name] = @{
    Line = [double]$pkg.'line-rate'
    Branch = [double]$pkg.'branch-rate'
  }
}

if ($packages.Count -eq 0) {
  throw "No production package coverage was found in merged Cobertura output."
}

$line = [math]::Round(100.0 * (($packages.Values | ForEach-Object { $_.Line } | Measure-Object -Average).Average), 2)
$branch = [math]::Round(100.0 * (($packages.Values | ForEach-Object { $_.Branch } | Measure-Object -Average).Average), 2)

Write-Host "Coverage by package (merged across hosts):"
foreach ($name in ($packages.Keys | Sort-Object)) {
  Write-Host ("  {0}: line={1:n2}% branch={2:n2}%" -f $name, (100.0 * $packages[$name].Line), (100.0 * $packages[$name].Branch))
}
Write-Host ("Average line coverage:   {0}%" -f $line)
Write-Host ("Average branch coverage: {0}%" -f $branch)

$htmlDir = Join-Path $root "artifacts/coverage-html"
if (Test-Path $htmlDir) { Remove-Item $htmlDir -Recurse -Force }
Copy-Item -Path (Join-Path $mergedDir "*") -Destination $htmlDir -Recurse -Force

if ($line -lt $LineThreshold -or $branch -lt $BranchThreshold) {
  throw "Coverage gate failed. Required >= $LineThreshold% line and >= $BranchThreshold% branch. Observed line=$line branch=$branch"
}

Write-Host "Coverage gate passed."
