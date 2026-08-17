#!/usr/bin/env python3
"""Guarantee that a BREAKING release leaves the watcher PR RED.

The keep-breaking-red gate (verify_crossfile_consistency.check_unfilled_current_drafts)
only turns a PR red when there is >=1 UNFILLED registry draft for the current
version. Drafts now come from the same validated package artifacts as the
classifier. They can still be empty for a behavioral release-note break, a
package lifecycle transition, failed/unverifiable evidence, dedupe stripping
every entry, or a parser gap. Without a sentinel such a genuinely breaking
release could otherwise sail through GREEN while the PR says "breaking".

This guard closes that gap. It appends a general review draft when a breaking
release has no current-version entry, and appends one review draft for every
breaking lifecycle event that is not explicitly covered by a current-version
entry for one of that event's source/target packages. The maintainer fills or
replaces each draft before merge. Run AFTER registry-extract + classify, BEFORE
commit.

Reads NEW_VERSION and VERDICT from the environment.
"""
import os
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from extract_registry_from_plan import (  # noqa: E402
    REGISTRY_EXTRACTION_RESULTS_FILENAME,
    load_registry_extraction_results,
)
from dedupe_registry_entries import split_blocks  # noqa: E402
from maf_package_evidence import (  # noqa: E402
    RESULTS_FILENAME,
    PackageEvidenceError,
    load_evidence_from_files,
    load_package_plan,
)
from release_classification import classify  # noqa: E402

REGISTRY = Path(".github/skills/maf-obsolete-api-registry/registry.yaml")
PLAN = Path("maf-package-plan.json")
EXTRACTION_RESULTS = Path(REGISTRY_EXTRACTION_RESULTS_FILENAME)


def _release_id_digits(version: str) -> str | None:
    """Return the full major+minor ID key (1.100.0 -> 1100), never truncate."""
    match = re.fullmatch(r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)", version)
    return f"{match.group(1)}{match.group(2)}" if match else None


def _current_entry_packages(text: str, version: str) -> set[str]:
    """Return package IDs from current-version entry blocks."""
    packages: set[str] = set()
    blocks = re.split(r"(?=^\s*-\s+id:\s*)", text, flags=re.MULTILINE)
    version_re = re.compile(
        rf'^\s*version_introduced:\s*["\']?{re.escape(version)}["\']?\s*(?:#.*)?$',
        re.MULTILINE,
    )
    package_re = re.compile(r"^\s*package:\s*([A-Za-z0-9_.-]+)\s*$", re.MULTILINE)
    for block in blocks:
        if not version_re.search(block):
            continue
        match = package_re.search(block)
        if match:
            packages.add(match.group(1).casefold())
    return packages


def _current_entry_index(text: str, version: str) -> dict[str, str]:
    """Index current-cycle entry IDs to packages, case-insensitively."""
    version_re = re.compile(
        rf'^\s*version_introduced:\s*["\']?{re.escape(version)}["\']?\s*(?:#.*)?$',
        re.MULTILINE,
    )
    package_re = re.compile(
        r"^\s*package:\s*([A-Za-z0-9_.-]+)\s*$", re.MULTILINE
    )
    entries: dict[str, str] = {}
    for entry_id, block in split_blocks(text):
        if not version_re.search(block):
            continue
        package_match = package_re.search(block)
        if package_match:
            entries[entry_id.casefold()] = package_match.group(1).casefold()
    return entries


def _current_review_surface_slugs(text: str, version: str) -> set[str]:
    """Return surface slugs explicitly named by current REVIEW drafts."""
    covered: set[str] = set()
    blocks = re.split(r"(?=^\s*-\s+id:\s*)", text, flags=re.MULTILINE)
    version_re = re.compile(
        rf'^\s*version_introduced:\s*["\']?{re.escape(version)}["\']?\s*(?:#.*)?$',
        re.MULTILINE,
    )
    review_id_re = re.compile(
        r"^\s*-\s+id:\s*MAF[0-9]+-REVIEW-[0-9]+\s*$",
        re.MULTILINE | re.IGNORECASE,
    )
    surface_re = re.compile(r"\bsurface\s+([a-z0-9]+(?:-[a-z0-9]+)*)\b", re.IGNORECASE)
    for block in blocks:
        if not version_re.search(block) or not review_id_re.search(block):
            continue
        covered.update(match.casefold() for match in surface_re.findall(block))
    return covered


