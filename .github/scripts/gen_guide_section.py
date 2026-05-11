#!/usr/bin/env python3
"""Creates a per-version migration-guide stub at guides/maf-<NEW_VERSION>-migration-guide.md.

Reads OLD_VERSION, NEW_VERSION from environment variables.
Called by .github/workflows/maf-release-watcher.yml.

Per-version output (not append to 1.3.0!) means a 1.4.0 release produces
guides/maf-1.4.0-migration-guide.md instead of polluting the 1.3.0 doc.

Idempotency: if the per-version file already exists, the script overwrites the
auto-generated stub but PRESERVES any content under the `## Human additions`
heading. If the file existed but had no marker, we exit with status 0 (no-op).
"""
import os
import re
import sys
from pathlib import Path

old = os.environ.get('OLD_VERSION', 'unknown')
new = os.environ.get('NEW_VERSION', 'unknown')


def read_file(path: str, fallback: str) -> str:
    try:
        with open(path, encoding='utf-8') as f:
            return f.read()
    except Exception:
        return fallback


diff_lines = read_file('diff-core.txt', '(diff not available)').splitlines()
# Trim to a reasonable length — full diffs can be huge.
diff = '\n'.join(diff_lines[:120])
notes = read_file('release-notes.txt', '(no release notes available)').strip()

AUTO_START = '<!-- AUTO-GENERATED START — anything between AUTO-GENERATED START and AUTO-GENERATED END is overwritten on re-run -->'
AUTO_END = '<!-- AUTO-GENERATED END -->'
HUMAN_HEADING = '## Human additions'

auto_body = (
    f"# MAF {new} Migration Guide (draft)\n\n"
    f"<!-- introduced: {new} | applies-to: {old}.x → {new}.x | deprecated-in: none -->\n\n"
    f"{AUTO_START}\n\n"
    f"> ⚠️ Auto-generated stub. Review before relying on it for migrations.\n\n"
    f"## Versions\n\n"
    f"- Migrating from: `{old}`\n"
    f"- Migrating to: `{new}`\n\n"
    f"## Diff Summary (first 120 lines of `dotnet-inspect` output)\n\n"
    f"```\n{diff}\n```\n\n"
    f"## Release Notes Extract\n\n"
    f"{notes}\n\n"
    f"## Breaking Changes (requires human verification)\n\n"
    f"<!-- TODO: Review the diff above and list breaking changes here -->\n\n"
    f"## New Patterns\n\n"
    f"<!-- TODO: Document any new recommended patterns from release notes -->\n\n"
    f"## Obsolete APIs Added\n\n"
    f"<!-- TODO: Use MafRunCs0618Hunt against a project pinned to {new} and document findings -->\n\n"
    f"## Known Misalignments\n\n"
    f"<!-- TODO: Document any discrepancies between official docs and assembly behavior -->\n\n"
    f"{AUTO_END}\n\n"
    f"{HUMAN_HEADING}\n\n"
    f"<!-- Add notes, corrections, and refinements below this heading.\n"
    f"     Content under this heading is PRESERVED across re-runs of the watcher. -->\n"
)

guide_path = Path(f'guides/maf-{new}-migration-guide.md')
guide_path.parent.mkdir(parents=True, exist_ok=True)

if guide_path.exists():
    existing = guide_path.read_text(encoding='utf-8')
    # Extract everything from the human-additions heading onward and preserve it.
    match = re.search(rf'^{re.escape(HUMAN_HEADING)}.*', existing, re.MULTILINE | re.DOTALL)
    human_section = match.group(0) if match else f"{HUMAN_HEADING}\n\n"
    # Re-emit the auto block followed by the preserved human section.
    auto_only = auto_body.split(HUMAN_HEADING)[0]
    new_content = auto_only + human_section
else:
    new_content = auto_body

guide_path.write_text(new_content, encoding='utf-8')
print(f"Wrote {guide_path} (idempotent — human additions preserved if present).")
