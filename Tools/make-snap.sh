#!/usr/bin/env bash
# (untested)
# (unused)
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 /path/to/linux-build-folder" >&2
  exit 1
fi

BUILD_DIR="$(cd "$1" && pwd)"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PUBLISHED_DIR="$ROOT_DIR/Builds/Published"
GENERATED_DIR="$ROOT_DIR/Builds/Generated/Snap"

SNAP_NAME="bple"              # placeholder
SNAP_BASE="core24"            # placeholder
SNAP_GRADE="devel"            # placeholder
APP_EXE="新创Unity.x86_64"
BACKUP_FOLDER="新创Unity_BackUpThisFolder_ButDontShipItWithYourGame"

BUILD_NAME="$(basename "$BUILD_DIR")"
if [[ "$BUILD_NAME" =~ ^BPLE[[:space:]]+(.+)[[:space:]]+Linux$ ]]; then
  VERSION="${BASH_REMATCH[1]}"
else
  echo "Could not infer version from build folder name: $BUILD_NAME" >&2
  exit 1
fi

WORK_DIR="$GENERATED_DIR/work"
PRIME_DIR="$WORK_DIR/prime"
META_DIR="$PRIME_DIR/meta"
GUI_DIR="$META_DIR/gui"
BIN_DIR="$PRIME_DIR/bin"
PAYLOAD_DIR="$PRIME_DIR/opt/$SNAP_NAME"
ICON_SRC="$ROOT_DIR/Assets/Texture2D/App Icon.png"
ICON_OUT="$GUI_DIR/${SNAP_NAME}.png"
DESKTOP_OUT="$GUI_DIR/${SNAP_NAME}.desktop"
SNAPYAML_OUT="$META_DIR/snap.yaml"
OUT_FILE="$PUBLISHED_DIR/BPLE-${VERSION}-linux-x86_64.snap"

cleanup() {
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

mkdir -p "$PUBLISHED_DIR" "$GUI_DIR" "$BIN_DIR" "$PAYLOAD_DIR"

if [[ ! -f "$ICON_SRC" ]]; then
  echo "Icon not found: $ICON_SRC" >&2
  exit 1
fi

cp -a "$BUILD_DIR"/. "$PAYLOAD_DIR"/
rm -rf "$PAYLOAD_DIR/$BACKUP_FOLDER"

if [[ ! -f "$PAYLOAD_DIR/$APP_EXE" ]]; then
  echo "Missing Linux executable: $APP_EXE" >&2
  exit 1
fi

if command -v magick >/dev/null 2>&1; then
  magick "$ICON_SRC" -resize 512x512 "$ICON_OUT"
elif command -v convert >/dev/null 2>&1; then
  convert "$ICON_SRC" -resize 512x512 "$ICON_OUT"
else
  echo "ImageMagick not found. Install it first so the icon can be resized." >&2
  exit 1
fi

cat > "$DESKTOP_OUT" <<EOF
[Desktop Entry]
Type=Application
Name=BPLE ${VERSION}
Exec=${SNAP_NAME}
Icon=${SNAP_NAME}
Terminal=false
Categories=Game;
EOF

cat > "$BIN_DIR/run.sh" <<EOF
#!/bin/sh
DIR="\${SNAP}/opt/${SNAP_NAME}"
cd "\$DIR"
exec "./$APP_EXE" "\$@"
EOF
chmod +x "$BIN_DIR/run.sh" "$PAYLOAD_DIR/$APP_EXE"

cat > "$SNAPYAML_OUT" <<EOF
name: ${SNAP_NAME}
base: ${SNAP_BASE}
version: "${VERSION}"
summary: BPLE ${VERSION}
description: |
  BPLE ${VERSION}
grade: ${SNAP_GRADE}
confinement: strict

apps:
  ${SNAP_NAME}:
    command: bin/run.sh
EOF

if ! command -v snapcraft >/dev/null 2>&1; then
  echo "snapcraft is not installed or not on PATH." >&2
  exit 1
fi

rm -f "$OUT_FILE"
(
  cd "$PUBLISHED_DIR"
  snapcraft pack "$PRIME_DIR"
)

BUILT_SNAP="$(find "$PUBLISHED_DIR" -maxdepth 1 -name '*.snap' -print -quit || true)"
if [[ -z "$BUILT_SNAP" ]]; then
  echo "snapcraft finished but no .snap was produced." >&2
  exit 1
fi

mv -f "$BUILT_SNAP" "$OUT_FILE"
echo "Built: $OUT_FILE"
