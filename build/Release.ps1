#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Cut a release from the current tree (version bump → commit → tag → optional push).

.DESCRIPTION
  Reads the current Version from Directory.Build.props, bumps it (or uses -Version),
  updates CHANGELOG.md, commits, creates annotated tag vX.Y.Z, and optionally pushes
  to origin. Pushing the tag triggers .github/workflows/release.yml.

.EXAMPLE
  pwsh -File build/Release.ps1 -Bump patch -Push
.EXAMPLE
  pwsh -File build/Release.ps1 -Version 0.2.0 -SkipTests -Push
.EXAMPLE
  pwsh -File build/Release.ps1 -Bump minor -DryRun
#>
param(
  [string]$Version,
  [ValidateSet("patch", "minor", "major")]
  [string]$Bump = "patch",
  [string]$Message,
  [switch]$SkipTests,
  [switch]$SkipCommit,
  [switch]$Push,
  [switch]$DryRun,
  [switch]$AllowDirty,
  [switch]$AllowBranch,
  [string]$Remote = "origin",
  [string]$Branch = "main"
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $root

function Write-Step([string]$Message) {
  Write-Host ""
  Write-Host "==> $Message" -ForegroundColor Cyan
}

function Get-PropsVersion([string]$PropsPath) {
  $raw = Get-Content -LiteralPath $PropsPath -Raw
  if ($raw -notmatch '<Version>([^<]+)</Version>') {
    throw "Could not read <Version> from $PropsPath"
  }
  return $Matches[1].Trim()
}

function Set-PropsVersion([string]$PropsPath, [string]$NewVersion) {
  $raw = Get-Content -LiteralPath $PropsPath -Raw
  $assembly = "$NewVersion.0"
  $raw = [regex]::Replace($raw, '<Version>[^<]+</Version>', "<Version>$NewVersion</Version>")
  $raw = [regex]::Replace($raw, '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$assembly</AssemblyVersion>")
  $raw = [regex]::Replace($raw, '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$assembly</FileVersion>")
  $raw = [regex]::Replace($raw, '<InformationalVersion>[^<]+</InformationalVersion>', "<InformationalVersion>$NewVersion</InformationalVersion>")
  if (-not $raw.EndsWith("`n")) { $raw += "`n" }
  $utf8NoBom = New-Object System.Text.UTF8Encoding $false
  [System.IO.File]::WriteAllText($PropsPath, $raw, $utf8NoBom)
}

function Get-NextVersion([string]$Current, [string]$BumpKind) {
  $parts = $Current.Split('.')
  if ($parts.Length -lt 3) {
    throw "Version '$Current' must be MAJOR.MINOR.PATCH"
  }
  $major = [int]$parts[0]
  $minor = [int]$parts[1]
  $patch = [int]$parts[2]
  switch ($BumpKind) {
    "major" { return "{0}.0.0" -f ($major + 1) }
    "minor" { return "{0}.{1}.0" -f $major, ($minor + 1) }
    "patch" { return "{0}.{1}.{2}" -f $major, $minor, ($patch + 1) }
  }
  throw "Unknown bump kind: $BumpKind"
}

function Update-Changelog([string]$ChangelogPath, [string]$NewVersion, [string]$Date) {
  if (-not (Test-Path -LiteralPath $ChangelogPath)) {
    Write-Host "CHANGELOG.md missing — skipping changelog update." -ForegroundColor Yellow
    return
  }

  $text = Get-Content -LiteralPath $ChangelogPath -Raw
  if ($text -notmatch '(?m)^## \[Unreleased\]\s*$') {
    throw "CHANGELOG.md has no '## [Unreleased]' section."
  }
  if ($text -match "(?m)^## \[$([regex]::Escape($NewVersion))\]") {
    throw "CHANGELOG.md already contains section [$NewVersion]."
  }

  $replacement = @"
## [Unreleased]

### Added

### Changed

### Fixed

## [$NewVersion] - $Date
"@
  $updated = [regex]::Replace($text, '(?m)^## \[Unreleased\]\s*', ($replacement + "`n"), 1)
  if (-not $updated.EndsWith("`n")) { $updated += "`n" }
  $utf8NoBom = New-Object System.Text.UTF8Encoding $false
  [System.IO.File]::WriteAllText($ChangelogPath, $updated.TrimEnd() + "`n", $utf8NoBom)
}

function Invoke-Git([string[]]$GitArgs) {
  & git @GitArgs
  if ($LASTEXITCODE -ne 0) {
    throw "git $($GitArgs -join ' ') failed with exit code $LASTEXITCODE"
  }
}

# --- preflight ----------------------------------------------------------------

$props = Join-Path $root "Directory.Build.props"
$changelog = Join-Path $root "CHANGELOG.md"
$current = Get-PropsVersion $props

if ([string]::IsNullOrWhiteSpace($Version)) {
  $Version = Get-NextVersion $current $Bump
}
elseif ($Version -notmatch '^\d+\.\d+\.\d+$') {
  throw "Version must be MAJOR.MINOR.PATCH (got '$Version')."
}

$tag = "v$Version"
$date = (Get-Date).ToString("yyyy-MM-dd")
$tagMessage = if ([string]::IsNullOrWhiteSpace($Message)) {
  "Release $tag"
} else {
  $Message
}

Write-Step "Release plan"
Write-Host "  Current version : $current"
Write-Host "  New version     : $Version"
Write-Host "  Tag             : $tag"
Write-Host "  Branch expected : $Branch"
Write-Host "  DryRun          : $DryRun"
Write-Host "  Push            : $Push"
Write-Host "  SkipTests       : $SkipTests"

$branchNow = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branchNow -ne $Branch -and -not $AllowBranch) {
  throw "Current branch is '$branchNow' (expected '$Branch'). Pass -AllowBranch to override."
}

$status = git status --porcelain
if ($status -and -not $AllowDirty) {
  throw "Working tree is dirty. Commit/stash first, or pass -AllowDirty.`n$status"
}

$existingTag = git tag -l $tag
if ($existingTag) {
  throw "Tag '$tag' already exists."
}

if (-not $SkipTests) {
  Write-Step "Running coverage gate (build/Test.ps1)"
  if ($DryRun) {
    Write-Host "DryRun: would run pwsh -File build/Test.ps1 -Configuration Release"
  }
  else {
    & pwsh -File (Join-Path $root "build/Test.ps1") -Configuration Release
    if ($LASTEXITCODE -ne 0) {
      throw "Tests/coverage gate failed."
    }
  }
}
else {
  Write-Host "Skipping tests (-SkipTests)." -ForegroundColor Yellow
}

# --- mutate -------------------------------------------------------------------

Write-Step "Bump version files"
if ($DryRun) {
  Write-Host "DryRun: would set Directory.Build.props Version=$Version"
  Write-Host "DryRun: would update CHANGELOG.md with [$Version] - $date"
}
else {
  Set-PropsVersion $props $Version
  Update-Changelog $changelog $Version $date
}

if ($SkipCommit) {
  Write-Step "Done (files updated, commit/tag skipped)"
  Write-Host "Next: review diff, then commit and tag $tag manually."
  exit 0
}

Write-Step "Commit + tag"
if ($DryRun) {
  Write-Host "DryRun: would commit version bump and create annotated tag $tag"
}
else {
  Invoke-Git @("add", "--", "Directory.Build.props", "CHANGELOG.md")
  $commitMsg = "chore(release): $tag"
  # Use a here-string file to avoid PowerShell quoting issues on Windows.
  $msgFile = Join-Path $env:TEMP "pien-release-msg.txt"
  Set-Content -LiteralPath $msgFile -Value $commitMsg -Encoding utf8
  try {
    Invoke-Git @("commit", "-F", $msgFile)
  }
  finally {
    Remove-Item -LiteralPath $msgFile -Force -ErrorAction SilentlyContinue
  }

  $tagFile = Join-Path $env:TEMP "pien-release-tag.txt"
  Set-Content -LiteralPath $tagFile -Value $tagMessage -Encoding utf8
  try {
    Invoke-Git @("tag", "-a", $tag, "-F", $tagFile)
  }
  finally {
    Remove-Item -LiteralPath $tagFile -Force -ErrorAction SilentlyContinue
  }
  Write-Host "Created commit and tag $tag"
}

if ($Push) {
  Write-Step "Push branch + tag to $Remote"
  if ($DryRun) {
    Write-Host "DryRun: would push $Branch and $tag to $Remote"
  }
  else {
    Invoke-Git @("push", $Remote, "HEAD:$Branch")
    Invoke-Git @("push", $Remote, $tag)
    Write-Host "Pushed. GitHub Actions release.yml should build publish artifacts for $tag."
  }
}
else {
  Write-Step "Local release ready (not pushed)"
  Write-Host "Push when ready:"
  Write-Host "  git push $Remote HEAD:$Branch"
  Write-Host "  git push $Remote $tag"
}
