#!/usr/bin/env bash
set -euo pipefail
CONFIGURATION="${1:-Release}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
dotnet restore "$ROOT/ARTR.Pien.sln"
dotnet build "$ROOT/ARTR.Pien.sln" -c "$CONFIGURATION" --no-restore
