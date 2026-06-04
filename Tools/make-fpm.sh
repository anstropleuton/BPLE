#!/usr/bin/env bash
# (untested)
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "Usage: $0 <deb|rpm|pacman|apk> /path/to/linux-build-folder" >&2
  exit 1
fi

TYPE="$1"
BUILD_DIR="$(cd "$2" && pwd)"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
PUBLISHED_DIR="$ROOT_DIR/Builds/Published"
GENERATED_DIR="$ROOT_DIR/Builds/Generated/Linux"
STAGE_DIR="$GENERATED_DIR/fpm-stage-$TYPE"
ICON_SRC="$ROOT_DIR/Assets/Texture2D/App Icon.png"

BUILD_NAME="$(basename "$BUILD_DIR")"
if [[ "$BUILD_NAME" =~ ^BPLE[[:space:]]+(.+)[[:space:]]+Linux$ ]]; then
  VERSION="${BASH_REMATCH[1]}"
else
  echo "Could not infer version from build folder name: $BUILD_NAME" >&2
  exit 1
fi

APP_NAME="BPLE ${VERSION}"
PKG_NAME="bple"
APP_EXE="新创Unity.x86_64"
BACKUP_FOLDER="新创Unity_BackUpThisFolder_ButDontShipItWithYourGame"

case "$TYPE" in
  deb)
    FPM_ARCH="amd64"
    OUT_FILE="$PUBLISHED_DIR/${PKG_NAME}_${VERSION}_amd64.deb"
    ;;
  rpm)
    FPM_ARCH="x86_64"
    OUT_FILE="$PUBLISHED_DIR/${PKG_NAME}-${VERSION}.x86_64.rpm"
    ;;
  pacman)
    FPM_ARCH="x86_64"
    OUT_FILE="$PUBLISHED_DIR/${PKG_NAME}-${VERSION}-x86_64.pkg.tar.zst"
    ;;
  apk)
    FPM_ARCH="x86_64"
    OUT_FILE="$PUBLISHED_DIR/${PKG_NAME}-${VERSION}-x86_64.apk"
    ;;
  sh)
    FPM_ARCH="x86_64"
    OUT_FILE="$PUBLISHED_DIR/${PKG_NAME}-${VERSION}-x86_64.sh"
    ;;
  *)
    echo "Unsupported package type: $TYPE" >&2
    exit 1
    ;;
esac

if ! command -v fpm >/dev/null 2>&1; then
  echo "fpm is not installed or not on PATH." >&2
  exit 1
fi

if [[ ! -f "$ICON_SRC" ]]; then
  echo "Icon not found: $ICON_SRC" >&2
  exit 1
fi

rm -rf "$STAGE_DIR"
mkdir -p \
  "$PUBLISHED_DIR" \
  "$GENERATED_DIR" \
  "$STAGE_DIR/opt/$PKG_NAME" \
  "$STAGE_DIR/usr/bin" \
  "$STAGE_DIR/usr/share/applications" \
  "$STAGE_DIR/usr/share/pixmaps" \
  "$STAGE_DIR/usr/share/icons/hicolor/512x512/apps"

cp -a "$BUILD_DIR"/. "$STAGE_DIR/opt/$PKG_NAME"/
rm -rf "$STAGE_DIR/opt/$PKG_NAME/$BACKUP_FOLDER"

if [[ ! -f "$STAGE_DIR/opt/$PKG_NAME/$APP_EXE" ]]; then
  echo "Missing Linux executable: $APP_EXE" >&2
  exit 1
fi

cat > "$STAGE_DIR/usr/bin/$PKG_NAME" <<EOF
#!/bin/sh
DIR="/opt/$PKG_NAME"
cd "\$DIR"
exec "./$APP_EXE" "\$@"
EOF
chmod +x "$STAGE_DIR/usr/bin/$PKG_NAME" "$STAGE_DIR/opt/$PKG_NAME/$APP_EXE"

cat > "$STAGE_DIR/usr/share/applications/$PKG_NAME.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=${APP_NAME}
Exec=${PKG_NAME}
Icon=${PKG_NAME}
Terminal=false
Categories=Game;
EOF

cp "$ICON_SRC" "$STAGE_DIR/usr/share/pixmaps/${PKG_NAME}.png"
cp "$ICON_SRC" "$STAGE_DIR/usr/share/icons/hicolor/512x512/apps/${PKG_NAME}.png"

rm -f "$OUT_FILE"

pushd "$STAGE_DIR" >/dev/null
fpm -s dir -t "$TYPE" -a "$FPM_ARCH" \
  -n "$PKG_NAME" \
  -v "$VERSION" \
  --iteration 1 \
  --description "$APP_NAME" \
  -p "$OUT_FILE" \
  opt/$PKG_NAME=/opt/$PKG_NAME \
  usr/bin/$PKG_NAME=/usr/bin/$PKG_NAME \
  usr/share/applications/$PKG_NAME.desktop=/usr/share/applications/$PKG_NAME.desktop \
  usr/share/pixmaps/${PKG_NAME}.png=/usr/share/pixmaps/${PKG_NAME}.png \
  usr/share/icons/hicolor/512x512/apps/${PKG_NAME}.png=/usr/share/icons/hicolor/512x512/apps/${PKG_NAME}.png
popd >/dev/null

rm -rf "$STAGE_DIR"
echo "Built: $OUT_FILE"