def _next_review_id(text: str, release_digits: str, start: int) -> tuple[str, int]:
    number = start
    while True:
        candidate = f"MAF{release_digits}-REVIEW-{number:03d}"
        if not re.search(
            rf"^\s*-\s+id:\s*{re.escape(candidate)}\s*$",
            text,
            re.MULTILINE | re.IGNORECASE,
        ):
            return candidate, number + 1
        number += 1


def _review_sentinel(
    sentinel_id: str,
    package: str,
    version: str,
    reason: str,
) -> str:
    safe_reason = " ".join(reason.split())[:500]
    return (
        "\n# === Keep-breaking-red review sentinel auto-added by release-watcher. "
        "Fill or replace before merging. ===\n"
        f"  - id: {sentinel_id}\n"
        f"    package: {package}\n"
        f'    version_introduced: "{version}"\n'
        "    type: UNKNOWN\n"
        "    method: UNKNOWN\n"
        "    obsolete_signature: TODO\n"
        "    replacement_signature: TODO\n"
        "    argument_order_change: false\n"
        "    fix_description: >-\n"
        f"      TODO — {safe_reason} Review the release notes and validated package\n"
        "      artifacts, document the change, then replace this review sentinel.\n"
        "    example_before: |\n"
        "      // TODO — show a real call site that breaks\n"
        "    example_after: |\n"
        "      // TODO — show the canonical replacement\n"
        "    cs_warning: TODO\n"
        '    guide_section: "N/A"\n'
        "    dotnet_inspect_detectable: false\n"
        "    notes: >-\n"
        f"      Review sentinel: {safe_reason}\n"
        f'    applies_to_codebases: "pre-{version}"\n'
    )


