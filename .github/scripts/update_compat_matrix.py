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
  * The Notes cell states the additive-vs-breaking verdict from
    release_classification (see release-notes.txt + diff-core.txt).
  * The "Current tracked version" line and the `last-updated:` header date are
    advanced to the new version / today, which the cross-file consistency gate
    also requires.

Idempotency: if a row for NEW_VERSION already exists, the script exits without
modifying the file.
"""
import datetime as dt
import os
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from release_classification import classify, safe_members  # noqa: E402

old = os.environ.get('OLD_VERSION', 'unknown')
new = os.environ.get('NEW_VERSION', 'unknown')

matrix_path = 'docs/compatibility-matrix.md'
with open(matrix_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Idempotency: a row matching ` **<NEW>** ` already exists → no-op.
if re.search(rf'^\| \*\*{re.escape(new)}\*\* \|', content, re.MULTILINE):
    print(f"compatibility-matrix.md already has a row for MAF {new}; no-op.")
    sys.exit(0)


def read_sibling(name: str) -> str:
    p = Path(name)
    return p.read_text(encoding='utf-8') if p.is_file() else ''


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
    if verdict['additive']:
        added, _ = safe_members(verdict['added'])
        if added:
            shown = ', '.join(f'`{m}`' for m in added[:8])
            more = '' if len(added) <= 8 else f' (+{len(added) - 8} more)'
            tail = f" New members: {shown}{more}."
        else:
            tail = ''
        return (
            f"Additive release — no `.NET … [BREAKING]` changes and no removed members "
            f"(source-compatible).{tail} Transitive pins carried from {old}."
        )
    removed, _ = safe_members(verdict['removed'])
    rem = (' Removed: ' + ', '.join(f'`{m}`' for m in removed[:8]) + '.') if removed else ''
    return (
        f"**Breaking** — review required.{rem} See `guides/maf-{new}-migration-guide.md` "
        f"and the registry entries. Transitive pins carried from {old} (verify)."
    )


verdict = classify(read_sibling('release-notes.txt'), read_sibling('diff-core.txt'))

# Carry the transitive pins forward from the current top row; fall back to
# conservative defaults if the table can't be parsed.
cells = top_row_cells(content)
if cells:
    meai, dotnet, azure = cells[1], cells[2], cells[3]
else:
    meai, dotnet, azure = '`≥ 8.0`', '`≥ 8.0`', '_(not pinned by MAF — BYO via IChatClient)_'

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

# Advance the "Current tracked version" line (the cross-file gate checks this).
content, n_tracked = re.subn(
    r'(Current tracked version:\s*\*\*`?)\d+\.\d+(?:\.\d+)?(`?\*\*)',
    rf'\g<1>{new}\g<2>',
    content,
)

# Advance the `last-updated:` header date to today.
today = dt.date.today().isoformat()
content, n_date = re.subn(
    r'(last-updated:\s*)\d{4}-\d{2}-\d{2}',
    rf'\g<1>{today}',
    content,
)

with open(matrix_path, 'w', encoding='utf-8') as f:
    f.write(content)

print(
    f"Updated {matrix_path}: added {'additive' if verdict['additive'] else 'breaking'} "
    f"row for MAF {new}; tracked-version line updated x{n_tracked}; date updated x{n_date}."
)
