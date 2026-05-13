#!/usr/bin/env bash
# Render BOTH README casts (install + migration) from their .tape scripts.
#
# Requires:
#   vhs    https://github.com/charmbracelet/vhs
#   ttyd   (used by vhs internally)
#   ffmpeg (used by vhs internally)
#
# Install:
#   macOS    : brew install vhs ffmpeg ttyd
#   Linux    : see https://github.com/charmbracelet/vhs#installation
#   Windows  : winget install charmbracelet.vhs  (or use the WSL path)
#
# Usage:
#   bash scripts/make-cast.sh                # render both (default)
#   bash scripts/make-cast.sh install        # render only install-cast
#   bash scripts/make-cast.sh migration      # render only migration-cast
#
# Output:
#   docs/assets/install-cast.gif
#   docs/assets/migration-cast.gif

set -euo pipefail

# Resolve repo root regardless of where the script is invoked from.
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if ! command -v vhs >/dev/null 2>&1; then
  echo "❌ vhs not on PATH."
  echo
  echo "Install:"
  echo "  macOS:    brew install vhs ffmpeg ttyd"
  echo "  Linux:    see https://github.com/charmbracelet/vhs#installation"
  echo "  Windows:  winget install charmbracelet.vhs"
  exit 2
fi

cd "$REPO_ROOT"

target="${1:-both}"
case "$target" in
  install)
    vhs "$REPO_ROOT/docs/assets/install-cast.tape"
    echo "✓ Rendered docs/assets/install-cast.gif"
    ;;
  migration)
    vhs "$REPO_ROOT/docs/assets/migration-cast.tape"
    echo "✓ Rendered docs/assets/migration-cast.gif"
    ;;
  both)
    vhs "$REPO_ROOT/docs/assets/install-cast.tape"
    vhs "$REPO_ROOT/docs/assets/migration-cast.tape"
    echo
    echo "✓ Rendered both casts:"
    echo "    docs/assets/install-cast.gif"
    echo "    docs/assets/migration-cast.gif"
    ;;
  *)
    echo "❌ Unknown target '$target'. Use: install | migration | both (default)"
    exit 2
    ;;
esac

echo
echo "Commit with: git add docs/assets/*.gif && git commit"
