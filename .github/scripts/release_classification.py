#!/usr/bin/env python3
"""Classify a MAF release as additive vs. breaking from its upstream artifacts.

Shared by the release-watcher scripts (`update_compat_matrix.py`,
`gen_guide_section.py`) and the workflow so the "green-on-arrival" decision is
made in ONE place.

A release is **additive** only when ALL of these hold:
  * the diff is TRUSTWORTHY — it shows recognizable dotnet-inspect output (change
    markers or the explicit "No API changes detected" banner), NOT an error dump
    or empty text;
  * the release notes carry no `.NET … [BREAKING]` entry;
  * the diff removed no members;
  * the diff changed no member SIGNATURES.
Anything else is **breaking** (or unverifiable → treated as breaking). A false
"additive" is the dangerous direction — it would let an unreviewed breaking
release go green — so the verdict is FAIL-CLOSED: missing/garbled evidence never
yields additive.

Design choices that keep this safe:
  1. `.NET`-scoped, order-independent notes match. Python-only `[BREAKING]` lines
     don't affect the .NET package this toolkit tracks. We match a line that
     mentions BOTH `.NET` and `[BREAKING]` in either order.
  2. Removals AND signature changes both count as breaking, even with no
     `[BREAKING]` note — they are CS0246 / source-incompatible breaks. This
     mirrors DiffPackageTool.ClassifyKind / RegistryExtractCommand in the C#
     code so the Python classifier and the extractor agree on "breaking".
  3. Both package diffs (Microsoft.Agents.AI AND .Workflows) are considered;
     a removal in either is breaking.
"""
from __future__ import annotations

import pathlib
import re

# A ".NET … [BREAKING]" marker on a line, in EITHER order (`.NET: [BREAKING] …`
# or `… [BREAKING] … .NET`). Excludes `Python: [BREAKING] …` (no `.NET` token).
_NET_BREAKING_RE = re.compile(
    r'(?:\.NET[^\n]*\[BREAKING\]|\[BREAKING\][^\n]*\.NET)', re.IGNORECASE)

# dotnet-inspect diff verbs. `[^'\n]` (NOT `[^']`) so a crafted name carrying a
# newline cannot span lines and smuggle structure into a captured member name.
_ADDED_MEMBER_RE = re.compile(r"(?:Member|Type)\s+'([^'\n]+)'\s+was added")
_REMOVED_MEMBER_RE = re.compile(r"(?:Member|Type)\s+'([^'\n]+)'\s+was removed")
_SIGNATURE_CHANGED_RE = re.compile(r"(?:Member|Type)\s+'([^'\n]+)'\s+signature changed")

# A newly-added member carrying `[Obsolete]` is a deprecation: source-compatible
# (CS0618 warning) but a heads-up consumers must be told about, and the C#
# registry-extract DOES emit a draft for it. So it is NOT "purely additive" — we
# route it to the breaking/needs-review path so the verdict and the extractor
# agree (otherwise the PR is labeled additive-green while the gate holds it red).
_OBSOLETE_RE = re.compile(r'\[Obsolete\]', re.IGNORECASE)

# A member/type name is only safe to echo into generated SOURCE or docs if it is
# a plain C# identifier. Names come from untrusted upstream (TA-1). `\A…\Z` (not
# `^…$`) so a trailing newline cannot slip through ($ matches before a final \n).
_SAFE_MEMBER_RE = re.compile(r'\A[A-Za-z_][A-Za-z0-9_]*\Z')

# Markers whose presence proves the diff actually PARSED (vs. an error dump).
# `API Diff:` is dotnet-inspect's real report header (`# API Diff: <package>`);
# we do NOT trust `diff --package` (that's invocation/usage syntax echoed by
# help/error text, never emitted by a successful diff — admitting it would let
# an error dump masquerade as a parsed diff and re-open the fail-open hole).
_DIFF_PARSED_MARKERS = (
    'was added', 'was removed', 'signature changed',
    'No API changes detected', 'API Diff:',
)


