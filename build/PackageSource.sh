#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/artifacts"
STAGE="$OUT/source-stage/ARTR/Pien"
ZIP="$OUT/ARTR.Pien-source.zip"
rm -rf "$OUT/source-stage"
mkdir -p "$STAGE"
rsync -a --exclude '.git' --exclude 'artifacts' --exclude 'bin' --exclude 'obj' --exclude 'inDev' --exclude '.pien' --exclude 'TestResults' "$ROOT/" "$STAGE/"
(cd "$OUT/source-stage" && zip -qr "$ZIP" ARTR)
HASH="$(sha256sum "$ZIP" | awk '{print $1}')"
echo "$HASH  ARTR.Pien-source.zip" > "$OUT/ARTR.Pien-source.zip.sha256"
printf 'name=ARTR.Pien-source.zip\nsha256=%s\ncreated=%s\n' "$HASH" "$(date -Iseconds)" > "$OUT/ARTR.Pien-source.manifest.txt"
echo "Created $ZIP"
echo "SHA256 $HASH"
