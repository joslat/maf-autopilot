#!/usr/bin/env python3
"""
Build the review prompt for the AI semantic-review step.

Reads:
- Env: BASE_SHA, HEAD_SHA
- Git: registry.yaml at both refs

Writes:
- stdout: the model prompt (markdown text)
- file:   .ai-review-entries.json — the list of entry IDs being reviewed
          (used by the post-comment step to title the review)

Side-effect-free w.r.t. the network. The actual model call happens in a
separate workflow step (actions/ai-inference).
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
import textwrap
from pathlib import Path
from typing import Any

import yaml

# Phase 2.7 — fence PR-controlled registry entries before embedding in the
# model prompt. A malicious PR contributor can put prompt-injection payloads
# in `notes:` / `example_before` / `example_after` fields; without fencing
# those flow verbatim into the AI semantic-review model prompt. Today's
# COMMENT_ONLY_MODE makes this advisory, but locking the fence in BEFORE a
# future rung-3 (hard-gate) promotion eliminates the lane proactively.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from llm_fencing import fence  # noqa: E402

REGISTRY_PATH = ".github/skills/maf-obsolete-api-registry/registry.yaml"
ENTRIES_OUT = Path(".ai-review-entries.json")

SYSTEM_INSTRUCTIONS = textwrap.dedent("""
You are reviewing entries in a Microsoft Agent Framework (MAF) breaking-change
registry. A Coding Agent just filled in human-readable fields based on a raw
`dotnet-inspect` diff embedded in each entry's `notes:` field. Your job is to
catch semantic contradictions between the diff and the fills.

For each entry, evaluate these checks:

C1. example_before authenticity: does it actually use the obsolete API mentioned
    in obsolete_signature / notes? If it uses a type the diff says is NEW
    (replacement-side), it's wrong.
C2. example_after authenticity: does it use the replacement API? It must NOT
    reference any identifier the diff says was removed.
C3. non-noop: example_before must materially differ from example_after.
C4. fix_description matches the actual change: prose can't contradict notes
    (e.g. claims rename when notes show signature change).
C5. category is correct: TYPE-REMOVED / METHOD-RENAMED / SIGNATURE-CHANGED /
    BEHAVIOR-CHANGED / ATTRIBUTE-REMOVED inferred from cs_warning + diff.
C6. guide_section: must be a real section ID or "N/A" (not "TBD" / "TODO" /
    invented).
C7. code parses: balanced {}, (), valid statement terminators.

Verdict per entry:
- FAIL if any of C1-C5 fails (a real semantic contradiction).
- WARN if C6 or C7 fails, or fix_description is generic / unspecific.
- PASS otherwise.

OUTPUT FORMAT — return a single JSON object, no markdown fences, no prose:

{
  "entries": [
    {
      "id": "MAFXXX-YYY-NNN",
      "verdict": "PASS" | "WARN" | "FAIL",
      "findings": ["short bullet 1", "short bullet 2"]
    }
  ]
}

Each "findings" entry should be one sentence, naming the failing check (e.g.
"C2: example_after references `WorkflowEvaluationExtensions` which the diff
says was removed").

If an entry passes all checks, use "verdict": "PASS" and "findings": [].
""").strip()


def run(cmd: list[str]) -> str:
    return subprocess.check_output(cmd, text=True, encoding="utf-8")


def load_at_ref(ref: str) -> dict[str, Any]:
    try:
        raw = run(["git", "show", f"{ref}:{REGISTRY_PATH}"])
    except subprocess.CalledProcessError:
        return {}
    return yaml.safe_load(raw) or {}


def find_changed(base_sha: str, head_sha: str) -> list[dict[str, Any]]:
    base = {e["id"]: e for e in load_at_ref(base_sha).get("entries", [])}
    head = {e["id"]: e for e in load_at_ref(head_sha).get("entries", [])}
    changed = []
    for eid, entry in head.items():
        if eid not in base:
            changed.append(entry)
        elif json.dumps(entry, sort_keys=True) != json.dumps(base[eid], sort_keys=True):
            changed.append(entry)
    return changed


def main() -> int:
    base = os.environ.get("BASE_SHA", "").strip()
    head = os.environ.get("HEAD_SHA", "").strip()
    if not base or not head:
        print("ERROR: BASE_SHA and HEAD_SHA env vars required", file=sys.stderr)
        return 1

    entries = find_changed(base, head)
    ENTRIES_OUT.write_text(
        json.dumps([e["id"] for e in entries]),
        encoding="utf-8",
    )
    print(f"# Found {len(entries)} changed entries: "
          f"{[e['id'] for e in entries]}", file=sys.stderr)

    if not entries:
        # Emit an empty prompt — workflow step will skip the AI call.
        return 0

    parts = [SYSTEM_INSTRUCTIONS, "", "---", "", "Here are the entries to review:", ""]
    for e in entries:
        # Phase 2.7 — fence each entry. The fence carries explicit "treat as
        # data" framing so the reviewing model knows the YAML is candidate
        # content, not its own instructions. 16 KB cap per entry is generous
        # (real entries are ~1-2 KB) but bounded enough that 50 hostile
        # entries can't flood the context window.
        entry_id = e.get("id", "unknown-entry")
        yaml_dump = yaml.safe_dump(e, sort_keys=False, allow_unicode=True).rstrip()
        parts.append(fence(f"registry-entry-{entry_id}", yaml_dump, max_bytes=16 * 1024))
        parts.append("")

    sys.stdout.buffer.write("\n".join(parts).encode("utf-8"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
