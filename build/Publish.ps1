#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Release packaging for ARTR Pien (no Docker, no trim/single-file/NativeAOT).

.DESCRIPTION
  1. Framework-dependent CLI → artifacts/publish/fd/
  2. Self-contained CLI for supported RIDs → artifacts/publish/sc/<rid>/
  3. NuGet packs (packable libraries + Cli tool) → artifacts/publish/nupkg/
  4. SHA-256 checksums for published files
  5. SBOM via CycloneDX local tool when available (honest skip otherwise)
#>
param(
  [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishRoot = Join-Path $root "artifacts/publish"
$fdOut = Join-Path $publishRoot "fd"
$scRoot = Join-Path $publishRoot "sc"
$nupkgOut = Join-Path $publishRoot "nupkg"
$sbomOut = Join-Path $publishRoot "sbom"
$cliProject = Join-Path $root "src/ARTR.Pien.Cli/ARTR.Pien.Cli.csproj"
$sln = Join-Path $root "ARTR.Pien.sln"

function Write-Step([string]$Message) {
  Write-Host ""
  Write-Host "==> $Message" -ForegroundColor Cyan
}

function Get-FileSha256([string]$Path) {
  return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Write-Checksums([string]$RootDir, [string]$OutFile) {
  $files = @(Get-ChildItem -LiteralPath $RootDir -Recurse -File |
    Where-Object { $_.Name -ne "SHA256SUMS.txt" })
  $lines = foreach ($f in ($files | Sort-Object FullName)) {
    $rel = $f.FullName.Substring($RootDir.Length).TrimStart('\', '/') -replace '\\', '/'
    "$(Get-FileSha256 $f.FullName)  $rel"
  }
  Set-Content -LiteralPath $OutFile -Value $lines -Encoding utf8
  Write-Host "Wrote checksums: $OutFile ($($lines.Count) files)"
}

function Invoke-SelfContainedPublish([string]$Rid, [string]$OutDir) {
  if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

  $prevEap = $ErrorActionPreference
  $ErrorActionPreference = "Continue"
  $output = & dotnet publish $cliProject `
    -c $Configuration `
    -r $Rid `
    --self-contained true `
    -o $OutDir `
    -p:PublishReadyToRun=false `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:PublishAot=false 2>&1
  $code = $LASTEXITCODE
  $ErrorActionPreference = $prevEap

  if ($code -ne 0) {
    Remove-Item $OutDir -Recurse -Force -ErrorAction SilentlyContinue
    $detail = (($output | Out-String).Trim() -replace '\s+', ' ')
    if ($detail.Length -gt 300) { $detail = $detail.Substring(0, 300) + "..." }
    return @{ Ok = $false; Detail = $detail }
  }
  return @{ Ok = $true; Detail = "ok" }
}

# Fresh publish tree (keep sibling artifacts like source zip / coverage).
if (Test-Path $publishRoot) {
  Remove-Item $publishRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $fdOut, $scRoot, $nupkgOut, $sbomOut | Out-Null

Write-Step "Restore solution"
Push-Location $root
try {
  dotnet tool restore
  if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed ($LASTEXITCODE)" }
  dotnet restore $sln
  if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed ($LASTEXITCODE)" }
}
finally {
  Pop-Location
}

# --- 1) Framework-dependent CLI ---
Write-Step "Framework-dependent publish → artifacts/publish/fd/"
dotnet publish $cliProject `
  -c $Configuration `
  -o $fdOut `
  --self-contained false `
  -p:PublishReadyToRun=false `
  -p:PublishSingleFile=false `
  -p:PublishTrimmed=false `
  -p:PublishAot=false
if ($LASTEXITCODE -ne 0) { throw "Framework-dependent publish failed ($LASTEXITCODE)" }
Write-Host "FD publish OK: $fdOut"

# --- 2) Self-contained RIDs ---
$hostRid = [string]([System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier)
if ([string]::IsNullOrWhiteSpace($hostRid)) { $hostRid = "win-x64" }

$candidateRids = [System.Collections.Generic.List[string]]::new()
# Always try win-x64 first on this Windows host; then other RIDs as cross-publish.
foreach ($rid in @("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64")) {
  if (-not $candidateRids.Contains($rid)) { [void]$candidateRids.Add($rid) }
}
if (-not $candidateRids.Contains($hostRid)) {
  $candidateRids.Insert(0, $hostRid)
}

$ridResults = [ordered]@{}
Write-Step "Self-contained publishes (no trim / single-file / NativeAOT)"
foreach ($rid in $candidateRids) {
  $ridOut = Join-Path $scRoot $rid
  Write-Host "--- RID $rid ---"
  $result = Invoke-SelfContainedPublish -Rid $rid -OutDir $ridOut
  if ($result.Ok) {
    Write-Host "OK   $rid → $ridOut"
    $ridResults[$rid] = "ok"
  }
  else {
    Write-Host "SKIP $rid — SDK/runtime pack unavailable or publish unsupported without Docker."
    if ($result.Detail) { Write-Host "  detail: $($result.Detail)" }
    $ridResults[$rid] = "skipped"
  }
}

# --- 3) NuGet packs ---
Write-Step "dotnet pack (packable libraries + Cli tool package)"
$packable = @(
  "src/ARTR.Pien.Core/ARTR.Pien.Core.csproj",
  "src/ARTR.Pien.Web/ARTR.Pien.Web.csproj",
  "src/ARTR.Pien.Checks/ARTR.Pien.Checks.csproj",
  "src/ARTR.Pien.Engine/ARTR.Pien.Engine.csproj",
  "src/ARTR.Pien.Reporting/ARTR.Pien.Reporting.csproj",
  "src/ARTR.Pien.Storage/ARTR.Pien.Storage.csproj",
  "src/ARTR.Pien.Hosting/ARTR.Pien.Hosting.csproj",
  "src/ARTR.Pien.Cli/ARTR.Pien.Cli.csproj"
)
foreach ($rel in $packable) {
  $proj = Join-Path $root $rel
  Write-Host "Pack $rel"
  dotnet pack $proj -c $Configuration -o $nupkgOut --no-restore
  if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed for $rel ($LASTEXITCODE)" }
}
Write-Host "NuGet packages → $nupkgOut"
Get-ChildItem $nupkgOut -File | ForEach-Object { Write-Host "  $($_.Name)" }

# --- 4) Checksums ---
Write-Step "SHA-256 checksums"
$checksumFile = Join-Path $publishRoot "SHA256SUMS.txt"
Write-Checksums -RootDir $publishRoot -OutFile $checksumFile

# --- 5) SBOM (CycloneDX local tool) ---
Write-Step "SBOM (CycloneDX)"
$sbomGenerated = $false
Push-Location $root
try {
  $toolList = & dotnet tool list --local 2>&1 | Out-String
  if ($toolList -match "(?i)CycloneDX") {
    $sbomFile = Join-Path $sbomOut "bom.json"
    Write-Host "Generating CycloneDX SBOM..."
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    & dotnet tool run dotnet-CycloneDX -- $sln -o $sbomOut -fn bom.json -F Json -t -ed 2>&1 | Out-Host
    $sbomCode = $LASTEXITCODE
    if ($sbomCode -ne 0) {
      Write-Host "Solution SBOM failed; retrying against Cli project..."
      & dotnet tool run dotnet-CycloneDX -- $cliProject -o $sbomOut -fn bom.json -F Json -rs -ed 2>&1 | Out-Host
      $sbomCode = $LASTEXITCODE
    }
    $ErrorActionPreference = $prevEap
    # CycloneDX may emit "bom" or "bom.json" depending on -fn / format handling.
    $legacyBom = Join-Path $sbomOut "bom"
    if ((Test-Path $legacyBom) -and -not (Test-Path $sbomFile)) {
      Move-Item -LiteralPath $legacyBom -Destination $sbomFile -Force
    }
    if ($sbomCode -eq 0 -and (Test-Path $sbomFile)) {
      $sbomGenerated = $true
      Write-Host "SBOM OK: $sbomFile"
      Write-Checksums -RootDir $publishRoot -OutFile $checksumFile
    }
    else {
      Write-Host "SKIP SBOM — CycloneDX tool present but generation failed (exit $sbomCode)."
    }
  }
  else {
    Write-Host "SKIP SBOM — CycloneDX not listed in local tool manifest after restore."
    Write-Host "  Expected package id 'CycloneDX' in .config/dotnet-tools.json."
  }
}
finally {
  Pop-Location
}

# --- Summary ---
Write-Step "Publish summary"
Write-Host "Host RID: $hostRid"
Write-Host "Framework-dependent: $fdOut"
Write-Host "Self-contained results:"
foreach ($key in $ridResults.Keys) {
  Write-Host ("  {0,-12} {1}" -f $key, $ridResults[$key])
}
Write-Host "NuGet: $nupkgOut"
Write-Host "Checksums: $checksumFile"
if ($sbomGenerated) {
  Write-Host "SBOM: $(Join-Path $sbomOut 'bom.json')"
}
else {
  Write-Host "SBOM: skipped"
}
Write-Host "Trim/single-file/NativeAOT: disabled (not verified for this release)"
Write-Host "Docker: not used"
Write-Host "Publish completed."
