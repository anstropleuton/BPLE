#!/usr/bin/env bash
# (untested)
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 /path/to/linux-build-folder" >&2
  exit 1
fi

BUILD_DIR="$(cd "$1" && pwd)"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PUBLISHED_DIR="$ROOT_DIR/Builds/Published"
GENERATED_DIR="$ROOT_DIR/Builds/Generated/Linux"
STAGE_DIR="$GENERATED_DIR/tar-stage"

BUILD_NAME="$(basename "$BUILD_DIR")"
if [[ "$BUILD_NAME" =~ ^BPLE[[:space:]]+(.+)[[:space:]]+Linux$ ]]; then
  VERSION="${BASH_REMATCH[1]}"
else
  echo "Could not infer version from build folder name: $BUILD_NAME" >&2
  exit 1
fi

BACKUP_FOLDER="新创Unity_BackUpThisFolder_ButDontShipItWithYourGame"
OUT_FILE="$PUBLISHED_DIR/BPLE-${VERSION}-linux-x86_64.tar.gz"

rm -rf "$STAGE_DIR"
mkdir -p "$PUBLISHED_DIR" "$GENERATED_DIR" "$STAGE_DIR"

cp -a "$BUILD_DIR"/. "$STAGE_DIR"/
rm -rf "$STAGE_DIR/$BACKUP_FOLDER"

tar -czf "$OUT_FILE" -C "$STAGE_DIR" .

rm -rf "$STAGE_DIR"
echo "Built: $OUT_FILE"