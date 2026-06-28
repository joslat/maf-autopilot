#!/usr/bin/env python3
"""Classify a MAF release as additive vs. breaking from its upstream artifacts.

Shared by the release-watcher scripts (`update_compat_matrix.py`,
`gen_guide_section.py`) and the workflow so the "green-on-arrival" decision is
made in ONE place:

  * A release is **additive** when its release notes carry no `.NET … [BREAKING]`
    entry AND its `dotnet-inspect` diff removed no members. Additive releases can
    be filled deterministically, so the watcher PR comes up green.
  * A release is **breaking** otherwise. Those need human-written migration
    guidance, so the watcher leaves a red scaffold for review.

Two design choices that keep this SAFE (a false "additive" is the dangerous
direction — it would let an unreviewed breaking release go green):

  1. `.NET`-scoped notes match. The upstream notes interleave Python and .NET
     changes; a `Python: [BREAKING]` line does not affect the .NET package this
     toolkit tracks and must NOT flip the verdict. We match only lines that
     mention `.NET` AND `[BREAKING]`.
  2. Removals always count as breaking. A member removal is a CS0246 break even
     if the notes never tagged it `[BREAKING]`, so ANY removed member in the
     diff forces a breaking verdict regardless of the notes.
"""
from __future__ import annotations

import re

# A ".NET … [BREAKING]" marker anywhere on a release-notes line. Matches
# ".NET: [BREAKING] …" and ".NET/Python: [BREAKING] …", but NOT
# "Python: [BREAKING] …" (no ".NET" token on the line before the marker).
_NET_BREAKING_RE = re.compile(r'\.NET[^\n]*\[BREAKING\]', re.IGNORECASE)

# "Member 'Foo' was added" / "Type 'Bar' was added" from a dotnet-inspect diff.
_ADDED_MEMBER_RE = re.compile(r"(?:Member|Type)\s+'([^']+)'\s+was added")
_REMOVED_MEMBER_RE = re.compile(r"(?:Member|Type)\s+'([^']+)'\s+was removed")


def dotnet_breaking_lines(release_notes: str) -> list[str]:
    """Return the trimmed `.NET … [BREAKING]` lines from the release notes."""
    if not release_notes:
        return []
    return [
        line.strip()
        for line in release_notes.splitlines()
        if _NET_BREAKING_RE.search(line)
    ]


def _distinct_in_order(names) -> list[str]:
    seen: dict[str, None] = {}
    for name in names:
        seen.setdefault(name, None)
    return list(seen)


def added_members(diff_text: str) -> list[str]:
    """Distinct member/type names reported as added in a diff, first-seen order."""
    return _distinct_in_order(_ADDED_MEMBER_RE.findall(diff_text or ""))


def removed_members(diff_text: str) -> list[str]:
    """Distinct member/type names reported as removed in a diff, first-seen order."""
    return _distinct_in_order(_REMOVED_MEMBER_RE.findall(diff_text or ""))


def is_additive(release_notes: str, diff_text: str = "") -> bool:
    """True only when the notes carry no .NET breaking marker AND the diff
    removed no members. Conservative by design: ANY breaking signal ⇒ False."""
    return not dotnet_breaking_lines(release_notes) and not removed_members(diff_text)


def classify(release_notes: str, diff_text: str = "") -> dict:
    """Full verdict, handy for the workflow / labeling.

    Returns a dict with: `additive` (bool), `breaking_notes` (list[str]),
    `removed` (list[str]), `added` (list[str])."""
    breaking_notes = dotnet_breaking_lines(release_notes)
    removed = removed_members(diff_text)
    return {
        "additive": not breaking_notes and not removed,
        "breaking_notes": breaking_notes,
        "removed": removed,
        "added": added_members(diff_text),
    }


if __name__ == "__main__":
    # CLI: prints "additive" or "breaking" for the workflow to capture.
    # Reads release-notes.txt + diff-core.txt from CWD (the same artifacts the
    # sibling watcher scripts read).
    import pathlib

    def _read(name: str) -> str:
        p = pathlib.Path(name)
        return p.read_text(encoding="utf-8") if p.is_file() else ""

    verdict = classify(_read("release-notes.txt"), _read("diff-core.txt"))
    print("additive" if verdict["additive"] else "breaking")
