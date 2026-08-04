#!/usr/bin/env pwsh
param([string]$Configuration = "Release", [string]$Runtime = "")
$ErrorActionPreference = "Stop"
$out = Join-Path $PSScriptRoot "../artifacts/publish"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$args = @("publish","$PSScriptRoot/../src/ARTR.Pien.Cli/ARTR.Pien.Cli.csproj","-c",$Configuration,"-o",$out)
if ($Runtime) { $args += @("-r",$Runtime,"--self-contained","false") }
dotnet @args
