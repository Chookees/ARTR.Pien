#!/usr/bin/env pwsh
param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
dotnet test "$PSScriptRoot/../ARTR.Pien.sln" -c $Configuration --collect:"XPlat Code Coverage"
