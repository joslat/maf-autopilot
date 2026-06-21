"""De-duplicate freshly extracted registry entries against the existing registry.

Usage: dedupe_registry_entries.py <registry.yaml> <new-entries.yaml>

Prints, to stdout, only the entry blocks from <new-entries.yaml> whose `id:` is
NOT already present in <registry.yaml> (and not duplicated within the batch).
Used by the release watcher so re-runs — or the daily cron re-detecting the same
version before .maf-version catches up — don't append duplicate draft entries.
"""
from __future__ import annotations

import re
import sys

_ID = re.compile(r"^\s*-\s*id:\s*(\S+)")


def existing_ids(registry_text: str) -> set[str]:
    return {
        m.group(1).strip().strip("\"'")
        for line in registry_text.splitlines()
        if (m := _ID.match(line))
    }


def split_blocks(entries_text: str) -> list[tuple[str, str]]:
    """Split extractor output into (id, block-text) pairs.

    A block starts at a line matching `- id:` and runs until the next such line.
    Anything before the first `- id:` (comments / blanks) is dropped.
    """
    blocks: list[tuple[str, str]] = []
    cur: list[str] = []
    cur_id: str | None = None
    for line in entries_text.splitlines(keepends=True):
        m = _ID.match(line)
        if m:
            if cur and cur_id is not None:
                blocks.append((cur_id, "".join(cur)))
            cur = [line]
            cur_id = m.group(1).strip().strip("\"'")
        elif cur:
            cur.append(line)
    if cur and cur_id is not None:
        blocks.append((cur_id, "".join(cur)))
    return blocks


def dedupe(registry_text: str, entries_text: str) -> str:
    have = existing_ids(registry_text)
    out: list[str] = []
    for eid, block in split_blocks(entries_text):
        if eid in have:
            print(f"skip duplicate id: {eid}", file=sys.stderr)
            continue
        have.add(eid)  # also de-dupe within this batch
        out.append(block.rstrip("\n"))
    return ("\n".join(out) + "\n") if out else ""


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        print("usage: dedupe_registry_entries.py <registry.yaml> <new-entries.yaml>", file=sys.stderr)
        return 2
    with open(argv[1], encoding="utf-8") as f:
        registry = f.read()
    with open(argv[2], encoding="utf-8") as f:
        entries = f.read()
    sys.stdout.write(dedupe(registry, entries))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
