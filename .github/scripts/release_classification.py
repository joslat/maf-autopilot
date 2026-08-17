#!/usr/bin/env python3
"""Classify a MAF release as additive vs. breaking from its upstream artifacts.

Shared by the release-watcher scripts (`update_compat_matrix.py`,
`gen_guide_section.py`) and the workflow so the "green-on-arrival" decision is
made in ONE place.

A release is **additive** only when ALL of these hold:
  * every diffable surface in ``maf-package-plan.json`` has a corresponding,
    independently validated report in manifest order;
  * the release notes carry no `.NET … [BREAKING]` entry;
  * the diff removed no members;
  * the diff changed no member SIGNATURES.
  * no validated summary reports breaking OR potentially-breaking API rows.
Anything else is **breaking** (or unverifiable → treated as breaking). A false
"additive" is the dangerous direction — it would let an unreviewed breaking
release go green — so the verdict is FAIL-CLOSED: missing/garbled evidence never
yields additive.

Design choices that keep this safe:
  1. `.NET`-scoped, order-independent notes match. Python-only `[BREAKING]` lines
     don't affect the .NET package this toolkit tracks. We match a line that
     mentions BOTH `.NET` and `[BREAKING]` in either order.
  2. Removals, signature changes, and every upstream summary row classified as
     breaking or potentially breaking count as review-required, even with no
     `[BREAKING]` note. Summary counts cover base-type, sealed/virtual, enum,
     generic-constraint, and other source/binary compatibility shapes that are
     intentionally broader than the registry extractor's member recipes.
  3. Every targeted manifest surface is considered; a break in a later package can never
     be hidden by a clean Core diff.
  4. Checked-in lifecycle events participate in the verdict. Package splits are
     breaking; repository externalizations are visible but informational.
"""
from __future__ import annotations

import argparse
import pathlib
import re
import sys

from maf_package_evidence import (
    PLAN_FILENAME,
    RESULTS_FILENAME,
    PackageEvidenceError,
    load_evidence_from_files,
    load_package_plan,
)

# A ".NET … [BREAKING]" marker on a line, in EITHER order (`.NET: [BREAKING] …`
# or `… [BREAKING] … .NET`). Excludes `Python: [BREAKING] …` (no `.NET` token).
_BREAKING_MARKER = r"\[BREAKING(?:\s+CHANGES?)?\]"
_NET_BREAKING_RE = re.compile(
    rf'(?:\.NET[^\n]*{_BREAKING_MARKER}|{_BREAKING_MARKER}[^\n]*\.NET)',
    re.IGNORECASE,
)

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
# Matches `[Obsolete]`, `[Obsolete("msg")]`, and `[ObsoleteAttribute]` — the real
# dotnet-inspect form carries a message, so a bare `\[Obsolete\]` would miss it.
# Kept in lockstep with DiffPackageTool's parser (`\[Obsolete(?:Attribute)?\b`).
_OBSOLETE_RE = re.compile(r'\[Obsolete(?:Attribute)?\b', re.IGNORECASE)

# dotnet-inspect prints breaking, potentially-breaking, and additive counts in
# its Summary row. Treat both non-additive categories as authoritative. The
# watcher's stricter evidence validator reconciles these counts with emitted
# rows before production classification runs.
_SUMMARY_BREAKING_RE = re.compile(r'(\d+)\s+breaking', re.IGNORECASE)
_SUMMARY_POTENTIALLY_BREAKING_RE = re.compile(
    r'(\d+)\s+potentially\s+breaking', re.IGNORECASE
)

# A member/type name is only safe to echo into generated SOURCE or docs if it is
# a plain C# identifier. Names come from untrusted upstream (TA-1). `\A…\Z` (not
# `^…$`) so a trailing newline cannot slip through ($ matches before a final \n).
_SAFE_MEMBER_RE = re.compile(r'\A[A-Za-z_][A-Za-z0-9_]*\Z')

# Markers whose presence proves the diff actually COMPLETED parsing — content
# bullets, or the explicit no-changes banner. We deliberately do NOT trust the
# report HEADER (`# API Diff: …`) or the invocation token `diff --package`: the
# header is the FIRST line, so a header-printed-then-truncated diff would carry it
# while the breaking bullets never arrived — that would let a truncated breaking
# diff masquerade as additive (fail-open). Every COMPLETE diff carries at least
# one of these content markers.
_CHANGE_BULLET_MARKERS = (
    'was added',
    'was removed',
    'signature changed',
    'Type kind changed',
    'Type was sealed',
    'Type was unsealed',
    'was made abstract',
    'was made non-abstract',
    'Base type changed',
    'Generic parameter count changed',
    'Generic parameter ',
    'is no longer virtual',
    'Enum member ',
)
_DIFF_PARSED_MARKERS = _CHANGE_BULLET_MARKERS + ('No API changes detected',)


