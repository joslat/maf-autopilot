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
import re
import sys
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any

COMMENT_ONLY_MODE = True
ENTRIES_FILE = Path(".ai-review-entries.json")

# REP-10: hidden marker so the review is a single UPDATED sticky comment per PR,
# not a fresh review on every push.
MARKER = "<!-- maf-ai-semantic-review -->"

# SEC-19: the model response is untrusted output. Strip HTML comments and neutralize
# triple-backtick runs before embedding it in the comment so a crafted response can't
# smuggle markdown / escape a fence.
_HTML_COMMENT_RE = re.compile(r"<!--.*?-->", re.DOTALL)
_SAFE_ID = re.compile(r"^[A-Za-z0-9._-]{1,64}$")


def md_safe(s: Any) -> str:
    s = _HTML_COMMENT_RE.sub("", str(s))
    return s.replace("```", "`​`​`")


def safe_id_str(s: Any) -> str:
    s = str(s)
    return s if _SAFE_ID.fullmatch(s) else "unknown-entry"


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
            + md_safe(raw[:3500])
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
        eid_safe = safe_id_str(eid)  # SEC-19: eid heading comes from attacker-influenced source
        if not e:
            lines.append(f"### :grey_question: `{eid_safe}` — not reviewed (missing from model output)")
            lines.append("")
            continue
        v = e.get("verdict", "?")
        ve = {"PASS": ":white_check_mark:", "WARN": ":warning:", "FAIL": ":x:"}.get(v, ":grey_question:")
        lines.append(f"### {ve} `{eid_safe}` — {v}")
        findings = e.get("findings", [])
        if findings:
            for f in findings:
                # SEC-19: neutralize per-finding model text and collapse newlines.
                lines.append(f"- {md_safe(f).replace(chr(10), ' ').replace(chr(13), ' ')}")
        else:
            lines.append("- _No issues found._")
        lines.append("")

    return "\n".join(lines), overall


def _api(url: str, token: str, method: str = "GET", data: dict | None = None) -> Any:
    payload = json.dumps(data).encode("utf-8") if data is not None else None
    req = urllib.request.Request(
        url,
        data=payload,
        headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/vnd.github+json",
            "Content-Type": "application/json",
            "X-GitHub-Api-Version": "2022-11-28",
        },
        method=method,
    )
    with urllib.request.urlopen(req, timeout=30) as resp:
        raw = resp.read()
    return json.loads(raw) if raw else None


def upsert_sticky_comment(owner: str, repo: str, pr: int, body: str, token: str) -> None:
    """REP-10: post ONE live sticky comment per PR — find the existing marked comment
    and PATCH it in place, else POST a new one. Replaces posting a brand-new PR review
    on every push (which accumulated a stack of stale verdicts). Needs only issues:write."""
    existing_id = None
    page = 1
    while True:
        listing = _api(
            f"https://api.github.com/repos/{owner}/{repo}/issues/{pr}/comments?per_page=100&page={page}",
            token,
        ) or []
        for c in listing:
            if MARKER in (c.get("body") or ""):
                existing_id = c.get("id")  # take the latest match
        if len(listing) < 100:
            break
        page += 1

    if existing_id is not None:
        _api(f"https://api.github.com/repos/{owner}/{repo}/issues/comments/{existing_id}",
             token, method="PATCH", data={"body": body})
    else:
        _api(f"https://api.github.com/repos/{owner}/{repo}/issues/{pr}/comments",
             token, method="POST", data={"body": body})


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

    head_sha = os.environ.get("HEAD_SHA", "").strip() or os.environ.get("GITHUB_SHA", "").strip()

    parsed = parse_response(raw)
    body, overall = render_body(parsed, raw, ids)
    # REP-10: prepend the hidden marker (so the upsert finds THIS comment) + the reviewed
    # head SHA (so a reader knows which push the verdict covers).
    body = f"{MARKER}\n_Reviewed head: `{safe_id_str(head_sha) if head_sha else 'unknown'}`_\n\n{body}"

    print("--- Review body (preview, first 800 chars) ---")
    print(body[:800])
    print("--- end preview ---")
    print(f"Overall verdict: {overall}")

    try:
        upsert_sticky_comment(owner, repo, int(pr), body, token)
        print(f"Upserted sticky review comment on PR #{pr}.")
    except Exception as exc:
        print(f"ERROR posting review: {exc!r}", file=sys.stderr)
        return 0 if COMMENT_ONLY_MODE else 1

    if not COMMENT_ONLY_MODE and overall == "FAIL":
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
