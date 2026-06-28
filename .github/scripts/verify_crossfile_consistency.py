#!/usr/bin/env python3
"""
Cross-file consistency checks for MAF release artifacts.

Runs AFTER `maf-autopilot verify-registry` (which checks registry.yaml
entry-internal consistency). This script checks consistency ACROSS the
files the release-watcher touches.

Findings discovered 2026-05-17 on PR #26 and PR #36 motivated these checks:
- Bot ADDED a new `**1.6.1**` row to compat-matrix instead of UPDATING the
  watcher-inserted placeholder row → duplicate version rows.
- "Version Tracking" section still said the old version after a release.
- Bot left the `last-updated: YYYY-MM-DD` header date stale.
- Bot invented a `guide_section: "1"` reference that doesn't exist.

Checks performed:
1. docs/compatibility-matrix.md has no DUPLICATE `**X.Y.Z**` rows.
2. The TOP data row in the compat-matrix table matches `.maf-version`.
3. "Version Tracking" section's "Current tracked version: **X.Y.Z**" line
   matches `.maf-version`.
4. `guides/maf-{VERSION}-migration-guide.md` exists for `.maf-version`.
5. `last-updated: YYYY-MM-DD` header date is within the last 30 days
   (catches "bot ignored the date" cases without being timezone-strict).
6. Every registry entry's `guide_section` value is either "N/A" or a
   section ID that exists as a heading in the 1.3.0 migration guide.

Exits 0 if all consistent, 1 if any check fails. Prints findings to stdout.
"""
from __future__ import annotations

import datetime as dt
import re
import sys
from pathlib import Path

import yaml

ROOT = Path.cwd()
MAF_VERSION_FILE = ROOT / ".maf-version"
COMPAT_MATRIX = ROOT / "docs" / "compatibility-matrix.md"
GUIDES_DIR = ROOT / "guides"
REGISTRY = ROOT / ".github" / "skills" / "maf-obsolete-api-registry" / "registry.yaml"
MAX_LAST_UPDATED_AGE_DAYS = 30

