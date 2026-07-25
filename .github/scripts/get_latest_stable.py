"""Pick the latest STABLE version from a NuGet v3 flatcontainer index.json.

Reads the raw index JSON on stdin and prints the latest stable version to
stdout (empty string if there are only prereleases / no versions).

Extracted from an inline `python3 -c "..."` heredoc in
.github/workflows/maf-release-watcher.yml. The inline form carried the
surrounding shell-block indentation into the code string, so Python raised
`IndentationError: unexpected indent` on every scheduled run (the NuGet-query
path). A real module cannot inherit that indentation, and it is unit-testable
and covered by the compile-check CI gate.
"""
from __future__ import annotations

import json
import sys


def latest_stable(raw: str) -> str:
    """Return the last (latest) stable version in the NuGet index, or ''.

    NuGet's flatcontainer index lists versions in ascending order, so the last
    stable entry is the newest. Empty/whitespace input or only-prereleases both
    yield ''.

    SEC-28: a version is prerelease iff its SemVer core (the part before any '+'
    build-metadata) contains a hyphen. The old denylist only knew -alpha/-beta/-rc/
    -preview, so any other label (-dev, -nightly, -daily, -ci, …) was auto-processed
    as stable. A manual workflow_dispatch with an explicit `maf_version` remains the
    path for prereleases.
    """
    raw = (raw or "").strip()
    if not raw:
        return ""
    data = json.loads(raw)
    stable = [v for v in data.get("versions", []) if "-" not in v.split("+", 1)[0]]
    return stable[-1] if stable else ""


if __name__ == "__main__":
    print(latest_stable(sys.stdin.read()))
