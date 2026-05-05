#!/usr/bin/env python3
"""Adds a new row to docs/compatibility-matrix.md for a new MAF version.

Reads OLD_VERSION, NEW_VERSION from environment variables.
Called by .github/workflows/maf-release-watcher.yml
"""
import os
import re

old = os.environ.get('OLD_VERSION', 'unknown')
new = os.environ.get('NEW_VERSION', 'unknown')

with open('docs/compatibility-matrix.md', 'r', encoding='utf-8') as f:
    content = f.read()

new_row = (
    f"| **{new}** | `>= unknown` | `>= 8.0` | `>= unknown` | `{new}` |"
    " Auto-detected — verify versions in PR review. |"
)

# Insert before the current latest row (1.3.0) so newest is at top
table_pattern = r'(\| \*\*1\.3\.0\*\* \|)'
if re.search(table_pattern, content):
    updated = re.sub(table_pattern, new_row + '\n' + r'\1', content, count=1)
else:
    updated = content + '\n' + new_row + '\n'

with open('docs/compatibility-matrix.md', 'w', encoding='utf-8') as f:
    f.write(updated)

print(f"Updated compatibility-matrix.md with row for MAF {new}")