# A "version-stem" row in compat-matrix table starts with: | **X.Y.Z** | ...
# Allow optional leading whitespace and a 2- or 3-part version.
VERSION_ROW_RE = re.compile(
    r'^\s*\|\s*\*\*(\d+\.\d+(?:\.\d+)?)\*\*\s*\|', re.MULTILINE
)
CURRENT_TRACKED_RE = re.compile(
    r'Current tracked version:\s*\*\*`?(\d+\.\d+(?:\.\d+)?)`?\*\*',
    re.IGNORECASE,
)
LAST_UPDATED_RE = re.compile(
    r'last-updated:\s*(\d{4}-\d{2}-\d{2})', re.IGNORECASE
)
# Section headings in migration guides — match "## 1." "### 2.3." "#### 1.2.3." etc.
GUIDE_SECTION_HEADING_RE = re.compile(
    r'^##+\s+(\d+(?:\.\d+)*)\.', re.MULTILINE
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


def last_updated_date(text: str) -> dt.date | None:
    """Return the date in the 'last-updated: YYYY-MM-DD' header comment."""
    m = LAST_UPDATED_RE.search(text)
    if not m:
        return None
    try:
        return dt.date.fromisoformat(m.group(1))
    except ValueError:
        return None


def collect_guide_sections(guide_path: Path) -> set[str]:
    """Return the set of section IDs (e.g. '1', '2.3') from a migration guide."""
    if not guide_path.is_file():
        return set()
    text = guide_path.read_text(encoding="utf-8")
    return set(GUIDE_SECTION_HEADING_RE.findall(text))


def collect_all_guide_sections(guides_dir: Path) -> set[str]:
    """Union of section IDs across EVERY per-version migration guide.

    The watcher now emits per-version guides (1.4.0 / 1.5.0 / …). A guide_section
    that legitimately points at a section in a newer guide must not be flagged
    'invented' just because it's absent from the 1.3.0 guide (task 4.2).
    """
    sections: set[str] = set()
    if not guides_dir.is_dir():
        return sections
    for guide in guides_dir.glob("maf-*-migration-guide.md"):
        if guide.name == "maf-current-migration-guide.md":
            continue  # cumulative file is a regenerated superset of the others
        sections |= collect_guide_sections(guide)
    return sections


# Watcher placeholder cells the fill bot must replace before merge (task 4.4).
MATRIX_PLACEHOLDERS = (">= unknown", "Auto-detected — verify")


def top_row_line(text: str) -> str | None:
    """Return the full markdown line of the top `**X.Y.Z**` data row, or None."""
    for line in text.splitlines():
        if VERSION_ROW_RE.search(line):
            return line
    return None


def unreplaced_placeholders(top_line: str | None) -> list[str]:
    """Watcher placeholders still present in the current (top) matrix row."""
    if not top_line:
        return []
    return [p for p in MATRIX_PLACEHOLDERS if p in top_line]


def check_invented_guide_sections(
    registry_path: Path,
    valid_sections: set[str],
    current_version: str,
) -> list[str]:
    """For each registry entry from the CURRENT release cycle, confirm its
    `guide_section` value is either 'N/A' or a section ID present in the
    1.3.0 migration guide.

    Scoped to the current cycle (entries with id `MAF{digits}-...` where
    digits == current_version stripped of dots) so legacy tech debt in
    older entries doesn't block PRs touching the registry. Legacy
    cleanup is a separate concern, handled by ad-hoc cleanup PRs."""
    if not registry_path.is_file():
        return [f"{registry_path} not found."]

    try:
        data = yaml.safe_load(registry_path.read_text(encoding="utf-8")) or {}
    except yaml.YAMLError as exc:
        return [f"registry.yaml is not valid YAML: {exc}"]

    findings: list[str] = []
    scoped_count = 0
    for entry in data.get("entries", []):
        eid = entry.get("id", "<missing id>")
        # Scope by the version_introduced FIELD, not an id-prefix guess: the
        # extractor caps the id digit-segment at 3 chars (1.11.1 -> "MAF111-",
        # NOT "MAF1111-"), so a prefix built from the full dotted version never
        # matched for 1.10+/1.11+ and this check was silently dead.
        if str(entry.get("version_introduced", "")).strip() != current_version:
            continue
        scoped_count += 1
        gs = entry.get("guide_section")
        if gs is None or gs == "":
            continue
        gs_str = str(gs).strip()
        if gs_str.upper() == "N/A":
            continue
        if gs_str.upper() in ("TODO", "TBD"):
            findings.append(
                f"{eid}: guide_section is '{gs_str}' — must be a real section "
                f"ID from one of the migration guides (guides/maf-*-migration-"
                f"guide.md) OR the literal 'N/A'."
            )
            continue
        if gs_str not in valid_sections:
            findings.append(
                f"{eid}: guide_section '{gs_str}' is not a section heading in "
                f"any migration guide (guides/maf-*-migration-guide.md). Either "
                f"use a real section ID from one of those guides, or use 'N/A' "
                f"(when the change touches a surface that didn't exist yet). "
                f"NEVER invent section numbers."
            )
    print(
        f"Validated guide_section on {scoped_count} entries introduced in "
        f"{current_version} (current release cycle scope)."
    )
    return findings


_DRAFT_PLACEHOLDER = re.compile(r'^\s*(TODO|TBD|XXX)\b', re.IGNORECASE)
_TODO_EXAMPLE_RE = re.compile(r'(?m)^\s*//\s*TODO\b', re.IGNORECASE)


def _looks_unfilled(value) -> bool:
    if value is None:
        return True
    s = str(value).strip()
    return s == "" or bool(_DRAFT_PLACEHOLDER.match(s))


def _is_todo_example(value) -> bool:
    """True if an example_* field is still the extractor's `// TODO …` stub."""
    return value is not None and bool(_TODO_EXAMPLE_RE.search(str(value)))


def check_unfilled_current_drafts(registry_path: Path, current_version: str) -> list[str]:
    """The "keep breaking red" gate for the green-on-arrival watcher.

    A release the watcher classifies as ADDITIVE produces no registry entries,
    so this finds nothing and the scaffold PR is green. A BREAKING release
    produces draft entries (member removals / signature changes) whose migration
    text a human must write; until they do, the entry's core fields stay TODO
    placeholders and this gate fails the PR.

    Scoped to entries whose `version_introduced` equals the current release
    (`.maf-version`) so the intentional historical TODO drafts from older cycles
    never block an unrelated PR."""
    if not registry_path.is_file():
        return [f"{registry_path} not found."]
    try:
        data = yaml.safe_load(registry_path.read_text(encoding="utf-8")) or {}
    except yaml.YAMLError as exc:
        return [f"registry.yaml is not valid YAML: {exc}"]

    findings: list[str] = []
    checked = 0
    for entry in data.get("entries", []):
        if str(entry.get("version_introduced", "")).strip() != current_version:
            continue
        checked += 1
        # fix_description is the field a human MUST write (the extractor can
        # auto-fill the signatures of a SignatureChanged entry, so an all-three
        # AND would miss it). Fire if the migration prose OR the example stubs
        # are still the extractor's TODO placeholders.
        if (
            _looks_unfilled(entry.get("fix_description"))
            or _is_todo_example(entry.get("example_before"))
            or _is_todo_example(entry.get("example_after"))
        ):
            findings.append(
                f"{entry.get('id', '<missing id>')}: registry draft for the current "
                f"release ({current_version}) is unfilled — a breaking release's entries "
                f"need a human-written migration. Fill fix_description and the before/after "
                f"examples (or, if this release is genuinely additive, the watcher should "
                f"not have created the entry)."
            )
    print(
        f"Checked {checked} registry entr(y/ies) introduced in {current_version} "
        f"for unfilled drafts (keep-breaking-red gate)."
    )
    return findings


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

        # Task 4.4 — the watcher inserts placeholder cells (`>= unknown`,
        # `Auto-detected — verify…`) that the fill bot must replace. Nothing
        # else verified they were replaced, so a literal placeholder could reach
        # main and silently defeat the matrix. Fail if the current (top) row
        # still carries them.
        leftover = unreplaced_placeholders(top_row_line(text))
        if leftover:
            findings.append(
                f"compatibility-matrix.md top row still contains watcher "
                f"placeholder(s) {leftover} — replace `>= unknown` with the real "
                f"minimum versions and the `Auto-detected — verify…` note with a "
                f"real note before merging."
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

        last_upd = last_updated_date(text)
        if last_upd is None:
            print(
                "Note: no 'last-updated: YYYY-MM-DD' header found in "
                "compatibility-matrix.md — skipping that check."
            )
        else:
            today = dt.date.today()
            age = (today - last_upd).days
            if age > MAX_LAST_UPDATED_AGE_DAYS:
                # Task 4.3 — WARN, do not fail. MAF's release cadence is irregular;
                # a structurally-correct release must not be blocked just because
                # the previous matrix touch was >30d ago (the same fill cycle fixes
                # the date). Surfaced as a warning annotation, not a blocking finding.
                print(
                    f"::warning::compatibility-matrix.md 'last-updated:' date is "
                    f"{last_upd.isoformat()} — {age} days old (max {MAX_LAST_UPDATED_AGE_DAYS}). "
                    f"Consider updating it to {today.isoformat()}."
                )

    guide_file = GUIDES_DIR / f"maf-{current}-migration-guide.md"
    if not guide_file.is_file():
        findings.append(
            f"missing migration guide: {guide_file.relative_to(ROOT)}"
        )

    # Check for invented guide_section values in registry entries from
    # the CURRENT release cycle (older entries' TODOs are tech debt to
    # be cleaned up in dedicated PRs, not blockers for new fills).
    valid_sections = collect_all_guide_sections(GUIDES_DIR)
    if not valid_sections:
        print(
            "Note: could not extract section IDs from any guides/maf-*-migration-"
            "guide.md — skipping guide_section validity check."
        )
    else:
        print(f"Guide section IDs available (all per-version guides): {sorted(valid_sections)}")
        section_findings = check_invented_guide_sections(
            REGISTRY, valid_sections, current
        )
        findings.extend(section_findings)

    # Keep-breaking-red gate — unfilled drafts for the CURRENT release fail the
    # PR (an additive release creates none, so it stays green).
    findings.extend(check_unfilled_current_drafts(REGISTRY, current))

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
