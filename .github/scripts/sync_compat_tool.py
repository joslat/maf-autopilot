#!/usr/bin/env python3
"""Keeps src/maf-autopilot/Tools/CompatibilityTool.cs in lockstep with the
compatibility-matrix doc.

`CompatibilityToolTests.Matrix_StaticTable_CoversEveryRowInCompatibilityMatrixDoc`
fails the build whenever the doc has a version row with no matching static entry
in `CompatibilityTool.Matrix`. Before green-on-arrival the watcher only updated
the doc, so every release scaffold was red on that drift test until a human
hand-added the code row. This script adds the code row deterministically from
the row update_compat_matrix.py just wrote, so the scaffold is green.

Reads NEW_VERSION (and OLD_VERSION, for the carry-forward note) from the
environment. Run AFTER update_compat_matrix.py. Idempotent: no-op if the entry
already exists.
"""
import os
import re
import sys
from pathlib import Path

new = os.environ.get('NEW_VERSION', 'unknown')
old = os.environ.get('OLD_VERSION', 'unknown')

TOOL = Path('src/maf-autopilot/Tools/CompatibilityTool.cs')
MATRIX = Path('docs/compatibility-matrix.md')


def _neutralize(cell: str) -> str:
    """A cell must never break out of the C# raw-string literal it's written
    into. Insert a zero-width space inside any triple-quote run (mirrors
    RegistryService.NeutralizeFences) and drop CR/LF so a cell stays one line.
    Defense-in-depth: build_notes already restricts embedded upstream content to
    identifier-allowlisted member names, but the matrix file could also be
    hand-edited, so we never trust the cell verbatim."""
    return (cell or '').replace('"""', '"​"​"').replace('\r', ' ').replace('\n', ' ')


def matrix_row_cells(version: str):
    """Parse the `| **version** | … |` row from the matrix doc into cells."""
    text = MATRIX.read_text(encoding='utf-8')
    for line in text.splitlines():
        if re.match(rf'^\|\s*\*\*{re.escape(version)}\*\*\s*\|', line):
            parts = [c.strip() for c in line.split('|')[1:-1]]
            if len(parts) >= 6:
                return parts
    return None


def main() -> int:
    if not TOOL.is_file():
        print(f"{TOOL} not found; skipping code-matrix sync.", file=sys.stderr)
        return 0
    source = TOOL.read_text(encoding='utf-8')

    if re.search(rf'\["{re.escape(new)}"\]\s*=', source):
        print(f"CompatibilityTool.Matrix already has an entry for {new}; no-op.")
        return 0

    cells = matrix_row_cells(new)
    if not cells:
        print(f"No matrix row found for {new}; cannot sync code matrix.", file=sys.stderr)
        return 1
    _version, meai, dotnet, azure, generators, notes = (_neutralize(c) for c in cells[:6])

    # 16-space indentation matches the existing entries; the closing `"""` sits
    # at the same indent so the C# raw-string literal strips it uniformly.
    i = ' ' * 16
    block = (
        f'            ["{new}"] = """\n'
        f'{i}## MAF {new} Compatibility\n'
        f'{i}\n'
        f'{i}| Dependency                                | Version          | Notes |\n'
        f'{i}|-------------------------------------------|------------------|-------|\n'
        f'{i}| .NET runtime                              | {dotnet} | net8.0, net9.0, net10.0 TFMs all supported |\n'
        f'{i}| Microsoft.Extensions.AI                   | {meai} | Carried from {old} |\n'
        f'{i}| Azure.AI.OpenAI                           | {azure} | |\n'
        f'{i}| Microsoft.Agents.AI.Workflows.Generators  | `{generators}` | Source-gen package |\n'
        f'{i}| Identity                                  | `ManagedIdentityCredential` | NEVER `DefaultAzureCredential` in prod (analyzer rule MAF002) |\n'
        f'{i}\n'
        f'{i}{notes}\n'
        f'{i}""",\n\n'
    )

    # Insert before the FIRST `            ["…"] = """` entry (newest-first, like
    # the doc table). The SortedDictionary re-sorts at runtime, so ordering in
    # source is cosmetic — but keeping newest-first matches the doc.
    anchor = re.search(r'\n( {12}\["\d)', source)
    if not anchor:
        print("Could not find the Matrix initializer anchor; aborting.", file=sys.stderr)
        return 1
    insert_at = anchor.start() + 1  # after the '\n'
    source = source[:insert_at] + block + source[insert_at:]

    TOOL.write_text(source, encoding='utf-8')
    print(f"Synced CompatibilityTool.Matrix with a new entry for {new}.")
    return 0


if __name__ == '__main__':
    sys.exit(main())