# Change-bullet markers (a subset of _DIFF_PARSED_MARKERS). A complete
# change-bearing dotnet-inspect report also contains a `**Summary:**` row. A diff
# missing that row is malformed/truncated and must not be trusted (WM-11). The
# "No API changes detected" banner is a complete report with no Summary.
RELEASE_NOTES_UNAVAILABLE_MARKER = "Release notes unavailable for automated classification."


def diff_is_trustworthy(diff_text: str) -> bool:
    """An ADDITIVE verdict may only rest on a diff we can read. Empty text, a
    NuGet error, or a stack trace shows none of the dotnet-inspect markers and is
    NOT trustworthy → the caller must treat the release as breaking/unverifiable.

    WM-11: a diff that carries change bullets (`was added`/`was removed`/`signature
    changed`) but LACKS its `**Summary:**` row is malformed or truncated even if the
    process exited cleanly. Treat it as untrustworthy so that path also fails closed
    to breaking."""
    if not diff_text or not diff_text.strip():
        return False
    if not any(m in diff_text for m in _DIFF_PARSED_MARKERS):
        return False
    has_change_bullets = any(m in diff_text for m in _CHANGE_BULLET_MARKERS)
    if has_change_bullets and 'Summary:' not in diff_text:
        return False
    return True


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


def summary_breaking_count(diff_text: str) -> int:
    """Sum `N breaking` across manifest-ordered package Summary rows."""
    counts = [int(n) for n in _SUMMARY_BREAKING_RE.findall(diff_text or "")]
    return sum(counts)


def summary_potentially_breaking_count(diff_text: str) -> int:
    """Sum `N potentially breaking` across package Summary rows."""
    counts = [
        int(n) for n in _SUMMARY_POTENTIALLY_BREAKING_RE.findall(diff_text or "")
    ]
    return sum(counts)


def unrepresented_structural_rows(diff_text: str) -> list[str]:
    """Review rows that registry-extract cannot turn into member recipes.

    The extractor intentionally emits drafts only for removed/signature-changed
    or obsolete members. Base-type, sealed/virtual, enum, generic, and other
    structural rows therefore need an explicit package review draft even when a
    different row from the same package produced a normal registry entry.
    """
    rows: list[str] = []
    review_section = False
    for line in (diff_text or "").splitlines():
        if line in {"## Breaking Changes", "## Potentially Breaking Changes"}:
            review_section = True
            continue
        if line.startswith("## "):
            review_section = False
            continue
        if not review_section or not line.startswith("- "):
            continue
        if (
            "was removed" in line
            or "signature changed" in line
            or _OBSOLETE_RE.search(line)
        ):
            continue
        rows.append(line)
    return rows


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
        and summary_breaking_count(diff_text) == 0
        and summary_potentially_breaking_count(diff_text) == 0
    )


def classify(release_notes: str, diff_text: str = "") -> dict:
    """Full verdict for the workflow / scripts."""
    breaking_notes = dotnet_breaking_lines(release_notes)
    removed = removed_members(diff_text)
    sig_changed = signature_changed_members(diff_text)
    obsolete = has_obsolete_annotation(diff_text)
    summary_breaking = summary_breaking_count(diff_text)
    summary_potentially_breaking = summary_potentially_breaking_count(diff_text)
    structural_rows = unrepresented_structural_rows(diff_text)
    trustworthy = diff_is_trustworthy(diff_text)
    return {
        "additive": (trustworthy and not breaking_notes and not removed and not sig_changed
                     and not obsolete and summary_breaking == 0
                     and summary_potentially_breaking == 0),
        "diff_trustworthy": trustworthy,
        "breaking_notes": breaking_notes,
        "removed": removed,
        "signature_changed": sig_changed,
        "obsolete": obsolete,
        "summary_breaking": summary_breaking,
        "summary_potentially_breaking": summary_potentially_breaking,
        "unrepresented_structural_rows": structural_rows,
        "added": added_members(diff_text),
    }


def lifecycle_event_summary(event: dict) -> str:
    """Return a bounded, deterministic human summary for a validated event."""
    kind = str(event.get("kind", "lifecycle event")).replace("_", " ")
    if event.get("kind") == "repository_externalization":
        source = event.get("source_repository", "unknown repository")
        target = event.get("target_repository", "unknown repository")
        suffix = "; package IDs unchanged" if event.get("same_package_ids") else ""
        return f"{kind}: {source} → {target}{suffix}"

    def packages(field: str) -> str:
        refs = event.get(field, [])
        return ", ".join(
            str(ref.get("package", "unknown"))
            for ref in refs
            if isinstance(ref, dict)
        ) or "none"

    return f"{kind}: {packages('source_packages')} → {packages('target_packages')}"


