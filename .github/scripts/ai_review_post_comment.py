#!/usr/bin/env python3
"""
Parse the AI semantic-review response and post a single review comment on
the PR.

Reads:
- Env: PR_NUMBER, GH_TOKEN, GITHUB_REPOSITORY, AI_RESPONSE
- File: .ai-review-entries.json (list of IDs from the build-prompt step)

Posts a PR review with event=COMMENT. Always exits 0 in COMMENT-ONLY mode;
flip COMMENT_ONLY_MODE to False below to make the workflow fail when the
model returns FAIL verdicts (rung-3 — gate mode).
"""
from __future__ import annotations

import json
import os
import sys
import urllib.request
from pathlib import Path
from typing import Any

COMMENT_ONLY_MODE = True
ENTRIES_FILE = Path(".ai-review-entries.json")


def parse_response(raw: str) -> dict[str, Any] | None:
    """Parse the model output as JSON. Handles markdown fences, leading prose."""
    s = raw.strip()
    if "```" in s:
        # Strip any markdown code fence wrapping.
        parts = s.split("```")
        if len(parts) >= 3:
            inner = parts[1]
            if inner.startswith("json\n"):
                inner = inner[5:]
            elif inner.startswith("json"):
                inner = inner[4:].lstrip()
            s = inner.strip()
    # Some models prepend prose before the JSON; find the first '{'.
    if not s.startswith("{"):
        idx = s.find("{")
        if idx >= 0:
            s = s[idx:]
    try:
        return json.loads(s)
    except json.JSONDecodeError:
        return None


def render_body(parsed: dict[str, Any] | None, raw: str, ids: list[str]) -> tuple[str, str]:
    """Render the markdown review body + overall verdict."""
    if parsed is None or "entries" not in parsed:
        body = (
            "## :warning: AI Semantic Review — verdict: **WARN**\n\n"
            "The model returned output that didn't parse as the expected JSON "
            "shape. Falling back to raw output below.\n\n"
            "_This is comment-only mode; the human still makes the merge call._\n\n"
            "---\n\n"
            "```\n"
            + raw[:3500]
            + ("\n... (truncated)" if len(raw) > 3500 else "")
            + "\n```"
        )
        return body, "WARN"

    entries = parsed.get("entries", [])
    pass_n = sum(1 for e in entries if e.get("verdict") == "PASS")
    warn_n = sum(1 for e in entries if e.get("verdict") == "WARN")
    fail_n = sum(1 for e in entries if e.get("verdict") == "FAIL")

    if fail_n:
        overall, emoji = "FAIL", ":x:"
        header = ("**One or more entries contradict the dotnet-inspect diff** "
                  "preserved in their `notes:` field. See per-entry findings.")
    elif warn_n:
        overall, emoji = "WARN", ":warning:"
        header = ("Entries are structurally consistent with the diff, but some "
                  "have minor smells (generic phrasing, missing section IDs, "
                  "etc.). Review the findings below.")
    else:
        overall, emoji = "PASS", ":white_check_mark:"
        header = ("All filled entries are semantically consistent with the "
                  "`dotnet-inspect` diff captured in their `notes:` field.")

    lines = [
        f"## {emoji} AI Semantic Review — verdict: **{overall}**",
        "",
        f"Reviewed **{len(entries)}** changed registry entr"
        f"{'y' if len(entries) == 1 else 'ies'}: "
        f"`PASS={pass_n}`, `WARN={warn_n}`, `FAIL={fail_n}`.",
        "",
        header,
        "",
        "_This is rung-2 of the AI trust ladder (comment-only). The human "
        "still merges. To turn this into a hard gate, flip "
        "`COMMENT_ONLY_MODE` in `.github/scripts/ai_review_post_comment.py`._",
        "",
        "---",
        "",
    ]

    by_id = {e.get("id"): e for e in entries}
    for eid in ids:
        e = by_id.get(eid)
        if not e:
            lines.append(f"### :grey_question: `{eid}` — not reviewed (missing from model output)")
            lines.append("")
            continue
        v = e.get("verdict", "?")
        ve = {"PASS": ":white_check_mark:", "WARN": ":warning:", "FAIL": ":x:"}.get(v, ":grey_question:")
        lines.append(f"### {ve} `{eid}` — {v}")
        findings = e.get("findings", [])
        if findings:
            for f in findings:
                lines.append(f"- {f}")
        else:
            lines.append("- _No issues found._")
        lines.append("")

    return "\n".join(lines), overall


def post_review(owner: str, repo: str, pr: int, body: str, token: str) -> None:
    url = f"https://api.github.com/repos/{owner}/{repo}/pulls/{pr}/reviews"
    req = urllib.request.Request(
        url,
        data=json.dumps({"body": body, "event": "COMMENT"}).encode("utf-8"),
        headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/vnd.github+json",
            "Content-Type": "application/json",
            "X-GitHub-Api-Version": "2022-11-28",
        },
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=30) as resp:
        if resp.status >= 300:
            raise RuntimeError(f"Posting review failed: HTTP {resp.status}")


def main() -> int:
    raw = os.environ.get("AI_RESPONSE", "")
    pr = os.environ.get("PR_NUMBER", "").strip()
    token = os.environ.get("GH_TOKEN", "").strip()
    repo_full = os.environ.get("GITHUB_REPOSITORY", "").strip()

    missing = [n for n, v in [
        ("AI_RESPONSE", raw),
        ("PR_NUMBER", pr),
        ("GH_TOKEN", token),
        ("GITHUB_REPOSITORY", repo_full),
    ] if not v]
    if missing:
        print(f"ERROR: missing env vars: {', '.join(missing)}", file=sys.stderr)
        return 1
    owner, _, repo = repo_full.partition("/")

    ids: list[str] = []
    if ENTRIES_FILE.is_file():
        try:
            ids = json.loads(ENTRIES_FILE.read_text(encoding="utf-8"))
        except Exception:
            ids = []

    parsed = parse_response(raw)
    body, overall = render_body(parsed, raw, ids)

    print("--- Review body (preview, first 800 chars) ---")
    print(body[:800])
    print("--- end preview ---")
    print(f"Overall verdict: {overall}")

    try:
        post_review(owner, repo, int(pr), body, token)
        print(f"Posted review to PR #{pr}.")
    except Exception as exc:
        print(f"ERROR posting review: {exc!r}", file=sys.stderr)
        return 0 if COMMENT_ONLY_MODE else 1

    if not COMMENT_ONLY_MODE and overall == "FAIL":
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
