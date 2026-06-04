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
DIST_DIR="$ROOT_DIR/dist"
GENERATED_DIR="$ROOT_DIR/Builds/Generated/Linux"
APPDIR="$GENERATED_DIR/AppDir"
LINUXDEPLOY="$GENERATED_DIR/linuxdeploy-x86_64.AppImage"
ICON_SRC="$ROOT_DIR/Assets/Texture2D/App Icon.png"

BUILD_NAME="$(basename "$BUILD_DIR")"
if [[ "$BUILD_NAME" =~ ^BPLE[[:space:]]+(.+)[[:space:]]+Linux$ ]]; then
  VERSION="${BASH_REMATCH[1]}"
else
  echo "Could not infer version from build folder name: $BUILD_NAME" >&2
  exit 1
fi

APP_NAME="BPLE $VERSION"
PKG_NAME="bple"
APP_EXE="新创Unity.x86_64"
BACKUP_FOLDER="新创Unity_BackUpThisFolder_ButDontShipItWithYourGame"
APPIMAGE_OUT="$DIST_DIR/BPLE-${VERSION}-linux-x86_64.AppImage"

fetch_linuxdeploy() {
  local url="https://github.com/linuxdeploy/linuxdeploy/releases/latest/download/linuxdeploy-x86_64.AppImage"

  mkdir -p "$GENERATED_DIR"

  if command -v curl >/dev/null 2>&1; then
    curl -L --fail --silent --show-error -o "$LINUXDEPLOY" "$url"
  elif command -v wget >/dev/null 2>&1; then
    wget -O "$LINUXDEPLOY" "$url"
  else
    echo "Neither curl nor wget is available to fetch linuxdeploy." >&2
    exit 1
  fi

  chmod +x "$LINUXDEPLOY"
}

rm -rf "$APPDIR"
mkdir -p "$DIST_DIR" "$GENERATED_DIR" "$APPDIR"

if [[ ! -f "$ICON_SRC" ]]; then
  echo "Icon not found: $ICON_SRC" >&2
  exit 1
fi

if [[ ! -f "$LINUXDEPLOY" ]]; then
  fetch_linuxdeploy
else
  chmod +x "$LINUXDEPLOY"
fi

cp -a "$BUILD_DIR"/. "$APPDIR"/
rm -rf "$APPDIR/$BACKUP_FOLDER"

if [[ ! -f "$APPDIR/$APP_EXE" ]]; then
  echo "Missing Linux executable: $APP_EXE" >&2
  exit 1
fi

cat > "$APPDIR/AppRun" <<EOF
#!/bin/sh
DIR="\$(dirname "\$(readlink -f "\$0")")"
cd "\$DIR"
exec "./$APP_EXE" "\$@"
EOF
chmod +x "$APPDIR/AppRun" "$APPDIR/$APP_EXE"

cat > "$APPDIR/${PKG_NAME}.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=${APP_NAME}
Exec=${APP_EXE}
Icon=${PKG_NAME}
Terminal=false
Categories=Game;
EOF

cp "$ICON_SRC" "$APPDIR/${PKG_NAME}.png"

find "$DIST_DIR" -maxdepth 1 -name '*.AppImage' -delete

(
  cd "$DIST_DIR"
  "$LINUXDEPLOY" \
    --appdir "$APPDIR" \
    --executable "$APPDIR/$APP_EXE" \
    --desktop-file "$APPDIR/${PKG_NAME}.desktop" \
    --icon-file "$APPDIR/${PKG_NAME}.png" \
    --output appimage
)

BUILT_APPIMAGE="$(find "$DIST_DIR" -maxdepth 1 -name '*.AppImage' -print -quit || true)"
if [[ -z "$BUILT_APPIMAGE" ]]; then
  echo "linuxdeploy finished but no AppImage was produced." >&2
  exit 1
fi

mv -f "$BUILT_APPIMAGE" "$APPIMAGE_OUT"
rm -rf "$APPDIR"

echo "Built: $APPIMAGE_OUT"