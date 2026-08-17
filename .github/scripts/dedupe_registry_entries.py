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
_IDENTITY_FIELD = re.compile(r"^\s+(package|type|method):\s*(.*?)\s*$")


def existing_ids(registry_text: str) -> set[str]:
    return {
        m.group(1).strip().strip("\"'")
        for line in registry_text.splitlines()
        if (m := _ID.match(line))
    }


def split_blocks(entries_text: str) -> list[tuple[str, str]]:
    """Split extractor output into (id, block-text) pairs.

    A block starts at a line matching `- id:` and includes its more-indented
    child lines. The next entry or top-level extractor output (such as the next
    package's "no entries" message) ends it. Anything outside an entry is dropped.
    """
    blocks: list[tuple[str, str]] = []
    cur: list[str] = []
    cur_id: str | None = None
    cur_indent = 0
    for line in entries_text.splitlines(keepends=True):
        m = _ID.match(line)
        if m:
            if cur and cur_id is not None:
                blocks.append((cur_id, "".join(cur)))
            cur = [line]
            cur_id = m.group(1).strip().strip("\"'")
            cur_indent = len(line) - len(line.lstrip(" \t"))
        elif cur and cur_id is not None:
            line_indent = len(line) - len(line.lstrip(" \t"))
            if line.strip() and line_indent <= cur_indent:
                blocks.append((cur_id, "".join(cur)))
                cur = []
                cur_id = None
            else:
                cur.append(line)
    if cur and cur_id is not None:
        blocks.append((cur_id, "".join(cur)))
    return blocks


def block_identity(block: str) -> tuple[str, str, str]:
    """Return the package/type/method identity carried by one YAML entry."""
    fields = {"package": "", "type": "", "method": ""}
    lines = block.splitlines()
    entry_indent = len(lines[0]) - len(lines[0].lstrip(" \t")) if lines else 0
    for line in lines:
        line_indent = len(line) - len(line.lstrip(" \t"))
        if line_indent == entry_indent + 2 and (match := _IDENTITY_FIELD.match(line)):
            fields[match.group(1)] = match.group(2).strip().strip("\"'")
    return fields["package"], fields["type"], fields["method"]


def existing_entry_identities(
    registry_text: str,
) -> dict[str, tuple[str, tuple[str, str, str]]]:
    """Index existing IDs case-insensitively with their semantic identity."""
    entries: dict[str, tuple[str, tuple[str, str, str]]] = {}
    for eid, block in split_blocks(registry_text):
        key = eid.casefold()
        identity = block_identity(block)
        if key in entries and entries[key][1] != identity:
            first_id, first_identity = entries[key]
            raise ValueError(
                f"id collision in existing registry: {eid} identifies {identity!r}, "
                f"but {first_id} already identifies {first_identity!r}")
        entries.setdefault(key, (eid, identity))
    return entries


def dedupe(registry_text: str, entries_text: str) -> str:
    existing = existing_entry_identities(registry_text)
    batch: dict[str, tuple[str, tuple[str, str, str], str]] = {}
    out: list[str] = []
    for eid, block in split_blocks(entries_text):
        key = eid.casefold()
        identity = block_identity(block)
        normalized_block = block.rstrip()
        if key in batch:
            first_id, first_identity, first_block = batch[key]
            if identity != first_identity:
                raise ValueError(
                    f"id collision in extraction batch: {eid} identifies {identity!r}, "
                    f"but {first_id} already identifies {first_identity!r}")
            if normalized_block != first_block:
                raise ValueError(
                    f"non-identical duplicate in extraction batch: {eid} has the same "
                    "package/type/method identity but different entry content")
            print(f"skip identical batch duplicate id: {eid}", file=sys.stderr)
            continue

        batch[key] = (eid, identity, normalized_block)
        if key in existing:
            existing_id, existing_identity = existing[key]
            if identity != existing_identity:
                raise ValueError(
                    f"id collision with existing registry: {eid} identifies {identity!r}, "
                    f"but {existing_id} already identifies {existing_identity!r}")
            print(f"skip duplicate id: {eid}", file=sys.stderr)
            continue
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
    try:
        output = dedupe(registry, entries)
    except ValueError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1
    sys.stdout.write(output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
