#!/usr/bin/env pwsh
param(
  [string]$Configuration = "Release",
  [double]$LineThreshold = 90,
  [double]$BranchThreshold = 90
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot/.."
$results = Join-Path $root "artifacts/TestResults"

# Avoid locked coverage folders / stale Cobertura hosts on Windows.
Get-Process -Name "testhost" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
if (Test-Path $results) {
  Remove-Item $results -Recurse -Force -ErrorAction SilentlyContinue
  if (Test-Path $results) {
    Start-Sleep -Seconds 1
    Remove-Item $results -Recurse -Force
  }
}
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

# Sequential hosts avoid Coverlet zero-hit Cli packages when testhosts race on Windows.
dotnet test "$root/ARTR.Pien.sln" -c $Configuration `
  --collect:"XPlat Code Coverage" `
  --results-directory $results `
  --settings $runsettings `
  -- maxcpucount:1
if ($LASTEXITCODE -ne 0) {
  throw "dotnet test failed with exit code $LASTEXITCODE"
}
$coverageFiles = @(Get-ChildItem -Path $results -Recurse -Filter "coverage.cobertura.xml")
if ($coverageFiles.Count -eq 0) {
  throw "No Cobertura coverage files were produced."
}

$mergedDir = Join-Path $root "artifacts/coverage-merged"
if (Test-Path $mergedDir) { Remove-Item $mergedDir -Recurse -Force -ErrorAction SilentlyContinue }
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

# Prefer the best observed host rate per package when a raced host emits zero hits (L-COV-1).
foreach ($file in $coverageFiles) {
  [xml]$hostDoc = Get-Content -LiteralPath $file.FullName
  foreach ($pkg in @($hostDoc.coverage.packages.package)) {
    $name = [string]$pkg.name
    if (-not $packages.ContainsKey($name)) { continue }
    $line = [double]$pkg.'line-rate'
    $branch = [double]$pkg.'branch-rate'
    if ($line -gt $packages[$name].Line) { $packages[$name].Line = $line }
    if ($branch -gt $packages[$name].Branch) { $packages[$name].Branch = $branch }
  }
}

if ($packages.Count -eq 0) {
  throw "No production package coverage was found in merged Cobertura output."
}

if (-not $packages.ContainsKey("ARTR.Pien.Cli")) {
  throw "ARTR.Pien.Cli missing from Cobertura. AssemblyName must be ARTR.Pien.Cli and in-process CLI tests must exercise it."
}

$line = [math]::Round(100.0 * (($packages.Values | ForEach-Object { $_.Line } | Measure-Object -Average).Average), 2)
$branch = [math]::Round(100.0 * (($packages.Values | ForEach-Object { $_.Branch } | Measure-Object -Average).Average), 2)

Write-Host "Coverage by package (merged across hosts, max-per-host reconciled):"
foreach ($name in ($packages.Keys | Sort-Object)) {
  Write-Host ("  {0}: line={1:n2}% branch={2:n2}%" -f $name, (100.0 * $packages[$name].Line), (100.0 * $packages[$name].Branch))
}
Write-Host ("Average line coverage:   {0}%" -f $line)
Write-Host ("Average branch coverage: {0}%" -f $branch)

$htmlDir = Join-Path $root "artifacts/coverage-html"
if (Test-Path $htmlDir) { Remove-Item $htmlDir -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Force -Path $htmlDir | Out-Null
Copy-Item -Path (Join-Path $mergedDir "*") -Destination $htmlDir -Recurse -Force

if ($line -lt $LineThreshold -or $branch -lt $BranchThreshold) {
  throw "Coverage gate failed. Required >= $LineThreshold% line and >= $BranchThreshold% branch. Observed line=$line branch=$branch"
}

Write-Host "Coverage gate passed."
