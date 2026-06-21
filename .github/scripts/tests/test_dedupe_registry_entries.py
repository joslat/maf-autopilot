"""Tests for dedupe_registry_entries.py (release-watcher idempotent append)."""
from __future__ import annotations

import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPT_DIR))

from dedupe_registry_entries import dedupe, existing_ids, split_blocks  # noqa: E402

REGISTRY = """\
entries:
  - id: MAF100-foo
    package: Microsoft.Agents.AI
  - id: MAF101-bar
    package: Microsoft.Agents.AI
"""

NEW_ENTRIES = """\
# === Draft entries auto-appended ===
  - id: MAF100-foo
    package: Microsoft.Agents.AI
    note: duplicate of an existing entry
  - id: MAF200-baz
    package: Microsoft.Agents.AI.Workflows
    note: genuinely new
"""


def test_existing_ids_parsed():
    assert existing_ids(REGISTRY) == {"MAF100-foo", "MAF101-bar"}


def test_split_blocks_groups_by_id():
    blocks = split_blocks(NEW_ENTRIES)
    assert [b[0] for b in blocks] == ["MAF100-foo", "MAF200-baz"]
    # The leading comment line is dropped (before the first `- id:`).
    assert "Draft entries auto-appended" not in blocks[0][1]


def test_dedupe_drops_existing_keeps_new():
    out = dedupe(REGISTRY, NEW_ENTRIES)
    assert "MAF200-baz" in out
    assert "MAF100-foo" not in out
    assert "genuinely new" in out


def test_dedupe_within_batch():
    dupe_batch = NEW_ENTRIES + "  - id: MAF200-baz\n    package: dup-in-batch\n"
    out = dedupe(REGISTRY, dupe_batch)
    assert out.count("MAF200-baz") == 1


def test_all_duplicates_returns_empty():
    only_dupes = "  - id: MAF100-foo\n    package: x\n  - id: MAF101-bar\n    package: y\n"
    assert dedupe(REGISTRY, only_dupes) == ""
