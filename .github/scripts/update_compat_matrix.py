#!/usr/bin/env python3
"""Adds a new row to docs/compatibility-matrix.md for a new MAF version.

Reads OLD_VERSION, NEW_VERSION from environment variables.
Called by .github/workflows/maf-release-watcher.yml.

Idempotency: if a row for NEW_VERSION already exists, the script exits without
modifying the file. Otherwise the new row is inserted at the top of the data
section — i.e. above whatever the current first version row is — so the matrix
stays ordered newest-first regardless of which version was previously latest.
"""
import os
import re
import sys

old = os.environ.get('OLD_VERSION', 'unknown')
new = os.environ.get('NEW_VERSION', 'unknown')

matrix_path = 'docs/compatibility-matrix.md'
with open(matrix_path, 'r', encoding='utf-8') as f:
    content = f.read()

new_row = (
    f"| **{new}** | `>= unknown` | `>= 8.0` | `>= unknown` | `{new}` |"
    " Auto-detected — verify versions in PR review. |"
)

# Idempotency: a row matching ` **<NEW>** ` already exists → no-op.
if re.search(rf'^\| \*\*{re.escape(new)}\*\* \|', content, re.MULTILINE):
    print(f"compatibility-matrix.md already has a row for MAF {new}; no-op.")
    sys.exit(0)

# Find the FIRST data row in the matrix (any line matching `| **X.Y.Z** |`)
# and insert the new row before it. This makes the script ordering-agnostic —
# it no longer hard-codes `1.3.0` as the anchor.
first_row_pattern = r'^(\| \*\*\d+\.\d+\.\d+[^|]*\*\* \|)'
match = re.search(first_row_pattern, content, re.MULTILINE)
if match:
    insertion_point = match.start()
    updated = content[:insertion_point] + new_row + '\n' + content[insertion_point:]
else:
    # No data rows yet — append at the end.
    updated = content.rstrip() + '\n' + new_row + '\n'

with open(matrix_path, 'w', encoding='utf-8') as f:
    f.write(updated)

print(f"Updated {matrix_path} with new row for MAF {new}.")
