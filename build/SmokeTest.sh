#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
dotnet run --project "$ROOT/src/ARTR.Pien.Cli" -c Release -- version
dotnet run --project "$ROOT/src/ARTR.Pien.Cli" -c Release -- doctor
dotnet run --project "$ROOT/src/ARTR.Pien.Cli" -c Release -- list-checks
echo "Smoke test completed."
