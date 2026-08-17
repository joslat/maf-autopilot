#!/usr/bin/env python3
"""
Render the AI-fill issue body from a clean markdown template.

Reads:
- Env: TARGET (e.g. "1.14.0")
- File: .github/scripts/ai_fill_issue_prompt.md.tpl

Writes substituted prompt body to stdout. The body is the full structured
prompt that goes into the GitHub issue assigned to Copilot / Claude / Codex
Coding Agent.

Substitutes {{TARGET}}, {{ID_SEGMENT}}, and {{BRANCH}} placeholders. The
registry ID segment follows the repository convention of concatenating only
major + minor (for example, 1.14.0 -> 114). Nothing else is touched.

Why this exists: previously the issue body was a ~180-line bash heredoc inside
maf-ai-fill-todos.yml with every backtick escaped (\\`). That's the same
class of failure that caused the release-watcher's backtick-as-command-
substitution bug. A Python template substitution is safe by construction.
"""
import os
import re
import sys
from pathlib import Path


_SEMVER = re.compile(
    r"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)"
    r"(?:-[A-Za-z0-9.-]+)?$"
)


def registry_id_segment(target: str) -> str:
    """Return the shared major+minor registry ID segment for a MAF version."""
    match = _SEMVER.fullmatch(target)
    if match is None:
        raise ValueError("TARGET must be a semantic version (for example, 1.14.0)")
    return f"{match.group(1)}{match.group(2)}"


def main() -> int:
    target = os.environ.get("TARGET", "").strip()
    # WM-28: the scaffold branch that carries the TODO placeholders (post the 2026-06-28
    # "C" refactor the watcher PRs instead of pushing to main). Defaults to the watcher's
    # canonical branch name when the caller doesn't override it.
    branch = os.environ.get("BRANCH", "").strip() or (f"release-watcher/maf-{target}" if target else "")

    if not target:
        print("ERROR: TARGET env var is required (e.g. 1.14.0)", file=sys.stderr)
        return 1
    try:
        id_segment = registry_id_segment(target)
    except ValueError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1

    template_path = Path(__file__).parent / "ai_fill_issue_prompt.md.tpl"
    if not template_path.is_file():
        print(f"ERROR: template not found at {template_path}", file=sys.stderr)
        return 1

    body = template_path.read_text(encoding="utf-8")
    body = (body.replace("{{TARGET}}", target)
                .replace("{{ID_SEGMENT}}", id_segment)
                .replace("{{BRANCH}}", branch))

    if "{{" in body:
        idx = body.index("{{")
        print(
            f"ERROR: unresolved placeholder near offset {idx}: "
            f"{body[idx : idx + 60]!r}",
            file=sys.stderr,
        )
        return 1

    sys.stdout.buffer.write(body.encode("utf-8"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
