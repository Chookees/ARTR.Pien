#!/usr/bin/env bash
set -euo pipefail
CONFIGURATION="${1:-Release}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
dotnet test "$ROOT/ARTR.Pien.sln" -c "$CONFIGURATION" --collect:"XPlat Code Coverage"
