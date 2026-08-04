#!/usr/bin/env pwsh
param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
dotnet restore "$PSScriptRoot/../ARTR.Pien.sln"
dotnet build "$PSScriptRoot/../ARTR.Pien.sln" -c $Configuration --no-restore