def diff_is_trustworthy(diff_text: str) -> bool:
    """An ADDITIVE verdict may only rest on a diff we can read. Empty text, a
    NuGet error, or a stack trace shows none of the dotnet-inspect markers and is
    NOT trustworthy → the caller must treat the release as breaking/unverifiable."""
    if not diff_text or not diff_text.strip():
        return False
    return any(m in diff_text for m in _DIFF_PARSED_MARKERS)


def dotnet_breaking_lines(release_notes: str) -> list[str]:
    """Return the trimmed `.NET … [BREAKING]` lines from the release notes."""
    if not release_notes:
        return []
    return [line.strip() for line in release_notes.splitlines() if _NET_BREAKING_RE.search(line)]


def _distinct_in_order(names) -> list[str]:
    seen: dict[str, None] = {}
    for name in names:
        seen.setdefault(name, None)
    return list(seen)


def added_members(diff_text: str) -> list[str]:
    return _distinct_in_order(_ADDED_MEMBER_RE.findall(diff_text or ""))


def removed_members(diff_text: str) -> list[str]:
    return _distinct_in_order(_REMOVED_MEMBER_RE.findall(diff_text or ""))


def signature_changed_members(diff_text: str) -> list[str]:
    return _distinct_in_order(_SIGNATURE_CHANGED_RE.findall(diff_text or ""))


def has_obsolete_annotation(diff_text: str) -> bool:
    """True if the diff mentions `[Obsolete]` (a deprecation added/changed). Such
    a release is not purely additive — it needs review + a registry entry."""
    return bool(_OBSOLETE_RE.search(diff_text or ""))


def safe_members(names) -> tuple[list[str], int]:
    """Filter member names to plain C# identifiers (no newlines). Returns
    (safe, dropped_count)."""
    names = list(names)
    safe = [n for n in names if n and '\n' not in n and '\r' not in n and _SAFE_MEMBER_RE.match(n)]
    return safe, len(names) - len(safe)


def is_additive(release_notes: str, diff_text: str = "") -> bool:
    """Fail-closed: additive only with a trustworthy diff that shows no removals,
    no signature changes, and no `.NET [BREAKING]` note."""
    return (
        diff_is_trustworthy(diff_text)
        and not dotnet_breaking_lines(release_notes)
        and not removed_members(diff_text)
        and not signature_changed_members(diff_text)
        and not has_obsolete_annotation(diff_text)
    )


def classify(release_notes: str, diff_text: str = "") -> dict:
    """Full verdict for the workflow / scripts."""
    breaking_notes = dotnet_breaking_lines(release_notes)
    removed = removed_members(diff_text)
    sig_changed = signature_changed_members(diff_text)
    obsolete = has_obsolete_annotation(diff_text)
    trustworthy = diff_is_trustworthy(diff_text)
    return {
        "additive": trustworthy and not breaking_notes and not removed and not sig_changed and not obsolete,
        "diff_trustworthy": trustworthy,
        "breaking_notes": breaking_notes,
        "removed": removed,
        "signature_changed": sig_changed,
        "obsolete": obsolete,
        "added": added_members(diff_text),
    }


def combined_diff_text() -> str:
    """Concatenate BOTH diff files the watcher produces (core + workflows), so a
    removal/signature-change in either package counts toward the verdict."""
    parts = []
    for name in ('diff-core.txt', 'diff-workflows.txt'):
        p = pathlib.Path(name)
        if p.is_file():
            parts.append(p.read_text(encoding='utf-8'))
    return '\n'.join(parts)


if __name__ == "__main__":
    # CLI: prints "additive" or "breaking" for the workflow to capture. Reads
    # release-notes.txt + BOTH diff files from CWD (the watcher's artifacts).
    notes = pathlib.Path("release-notes.txt")
    verdict = classify(
        notes.read_text(encoding="utf-8") if notes.is_file() else "",
        combined_diff_text(),
    )
    print("additive" if verdict["additive"] else "breaking")
