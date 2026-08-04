#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot/.."
$outDir = Join-Path $root "artifacts"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$zip = Join-Path $outDir "ARTR.Pien-source.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
$stage = Join-Path $outDir "source-stage/ARTR/Pien"
Remove-Item (Join-Path $outDir "source-stage") -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $stage | Out-Null
$exclude = @('.git','artifacts','bin','obj','inDev','.pien','TestResults')
Get-ChildItem $root -Force | Where-Object { $exclude -notcontains $_.Name } | ForEach-Object {
  Copy-Item $_.FullName -Destination (Join-Path $stage $_.Name) -Recurse -Force
}
Compress-Archive -Path (Join-Path $outDir "source-stage/ARTR") -DestinationPath $zip -Force
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path (Join-Path $outDir "ARTR.Pien-source.zip.sha256") -Value "$hash  ARTR.Pien-source.zip"
@(
  "name=ARTR.Pien-source.zip"
  "sha256=$hash"
  "created=$(Get-Date -Format o)"
) | Set-Content (Join-Path $outDir "ARTR.Pien-source.manifest.txt")
Write-Host "Created $zip"
Write-Host "SHA256 $hash"
