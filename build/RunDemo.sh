#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
dotnet run --project "$ROOT/samples/ARTR.Pien.SampleSite" -c Release --urls http://127.0.0.1:5088 &
PID=$!
sleep 3
dotnet run --project "$ROOT/src/ARTR.Pien.Cli" -c Release -- scan --target http://127.0.0.1:5088/ --format console,json --output artifacts/demo --quiet || true
kill $PID || true
