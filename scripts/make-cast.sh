#!/usr/bin/env bash
# Render docs/assets/install-cast.gif from the vhs .tape script.
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
#   bash scripts/make-cast.sh           # from repo root
#
# Output:
#   docs/assets/install-cast.gif

set -euo pipefail

# Resolve repo root regardless of where the script is invoked from.
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TAPE="$REPO_ROOT/docs/assets/install-cast.tape"

if ! command -v vhs >/dev/null 2>&1; then
  echo "❌ vhs not on PATH."
  echo
  echo "Install:"
  echo "  macOS:    brew install vhs ffmpeg ttyd"
  echo "  Linux:    see https://github.com/charmbracelet/vhs#installation"
  echo "  Windows:  winget install charmbracelet.vhs"
  exit 2
fi

if [ ! -f "$TAPE" ]; then
  echo "❌ Tape file not found: $TAPE"
  exit 2
fi

cd "$REPO_ROOT"
vhs "$TAPE"

echo
echo "✓ Rendered docs/assets/install-cast.gif"
echo "  Commit with:  git add docs/assets/install-cast.gif && git commit"
