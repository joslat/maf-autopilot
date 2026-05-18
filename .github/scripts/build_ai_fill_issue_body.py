#!/usr/bin/env python3
"""
Render the AI-fill issue body from a clean markdown template.

Reads:
- Env: TARGET (e.g. "1.6.1"), DIGITS (e.g. "161")
- File: .github/scripts/ai_fill_issue_prompt.md.tpl

Writes substituted prompt body to stdout. The body is the full structured
prompt that goes into the GitHub issue assigned to Copilot / Claude / Codex
Coding Agent.

Substitutes {{TARGET}} and {{DIGITS}} placeholders. Nothing else is touched.

Why this exists: previously the issue body was a ~180-line bash heredoc inside
maf-ai-fill-todos.yml with every backtick escaped (\\`). That's the same
class of failure that caused the release-watcher's backtick-as-command-
substitution bug. A Python template substitution is safe by construction.
"""
import os
import sys
from pathlib import Path


def main() -> int:
    target = os.environ.get("TARGET", "").strip()
    digits = os.environ.get("DIGITS", "").strip()

    if not target:
        print("ERROR: TARGET env var is required (e.g. 1.6.1)", file=sys.stderr)
        return 1
    if not digits:
        print("ERROR: DIGITS env var is required (e.g. 161)", file=sys.stderr)
        return 1

    template_path = Path(__file__).parent / "ai_fill_issue_prompt.md.tpl"
    if not template_path.is_file():
        print(f"ERROR: template not found at {template_path}", file=sys.stderr)
        return 1

    body = template_path.read_text(encoding="utf-8")
    body = body.replace("{{TARGET}}", target).replace("{{DIGITS}}", digits)

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
