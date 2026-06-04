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
GENERATED_DIR="$ROOT_DIR/Builds/Generated/Flatpak"

APP_ID="org.bple.BPLE"                      # placeholder
RUNTIME="org.freedesktop.Platform"          # placeholder
RUNTIME_VERSION="24.08"                     # placeholder
SDK="org.freedesktop.Sdk"                   # placeholder
BRANCH="stable"                             # placeholder
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
PAYLOAD_DIR="$WORK_DIR/payload"
REPO_DIR="$WORK_DIR/repo"
BUILD_ROOT="$WORK_DIR/build"
MANIFEST="$WORK_DIR/${APP_ID}.yml"
ICON_SRC="$ROOT_DIR/Assets/Texture2D/App Icon.png"
ICON_OUT="$PAYLOAD_DIR/${APP_ID}.png"
DESKTOP_OUT="$PAYLOAD_DIR/${APP_ID}.desktop"
METAINF_OUT="$PAYLOAD_DIR/${APP_ID}.metainfo.xml"
OUT_FILE="$PUBLISHED_DIR/BPLE-${VERSION}-linux-x86_64.flatpak"

cleanup() {
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

mkdir -p "$PUBLISHED_DIR" "$PAYLOAD_DIR" "$REPO_DIR" "$BUILD_ROOT"

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
Exec=${APP_EXE}
Icon=${APP_ID}
Terminal=false
Categories=Game;
EOF

cat > "$METAINF_OUT" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<component type="desktop-application">
  <id>${APP_ID}</id>
  <name>BPLE ${VERSION}</name>
  <summary>BPLE ${VERSION}</summary>
  <metadata_license>CC0-1.0</metadata_license>
  <project_license>Proprietary</project_license>
</component>
EOF

cat > "$PAYLOAD_DIR/run.sh" <<EOF
#!/bin/sh
DIR="\$(dirname "\$(readlink -f "\$0")")"
cd "\$DIR"
exec "./$APP_EXE" "\$@"
EOF
chmod +x "$PAYLOAD_DIR/run.sh" "$PAYLOAD_DIR/$APP_EXE"

cat > "$MANIFEST" <<EOF
app-id: ${APP_ID}
runtime: ${RUNTIME}
runtime-version: '${RUNTIME_VERSION}'
sdk: ${SDK}
command: ${APP_EXE}

finish-args:
  - --share=ipc
  - --socket=x11
  - --socket=wayland
  - --socket=pulseaudio
  - --device=dri

modules:
  - name: bple
    buildsystem: simple
    build-commands:
      - install -D -m 755 "${APP_EXE}" "/app/bin/${APP_EXE}"
      - install -D -m 755 "run.sh" "/app/bin/run.sh"
      - install -D -m 644 "${APP_ID}.desktop" "/app/share/applications/${APP_ID}.desktop"
      - install -D -m 644 "${APP_ID}.metainfo.xml" "/app/share/metainfo/${APP_ID}.metainfo.xml"
      - install -D -m 644 "${APP_ID}.png" "/app/share/icons/hicolor/512x512/apps/${APP_ID}.png"
    sources:
      - type: dir
        path: ${PAYLOAD_DIR}
EOF

if ! command -v flatpak-builder >/dev/null 2>&1; then
  echo "flatpak-builder is not installed or not on PATH." >&2
  exit 1
fi

rm -rf "$REPO_DIR" "$BUILD_ROOT"
mkdir -p "$REPO_DIR" "$BUILD_ROOT"

flatpak-builder --force-clean --repo="$REPO_DIR" "$BUILD_ROOT" "$MANIFEST"

if [[ ! -f "$REPO_DIR/config" ]]; then
  echo "Flatpak repository was not created." >&2
  exit 1
fi

rm -f "$OUT_FILE"
flatpak build-bundle "$REPO_DIR" "$OUT_FILE" "$APP_ID" "$BRANCH"

echo "Built: $OUT_FILE"
