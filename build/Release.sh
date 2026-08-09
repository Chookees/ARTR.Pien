#!/usr/bin/env bash
# Twin of build/Release.ps1 — cut a release from the current tree.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
exec pwsh -File "$ROOT/build/Release.ps1" "$@"
