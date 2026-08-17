"""Pick a STABLE version from a NuGet v3 flatcontainer index.json.

Reads the raw index JSON on stdin. By default it prints the latest stable
version. With ``--after X.Y.Z`` it prints the oldest stable version newer than
that version, allowing the release watcher to drain a backlog one release at a
time without skipping intermediate migration guides.

Extracted from an inline `python3 -c "..."` heredoc in
.github/workflows/maf-release-watcher.yml. The inline form carried the
surrounding shell-block indentation into the code string, so Python raised
`IndentationError: unexpected indent` on every scheduled run (the NuGet-query
path). A real module cannot inherit that indentation, and it is unit-testable
and covered by the compile-check CI gate.
"""
from __future__ import annotations

import argparse
import json
import re
import sys


def latest_stable(raw: str) -> str:
    """Return the last (latest) stable version in the NuGet index, or ''.

    NuGet's flatcontainer index lists versions in ascending order, so the last
    stable entry is the newest. Empty/whitespace input or only-prereleases both
    yield ''.

    SEC-28: a version is prerelease iff its SemVer core (the part before any '+'
    build-metadata) contains a hyphen. The old denylist only knew -alpha/-beta/-rc/
    -preview, so any other label (-dev, -nightly, -daily, -ci, …) was auto-processed
    as stable. The release watcher intentionally processes stable releases only;
    its manual input is an assertion of the next pending stable version, not a
    prerelease override.
    """
    raw = (raw or "").strip()
    if not raw:
        return ""
    data = json.loads(raw)
    stable = [v for v in data.get("versions", []) if "-" not in v.split("+", 1)[0]]
    return max(stable, key=_semver_key) if stable else ""


def _semver_key(version: str) -> tuple[int, int, int]:
    """Return the numeric SemVer core used by the MAF stable feed."""
    core = version.split("+", 1)[0]
    match = re.fullmatch(r"(\d+)\.(\d+)\.(\d+)", core)
    if not match:
        raise ValueError(f"not a stable three-part SemVer: {version}")
    return tuple(int(part) for part in match.groups())


def next_stable_after(raw: str, current: str) -> str:
    """Return the oldest stable version newer than ``current``, or ''.

    Selecting the first pending stable release is intentional. A watcher PR
    must be filled and merged before the next release is scaffolded, otherwise
    multiple branches are generated from the same stale ``.maf-version`` and
    intermediate matrix/guide entries disappear from the additive chain.
    """
    raw = (raw or "").strip()
    if not raw:
        return ""
    data = json.loads(raw)
    current_key = _semver_key(current)
    stable = sorted(
        (v for v in data.get("versions", []) if "-" not in v.split("+", 1)[0]),
        key=_semver_key,
    )
    return next((v for v in stable if _semver_key(v) > current_key), "")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--after",
        help="print the oldest stable version newer than this SemVer",
    )
    args = parser.parse_args()
    raw_index = sys.stdin.read()
    print(next_stable_after(raw_index, args.after) if args.after else latest_stable(raw_index))
