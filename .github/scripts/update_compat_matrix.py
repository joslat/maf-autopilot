#!/usr/bin/env python3
"""Adds a new row to docs/compatibility-matrix.md for a new MAF version.

Reads OLD_VERSION, NEW_VERSION from environment variables.
Called by .github/workflows/maf-release-watcher.yml.

Green-on-arrival (2026-06): instead of emitting `>= unknown` placeholders that
the cross-file gate rejects, this script now fills the row DETERMINISTICALLY:

  * Transitive pins (Extensions.AI / .NET / Azure) are carried forward from the
    current top row — correct for additive/patch releases, which don't move the
    floor; a breaking release is reviewed by a human anyway.
  * The Generators-package column is the new version.
  * The Notes cell states the additive-vs-breaking verdict from every validated
    package diff in maf-package-plan.json and summarizes package/repository
    lifecycle transitions without changing the table schema.
  * The "Current tracked version" line and the `last-updated:` header date are
    advanced to the new version / today, which the cross-file consistency gate
    also requires.

Idempotency: if a row for NEW_VERSION already exists, the script does not add a
second row; it still normalizes the single tracking line and header date.
"""
import datetime as dt
import os
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from release_classification import (  # noqa: E402
    classify_from_files,
    lifecycle_event_summary,
    safe_members,
)

old = os.environ.get('OLD_VERSION', 'unknown')
new = os.environ.get('NEW_VERSION', 'unknown')

matrix_path = 'docs/compatibility-matrix.md'
with open(matrix_path, 'r', encoding='utf-8') as f:
    content = f.read()

TRACKED_PATTERN = (
    r'(Current tracked version:\s*\*\*`?)\d+\.\d+(?:\.\d+)?(`?\*\*)'
)
LAST_UPDATED_PATTERN = r'(last-updated:\s*)\d{4}-\d{2}-\d{2}'
TRACKED_LABEL_PATTERN = r'Current tracked version\s*:'
LAST_UPDATED_LABEL_PATTERN = r'last-updated\s*:'

# Metadata is part of the release transaction. Refuse to mutate the matrix when
# either field is missing or duplicated: re.sub() would otherwise silently leave
# stale metadata or update several conflicting sources of truth.
n_tracked_fields = len(re.findall(TRACKED_PATTERN, content))
n_date_fields = len(re.findall(LAST_UPDATED_PATTERN, content))
n_tracked_labels = len(re.findall(TRACKED_LABEL_PATTERN, content, re.IGNORECASE))
n_date_labels = len(re.findall(LAST_UPDATED_LABEL_PATTERN, content, re.IGNORECASE))
metadata_errors = []
if n_tracked_fields != 1 or n_tracked_labels != 1:
    metadata_errors.append(
        "expected exactly one valid 'Current tracked version' field; "
        f"found {n_tracked_fields} valid value(s) across {n_tracked_labels} field(s)"
    )
if n_date_fields != 1 or n_date_labels != 1:
    metadata_errors.append(
        "expected exactly one valid 'last-updated: YYYY-MM-DD' field; "
        f"found {n_date_fields} valid value(s) across {n_date_labels} field(s)"
    )
if metadata_errors:
    print(
        "::error::update_compat_matrix.py cannot update release metadata: "
        + "; ".join(metadata_errors)
        + ". Fix the compatibility-matrix format and re-run."
    )
    sys.exit(1)

row_exists = bool(
    re.search(rf'^\| \*\*{re.escape(new)}\*\* \|', content, re.MULTILINE)
)


def top_row_cells(text: str):
    """Return the cell list of the first `| **X.Y.Z** |` data row, or None.

    Cells are the pipe-separated fields with surrounding whitespace stripped:
    [version, extensions_ai, dotnet, azure, generators, notes].
    """
    for line in text.splitlines():
        if re.match(r'^\|\s*\*\*\d+\.\d+\.\d+[^|]*\*\*\s*\|', line):
            # Drop the leading/trailing empty fields produced by the edge pipes.
            parts = [c.strip() for c in line.split('|')[1:-1]]
            if len(parts) >= 6:
                return parts
            return None
    return None


