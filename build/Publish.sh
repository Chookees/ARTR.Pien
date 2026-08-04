#!/usr/bin/env bash
set -euo pipefail
CONFIGURATION="${1:-Release}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/artifacts/publish"
mkdir -p "$OUT"
dotnet publish "$ROOT/src/ARTR.Pien.Cli/ARTR.Pien.Cli.csproj" -c "$CONFIGURATION" -o "$OUT"