def classify_from_evidence(release_notes: str, evidence: dict) -> dict:
    """Classify a release using strict per-surface evidence and lifecycle data."""
    verdict = classify(release_notes, evidence["combined_diff_text"])
    breaking_events = evidence["breaking_lifecycle_events"]
    evidence_errors = list(evidence["document_errors"])
    for surface in evidence["surfaces"]:
        if surface["errors"]:
            evidence_errors.append(
                f"{surface['slug']}: " + "; ".join(surface["errors"])
            )

    verdict["diff_trustworthy"] = evidence["trustworthy"]
    verdict["lifecycle_events"] = evidence["lifecycle_events"]
    verdict["breaking_lifecycle_events"] = breaking_events
    verdict["informational_lifecycle_events"] = evidence[
        "informational_lifecycle_events"
    ]
    verdict["evidence_errors"] = evidence_errors
    verdict["surface_evidence"] = evidence["surfaces"]
    verdict["additive"] = bool(
        evidence["trustworthy"]
        and not verdict["breaking_notes"]
        and not verdict["removed"]
        and not verdict["signature_changed"]
        and not verdict["obsolete"]
        and verdict["summary_breaking"] == 0
        and verdict["summary_potentially_breaking"] == 0
        and not breaking_events
    )
    return verdict


def classify_from_files(base_dir: pathlib.Path | str = ".") -> dict:
    """Read watcher artifacts and classify fail-closed on any missing contract."""
    root = pathlib.Path(base_dir)
    notes_path = root / "release-notes.txt"
    notes_error: str | None = None
    try:
        notes = notes_path.read_text(encoding="utf-8")
    except (OSError, UnicodeDecodeError) as exc:
        notes = ""
        notes_error = f"release-note evidence is missing or unreadable: {exc}"
    else:
        if not notes.strip():
            notes_error = "release-note evidence is empty"
        elif RELEASE_NOTES_UNAVAILABLE_MARKER in notes:
            notes_error = "upstream release notes were unavailable"
    try:
        _, evidence = load_evidence_from_files(
            root / PLAN_FILENAME,
            root / RESULTS_FILENAME,
            root,
        )
    except (OSError, PackageEvidenceError) as exc:
        verdict = classify(notes, "")
        verdict.update(
            {
                "additive": False,
                "diff_trustworthy": False,
                "lifecycle_events": [],
                "breaking_lifecycle_events": [],
                "informational_lifecycle_events": [],
                "evidence_errors": [str(exc)],
                "surface_evidence": [],
            }
        )
        if notes_error:
            verdict["evidence_errors"].append(notes_error)
        verdict["release_notes_trustworthy"] = notes_error is None
        return verdict
    verdict = classify_from_evidence(notes, evidence)
    verdict["release_notes_trustworthy"] = notes_error is None
    if notes_error:
        verdict["additive"] = False
        verdict["evidence_errors"].append(notes_error)
    return verdict


def combined_diff_text(base_dir: pathlib.Path | str = ".") -> str:
    """Return only independently validated, manifest-ordered package diffs.

    Missing plan/results evidence returns empty text. This compatibility helper
    is intentionally fail-closed; production callers should use
    :func:`classify_from_files` so missing surfaces remain explicit.
    """
    root = pathlib.Path(base_dir)
    try:
        _, evidence = load_evidence_from_files(
            root / PLAN_FILENAME,
            root / RESULTS_FILENAME,
            root,
        )
    except (OSError, PackageEvidenceError):
        return ""
    return evidence["combined_diff_text"]


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--require-complete-evidence",
        action="store_true",
        help=(
            "Exit non-zero unless package diffs and release notes are both "
            "complete and trustworthy."
        ),
    )
    args = parser.parse_args(argv)
    verdict = classify_from_files()
    if args.require_complete_evidence:
        extraction_error: str | None = None
        try:
            from extract_registry_from_plan import (
                REGISTRY_EXTRACTION_RESULTS_FILENAME,
                load_registry_extraction_results,
            )

            plan = load_package_plan(pathlib.Path(PLAN_FILENAME))
            load_registry_extraction_results(
                pathlib.Path(REGISTRY_EXTRACTION_RESULTS_FILENAME), plan
            )
        except (OSError, PackageEvidenceError) as exc:
            extraction_error = str(exc)
        complete = bool(
            verdict.get("diff_trustworthy")
            and verdict.get("release_notes_trustworthy")
            and extraction_error is None
        )
        if not complete:
            print(
                "::error::Release evidence is incomplete; refusing to advance "
                ".maf-version or create a watcher commit.",
                file=sys.stderr,
            )
            for error in verdict.get("evidence_errors", []):
                print(f"- {error}", file=sys.stderr)
            if extraction_error:
                print(f"- {extraction_error}", file=sys.stderr)
            return 1
        print("complete")
        return 0

    # Prints the single token captured by the workflow classification step.
    print("additive" if verdict["additive"] else "breaking")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
