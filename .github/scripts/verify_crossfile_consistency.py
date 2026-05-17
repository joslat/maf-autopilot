#!/usr/bin/env python3
"""
Cross-file consistency checks for MAF release artifacts.

Runs AFTER `maf-autopilot verify-registry` (which checks registry.yaml
entry-internal consistency). This script checks consistency ACROSS the
files the release-watcher touches.

Findings discovered 2026-05-17 on PR #26 that motivated these checks:
- Bot ADDED a new `**1.6.1**` row to compat-matrix instead of UPDATING the
  watcher-inserted placeholder row → duplicate version rows.
- "Version Tracking" section still said the old version after a release.

Checks performed:
1. docs/compatibility-matrix.md has no DUPLICATE `**X.Y.Z**` rows.
2. The TOP data row in the compat-matrix table matches `.maf-version`.
3. "Version Tracking" section's "Current tracked version: **X.Y.Z**" line
   matches `.maf-version`.
4. `guides/maf-{VERSION}-migration-guide.md` exists for `.maf-version`.

Exits 0 if all consistent, 1 if any check fails. Prints findings to stdout.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path.cwd()
MAF_VERSION_FILE = ROOT / ".maf-version"
COMPAT_MATRIX = ROOT / "docs" / "compatibility-matrix.md"
GUIDES_DIR = ROOT / "guides"

# A "version-stem" row in compat-matrix table starts with: | **X.Y.Z** | ...
# Allow optional leading whitespace and a 2- or 3-part version.
VERSION_ROW_RE = re.compile(
    r'^\s*\|\s*\*\*(\d+\.\d+(?:\.\d+)?)\*\*\s*\|', re.MULTILINE
)
CURRENT_TRACKED_RE = re.compile(
    r'Current tracked version:\s*\*\*`?(\d+\.\d+(?:\.\d+)?)`?\*\*',
    re.IGNORECASE,
)


def find_duplicate_rows(text: str) -> list[str]:
    """Return version strings that appear more than once as a row stem."""
    versions = VERSION_ROW_RE.findall(text)
    seen: set[str] = set()
    dupes: list[str] = []
    for v in versions:
        if v in seen and v not in dupes:
            dupes.append(v)
        seen.add(v)
    return dupes


def top_version_row(text: str) -> str | None:
    """Return the first matched version (the top row of the table), or None."""
    m = VERSION_ROW_RE.search(text)
    return m.group(1) if m else None


def version_tracking_current(text: str) -> str | None:
    """Return the version mentioned in 'Current tracked version: **X.Y.Z**'."""
    m = CURRENT_TRACKED_RE.search(text)
    return m.group(1) if m else None


def main() -> int:
    findings: list[str] = []

    if not MAF_VERSION_FILE.is_file():
        print(f"ERROR: {MAF_VERSION_FILE} not found.", file=sys.stderr)
        return 1

    current = MAF_VERSION_FILE.read_text(encoding="utf-8").strip()
    print(f"Current .maf-version: {current}")

    if not COMPAT_MATRIX.is_file():
        findings.append(f"{COMPAT_MATRIX} not found.")
    else:
        text = COMPAT_MATRIX.read_text(encoding="utf-8")

        dupes = find_duplicate_rows(text)
        if dupes:
            findings.append(
                f"compatibility-matrix.md has DUPLICATE version rows: "
                f"{', '.join(dupes)}. Each version must appear at most once "
                f"as a `**X.Y.Z**` row. The watcher inserts a placeholder row "
                f"at the top — the filler bot should UPDATE that row, not "
                f"add a new one below."
            )

        top = top_version_row(text)
        if top is None:
            findings.append(
                "compatibility-matrix.md: could not find any `**X.Y.Z**` "
                "version row — table may be malformed."
            )
        elif top != current:
            findings.append(
                f"compatibility-matrix.md top data row is `**{top}**` but "
                f".maf-version is `{current}`. The newest version should "
                f"be at the top of the table."
            )

        tracked = version_tracking_current(text)
        if tracked is None:
            print(
                "Note: no 'Current tracked version: **X.Y.Z**' line found in "
                "compatibility-matrix.md — skipping that check."
            )
        elif tracked != current:
            findings.append(
                f"compatibility-matrix.md 'Version Tracking' section says "
                f"current tracked version is `{tracked}` but .maf-version is "
                f"`{current}`. Update the 'Current tracked version' line."
            )

    guide_file = GUIDES_DIR / f"maf-{current}-migration-guide.md"
    if not guide_file.is_file():
        findings.append(
            f"missing migration guide: {guide_file.relative_to(ROOT)}"
        )

    if findings:
        print("")
        print("=" * 72)
        print("CROSS-FILE CONSISTENCY ISSUES:")
        print("=" * 72)
        for f in findings:
            print(f"  * {f}")
        print("")
        return 1

    print("All cross-file consistency checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