def main() -> int:
    verdict = os.environ.get("VERDICT", "").strip()
    new = os.environ.get("NEW_VERSION", "").strip()

    if verdict != "breaking":
        print(f"Verdict is '{verdict or '(unset)'}' — no keep-breaking-red guard needed.")
        return 0
    release_digits = _release_id_digits(new)
    if release_digits is None:
        print("::error::NEW_VERSION must be a stable X.Y.Z version; cannot guard the breaking gate.")
        return 1
    if not REGISTRY.is_file():
        print(f"::error::{REGISTRY} not found; cannot guarantee the breaking gate.")
        return 1

    # Regex (not PyYAML) so the watcher job needs no extra Python deps — matching
    # the sibling dedupe_registry_entries.py approach. A `version_introduced:` line
    # for the new version (quoted or bare, optional trailing comment) means the
    # registry already carries an entry for this release.
    text = REGISTRY.read_text(encoding="utf-8")
    has_current = bool(re.search(
        rf'^\s*version_introduced:\s*["\']?{re.escape(new)}["\']?\s*(?:#.*)?$',
        text, re.MULTILINE))
    current_packages = _current_entry_packages(text, new)
    current_entries = _current_entry_index(text, new)
    covered_review_surfaces = _current_review_surface_slugs(text, new)

    breaking_events: list[dict] = []
    review_surfaces: list[tuple[dict, dict, dict]] = []
    if PLAN.exists():
        try:
            plan = load_package_plan(PLAN)
        except (OSError, PackageEvidenceError) as exc:
            print(f"::error::Cannot validate {PLAN}: {exc}")
            return 1
        if plan["release"]["new_version"] != new:
            print(
                f"::error::{PLAN} targets {plan['release']['new_version']}, "
                f"not NEW_VERSION {new}."
            )
            return 1
        try:
            _, evidence = load_evidence_from_files(
                PLAN, Path(RESULTS_FILENAME), Path(".")
            )
            extraction = load_registry_extraction_results(
                EXTRACTION_RESULTS, plan
            )
        except (OSError, PackageEvidenceError) as exc:
            print(
                "::error::Cannot prove per-package registry extraction coverage: "
                f"{exc}"
            )
            return 1
        for surface, extraction_row in zip(
            evidence["surfaces"], extraction["surfaces"]
        ):
            if surface["status"] != "diffable" or not surface["trusted"]:
                continue
            surface_verdict = classify("", surface.get("text", ""))
            requires_review = bool(
                surface_verdict["summary_breaking"]
                or surface_verdict["summary_potentially_breaking"]
                or surface_verdict["obsolete"]
                or surface_verdict["removed"]
                or surface_verdict["signature_changed"]
            )
            if requires_review:
                review_surfaces.append(
                    (surface, extraction_row, surface_verdict)
                )
        breaking_events = [
            event for event in plan["lifecycle_events"]
            if event["impact"] == "breaking"
        ]

    additions: list[str] = []
    next_number = 1
    id_source = text
    for surface, extraction_row, surface_verdict in review_surfaces:
        extraction_failed = extraction_row["extraction_status"] == "failed"
        slug = surface["slug"]
        structural_rows = surface_verdict["unrepresented_structural_rows"]
        expected_ids = extraction_row["generated_entry_ids"]
        expected_package = surface["package"].casefold()
        missing_generated_ids = [
            entry_id
            for entry_id in expected_ids
            if current_entries.get(entry_id.casefold()) != expected_package
        ]
        explicit_review_covered = slug.casefold() in covered_review_surfaces
        if explicit_review_covered:
            continue
        if (
            not extraction_failed
            and not structural_rows
            and expected_ids
            and not missing_generated_ids
        ):
            continue
        sentinel_id, next_number = _next_review_id(
            id_source, release_digits, next_number
        )
        counts = []
        if surface_verdict["summary_breaking"]:
            counts.append(
                f"{surface_verdict['summary_breaking']} breaking"
            )
        if surface_verdict["summary_potentially_breaking"]:
            counts.append(
                f"{surface_verdict['summary_potentially_breaking']} potentially breaking"
            )
        count_summary = ", ".join(counts) or "review-required API"
        if extraction_failed:
            detail = "; ".join(extraction_row["errors"])
            reason = (
                f"Registry extraction failed for review-relevant surface "
                f"{slug} ({count_summary}): {detail}."
            )
        elif structural_rows:
            reason = (
                f"Review-relevant surface {slug} contains {len(structural_rows)} "
                f"structural breaking/potentially-breaking row(s) ({count_summary}) "
                "that registry-extract "
                "does not represent with removed/signature recipes."
            )
        elif missing_generated_ids:
            reason = (
                f"Registry append did not preserve {len(missing_generated_ids)} of "
                f"{len(expected_ids)} draft ID(s) generated for review-relevant "
                f"surface {slug} ({count_summary}): "
                + ", ".join(missing_generated_ids[:10])
                + (" ..." if len(missing_generated_ids) > 10 else "")
                + "."
            )
        else:
            reason = (
                f"Review-relevant surface {slug} has {count_summary}, "
                "but registry-extract generated no concrete draft ID for that "
                "surface."
            )
        addition = _review_sentinel(
            sentinel_id, surface["package"], new, reason
        )
        additions.append(addition)
        id_source += addition

    for event in breaking_events:
        package_refs = event["source_packages"] + event["target_packages"]
        relevant_packages = {
            ref["package"].casefold() for ref in package_refs
        }
        if current_packages.intersection(relevant_packages):
            continue
        sentinel_id, next_number = _next_review_id(
            id_source, release_digits, next_number
        )
        package = (
            package_refs[0]["package"] if package_refs else "Microsoft.Agents.AI"
        )
        reason = (
            f"Breaking lifecycle event {event['id']}: {event['reason']}"
        )
        addition = _review_sentinel(sentinel_id, package, new, reason)
        additions.append(addition)
        id_source += addition

    if not has_current and not additions:
        sentinel_id, _ = _next_review_id(id_source, release_digits, next_number)
        additions.append(_review_sentinel(
            sentinel_id,
            "Microsoft.Agents.AI",
            new,
            (
                f"Validated evidence for {new} produced no current-version registry "
                "draft, but the release was classified breaking."
            ),
        ))

    if not additions:
        print(
            f"Registry entries for {new} explicitly cover every breaking lifecycle "
            "event; the keep-breaking-red gate will fire normally."
        )
        return 0

    with REGISTRY.open("a", encoding="utf-8") as f:
        f.write("".join(additions))
    print(
        f"::warning::Appended {len(additions)} unresolved review draft(s) for "
        f"breaking release {new}; fill or replace them before merge."
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