def build_notes(verdict: dict) -> str:
    # Only identifier-allowlisted member names are embedded — NO freeform upstream
    # text (release-note lines), because this cell flows into CompatibilityTool.cs
    # source via sync_compat_tool.py and must not carry an injection payload.
    transition_parts = [
        f"{event['impact'].title()}: {lifecycle_event_summary(event)}"
        for event in verdict.get('lifecycle_events', [])
    ]
    transition_tail = (
        " Lifecycle transitions: " + "; ".join(transition_parts) + "."
        if transition_parts else ""
    )
    if verdict['additive']:
        added, _ = safe_members(verdict['added'])
        if added:
            shown = ', '.join(f'`{m}`' for m in added[:8])
            more = '' if len(added) <= 8 else f' (+{len(added) - 8} more)'
            tail = f" New members: {shown}{more}."
        else:
            tail = ''
        return (
            f"Additive release — no `.NET … [BREAKING]` changes and no breaking or "
            f"potentially-breaking API rows "
            f"(source-compatible).{tail}{transition_tail} Transitive pins carried from {old}."
        )
    removed, _ = safe_members(verdict['removed'])
    rem = (' Removed: ' + ', '.join(f'`{m}`' for m in removed[:8]) + '.') if removed else ''
    api_counts = []
    if verdict.get('summary_breaking', 0):
        api_counts.append(f"{verdict['summary_breaking']} breaking")
    if verdict.get('summary_potentially_breaking', 0):
        api_counts.append(
            f"{verdict['summary_potentially_breaking']} potentially breaking"
        )
    api_summary = (
        " API summary: " + ", ".join(api_counts) + "." if api_counts else ""
    )
    return (
        f"**Breaking** — review required.{rem}{api_summary} See `guides/maf-{new}-migration-guide.md` "
        f"and the registry entries.{transition_tail} Transitive pins carried from {old} (verify)."
    )


# Update both required metadata fields in memory before doing any write. The
# exact-one preflight above makes these counts deterministic and fail-closed.
today = dt.date.today().isoformat()
content, n_tracked = re.subn(
    TRACKED_PATTERN,
    rf'\g<1>{new}\g<2>',
    content,
)
content, n_date = re.subn(
    LAST_UPDATED_PATTERN,
    rf'\g<1>{today}',
    content,
)

# Recovery/idempotency path: do not require release evidence again merely to
# finish metadata normalization after the version row was already inserted.
if row_exists:
    with open(matrix_path, 'w', encoding='utf-8') as f:
        f.write(content)
    print(
        f"compatibility-matrix.md already has a row for MAF {new}; no duplicate "
        f"added; tracked-version line updated x{n_tracked}; date updated x{n_date}."
    )
    sys.exit(0)


verdict = classify_from_files()

# Carry the transitive pins forward from the current top row. Fail-closed: if the
# table format changed and we can't parse it, refuse to GUESS pins — publishing a
# wrong dependency floor (and syncing it into CompatibilityTool.cs) is worse than
# failing the watcher run loudly.
cells = top_row_cells(content)
if not cells:
    print("::error::update_compat_matrix.py could not parse the top matrix row to carry "
          "pins forward — the table format may have changed. Refusing to guess pins; "
          "fix the doc/parser and re-run.")
    sys.exit(1)
meai, dotnet, azure = cells[1], cells[2], cells[3]

new_row = (
    f"| **{new}** | {meai} | {dotnet} | {azure} | `{new}` | {build_notes(verdict)} |"
)

# Insert the new row above the current first data row (newest-first ordering).
first_row_pattern = r'^(\| \*\*\d+\.\d+\.\d+[^|]*\*\* \|)'
match = re.search(first_row_pattern, content, re.MULTILINE)
if match:
    insertion_point = match.start()
    content = content[:insertion_point] + new_row + '\n' + content[insertion_point:]
else:
    content = content.rstrip() + '\n' + new_row + '\n'

with open(matrix_path, 'w', encoding='utf-8') as f:
    f.write(content)

print(
    f"Updated {matrix_path}: added {'additive' if verdict['additive'] else 'breaking'} "
    f"row for MAF {new}; tracked-version line updated x{n_tracked}; date updated x{n_date}."
)
