#!/usr/bin/env bash
set -euo pipefail
# (untested)

if [[ $# -ne 1 ]]; then
  echo "Usage: $0 /path/to/linux-build-folder" >&2
  exit 1
fi

BUILD_DIR="$1"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

chmod +x "$SCRIPT_DIR"/*.sh

"$SCRIPT_DIR/make-tar.sh" "$BUILD_DIR"
"$SCRIPT_DIR/make-appimage.sh" "$BUILD_DIR"
"$SCRIPT_DIR/make-fpm.sh" deb "$BUILD_DIR"
"$SCRIPT_DIR/make-fpm.sh" rpm "$BUILD_DIR"
"$SCRIPT_DIR/make-fpm.sh" pacman "$BUILD_DIR"
"$SCRIPT_DIR/make-fpm.sh" apk "$BUILD_DIR"
"$SCRIPT_DIR/make-fpm.sh" sh "$BUILD_DIR"
#"$SCRIPT_DIR/make-flatpak.sh" "$BUILD_DIR"
#"$SCRIPT_DIR/make-snap.sh" "$BUILD_DIR"
