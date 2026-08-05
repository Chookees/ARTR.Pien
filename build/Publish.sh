#!/usr/bin/env bash
# Release packaging for ARTR Pien (no Docker, no trim/single-file/NativeAOT).
# Twin of build/Publish.ps1 — practical subset for Unix hosts.
set -euo pipefail

CONFIGURATION="${1:-Release}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PUBLISH_ROOT="$ROOT/artifacts/publish"
FD_OUT="$PUBLISH_ROOT/fd"
SC_ROOT="$PUBLISH_ROOT/sc"
NUPKG_OUT="$PUBLISH_ROOT/nupkg"
SBOM_OUT="$PUBLISH_ROOT/sbom"
CLI_PROJECT="$ROOT/src/ARTR.Pien.Cli/ARTR.Pien.Cli.csproj"
SLN="$ROOT/ARTR.Pien.sln"
CHECKSUM_FILE="$PUBLISH_ROOT/SHA256SUMS.txt"

step() { printf '\n==> %s\n' "$1"; }

write_checksums() {
  local root_dir="$1"
  local out_file="$2"
  : > "$out_file"
  (
    cd "$root_dir"
    # Prefer sha256sum; fall back to shasum on macOS.
    if command -v sha256sum >/dev/null 2>&1; then
      find . -type f ! -name 'SHA256SUMS.txt' -print0 | sort -z | xargs -0 sha256sum
    else
      find . -type f ! -name 'SHA256SUMS.txt' -print0 | sort -z | xargs -0 shasum -a 256
    fi
  ) > "$out_file"
  echo "Wrote checksums: $out_file"
}

publish_sc() {
  local rid="$1"
  local out="$SC_ROOT/$rid"
  rm -rf "$out"
  mkdir -p "$out"
  if dotnet publish "$CLI_PROJECT" \
      -c "$CONFIGURATION" \
      -r "$rid" \
      --self-contained true \
      -o "$out" \
      -p:PublishReadyToRun=false \
      -p:PublishSingleFile=false \
      -p:PublishTrimmed=false \
      -p:PublishAot=false; then
    echo "OK   $rid → $out"
    return 0
  fi
  rm -rf "$out"
  echo "SKIP $rid — SDK/runtime pack unavailable or publish unsupported without Docker."
  return 1
}

rm -rf "$PUBLISH_ROOT"
mkdir -p "$FD_OUT" "$SC_ROOT" "$NUPKG_OUT" "$SBOM_OUT"

step "Restore solution"
cd "$ROOT"
dotnet tool restore
dotnet restore "$SLN"

step "Framework-dependent publish → artifacts/publish/fd/"
dotnet publish "$CLI_PROJECT" \
  -c "$CONFIGURATION" \
  -o "$FD_OUT" \
  --self-contained false \
  -p:PublishReadyToRun=false \
  -p:PublishSingleFile=false \
  -p:PublishTrimmed=false \
  -p:PublishAot=false
echo "FD publish OK: $FD_OUT"

HOST_RID="$(dotnet --info 2>/dev/null | awk -F': ' '/RID:/{print $2; exit}' | tr -d '[:space:]')"
HOST_RID="${HOST_RID:-unknown}"

step "Self-contained publishes (no trim / single-file / NativeAOT)"
# Prefer host-family RIDs first; attempt cross-publish without Docker.
CANDIDATES=(win-x64 win-arm64 linux-x64 linux-arm64 osx-x64 osx-arm64)
declare -A RID_RESULTS=()
for rid in "${CANDIDATES[@]}"; do
  echo "--- RID $rid ---"
  if publish_sc "$rid"; then
    RID_RESULTS["$rid"]=ok
  else
    RID_RESULTS["$rid"]=skipped
  fi
done

step "dotnet pack (packable libraries + Cli tool package)"
PACKABLE=(
  src/ARTR.Pien.Core/ARTR.Pien.Core.csproj
  src/ARTR.Pien.Web/ARTR.Pien.Web.csproj
  src/ARTR.Pien.Checks/ARTR.Pien.Checks.csproj
  src/ARTR.Pien.Engine/ARTR.Pien.Engine.csproj
  src/ARTR.Pien.Reporting/ARTR.Pien.Reporting.csproj
  src/ARTR.Pien.Storage/ARTR.Pien.Storage.csproj
  src/ARTR.Pien.Hosting/ARTR.Pien.Hosting.csproj
  src/ARTR.Pien.Cli/ARTR.Pien.Cli.csproj
)
for rel in "${PACKABLE[@]}"; do
  echo "Pack $rel"
  dotnet pack "$ROOT/$rel" -c "$CONFIGURATION" -o "$NUPKG_OUT" --no-restore
done
echo "NuGet packages → $NUPKG_OUT"
ls -1 "$NUPKG_OUT"

step "SHA-256 checksums"
write_checksums "$PUBLISH_ROOT" "$CHECKSUM_FILE"

step "SBOM (CycloneDX)"
SBOM_GENERATED=0
if dotnet tool list --local 2>/dev/null | grep -qi CycloneDX; then
  if dotnet tool run dotnet-CycloneDX -- "$SLN" -o "$SBOM_OUT" -fn bom.json -F Json -t -ed \
    || dotnet tool run dotnet-CycloneDX -- "$CLI_PROJECT" -o "$SBOM_OUT" -fn bom.json -F Json -rs -ed; then
    if [[ -f "$SBOM_OUT/bom" && ! -f "$SBOM_OUT/bom.json" ]]; then
      mv "$SBOM_OUT/bom" "$SBOM_OUT/bom.json"
    fi
    if [[ -f "$SBOM_OUT/bom.json" ]]; then
      SBOM_GENERATED=1
      echo "SBOM OK: $SBOM_OUT/bom.json"
      write_checksums "$PUBLISH_ROOT" "$CHECKSUM_FILE"
    else
      echo "SKIP SBOM — tool ran but bom.json missing."
    fi
  else
    echo "SKIP SBOM — CycloneDX generation failed."
  fi
else
  echo "SKIP SBOM — CycloneDX not listed in local tool manifest after restore."
fi

step "Publish summary"
echo "Host RID: $HOST_RID"
echo "Framework-dependent: $FD_OUT"
echo "Self-contained results:"
for rid in "${CANDIDATES[@]}"; do
  printf '  %-12s %s\n' "$rid" "${RID_RESULTS[$rid]:-skipped}"
done
echo "NuGet: $NUPKG_OUT"
echo "Checksums: $CHECKSUM_FILE"
if [[ "$SBOM_GENERATED" -eq 1 ]]; then
  echo "SBOM: $SBOM_OUT/bom.json"
else
  echo "SBOM: skipped"
fi
echo "Trim/single-file/NativeAOT: disabled (not verified for this release)"
echo "Docker: not used"
echo "Publish completed."
